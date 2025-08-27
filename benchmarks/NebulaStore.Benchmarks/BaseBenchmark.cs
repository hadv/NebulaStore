using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NebulaStore.Benchmarks;

/// <summary>
/// Base class for benchmark implementations providing common functionality.
/// </summary>
public abstract class BaseBenchmark : IBenchmark
{
    protected BenchmarkConfig _config = new();
    protected bool _disposed = false;

    /// <summary>
    /// Name of the benchmark implementation.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Initialize the benchmark with the specified configuration.
    /// </summary>
    public virtual async Task InitializeAsync(BenchmarkConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        await InitializeImplementationAsync();
    }

    /// <summary>
    /// Implementation-specific initialization logic.
    /// </summary>
    protected abstract Task InitializeImplementationAsync();

    /// <summary>
    /// Prepare the database/storage for benchmarking.
    /// </summary>
    public abstract Task PrepareAsync();

    /// <summary>
    /// Clean up any existing data before running benchmarks.
    /// </summary>
    public abstract Task CleanupAsync();

    /// <summary>
    /// Insert a batch of records and measure performance.
    /// </summary>
    public async Task<BenchmarkResult> InsertBatchAsync<T>(IEnumerable<T> records) where T : class
    {
        var recordList = records.ToList();
        var result = new BenchmarkResult
        {
            OperationName = "InsertBatch",
            RecordCount = recordList.Count
        };

        try
        {
            // Measure memory and storage before
            result.MemoryBefore = await GetMemoryUsageAsync();
            result.StorageBefore = await GetStorageSizeAsync();

            // Execute the insert operation
            var stopwatch = Stopwatch.StartNew();
            await InsertBatchImplementationAsync(recordList);
            stopwatch.Stop();

            result.Duration = stopwatch.Elapsed;

            // Measure memory and storage after
            result.MemoryAfter = await GetMemoryUsageAsync();
            result.StorageAfter = await GetStorageSizeAsync();

            LogVerbose($"Inserted {recordList.Count} records in {result.Duration.TotalMilliseconds:F2}ms " +
                      $"({result.OperationsPerSecond:F0} ops/sec)");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogError($"Insert batch failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Query records by ID and measure performance.
    /// </summary>
    public async Task<BenchmarkResult> QueryByIdAsync<T>(IEnumerable<int> ids) where T : class
    {
        var idList = ids.ToList();
        var result = new BenchmarkResult
        {
            OperationName = "QueryById",
            RecordCount = idList.Count
        };

        try
        {
            result.MemoryBefore = await GetMemoryUsageAsync();
            result.StorageBefore = await GetStorageSizeAsync();

            var stopwatch = Stopwatch.StartNew();
            var queryResults = await QueryByIdImplementationAsync<T>(idList);
            stopwatch.Stop();

            result.Duration = stopwatch.Elapsed;
            result.MemoryAfter = await GetMemoryUsageAsync();
            result.StorageAfter = await GetStorageSizeAsync();

            result.Metadata["ResultCount"] = queryResults.Count();

            LogVerbose($"Queried {idList.Count} IDs in {result.Duration.TotalMilliseconds:F2}ms " +
                      $"({result.OperationsPerSecond:F0} ops/sec)");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogError($"Query by ID failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Query records with a filter condition and measure performance.
    /// </summary>
    public async Task<BenchmarkResult> QueryWithFilterAsync<T>(Func<T, bool> predicate) where T : class
    {
        var result = new BenchmarkResult
        {
            OperationName = "QueryWithFilter"
        };

        try
        {
            result.MemoryBefore = await GetMemoryUsageAsync();
            result.StorageBefore = await GetStorageSizeAsync();

            var stopwatch = Stopwatch.StartNew();
            var queryResults = await QueryWithFilterImplementationAsync(predicate);
            stopwatch.Stop();

            result.Duration = stopwatch.Elapsed;
            result.RecordCount = queryResults.Count();
            result.MemoryAfter = await GetMemoryUsageAsync();
            result.StorageAfter = await GetStorageSizeAsync();

            result.Metadata["ResultCount"] = result.RecordCount;

            LogVerbose($"Filtered query returned {result.RecordCount} results in {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogError($"Query with filter failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Query records with complex conditions and measure performance.
    /// </summary>
    public async Task<BenchmarkResult> QueryComplexAsync<T>() where T : class
    {
        var result = new BenchmarkResult
        {
            OperationName = "QueryComplex"
        };

        try
        {
            result.MemoryBefore = await GetMemoryUsageAsync();
            result.StorageBefore = await GetStorageSizeAsync();

            var stopwatch = Stopwatch.StartNew();
            var queryResults = await QueryComplexImplementationAsync<T>();
            stopwatch.Stop();

            result.Duration = stopwatch.Elapsed;
            result.RecordCount = queryResults.Count();
            result.MemoryAfter = await GetMemoryUsageAsync();
            result.StorageAfter = await GetStorageSizeAsync();

            result.Metadata["ResultCount"] = result.RecordCount;

            LogVerbose($"Complex query returned {result.RecordCount} results in {result.Duration.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogError($"Complex query failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Get current memory usage statistics.
    /// </summary>
    public virtual Task<long> GetMemoryUsageAsync()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Task.FromResult(GC.GetTotalMemory(false));
    }

    /// <summary>
    /// Get current storage size statistics.
    /// </summary>
    public abstract Task<long> GetStorageSizeAsync();

    #region Abstract Implementation Methods

    /// <summary>
    /// Implementation-specific insert batch logic.
    /// </summary>
    protected abstract Task InsertBatchImplementationAsync<T>(IList<T> records) where T : class;

    /// <summary>
    /// Implementation-specific query by ID logic.
    /// </summary>
    protected abstract Task<IEnumerable<T>> QueryByIdImplementationAsync<T>(IList<int> ids) where T : class;

    /// <summary>
    /// Implementation-specific query with filter logic.
    /// </summary>
    protected abstract Task<IEnumerable<T>> QueryWithFilterImplementationAsync<T>(Func<T, bool> predicate) where T : class;

    /// <summary>
    /// Implementation-specific complex query logic.
    /// </summary>
    protected abstract Task<IEnumerable<T>> QueryComplexImplementationAsync<T>() where T : class;

    #endregion

    #region Utility Methods

    /// <summary>
    /// Log verbose message if verbose logging is enabled.
    /// </summary>
    protected void LogVerbose(string message)
    {
        if (_config.VerboseLogging)
        {
            Console.WriteLine($"[{Name}] {message}");
        }
    }

    /// <summary>
    /// Log error message.
    /// </summary>
    protected void LogError(string message)
    {
        Console.WriteLine($"[{Name}] ERROR: {message}");
    }

    /// <summary>
    /// Log information message.
    /// </summary>
    protected void LogInfo(string message)
    {
        Console.WriteLine($"[{Name}] {message}");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            DisposeImplementation();
            _disposed = true;
        }
    }

    /// <summary>
    /// Implementation-specific disposal logic.
    /// </summary>
    protected abstract void DisposeImplementation();

    #endregion
}
