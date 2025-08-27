using System;
using System.IO;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Manages lock files to prevent multiple storage instances from accessing the same directory following Eclipse Store patterns.
/// Provides exclusive access control and automatic cleanup of stale locks.
/// </summary>
public class LockFileManager : IDisposable
{
    #region Private Fields

    private readonly string _storageDirectory;
    private readonly string _lockFilePath;
    private readonly int _processId;
    private readonly string _instanceId;
    private FileStream? _lockFileStream;
    private bool _disposed;
    private readonly object _lock = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the LockFileManager class.
    /// </summary>
    /// <param name="storageDirectory">The storage directory to lock.</param>
    public LockFileManager(string storageDirectory)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _lockFilePath = Path.Combine(_storageDirectory, ".nebulastore.lock");
        _processId = Environment.ProcessId;
        _instanceId = Guid.NewGuid().ToString("N")[..8]; // Short instance ID

        // Ensure storage directory exists
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets a value indicating whether this instance holds the lock.
    /// </summary>
    public bool IsLocked => _lockFileStream != null && !_disposed;

    /// <summary>
    /// Gets the storage directory being locked.
    /// </summary>
    public string StorageDirectory => _storageDirectory;

    /// <summary>
    /// Gets the lock file path.
    /// </summary>
    public string LockFilePath => _lockFilePath;

    #endregion

    #region Public Methods

    /// <summary>
    /// Attempts to acquire the lock following Eclipse Store patterns.
    /// </summary>
    /// <returns>The lock result indicating success or failure.</returns>
    public LockResult AcquireLock()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (IsLocked)
            {
                return new LockResult
                {
                    Success = true,
                    Message = "Lock already held by this instance",
                    LockInfo = CreateCurrentLockInfo()
                };
            }

            try
            {
                // Check if lock file exists and is valid
                if (File.Exists(_lockFilePath))
                {
                    var existingLock = ReadLockInfo();
                    if (existingLock != null && IsLockValid(existingLock))
                    {
                        return new LockResult
                        {
                            Success = false,
                            Message = $"Storage is locked by another instance (PID: {existingLock.ProcessId}, Instance: {existingLock.InstanceId})",
                            LockInfo = existingLock
                        };
                    }

                    // Lock file exists but is stale - remove it
                    if (existingLock != null)
                    {
                        try
                        {
                            File.Delete(_lockFilePath);
                        }
                        catch
                        {
                            // If we can't delete it, it might be held by another process
                            return new LockResult
                            {
                                Success = false,
                                Message = "Cannot remove stale lock file - may be held by another process",
                                LockInfo = existingLock
                            };
                        }
                    }
                }

                // Create new lock file
                try
                {
                    _lockFileStream = new FileStream(_lockFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    var lockInfo = CreateCurrentLockInfo();
                    WriteLockInfo(lockInfo);

                    return new LockResult
                    {
                        Success = true,
                        Message = "Lock acquired successfully",
                        LockInfo = lockInfo
                    };
                }
                catch (IOException ex) when (ex.Message.Contains("process cannot access"))
                {
                    // File is locked by another process
                    var existingLock = ReadLockInfo();
                    return new LockResult
                    {
                        Success = false,
                        Message = existingLock != null
                            ? $"Storage is locked by another instance (PID: {existingLock.ProcessId}, Instance: {existingLock.InstanceId})"
                            : "Storage is locked by another process",
                        LockInfo = existingLock
                    };
                }
            }
            catch (Exception ex)
            {
                return new LockResult
                {
                    Success = false,
                    Message = $"Failed to acquire lock: {ex.Message}",
                    Exception = ex
                };
            }
        }
    }

    /// <summary>
    /// Releases the lock following Eclipse Store patterns.
    /// </summary>
    /// <returns>True if the lock was released successfully, false otherwise.</returns>
    public bool ReleaseLock()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!IsLocked)
                return true; // Already released

            try
            {
                _lockFileStream?.Dispose();
                _lockFileStream = null;

                if (File.Exists(_lockFilePath))
                {
                    File.Delete(_lockFilePath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Checks if the storage directory is currently locked by any instance.
    /// </summary>
    /// <returns>The lock status information.</returns>
    public LockStatus CheckLockStatus()
    {
        try
        {
            // If we hold the lock, return that information
            if (IsLocked)
            {
                return new LockStatus
                {
                    IsLocked = true,
                    LockInfo = CreateCurrentLockInfo(),
                    Message = "Storage is locked by this instance"
                };
            }

            if (!File.Exists(_lockFilePath))
            {
                return new LockStatus
                {
                    IsLocked = false,
                    Message = "No lock file found - storage is available"
                };
            }

            var lockInfo = ReadLockInfo();
            if (lockInfo == null)
            {
                return new LockStatus
                {
                    IsLocked = false,
                    Message = "Lock file exists but is invalid - storage may be available"
                };
            }

            var isValid = IsLockValid(lockInfo);
            return new LockStatus
            {
                IsLocked = isValid,
                LockInfo = lockInfo,
                Message = isValid
                    ? $"Storage is locked by PID {lockInfo.ProcessId}, Instance {lockInfo.InstanceId}"
                    : "Lock file exists but process is no longer running - storage may be available"
            };
        }
        catch (Exception ex)
        {
            return new LockStatus
            {
                IsLocked = true, // Assume locked if we can't determine status
                Message = $"Error checking lock status: {ex.Message}",
                Exception = ex
            };
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates lock information for the current instance.
    /// </summary>
    /// <returns>The current lock information.</returns>
    private LockInfo CreateCurrentLockInfo()
    {
        return new LockInfo
        {
            ProcessId = _processId,
            InstanceId = _instanceId,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            CreatedTime = DateTime.UtcNow,
            StorageDirectory = _storageDirectory
        };
    }

    /// <summary>
    /// Writes lock information to the lock file.
    /// </summary>
    /// <param name="lockInfo">The lock information to write.</param>
    private void WriteLockInfo(LockInfo lockInfo)
    {
        if (_lockFileStream == null)
            return;

        var json = System.Text.Json.JsonSerializer.Serialize(lockInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        _lockFileStream.Write(bytes);
        _lockFileStream.Flush();
    }

    /// <summary>
    /// Reads lock information from the lock file.
    /// </summary>
    /// <returns>The lock information, or null if invalid.</returns>
    private LockInfo? ReadLockInfo()
    {
        try
        {
            if (!File.Exists(_lockFilePath))
                return null;

            var json = File.ReadAllText(_lockFilePath);
            return System.Text.Json.JsonSerializer.Deserialize<LockInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a lock is still valid (process is running).
    /// </summary>
    /// <param name="lockInfo">The lock information to validate.</param>
    /// <returns>True if the lock is valid, false otherwise.</returns>
    private bool IsLockValid(LockInfo lockInfo)
    {
        try
        {
            // Check if the process is still running
            var process = System.Diagnostics.Process.GetProcessById(lockInfo.ProcessId);
            return !process.HasExited;
        }
        catch
        {
            // Process not found or access denied - assume lock is stale
            return false;
        }
    }

    /// <summary>
    /// Throws if the manager is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LockFileManager));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the lock file manager and releases any held locks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            ReleaseLock();
            _disposed = true;
        }
    }

    #endregion
}