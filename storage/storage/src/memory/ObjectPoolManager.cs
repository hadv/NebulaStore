using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// Manager for coordinating multiple object pools with global optimization.
/// </summary>
public class ObjectPoolManager : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _pools;
    private readonly Timer _optimizationTimer;
    private readonly ObjectPoolManagerConfiguration _configuration;
    private volatile bool _isDisposed;

    public ObjectPoolManager(ObjectPoolManagerConfiguration? configuration = null)
    {
        _configuration = configuration ?? new ObjectPoolManagerConfiguration();
        _pools = new ConcurrentDictionary<string, object>();

        // Set up optimization timer
        if (_configuration.OptimizationInterval > TimeSpan.Zero)
        {
            _optimizationTimer = new Timer(PerformOptimization, null, 
                _configuration.OptimizationInterval, _configuration.OptimizationInterval);
        }
    }

    /// <summary>
    /// Gets the number of managed pools.
    /// </summary>
    public int PoolCount => _pools.Count;

    /// <summary>
    /// Creates or gets an object pool for the specified type.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool</typeparam>
    /// <param name="name">Pool name</param>
    /// <param name="factory">Object factory</param>
    /// <param name="configuration">Pool configuration</param>
    /// <returns>Object pool instance</returns>
    public IObjectPool<T> GetOrCreatePool<T>(
        string name, 
        IObjectFactory<T>? factory = null, 
        ObjectPoolConfiguration? configuration = null) 
        where T : class, new()
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool name cannot be null or empty", nameof(name));

        return (IObjectPool<T>)_pools.GetOrAdd(name, _ =>
        {
            var poolFactory = factory ?? new DefaultObjectFactory<T>();
            var poolConfig = configuration ?? new ObjectPoolConfiguration();
            return new ObjectPool<T>(name, poolFactory, poolConfig);
        });
    }

    /// <summary>
    /// Gets an existing object pool.
    /// </summary>
    /// <typeparam name="T">Type of objects in the pool</typeparam>
    /// <param name="name">Pool name</param>
    /// <returns>Object pool instance, or null if not found</returns>
    public IObjectPool<T>? GetPool<T>(string name) where T : class
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool name cannot be null or empty", nameof(name));

        return _pools.TryGetValue(name, out var pool) && pool is IObjectPool<T> typedPool ? typedPool : null;
    }

    /// <summary>
    /// Removes and disposes a pool.
    /// </summary>
    /// <param name="name">Pool name</param>
    /// <returns>True if the pool was found and removed</returns>
    public bool RemovePool(string name)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool name cannot be null or empty", nameof(name));

        if (_pools.TryRemove(name, out var pool))
        {
            if (pool is IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all pool names.
    /// </summary>
    /// <returns>Collection of pool names</returns>
    public IEnumerable<string> GetPoolNames()
    {
        ThrowIfDisposed();
        return _pools.Keys.ToList();
    }

    /// <summary>
    /// Gets aggregate statistics for all pools.
    /// </summary>
    /// <returns>Aggregate statistics</returns>
    public ObjectPoolManagerStatistics GetAggregateStatistics()
    {
        ThrowIfDisposed();

        var poolStats = new List<IObjectPoolStatistics>();
        
        foreach (var pool in _pools.Values)
        {
            if (pool is IObjectPool<object> genericPool)
            {
                poolStats.Add(genericPool.Statistics);
            }
        }

        return new ObjectPoolManagerStatistics(
            poolStats.Sum(s => s.TotalCreated),
            poolStats.Sum(s => s.TotalRetrieved),
            poolStats.Sum(s => s.TotalReturned),
            poolStats.Sum(s => s.TotalDiscarded),
            poolStats.Count > 0 ? poolStats.Average(s => s.HitRatio) : 0.0,
            poolStats.Count > 0 ? poolStats.Average(s => s.Utilization) : 0.0,
            poolStats.Sum(s => s.PeakSize),
            poolStats.Sum(s => s.PeakInUse),
            _pools.Count,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Performs global optimization across all pools.
    /// </summary>
    /// <returns>Optimization result</returns>
    public async Task<ObjectPoolOptimizationResult> OptimizeAsync()
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var optimizedPools = 0;
        var totalTrimmed = 0;
        var errors = 0;

        foreach (var kvp in _pools.ToList())
        {
            try
            {
                if (kvp.Value is IObjectPool<object> pool)
                {
                    optimizedPools++;
                    
                    // Analyze pool utilization
                    var utilization = pool.Statistics.Utilization;
                    
                    if (utilization < _configuration.LowUtilizationThreshold)
                    {
                        // Pool is under-utilized, trim it
                        var targetSize = Math.Max(1, (int)(pool.Count * _configuration.TrimRatio));
                        var trimmed = pool.Trim(targetSize);
                        totalTrimmed += trimmed;
                    }
                    else if (utilization > _configuration.HighUtilizationThreshold)
                    {
                        // Pool is over-utilized, preload more objects
                        var growthSize = Math.Max(1, pool.Count / 10); // Grow by 10%
                        pool.Preload(growthSize);
                    }
                }
            }
            catch
            {
                errors++;
            }
        }

        var duration = DateTime.UtcNow - startTime;
        
        return new ObjectPoolOptimizationResult(
            optimizedPools,
            totalTrimmed,
            errors,
            duration
        );
    }

    /// <summary>
    /// Clears all pools.
    /// </summary>
    public void ClearAllPools()
    {
        ThrowIfDisposed();

        foreach (var pool in _pools.Values)
        {
            if (pool is IObjectPool<object> genericPool)
            {
                genericPool.Clear();
            }
        }
    }

    /// <summary>
    /// Gets memory usage information for all pools.
    /// </summary>
    /// <returns>Memory usage information</returns>
    public ObjectPoolMemoryInfo GetMemoryInfo()
    {
        ThrowIfDisposed();

        long estimatedMemoryUsage = 0;
        var poolInfos = new List<PoolMemoryInfo>();

        foreach (var kvp in _pools)
        {
            if (kvp.Value is IObjectPool<object> pool)
            {
                var poolMemory = EstimatePoolMemoryUsage(pool);
                estimatedMemoryUsage += poolMemory;
                
                poolInfos.Add(new PoolMemoryInfo(
                    kvp.Key,
                    pool.Count,
                    pool.InUseCount,
                    poolMemory
                ));
            }
        }

        return new ObjectPoolMemoryInfo(
            estimatedMemoryUsage,
            poolInfos,
            DateTime.UtcNow
        );
    }

    private void PerformOptimization(object? state)
    {
        try
        {
            _ = Task.Run(async () => await OptimizeAsync());
        }
        catch
        {
            // Ignore optimization errors
        }
    }

    private static long EstimatePoolMemoryUsage(IObjectPool<object> pool)
    {
        // Rough estimation: assume each object uses 1KB on average
        // This could be made more sophisticated with actual object size measurement
        const long averageObjectSize = 1024;
        return (pool.Count + pool.InUseCount) * averageObjectSize;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ObjectPoolManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _optimizationTimer?.Dispose();

        // Dispose all pools
        foreach (var pool in _pools.Values)
        {
            if (pool is IDisposable disposablePool)
            {
                disposablePool.Dispose();
            }
        }

        _pools.Clear();
    }
}

/// <summary>
/// Configuration for object pool manager.
/// </summary>
public class ObjectPoolManagerConfiguration
{
    /// <summary>
    /// Gets or sets the optimization interval.
    /// </summary>
    public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the low utilization threshold for trimming.
    /// </summary>
    public double LowUtilizationThreshold { get; set; } = 0.3; // 30%

    /// <summary>
    /// Gets or sets the high utilization threshold for growth.
    /// </summary>
    public double HighUtilizationThreshold { get; set; } = 0.9; // 90%

    /// <summary>
    /// Gets or sets the trim ratio for under-utilized pools.
    /// </summary>
    public double TrimRatio { get; set; } = 0.7; // Trim to 70% of current size
}

/// <summary>
/// Aggregate statistics for object pool manager.
/// </summary>
public class ObjectPoolManagerStatistics
{
    public ObjectPoolManagerStatistics(
        long totalCreated,
        long totalRetrieved,
        long totalReturned,
        long totalDiscarded,
        double averageHitRatio,
        double averageUtilization,
        int totalPeakSize,
        int totalPeakInUse,
        int poolCount,
        DateTime timestamp)
    {
        TotalCreated = totalCreated;
        TotalRetrieved = totalRetrieved;
        TotalReturned = totalReturned;
        TotalDiscarded = totalDiscarded;
        AverageHitRatio = averageHitRatio;
        AverageUtilization = averageUtilization;
        TotalPeakSize = totalPeakSize;
        TotalPeakInUse = totalPeakInUse;
        PoolCount = poolCount;
        Timestamp = timestamp;
    }

    public long TotalCreated { get; }
    public long TotalRetrieved { get; }
    public long TotalReturned { get; }
    public long TotalDiscarded { get; }
    public double AverageHitRatio { get; }
    public double AverageUtilization { get; }
    public int TotalPeakSize { get; }
    public int TotalPeakInUse { get; }
    public int PoolCount { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"ObjectPoolManager Statistics [{Timestamp:HH:mm:ss}]: " +
               $"Pools={PoolCount}, Created={TotalCreated:N0}, " +
               $"Retrieved={TotalRetrieved:N0}, AvgHitRatio={AverageHitRatio:P1}, " +
               $"AvgUtilization={AverageUtilization:P1}";
    }
}

/// <summary>
/// Result of object pool optimization.
/// </summary>
public class ObjectPoolOptimizationResult
{
    public ObjectPoolOptimizationResult(int optimizedPools, int totalTrimmed, int errors, TimeSpan duration)
    {
        OptimizedPools = optimizedPools;
        TotalTrimmed = totalTrimmed;
        Errors = errors;
        Duration = duration;
        Timestamp = DateTime.UtcNow;
    }

    public int OptimizedPools { get; }
    public int TotalTrimmed { get; }
    public int Errors { get; }
    public TimeSpan Duration { get; }
    public DateTime Timestamp { get; }

    public bool IsSuccessful => Errors == 0;

    public override string ToString()
    {
        return $"ObjectPool Optimization [{Timestamp:HH:mm:ss}]: " +
               $"Optimized={OptimizedPools}, Trimmed={TotalTrimmed}, " +
               $"Errors={Errors}, Duration={Duration.TotalMilliseconds:F0}ms";
    }
}

/// <summary>
/// Memory usage information for object pools.
/// </summary>
public class ObjectPoolMemoryInfo
{
    public ObjectPoolMemoryInfo(long totalEstimatedMemory, IEnumerable<PoolMemoryInfo> poolInfos, DateTime timestamp)
    {
        TotalEstimatedMemory = totalEstimatedMemory;
        PoolInfos = poolInfos.ToList();
        Timestamp = timestamp;
    }

    public long TotalEstimatedMemory { get; }
    public IReadOnlyList<PoolMemoryInfo> PoolInfos { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"ObjectPool Memory [{Timestamp:HH:mm:ss}]: " +
               $"Total={TotalEstimatedMemory:N0} bytes, Pools={PoolInfos.Count}";
    }
}

/// <summary>
/// Memory information for a single pool.
/// </summary>
public class PoolMemoryInfo
{
    public PoolMemoryInfo(string name, int pooledCount, int inUseCount, long estimatedMemory)
    {
        Name = name;
        PooledCount = pooledCount;
        InUseCount = inUseCount;
        EstimatedMemory = estimatedMemory;
    }

    public string Name { get; }
    public int PooledCount { get; }
    public int InUseCount { get; }
    public long EstimatedMemory { get; }

    public int TotalObjects => PooledCount + InUseCount;

    public override string ToString()
    {
        return $"Pool '{Name}': {TotalObjects} objects ({PooledCount} pooled, {InUseCount} in-use), " +
               $"~{EstimatedMemory:N0} bytes";
    }
}
