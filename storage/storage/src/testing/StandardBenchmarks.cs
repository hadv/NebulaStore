using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Integration;

namespace NebulaStore.Storage.Embedded.Testing;

/// <summary>
/// Base class for standard benchmarks.
/// </summary>
public abstract class BenchmarkBase : IBenchmark
{
    protected BenchmarkBase(string name, BenchmarkCategory category, string description, TimeSpan estimatedDuration)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        EstimatedDuration = estimatedDuration;
    }

    public string Name { get; }
    public BenchmarkCategory Category { get; }
    public string Description { get; }
    public TimeSpan EstimatedDuration { get; }

    public virtual Task SetupAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public abstract Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default);

    public virtual Task CleanupAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cache read performance benchmark.
/// </summary>
public class CacheReadBenchmark : BenchmarkBase
{
    public CacheReadBenchmark() : base(
        "Cache Read Benchmark",
        BenchmarkCategory.Cache,
        "Measures cache read performance with various key patterns",
        TimeSpan.FromMinutes(2))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        var cacheManager = integration.CacheManager;
        var iterations = 100000;

        // Populate cache with test data
        for (int i = 0; i < 1000; i++)
        {
            await cacheManager.SetAsync($"test-key-{i}", $"test-value-{i}", TimeSpan.FromMinutes(10), cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        // Perform read operations
        for (int i = 0; i < iterations; i++)
        {
            var key = $"test-key-{i % 1000}";
            var result = await cacheManager.GetAsync<string>(key, cancellationToken);
            if (result != null) successCount++;

            if (i % 10000 == 0)
            {
                context.RecordMetric("cache.read.progress", (double)i / iterations);
            }
        }

        stopwatch.Stop();

        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        var hitRatio = (double)successCount / iterations;

        context.RecordMetric("cache.read.throughput", throughput);
        context.RecordMetric("cache.read.hit_ratio", hitRatio);
        context.RecordMetric("cache.read.avg_latency", stopwatch.Elapsed.TotalMilliseconds / iterations);
    }
}

/// <summary>
/// Cache write performance benchmark.
/// </summary>
public class CacheWriteBenchmark : BenchmarkBase
{
    public CacheWriteBenchmark() : base(
        "Cache Write Benchmark",
        BenchmarkCategory.Cache,
        "Measures cache write performance with various data sizes",
        TimeSpan.FromMinutes(2))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        var cacheManager = integration.CacheManager;
        var iterations = 50000;

        var stopwatch = Stopwatch.StartNew();

        // Perform write operations
        for (int i = 0; i < iterations; i++)
        {
            var key = $"write-test-key-{i}";
            var value = $"write-test-value-{i}-{new string('x', i % 100)}"; // Variable size data
            
            await cacheManager.SetAsync(key, value, TimeSpan.FromMinutes(10), cancellationToken);

            if (i % 5000 == 0)
            {
                context.RecordMetric("cache.write.progress", (double)i / iterations);
            }
        }

        stopwatch.Stop();

        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        context.RecordMetric("cache.write.throughput", throughput);
        context.RecordMetric("cache.write.avg_latency", stopwatch.Elapsed.TotalMilliseconds / iterations);
    }
}

/// <summary>
/// Index lookup performance benchmark.
/// </summary>
public class IndexLookupBenchmark : BenchmarkBase
{
    public IndexLookupBenchmark() : base(
        "Index Lookup Benchmark",
        BenchmarkCategory.Index,
        "Measures index lookup performance with various key distributions",
        TimeSpan.FromMinutes(3))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        var indexManager = integration.IndexManager;
        var iterations = 200000;

        // Create and populate test index
        var index = indexManager.GetOrCreateIndex<string, string>("benchmark-index");
        
        // Populate index with test data
        for (int i = 0; i < 10000; i++)
        {
            index.Put($"index-key-{i}", $"index-value-{i}");
        }

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        // Perform lookup operations
        for (int i = 0; i < iterations; i++)
        {
            var key = $"index-key-{i % 10000}";
            if (index.TryGet(key, out var value))
            {
                successCount++;
            }

            if (i % 20000 == 0)
            {
                context.RecordMetric("index.lookup.progress", (double)i / iterations);
            }
        }

        stopwatch.Stop();

        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        var hitRatio = (double)successCount / iterations;

        context.RecordMetric("index.lookup.throughput", throughput);
        context.RecordMetric("index.lookup.hit_ratio", hitRatio);
        context.RecordMetric("index.lookup.avg_latency", stopwatch.Elapsed.TotalMilliseconds / iterations);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Query execution performance benchmark.
/// </summary>
public class QueryExecutionBenchmark : BenchmarkBase
{
    public QueryExecutionBenchmark() : base(
        "Query Execution Benchmark",
        BenchmarkCategory.Query,
        "Measures query execution performance with various complexity levels",
        TimeSpan.FromMinutes(5))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var iterations = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Simulate query execution
        for (int i = 0; i < iterations; i++)
        {
            // Simulate different query complexities
            var complexity = i % 3;
            var delay = complexity switch
            {
                0 => TimeSpan.FromMilliseconds(1), // Simple query
                1 => TimeSpan.FromMilliseconds(5), // Medium query
                2 => TimeSpan.FromMilliseconds(10), // Complex query
                _ => TimeSpan.FromMilliseconds(1)
            };

            await Task.Delay(delay, cancellationToken);

            if (i % 100 == 0)
            {
                context.RecordMetric("query.execution.progress", (double)i / iterations);
            }
        }

        stopwatch.Stop();

        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        context.RecordMetric("query.execution.throughput", throughput);
        context.RecordMetric("query.execution.avg_latency", stopwatch.Elapsed.TotalMilliseconds / iterations);
    }
}

/// <summary>
/// Concurrency performance benchmark.
/// </summary>
public class ConcurrencyBenchmark : BenchmarkBase
{
    public ConcurrencyBenchmark() : base(
        "Concurrency Benchmark",
        BenchmarkCategory.Concurrency,
        "Measures performance under concurrent load",
        TimeSpan.FromMinutes(3))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        var cacheManager = integration.CacheManager;
        var concurrentTasks = 10;
        var operationsPerTask = 5000;

        var stopwatch = Stopwatch.StartNew();

        // Create concurrent tasks
        var tasks = new List<Task>();
        for (int taskId = 0; taskId < concurrentTasks; taskId++)
        {
            var localTaskId = taskId;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    var key = $"concurrent-key-{localTaskId}-{i}";
                    var value = $"concurrent-value-{localTaskId}-{i}";
                    
                    // Mix of read and write operations
                    if (i % 2 == 0)
                    {
                        await cacheManager.SetAsync(key, value, TimeSpan.FromMinutes(5), cancellationToken);
                    }
                    else
                    {
                        await cacheManager.GetAsync<string>(key, cancellationToken);
                    }
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var totalOperations = concurrentTasks * operationsPerTask;
        var throughput = totalOperations / stopwatch.Elapsed.TotalSeconds;

        context.RecordMetric("concurrency.throughput", throughput);
        context.RecordMetric("concurrency.avg_latency", stopwatch.Elapsed.TotalMilliseconds / totalOperations);
        context.RecordMetric("concurrency.parallelism", concurrentTasks);
    }
}

/// <summary>
/// Memory allocation performance benchmark.
/// </summary>
public class MemoryAllocationBenchmark : BenchmarkBase
{
    public MemoryAllocationBenchmark() : base(
        "Memory Allocation Benchmark",
        BenchmarkCategory.Memory,
        "Measures memory allocation and garbage collection performance",
        TimeSpan.FromMinutes(2))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        var bufferManager = integration.BufferManager;
        var iterations = 10000;

        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        // Perform memory allocations
        var buffers = new List<object>();
        for (int i = 0; i < iterations; i++)
        {
            var size = (i % 10 + 1) * 1024; // 1KB to 10KB buffers
            var buffer = bufferManager.GetBuffer(size);
            buffers.Add(buffer);

            // Periodically release some buffers
            if (i % 100 == 0 && buffers.Count > 50)
            {
                for (int j = 0; j < 25; j++)
                {
                    if (buffers[j] is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                buffers.RemoveRange(0, 25);
            }

            if (i % 1000 == 0)
            {
                context.RecordMetric("memory.allocation.progress", (double)i / iterations);
            }
        }

        stopwatch.Stop();

        // Clean up remaining buffers
        foreach (var buffer in buffers)
        {
            if (buffer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        var finalMemory = GC.GetTotalMemory(true);
        var memoryUsed = finalMemory - initialMemory;

        var throughput = iterations / stopwatch.Elapsed.TotalSeconds;
        context.RecordMetric("memory.allocation.throughput", throughput);
        context.RecordMetric("memory.allocation.avg_latency", stopwatch.Elapsed.TotalMilliseconds / iterations);
        context.RecordMetric("memory.allocation.memory_used", memoryUsed);

        await Task.CompletedTask;
    }
}

/// <summary>
/// System warmup benchmark.
/// </summary>
public class WarmupBenchmark : BenchmarkBase
{
    public WarmupBenchmark() : base(
        "System Warmup",
        BenchmarkCategory.Integration,
        "Warms up the system before running benchmarks",
        TimeSpan.FromSeconds(30))
    {
    }

    public override async Task RunAsync(IBenchmarkContext context, CancellationToken cancellationToken = default)
    {
        var integration = (IPerformanceIntegration)context.PerformanceIntegration;
        
        // Warm up cache
        var cacheManager = integration.CacheManager;
        for (int i = 0; i < 100; i++)
        {
            await cacheManager.SetAsync($"warmup-key-{i}", $"warmup-value-{i}", TimeSpan.FromMinutes(1), cancellationToken);
            await cacheManager.GetAsync<string>($"warmup-key-{i}", cancellationToken);
        }

        // Warm up indexes
        var indexManager = integration.IndexManager;
        var warmupIndex = indexManager.GetOrCreateIndex<string, string>("warmup-index");
        for (int i = 0; i < 100; i++)
        {
            warmupIndex.Put($"warmup-index-key-{i}", $"warmup-index-value-{i}");
            warmupIndex.TryGet($"warmup-index-key-{i}", out _);
        }

        // Warm up memory management
        var bufferManager = integration.BufferManager;
        var warmupBuffers = new List<object>();
        for (int i = 0; i < 50; i++)
        {
            var buffer = bufferManager.GetBuffer(1024);
            warmupBuffers.Add(buffer);
        }

        // Clean up warmup buffers
        foreach (var buffer in warmupBuffers)
        {
            if (buffer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        context.RecordMetric("warmup.completed", 1.0);
    }
}
