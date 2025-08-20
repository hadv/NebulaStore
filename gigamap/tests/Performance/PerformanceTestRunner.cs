using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.GigaMap.Tests.Performance;

/// <summary>
/// Comprehensive performance test runner for GigaMap optimizations.
/// </summary>
public class PerformanceTestRunner
{
    public class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Runs a comprehensive performance test suite.
    /// </summary>
    public static async Task<PerformanceTestResults> RunComprehensiveTestsAsync(
        int entityCount = 5000,
        bool verbose = true)
    {
        var results = new PerformanceTestResults
        {
            EntityCount = entityCount,
            StartTime = DateTime.UtcNow
        };

        if (verbose)
        {
            Console.WriteLine("🚀 GigaMap Performance Test Suite");
            Console.WriteLine("==================================");
            Console.WriteLine($"Testing with {entityCount:N0} entities");
            Console.WriteLine();
        }

        try
        {
            // Test 1: Basic Operations Performance
            results.BasicOperationsResult = await TestBasicOperationsAsync(entityCount, verbose);

            // Test 2: Compression Performance
            results.CompressionResult = await TestCompressionPerformanceAsync(entityCount, verbose);

            // Test 3: Query Cache Performance
            results.QueryCacheResult = await TestQueryCachePerformanceAsync(entityCount, verbose);

            // Test 4: Memory Optimization
            results.MemoryOptimizationResult = await TestMemoryOptimizationAsync(entityCount, verbose);

            // Test 5: Bulk Operations
            results.BulkOperationsResult = await TestBulkOperationsAsync(entityCount, verbose);

            results.Success = true;
            results.EndTime = DateTime.UtcNow;

            if (verbose)
            {
                PrintSummary(results);
            }
        }
        catch (Exception ex)
        {
            results.Success = false;
            results.Error = ex.Message;
            results.EndTime = DateTime.UtcNow;

            if (verbose)
            {
                Console.WriteLine($"❌ Test suite failed: {ex.Message}");
            }
        }

        return results;
    }

    private static Task<BasicOperationsTestResult> TestBasicOperationsAsync(int entityCount, bool verbose)
    {
        if (verbose) Console.WriteLine("📊 Testing Basic Operations Performance...");

        var entities = CreateTestEntities(entityCount);
        var gigaMap = GigaMap.Builder<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Category", e => e.Category))
            .Build();

        var result = new BasicOperationsTestResult();

        // Test Add operations
        var addStopwatch = Stopwatch.StartNew();
        foreach (var entity in entities.Take(1000))
        {
            gigaMap.Add(entity);
        }
        addStopwatch.Stop();
        result.AddOperationsPerSecond = 1000 / addStopwatch.Elapsed.TotalSeconds;

        // Test Get operations
        var getStopwatch = Stopwatch.StartNew();
        for (long i = 0; i < 1000; i++)
        {
            gigaMap.Get(i);
        }
        getStopwatch.Stop();
        result.GetOperationsPerSecond = 1000 / getStopwatch.Elapsed.TotalSeconds;

        // Test Query operations
        var queryStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var queryResults = gigaMap.Query("Category", "Category_0").Execute();
        }
        queryStopwatch.Stop();
        result.QueryOperationsPerSecond = 100 / queryStopwatch.Elapsed.TotalSeconds;

        if (verbose)
        {
            Console.WriteLine($"  Add: {result.AddOperationsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Get: {result.GetOperationsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Query: {result.QueryOperationsPerSecond:F0} ops/sec");
            Console.WriteLine();
        }

        return Task.FromResult(result);
    }

    private static async Task<CompressionTestResult> TestCompressionPerformanceAsync(int entityCount, bool verbose)
    {
        if (verbose) Console.WriteLine("🗜️ Testing Compression Performance...");

        var entities = CreateTestEntities(Math.Min(entityCount, 1000)); // Limit for compression test
        var result = new CompressionTestResult();

        // Test compression analysis
        var analysisStopwatch = Stopwatch.StartNew();
        var analysis = await CompressionOptimizer.AnalyzeCompressionAsync(entities);
        analysisStopwatch.Stop();

        result.AnalysisTimeMs = analysisStopwatch.ElapsedMilliseconds;
        result.OriginalSizeKB = analysis.OriginalSize / 1024.0;
        result.BestCompressionRatio = analysis.BestCompressionRatio;
        result.RecommendedLevel = analysis.RecommendedLevel;

        // Test entity compression
        var compressionStopwatch = Stopwatch.StartNew();
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(entities);
        compressionStopwatch.Stop();

        result.CompressionTimeMs = compressionStopwatch.ElapsedMilliseconds;
        result.CompressedSizeKB = compressedData.CompressedSize / 1024.0;
        result.CompressionRatio = compressedData.CompressionRatio;

        // Test decompression
        var decompressionStopwatch = Stopwatch.StartNew();
        var decompressedEntities = await CompressionOptimizer.DecompressEntitiesAsync(compressedData);
        decompressionStopwatch.Stop();

        result.DecompressionTimeMs = decompressionStopwatch.ElapsedMilliseconds;
        result.DataIntegrityValid = decompressedEntities.Count == entities.Count;

        if (verbose)
        {
            Console.WriteLine($"  Analysis: {result.AnalysisTimeMs}ms");
            Console.WriteLine($"  Compression: {result.OriginalSizeKB:F1} KB → {result.CompressedSizeKB:F1} KB ({result.CompressionRatio * 100:F1}%)");
            Console.WriteLine($"  Time: {result.CompressionTimeMs}ms compress, {result.DecompressionTimeMs}ms decompress");
            Console.WriteLine($"  Recommended level: {result.RecommendedLevel}");
            Console.WriteLine();
        }

        return result;
    }

    private static async Task<QueryCacheTestResult> TestQueryCachePerformanceAsync(int entityCount, bool verbose)
    {
        if (verbose) Console.WriteLine("🚀 Testing Query Cache Performance...");

        var entities = CreateTestEntities(Math.Min(entityCount, 500));
        var result = new QueryCacheTestResult();

        using var cache = new CompressedQueryCache<TestEntity>();

        // Test cache write
        var cacheWriteStopwatch = Stopwatch.StartNew();
        await cache.CacheResultAsync("test_query", entities);
        cacheWriteStopwatch.Stop();
        result.CacheWriteTimeMs = cacheWriteStopwatch.ElapsedMilliseconds;

        // Test cache read (multiple times for average)
        var readTimes = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var readStopwatch = Stopwatch.StartNew();
            var cachedResult = await cache.TryGetCachedResultAsync("test_query");
            readStopwatch.Stop();
            readTimes.Add(readStopwatch.ElapsedMilliseconds);
        }

        result.AverageCacheReadTimeMs = readTimes.Average();

        var stats = cache.GetStatistics();
        result.CompressionRatio = stats.CompressionRatio;
        result.MemorySavingsKB = stats.MemorySavings / 1024.0;

        if (verbose)
        {
            Console.WriteLine($"  Cache write: {result.CacheWriteTimeMs}ms");
            Console.WriteLine($"  Cache read: {result.AverageCacheReadTimeMs:F1}ms average");
            Console.WriteLine($"  Compression: {result.CompressionRatio * 100:F1}%");
            Console.WriteLine($"  Memory savings: {result.MemorySavingsKB:F1} KB");
            Console.WriteLine();
        }

        return result;
    }

    private static async Task<MemoryOptimizationTestResult> TestMemoryOptimizationAsync(int entityCount, bool verbose)
    {
        if (verbose) Console.WriteLine("💾 Testing Memory Optimization...");

        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(Math.Min(entityCount, 2000));
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        var result = new MemoryOptimizationTestResult();

        // Measure memory before optimization
        var memoryBefore = MemoryOptimizer.ForceGarbageCollection();
        result.MemoryBeforeOptimizationMB = memoryBefore.MemoryAfterGC / 1024.0 / 1024.0;

        // Apply optimizations
        var optimizationStopwatch = Stopwatch.StartNew();
        await SimplePerformanceOptimizations.ApplyBasicOptimizationsAsync(gigaMap);
        optimizationStopwatch.Stop();
        result.OptimizationTimeMs = optimizationStopwatch.ElapsedMilliseconds;

        // Measure memory after optimization
        var memoryAfter = MemoryOptimizer.ForceGarbageCollection();
        result.MemoryAfterOptimizationMB = memoryAfter.MemoryAfterGC / 1024.0 / 1024.0;
        result.MemoryFreedMB = memoryAfter.MemoryFreed / 1024.0 / 1024.0;

        if (verbose)
        {
            Console.WriteLine($"  Before: {result.MemoryBeforeOptimizationMB:F1} MB");
            Console.WriteLine($"  After: {result.MemoryAfterOptimizationMB:F1} MB");
            Console.WriteLine($"  Freed: {result.MemoryFreedMB:F1} MB");
            Console.WriteLine($"  Optimization time: {result.OptimizationTimeMs}ms");
            Console.WriteLine();
        }

        return result;
    }

    private static async Task<BulkOperationsTestResult> TestBulkOperationsAsync(int entityCount, bool verbose)
    {
        if (verbose) Console.WriteLine("⚡ Testing Bulk Operations Performance...");

        var entities = CreateTestEntities(Math.Min(entityCount, 2000));
        var result = new BulkOperationsTestResult();

        // Test individual adds
        var individualGigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var individualStopwatch = Stopwatch.StartNew();
        foreach (var entity in entities)
        {
            individualGigaMap.Add(entity);
        }
        individualStopwatch.Stop();
        result.IndividualAddOperationsPerSecond = entities.Count / individualStopwatch.Elapsed.TotalSeconds;

        // Test bulk adds
        var bulkGigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var bulkStopwatch = Stopwatch.StartNew();
        await SimplePerformanceOptimizations.BulkAddAsync(bulkGigaMap, entities);
        bulkStopwatch.Stop();
        result.BulkAddOperationsPerSecond = entities.Count / bulkStopwatch.Elapsed.TotalSeconds;

        result.BulkImprovementFactor = result.BulkAddOperationsPerSecond / result.IndividualAddOperationsPerSecond;

        if (verbose)
        {
            Console.WriteLine($"  Individual: {result.IndividualAddOperationsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Bulk: {result.BulkAddOperationsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Improvement: {result.BulkImprovementFactor:F2}x faster");
            Console.WriteLine();
        }

        return result;
    }

    private static void PrintSummary(PerformanceTestResults results)
    {
        Console.WriteLine("📈 Performance Test Summary");
        Console.WriteLine("===========================");
        Console.WriteLine($"Test Duration: {results.Duration.TotalSeconds:F1} seconds");
        Console.WriteLine($"Entity Count: {results.EntityCount:N0}");
        Console.WriteLine($"Success: {(results.Success ? "✅" : "❌")}");
        Console.WriteLine();

        if (results.Success)
        {
            Console.WriteLine("🏆 Key Performance Metrics:");
            Console.WriteLine($"  Bulk operations improvement: {results.BulkOperationsResult.BulkImprovementFactor:F2}x");
            Console.WriteLine($"  Compression ratio: {results.CompressionResult.CompressionRatio * 100:F1}%");
            Console.WriteLine($"  Cache read time: {results.QueryCacheResult.AverageCacheReadTimeMs:F1}ms");
            Console.WriteLine($"  Memory freed: {results.MemoryOptimizationResult.MemoryFreedMB:F1} MB");
        }
        else
        {
            Console.WriteLine($"❌ Error: {results.Error}");
        }

        Console.WriteLine();
    }

    private static List<TestEntity> CreateTestEntities(int count)
    {
        var entities = new List<TestEntity>();
        var categories = new[] { "Category_0", "Category_1", "Category_2", "Category_3", "Category_4" };
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            entities.Add(new TestEntity
            {
                Name = $"Entity_{i:D6}",
                Value = random.Next(1, 10000),
                Category = categories[i % categories.Length],
                Description = $"Test entity {i} with some descriptive text for compression testing. Random: {random.Next()}",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                Tags = Enumerable.Range(0, random.Next(1, 4))
                    .Select(j => $"tag_{(i + j) % 10}")
                    .ToList()
            });
        }

        return entities;
    }
}

/// <summary>
/// Results from comprehensive performance tests.
/// </summary>
public class PerformanceTestResults
{
    public int EntityCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public bool Success { get; set; }
    public string? Error { get; set; }

    public BasicOperationsTestResult BasicOperationsResult { get; set; } = new();
    public CompressionTestResult CompressionResult { get; set; } = new();
    public QueryCacheTestResult QueryCacheResult { get; set; } = new();
    public MemoryOptimizationTestResult MemoryOptimizationResult { get; set; } = new();
    public BulkOperationsTestResult BulkOperationsResult { get; set; } = new();
}

/// <summary>
/// Results from basic operations performance test.
/// </summary>
public class BasicOperationsTestResult
{
    public double AddOperationsPerSecond { get; set; }
    public double GetOperationsPerSecond { get; set; }
    public double QueryOperationsPerSecond { get; set; }
}

/// <summary>
/// Results from compression performance test.
/// </summary>
public class CompressionTestResult
{
    public long AnalysisTimeMs { get; set; }
    public double OriginalSizeKB { get; set; }
    public double BestCompressionRatio { get; set; }
    public System.IO.Compression.CompressionLevel RecommendedLevel { get; set; }
    public long CompressionTimeMs { get; set; }
    public double CompressedSizeKB { get; set; }
    public double CompressionRatio { get; set; }
    public long DecompressionTimeMs { get; set; }
    public bool DataIntegrityValid { get; set; }
}

/// <summary>
/// Results from query cache performance test.
/// </summary>
public class QueryCacheTestResult
{
    public long CacheWriteTimeMs { get; set; }
    public double AverageCacheReadTimeMs { get; set; }
    public double CompressionRatio { get; set; }
    public double MemorySavingsKB { get; set; }
}

/// <summary>
/// Results from memory optimization test.
/// </summary>
public class MemoryOptimizationTestResult
{
    public double MemoryBeforeOptimizationMB { get; set; }
    public double MemoryAfterOptimizationMB { get; set; }
    public double MemoryFreedMB { get; set; }
    public long OptimizationTimeMs { get; set; }
}

/// <summary>
/// Results from bulk operations performance test.
/// </summary>
public class BulkOperationsTestResult
{
    public double IndividualAddOperationsPerSecond { get; set; }
    public double BulkAddOperationsPerSecond { get; set; }
    public double BulkImprovementFactor { get; set; }
}
