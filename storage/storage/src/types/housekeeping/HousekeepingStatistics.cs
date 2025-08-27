using System;

namespace NebulaStore.Storage.Embedded.Types.Housekeeping;

/// <summary>
/// Contains housekeeping statistics following Eclipse Store patterns.
/// </summary>
public class HousekeepingStatistics
{
    /// <summary>
    /// Gets or sets the total number of garbage collections performed.
    /// </summary>
    public long TotalGarbageCollections { get; set; }

    /// <summary>
    /// Gets or sets the total number of file consolidations performed.
    /// </summary>
    public long TotalFileConsolidations { get; set; }

    /// <summary>
    /// Gets or sets the total bytes reclaimed through housekeeping.
    /// </summary>
    public long TotalBytesReclaimed { get; set; }

    /// <summary>
    /// Gets or sets the last housekeeping run time.
    /// </summary>
    public DateTime LastHousekeepingRun { get; set; }

    /// <summary>
    /// Gets or sets the housekeeping interval.
    /// </summary>
    public TimeSpan HousekeepingInterval { get; set; }

    /// <summary>
    /// Gets or sets the time budget in nanoseconds.
    /// </summary>
    public long TimeBudgetNanoseconds { get; set; }

    /// <summary>
    /// Gets the time since last housekeeping run.
    /// </summary>
    public TimeSpan TimeSinceLastRun => DateTime.UtcNow - LastHousekeepingRun;

    /// <summary>
    /// Gets the average bytes reclaimed per garbage collection.
    /// </summary>
    public double AverageBytesReclaimedPerGC => TotalGarbageCollections > 0 ?
        (double)TotalBytesReclaimed / TotalGarbageCollections : 0.0;

    /// <summary>
    /// Gets a value indicating whether housekeeping is overdue.
    /// </summary>
    public bool IsOverdue => TimeSinceLastRun > HousekeepingInterval * 1.5;

    /// <summary>
    /// Gets a summary of the housekeeping statistics.
    /// </summary>
    public string Summary => $"GC Runs: {TotalGarbageCollections:N0}, " +
                           $"Consolidations: {TotalFileConsolidations:N0}, " +
                           $"Bytes Reclaimed: {TotalBytesReclaimed:N0}, " +
                           $"Last Run: {TimeSinceLastRun.TotalMinutes:F1}m ago, " +
                           $"Interval: {HousekeepingInterval.TotalMinutes:F1}m";

    /// <summary>
    /// Returns a string representation of the housekeeping statistics.
    /// </summary>
    /// <returns>A string representation of the housekeeping statistics.</returns>
    public override string ToString()
    {
        return $"HousekeepingStatistics: {Summary}";
    }
}