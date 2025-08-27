using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.Benchmarks.Implementations;
using NebulaStore.Benchmarks.Models;

namespace NebulaStore.Benchmarks;

/// <summary>
/// Main benchmark runner that executes performance tests and generates reports.
/// </summary>
public class BenchmarkRunner : IDisposable
{
    private readonly BenchmarkConfig _config;
    private readonly List<IBenchmark> _benchmarks;

    public BenchmarkRunner(BenchmarkConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _benchmarks = new List<IBenchmark>();
    }

    /// <summary>
    /// Add a benchmark implementation to the runner.
    /// </summary>
    public BenchmarkRunner AddBenchmark(IBenchmark benchmark)
    {
        _benchmarks.Add(benchmark ?? throw new ArgumentNullException(nameof(benchmark)));
        return this;
    }

    /// <summary>
    /// Run all benchmarks and return comprehensive results.
    /// </summary>
    public async Task<List<BenchmarkSuite>> RunAllBenchmarksAsync()
    {
        Console.WriteLine("🚀 Starting Performance Benchmark Suite");
        Console.WriteLine("========================================");
        Console.WriteLine($"Total Records: {_config.TotalRecords:N0}");
        Console.WriteLine($"Batch Size: {_config.BatchSize:N0}");
        Console.WriteLine($"Query Count: {_config.QueryCount:N0}");
        Console.WriteLine($"Warmup Iterations: {_config.WarmupIterations}");
        Console.WriteLine($"Benchmark Iterations: {_config.BenchmarkIterations}");
        Console.WriteLine();

        var results = new List<BenchmarkSuite>();

        foreach (var benchmark in _benchmarks)
        {
            Console.WriteLine($"🔧 Running benchmark: {benchmark.Name}");
            Console.WriteLine(new string('-', 50));

            try
            {
                var suite = await RunSingleBenchmarkAsync(benchmark);
                results.Add(suite);
                
                Console.WriteLine($"✅ {benchmark.Name} benchmark completed");
                Console.WriteLine($"   Peak Memory: {suite.PeakMemoryUsage / 1024 / 1024:F1} MB");
                Console.WriteLine($"   Storage Size: {suite.FinalStorageSize / 1024 / 1024:F1} MB");
                Console.WriteLine($"   Total Time: {suite.TotalDuration.TotalMinutes:F1} minutes");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {benchmark.Name} benchmark failed: {ex.Message}");
                Console.WriteLine();
            }
        }

        return results;
    }

    /// <summary>
    /// Run a single benchmark implementation.
    /// </summary>
    private async Task<BenchmarkSuite> RunSingleBenchmarkAsync(IBenchmark benchmark)
    {
        var suite = new BenchmarkSuite
        {
            Name = benchmark.Name,
            Config = _config
        };

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            // Initialize benchmark
            await benchmark.InitializeAsync(_config);
            await benchmark.PrepareAsync();

            // Generate test data
            Console.WriteLine("📊 Generating test data...");
            var customers = DataGenerator.GenerateCustomers(_config.TotalRecords);
            var products = DataGenerator.GenerateProducts(Math.Min(10000, _config.TotalRecords / 100)); // 1% of customers
            var orders = DataGenerator.GenerateOrders(customers.Take(Math.Min(customers.Count, _config.TotalRecords / 3)).ToList());
            var orderItems = DataGenerator.GenerateOrderItems(orders, products);

            Console.WriteLine($"   Generated {customers.Count:N0} customers");
            Console.WriteLine($"   Generated {products.Count:N0} products");
            Console.WriteLine($"   Generated {orders.Count:N0} orders");
            Console.WriteLine($"   Generated {orderItems.Count:N0} order items");
            Console.WriteLine();

            // Warmup phase
            if (_config.WarmupIterations > 0)
            {
                Console.WriteLine($"🔥 Warmup phase ({_config.WarmupIterations} iterations)...");
                await RunWarmupAsync(benchmark, customers.Take(1000).ToList());
                Console.WriteLine("   Warmup completed");
                Console.WriteLine();
            }

            // Run insert benchmarks
            Console.WriteLine("📝 Running insert benchmarks...");
            await RunInsertBenchmarksAsync(benchmark, suite, customers, products, orders, orderItems);

            // Run query benchmarks
            Console.WriteLine("🔍 Running query benchmarks...");
            await RunQueryBenchmarksAsync(benchmark, suite, customers, products, orders);

            // Measure final state
            suite.PeakMemoryUsage = await benchmark.GetMemoryUsageAsync();
            suite.FinalStorageSize = await benchmark.GetStorageSizeAsync();
        }
        finally
        {
            totalStopwatch.Stop();
            suite.TotalDuration = totalStopwatch.Elapsed;
        }

        return suite;
    }

    /// <summary>
    /// Run warmup iterations to stabilize performance.
    /// </summary>
    private async Task RunWarmupAsync(IBenchmark benchmark, List<Customer> sampleCustomers)
    {
        for (int i = 0; i < _config.WarmupIterations; i++)
        {
            await benchmark.CleanupAsync();
            await benchmark.InsertBatchAsync(sampleCustomers);
            
            var sampleIds = sampleCustomers.Take(100).Select(c => c.Id).ToList();
            await benchmark.QueryByIdAsync<Customer>(sampleIds);
        }
        
        await benchmark.CleanupAsync();
    }

    /// <summary>
    /// Run insert performance benchmarks.
    /// </summary>
    private async Task RunInsertBenchmarksAsync(IBenchmark benchmark, BenchmarkSuite suite, 
        List<Customer> customers, List<Product> products, List<Order> orders, List<OrderItem> orderItems)
    {
        // Insert customers in batches
        Console.WriteLine("   Inserting customers...");
        var customerBatches = customers.Chunk(_config.BatchSize);
        foreach (var batch in customerBatches)
        {
            var result = await benchmark.InsertBatchAsync(batch.ToList());
            suite.InsertResults.Add(result);
            
            if (_config.VerboseLogging)
            {
                Console.WriteLine($"     Batch: {result.RecordCount} records, {result.OperationsPerSecond:F0} ops/sec");
            }
        }

        // Insert products
        Console.WriteLine("   Inserting products...");
        var productBatches = products.Chunk(_config.BatchSize);
        foreach (var batch in productBatches)
        {
            var result = await benchmark.InsertBatchAsync(batch.ToList());
            suite.InsertResults.Add(result);
        }

        // Insert orders
        Console.WriteLine("   Inserting orders...");
        var orderBatches = orders.Chunk(_config.BatchSize);
        foreach (var batch in orderBatches)
        {
            var result = await benchmark.InsertBatchAsync(batch.ToList());
            suite.InsertResults.Add(result);
        }

        // Insert order items
        Console.WriteLine("   Inserting order items...");
        var orderItemBatches = orderItems.Chunk(_config.BatchSize);
        foreach (var batch in orderItemBatches)
        {
            var result = await benchmark.InsertBatchAsync(batch.ToList());
            suite.InsertResults.Add(result);
        }

        var totalInserted = suite.InsertResults.Sum(r => r.RecordCount);
        var avgInsertOps = suite.AverageInsertOpsPerSecond;
        Console.WriteLine($"   Total inserted: {totalInserted:N0} records");
        Console.WriteLine($"   Average speed: {avgInsertOps:F0} ops/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// Run query performance benchmarks.
    /// </summary>
    private async Task RunQueryBenchmarksAsync(IBenchmark benchmark, BenchmarkSuite suite,
        List<Customer> customers, List<Product> products, List<Order> orders)
    {
        var random = new Random(42);

        // Query by ID benchmarks
        Console.WriteLine("   Running ID queries...");
        for (int i = 0; i < _config.BenchmarkIterations; i++)
        {
            var customerIds = customers.OrderBy(x => random.Next()).Take(_config.QueryCount).Select(c => c.Id).ToList();
            var result = await benchmark.QueryByIdAsync<Customer>(customerIds);
            suite.QueryByIdResults.Add(result);
        }

        // Filter query benchmarks
        Console.WriteLine("   Running filter queries...");
        for (int i = 0; i < _config.BenchmarkIterations; i++)
        {
            var result = await benchmark.QueryWithFilterAsync<Customer>(c => c.IsActive && c.TotalSpent > 1000);
            suite.QueryFilterResults.Add(result);
        }

        // Complex query benchmarks
        Console.WriteLine("   Running complex queries...");
        for (int i = 0; i < _config.BenchmarkIterations; i++)
        {
            var result = await benchmark.QueryComplexAsync<Customer>();
            suite.QueryComplexResults.Add(result);
        }

        Console.WriteLine($"   ID queries: {suite.AverageQueryByIdOpsPerSecond:F0} ops/sec");
        Console.WriteLine($"   Filter queries: {suite.AverageQueryFilterOpsPerSecond:F0} ops/sec");
        Console.WriteLine($"   Complex queries: {suite.AverageQueryComplexOpsPerSecond:F0} ops/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// Dispose of all benchmark resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var benchmark in _benchmarks)
        {
            benchmark?.Dispose();
        }
        _benchmarks.Clear();
    }
}

/// <summary>
/// Extension methods for collections.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Split a collection into chunks of specified size.
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
    {
        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));

        var list = source.ToList();
        for (int i = 0; i < list.Count; i += chunkSize)
        {
            yield return list.Skip(i).Take(chunkSize);
        }
    }
}
