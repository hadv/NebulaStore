using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Represents the type of backup following Eclipse Store patterns.
/// </summary>
public enum BackupType
{
    /// <summary>
    /// Full backup of all storage files.
    /// </summary>
    Full,

    /// <summary>
    /// Incremental backup of changed files only.
    /// </summary>
    Incremental
}

/// <summary>
/// Represents the status of backup operations following Eclipse Store patterns.
/// </summary>
public enum BackupStatus
{
    /// <summary>
    /// Backup completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Backup failed due to an error.
    /// </summary>
    Failed
}

/// <summary>
/// Represents the status of restore operations following Eclipse Store patterns.
/// </summary>
public enum RestoreStatus
{
    /// <summary>
    /// Restore completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Restore failed due to an error.
    /// </summary>
    Failed
}

/// <summary>
/// Contains the results of a backup operation following Eclipse Store patterns.
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Gets or sets the backup type.
    /// </summary>
    public BackupType BackupType { get; set; }

    /// <summary>
    /// Gets or sets the backup status.
    /// </summary>
    public BackupStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the backup path.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time of the backup.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the backup.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of the backup.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the list of files that were backed up.
    /// </summary>
    public List<string> BackedUpFiles { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the exception if backup failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether the backup was successful.
    /// </summary>
    public bool IsSuccessful => Status == BackupStatus.Completed;

    /// <summary>
    /// Gets a summary of the backup operation.
    /// </summary>
    public string Summary => $"Type: {BackupType}, " +
                           $"Status: {Status}, " +
                           $"Files: {BackedUpFiles.Count}, " +
                           $"Duration: {Duration.TotalSeconds:F1}s";
}

/// <summary>
/// Contains the results of a restore operation following Eclipse Store patterns.
/// </summary>
public class RestoreResult
{
    /// <summary>
    /// Gets or sets the restore status.
    /// </summary>
    public RestoreStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the backup path that was restored from.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the safety backup path created before restore.
    /// </summary>
    public string? SafetyBackupPath { get; set; }

    /// <summary>
    /// Gets or sets the start time of the restore.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the restore.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of the restore.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the list of files that were restored.
    /// </summary>
    public List<string> RestoredFiles { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the exception if restore failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether the restore was successful.
    /// </summary>
    public bool IsSuccessful => Status == RestoreStatus.Completed;

    /// <summary>
    /// Gets a summary of the restore operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Files: {RestoredFiles.Count}, " +
                           $"Duration: {Duration.TotalSeconds:F1}s";
}

/// <summary>
/// Contains information about a backup following Eclipse Store patterns.
/// </summary>
public class BackupInfo
{
    /// <summary>
    /// Gets or sets the backup ID.
    /// </summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backup type.
    /// </summary>
    public BackupType BackupType { get; set; }

    /// <summary>
    /// Gets or sets the time when the backup was created.
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the number of files in the backup.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Gets or sets the backup path.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a display name for the backup.
    /// </summary>
    public string DisplayName => $"{BackupType} Backup - {CreatedTime:yyyy-MM-dd HH:mm:ss} ({FileCount} files)";
}