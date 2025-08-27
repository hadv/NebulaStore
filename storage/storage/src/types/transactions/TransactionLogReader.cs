using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Reads and parses transaction log files for crash recovery following Eclipse Store patterns.
/// </summary>
public class TransactionLogReader : IDisposable
{
    #region Private Fields

    private readonly string _logFilePath;
    private FileStream? _logFileStream;
    private BinaryReader? _logReader;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the TransactionLogReader class.
    /// </summary>
    /// <param name="logFilePath">The path to the transaction log file.</param>
    public TransactionLogReader(string logFilePath)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));

        if (File.Exists(_logFilePath))
        {
            _logFileStream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _logReader = new BinaryReader(_logFileStream);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Reads all transaction log entries from the file.
    /// </summary>
    /// <returns>The list of transaction log entries.</returns>
    public List<TransactionLogEntry> ReadAllEntries()
    {
        ThrowIfDisposed();

        var entries = new List<TransactionLogEntry>();

        if (_logReader == null)
            return entries; // No log file exists

        _logFileStream!.Seek(0, SeekOrigin.Begin);

        while (_logFileStream.Position < _logFileStream.Length)
        {
            try
            {
                var entry = ReadNextEntry();
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            catch (EndOfStreamException)
            {
                // Reached end of file
                break;
            }
            catch (Exception)
            {
                // Corrupted entry - stop reading to avoid further corruption
                break;
            }
        }

        return entries;
    }

    /// <summary>
    /// Gets all committed transactions from the log.
    /// </summary>
    /// <returns>Dictionary of transaction ID to list of entries for committed transactions.</returns>
    public Dictionary<long, List<TransactionLogEntry>> GetCommittedTransactions()
    {
        var allEntries = ReadAllEntries();
        var transactionGroups = allEntries.GroupBy(e => e.TransactionId).ToDictionary(g => g.Key, g => g.ToList());
        var committedTransactions = new Dictionary<long, List<TransactionLogEntry>>();

        foreach (var (transactionId, entries) in transactionGroups)
        {
            // Check if transaction has a commit entry
            if (entries.Any(e => e.EntryType == TransactionLogEntryType.Commit))
            {
                committedTransactions[transactionId] = entries.OrderBy(e => e.SequenceNumber).ToList();
            }
        }

        return committedTransactions;
    }

    /// <summary>
    /// Gets the last committed transaction ID.
    /// </summary>
    /// <returns>The last committed transaction ID, or 0 if no transactions are committed.</returns>
    public long GetLastCommittedTransactionId()
    {
        var committedTransactions = GetCommittedTransactions();
        return committedTransactions.Keys.DefaultIfEmpty(0).Max();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Reads the next transaction log entry from the stream.
    /// </summary>
    /// <returns>The transaction log entry, or null if end of stream.</returns>
    private TransactionLogEntry? ReadNextEntry()
    {
        if (_logReader == null)
            return null;

        // Read entry length
        var entryLength = _logReader.ReadInt32();
        if (entryLength <= 0 || entryLength > 1024 * 1024) // Sanity check: max 1MB per entry
        {
            throw new InvalidDataException($"Invalid entry length: {entryLength}");
        }

        // Read entry data
        var entryData = _logReader.ReadBytes(entryLength);
        if (entryData.Length != entryLength)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading entry data");
        }

        // Read and verify checksum
        var expectedChecksum = _logReader.ReadUInt32();
        var actualChecksum = CalculateChecksum(entryData);
        if (actualChecksum != expectedChecksum)
        {
            throw new InvalidDataException($"Checksum mismatch: expected {expectedChecksum}, got {actualChecksum}");
        }

        // Parse entry
        return ParseEntry(entryData);
    }

    /// <summary>
    /// Parses a transaction log entry from raw data.
    /// </summary>
    /// <param name="data">The entry data.</param>
    /// <returns>The parsed transaction log entry.</returns>
    private static TransactionLogEntry? ParseEntry(byte[] data)
    {
        if (data.Length < 1)
            return null;

        var entryType = (TransactionLogEntryType)data[0];
        var offset = 1;

        // Read common fields
        var transactionId = BitConverter.ToInt64(data, offset);
        offset += 8;
        var timestamp = BitConverter.ToInt64(data, offset);
        offset += 8;
        var channelIndex = BitConverter.ToInt32(data, offset);
        offset += 4;
        var sequenceNumber = BitConverter.ToInt64(data, offset);
        offset += 8;

        return entryType switch
        {
            TransactionLogEntryType.Store => ParseStoreEntry(data, offset, transactionId, timestamp, channelIndex, sequenceNumber),
            TransactionLogEntryType.Create => ParseCreateEntry(data, offset, transactionId, timestamp, channelIndex, sequenceNumber),
            TransactionLogEntryType.Commit => ParseCommitEntry(data, offset, transactionId, timestamp, channelIndex, sequenceNumber),
            _ => null // Unknown entry type
        };
    }

    /// <summary>
    /// Parses a store transaction log entry.
    /// </summary>
    private static StoreTransactionLogEntry ParseStoreEntry(byte[] data, int offset, long transactionId, long timestamp, int channelIndex, long sequenceNumber)
    {
        var dataFileNumber = BitConverter.ToInt64(data, offset);
        offset += 8;
        var fileOffset = BitConverter.ToInt64(data, offset);
        offset += 8;
        var length = BitConverter.ToInt64(data, offset);
        offset += 8;

        var objectIdCount = BitConverter.ToInt32(data, offset);
        offset += 4;

        var objectIds = new List<long>();
        for (int i = 0; i < objectIdCount; i++)
        {
            objectIds.Add(BitConverter.ToInt64(data, offset));
            offset += 8;
        }

        return new StoreTransactionLogEntry(transactionId, timestamp, channelIndex, sequenceNumber, dataFileNumber, fileOffset, length, objectIds);
    }

    /// <summary>
    /// Parses a create transaction log entry.
    /// </summary>
    private static CreateTransactionLogEntry ParseCreateEntry(byte[] data, int offset, long transactionId, long timestamp, int channelIndex, long sequenceNumber)
    {
        var dataFileNumber = BitConverter.ToInt64(data, offset);
        offset += 8;

        var pathLength = BitConverter.ToInt32(data, offset);
        offset += 4;

        var filePath = System.Text.Encoding.UTF8.GetString(data, offset, pathLength);

        return new CreateTransactionLogEntry(transactionId, timestamp, channelIndex, sequenceNumber, dataFileNumber, filePath);
    }

    /// <summary>
    /// Parses a commit transaction log entry.
    /// </summary>
    private static CommitTransactionLogEntry ParseCommitEntry(byte[] data, int offset, long transactionId, long timestamp, int channelIndex, long sequenceNumber)
    {
        var operationCount = BitConverter.ToInt32(data, offset);

        return new CommitTransactionLogEntry(transactionId, timestamp, channelIndex, sequenceNumber, operationCount);
    }

    /// <summary>
    /// Calculates a simple checksum for data integrity.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The checksum value.</returns>
    private static uint CalculateChecksum(byte[] data)
    {
        uint checksum = 0;
        foreach (var b in data)
        {
            checksum = (checksum << 1) ^ b;
        }
        return checksum;
    }

    /// <summary>
    /// Throws if the reader is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TransactionLogReader));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the transaction log reader.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logReader?.Dispose();
        _logFileStream?.Dispose();
        _disposed = true;
    }

    #endregion
}