using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Performance;

/// <summary>
/// Contains performance analysis results following Eclipse Store patterns.
/// </summary>
public class PerformanceAnalysis
{
    /// <summary>
    /// Gets or sets the analysis time.
    /// </summary>
    public DateTime AnalysisTime { get; set; }

    /// <summary>
    /// Gets or sets the monitor uptime.
    /// </summary>
    public TimeSpan MonitorUptime { get; set; }

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
    /// Gets the list of operation analyses.
    /// </summary>
    public List<OperationAnalysis> OperationAnalyses { get; } = new List<OperationAnalysis>();

    /// <summary>
    /// Gets the list of overall recommendations.
    /// </summary>
    public List<string> OverallRecommendations { get; } = new List<string>();

    /// <summary>
    /// Gets the operations per second.
    /// </summary>
    public double OperationsPerSecond => MonitorUptime.TotalSeconds > 0 ? TotalOperations / MonitorUptime.TotalSeconds : 0.0;

    /// <summary>
    /// Gets a value indicating whether performance is healthy.
    /// </summary>
    public bool IsHealthy => ErrorRate < 5.0 && OperationsPerSecond > 1.0;

    /// <summary>
    /// Gets a summary of the performance analysis.
    /// </summary>
    public string Summary => $"Operations: {TotalOperations:N0}, " +
                           $"Error Rate: {ErrorRate:F1}%, " +
                           $"Ops/Sec: {OperationsPerSecond:F1}, " +
                           $"Uptime: {MonitorUptime.TotalHours:F1}h, " +
                           $"Health: {(IsHealthy ? "Good" : "Poor")}";
}

/// <summary>
/// Contains analysis results for a specific operation type following Eclipse Store patterns.
/// </summary>
public class OperationAnalysis
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of operations.
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of successful operations.
    /// </summary>
    public long SuccessfulOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed operations.
    /// </summary>
    public long FailedOperations { get; set; }

    /// <summary>
    /// Gets or sets the average latency.
    /// </summary>
    public TimeSpan AverageLatency { get; set; }

    /// <summary>
    /// Gets or sets the minimum latency.
    /// </summary>
    public TimeSpan MinLatency { get; set; }

    /// <summary>
    /// Gets or sets the maximum latency.
    /// </summary>
    public TimeSpan MaxLatency { get; set; }

    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets the list of recommendations for this operation type.
    /// </summary>
    public List<string> Recommendations { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether this operation type is performing well.
    /// </summary>
    public bool IsPerformingWell => SuccessRate > 0.95 && AverageLatency.TotalMilliseconds < 100;

    /// <summary>
    /// Gets a summary of the operation analysis.
    /// </summary>
    public string Summary => $"{OperationType}: {TotalOperations:N0} ops, " +
                           $"Avg: {AverageLatency.TotalMilliseconds:F1}ms, " +
                           $"Success: {SuccessRate:P1}, " +
                           $"Performance: {(IsPerformingWell ? "Good" : "Poor")}";
}

/// <summary>
/// Represents a performance event following Eclipse Store patterns.
/// </summary>
public class PerformanceEvent
{
    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the event.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets a summary of the performance event.
    /// </summary>
    public string Summary => $"{Timestamp:HH:mm:ss.fff} {OperationType} " +
                           $"{Duration.TotalMilliseconds:F1}ms " +
                           $"{(Success ? "✓" : "✗")}";
}