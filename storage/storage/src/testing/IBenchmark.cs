using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Testing;

/// <summary>
/// Interface for performance benchmarks.
/// </summary>
public interface IBenchmark
{
    /// <summary>
    /// Gets the benchmark name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the benchmark category.
    /// </summary>
    BenchmarkCategory Category { get; }

    /// <summary>
    /// Gets the benchmark description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the estimated execution time.
    /// </summary>
    TimeSpan EstimatedDuration { get; }

    /// <summary>
    /// Sets up the benchmark environment.
    /// </summary>
    /// <param name="context">Benchmark context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetupAsync(IBenchmarkContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the benchmark.
    /// </summary>
    /// <param name="context">Benchmark context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up after the benchmark.
    /// </summary>
    /// <param name="context">Benchmark context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupAsync(IBenchmarkContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for benchmark execution context.
/// </summary>
public interface IBenchmarkContext
{
    /// <summary>
    /// Gets the performance integration.
    /// </summary>
    object PerformanceIntegration { get; }

    /// <summary>
    /// Gets the benchmark configuration.
    /// </summary>
    BenchmarkConfiguration Configuration { get; }

    /// <summary>
    /// Gets benchmark-specific data.
    /// </summary>
    /// <param name="key">Data key</param>
    /// <returns>Data value</returns>
    object? GetData(string key);

    /// <summary>
    /// Sets benchmark-specific data.
    /// </summary>
    /// <param name="key">Data key</param>
    /// <param name="value">Data value</param>
    void SetData(string key, object? value);

    /// <summary>
    /// Records a metric during benchmark execution.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Metric value</param>
    void RecordMetric(string name, double value);
}

/// <summary>
/// Enumeration of benchmark categories.
/// </summary>
public enum BenchmarkCategory
{
    /// <summary>
    /// Cache performance benchmarks.
    /// </summary>
    Cache,

    /// <summary>
    /// Index performance benchmarks.
    /// </summary>
    Index,

    /// <summary>
    /// Query performance benchmarks.
    /// </summary>
    Query,

    /// <summary>
    /// Memory management benchmarks.
    /// </summary>
    Memory,

    /// <summary>
    /// I/O performance benchmarks.
    /// </summary>
    IO,

    /// <summary>
    /// Concurrency benchmarks.
    /// </summary>
    Concurrency,

    /// <summary>
    /// Integration benchmarks.
    /// </summary>
    Integration,

    /// <summary>
    /// Stress testing benchmarks.
    /// </summary>
    Stress
}

/// <summary>
/// Configuration for benchmarking framework.
/// </summary>
public class BenchmarkConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable system warmup.
    /// </summary>
    public bool EnableWarmup { get; set; } = true;

    /// <summary>
    /// Gets or sets the warmup duration.
    /// </summary>
    public TimeSpan WarmupDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of benchmark iterations.
    /// </summary>
    public int Iterations { get; set; } = 3;

    /// <summary>
    /// Gets or sets the benchmark timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets whether to collect detailed metrics.
    /// </summary>
    public bool CollectDetailedMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics collection interval.
    /// </summary>
    public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to enable garbage collection between benchmarks.
    /// </summary>
    public bool EnableGCBetweenBenchmarks { get; set; } = true;

    /// <summary>
    /// Gets or sets custom benchmark parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    public override string ToString()
    {
        return $"BenchmarkConfiguration[Warmup={EnableWarmup} ({WarmupDuration}), " +
               $"Iterations={Iterations}, Timeout={Timeout}, " +
               $"DetailedMetrics={CollectDetailedMetrics}, GC={EnableGCBetweenBenchmarks}]";
    }
}

/// <summary>
/// Benchmark execution result.
/// </summary>
public class BenchmarkResult
{
    public BenchmarkResult(
        string benchmarkName,
        BenchmarkCategory category,
        bool successful,
        TimeSpan duration,
        BenchmarkMetrics metrics,
        string? errorMessage = null)
    {
        BenchmarkName = benchmarkName ?? throw new ArgumentNullException(nameof(benchmarkName));
        Category = category;
        Successful = successful;
        Duration = duration;
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the benchmark name.
    /// </summary>
    public string BenchmarkName { get; }

    /// <summary>
    /// Gets the benchmark category.
    /// </summary>
    public BenchmarkCategory Category { get; }

    /// <summary>
    /// Gets whether the benchmark was successful.
    /// </summary>
    public bool Successful { get; }

    /// <summary>
    /// Gets the benchmark duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the benchmark metrics.
    /// </summary>
    public BenchmarkMetrics Metrics { get; }

    /// <summary>
    /// Gets the error message if the benchmark failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the benchmark timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = Successful ? "SUCCESS" : "FAILED";
        var error = !string.IsNullOrEmpty(ErrorMessage) ? $" - {ErrorMessage}" : "";
        return $"[{status}] {BenchmarkName} ({Category}): {Duration.TotalMilliseconds:F0}ms, " +
               $"Throughput: {Metrics.Throughput:F0} ops/s{error}";
    }
}

/// <summary>
/// Benchmark performance metrics.
/// </summary>
public class BenchmarkMetrics
{
    public BenchmarkMetrics(
        double throughput,
        double averageLatency,
        double p95Latency,
        double p99Latency,
        double cpuUsage,
        double memoryUsage,
        double diskIO)
    {
        Throughput = throughput;
        AverageLatency = averageLatency;
        P95Latency = p95Latency;
        P99Latency = p99Latency;
        CpuUsage = cpuUsage;
        MemoryUsage = memoryUsage;
        DiskIO = diskIO;
    }

    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double Throughput { get; }

    /// <summary>
    /// Gets the average latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; }

    /// <summary>
    /// Gets the 95th percentile latency in milliseconds.
    /// </summary>
    public double P95Latency { get; }

    /// <summary>
    /// Gets the 99th percentile latency in milliseconds.
    /// </summary>
    public double P99Latency { get; }

    /// <summary>
    /// Gets the CPU usage percentage.
    /// </summary>
    public double CpuUsage { get; }

    /// <summary>
    /// Gets the memory usage in MB.
    /// </summary>
    public double MemoryUsage { get; }

    /// <summary>
    /// Gets the disk I/O in bytes per second.
    /// </summary>
    public double DiskIO { get; }

    public override string ToString()
    {
        return $"BenchmarkMetrics[Throughput={Throughput:F0} ops/s, " +
               $"AvgLatency={AverageLatency:F1}ms, P95={P95Latency:F1}ms, P99={P99Latency:F1}ms, " +
               $"CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}MB, DiskIO={DiskIO:F0} B/s]";
    }
}

/// <summary>
/// Comprehensive benchmark report.
/// </summary>
public class BenchmarkReport
{
    public BenchmarkReport(
        SystemInfo systemInfo,
        IEnumerable<BenchmarkResult> results,
        IEnumerable<BenchmarkComparison> comparisons,
        IEnumerable<BenchmarkRecommendation> recommendations,
        TimeSpan totalDuration,
        DateTime generatedAt)
    {
        SystemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        Results = results?.ToList() ?? new List<BenchmarkResult>();
        Comparisons = comparisons?.ToList() ?? new List<BenchmarkComparison>();
        Recommendations = recommendations?.ToList() ?? new List<BenchmarkRecommendation>();
        TotalDuration = totalDuration;
        GeneratedAt = generatedAt;
    }

    /// <summary>
    /// Gets the system information.
    /// </summary>
    public SystemInfo SystemInfo { get; }

    /// <summary>
    /// Gets the benchmark results.
    /// </summary>
    public IReadOnlyList<BenchmarkResult> Results { get; }

    /// <summary>
    /// Gets the benchmark comparisons.
    /// </summary>
    public IReadOnlyList<BenchmarkComparison> Comparisons { get; }

    /// <summary>
    /// Gets the performance recommendations.
    /// </summary>
    public IReadOnlyList<BenchmarkRecommendation> Recommendations { get; }

    /// <summary>
    /// Gets the total benchmark duration.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; }

    /// <summary>
    /// Gets the number of successful benchmarks.
    /// </summary>
    public int SuccessfulBenchmarks => Results.Count(r => r.Successful);

    /// <summary>
    /// Gets the number of failed benchmarks.
    /// </summary>
    public int FailedBenchmarks => Results.Count(r => !r.Successful);

    /// <summary>
    /// Gets the overall success rate.
    /// </summary>
    public double SuccessRate => Results.Count > 0 ? (double)SuccessfulBenchmarks / Results.Count : 0.0;

    public override string ToString()
    {
        return $"Benchmark Report [{GeneratedAt:yyyy-MM-dd HH:mm:ss}]: " +
               $"{SuccessfulBenchmarks}/{Results.Count} benchmarks successful " +
               $"({SuccessRate:P1}), Duration: {TotalDuration}, " +
               $"{Recommendations.Count} recommendations";
    }
}

/// <summary>
/// System information for benchmark reports.
/// </summary>
public class SystemInfo
{
    public SystemInfo(
        string machineName,
        string operatingSystem,
        int processorCount,
        long totalMemoryBytes,
        string runtimeVersion,
        DateTime capturedAt)
    {
        MachineName = machineName ?? throw new ArgumentNullException(nameof(machineName));
        OperatingSystem = operatingSystem ?? throw new ArgumentNullException(nameof(operatingSystem));
        ProcessorCount = processorCount;
        TotalMemoryBytes = totalMemoryBytes;
        RuntimeVersion = runtimeVersion ?? throw new ArgumentNullException(nameof(runtimeVersion));
        CapturedAt = capturedAt;
    }

    public string MachineName { get; }
    public string OperatingSystem { get; }
    public int ProcessorCount { get; }
    public long TotalMemoryBytes { get; }
    public string RuntimeVersion { get; }
    public DateTime CapturedAt { get; }

    public double TotalMemoryGB => TotalMemoryBytes / (1024.0 * 1024.0 * 1024.0);

    public override string ToString()
    {
        return $"System: {MachineName} ({OperatingSystem}), " +
               $"CPU: {ProcessorCount} cores, Memory: {TotalMemoryGB:F1} GB, " +
               $"Runtime: {RuntimeVersion}";
    }
}
