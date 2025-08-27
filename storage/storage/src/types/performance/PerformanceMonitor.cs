using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types.Performance;

/// <summary>
/// Monitors storage performance following Eclipse Store patterns.
/// Provides metrics collection, performance analysis, and optimization recommendations.
/// </summary>
public class PerformanceMonitor : IDisposable
{
    #region Private Fields

    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    private readonly ConcurrentQueue<PerformanceEvent> _events;
    private readonly Timer _metricsCollectionTimer;
    private readonly object _lock = new();

    private long _totalOperations;
    private long _totalErrors;
    private DateTime _startTime;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the PerformanceMonitor class.
    /// </summary>
    /// <param name="metricsCollectionInterval">The interval for metrics collection.</param>
    public PerformanceMonitor(TimeSpan? metricsCollectionInterval = null)
    {
        _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
        _events = new ConcurrentQueue<PerformanceEvent>();
        _startTime = DateTime.UtcNow;

        var interval = metricsCollectionInterval ?? TimeSpan.FromMinutes(1);
        _metricsCollectionTimer = new Timer(CollectMetrics, null, interval, interval);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the total number of operations monitored.
    /// </summary>
    public long TotalOperations => Interlocked.Read(ref _totalOperations);

    /// <summary>
    /// Gets the total number of errors recorded.
    /// </summary>
    public long TotalErrors => Interlocked.Read(ref _totalErrors);

    /// <summary>
    /// Gets the monitor start time.
    /// </summary>
    public DateTime StartTime => _startTime;

    /// <summary>
    /// Gets the uptime of the monitor.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;

    /// <summary>
    /// Gets the error rate as a percentage.
    /// </summary>
    public double ErrorRate => TotalOperations > 0 ? (double)TotalErrors / TotalOperations * 100 : 0.0;

    #endregion

    #region Public Methods

    /// <summary>
    /// Records an operation performance following Eclipse Store patterns.
    /// </summary>
    /// <param name="operationType">The type of operation.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="metadata">Additional metadata about the operation.</param>
    public void RecordOperation(string operationType, TimeSpan duration, bool success = true, Dictionary<string, object>? metadata = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(operationType))
            throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

        // Update counters
        Interlocked.Increment(ref _totalOperations);
        if (!success)
        {
            Interlocked.Increment(ref _totalErrors);
        }

        // Update or create metric
        _metrics.AddOrUpdate(operationType,
            key =>
            {
                var newMetric = new PerformanceMetric(operationType);
                newMetric.RecordOperation(duration, success);
                return newMetric;
            },
            (key, existing) =>
            {
                existing.RecordOperation(duration, success);
                return existing;
            });

        // Record event
        var performanceEvent = new PerformanceEvent
        {
            Timestamp = DateTime.UtcNow,
            OperationType = operationType,
            Duration = duration,
            Success = success,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _events.Enqueue(performanceEvent);

        // Keep only recent events (last 1000)
        while (_events.Count > 1000)
        {
            _events.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Records a storage read operation following Eclipse Store patterns.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="bytesRead">The number of bytes read.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="cacheHit">Whether this was a cache hit.</param>
    public void RecordReadOperation(long entityId, long bytesRead, TimeSpan duration, bool cacheHit = false)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EntityId"] = entityId,
            ["BytesRead"] = bytesRead,
            ["CacheHit"] = cacheHit
        };

        RecordOperation("Read", duration, true, metadata);
    }

    /// <summary>
    /// Records a storage write operation following Eclipse Store patterns.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public void RecordWriteOperation(long entityId, long bytesWritten, TimeSpan duration, bool success = true)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EntityId"] = entityId,
            ["BytesWritten"] = bytesWritten
        };

        RecordOperation("Write", duration, success, metadata);
    }

    /// <summary>
    /// Records a storage commit operation following Eclipse Store patterns.
    /// </summary>
    /// <param name="entitiesCommitted">The number of entities committed.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public void RecordCommitOperation(int entitiesCommitted, TimeSpan duration, bool success = true)
    {
        var metadata = new Dictionary<string, object>
        {
            ["EntitiesCommitted"] = entitiesCommitted
        };

        RecordOperation("Commit", duration, success, metadata);
    }

    /// <summary>
    /// Gets performance metrics for a specific operation type.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <returns>The performance metric, or null if not found.</returns>
    public PerformanceMetric? GetMetric(string operationType)
    {
        ThrowIfDisposed();

        return _metrics.TryGetValue(operationType, out var metric) ? metric : null;
    }

    /// <summary>
    /// Gets all performance metrics following Eclipse Store patterns.
    /// </summary>
    /// <returns>A dictionary of all performance metrics.</returns>
    public Dictionary<string, PerformanceMetric> GetAllMetrics()
    {
        ThrowIfDisposed();

        return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets recent performance events following Eclipse Store patterns.
    /// </summary>
    /// <param name="count">The maximum number of events to return.</param>
    /// <returns>A list of recent performance events.</returns>
    public List<PerformanceEvent> GetRecentEvents(int count = 100)
    {
        ThrowIfDisposed();

        return _events.TakeLast(Math.Min(count, _events.Count)).ToList();
    }

    /// <summary>
    /// Analyzes performance and provides optimization recommendations following Eclipse Store patterns.
    /// </summary>
    /// <returns>The performance analysis result.</returns>
    public PerformanceAnalysis AnalyzePerformance()
    {
        ThrowIfDisposed();

        var analysis = new PerformanceAnalysis
        {
            AnalysisTime = DateTime.UtcNow,
            MonitorUptime = Uptime,
            TotalOperations = TotalOperations,
            TotalErrors = TotalErrors,
            ErrorRate = ErrorRate
        };

        // Analyze each operation type
        foreach (var metric in _metrics.Values)
        {
            var operationAnalysis = new OperationAnalysis
            {
                OperationType = metric.OperationType,
                TotalOperations = metric.TotalOperations,
                SuccessfulOperations = metric.SuccessfulOperations,
                FailedOperations = metric.FailedOperations,
                AverageLatency = metric.AverageLatency,
                MinLatency = metric.MinLatency,
                MaxLatency = metric.MaxLatency,
                SuccessRate = metric.SuccessRate
            };

            // Generate recommendations based on performance
            GenerateRecommendations(operationAnalysis, metric);

            analysis.OperationAnalyses.Add(operationAnalysis);
        }

        // Generate overall recommendations
        GenerateOverallRecommendations(analysis);

        return analysis;
    }

    /// <summary>
    /// Gets performance statistics following Eclipse Store patterns.
    /// </summary>
    /// <returns>The performance statistics.</returns>
    public PerformanceStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var statistics = new PerformanceStatistics
        {
            StartTime = StartTime,
            Uptime = Uptime,
            TotalOperations = TotalOperations,
            TotalErrors = TotalErrors,
            ErrorRate = ErrorRate,
            OperationsPerSecond = TotalOperations > 0 ? TotalOperations / Uptime.TotalSeconds : 0.0
        };

        // Add operation-specific statistics
        foreach (var metric in _metrics.Values)
        {
            statistics.OperationStatistics[metric.OperationType] = new OperationStatistics
            {
                TotalOperations = metric.TotalOperations,
                AverageLatency = metric.AverageLatency,
                SuccessRate = metric.SuccessRate,
                OperationsPerSecond = metric.TotalOperations / Uptime.TotalSeconds
            };
        }

        return statistics;
    }

    /// <summary>
    /// Resets all performance metrics and events.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _metrics.Clear();
            while (_events.TryDequeue(out _)) { }

            Interlocked.Exchange(ref _totalOperations, 0);
            Interlocked.Exchange(ref _totalErrors, 0);
            _startTime = DateTime.UtcNow;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Timer callback for metrics collection.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void CollectMetrics(object? state)
    {
        try
        {
            if (!_disposed)
            {
                // Perform periodic metrics collection
                // In a full implementation, this could include:
                // - Memory usage sampling
                // - Disk I/O monitoring
                // - Cache hit rate calculation
                // - Performance trend analysis
            }
        }
        catch
        {
            // Ignore collection errors to prevent timer from stopping
        }
    }

    /// <summary>
    /// Generates recommendations for a specific operation type.
    /// </summary>
    /// <param name="analysis">The operation analysis.</param>
    /// <param name="metric">The performance metric.</param>
    private void GenerateRecommendations(OperationAnalysis analysis, PerformanceMetric metric)
    {
        // High latency recommendations
        if (metric.AverageLatency.TotalMilliseconds > 100)
        {
            analysis.Recommendations.Add($"High average latency detected for {metric.OperationType} operations ({metric.AverageLatency.TotalMilliseconds:F1}ms). Consider optimizing data access patterns or increasing cache size.");
        }

        // High error rate recommendations
        if (metric.SuccessRate < 0.95)
        {
            analysis.Recommendations.Add($"Low success rate for {metric.OperationType} operations ({metric.SuccessRate:P1}). Investigate error causes and improve error handling.");
        }

        // Performance variance recommendations
        var latencyVariance = metric.MaxLatency.TotalMilliseconds - metric.MinLatency.TotalMilliseconds;
        if (latencyVariance > 1000) // More than 1 second variance
        {
            analysis.Recommendations.Add($"High latency variance for {metric.OperationType} operations ({latencyVariance:F1}ms). Consider load balancing or resource optimization.");
        }
    }

    /// <summary>
    /// Generates overall performance recommendations.
    /// </summary>
    /// <param name="analysis">The performance analysis.</param>
    private void GenerateOverallRecommendations(PerformanceAnalysis analysis)
    {
        // Overall error rate recommendations
        if (analysis.ErrorRate > 5.0)
        {
            analysis.OverallRecommendations.Add($"High overall error rate ({analysis.ErrorRate:F1}%). Review system stability and error handling mechanisms.");
        }

        // Operations per second recommendations
        var opsPerSecond = analysis.TotalOperations / analysis.MonitorUptime.TotalSeconds;
        if (opsPerSecond < 10)
        {
            analysis.OverallRecommendations.Add($"Low throughput detected ({opsPerSecond:F1} ops/sec). Consider performance optimization or scaling.");
        }

        // Memory and resource recommendations
        if (analysis.OperationAnalyses.Count > 10)
        {
            analysis.OverallRecommendations.Add("Many operation types detected. Consider consolidating similar operations for better performance monitoring.");
        }
    }

    /// <summary>
    /// Throws if the monitor is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the performance monitor and stops all operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            _metricsCollectionTimer?.Dispose();
        }
    }

    #endregion
}