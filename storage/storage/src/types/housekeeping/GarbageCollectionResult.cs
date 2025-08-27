using System;

namespace NebulaStore.Storage.Embedded.Types.Housekeeping;

/// <summary>
/// Contains the results of a garbage collection operation following Eclipse Store patterns.
/// </summary>
public class GarbageCollectionResult
{
    /// <summary>
    /// Gets or sets the garbage collection status.
    /// </summary>
    public GarbageCollectionStatus Status { get; set; } = GarbageCollectionStatus.InProgress;

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the time budget in nanoseconds.
    /// </summary>
    public long TimeBudgetNanoseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of files deleted.
    /// </summary>
    public int FilesDeleted { get; set; }

    /// <summary>
    /// Gets or sets the bytes reclaimed.
    /// </summary>
    public long BytesReclaimed { get; set; }

    /// <summary>
    /// Gets or sets the exception if garbage collection failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether garbage collection was successful.
    /// </summary>
    public bool IsSuccessful => Status == GarbageCollectionStatus.Completed;

    /// <summary>
    /// Gets a summary of the garbage collection operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Files Deleted: {FilesDeleted}, " +
                           $"Bytes Reclaimed: {BytesReclaimed:N0}, " +
                           $"Duration: {Duration.TotalMilliseconds:F0}ms";
}

/// <summary>
/// Contains the results of a file consolidation operation following Eclipse Store patterns.
/// </summary>
public class FileConsolidationResult
{
    /// <summary>
    /// Gets or sets the file consolidation status.
    /// </summary>
    public FileConsolidationStatus Status { get; set; } = FileConsolidationStatus.InProgress;

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the time budget in nanoseconds.
    /// </summary>
    public long TimeBudgetNanoseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of files consolidated.
    /// </summary>
    public int FilesConsolidated { get; set; }

    /// <summary>
    /// Gets or sets the bytes consolidated.
    /// </summary>
    public long BytesConsolidated { get; set; }

    /// <summary>
    /// Gets or sets the exception if file consolidation failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether file consolidation was successful.
    /// </summary>
    public bool IsSuccessful => Status == FileConsolidationStatus.Completed;

    /// <summary>
    /// Gets a summary of the file consolidation operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Files Consolidated: {FilesConsolidated}, " +
                           $"Bytes Consolidated: {BytesConsolidated:N0}, " +
                           $"Duration: {Duration.TotalMilliseconds:F0}ms";
}

/// <summary>
/// Contains the results of a storage optimization operation following Eclipse Store patterns.
/// </summary>
public class StorageOptimizationResult
{
    /// <summary>
    /// Gets or sets the storage optimization status.
    /// </summary>
    public StorageOptimizationStatus Status { get; set; } = StorageOptimizationStatus.InProgress;

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the time budget in nanoseconds.
    /// </summary>
    public long TimeBudgetNanoseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of optimizations performed.
    /// </summary>
    public int OptimizationsPerformed { get; set; }

    /// <summary>
    /// Gets or sets the exception if storage optimization failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether storage optimization was successful.
    /// </summary>
    public bool IsSuccessful => Status == StorageOptimizationStatus.Completed;

    /// <summary>
    /// Gets a summary of the storage optimization operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Optimizations: {OptimizationsPerformed}, " +
                           $"Duration: {Duration.TotalMilliseconds:F0}ms";
}