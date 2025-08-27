using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebulaStore.Benchmarks;

/// <summary>
/// Interface for database benchmarks comparing NebulaStore with traditional databases.
/// </summary>
public interface IBenchmark : IDisposable
{
    /// <summary>
    /// Name of the benchmark implementation (e.g., "NebulaStore", "MySQL").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initialize the benchmark with the specified configuration.
    /// </summary>
    /// <param name="config">Benchmark configuration</param>
    Task InitializeAsync(BenchmarkConfig config);

    /// <summary>
    /// Prepare the database/storage for benchmarking (create tables, indexes, etc.).
    /// </summary>
    Task PrepareAsync();

    /// <summary>
    /// Clean up any existing data before running benchmarks.
    /// </summary>
    Task CleanupAsync();

    /// <summary>
    /// Insert a batch of records and measure performance.
    /// </summary>
    /// <param name="records">Records to insert</param>
    /// <returns>Insert performance metrics</returns>
    Task<BenchmarkResult> InsertBatchAsync<T>(IEnumerable<T> records) where T : class;

    /// <summary>
    /// Query records by ID and measure performance.
    /// </summary>
    /// <param name="ids">IDs to query</param>
    /// <returns>Query performance metrics</returns>
    Task<BenchmarkResult> QueryByIdAsync<T>(IEnumerable<int> ids) where T : class;

    /// <summary>
    /// Query records with a filter condition and measure performance.
    /// </summary>
    /// <param name="predicate">Filter predicate</param>
    /// <returns>Query performance metrics</returns>
    Task<BenchmarkResult> QueryWithFilterAsync<T>(Func<T, bool> predicate) where T : class;

    /// <summary>
    /// Query records with complex conditions and measure performance.
    /// </summary>
    /// <returns>Query performance metrics</returns>
    Task<BenchmarkResult> QueryComplexAsync<T>() where T : class;

    /// <summary>
    /// Get current memory usage statistics.
    /// </summary>
    /// <returns>Memory usage in bytes</returns>
    Task<long> GetMemoryUsageAsync();

    /// <summary>
    /// Get current storage size statistics.
    /// </summary>
    /// <returns>Storage size in bytes</returns>
    Task<long> GetStorageSizeAsync();
}

/// <summary>
/// Configuration for benchmark execution.
/// </summary>
public class BenchmarkConfig
{
    /// <summary>
    /// Total number of records to use in benchmarks.
    /// </summary>
    public int TotalRecords { get; set; } = 3_000_000;

    /// <summary>
    /// Batch size for insert operations.
    /// </summary>
    public int BatchSize { get; set; } = 10_000;

    /// <summary>
    /// Number of query operations to perform.
    /// </summary>
    public int QueryCount { get; set; } = 1_000;

    /// <summary>
    /// Connection string for database benchmarks.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Storage directory for file-based benchmarks.
    /// </summary>
    public string StorageDirectory { get; set; } = "benchmark-storage";

    /// <summary>
    /// Whether to enable verbose logging.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// Number of warmup iterations before actual benchmarking.
    /// </summary>
    public int WarmupIterations { get; set; } = 3;

    /// <summary>
    /// Number of benchmark iterations to average results.
    /// </summary>
    public int BenchmarkIterations { get; set; } = 5;
}

/// <summary>
/// Result of a benchmark operation.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Name of the operation that was benchmarked.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Number of records processed.
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// Total time taken for the operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Operations per second.
    /// </summary>
    public double OperationsPerSecond => RecordCount / Duration.TotalSeconds;

    /// <summary>
    /// Memory usage before the operation (in bytes).
    /// </summary>
    public long MemoryBefore { get; set; }

    /// <summary>
    /// Memory usage after the operation (in bytes).
    /// </summary>
    public long MemoryAfter { get; set; }

    /// <summary>
    /// Memory used by the operation (in bytes).
    /// </summary>
    public long MemoryUsed => MemoryAfter - MemoryBefore;

    /// <summary>
    /// Storage size before the operation (in bytes).
    /// </summary>
    public long StorageBefore { get; set; }

    /// <summary>
    /// Storage size after the operation (in bytes).
    /// </summary>
    public long StorageAfter { get; set; }

    /// <summary>
    /// Storage used by the operation (in bytes).
    /// </summary>
    public long StorageUsed => StorageAfter - StorageBefore;

    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata about the operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Comprehensive benchmark results for comparison.
/// </summary>
public class BenchmarkSuite
{
    /// <summary>
    /// Name of the benchmark suite.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Configuration used for the benchmark.
    /// </summary>
    public BenchmarkConfig Config { get; set; } = new();

    /// <summary>
    /// Results for insert operations.
    /// </summary>
    public List<BenchmarkResult> InsertResults { get; set; } = new();

    /// <summary>
    /// Results for query by ID operations.
    /// </summary>
    public List<BenchmarkResult> QueryByIdResults { get; set; } = new();

    /// <summary>
    /// Results for filtered query operations.
    /// </summary>
    public List<BenchmarkResult> QueryFilterResults { get; set; } = new();

    /// <summary>
    /// Results for complex query operations.
    /// </summary>
    public List<BenchmarkResult> QueryComplexResults { get; set; } = new();

    /// <summary>
    /// Total time for the entire benchmark suite.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Peak memory usage during the benchmark.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Final storage size after all operations.
    /// </summary>
    public long FinalStorageSize { get; set; }

    /// <summary>
    /// Calculate average insert performance.
    /// </summary>
    public double AverageInsertOpsPerSecond => 
        InsertResults.Count > 0 ? InsertResults.Average(r => r.OperationsPerSecond) : 0;

    /// <summary>
    /// Calculate average query by ID performance.
    /// </summary>
    public double AverageQueryByIdOpsPerSecond => 
        QueryByIdResults.Count > 0 ? QueryByIdResults.Average(r => r.OperationsPerSecond) : 0;

    /// <summary>
    /// Calculate average filtered query performance.
    /// </summary>
    public double AverageQueryFilterOpsPerSecond => 
        QueryFilterResults.Count > 0 ? QueryFilterResults.Average(r => r.OperationsPerSecond) : 0;

    /// <summary>
    /// Calculate average complex query performance.
    /// </summary>
    public double AverageQueryComplexOpsPerSecond => 
        QueryComplexResults.Count > 0 ? QueryComplexResults.Average(r => r.OperationsPerSecond) : 0;
}
