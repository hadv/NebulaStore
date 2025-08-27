using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Performance;

/// <summary>
/// Contains performance statistics following Eclipse Store patterns.
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// Gets or sets the monitor start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the monitor uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the total number of operations.
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Gets or sets the total number of errors.
    /// </summary>
    public long TotalErrors { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the operations per second.
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets the operation-specific statistics.
    /// </summary>
    public Dictionary<string, OperationStatistics> OperationStatistics { get; } = new Dictionary<string, OperationStatistics>();

    /// <summary>
    /// Gets the current time.
    /// </summary>
    public DateTime CurrentTime => DateTime.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the system is performing well.
    /// </summary>
    public bool IsPerformingWell => ErrorRate < 5.0 && OperationsPerSecond > 1.0;

    /// <summary>
    /// Gets a summary of the performance statistics.
    /// </summary>
    public string Summary => $"Uptime: {Uptime.TotalHours:F1}h, " +
                           $"Operations: {TotalOperations:N0}, " +
                           $"Ops/Sec: {OperationsPerSecond:F1}, " +
                           $"Error Rate: {ErrorRate:F1}%, " +
                           $"Status: {(IsPerformingWell ? "Good" : "Poor")}";

    /// <summary>
    /// Returns a string representation of the performance statistics.
    /// </summary>
    /// <returns>A string representation of the performance statistics.</returns>
    public override string ToString()
    {
        return $"PerformanceStatistics: {Summary}";
    }
}

/// <summary>
/// Contains statistics for a specific operation type following Eclipse Store patterns.
/// </summary>
public class OperationStatistics
{
    /// <summary>
    /// Gets or sets the total number of operations.
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Gets or sets the average latency.
    /// </summary>
    public TimeSpan AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the operations per second.
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets a value indicating whether this operation is performing well.
    /// </summary>
    public bool IsPerformingWell => SuccessRate > 0.95 && AverageLatency.TotalMilliseconds < 100;

    /// <summary>
    /// Gets a summary of the operation statistics.
    /// </summary>
    public string Summary => $"Ops: {TotalOperations:N0}, " +
                           $"Avg: {AverageLatency.TotalMilliseconds:F1}ms, " +
                           $"Success: {SuccessRate:P1}, " +
                           $"Ops/Sec: {OperationsPerSecond:F1}";

    /// <summary>
    /// Returns a string representation of the operation statistics.
    /// </summary>
    /// <returns>A string representation of the operation statistics.</returns>
    public override string ToString()
    {
        return $"OperationStatistics: {Summary}";
    }
}