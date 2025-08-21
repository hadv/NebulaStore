using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Integration;
using NebulaStore.Storage.Embedded.Monitoring;

namespace NebulaStore.Storage.Embedded.Testing;

/// <summary>
/// Comprehensive performance benchmarking framework for NebulaStore.
/// </summary>
public class PerformanceBenchmarkFramework : IDisposable
{
    private readonly string _name;
    private readonly BenchmarkConfiguration _configuration;
    private readonly IPerformanceIntegration _performanceIntegration;
    private readonly List<IBenchmark> _benchmarks;
    private readonly BenchmarkStatistics _statistics;
    private volatile bool _isDisposed;

    public PerformanceBenchmarkFramework(
        string name,
        IPerformanceIntegration performanceIntegration,
        BenchmarkConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _performanceIntegration = performanceIntegration ?? throw new ArgumentNullException(nameof(performanceIntegration));
        _configuration = configuration ?? new BenchmarkConfiguration();
        _benchmarks = new List<IBenchmark>();
        _statistics = new BenchmarkStatistics();

        InitializeStandardBenchmarks();
    }

    /// <summary>
    /// Gets the framework name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the benchmark configuration.
    /// </summary>
    public BenchmarkConfiguration Configuration => _configuration;

    /// <summary>
    /// Gets the benchmark statistics.
    /// </summary>
    public BenchmarkStatistics Statistics => _statistics;

    /// <summary>
    /// Adds a custom benchmark to the framework.
    /// </summary>
    /// <param name="benchmark">Benchmark to add</param>
    public void AddBenchmark(IBenchmark benchmark)
    {
        if (benchmark == null) throw new ArgumentNullException(nameof(benchmark));
        _benchmarks.Add(benchmark);
    }

    /// <summary>
    /// Runs all benchmarks and generates a comprehensive report.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Benchmark report</returns>
    public async Task<BenchmarkReport> RunBenchmarksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var results = new List<BenchmarkResult>();
        var systemInfo = await CollectSystemInfoAsync();

        try
        {
            // Warm up the system
            await WarmUpSystemAsync(cancellationToken);

            // Run each benchmark
            foreach (var benchmark in _benchmarks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var result = await RunBenchmarkAsync(benchmark, cancellationToken);
                    results.Add(result);
                    _statistics.RecordBenchmarkResult(result);
                }
                catch (Exception ex)
                {
                    var failedResult = new BenchmarkResult(
                        benchmark.Name,
                        benchmark.Category,
                        false,
                        TimeSpan.Zero,
                        new BenchmarkMetrics(0, 0, 0, 0, 0, 0, 0),
                        ex.Message
                    );
                    results.Add(failedResult);
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;
            var comparisons = GenerateComparisons(results);
            var recommendations = GenerateRecommendations(results);

            return new BenchmarkReport(
                systemInfo,
                results,
                comparisons,
                recommendations,
                totalDuration,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            throw new BenchmarkException($"Benchmark execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Runs a specific benchmark category.
    /// </summary>
    /// <param name="category">Benchmark category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Benchmark results</returns>
    public async Task<IEnumerable<BenchmarkResult>> RunCategoryAsync(
        BenchmarkCategory category, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var categoryBenchmarks = _benchmarks.Where(b => b.Category == category).ToList();
        var results = new List<BenchmarkResult>();

        await WarmUpSystemAsync(cancellationToken);

        foreach (var benchmark in categoryBenchmarks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var result = await RunBenchmarkAsync(benchmark, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Runs a continuous benchmark for the specified duration.
    /// </summary>
    /// <param name="duration">Benchmark duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Continuous benchmark result</returns>
    public async Task<ContinuousBenchmarkResult> RunContinuousBenchmarkAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(duration);
        var snapshots = new List<PerformanceSnapshot>();
        var interval = TimeSpan.FromSeconds(1);

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var snapshot = await CapturePerformanceSnapshotAsync();
            snapshots.Add(snapshot);

            await Task.Delay(interval, cancellationToken);
        }

        var actualDuration = DateTime.UtcNow - startTime;
        
        return new ContinuousBenchmarkResult(
            snapshots,
            actualDuration,
            CalculateAverageMetrics(snapshots),
            DateTime.UtcNow
        );
    }

    private async Task<BenchmarkResult> RunBenchmarkAsync(IBenchmark benchmark, CancellationToken cancellationToken)
    {
        var context = new BenchmarkContext(_performanceIntegration, _configuration);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Pre-benchmark setup
            await benchmark.SetupAsync(context, cancellationToken);

            // Capture baseline metrics
            var baselineMetrics = await CaptureMetricsAsync();

            // Run the benchmark
            stopwatch.Restart();
            await benchmark.RunAsync(context, cancellationToken);
            stopwatch.Stop();

            // Capture final metrics
            var finalMetrics = await CaptureMetricsAsync();

            // Calculate benchmark metrics
            var benchmarkMetrics = CalculateBenchmarkMetrics(baselineMetrics, finalMetrics, stopwatch.Elapsed);

            // Cleanup
            await benchmark.CleanupAsync(context, cancellationToken);

            return new BenchmarkResult(
                benchmark.Name,
                benchmark.Category,
                true,
                stopwatch.Elapsed,
                benchmarkMetrics,
                null
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            try
            {
                await benchmark.CleanupAsync(context, cancellationToken);
            }
            catch
            {
                // Ignore cleanup errors
            }

            return new BenchmarkResult(
                benchmark.Name,
                benchmark.Category,
                false,
                stopwatch.Elapsed,
                new BenchmarkMetrics(0, 0, 0, 0, 0, 0, 0),
                ex.Message
            );
        }
    }

    private async Task WarmUpSystemAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.EnableWarmup)
            return;

        // Perform system warm-up operations
        var warmupBenchmark = new WarmupBenchmark();
        var context = new BenchmarkContext(_performanceIntegration, _configuration);
        
        await warmupBenchmark.SetupAsync(context, cancellationToken);
        await warmupBenchmark.RunAsync(context, cancellationToken);
        await warmupBenchmark.CleanupAsync(context, cancellationToken);

        // Allow system to stabilize
        await Task.Delay(_configuration.WarmupDuration, cancellationToken);
    }

    private async Task<SystemInfo> CollectSystemInfoAsync()
    {
        var resourceSnapshot = _performanceIntegration.GetPerformanceStatistics().SystemResources;
        
        return new SystemInfo(
            Environment.MachineName,
            Environment.OSVersion.ToString(),
            Environment.ProcessorCount,
            resourceSnapshot.TotalMemoryBytes,
            Environment.Version.ToString(),
            DateTime.UtcNow
        );
    }

    private async Task<PerformanceSnapshot> CapturePerformanceSnapshotAsync()
    {
        var stats = _performanceIntegration.GetPerformanceStatistics();
        
        return new PerformanceSnapshot(
            stats.CacheStatistics.AverageHitRatio,
            stats.IndexStatistics.AverageHitRatio,
            stats.SystemResources.CpuUsagePercent,
            stats.SystemResources.MemoryUsagePercent,
            stats.SystemResources.TotalDiskBytesPerSecond,
            stats.SystemResources.ThreadCount,
            DateTime.UtcNow
        );
    }

    private async Task<BaselineMetrics> CaptureMetricsAsync()
    {
        var snapshot = await CapturePerformanceSnapshotAsync();
        
        return new BaselineMetrics(
            snapshot.CpuUsage,
            snapshot.MemoryUsage,
            snapshot.DiskIO,
            snapshot.ThreadCount,
            DateTime.UtcNow
        );
    }

    private BenchmarkMetrics CalculateBenchmarkMetrics(
        BaselineMetrics baseline,
        BaselineMetrics final,
        TimeSpan duration)
    {
        var cpuDelta = final.CpuUsage - baseline.CpuUsage;
        var memoryDelta = final.MemoryUsage - baseline.MemoryUsage;
        var diskIODelta = final.DiskIO - baseline.DiskIO;
        
        return new BenchmarkMetrics(
            1000.0 / duration.TotalMilliseconds, // Operations per second (assuming 1 operation)
            duration.TotalMilliseconds,
            duration.TotalMilliseconds, // P95 = avg for single operation
            duration.TotalMilliseconds, // P99 = avg for single operation
            cpuDelta,
            memoryDelta,
            diskIODelta
        );
    }

    private BenchmarkMetrics CalculateAverageMetrics(IEnumerable<PerformanceSnapshot> snapshots)
    {
        var snapshotList = snapshots.ToList();
        if (snapshotList.Count == 0)
            return new BenchmarkMetrics(0, 0, 0, 0, 0, 0, 0);

        return new BenchmarkMetrics(
            snapshotList.Count / (snapshotList.Last().Timestamp - snapshotList.First().Timestamp).TotalSeconds,
            snapshotList.Average(s => 1000.0), // Placeholder latency
            snapshotList.Average(s => 1000.0), // Placeholder P95
            snapshotList.Average(s => 1000.0), // Placeholder P99
            snapshotList.Average(s => s.CpuUsage),
            snapshotList.Average(s => s.MemoryUsage),
            snapshotList.Average(s => s.DiskIO)
        );
    }

    private List<BenchmarkComparison> GenerateComparisons(IEnumerable<BenchmarkResult> results)
    {
        var comparisons = new List<BenchmarkComparison>();
        
        // Compare against baseline values (would be loaded from configuration or previous runs)
        foreach (var result in results.Where(r => r.Successful))
        {
            var baselineThroughput = GetBaselineThroughput(result.BenchmarkName);
            if (baselineThroughput > 0)
            {
                comparisons.Add(new BenchmarkComparison(
                    $"{result.BenchmarkName} Throughput",
                    result.Metrics.Throughput,
                    baselineThroughput,
                    "ops/sec"
                ));
            }
        }

        return comparisons;
    }

    private List<BenchmarkRecommendation> GenerateRecommendations(IEnumerable<BenchmarkResult> results)
    {
        var recommendations = new List<BenchmarkRecommendation>();
        
        foreach (var result in results.Where(r => r.Successful))
        {
            // Analyze results and generate recommendations
            if (result.Metrics.AverageLatency > 100) // 100ms threshold
            {
                recommendations.Add(new BenchmarkRecommendation(
                    "High Latency Detected",
                    $"Benchmark '{result.BenchmarkName}' shows high latency ({result.Metrics.AverageLatency:F1}ms). " +
                    "Consider optimizing I/O operations or increasing cache size.",
                    RecommendationPriority.High,
                    0.3 // 30% expected improvement
                ));
            }

            if (result.Metrics.CpuUsage > 80) // 80% CPU threshold
            {
                recommendations.Add(new BenchmarkRecommendation(
                    "High CPU Usage",
                    $"Benchmark '{result.BenchmarkName}' shows high CPU usage ({result.Metrics.CpuUsage:F1}%). " +
                    "Consider optimizing algorithms or increasing parallelism.",
                    RecommendationPriority.Medium,
                    0.2 // 20% expected improvement
                ));
            }
        }

        return recommendations;
    }

    private double GetBaselineThroughput(string benchmarkName)
    {
        // In a real implementation, this would load baseline values from configuration or database
        return benchmarkName switch
        {
            "Cache Read Benchmark" => 50000,
            "Cache Write Benchmark" => 30000,
            "Index Lookup Benchmark" => 40000,
            "Query Execution Benchmark" => 1000,
            _ => 0
        };
    }

    private void InitializeStandardBenchmarks()
    {
        // Add standard benchmarks
        _benchmarks.Add(new CacheReadBenchmark());
        _benchmarks.Add(new CacheWriteBenchmark());
        _benchmarks.Add(new IndexLookupBenchmark());
        _benchmarks.Add(new QueryExecutionBenchmark());
        _benchmarks.Add(new ConcurrencyBenchmark());
        _benchmarks.Add(new MemoryAllocationBenchmark());
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(PerformanceBenchmarkFramework));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        
        foreach (var benchmark in _benchmarks.OfType<IDisposable>())
        {
            benchmark.Dispose();
        }
    }
}

/// <summary>
/// Benchmark context implementation.
/// </summary>
public class BenchmarkContext : IBenchmarkContext
{
    private readonly Dictionary<string, object?> _data = new();
    private readonly Dictionary<string, double> _metrics = new();

    public BenchmarkContext(IPerformanceIntegration performanceIntegration, BenchmarkConfiguration configuration)
    {
        PerformanceIntegration = performanceIntegration ?? throw new ArgumentNullException(nameof(performanceIntegration));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public object PerformanceIntegration { get; }
    public BenchmarkConfiguration Configuration { get; }

    public object? GetData(string key)
    {
        return _data.TryGetValue(key, out var value) ? value : null;
    }

    public void SetData(string key, object? value)
    {
        _data[key] = value;
    }

    public void RecordMetric(string name, double value)
    {
        _metrics[name] = value;
    }

    public IReadOnlyDictionary<string, double> GetMetrics() => _metrics;
}

/// <summary>
/// Performance snapshot for continuous benchmarking.
/// </summary>
public class PerformanceSnapshot
{
    public PerformanceSnapshot(
        double cacheHitRatio,
        double indexHitRatio,
        double cpuUsage,
        double memoryUsage,
        double diskIO,
        int threadCount,
        DateTime timestamp)
    {
        CacheHitRatio = cacheHitRatio;
        IndexHitRatio = indexHitRatio;
        CpuUsage = cpuUsage;
        MemoryUsage = memoryUsage;
        DiskIO = diskIO;
        ThreadCount = threadCount;
        Timestamp = timestamp;
    }

    public double CacheHitRatio { get; }
    public double IndexHitRatio { get; }
    public double CpuUsage { get; }
    public double MemoryUsage { get; }
    public double DiskIO { get; }
    public int ThreadCount { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Snapshot [{Timestamp:HH:mm:ss}]: Cache={CacheHitRatio:P1}, " +
               $"Index={IndexHitRatio:P1}, CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}%, " +
               $"DiskIO={DiskIO:F0} B/s, Threads={ThreadCount}";
    }
}

/// <summary>
/// Continuous benchmark result.
/// </summary>
public class ContinuousBenchmarkResult
{
    public ContinuousBenchmarkResult(
        IEnumerable<PerformanceSnapshot> snapshots,
        TimeSpan duration,
        BenchmarkMetrics averageMetrics,
        DateTime timestamp)
    {
        Snapshots = snapshots?.ToList() ?? new List<PerformanceSnapshot>();
        Duration = duration;
        AverageMetrics = averageMetrics ?? throw new ArgumentNullException(nameof(averageMetrics));
        Timestamp = timestamp;
    }

    public IReadOnlyList<PerformanceSnapshot> Snapshots { get; }
    public TimeSpan Duration { get; }
    public BenchmarkMetrics AverageMetrics { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Continuous Benchmark [{Timestamp:HH:mm:ss}]: " +
               $"{Snapshots.Count} snapshots over {Duration}, " +
               $"Avg Throughput: {AverageMetrics.Throughput:F0} ops/s";
    }
}

/// <summary>
/// Baseline metrics for comparison.
/// </summary>
public class BaselineMetrics
{
    public BaselineMetrics(double cpuUsage, double memoryUsage, double diskIO, int threadCount, DateTime timestamp)
    {
        CpuUsage = cpuUsage;
        MemoryUsage = memoryUsage;
        DiskIO = diskIO;
        ThreadCount = threadCount;
        Timestamp = timestamp;
    }

    public double CpuUsage { get; }
    public double MemoryUsage { get; }
    public double DiskIO { get; }
    public int ThreadCount { get; }
    public DateTime Timestamp { get; }
}

/// <summary>
/// Benchmark statistics collector.
/// </summary>
public class BenchmarkStatistics
{
    private readonly List<BenchmarkResult> _results = new();
    private readonly object _lock = new();

    public void RecordBenchmarkResult(BenchmarkResult result)
    {
        lock (_lock)
        {
            _results.Add(result);
        }
    }

    public IReadOnlyList<BenchmarkResult> GetResults()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }

    public int TotalBenchmarks => _results.Count;
    public int SuccessfulBenchmarks => _results.Count(r => r.Successful);
    public int FailedBenchmarks => _results.Count(r => !r.Successful);
    public double SuccessRate => TotalBenchmarks > 0 ? (double)SuccessfulBenchmarks / TotalBenchmarks : 0.0;

    public void Reset()
    {
        lock (_lock)
        {
            _results.Clear();
        }
    }
}

/// <summary>
/// Benchmark recommendation.
/// </summary>
public class BenchmarkRecommendation
{
    public BenchmarkRecommendation(
        string title,
        string description,
        RecommendationPriority priority,
        double expectedImprovement)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Priority = priority;
        ExpectedImprovement = expectedImprovement;
    }

    public string Title { get; }
    public string Description { get; }
    public RecommendationPriority Priority { get; }
    public double ExpectedImprovement { get; }

    public override string ToString()
    {
        return $"[{Priority}] {Title}: {Description} " +
               $"(Expected improvement: {ExpectedImprovement:P1})";
    }
}



/// <summary>
/// Exception thrown during benchmark execution.
/// </summary>
public class BenchmarkException : Exception
{
    public BenchmarkException(string message) : base(message) { }
    public BenchmarkException(string message, Exception innerException) : base(message, innerException) { }
}
