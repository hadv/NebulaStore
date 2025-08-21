using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Testing;

/// <summary>
/// Interface for comprehensive performance testing and validation.
/// </summary>
public interface IPerformanceTestSuite : IDisposable
{
    /// <summary>
    /// Gets the test suite name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current test status.
    /// </summary>
    TestStatus Status { get; }

    /// <summary>
    /// Gets the test configuration.
    /// </summary>
    PerformanceTestConfiguration Configuration { get; }

    /// <summary>
    /// Runs all performance tests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test results</returns>
    Task<PerformanceTestResults> RunAllTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a specific test category.
    /// </summary>
    /// <param name="category">Test category to run</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test results</returns>
    Task<PerformanceTestResults> RunTestCategoryAsync(TestCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a specific test.
    /// </summary>
    /// <param name="testName">Test name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test result</returns>
    Task<PerformanceTestResult> RunTestAsync(string testName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates performance against target metrics.
    /// </summary>
    /// <param name="targetMetrics">Target performance metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<PerformanceValidationResult> ValidatePerformanceAsync(
        TargetPerformanceMetrics targetMetrics, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs load testing with specified parameters.
    /// </summary>
    /// <param name="loadTestParameters">Load test parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Load test result</returns>
    Task<LoadTestResult> PerformLoadTestAsync(
        LoadTestParameters loadTestParameters, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs stress testing to find system limits.
    /// </summary>
    /// <param name="stressTestParameters">Stress test parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stress test result</returns>
    Task<StressTestResult> PerformStressTestAsync(
        StressTestParameters stressTestParameters, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a performance benchmark report.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Benchmark report</returns>
    Task<PerformanceBenchmarkReport> GenerateBenchmarkReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when a test completes.
    /// </summary>
    event EventHandler<TestCompletedEventArgs> TestCompleted;

    /// <summary>
    /// Event fired when test progress updates.
    /// </summary>
    event EventHandler<TestProgressEventArgs> ProgressUpdated;
}

/// <summary>
/// Enumeration of test status.
/// </summary>
public enum TestStatus
{
    /// <summary>
    /// Test is not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Test is running.
    /// </summary>
    Running,

    /// <summary>
    /// Test completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Test failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Test was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Enumeration of test categories.
/// </summary>
public enum TestCategory
{
    /// <summary>
    /// Cache performance tests.
    /// </summary>
    Cache,

    /// <summary>
    /// Index performance tests.
    /// </summary>
    Index,

    /// <summary>
    /// Memory management tests.
    /// </summary>
    Memory,

    /// <summary>
    /// I/O performance tests.
    /// </summary>
    IO,

    /// <summary>
    /// Concurrency tests.
    /// </summary>
    Concurrency,

    /// <summary>
    /// Query performance tests.
    /// </summary>
    Query,

    /// <summary>
    /// Integration tests.
    /// </summary>
    Integration,

    /// <summary>
    /// Stress tests.
    /// </summary>
    Stress,

    /// <summary>
    /// Load tests.
    /// </summary>
    Load
}

/// <summary>
/// Performance test configuration.
/// </summary>
public class PerformanceTestConfiguration
{
    /// <summary>
    /// Gets or sets the test timeout.
    /// </summary>
    public TimeSpan TestTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the warm-up duration.
    /// </summary>
    public TimeSpan WarmupDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the test duration.
    /// </summary>
    public TimeSpan TestDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the number of test iterations.
    /// </summary>
    public int TestIterations { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to collect system metrics during tests.
    /// </summary>
    public bool CollectSystemMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics collection interval.
    /// </summary>
    public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(1);

    public override string ToString()
    {
        return $"PerformanceTestConfiguration[Timeout={TestTimeout}, " +
               $"Warmup={WarmupDuration}, Duration={TestDuration}, " +
               $"Iterations={TestIterations}, DetailedLogging={EnableDetailedLogging}]";
    }
}

/// <summary>
/// Target performance metrics for validation.
/// </summary>
public class TargetPerformanceMetrics
{
    /// <summary>
    /// Gets or sets the target throughput (operations per second).
    /// </summary>
    public double TargetThroughput { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the target average latency in milliseconds.
    /// </summary>
    public double TargetAverageLatencyMs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the target 95th percentile latency in milliseconds.
    /// </summary>
    public double TargetP95LatencyMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets the target 99th percentile latency in milliseconds.
    /// </summary>
    public double TargetP99LatencyMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the target cache hit ratio.
    /// </summary>
    public double TargetCacheHitRatio { get; set; } = 0.9; // 90%

    /// <summary>
    /// Gets or sets the target memory usage in MB.
    /// </summary>
    public double TargetMemoryUsageMB { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the target CPU usage percentage.
    /// </summary>
    public double TargetCpuUsagePercent { get; set; } = 80;

    public override string ToString()
    {
        return $"TargetMetrics[Throughput={TargetThroughput:F0} ops/s, " +
               $"AvgLatency={TargetAverageLatencyMs:F1}ms, P95={TargetP95LatencyMs:F1}ms, " +
               $"P99={TargetP99LatencyMs:F1}ms, CacheHit={TargetCacheHitRatio:P1}, " +
               $"Memory={TargetMemoryUsageMB:F0}MB, CPU={TargetCpuUsagePercent:F1}%]";
    }
}

/// <summary>
/// Load test parameters.
/// </summary>
public class LoadTestParameters
{
    /// <summary>
    /// Gets or sets the number of concurrent users/threads.
    /// </summary>
    public int ConcurrentUsers { get; set; } = 100;

    /// <summary>
    /// Gets or sets the operations per second per user.
    /// </summary>
    public double OperationsPerSecondPerUser { get; set; } = 10;

    /// <summary>
    /// Gets or sets the test duration.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the ramp-up time.
    /// </summary>
    public TimeSpan RampUpTime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the operation mix (operation type -> percentage).
    /// </summary>
    public Dictionary<string, double> OperationMix { get; set; } = new()
    {
        ["Read"] = 0.7,
        ["Write"] = 0.2,
        ["Query"] = 0.1
    };

    public override string ToString()
    {
        return $"LoadTestParameters[Users={ConcurrentUsers}, " +
               $"OpsPerUser={OperationsPerSecondPerUser:F1}/s, " +
               $"Duration={Duration}, RampUp={RampUpTime}]";
    }
}

/// <summary>
/// Stress test parameters.
/// </summary>
public class StressTestParameters
{
    /// <summary>
    /// Gets or sets the starting load level.
    /// </summary>
    public int StartingLoad { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum load level.
    /// </summary>
    public int MaximumLoad { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the load increment step.
    /// </summary>
    public int LoadIncrement { get; set; } = 10;

    /// <summary>
    /// Gets or sets the duration for each load level.
    /// </summary>
    public TimeSpan LoadLevelDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the failure threshold (error rate).
    /// </summary>
    public double FailureThreshold { get; set; } = 0.05; // 5%

    /// <summary>
    /// Gets or sets the latency threshold in milliseconds.
    /// </summary>
    public double LatencyThresholdMs { get; set; } = 1000;

    public override string ToString()
    {
        return $"StressTestParameters[Load={StartingLoad}-{MaximumLoad} (step {LoadIncrement}), " +
               $"Duration={LoadLevelDuration}, FailureThreshold={FailureThreshold:P1}, " +
               $"LatencyThreshold={LatencyThresholdMs:F0}ms]";
    }
}

/// <summary>
/// Performance test results.
/// </summary>
public class PerformanceTestResults
{
    public PerformanceTestResults(
        IEnumerable<PerformanceTestResult> testResults,
        TimeSpan totalDuration,
        DateTime timestamp)
    {
        TestResults = testResults?.ToList() ?? new List<PerformanceTestResult>();
        TotalDuration = totalDuration;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the individual test results.
    /// </summary>
    public IReadOnlyList<PerformanceTestResult> TestResults { get; }

    /// <summary>
    /// Gets the total test duration.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the test timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the number of passed tests.
    /// </summary>
    public int PassedTests => TestResults.Count(r => r.Passed);

    /// <summary>
    /// Gets the number of failed tests.
    /// </summary>
    public int FailedTests => TestResults.Count(r => !r.Passed);

    /// <summary>
    /// Gets the overall pass rate.
    /// </summary>
    public double PassRate => TestResults.Count > 0 ? (double)PassedTests / TestResults.Count : 0.0;

    public override string ToString()
    {
        return $"Performance Test Results [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Passed={PassedTests}, Failed={FailedTests}, " +
               $"PassRate={PassRate:P1}, Duration={TotalDuration}";
    }
}

/// <summary>
/// Individual performance test result.
/// </summary>
public class PerformanceTestResult
{
    public PerformanceTestResult(
        string testName,
        TestCategory category,
        bool passed,
        TimeSpan duration,
        PerformanceMetrics metrics,
        string? errorMessage = null)
    {
        TestName = testName ?? throw new ArgumentNullException(nameof(testName));
        Category = category;
        Passed = passed;
        Duration = duration;
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the test name.
    /// </summary>
    public string TestName { get; }

    /// <summary>
    /// Gets the test category.
    /// </summary>
    public TestCategory Category { get; }

    /// <summary>
    /// Gets whether the test passed.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// Gets the test duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the performance metrics.
    /// </summary>
    public PerformanceMetrics Metrics { get; }

    /// <summary>
    /// Gets the error message if the test failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the test timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = Passed ? "PASS" : "FAIL";
        var error = !string.IsNullOrEmpty(ErrorMessage) ? $" - {ErrorMessage}" : "";
        return $"[{status}] {TestName} ({Category}): {Duration.TotalMilliseconds:F0}ms{error}";
    }
}

/// <summary>
/// Performance metrics collected during testing.
/// </summary>
public class PerformanceMetrics
{
    public PerformanceMetrics(
        double throughput,
        double averageLatencyMs,
        double p95LatencyMs,
        double p99LatencyMs,
        double cacheHitRatio,
        double memoryUsageMB,
        double cpuUsagePercent,
        long totalOperations,
        long errorCount)
    {
        Throughput = throughput;
        AverageLatencyMs = averageLatencyMs;
        P95LatencyMs = p95LatencyMs;
        P99LatencyMs = p99LatencyMs;
        CacheHitRatio = cacheHitRatio;
        MemoryUsageMB = memoryUsageMB;
        CpuUsagePercent = cpuUsagePercent;
        TotalOperations = totalOperations;
        ErrorCount = errorCount;
    }

    public double Throughput { get; }
    public double AverageLatencyMs { get; }
    public double P95LatencyMs { get; }
    public double P99LatencyMs { get; }
    public double CacheHitRatio { get; }
    public double MemoryUsageMB { get; }
    public double CpuUsagePercent { get; }
    public long TotalOperations { get; }
    public long ErrorCount { get; }

    public double ErrorRate => TotalOperations > 0 ? (double)ErrorCount / TotalOperations : 0.0;

    public override string ToString()
    {
        return $"Metrics[Throughput={Throughput:F0} ops/s, " +
               $"AvgLatency={AverageLatencyMs:F1}ms, P95={P95LatencyMs:F1}ms, " +
               $"P99={P99LatencyMs:F1}ms, CacheHit={CacheHitRatio:P1}, " +
               $"Memory={MemoryUsageMB:F0}MB, CPU={CpuUsagePercent:F1}%, " +
               $"Errors={ErrorCount:N0}/{TotalOperations:N0} ({ErrorRate:P2})]";
    }
}

/// <summary>
/// Performance validation result.
/// </summary>
public class PerformanceValidationResult
{
    public PerformanceValidationResult(
        bool passed,
        TargetPerformanceMetrics targetMetrics,
        PerformanceMetrics actualMetrics,
        IEnumerable<ValidationFailure> failures)
    {
        Passed = passed;
        TargetMetrics = targetMetrics ?? throw new ArgumentNullException(nameof(targetMetrics));
        ActualMetrics = actualMetrics ?? throw new ArgumentNullException(nameof(actualMetrics));
        Failures = failures?.ToList() ?? new List<ValidationFailure>();
        Timestamp = DateTime.UtcNow;
    }

    public bool Passed { get; }
    public TargetPerformanceMetrics TargetMetrics { get; }
    public PerformanceMetrics ActualMetrics { get; }
    public IReadOnlyList<ValidationFailure> Failures { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = Passed ? "PASS" : "FAIL";
        return $"Performance Validation [{Timestamp:HH:mm:ss}]: {status} " +
               $"({Failures.Count} failure(s))";
    }
}

/// <summary>
/// Load test result.
/// </summary>
public class LoadTestResult
{
    public LoadTestResult(
        LoadTestParameters parameters,
        PerformanceMetrics metrics,
        bool successful,
        TimeSpan actualDuration,
        string? errorMessage = null)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Successful = successful;
        ActualDuration = actualDuration;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    public LoadTestParameters Parameters { get; }
    public PerformanceMetrics Metrics { get; }
    public bool Successful { get; }
    public TimeSpan ActualDuration { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = Successful ? "SUCCESS" : "FAILED";
        return $"Load Test [{Timestamp:HH:mm:ss}]: {status} - " +
               $"{Parameters.ConcurrentUsers} users, {Metrics.Throughput:F0} ops/s, " +
               $"{Metrics.AverageLatencyMs:F1}ms avg latency";
    }
}

/// <summary>
/// Stress test result.
/// </summary>
public class StressTestResult
{
    public StressTestResult(
        StressTestParameters parameters,
        int maxSustainableLoad,
        PerformanceMetrics metricsAtMaxLoad,
        int failurePoint,
        string? failureReason = null)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        MaxSustainableLoad = maxSustainableLoad;
        MetricsAtMaxLoad = metricsAtMaxLoad ?? throw new ArgumentNullException(nameof(metricsAtMaxLoad));
        FailurePoint = failurePoint;
        FailureReason = failureReason;
        Timestamp = DateTime.UtcNow;
    }

    public StressTestParameters Parameters { get; }
    public int MaxSustainableLoad { get; }
    public PerformanceMetrics MetricsAtMaxLoad { get; }
    public int FailurePoint { get; }
    public string? FailureReason { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Stress Test [{Timestamp:HH:mm:ss}]: " +
               $"Max sustainable load: {MaxSustainableLoad}, " +
               $"Failure point: {FailurePoint}" +
               (string.IsNullOrEmpty(FailureReason) ? "" : $" ({FailureReason})");
    }
}

/// <summary>
/// Performance benchmark report.
/// </summary>
public class PerformanceBenchmarkReport
{
    public PerformanceBenchmarkReport(
        string systemInfo,
        PerformanceTestResults testResults,
        IEnumerable<BenchmarkComparison> comparisons,
        IEnumerable<PerformanceRecommendation> recommendations)
    {
        SystemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        TestResults = testResults ?? throw new ArgumentNullException(nameof(testResults));
        Comparisons = comparisons?.ToList() ?? new List<BenchmarkComparison>();
        Recommendations = recommendations?.ToList() ?? new List<PerformanceRecommendation>();
        GeneratedAt = DateTime.UtcNow;
    }

    public string SystemInfo { get; }
    public PerformanceTestResults TestResults { get; }
    public IReadOnlyList<BenchmarkComparison> Comparisons { get; }
    public IReadOnlyList<PerformanceRecommendation> Recommendations { get; }
    public DateTime GeneratedAt { get; }

    public override string ToString()
    {
        return $"Performance Benchmark Report [{GeneratedAt:yyyy-MM-dd HH:mm:ss}]: " +
               $"{TestResults.TestResults.Count} tests, " +
               $"{Comparisons.Count} comparisons, " +
               $"{Recommendations.Count} recommendations";
    }
}

/// <summary>
/// Validation failure details.
/// </summary>
public class ValidationFailure
{
    public ValidationFailure(string metricName, double expected, double actual, string description)
    {
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        Expected = expected;
        Actual = actual;
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string MetricName { get; }
    public double Expected { get; }
    public double Actual { get; }
    public string Description { get; }

    public double Deviation => Math.Abs(Actual - Expected) / Expected;

    public override string ToString()
    {
        return $"{MetricName}: Expected {Expected:F2}, Actual {Actual:F2} " +
               $"(Deviation: {Deviation:P1}) - {Description}";
    }
}

/// <summary>
/// Benchmark comparison.
/// </summary>
public class BenchmarkComparison
{
    public BenchmarkComparison(string name, double currentValue, double baselineValue, string unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CurrentValue = currentValue;
        BaselineValue = baselineValue;
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }

    public string Name { get; }
    public double CurrentValue { get; }
    public double BaselineValue { get; }
    public string Unit { get; }

    public double ImprovementRatio => BaselineValue != 0 ? CurrentValue / BaselineValue : 0;
    public double ImprovementPercent => (ImprovementRatio - 1) * 100;

    public override string ToString()
    {
        var improvement = ImprovementPercent >= 0 ? "+" : "";
        return $"{Name}: {CurrentValue:F2} {Unit} vs {BaselineValue:F2} {Unit} " +
               $"({improvement}{ImprovementPercent:F1}%)";
    }
}

/// <summary>
/// Performance recommendation.
/// </summary>
public class PerformanceRecommendation
{
    public PerformanceRecommendation(
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
/// Enumeration of recommendation priorities.
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Event arguments for test completion.
/// </summary>
public class TestCompletedEventArgs : EventArgs
{
    public TestCompletedEventArgs(PerformanceTestResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PerformanceTestResult Result { get; }
}

/// <summary>
/// Event arguments for test progress updates.
/// </summary>
public class TestProgressEventArgs : EventArgs
{
    public TestProgressEventArgs(string testName, int completedTests, int totalTests, double progressPercent)
    {
        TestName = testName ?? throw new ArgumentNullException(nameof(testName));
        CompletedTests = completedTests;
        TotalTests = totalTests;
        ProgressPercent = progressPercent;
        Timestamp = DateTime.UtcNow;
    }

    public string TestName { get; }
    public int CompletedTests { get; }
    public int TotalTests { get; }
    public double ProgressPercent { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Test Progress: {TestName} - {CompletedTests}/{TotalTests} " +
               $"({ProgressPercent:F1}%) @ {Timestamp:HH:mm:ss}";
    }
}
