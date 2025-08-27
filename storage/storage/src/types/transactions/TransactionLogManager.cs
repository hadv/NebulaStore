using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Types.Files;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Manages transaction logging following Eclipse Store patterns for ACID compliance and crash recovery.
/// Implements append-only transaction log with atomic operations.
/// </summary>
public class TransactionLogManager : IDisposable
{
    #region Private Fields

    private readonly int _channelIndex;
    private readonly string _storageDirectory;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<long, TransactionContext> _activeTransactions = new();

    private long _nextTransactionId = 1;
    private long _nextSequenceNumber = 1;
    private FileStream? _logFileStream;
    private BinaryWriter? _logWriter;
    private string? _currentLogFilePath;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the TransactionLogManager class.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="storageDirectory">The storage directory.</param>
    public TransactionLogManager(int channelIndex, string storageDirectory)
    {
        _channelIndex = channelIndex;
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));

        InitializeLogFile();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    public int ChannelIndex => _channelIndex;

    /// <summary>
    /// Gets the number of active transactions.
    /// </summary>
    public int ActiveTransactionCount => _activeTransactions.Count;

    #endregion

    #region Transaction Management

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>The transaction ID.</returns>
    public long BeginTransaction()
    {
        ThrowIfDisposed();

        var transactionId = Interlocked.Increment(ref _nextTransactionId) - 1;
        var context = new TransactionContext(transactionId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        _activeTransactions.TryAdd(transactionId, context);

        return transactionId;
    }

    /// <summary>
    /// Commits a transaction by writing a commit marker to the log.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    /// <returns>The number of operations committed.</returns>
    public int CommitTransaction(long transactionId)
    {
        ThrowIfDisposed();

        if (!_activeTransactions.TryRemove(transactionId, out var context))
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found or already completed");
        }

        var commitEntry = new CommitTransactionLogEntry(
            transactionId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _channelIndex,
            Interlocked.Increment(ref _nextSequenceNumber) - 1,
            context.OperationCount);

        WriteLogEntry(commitEntry);
        FlushLog();

        return context.OperationCount;
    }

    /// <summary>
    /// Rolls back a transaction.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    public void RollbackTransaction(long transactionId)
    {
        ThrowIfDisposed();

        _activeTransactions.TryRemove(transactionId, out _);

        // In Eclipse Store, rollback is implicit - uncommitted transactions are ignored during recovery
        // We don't need to write rollback entries unless explicitly requested
    }

    #endregion

    #region Log Entry Writing

    /// <summary>
    /// Logs a store operation.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    /// <param name="dataFileNumber">The data file number.</param>
    /// <param name="offset">The offset in the data file.</param>
    /// <param name="length">The length of stored data.</param>
    /// <param name="objectIds">The object IDs that were stored.</param>
    public void LogStoreOperation(long transactionId, long dataFileNumber, long offset, long length, IReadOnlyList<long> objectIds)
    {
        ThrowIfDisposed();

        if (!_activeTransactions.TryGetValue(transactionId, out var context))
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found");
        }

        var storeEntry = new StoreTransactionLogEntry(
            transactionId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _channelIndex,
            Interlocked.Increment(ref _nextSequenceNumber) - 1,
            dataFileNumber,
            offset,
            length,
            objectIds);

        WriteLogEntry(storeEntry);
        context.IncrementOperationCount();
    }

    /// <summary>
    /// Logs a file creation operation.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    /// <param name="dataFileNumber">The data file number.</param>
    /// <param name="filePath">The file path.</param>
    public void LogCreateOperation(long transactionId, long dataFileNumber, string filePath)
    {
        ThrowIfDisposed();

        if (!_activeTransactions.TryGetValue(transactionId, out var context))
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found");
        }

        var createEntry = new CreateTransactionLogEntry(
            transactionId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            _channelIndex,
            Interlocked.Increment(ref _nextSequenceNumber) - 1,
            dataFileNumber,
            filePath);

        WriteLogEntry(createEntry);
        context.IncrementOperationCount();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initializes the transaction log file.
    /// </summary>
    private void InitializeLogFile()
    {
        var logFileName = $"channel_{_channelIndex:D3}_transactions.log";
        _currentLogFilePath = Path.Combine(_storageDirectory, logFileName);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_currentLogFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open log file for append
        _logFileStream = new FileStream(_currentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _logWriter = new BinaryWriter(_logFileStream);
    }

    /// <summary>
    /// Writes a log entry to the transaction log.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    private void WriteLogEntry(TransactionLogEntry entry)
    {
        lock (_lock)
        {
            if (_logWriter == null)
                throw new InvalidOperationException("Transaction log is not initialized");

            var entryData = entry.Serialize();

            // Write entry length first (for recovery parsing)
            _logWriter.Write(entryData.Length);

            // Write entry data
            _logWriter.Write(entryData);

            // Write checksum for integrity verification
            var checksum = CalculateChecksum(entryData);
            _logWriter.Write(checksum);
        }
    }

    /// <summary>
    /// Flushes the transaction log to disk.
    /// </summary>
    private void FlushLog()
    {
        lock (_lock)
        {
            _logWriter?.Flush();
            _logFileStream?.Flush(true); // Force OS to flush to disk
        }
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
    /// Throws if the manager is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TransactionLogManager));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the transaction log manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _logWriter?.Dispose();
            _logFileStream?.Dispose();
            _disposed = true;
        }
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Represents the context of an active transaction.
    /// </summary>
    private class TransactionContext
    {
        private int _operationCount;

        public long TransactionId { get; }
        public long StartTimestamp { get; }
        public int OperationCount => _operationCount;

        public TransactionContext(long transactionId, long startTimestamp)
        {
            TransactionId = transactionId;
            StartTimestamp = startTimestamp;
        }

        public void IncrementOperationCount()
        {
            Interlocked.Increment(ref _operationCount);
        }
    }

    #endregion
}