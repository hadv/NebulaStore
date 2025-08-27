using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types.Performance;

/// <summary>
/// Represents performance metrics for a specific operation type following Eclipse Store patterns.
/// </summary>
public class PerformanceMetric
{
    #region Private Fields

    private long _totalOperations;
    private long _successfulOperations;
    private long _failedOperations;
    private long _totalLatencyTicks;
    private long _minLatencyTicks = long.MaxValue;
    private long _maxLatencyTicks = long.MinValue;
    private readonly object _lock = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the PerformanceMetric class.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    public PerformanceMetric(string operationType)
    {
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
        CreatedTime = DateTime.UtcNow;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the operation type.
    /// </summary>
    public string OperationType { get; }

    /// <summary>
    /// Gets the time when this metric was created.
    /// </summary>
    public DateTime CreatedTime { get; }

    /// <summary>
    /// Gets the total number of operations.
    /// </summary>
    public long TotalOperations => Interlocked.Read(ref _totalOperations);

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public long SuccessfulOperations => Interlocked.Read(ref _successfulOperations);

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public long FailedOperations => Interlocked.Read(ref _failedOperations);

    /// <summary>
    /// Gets the average latency.
    /// </summary>
    public TimeSpan AverageLatency
    {
        get
        {
            var totalOps = TotalOperations;
            if (totalOps == 0)
                return TimeSpan.Zero;

            var avgTicks = Interlocked.Read(ref _totalLatencyTicks) / totalOps;
            return new TimeSpan(avgTicks);
        }
    }

    /// <summary>
    /// Gets the minimum latency.
    /// </summary>
    public TimeSpan MinLatency
    {
        get
        {
            var minTicks = Interlocked.Read(ref _minLatencyTicks);
            return minTicks == long.MaxValue ? TimeSpan.Zero : new TimeSpan(minTicks);
        }
    }

    /// <summary>
    /// Gets the maximum latency.
    /// </summary>
    public TimeSpan MaxLatency
    {
        get
        {
            var maxTicks = Interlocked.Read(ref _maxLatencyTicks);
            return maxTicks == long.MinValue ? TimeSpan.Zero : new TimeSpan(maxTicks);
        }
    }

    /// <summary>
    /// Gets the success rate as a percentage (0.0 to 1.0).
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var totalOps = TotalOperations;
            return totalOps > 0 ? (double)SuccessfulOperations / totalOps : 0.0;
        }
    }

    /// <summary>
    /// Gets the failure rate as a percentage (0.0 to 1.0).
    /// </summary>
    public double FailureRate => 1.0 - SuccessRate;

    #endregion

    #region Public Methods

    /// <summary>
    /// Records an operation following Eclipse Store patterns.
    /// </summary>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public void RecordOperation(TimeSpan duration, bool success)
    {
        var durationTicks = duration.Ticks;

        // Update counters
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Add(ref _totalLatencyTicks, durationTicks);

        if (success)
        {
            Interlocked.Increment(ref _successfulOperations);
        }
        else
        {
            Interlocked.Increment(ref _failedOperations);
        }

        // Update min/max latency (thread-safe)
        lock (_lock)
        {
            if (durationTicks < _minLatencyTicks)
            {
                _minLatencyTicks = durationTicks;
            }

            if (durationTicks > _maxLatencyTicks)
            {
                _maxLatencyTicks = durationTicks;
            }
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Interlocked.Exchange(ref _totalOperations, 0);
            Interlocked.Exchange(ref _successfulOperations, 0);
            Interlocked.Exchange(ref _failedOperations, 0);
            Interlocked.Exchange(ref _totalLatencyTicks, 0);
            _minLatencyTicks = long.MaxValue;
            _maxLatencyTicks = long.MinValue;
        }
    }

    /// <summary>
    /// Gets a summary of the performance metric.
    /// </summary>
    public string Summary => $"{OperationType}: {TotalOperations:N0} ops, " +
                           $"Avg: {AverageLatency.TotalMilliseconds:F1}ms, " +
                           $"Success: {SuccessRate:P1}, " +
                           $"Range: {MinLatency.TotalMilliseconds:F1}-{MaxLatency.TotalMilliseconds:F1}ms";

    /// <summary>
    /// Returns a string representation of the performance metric.
    /// </summary>
    /// <returns>A string representation of the performance metric.</returns>
    public override string ToString()
    {
        return $"PerformanceMetric: {Summary}";
    }

    #endregion
}