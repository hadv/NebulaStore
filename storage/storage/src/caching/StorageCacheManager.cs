using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Integrated cache manager for NebulaStore that manages multi-level caching,
/// coherence across storage channels, and cache warming.
/// </summary>
public class StorageCacheManager : IDisposable
{
    private readonly string _storageDirectory;
    private readonly StorageCacheConfiguration _configuration;
    private readonly ConcurrentDictionary<string, object> _caches;
    private readonly ICacheCoherenceManager<long, object> _coherenceManager;
    private readonly CacheFactory _cacheFactory;
    private readonly Timer? _maintenanceTimer;
    private volatile bool _isDisposed;

    public StorageCacheManager(string storageDirectory, StorageCacheConfiguration? configuration = null)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _configuration = configuration ?? new StorageCacheConfiguration();
        _caches = new ConcurrentDictionary<string, object>();
        _coherenceManager = new CacheCoherenceManager<long, object>(_configuration.CoherenceStrategy);
        _cacheFactory = new CacheFactory();

        // Ensure cache directory exists
        var cacheDirectory = Path.Combine(_storageDirectory, "cache");
        Directory.CreateDirectory(cacheDirectory);

        // Set up maintenance timer
        if (_configuration.MaintenanceInterval > TimeSpan.Zero)
        {
            _maintenanceTimer = new Timer(PerformMaintenance, null, _configuration.MaintenanceInterval, _configuration.MaintenanceInterval);
        }
    }

    /// <summary>
    /// Gets or creates a cache for the specified channel and type.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="channelId">Storage channel ID</param>
    /// <param name="cacheType">Type of data being cached</param>
    /// <returns>Cache instance</returns>
    public ICache<TKey, TValue> GetOrCreateCache<TKey, TValue>(int channelId, string cacheType)
        where TKey : notnull
    {
        ThrowIfDisposed();
        
        var cacheKey = $"channel_{channelId}_{cacheType}_{typeof(TKey).Name}_{typeof(TValue).Name}";
        
        if (_caches.TryGetValue(cacheKey, out var existingCache) && existingCache is ICache<TKey, TValue> typedCache)
        {
            return typedCache;
        }

        var cache = CreateMultiLevelCache<TKey, TValue>(channelId, cacheType);
        _caches.TryAdd(cacheKey, cache);

        // Register with coherence manager if it's an object cache
        if (typeof(TKey) == typeof(long) && cache is ICache<long, object> objectCache)
        {
            _coherenceManager.RegisterCache(cacheKey, objectCache);
        }

        return cache;
    }

    /// <summary>
    /// Gets a cache for entity storage by channel.
    /// </summary>
    /// <param name="channelId">Storage channel ID</param>
    /// <returns>Entity cache instance</returns>
    public ICache<long, object> GetEntityCache(int channelId)
    {
        return GetOrCreateCache<long, object>(channelId, "entities");
    }

    /// <summary>
    /// Gets a cache for type metadata by channel.
    /// </summary>
    /// <param name="channelId">Storage channel ID</param>
    /// <returns>Type metadata cache instance</returns>
    public ICache<string, object> GetTypeMetadataCache(int channelId)
    {
        return GetOrCreateCache<string, object>(channelId, "type_metadata");
    }

    /// <summary>
    /// Gets a cache for file data by channel.
    /// </summary>
    /// <param name="channelId">Storage channel ID</param>
    /// <returns>File data cache instance</returns>
    public ICache<string, byte[]> GetFileDataCache(int channelId)
    {
        return GetOrCreateCache<string, byte[]>(channelId, "file_data");
    }

    /// <summary>
    /// Invalidates all caches for a specific channel.
    /// </summary>
    /// <param name="channelId">Storage channel ID</param>
    public void InvalidateChannelCaches(int channelId)
    {
        ThrowIfDisposed();
        
        var channelCaches = _caches.Where(kvp => kvp.Key.StartsWith($"channel_{channelId}_")).ToList();
        
        foreach (var kvp in channelCaches)
        {
            if (kvp.Value is ICache<object, object> cache)
            {
                cache.Clear();
            }
        }
    }

    /// <summary>
    /// Warms up caches for a specific channel.
    /// </summary>
    /// <param name="channelId">Storage channel ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items warmed</returns>
    public async Task<int> WarmupChannelCachesAsync(int channelId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var totalWarmed = 0;
        var channelCaches = _caches.Where(kvp => kvp.Key.StartsWith($"channel_{channelId}_")).ToList();
        
        foreach (var kvp in channelCaches)
        {
            try
            {
                if (kvp.Value is ICache<long, object> entityCache)
                {
                    // Warm entity cache with most recently accessed entities
                    var warmupData = await GetEntityWarmupDataAsync(channelId, cancellationToken);
                    await entityCache.WarmupAsync(warmupData);
                    totalWarmed += warmupData.Count();
                }
            }
            catch
            {
                // Ignore individual cache warming failures
            }
        }
        
        return totalWarmed;
    }

    /// <summary>
    /// Gets cache statistics for all caches.
    /// </summary>
    /// <returns>Dictionary of cache statistics by cache key</returns>
    public Dictionary<string, ICacheStatistics> GetAllCacheStatistics()
    {
        ThrowIfDisposed();
        
        var statistics = new Dictionary<string, ICacheStatistics>();
        
        foreach (var kvp in _caches)
        {
            try
            {
                if (kvp.Value is ICache<object, object> cache)
                {
                    statistics[kvp.Key] = cache.Statistics;
                }
            }
            catch
            {
                // Ignore individual cache errors
            }
        }
        
        return statistics;
    }

    /// <summary>
    /// Gets coherence statistics.
    /// </summary>
    /// <returns>Coherence statistics</returns>
    public ICacheCoherenceStatistics GetCoherenceStatistics()
    {
        ThrowIfDisposed();
        return _coherenceManager.Statistics;
    }

    /// <summary>
    /// Performs cache maintenance operations.
    /// </summary>
    /// <returns>Maintenance results</returns>
    public async Task<CacheMaintenanceResult> PerformMaintenanceAsync()
    {
        ThrowIfDisposed();
        
        var result = new CacheMaintenanceResult();
        var startTime = DateTime.UtcNow;
        
        foreach (var kvp in _caches.ToList())
        {
            try
            {
                if (kvp.Value is ICache<object, object> cache)
                {
                    // Clear expired entries
                    var expiredCount = cache.ClearExpired();
                    result.ExpiredEntriesRemoved += expiredCount;
                    
                    // Perform eviction if needed
                    var currentUtilization = (double)cache.SizeInBytes / cache.MaxSizeInBytes;
                    if (currentUtilization > _configuration.EvictionThreshold)
                    {
                        var targetSize = (long)(cache.MaxSizeInBytes * _configuration.EvictionTarget);
                        var evictedCount = cache.Evict(targetSize);
                        result.EntriesEvicted += evictedCount;
                    }
                }
            }
            catch
            {
                result.ErrorCount++;
            }
        }
        
        result.Duration = DateTime.UtcNow - startTime;
        result.CachesProcessed = _caches.Count;
        
        return result;
    }

    private ICache<TKey, TValue> CreateMultiLevelCache<TKey, TValue>(int channelId, string cacheType)
        where TKey : notnull
    {
        // Create L1 (in-memory) cache
        var l1Config = CacheConfiguration.Builder()
            .SetName($"L1_{channelId}_{cacheType}")
            .SetMaxEntryCount(_configuration.L1MaxEntries)
            .SetMaxSizeInBytes(_configuration.L1MaxSizeInBytes)
            .SetEvictionPolicy(CacheEvictionPolicyType.LRU)
            .SetCleanupInterval(_configuration.CleanupInterval)
            .EnableStatistics()
            .Build();
        
        var l1Cache = _cacheFactory.CreateInMemoryCache<TKey, TValue>(l1Config);
        
        // Create L2 (disk-based) cache
        var l2CacheDirectory = Path.Combine(_storageDirectory, "cache", $"channel_{channelId}", cacheType);
        var l2Cache = new DiskCache<TKey, TValue>(
            $"L2_{channelId}_{cacheType}",
            l2CacheDirectory,
            _configuration.L2MaxEntries,
            _configuration.L2MaxSizeInBytes,
            enableCompression: _configuration.EnableCompression,
            cleanupInterval: _configuration.CleanupInterval);
        
        // Create multi-level cache
        var multiLevelConfig = MultiLevelCacheConfiguration.Builder()
            .EnableAutoPromotion()
            .SetPromotionThreshold(_configuration.PromotionThreshold)
            .EnableCacheCoherence(true, _configuration.CoherenceStrategy)
            .Build();
        
        return new MultiLevelCache<TKey, TValue>(l1Cache, l2Cache, multiLevelConfig);
    }

    private async Task<IEnumerable<KeyValuePair<long, object>>> GetEntityWarmupDataAsync(int channelId, CancellationToken cancellationToken)
    {
        // This is a placeholder implementation
        // In a real implementation, you would query the storage system for recently accessed entities
        await Task.Delay(1, cancellationToken);
        return Enumerable.Empty<KeyValuePair<long, object>>();
    }

    private void PerformMaintenance(object? state)
    {
        try
        {
            _ = Task.Run(async () => await PerformMaintenanceAsync());
        }
        catch
        {
            // Ignore maintenance errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StorageCacheManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _maintenanceTimer?.Dispose();
        
        // Dispose all caches
        foreach (var cache in _caches.Values)
        {
            if (cache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }
        }
        
        _caches.Clear();
        _coherenceManager.Dispose();
        _cacheFactory.Dispose();
    }
}

/// <summary>
/// Configuration for storage cache manager.
/// </summary>
public class StorageCacheConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of entries in L1 caches.
    /// </summary>
    public long L1MaxEntries { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the maximum size of L1 caches in bytes.
    /// </summary>
    public long L1MaxSizeInBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets the maximum number of entries in L2 caches.
    /// </summary>
    public long L2MaxEntries { get; set; } = 100000;

    /// <summary>
    /// Gets or sets the maximum size of L2 caches in bytes.
    /// </summary>
    public long L2MaxSizeInBytes { get; set; } = 1024L * 1024 * 1024; // 1GB

    /// <summary>
    /// Gets or sets whether to enable compression for L2 caches.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the cleanup interval for expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maintenance interval.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the eviction threshold.
    /// </summary>
    public double EvictionThreshold { get; set; } = 0.9;

    /// <summary>
    /// Gets or sets the eviction target.
    /// </summary>
    public double EvictionTarget { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the promotion threshold for multi-level caches.
    /// </summary>
    public long PromotionThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the cache coherence strategy.
    /// </summary>
    public CacheCoherenceStrategy CoherenceStrategy { get; set; } = CacheCoherenceStrategy.WriteThrough;
}

/// <summary>
/// Result of cache maintenance operations.
/// </summary>
public class CacheMaintenanceResult
{
    /// <summary>
    /// Gets or sets the number of caches processed.
    /// </summary>
    public int CachesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of expired entries removed.
    /// </summary>
    public int ExpiredEntriesRemoved { get; set; }

    /// <summary>
    /// Gets or sets the number of entries evicted.
    /// </summary>
    public int EntriesEvicted { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the duration of the maintenance operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    public override string ToString()
    {
        return $"CacheMaintenanceResult[Processed={CachesProcessed}, Expired={ExpiredEntriesRemoved}, " +
               $"Evicted={EntriesEvicted}, Errors={ErrorCount}, Duration={Duration.TotalMilliseconds:F0}ms]";
    }
}
