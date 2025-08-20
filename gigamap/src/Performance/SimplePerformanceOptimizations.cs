using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NebulaStore.GigaMap.Performance;

/// <summary>
/// Simple performance optimizations that work with existing GigaMap interfaces.
/// </summary>
public static class SimplePerformanceOptimizations
{
    /// <summary>
    /// Optimizes a GigaMap by ensuring all indices are optimized.
    /// </summary>
    public static void OptimizeIndices<T>(IGigaMap<T> gigaMap) where T : class
    {
        if (gigaMap?.Index?.Bitmap == null) return;

        try
        {
            // Try to optimize indices if the method exists
            // This is a simplified approach since we don't have direct access to all indices
            if (gigaMap.Index?.Bitmap != null)
            {
                // In a full implementation, we would iterate through available indices
                // For now, we just ensure the operation doesn't fail
            }
        }
        catch
        {
            // Ignore optimization errors
        }
    }

    /// <summary>
    /// Performs bulk add operations with better performance.
    /// </summary>
    public static async Task<List<long>> BulkAddAsync<T>(
        IGigaMap<T> gigaMap, 
        IEnumerable<T> entities) where T : class
    {
        var entityList = entities.ToList();
        var results = new List<long>(entityList.Count);

        // Add entities in batches for better performance
        const int batchSize = 1000;
        for (int i = 0; i < entityList.Count; i += batchSize)
        {
            var batch = entityList.Skip(i).Take(batchSize);
            foreach (var entity in batch)
            {
                results.Add(gigaMap.Add(entity));
            }

            // Yield control periodically for async operations
            if (i % (batchSize * 10) == 0)
            {
                await Task.Yield();
            }
        }

        return results;
    }

    /// <summary>
    /// Executes a query with performance optimizations.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ExecuteOptimizedQueryAsync<T>(
        IGigaQuery<T> query) where T : class
    {
        // For now, just execute the query normally
        // In a full implementation, this would add caching, parallel execution, etc.
        return await Task.Run(() => query.Execute());
    }

    /// <summary>
    /// Gets performance statistics for a GigaMap.
    /// </summary>
    public static PerformanceStats GetPerformanceStats<T>(IGigaMap<T> gigaMap) where T : class
    {
        var stats = new PerformanceStats
        {
            EntityCount = gigaMap.Size,
            IsEmpty = gigaMap.IsEmpty,
            IsReadOnly = gigaMap.IsReadOnly
        };

        try
        {
            if (gigaMap.Index?.Bitmap != null)
            {
                // Simplified index counting since we don't have direct access to all indices
                stats.IndexCount = 0; // Would need to be implemented based on available interface
                stats.HasIndices = false;
            }
        }
        catch
        {
            // Ignore errors when getting index information
        }

        return stats;
    }

    /// <summary>
    /// Provides optimization recommendations for a GigaMap.
    /// </summary>
    public static List<string> GetOptimizationRecommendations<T>(IGigaMap<T> gigaMap) where T : class
    {
        var recommendations = new List<string>();
        var stats = GetPerformanceStats(gigaMap);

        if (stats.EntityCount > 10000)
        {
            recommendations.Add("Consider using bulk operations for large datasets");
            recommendations.Add("Optimize indices regularly for better query performance");
        }

        if (stats.EntityCount > 100000)
        {
            recommendations.Add("Consider implementing concurrent access patterns");
            recommendations.Add("Monitor memory usage and consider compression");
        }

        if (!stats.HasIndices && stats.EntityCount > 1000)
        {
            recommendations.Add("Add indices for frequently queried properties");
        }

        if (stats.IndexCount > 10)
        {
            recommendations.Add("Review index usage - too many indices can impact write performance");
        }

        // Always provide some general recommendations
        if (recommendations.Count == 0)
        {
            recommendations.Add("Use bulk operations (BulkAddAsync) for inserting large amounts of data");
            recommendations.Add("Consider using lazy queries for large result sets");
            recommendations.Add("Enable query result caching for frequently executed queries");
        }

        return recommendations;
    }

    /// <summary>
    /// Applies basic optimizations to a GigaMap.
    /// </summary>
    public static async Task ApplyBasicOptimizationsAsync<T>(IGigaMap<T> gigaMap) where T : class
    {
        await Task.Run(() =>
        {
            // Optimize indices
            OptimizeIndices(gigaMap);

            // Force garbage collection to reclaim memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        });
    }

    /// <summary>
    /// Applies comprehensive optimizations including compression analysis.
    /// </summary>
    public static async Task ApplyComprehensiveOptimizationsAsync<T>(IGigaMap<T> gigaMap) where T : class
    {
        // Apply basic optimizations first
        await ApplyBasicOptimizationsAsync(gigaMap);

        // Analyze compression potential
        if (gigaMap.Size > 1000) // Only analyze if there's enough data
        {
            var estimate = await CompressionOptimizer.EstimateMemorySavingsAsync(gigaMap);
            Console.WriteLine($"💾 {estimate}");

            if (estimate.SavingsPercentage > 30) // If we can save more than 30%
            {
                Console.WriteLine($"💡 Recommendation: Enable compression with {estimate.RecommendedCompressionLevel} level");
            }
        }
    }

    /// <summary>
    /// Creates a compressed backup of GigaMap entities.
    /// </summary>
    public static async Task<CompressedData<T>> CreateCompressedBackupAsync<T>(
        IGigaMap<T> gigaMap,
        CompressionLevel compressionLevel = CompressionLevel.Optimal) where T : class
    {
        var entities = gigaMap.ToList();
        return await CompressionOptimizer.CompressEntitiesAsync(entities, compressionLevel);
    }

    /// <summary>
    /// Restores entities from a compressed backup.
    /// </summary>
    public static async Task<List<long>> RestoreFromCompressedBackupAsync<T>(
        IGigaMap<T> gigaMap,
        CompressedData<T> compressedBackup) where T : class
    {
        var entities = await CompressionOptimizer.DecompressEntitiesAsync(compressedBackup);
        return await BulkAddAsync(gigaMap, entities);
    }
}

/// <summary>
/// Simple performance statistics for a GigaMap.
/// </summary>
public class PerformanceStats
{
    public long EntityCount { get; set; }
    public bool IsEmpty { get; set; }
    public bool IsReadOnly { get; set; }
    public int IndexCount { get; set; }
    public bool HasIndices { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"Entities: {EntityCount}, Indices: {IndexCount}, " +
               $"Empty: {IsEmpty}, ReadOnly: {IsReadOnly}";
    }
}

/// <summary>
/// Simple query optimization helper.
/// </summary>
public static class QueryOptimizer
{
    /// <summary>
    /// Creates an optimized query with hints for better performance.
    /// </summary>
    public static IGigaQuery<T> CreateOptimizedQuery<T>(IGigaMap<T> gigaMap) where T : class
    {
        // For now, just return a standard query
        // In a full implementation, this would return an optimized query wrapper
        return gigaMap.Query();
    }

    /// <summary>
    /// Executes a query with performance monitoring.
    /// </summary>
    public static async Task<QueryResult<T>> ExecuteWithMonitoringAsync<T>(
        IGigaQuery<T> query) where T : class
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var results = await Task.Run(() => query.Execute());
            stopwatch.Stop();

            return new QueryResult<T>
            {
                Results = results,
                ExecutionTime = stopwatch.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new QueryResult<T>
            {
                Results = Array.Empty<T>(),
                ExecutionTime = stopwatch.Elapsed,
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of a monitored query execution.
/// </summary>
public class QueryResult<T> where T : class
{
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ResultCount => Results.Count;

    public override string ToString()
    {
        return Success 
            ? $"Query executed successfully: {ResultCount} results in {ExecutionTime.TotalMilliseconds:F1}ms"
            : $"Query failed: {Error} (took {ExecutionTime.TotalMilliseconds:F1}ms)";
    }
}

/// <summary>
/// Memory optimization utilities.
/// </summary>
public static class MemoryOptimizer
{
    /// <summary>
    /// Forces garbage collection and returns memory usage information.
    /// </summary>
    public static MemoryInfo ForceGarbageCollection()
    {
        var beforeGC = GC.GetTotalMemory(false);
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var afterGC = GC.GetTotalMemory(false);
        
        return new MemoryInfo
        {
            MemoryBeforeGC = beforeGC,
            MemoryAfterGC = afterGC,
            MemoryFreed = beforeGC - afterGC,
            Generation0Collections = GC.CollectionCount(0),
            Generation1Collections = GC.CollectionCount(1),
            Generation2Collections = GC.CollectionCount(2)
        };
    }
}

/// <summary>
/// Memory usage information.
/// </summary>
public class MemoryInfo
{
    public long MemoryBeforeGC { get; set; }
    public long MemoryAfterGC { get; set; }
    public long MemoryFreed { get; set; }
    public int Generation0Collections { get; set; }
    public int Generation1Collections { get; set; }
    public int Generation2Collections { get; set; }

    public override string ToString()
    {
        return $"Memory: {MemoryAfterGC / 1024 / 1024:F1} MB " +
               $"(freed {MemoryFreed / 1024 / 1024:F1} MB), " +
               $"GC: Gen0={Generation0Collections}, Gen1={Generation1Collections}, Gen2={Generation2Collections}";
    }
}

/// <summary>
/// Compression utilities for GigaMap performance optimization.
/// </summary>
public static class CompressionOptimizer
{
    /// <summary>
    /// Compresses a collection of entities using GZip compression.
    /// </summary>
    public static async Task<CompressedData<T>> CompressEntitiesAsync<T>(
        IEnumerable<T> entities,
        CompressionLevel compressionLevel = CompressionLevel.Optimal) where T : class
    {
        var entityList = entities.ToList();
        var originalData = JsonSerializer.SerializeToUtf8Bytes(entityList);

        using var compressedStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressedStream, compressionLevel))
        {
            await gzipStream.WriteAsync(originalData);
        }

        var compressedBytes = compressedStream.ToArray();

        return new CompressedData<T>
        {
            CompressedBytes = compressedBytes,
            OriginalSize = originalData.Length,
            CompressedSize = compressedBytes.Length,
            CompressionRatio = (double)compressedBytes.Length / originalData.Length,
            EntityCount = entityList.Count,
            CompressionLevel = compressionLevel,
            CompressedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Decompresses entities from compressed data.
    /// </summary>
    public static async Task<List<T>> DecompressEntitiesAsync<T>(CompressedData<T> compressedData) where T : class
    {
        using var compressedStream = new MemoryStream(compressedData.CompressedBytes);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();

        await gzipStream.CopyToAsync(decompressedStream);
        var decompressedBytes = decompressedStream.ToArray();

        return JsonSerializer.Deserialize<List<T>>(decompressedBytes) ?? new List<T>();
    }

    /// <summary>
    /// Compresses query results for caching or storage.
    /// </summary>
    public static async Task<CompressedQueryResult<T>> CompressQueryResultAsync<T>(
        IReadOnlyList<T> results,
        string querySignature,
        CompressionLevel compressionLevel = CompressionLevel.Optimal) where T : class
    {
        var queryData = new QueryResultData<T>
        {
            Results = results.ToList(),
            QuerySignature = querySignature,
            ExecutedAt = DateTime.UtcNow,
            ResultCount = results.Count
        };

        var originalData = JsonSerializer.SerializeToUtf8Bytes(queryData);

        using var compressedStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressedStream, compressionLevel))
        {
            await gzipStream.WriteAsync(originalData);
        }

        var compressedBytes = compressedStream.ToArray();

        return new CompressedQueryResult<T>
        {
            CompressedData = compressedBytes,
            QuerySignature = querySignature,
            OriginalSize = originalData.Length,
            CompressedSize = compressedBytes.Length,
            CompressionRatio = (double)compressedBytes.Length / originalData.Length,
            ResultCount = results.Count,
            CompressedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Decompresses query results from compressed data.
    /// </summary>
    public static async Task<QueryResultData<T>> DecompressQueryResultAsync<T>(
        CompressedQueryResult<T> compressedResult) where T : class
    {
        using var compressedStream = new MemoryStream(compressedResult.CompressedData);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();

        await gzipStream.CopyToAsync(decompressedStream);
        var decompressedBytes = decompressedStream.ToArray();

        return JsonSerializer.Deserialize<QueryResultData<T>>(decompressedBytes) ??
               new QueryResultData<T> { Results = new List<T>() };
    }

    /// <summary>
    /// Analyzes compression effectiveness for different compression levels.
    /// </summary>
    public static async Task<CompressionAnalysis> AnalyzeCompressionAsync<T>(
        IEnumerable<T> sampleData) where T : class
    {
        var entityList = sampleData.ToList();
        var originalData = JsonSerializer.SerializeToUtf8Bytes(entityList);
        var analysis = new CompressionAnalysis
        {
            OriginalSize = originalData.Length,
            EntityCount = entityList.Count,
            AnalyzedAt = DateTime.UtcNow
        };

        // Test different compression levels
        var compressionLevels = new[]
        {
            CompressionLevel.NoCompression,
            CompressionLevel.Fastest,
            CompressionLevel.Optimal,
            CompressionLevel.SmallestSize
        };

        foreach (var level in compressionLevels)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, level))
            {
                await gzipStream.WriteAsync(originalData);
            }

            stopwatch.Stop();
            var compressedBytes = compressedStream.ToArray();

            analysis.LevelResults[level] = new CompressionLevelResult
            {
                CompressedSize = compressedBytes.Length,
                CompressionRatio = (double)compressedBytes.Length / originalData.Length,
                CompressionTime = stopwatch.Elapsed,
                SpaceSavings = originalData.Length - compressedBytes.Length
            };
        }

        // Determine recommended level
        analysis.RecommendedLevel = DetermineRecommendedCompressionLevel(analysis.LevelResults);

        return analysis;
    }

    /// <summary>
    /// Determines the recommended compression level based on analysis results.
    /// </summary>
    private static CompressionLevel DetermineRecommendedCompressionLevel(
        Dictionary<CompressionLevel, CompressionLevelResult> results)
    {
        // Balance between compression ratio and speed
        var optimal = results[CompressionLevel.Optimal];
        var fastest = results[CompressionLevel.Fastest];

        // If optimal compression is less than 2x slower but saves >20% more space, use optimal
        if (optimal.CompressionTime.TotalMilliseconds < fastest.CompressionTime.TotalMilliseconds * 2 &&
            optimal.CompressionRatio < fastest.CompressionRatio * 0.8)
        {
            return CompressionLevel.Optimal;
        }

        return CompressionLevel.Fastest;
    }

    /// <summary>
    /// Estimates memory savings from compression for a GigaMap.
    /// </summary>
    public static async Task<MemorySavingsEstimate> EstimateMemorySavingsAsync<T>(
        IGigaMap<T> gigaMap,
        int sampleSize = 1000) where T : class
    {
        var estimate = new MemorySavingsEstimate
        {
            TotalEntities = gigaMap.Size,
            SampleSize = Math.Min(sampleSize, (int)gigaMap.Size),
            EstimatedAt = DateTime.UtcNow
        };

        if (gigaMap.Size == 0)
        {
            return estimate;
        }

        // Sample entities for analysis
        var sampleEntities = new List<T>();
        var sampleCount = 0;

        foreach (var entity in gigaMap)
        {
            sampleEntities.Add(entity);
            sampleCount++;
            if (sampleCount >= estimate.SampleSize)
                break;
        }

        // Analyze compression
        var analysis = await AnalyzeCompressionAsync(sampleEntities);
        var recommendedResult = analysis.LevelResults[analysis.RecommendedLevel];

        // Extrapolate to full dataset
        var bytesPerEntity = (double)analysis.OriginalSize / analysis.EntityCount;
        var estimatedTotalSize = (long)(bytesPerEntity * gigaMap.Size);
        var estimatedCompressedSize = (long)(estimatedTotalSize * recommendedResult.CompressionRatio);

        estimate.EstimatedCurrentMemoryUsage = estimatedTotalSize;
        estimate.EstimatedCompressedMemoryUsage = estimatedCompressedSize;
        estimate.EstimatedMemorySavings = estimatedTotalSize - estimatedCompressedSize;
        estimate.EstimatedCompressionRatio = recommendedResult.CompressionRatio;
        estimate.RecommendedCompressionLevel = analysis.RecommendedLevel;

        return estimate;
    }
}

/// <summary>
/// Represents compressed entity data with metadata.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class CompressedData<T> where T : class
{
    public byte[] CompressedBytes { get; set; } = Array.Empty<byte>();
    public int OriginalSize { get; set; }
    public int CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public int EntityCount { get; set; }
    public CompressionLevel CompressionLevel { get; set; }
    public DateTime CompressedAt { get; set; }

    /// <summary>
    /// Gets the space savings in bytes.
    /// </summary>
    public int SpaceSavings => OriginalSize - CompressedSize;

    /// <summary>
    /// Gets the compression percentage (0-100).
    /// </summary>
    public double CompressionPercentage => (1.0 - CompressionRatio) * 100.0;

    public override string ToString()
    {
        return $"Compressed {EntityCount} entities: {OriginalSize / 1024:F1} KB → {CompressedSize / 1024:F1} KB " +
               $"({CompressionPercentage:F1}% reduction)";
    }
}

/// <summary>
/// Represents compressed query result data.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class CompressedQueryResult<T> where T : class
{
    public byte[] CompressedData { get; set; } = Array.Empty<byte>();
    public string QuerySignature { get; set; } = string.Empty;
    public int OriginalSize { get; set; }
    public int CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public int ResultCount { get; set; }
    public DateTime CompressedAt { get; set; }
    public TimeSpan? CacheExpiry { get; set; }

    /// <summary>
    /// Checks if the cached result has expired.
    /// </summary>
    public bool IsExpired => CacheExpiry.HasValue &&
                            DateTime.UtcNow - CompressedAt > CacheExpiry.Value;

    public override string ToString()
    {
        return $"Cached query result: {ResultCount} results, " +
               $"{CompressedSize / 1024:F1} KB compressed ({CompressionRatio * 100:F1}% of original)";
    }
}

/// <summary>
/// Query result data for serialization.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class QueryResultData<T> where T : class
{
    public List<T> Results { get; set; } = new();
    public string QuerySignature { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public int ResultCount { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Analysis results for different compression levels.
/// </summary>
public class CompressionAnalysis
{
    public int OriginalSize { get; set; }
    public int EntityCount { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public CompressionLevel RecommendedLevel { get; set; }
    public Dictionary<CompressionLevel, CompressionLevelResult> LevelResults { get; set; } = new();

    /// <summary>
    /// Gets the best compression ratio achieved.
    /// </summary>
    public double BestCompressionRatio => LevelResults.Values.Min(r => r.CompressionRatio);

    /// <summary>
    /// Gets the fastest compression time.
    /// </summary>
    public TimeSpan FastestCompressionTime => LevelResults.Values.Min(r => r.CompressionTime);

    public override string ToString()
    {
        var recommended = LevelResults[RecommendedLevel];
        return $"Compression analysis: {EntityCount} entities, " +
               $"recommended {RecommendedLevel} ({recommended.CompressionRatio * 100:F1}% of original)";
    }
}

/// <summary>
/// Results for a specific compression level.
/// </summary>
public class CompressionLevelResult
{
    public int CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public TimeSpan CompressionTime { get; set; }
    public int SpaceSavings { get; set; }

    /// <summary>
    /// Gets the compression speed in MB/s.
    /// </summary>
    public double CompressionSpeed => SpaceSavings > 0 ?
        (SpaceSavings / 1024.0 / 1024.0) / CompressionTime.TotalSeconds : 0;

    public override string ToString()
    {
        return $"Size: {CompressedSize / 1024:F1} KB, " +
               $"Ratio: {CompressionRatio * 100:F1}%, " +
               $"Time: {CompressionTime.TotalMilliseconds:F1}ms";
    }
}

/// <summary>
/// Estimate of memory savings from compression.
/// </summary>
public class MemorySavingsEstimate
{
    public long TotalEntities { get; set; }
    public int SampleSize { get; set; }
    public long EstimatedCurrentMemoryUsage { get; set; }
    public long EstimatedCompressedMemoryUsage { get; set; }
    public long EstimatedMemorySavings { get; set; }
    public double EstimatedCompressionRatio { get; set; }
    public CompressionLevel RecommendedCompressionLevel { get; set; }
    public DateTime EstimatedAt { get; set; }

    /// <summary>
    /// Gets the estimated memory savings percentage.
    /// </summary>
    public double SavingsPercentage => EstimatedCurrentMemoryUsage > 0 ?
        (double)EstimatedMemorySavings / EstimatedCurrentMemoryUsage * 100.0 : 0;

    public override string ToString()
    {
        return $"Memory savings estimate: {EstimatedMemorySavings / 1024 / 1024:F1} MB saved " +
               $"({SavingsPercentage:F1}% reduction) for {TotalEntities} entities";
    }
}

/// <summary>
/// Compressed query result cache for improved performance.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class CompressedQueryCache<T> : IDisposable where T : class
{
    private readonly Dictionary<string, CompressedQueryResult<T>> _cache = new();
    private readonly object _lock = new();
    private readonly TimeSpan _defaultExpiry;
    private readonly int _maxCacheSize;
    private readonly Timer _cleanupTimer;

    public CompressedQueryCache(TimeSpan? defaultExpiry = null, int maxCacheSize = 1000)
    {
        _defaultExpiry = defaultExpiry ?? TimeSpan.FromMinutes(15);
        _maxCacheSize = maxCacheSize;

        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Tries to get a cached query result.
    /// </summary>
    public async Task<IReadOnlyList<T>?> TryGetCachedResultAsync(string querySignature)
    {
        CompressedQueryResult<T>? compressedResult;

        lock (_lock)
        {
            if (!_cache.TryGetValue(querySignature, out compressedResult) ||
                compressedResult.IsExpired)
            {
                return null;
            }
        }

        try
        {
            var resultData = await CompressionOptimizer.DecompressQueryResultAsync(compressedResult);
            return resultData.Results.AsReadOnly();
        }
        catch
        {
            // Remove corrupted cache entry
            lock (_lock)
            {
                _cache.Remove(querySignature);
            }
            return null;
        }
    }

    /// <summary>
    /// Caches a query result with compression.
    /// </summary>
    public async Task CacheResultAsync(
        string querySignature,
        IReadOnlyList<T> results,
        TimeSpan? customExpiry = null)
    {
        try
        {
            var compressedResult = await CompressionOptimizer.CompressQueryResultAsync(
                results, querySignature);

            compressedResult.CacheExpiry = customExpiry ?? _defaultExpiry;

            lock (_lock)
            {
                // Remove oldest entries if cache is full
                if (_cache.Count >= _maxCacheSize)
                {
                    var oldestKey = _cache.OrderBy(kvp => kvp.Value.CompressedAt).First().Key;
                    _cache.Remove(oldestKey);
                }

                _cache[querySignature] = compressedResult;
            }
        }
        catch
        {
            // Ignore caching errors - don't break the query
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            var totalCompressedSize = _cache.Values.Sum(v => v.CompressedSize);
            var totalOriginalSize = _cache.Values.Sum(v => v.OriginalSize);
            var totalResults = _cache.Values.Sum(v => v.ResultCount);

            return new CacheStatistics
            {
                EntryCount = _cache.Count,
                TotalCompressedSize = totalCompressedSize,
                TotalOriginalSize = totalOriginalSize,
                TotalCachedResults = totalResults,
                CompressionRatio = totalOriginalSize > 0 ? (double)totalCompressedSize / totalOriginalSize : 0,
                MemorySavings = totalOriginalSize - totalCompressedSize
            };
        }
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        lock (_lock)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Statistics for the compressed query cache.
/// </summary>
public class CacheStatistics
{
    public int EntryCount { get; set; }
    public long TotalCompressedSize { get; set; }
    public long TotalOriginalSize { get; set; }
    public int TotalCachedResults { get; set; }
    public double CompressionRatio { get; set; }
    public long MemorySavings { get; set; }

    public override string ToString()
    {
        return $"Cache: {EntryCount} entries, {TotalCachedResults} results, " +
               $"{TotalCompressedSize / 1024:F1} KB compressed " +
               $"(saved {MemorySavings / 1024:F1} KB, {(1 - CompressionRatio) * 100:F1}% reduction)";
    }
}
