using System;
using System.IO;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Transactions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style lock file management system.
/// </summary>
public class LockFileTests : IDisposable
{
    private readonly string _testDirectory;

    public LockFileTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_LockFile_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void LockFileManager_AcquireLock_ShouldSucceedWhenNoLockExists()
    {
        // Arrange
        using var lockManager = new LockFileManager(_testDirectory);

        // Act
        var result = lockManager.AcquireLock();

        // Assert
        Assert.True(result.Success);
        Assert.True(lockManager.IsLocked);
        Assert.True(File.Exists(lockManager.LockFilePath));
        Assert.NotNull(result.LockInfo);
        Assert.Equal(Environment.ProcessId, result.LockInfo.ProcessId);
    }

    [Fact]
    public void LockFileManager_AcquireLock_ShouldFailWhenLockAlreadyHeld()
    {
        // Arrange
        using var lockManager1 = new LockFileManager(_testDirectory);
        using var lockManager2 = new LockFileManager(_testDirectory);

        var result1 = lockManager1.AcquireLock();
        Assert.True(result1.Success);

        // Act
        var result2 = lockManager2.AcquireLock();

        // Assert
        Assert.False(result2.Success);
        Assert.False(lockManager2.IsLocked);
        Assert.Contains("locked by another", result2.Message);
    }

    [Fact]
    public void LockFileManager_ReleaseLock_ShouldRemoveLockFile()
    {
        // Arrange
        using var lockManager = new LockFileManager(_testDirectory);
        var acquireResult = lockManager.AcquireLock();
        Assert.True(acquireResult.Success);
        Assert.True(File.Exists(lockManager.LockFilePath));

        // Act
        var releaseResult = lockManager.ReleaseLock();

        // Assert
        Assert.True(releaseResult);
        Assert.False(lockManager.IsLocked);
        Assert.False(File.Exists(lockManager.LockFilePath));
    }

    [Fact]
    public void LockFileManager_CheckLockStatus_ShouldReturnCorrectStatus()
    {
        // Arrange
        using var lockManager = new LockFileManager(_testDirectory);

        // Act & Assert - No lock initially
        var status1 = lockManager.CheckLockStatus();
        Assert.False(status1.IsLocked);
        Assert.Contains("No lock file found", status1.Message);

        // Acquire lock
        var acquireResult = lockManager.AcquireLock();
        Assert.True(acquireResult.Success);

        // Act & Assert - Lock exists
        var status2 = lockManager.CheckLockStatus();
        Assert.True(status2.IsLocked);
        Assert.NotNull(status2.LockInfo);
        Assert.Equal(Environment.ProcessId, status2.LockInfo.ProcessId);

        // Release lock
        lockManager.ReleaseLock();

        // Act & Assert - Lock released
        var status3 = lockManager.CheckLockStatus();
        Assert.False(status3.IsLocked);
    }

    [Fact]
    public void LockFileManager_Dispose_ShouldReleaseLock()
    {
        // Arrange
        var lockManager = new LockFileManager(_testDirectory);
        var acquireResult = lockManager.AcquireLock();
        Assert.True(acquireResult.Success);
        Assert.True(File.Exists(lockManager.LockFilePath));

        // Act
        lockManager.Dispose();

        // Assert
        Assert.False(lockManager.IsLocked);
        Assert.False(File.Exists(lockManager.LockFilePath));
    }

    [Fact]
    public void LockFileManager_AcquireLockTwice_ShouldSucceedWithSameInstance()
    {
        // Arrange
        using var lockManager = new LockFileManager(_testDirectory);

        // Act
        var result1 = lockManager.AcquireLock();
        var result2 = lockManager.AcquireLock();

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Contains("already held", result2.Message);
        Assert.True(lockManager.IsLocked);
    }

    [Fact]
    public void LockFileManager_LockInfo_ShouldContainCorrectInformation()
    {
        // Arrange
        using var lockManager = new LockFileManager(_testDirectory);

        // Act
        var result = lockManager.AcquireLock();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.LockInfo);

        var lockInfo = result.LockInfo;
        Assert.Equal(Environment.ProcessId, lockInfo.ProcessId);
        Assert.Equal(Environment.MachineName, lockInfo.MachineName);
        Assert.Equal(Environment.UserName, lockInfo.UserName);
        Assert.Equal(_testDirectory, lockInfo.StorageDirectory);
        Assert.NotEmpty(lockInfo.InstanceId);
        Assert.True(lockInfo.CreatedTime > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void LockFileManager_MultipleInstances_ShouldPreventConcurrentAccess()
    {
        // Arrange
        using var lockManager1 = new LockFileManager(_testDirectory);
        using var lockManager2 = new LockFileManager(_testDirectory);
        using var lockManager3 = new LockFileManager(_testDirectory);

        // Act
        var result1 = lockManager1.AcquireLock();
        var result2 = lockManager2.AcquireLock();
        var result3 = lockManager3.AcquireLock();

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.False(result3.Success);

        // Only first instance should hold the lock
        Assert.True(lockManager1.IsLocked);
        Assert.False(lockManager2.IsLocked);
        Assert.False(lockManager3.IsLocked);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}