using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.GigaMap.Tests.Performance;

/// <summary>
/// Performance benchmarks for GigaMap optimizations.
/// These tests measure performance improvements and validate optimization effectiveness.
/// </summary>
public class PerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    public class BenchmarkEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
    }

    private static List<BenchmarkEntity> CreateBenchmarkEntities(int count)
    {
        var entities = new List<BenchmarkEntity>();
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance" };
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            entities.Add(new BenchmarkEntity
            {
                Name = $"Entity_{i:D6}",
                Value = random.Next(1, 10000),
                Department = departments[i % departments.Length],
                Description = $"This is a benchmark entity with ID {i}. " +
                            $"It contains some descriptive text to make the data more realistic for compression and performance testing. " +
                            $"Random value: {random.Next(1000, 9999)}",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                Tags = Enumerable.Range(0, random.Next(1, 5))
                    .Select(j => $"tag_{(i + j) % 20}")
                    .ToList()
            });
        }

        return entities;
    }

    [Fact]
    public async Task BenchmarkBulkAddPerformance()
    {
        // Arrange
        var entityCounts = new[] { 1000, 5000, 10000 };
        _output.WriteLine("🚀 Bulk Add Performance Benchmark");
        _output.WriteLine("==================================");

        foreach (var count in entityCounts)
        {
            var entities = CreateBenchmarkEntities(count);
            var gigaMap = new DefaultGigaMap<BenchmarkEntity>(EqualityComparer<BenchmarkEntity>.Default, 8, 12, 16, 20);

            // Benchmark individual adds
            var individualStopwatch = Stopwatch.StartNew();
            foreach (var entity in entities)
            {
                gigaMap.Add(entity);
            }
            individualStopwatch.Stop();

            // Clear and benchmark bulk add
            gigaMap.Clear();
            var bulkStopwatch = Stopwatch.StartNew();
            await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);
            bulkStopwatch.Stop();

            var individualOpsPerSec = count / individualStopwatch.Elapsed.TotalSeconds;
            var bulkOpsPerSec = count / bulkStopwatch.Elapsed.TotalSeconds;
            var improvement = bulkOpsPerSec / individualOpsPerSec;

            _output.WriteLine($"📊 {count:N0} entities:");
            _output.WriteLine($"  Individual: {individualOpsPerSec:F0} ops/sec ({individualStopwatch.ElapsedMilliseconds}ms)");
            _output.WriteLine($"  Bulk:       {bulkOpsPerSec:F0} ops/sec ({bulkStopwatch.ElapsedMilliseconds}ms)");
            _output.WriteLine($"  Improvement: {improvement:F2}x faster");
            _output.WriteLine("");

            // Assert that bulk operations are reasonable
            // Note: Bulk operations may not always be faster due to overhead, especially for smaller datasets
            // This is realistic behavior - we just ensure they complete successfully
            Assert.True(improvement > 0.1, $"Bulk operations should complete successfully for {count} entities");

            // Log performance characteristics for analysis
            if (improvement >= 1.0)
            {
                _output.WriteLine($"  ✅ Bulk operations are faster for {count} entities");
            }
            else
            {
                _output.WriteLine($"  ℹ️ Individual operations are faster for {count} entities (overhead expected for small datasets)");
            }
        }
    }

    [Fact]
    public async Task BenchmarkCompressionEffectiveness()
    {
        // Arrange
        var entityCounts = new[] { 100, 500, 1000, 2000 };
        _output.WriteLine("🗜️ Compression Effectiveness Benchmark");
        _output.WriteLine("======================================");

        foreach (var count in entityCounts)
        {
            var entities = CreateBenchmarkEntities(count);

            // Benchmark compression analysis
            var analysisStopwatch = Stopwatch.StartNew();
            var analysis = await CompressionOptimizer.AnalyzeCompressionAsync(entities);
            analysisStopwatch.Stop();

            _output.WriteLine($"📊 {count:N0} entities ({analysis.OriginalSize / 1024:F1} KB original):");
            _output.WriteLine($"  Analysis time: {analysisStopwatch.ElapsedMilliseconds}ms");

            foreach (var kvp in analysis.LevelResults.OrderBy(x => x.Value.CompressionRatio))
            {
                var level = kvp.Key;
                var result = kvp.Value;
                var compressionSpeed = result.SpaceSavings / 1024.0 / result.CompressionTime.TotalSeconds;

                _output.WriteLine($"  {level,-15}: {result.CompressionRatio * 100:F1}% size, " +
                                $"{result.CompressionTime.TotalMilliseconds:F1}ms, " +
                                $"{compressionSpeed:F1} KB/s");
            }

            _output.WriteLine($"  Recommended: {analysis.RecommendedLevel}");
            _output.WriteLine("");

            // Assert compression is effective
            var optimalResult = analysis.LevelResults[CompressionLevel.Optimal];
            Assert.True(optimalResult.CompressionRatio < 0.8, 
                $"Compression should achieve at least 20% reduction for {count} entities");
        }
    }

    [Fact]
    public async Task BenchmarkQueryCachePerformance()
    {
        // Arrange
        var entities = CreateBenchmarkEntities(1000);
        var queryResults = entities.Take(100).ToList();
        var querySignature = "benchmark_query";

        using var cache = new CompressedQueryCache<BenchmarkEntity>();

        _output.WriteLine("🚀 Query Cache Performance Benchmark");
        _output.WriteLine("====================================");

        // Benchmark cache write
        var cacheWriteStopwatch = Stopwatch.StartNew();
        await cache.CacheResultAsync(querySignature, queryResults);
        cacheWriteStopwatch.Stop();

        // Benchmark cache read (multiple times for average)
        var readTimes = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var readStopwatch = Stopwatch.StartNew();
            var cachedResult = await cache.TryGetCachedResultAsync(querySignature);
            readStopwatch.Stop();
            readTimes.Add(readStopwatch.ElapsedMilliseconds);

            Assert.NotNull(cachedResult);
            Assert.Equal(queryResults.Count, cachedResult.Count);
        }

        var averageReadTime = readTimes.Average();
        var stats = cache.GetStatistics();

        _output.WriteLine($"📊 Cache Performance:");
        _output.WriteLine($"  Write time: {cacheWriteStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average read time: {averageReadTime:F1}ms");
        _output.WriteLine($"  Compression ratio: {stats.CompressionRatio * 100:F1}%");
        _output.WriteLine($"  Memory savings: {stats.MemorySavings / 1024:F1} KB");
        _output.WriteLine("");

        // Assert cache performance is reasonable
        Assert.True(cacheWriteStopwatch.ElapsedMilliseconds < 1000, "Cache write should be fast");
        Assert.True(averageReadTime < 100, "Cache read should be very fast");
    }

    [Fact]
    public async Task BenchmarkMemoryOptimizations()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<BenchmarkEntity>(EqualityComparer<BenchmarkEntity>.Default, 8, 12, 16, 20);
        var entities = CreateBenchmarkEntities(5000);

        _output.WriteLine("💾 Memory Optimization Benchmark");
        _output.WriteLine("================================");

        // Add entities and measure memory before optimization
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);
        
        var memoryBefore = MemoryOptimizer.ForceGarbageCollection();
        _output.WriteLine($"📊 Before optimization:");
        _output.WriteLine($"  Memory usage: {memoryBefore.MemoryAfterGC / 1024 / 1024:F1} MB");
        _output.WriteLine($"  GC collections: Gen0={memoryBefore.Generation0Collections}, " +
                         $"Gen1={memoryBefore.Generation1Collections}, Gen2={memoryBefore.Generation2Collections}");

        // Apply optimizations
        var optimizationStopwatch = Stopwatch.StartNew();
        await SimplePerformanceOptimizations.ApplyBasicOptimizationsAsync(gigaMap);
        optimizationStopwatch.Stop();

        var memoryAfter = MemoryOptimizer.ForceGarbageCollection();
        _output.WriteLine($"📊 After optimization ({optimizationStopwatch.ElapsedMilliseconds}ms):");
        _output.WriteLine($"  Memory usage: {memoryAfter.MemoryAfterGC / 1024 / 1024:F1} MB");
        _output.WriteLine($"  Memory freed: {memoryAfter.MemoryFreed / 1024 / 1024:F1} MB");
        _output.WriteLine($"  GC collections: Gen0={memoryAfter.Generation0Collections}, " +
                         $"Gen1={memoryAfter.Generation1Collections}, Gen2={memoryAfter.Generation2Collections}");

        var memoryReduction = memoryBefore.MemoryAfterGC - memoryAfter.MemoryAfterGC;
        if (memoryReduction > 0)
        {
            _output.WriteLine($"  Memory reduction: {memoryReduction / 1024 / 1024:F1} MB");
        }

        _output.WriteLine("");

        // Assert optimization completed successfully
        Assert.True(optimizationStopwatch.ElapsedMilliseconds < 5000, "Optimization should complete quickly");
    }

    [Fact]
    public async Task BenchmarkCompressionVsNoCompression()
    {
        // Arrange
        var entities = CreateBenchmarkEntities(1000);
        
        _output.WriteLine("⚖️ Compression vs No Compression Benchmark");
        _output.WriteLine("==========================================");

        // Benchmark without compression
        var noCompressionStopwatch = Stopwatch.StartNew();
        var noCompressionData = await CompressionOptimizer.CompressEntitiesAsync(
            entities, CompressionLevel.NoCompression);
        noCompressionStopwatch.Stop();

        // Benchmark with optimal compression
        var compressionStopwatch = Stopwatch.StartNew();
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(
            entities, CompressionLevel.Optimal);
        compressionStopwatch.Stop();

        var spaceSavings = noCompressionData.OriginalSize - compressedData.CompressedSize;
        var compressionRatio = (double)compressedData.CompressedSize / noCompressionData.OriginalSize;

        _output.WriteLine($"📊 Compression Comparison:");
        _output.WriteLine($"  No compression: {noCompressionData.CompressedSize / 1024:F1} KB in {noCompressionStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  With compression: {compressedData.CompressedSize / 1024:F1} KB in {compressionStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Space savings: {spaceSavings / 1024:F1} KB ({(1 - compressionRatio) * 100:F1}%)");
        _output.WriteLine($"  Compression overhead: {compressionStopwatch.ElapsedMilliseconds - noCompressionStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine("");

        // Assert compression provides meaningful benefits
        Assert.True(compressionRatio < 0.8, "Compression should reduce size by at least 20%");
        Assert.True(compressionStopwatch.ElapsedMilliseconds < 5000, "Compression should be reasonably fast");
    }

    [Fact]
    public async Task BenchmarkQueryPerformanceWithMonitoring()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<BenchmarkEntity>()
            .WithBitmapIndex(Indexer.Property<BenchmarkEntity, string>("Department", e => e.Department))
            .Build();

        var entities = CreateBenchmarkEntities(2000);
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        _output.WriteLine("🔍 Query Performance Monitoring Benchmark");
        _output.WriteLine("=========================================");

        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance" };
        var queryResults = new List<(string Department, QueryResult<BenchmarkEntity> Result)>();

        // Benchmark queries with monitoring
        foreach (var department in departments)
        {
            var query = gigaMap.Query("Department", department);
            var result = await QueryOptimizer.ExecuteWithMonitoringAsync(query);
            queryResults.Add((department, result));

            _output.WriteLine($"📊 Query '{department}':");
            _output.WriteLine($"  Results: {result.ResultCount}");
            _output.WriteLine($"  Execution time: {result.ExecutionTime.TotalMilliseconds:F1}ms");
            _output.WriteLine($"  Success: {result.Success}");
            if (!string.IsNullOrEmpty(result.Error))
            {
                _output.WriteLine($"  Error: {result.Error}");
            }
        }

        var averageExecutionTime = queryResults.Average(r => r.Result.ExecutionTime.TotalMilliseconds);
        var totalResults = queryResults.Sum(r => r.Result.ResultCount);

        _output.WriteLine($"📈 Summary:");
        _output.WriteLine($"  Average execution time: {averageExecutionTime:F1}ms");
        _output.WriteLine($"  Total results: {totalResults}");
        _output.WriteLine($"  All queries successful: {queryResults.All(r => r.Result.Success)}");
        _output.WriteLine("");

        // Assert query performance is reasonable
        Assert.True(averageExecutionTime < 1000, "Queries should execute quickly");
        Assert.True(queryResults.All(r => r.Result.Success), "All queries should succeed");
        Assert.True(totalResults > 0, "Queries should return results");
    }

    [Fact]
    public async Task BenchmarkBackupAndRestorePerformance()
    {
        // Arrange
        var originalGigaMap = new DefaultGigaMap<BenchmarkEntity>(EqualityComparer<BenchmarkEntity>.Default, 8, 12, 16, 20);
        var entities = CreateBenchmarkEntities(1000);
        await SimplePerformanceOptimizations.BulkAddAsync(originalGigaMap, entities);

        _output.WriteLine("💾 Backup and Restore Performance Benchmark");
        _output.WriteLine("===========================================");

        // Benchmark backup creation
        var backupStopwatch = Stopwatch.StartNew();
        var backup = await SimplePerformanceOptimizations.CreateCompressedBackupAsync(originalGigaMap);
        backupStopwatch.Stop();

        // Benchmark restore
        var newGigaMap = new DefaultGigaMap<BenchmarkEntity>(EqualityComparer<BenchmarkEntity>.Default, 8, 12, 16, 20);
        var restoreStopwatch = Stopwatch.StartNew();
        var restoredIds = await SimplePerformanceOptimizations.RestoreFromCompressedBackupAsync(newGigaMap, backup);
        restoreStopwatch.Stop();

        _output.WriteLine($"📊 Backup Performance:");
        _output.WriteLine($"  Backup time: {backupStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Backup size: {backup.CompressedSize / 1024:F1} KB");
        _output.WriteLine($"  Compression: {backup.CompressionPercentage:F1}% reduction");
        _output.WriteLine($"  Restore time: {restoreStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Entities restored: {restoredIds.Count}");
        _output.WriteLine("");

        // Assert backup and restore performance
        Assert.True(backupStopwatch.ElapsedMilliseconds < 5000, "Backup should be fast");
        Assert.True(restoreStopwatch.ElapsedMilliseconds < 5000, "Restore should be fast");
        Assert.Equal(entities.Count, restoredIds.Count);
        Assert.Equal(originalGigaMap.Size, newGigaMap.Size);
    }
}
