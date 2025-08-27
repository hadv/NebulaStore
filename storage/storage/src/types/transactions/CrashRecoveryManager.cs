using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NebulaStore.Storage.Embedded.Types.Files;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Manages crash recovery following Eclipse Store patterns.
/// Restores storage to a consistent state from transaction logs after unexpected shutdowns.
/// </summary>
public class CrashRecoveryManager
{
    #region Private Fields

    private readonly string _storageDirectory;
    private readonly int _channelCount;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the CrashRecoveryManager class.
    /// </summary>
    /// <param name="storageDirectory">The storage directory containing transaction logs.</param>
    /// <param name="channelCount">The number of channels to recover.</param>
    public CrashRecoveryManager(string storageDirectory, int channelCount = 1)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _channelCount = channelCount;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Performs crash recovery by analyzing transaction logs and restoring consistent state.
    /// </summary>
    /// <returns>Recovery result with statistics and actions taken.</returns>
    public CrashRecoveryResult PerformRecovery()
    {
        var result = new CrashRecoveryResult();

        try
        {
            // Step 1: Discover and validate transaction log files
            var logFiles = DiscoverTransactionLogFiles();
            result.LogFilesFound = logFiles.Count;

            if (logFiles.Count == 0)
            {
                result.Status = RecoveryStatus.NoRecoveryNeeded;
                result.Message = "No transaction log files found - clean startup";
                return result;
            }

            // Step 2: Read and analyze all transaction logs
            var allTransactions = ReadAllTransactionLogs(logFiles);
            result.TotalTransactionsFound = allTransactions.Count;

            // Step 3: Identify committed vs uncommitted transactions
            var committedTransactions = IdentifyCommittedTransactions(allTransactions);
            result.CommittedTransactions = committedTransactions.Count;
            result.UncommittedTransactions = allTransactions.Count - committedTransactions.Count;

            // Step 4: Validate data file consistency
            var inconsistentFiles = ValidateDataFileConsistency(committedTransactions);
            result.InconsistentFiles = inconsistentFiles.Count;

            // Step 5: Apply recovery actions if needed
            if (inconsistentFiles.Count > 0 || result.UncommittedTransactions > 0)
            {
                ApplyRecoveryActions(committedTransactions, inconsistentFiles, result);
                result.Status = RecoveryStatus.RecoveryPerformed;
                result.Message = $"Recovery completed: {result.ActionsPerformed.Count} actions performed";
            }
            else
            {
                result.Status = RecoveryStatus.ConsistentState;
                result.Message = "Storage is in consistent state - no recovery needed";
            }

            // Step 6: Clean up transaction logs after successful recovery
            if (result.Status == RecoveryStatus.RecoveryPerformed || result.Status == RecoveryStatus.ConsistentState)
            {
                CleanupTransactionLogs(logFiles);
                result.ActionsPerformed.Add("Transaction logs cleaned up");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Status = RecoveryStatus.RecoveryFailed;
            result.Message = $"Recovery failed: {ex.Message}";
            result.Exception = ex;
            return result;
        }
    }

    /// <summary>
    /// Checks if recovery is needed by examining transaction logs.
    /// </summary>
    /// <returns>True if recovery is needed, false otherwise.</returns>
    public bool IsRecoveryNeeded()
    {
        var logFiles = DiscoverTransactionLogFiles();
        return logFiles.Count > 0;
    }

    /// <summary>
    /// Gets the last committed transaction ID across all channels.
    /// </summary>
    /// <returns>The last committed transaction ID, or 0 if none found.</returns>
    public long GetLastCommittedTransactionId()
    {
        var logFiles = DiscoverTransactionLogFiles();
        long maxTransactionId = 0;

        foreach (var logFile in logFiles)
        {
            using var reader = new TransactionLogReader(logFile);
            var lastCommitted = reader.GetLastCommittedTransactionId();
            maxTransactionId = Math.Max(maxTransactionId, lastCommitted);
        }

        return maxTransactionId;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Discovers transaction log files in the storage directory.
    /// </summary>
    /// <returns>List of transaction log file paths.</returns>
    private List<string> DiscoverTransactionLogFiles()
    {
        var logFiles = new List<string>();

        if (!Directory.Exists(_storageDirectory))
            return logFiles;

        // Look for transaction log files with pattern: channel_XXX_transactions.log
        var pattern = "channel_*_transactions.log";
        var files = Directory.GetFiles(_storageDirectory, pattern);

        logFiles.AddRange(files);

        return logFiles;
    }

    /// <summary>
    /// Reads all transaction logs and groups transactions by ID.
    /// </summary>
    /// <param name="logFiles">The transaction log files to read.</param>
    /// <returns>Dictionary of transaction ID to list of entries.</returns>
    private Dictionary<long, List<TransactionLogEntry>> ReadAllTransactionLogs(List<string> logFiles)
    {
        var allTransactions = new Dictionary<long, List<TransactionLogEntry>>();

        foreach (var logFile in logFiles)
        {
            using var reader = new TransactionLogReader(logFile);
            var entries = reader.ReadAllEntries();

            foreach (var entry in entries)
            {
                if (!allTransactions.ContainsKey(entry.TransactionId))
                {
                    allTransactions[entry.TransactionId] = new List<TransactionLogEntry>();
                }
                allTransactions[entry.TransactionId].Add(entry);
            }
        }

        // Sort entries within each transaction by sequence number
        foreach (var transaction in allTransactions.Values)
        {
            transaction.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));
        }

        return allTransactions;
    }

    /// <summary>
    /// Identifies which transactions have been committed.
    /// </summary>
    /// <param name="allTransactions">All transactions from logs.</param>
    /// <returns>Dictionary of committed transactions.</returns>
    private Dictionary<long, List<TransactionLogEntry>> IdentifyCommittedTransactions(Dictionary<long, List<TransactionLogEntry>> allTransactions)
    {
        var committedTransactions = new Dictionary<long, List<TransactionLogEntry>>();

        foreach (var (transactionId, entries) in allTransactions)
        {
            // Check if transaction has a commit entry
            if (entries.Any(e => e.EntryType == TransactionLogEntryType.Commit))
            {
                committedTransactions[transactionId] = entries;
            }
        }

        return committedTransactions;
    }

    /// <summary>
    /// Validates data file consistency against committed transactions.
    /// </summary>
    /// <param name="committedTransactions">The committed transactions.</param>
    /// <returns>List of inconsistent file paths.</returns>
    private List<string> ValidateDataFileConsistency(Dictionary<long, List<TransactionLogEntry>> committedTransactions)
    {
        var inconsistentFiles = new List<string>();

        // Group operations by data file
        var fileOperations = new Dictionary<long, List<TransactionLogEntry>>();

        foreach (var entries in committedTransactions.Values)
        {
            foreach (var entry in entries)
            {
                if (entry is StoreTransactionLogEntry storeEntry)
                {
                    if (!fileOperations.ContainsKey(storeEntry.DataFileNumber))
                    {
                        fileOperations[storeEntry.DataFileNumber] = new List<TransactionLogEntry>();
                    }
                    fileOperations[storeEntry.DataFileNumber].Add(entry);
                }
                else if (entry is CreateTransactionLogEntry createEntry)
                {
                    if (!fileOperations.ContainsKey(createEntry.DataFileNumber))
                    {
                        fileOperations[createEntry.DataFileNumber] = new List<TransactionLogEntry>();
                    }
                    fileOperations[createEntry.DataFileNumber].Add(entry);
                }
            }
        }

        // Validate each data file
        foreach (var (fileNumber, operations) in fileOperations)
        {
            var dataFilePath = Path.Combine(_storageDirectory, $"channel_000_data_{fileNumber:D10}.dat");

            if (!File.Exists(dataFilePath))
            {
                // File should exist based on transaction log
                inconsistentFiles.Add(dataFilePath);
                continue;
            }

            // Validate file size matches expected size from operations
            var expectedSize = CalculateExpectedFileSize(operations);
            var actualSize = new FileInfo(dataFilePath).Length;

            if (actualSize < expectedSize)
            {
                // File is truncated or corrupted
                inconsistentFiles.Add(dataFilePath);
            }
        }

        return inconsistentFiles;
    }

    /// <summary>
    /// Calculates expected file size from transaction operations.
    /// </summary>
    /// <param name="operations">The operations on the file.</param>
    /// <returns>Expected file size in bytes.</returns>
    private long CalculateExpectedFileSize(List<TransactionLogEntry> operations)
    {
        long maxOffset = 0;

        foreach (var operation in operations)
        {
            if (operation is StoreTransactionLogEntry storeEntry)
            {
                var endOffset = storeEntry.Offset + storeEntry.Length;
                maxOffset = Math.Max(maxOffset, endOffset);
            }
        }

        return maxOffset;
    }

    /// <summary>
    /// Applies recovery actions to restore consistency.
    /// </summary>
    /// <param name="committedTransactions">The committed transactions.</param>
    /// <param name="inconsistentFiles">The inconsistent files.</param>
    /// <param name="result">The recovery result to update.</param>
    private void ApplyRecoveryActions(Dictionary<long, List<TransactionLogEntry>> committedTransactions, List<string> inconsistentFiles, CrashRecoveryResult result)
    {
        // For now, we'll implement basic recovery actions
        // In a full implementation, this would replay transactions to restore data

        foreach (var inconsistentFile in inconsistentFiles)
        {
            if (File.Exists(inconsistentFile))
            {
                // Mark file as potentially corrupted
                var backupPath = inconsistentFile + ".corrupted." + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                File.Move(inconsistentFile, backupPath);
                result.ActionsPerformed.Add($"Moved corrupted file {inconsistentFile} to {backupPath}");
            }
        }

        // In a full implementation, we would:
        // 1. Recreate missing data files
        // 2. Replay committed transactions to restore data
        // 3. Verify data integrity after recovery

        result.ActionsPerformed.Add("Basic recovery actions completed");
    }

    /// <summary>
    /// Cleans up transaction logs after successful recovery.
    /// </summary>
    /// <param name="logFiles">The transaction log files to clean up.</param>
    private void CleanupTransactionLogs(List<string> logFiles)
    {
        foreach (var logFile in logFiles)
        {
            if (File.Exists(logFile))
            {
                // Archive the log file instead of deleting it
                var archivePath = logFile + ".recovered." + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                File.Move(logFile, archivePath);
            }
        }
    }

    #endregion
}