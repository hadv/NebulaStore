using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Transactions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style crash recovery system.
/// </summary>
public class CrashRecoveryTests : IDisposable
{
    private readonly string _testDirectory;

    public CrashRecoveryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_CrashRecovery_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void CrashRecoveryManager_NoLogFiles_ShouldReturnNoRecoveryNeeded()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Act
        var result = recoveryManager.PerformRecovery();

        // Assert
        Assert.Equal(RecoveryStatus.NoRecoveryNeeded, result.Status);
        Assert.Equal(0, result.LogFilesFound);
        Assert.Contains("No transaction log files found", result.Message);
    }

    [Fact]
    public void CrashRecoveryManager_WithCommittedTransactions_ShouldReturnConsistentState()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Create a transaction log with committed transactions
        using (var logManager = new TransactionLogManager(0, _testDirectory))
        {
            var txId = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId, 1, 0, 100, new List<long> { 1001 });
            logManager.CommitTransaction(txId);
        }

        // Create the corresponding data file
        var dataFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        File.WriteAllBytes(dataFilePath, new byte[100]);

        // Act
        var result = recoveryManager.PerformRecovery();

        // Assert
        Assert.Equal(RecoveryStatus.ConsistentState, result.Status);
        Assert.Equal(1, result.LogFilesFound);
        Assert.Equal(1, result.CommittedTransactions);
        Assert.Equal(0, result.UncommittedTransactions);
    }

    [Fact]
    public void CrashRecoveryManager_WithUncommittedTransactions_ShouldPerformRecovery()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Create a transaction log with uncommitted transactions
        using (var logManager = new TransactionLogManager(0, _testDirectory))
        {
            var txId = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId, 1, 0, 100, new List<long> { 1001 });
            // Don't commit - simulate crash
        }

        // Act
        var result = recoveryManager.PerformRecovery();

        // Assert
        Assert.Equal(RecoveryStatus.RecoveryPerformed, result.Status);
        Assert.Equal(1, result.LogFilesFound);
        Assert.Equal(0, result.CommittedTransactions);
        Assert.Equal(1, result.UncommittedTransactions);
        Assert.True(result.ActionsPerformed.Count > 0);
    }

    [Fact]
    public void CrashRecoveryManager_WithMissingDataFile_ShouldDetectInconsistency()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Create a transaction log with committed transactions but no data file
        using (var logManager = new TransactionLogManager(0, _testDirectory))
        {
            var txId = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId, 1, 0, 100, new List<long> { 1001 });
            logManager.CommitTransaction(txId);
        }

        // Don't create the data file - simulate missing file

        // Act
        var result = recoveryManager.PerformRecovery();

        // Assert
        Assert.Equal(RecoveryStatus.RecoveryPerformed, result.Status);
        Assert.Equal(1, result.InconsistentFiles);
        Assert.True(result.ActionsPerformed.Count > 0);
    }

    [Fact]
    public void CrashRecoveryManager_IsRecoveryNeeded_ShouldDetectLogFiles()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Initially no recovery needed
        Assert.False(recoveryManager.IsRecoveryNeeded());

        // Create a transaction log
        using (var logManager = new TransactionLogManager(0, _testDirectory))
        {
            var txId = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId, 1, 0, 100, new List<long> { 1001 });
        }

        // Act & Assert
        Assert.True(recoveryManager.IsRecoveryNeeded());
    }

    [Fact]
    public void CrashRecoveryManager_GetLastCommittedTransactionId_ShouldReturnCorrectId()
    {
        // Arrange
        var recoveryManager = new CrashRecoveryManager(_testDirectory);

        // Create multiple transactions
        using (var logManager = new TransactionLogManager(0, _testDirectory))
        {
            // First transaction
            var txId1 = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId1, 1, 0, 100, new List<long> { 1001 });
            logManager.CommitTransaction(txId1);

            // Second transaction (uncommitted)
            var txId2 = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId2, 1, 100, 50, new List<long> { 1002 });
            // Don't commit

            // Third transaction
            var txId3 = logManager.BeginTransaction();
            logManager.LogStoreOperation(txId3, 1, 150, 75, new List<long> { 1003 });
            logManager.CommitTransaction(txId3);
        }

        // Act
        var lastCommittedId = recoveryManager.GetLastCommittedTransactionId();

        // Assert
        Assert.True(lastCommittedId > 0);
        // Should be the ID of the third transaction (highest committed)
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}