using System;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Contains the result of a lock acquisition attempt following Eclipse Store patterns.
/// </summary>
public class LockResult
{
    /// <summary>
    /// Gets or sets whether the lock was acquired successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lock information.
    /// </summary>
    public LockInfo? LockInfo { get; set; }

    /// <summary>
    /// Gets or sets the exception if lock acquisition failed.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Contains the status of a storage lock following Eclipse Store patterns.
/// </summary>
public class LockStatus
{
    /// <summary>
    /// Gets or sets whether the storage is currently locked.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lock information if locked.
    /// </summary>
    public LockInfo? LockInfo { get; set; }

    /// <summary>
    /// Gets or sets the exception if status check failed.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Contains information about a storage lock following Eclipse Store patterns.
/// </summary>
public class LockInfo
{
    /// <summary>
    /// Gets or sets the process ID that holds the lock.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the instance ID that holds the lock.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name where the lock was created.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user name that created the lock.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the lock was created.
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the storage directory being locked.
    /// </summary>
    public string StorageDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets a display string for the lock information.
    /// </summary>
    public string DisplayInfo => $"PID {ProcessId} ({InstanceId}) on {MachineName} by {UserName} at {CreatedTime:yyyy-MM-dd HH:mm:ss}";
}