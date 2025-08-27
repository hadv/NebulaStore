using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Represents the status of data integrity checks following Eclipse Store patterns.
/// </summary>
public enum IntegrityStatus
{
    /// <summary>
    /// All data is intact and valid.
    /// </summary>
    Intact,

    /// <summary>
    /// Some files are corrupted but system is recoverable.
    /// </summary>
    PartiallyCorrupted,

    /// <summary>
    /// Severe corruption detected - manual intervention may be required.
    /// </summary>
    SeverelyCorrupted,

    /// <summary>
    /// Integrity check failed due to an error.
    /// </summary>
    CheckFailed
}

/// <summary>
/// Represents the status of repair operations following Eclipse Store patterns.
/// </summary>
public enum RepairStatus
{
    /// <summary>
    /// All files were successfully repaired.
    /// </summary>
    AllRepaired,

    /// <summary>
    /// Some files were repaired, others remain corrupted.
    /// </summary>
    PartiallyRepaired,

    /// <summary>
    /// Repair operation failed.
    /// </summary>
    RepairFailed
}

/// <summary>
/// Contains the results of a data integrity check following Eclipse Store patterns.
/// </summary>
public class DataIntegrityResult
{
    /// <summary>
    /// Gets or sets the overall integrity status.
    /// </summary>
    public IntegrityStatus Status { get; set; } = IntegrityStatus.Intact;

    /// <summary>
    /// Gets or sets the total number of files checked.
    /// </summary>
    public int TotalFilesChecked { get; set; }

    /// <summary>
    /// Gets the list of corrupted data files.
    /// </summary>
    public List<string> CorruptedFiles { get; } = new List<string>();

    /// <summary>
    /// Gets the list of corrupted log files.
    /// </summary>
    public List<string> CorruptedLogFiles { get; } = new List<string>();

    /// <summary>
    /// Gets the detailed results for each file checked.
    /// </summary>
    public List<FileIntegrityResult> FileResults { get; } = new List<FileIntegrityResult>();

    /// <summary>
    /// Gets the detailed results for each log file checked.
    /// </summary>
    public List<FileIntegrityResult> LogFileResults { get; } = new List<FileIntegrityResult>();

    /// <summary>
    /// Gets or sets the duration of the integrity check.
    /// </summary>
    public TimeSpan CheckDuration { get; set; }

    /// <summary>
    /// Gets or sets the exception if the check failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether the integrity check was successful.
    /// </summary>
    public bool IsSuccessful => Status != IntegrityStatus.CheckFailed;

    /// <summary>
    /// Gets a summary of the integrity check results.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Files Checked: {TotalFilesChecked}, " +
                           $"Corrupted Files: {CorruptedFiles.Count}, " +
                           $"Corrupted Logs: {CorruptedLogFiles.Count}, " +
                           $"Duration: {CheckDuration.TotalMilliseconds:F0}ms";
}

/// <summary>
/// Contains the integrity check result for a single file following Eclipse Store patterns.
/// </summary>
public class FileIntegrityResult
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the file is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the current checksum of the file.
    /// </summary>
    public string? CurrentChecksum { get; set; }

    /// <summary>
    /// Gets or sets the stored checksum of the file.
    /// </summary>
    public string? StoredChecksum { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception if validation failed.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Contains the results of a data repair operation following Eclipse Store patterns.
/// </summary>
public class DataRepairResult
{
    /// <summary>
    /// Gets or sets the repair status.
    /// </summary>
    public RepairStatus Status { get; set; } = RepairStatus.AllRepaired;

    /// <summary>
    /// Gets the list of successfully repaired files.
    /// </summary>
    public List<string> RepairedFiles { get; } = new List<string>();

    /// <summary>
    /// Gets the list of files that could not be repaired.
    /// </summary>
    public List<string> UnrepairableFiles { get; } = new List<string>();

    /// <summary>
    /// Gets the list of repair actions performed.
    /// </summary>
    public List<RepairAction> RepairActions { get; } = new List<RepairAction>();

    /// <summary>
    /// Gets or sets the duration of the repair operation.
    /// </summary>
    public TimeSpan RepairDuration { get; set; }

    /// <summary>
    /// Gets or sets the exception if repair failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether the repair was successful.
    /// </summary>
    public bool IsSuccessful => Status != RepairStatus.RepairFailed;
}

/// <summary>
/// Represents a single repair action following Eclipse Store patterns.
/// </summary>
public class RepairAction
{
    /// <summary>
    /// Gets or sets the file path being repaired.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of repair action.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the repair was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the description of the repair action.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception if repair failed.
    /// </summary>
    public Exception? Exception { get; set; }
}