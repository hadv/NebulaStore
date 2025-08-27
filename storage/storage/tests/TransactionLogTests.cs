using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Transactions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style transaction logging system.
/// </summary>
public class TransactionLogTests : IDisposable
{
    private readonly string _testDirectory;

    public TransactionLogTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_TransactionLog_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void TransactionLogManager_BeginCommitTransaction_ShouldCreateLogEntry()
    {
        // Arrange
        using var logManager = new TransactionLogManager(0, _testDirectory);

        // Act
        var transactionId = logManager.BeginTransaction();
        logManager.LogStoreOperation(transactionId, 1, 0, 100, new List<long> { 1001, 1002 });
        var operationCount = logManager.CommitTransaction(transactionId);

        // Assert
        Assert.True(transactionId > 0);
        Assert.Equal(1, operationCount);
    }

    [Fact]
    public void TransactionLogReader_ReadCommittedTransactions_ShouldReturnCommittedOnly()
    {
        // Arrange
        using var logManager = new TransactionLogManager(0, _testDirectory);

        // Create committed transaction
        var committedTxId = logManager.BeginTransaction();
        logManager.LogStoreOperation(committedTxId, 1, 0, 100, new List<long> { 1001 });
        logManager.CommitTransaction(committedTxId);

        // Create uncommitted transaction
        var uncommittedTxId = logManager.BeginTransaction();
        logManager.LogStoreOperation(uncommittedTxId, 2, 0, 200, new List<long> { 2001 });
        // Don't commit this one

        logManager.Dispose();

        // Act
        var logFilePath = Path.Combine(_testDirectory, "channel_000_transactions.log");
        using var reader = new TransactionLogReader(logFilePath);
        var committedTransactions = reader.GetCommittedTransactions();

        // Assert
        Assert.Single(committedTransactions);
        Assert.True(committedTransactions.ContainsKey(committedTxId));
        Assert.False(committedTransactions.ContainsKey(uncommittedTxId));
    }

    [Fact]
    public void TransactionLogEntry_Serialize_ShouldRoundTripCorrectly()
    {
        // Arrange
        var objectIds = new List<long> { 1001, 1002, 1003 };
        var entry = new StoreTransactionLogEntry(
            transactionId: 123,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            channelIndex: 0,
            sequenceNumber: 1,
            dataFileNumber: 5,
            offset: 1024,
            length: 512,
            objectIds: objectIds);

        // Act
        var serialized = entry.Serialize();

        // Parse it back (simplified parsing for test)
        var entryType = (TransactionLogEntryType)serialized[0];
        var transactionId = BitConverter.ToInt64(serialized, 1);

        // Assert
        Assert.Equal(TransactionLogEntryType.Store, entryType);
        Assert.Equal(123, transactionId);
        Assert.True(serialized.Length > 0);
    }

    [Fact]
    public void TransactionLogManager_RollbackTransaction_ShouldRemoveFromActive()
    {
        // Arrange
        using var logManager = new TransactionLogManager(0, _testDirectory);

        // Act
        var transactionId = logManager.BeginTransaction();
        Assert.Equal(1, logManager.ActiveTransactionCount);

        logManager.RollbackTransaction(transactionId);

        // Assert
        Assert.Equal(0, logManager.ActiveTransactionCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}