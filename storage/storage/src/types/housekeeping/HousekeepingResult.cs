using System;

namespace NebulaStore.Storage.Embedded.Types.Housekeeping;

/// <summary>
/// Represents housekeeping operation status following Eclipse Store patterns.
/// </summary>
public enum HousekeepingStatus
{
    /// <summary>
    /// Housekeeping operation is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Housekeeping operation completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Housekeeping operation exceeded time budget.
    /// </summary>
    TimeBudgetExceeded,

    /// <summary>
    /// Housekeeping operation failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents garbage collection status following Eclipse Store patterns.
/// </summary>
public enum GarbageCollectionStatus
{
    /// <summary>
    /// Garbage collection is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Garbage collection completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Garbage collection exceeded time budget.
    /// </summary>
    TimeBudgetExceeded,

    /// <summary>
    /// Garbage collection failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents file consolidation status following Eclipse Store patterns.
/// </summary>
public enum FileConsolidationStatus
{
    /// <summary>
    /// File consolidation is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// File consolidation completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// File consolidation exceeded time budget.
    /// </summary>
    TimeBudgetExceeded,

    /// <summary>
    /// File consolidation failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents storage optimization status following Eclipse Store patterns.
/// </summary>
public enum StorageOptimizationStatus
{
    /// <summary>
    /// Storage optimization is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Storage optimization completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Storage optimization exceeded time budget.
    /// </summary>
    TimeBudgetExceeded,

    /// <summary>
    /// Storage optimization failed.
    /// </summary>
    Failed
}

/// <summary>
/// Contains the results of a housekeeping operation following Eclipse Store patterns.
/// </summary>
public class HousekeepingResult
{
    /// <summary>
    /// Gets or sets the housekeeping status.
    /// </summary>
    public HousekeepingStatus Status { get; set; } = HousekeepingStatus.InProgress;

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
    /// Gets or sets the garbage collection result.
    /// </summary>
    public GarbageCollectionResult? GarbageCollectionResult { get; set; }

    /// <summary>
    /// Gets or sets the file consolidation result.
    /// </summary>
    public FileConsolidationResult? FileConsolidationResult { get; set; }

    /// <summary>
    /// Gets or sets the storage optimization result.
    /// </summary>
    public StorageOptimizationResult? StorageOptimizationResult { get; set; }

    /// <summary>
    /// Gets or sets the exception if housekeeping failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a value indicating whether housekeeping was successful.
    /// </summary>
    public bool IsSuccessful => Status == HousekeepingStatus.Completed;

    /// <summary>
    /// Gets the total bytes reclaimed.
    /// </summary>
    public long TotalBytesReclaimed => (GarbageCollectionResult?.BytesReclaimed ?? 0) +
                                      (FileConsolidationResult?.BytesConsolidated ?? 0);

    /// <summary>
    /// Gets a summary of the housekeeping operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Duration: {Duration.TotalMilliseconds:F0}ms, " +
                           $"Bytes Reclaimed: {TotalBytesReclaimed:N0}, " +
                           $"GC: {GarbageCollectionResult?.Status ?? GarbageCollectionStatus.InProgress}, " +
                           $"Consolidation: {FileConsolidationResult?.Status ?? FileConsolidationStatus.InProgress}";
}