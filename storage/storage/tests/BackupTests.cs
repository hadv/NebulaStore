using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Transactions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style backup and restore system.
/// </summary>
public class BackupTests : IDisposable
{
    private readonly string _storageDirectory;
    private readonly string _backupDirectory;

    public BackupTests()
    {
        var testId = Guid.NewGuid().ToString("N");
        _storageDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_Storage_{testId}");
        _backupDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_Backup_{testId}");

        Directory.CreateDirectory(_storageDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    [Fact]
    public async Task BackupManager_CreateFullBackup_ShouldBackupAllFiles()
    {
        // Arrange
        var backupManager = new BackupManager(_storageDirectory, _backupDirectory);

        // Create test files
        var dataFile = Path.Combine(_storageDirectory, "channel_000_data_0000000001.dat");
        var logFile = Path.Combine(_storageDirectory, "channel_000_transactions.log");
        await File.WriteAllBytesAsync(dataFile, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(logFile, new byte[] { 4, 5, 6 });

        // Act
        var result = await backupManager.CreateFullBackupAsync();

        // Assert
        Assert.Equal(BackupStatus.Completed, result.Status);
        Assert.Equal(BackupType.Full, result.BackupType);
        Assert.Equal(2, result.BackedUpFiles.Count);
        Assert.True(Directory.Exists(result.BackupPath));
        Assert.True(File.Exists(Path.Combine(result.BackupPath, "channel_000_data_0000000001.dat")));
        Assert.True(File.Exists(Path.Combine(result.BackupPath, "channel_000_transactions.log")));
        Assert.True(File.Exists(Path.Combine(result.BackupPath, "backup_metadata.json")));
    }

    [Fact]
    public async Task BackupManager_CreateIncrementalBackup_ShouldBackupModifiedFiles()
    {
        // Arrange
        var backupManager = new BackupManager(_storageDirectory, _backupDirectory);

        // Create initial files
        var dataFile = Path.Combine(_storageDirectory, "channel_000_data_0000000001.dat");
        var logFile = Path.Combine(_storageDirectory, "channel_000_transactions.log");
        await File.WriteAllBytesAsync(dataFile, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(logFile, new byte[] { 4, 5, 6 });

        var lastBackupTime = DateTime.UtcNow.AddMinutes(-1);

        // Modify one file after the backup time
        await Task.Delay(100); // Ensure file timestamp is after lastBackupTime
        await File.WriteAllBytesAsync(dataFile, new byte[] { 1, 2, 3, 4 });

        // Act
        var result = await backupManager.CreateIncrementalBackupAsync(lastBackupTime);

        // Assert
        Assert.Equal(BackupStatus.Completed, result.Status);
        Assert.Equal(BackupType.Incremental, result.BackupType);
        Assert.True(result.BackedUpFiles.Count >= 1); // At least the modified file
        Assert.True(Directory.Exists(result.BackupPath));
    }

    [Fact]
    public async Task BackupManager_RestoreFromBackup_ShouldRestoreFiles()
    {
        // Arrange
        var backupManager = new BackupManager(_storageDirectory, _backupDirectory);

        // Create test files and backup
        var dataFile = Path.Combine(_storageDirectory, "channel_000_data_0000000001.dat");
        var originalData = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(dataFile, originalData);

        var backupResult = await backupManager.CreateFullBackupAsync();
        Assert.Equal(BackupStatus.Completed, backupResult.Status);

        // Modify the original file
        await File.WriteAllBytesAsync(dataFile, new byte[] { 9, 8, 7 });

        // Act
        var restoreResult = await backupManager.RestoreFromBackupAsync(backupResult.BackupPath);

        // Assert
        Assert.Equal(RestoreStatus.Completed, restoreResult.Status);
        Assert.True(restoreResult.RestoredFiles.Count > 0);
        Assert.True(File.Exists(dataFile));

        // Verify file content was restored
        var restoredData = await File.ReadAllBytesAsync(dataFile);
        Assert.Equal(originalData, restoredData);
    }

    [Fact]
    public async Task BackupManager_ListAvailableBackups_ShouldReturnBackupList()
    {
        // Arrange
        var backupManager = new BackupManager(_storageDirectory, _backupDirectory);

        // Create test files
        var dataFile = Path.Combine(_storageDirectory, "channel_000_data_0000000001.dat");
        await File.WriteAllBytesAsync(dataFile, new byte[] { 1, 2, 3 });

        // Create multiple backups
        await backupManager.CreateFullBackupAsync();
        await Task.Delay(1000); // Ensure different timestamps
        await backupManager.CreateFullBackupAsync();

        // Act
        var backups = await backupManager.ListAvailableBackupsAsync();

        // Assert
        Assert.True(backups.Count >= 2);
        Assert.All(backups, backup => Assert.Equal(BackupType.Full, backup.BackupType));
        Assert.All(backups, backup => Assert.True(backup.FileCount > 0));
    }

    [Fact]
    public async Task BackupManager_EmptyStorage_ShouldCreateEmptyBackup()
    {
        // Arrange
        var backupManager = new BackupManager(_storageDirectory, _backupDirectory);
        // No files in storage

        // Act
        var result = await backupManager.CreateFullBackupAsync();

        // Assert
        Assert.Equal(BackupStatus.Completed, result.Status);
        Assert.Equal(0, result.BackedUpFiles.Count);
        Assert.True(Directory.Exists(result.BackupPath));
        Assert.True(File.Exists(Path.Combine(result.BackupPath, "backup_metadata.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, true);
        }

        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, true);
        }
    }
}