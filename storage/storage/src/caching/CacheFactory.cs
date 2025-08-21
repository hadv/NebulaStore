using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Factory for creating and managing cache instances.
/// </summary>
public class CacheFactory : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _caches = new();
    private volatile bool _isDisposed;

    /// <summary>
    /// Creates a new in-memory cache with the specified configuration.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="configuration">Cache configuration</param>
    /// <returns>A new cache instance</returns>
    public ICache<TKey, TValue> CreateInMemoryCache<TKey, TValue>(CacheConfiguration configuration)
        where TKey : notnull
    {
        ThrowIfDisposed();
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (!configuration.IsValid()) throw new ArgumentException("Invalid cache configuration", nameof(configuration));

        var evictionPolicy = CreateEvictionPolicy<TKey, TValue>(configuration.EvictionPolicy);
        var cache = new InMemoryCache<TKey, TValue>(
            configuration.Name,
            configuration.MaxEntryCount,
            configuration.MaxSizeInBytes,
            evictionPolicy,
            configuration.CleanupInterval);

        // Register the cache for management
        _caches.TryAdd(configuration.Name, cache);

        return cache;
    }

    /// <summary>
    /// Gets an existing cache by name.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="name">Cache name</param>
    /// <returns>The cache instance, or null if not found</returns>
    public ICache<TKey, TValue>? GetCache<TKey, TValue>(string name)
        where TKey : notnull
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cache name cannot be null or empty", nameof(name));

        if (_caches.TryGetValue(name, out var cache) && cache is ICache<TKey, TValue> typedCache)
        {
            return typedCache;
        }

        return null;
    }

    /// <summary>
    /// Gets or creates a cache with the specified configuration.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="configuration">Cache configuration</param>
    /// <returns>The cache instance</returns>
    public ICache<TKey, TValue> GetOrCreateCache<TKey, TValue>(CacheConfiguration configuration)
        where TKey : notnull
    {
        ThrowIfDisposed();
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var existingCache = GetCache<TKey, TValue>(configuration.Name);
        if (existingCache != null)
        {
            return existingCache;
        }

        return CreateInMemoryCache<TKey, TValue>(configuration);
    }

    /// <summary>
    /// Removes and disposes a cache by name.
    /// </summary>
    /// <param name="name">Cache name</param>
    /// <returns>True if the cache was found and removed</returns>
    public bool RemoveCache(string name)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cache name cannot be null or empty", nameof(name));

        if (_caches.TryRemove(name, out var cache))
        {
            if (cache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the names of all registered caches.
    /// </summary>
    /// <returns>Collection of cache names</returns>
    public IEnumerable<string> GetCacheNames()
    {
        ThrowIfDisposed();
        return _caches.Keys;
    }

    /// <summary>
    /// Gets the count of registered caches.
    /// </summary>
    public int CacheCount => _caches.Count;

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public void ClearAllCaches()
    {
        ThrowIfDisposed();
        
        foreach (var cache in _caches.Values)
        {
            if (cache is ICache<object, object> genericCache)
            {
                genericCache.Clear();
            }
        }
    }

    /// <summary>
    /// Creates an eviction policy based on the specified type.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="policyType">The eviction policy type</param>
    /// <returns>An eviction policy instance</returns>
    private static ICacheEvictionPolicy<TKey, TValue> CreateEvictionPolicy<TKey, TValue>(CacheEvictionPolicyType policyType)
    {
        return policyType switch
        {
            CacheEvictionPolicyType.LRU => new LruEvictionPolicy<TKey, TValue>(),
            CacheEvictionPolicyType.LFU => new LfuEvictionPolicy<TKey, TValue>(),
            CacheEvictionPolicyType.TimeBased => new TimeBasedEvictionPolicy<TKey, TValue>(),
            CacheEvictionPolicyType.Custom => new LruEvictionPolicy<TKey, TValue>(), // Default to LRU for custom
            _ => throw new ArgumentException($"Unsupported eviction policy type: {policyType}")
        };
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CacheFactory));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        // Dispose all caches
        foreach (var cache in _caches.Values)
        {
            if (cache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }
        }

        _caches.Clear();
    }
}

/// <summary>
/// Static cache factory for global cache management.
/// </summary>
public static class GlobalCacheFactory
{
    private static readonly Lazy<CacheFactory> _instance = new(() => new CacheFactory());

    /// <summary>
    /// Gets the global cache factory instance.
    /// </summary>
    public static CacheFactory Instance => _instance.Value;

    /// <summary>
    /// Creates a new in-memory cache with default configuration.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="name">Cache name</param>
    /// <param name="maxEntries">Maximum number of entries</param>
    /// <param name="maxSizeInMB">Maximum size in megabytes</param>
    /// <returns>A new cache instance</returns>
    public static ICache<TKey, TValue> CreateCache<TKey, TValue>(
        string name,
        long maxEntries = 10000,
        long maxSizeInMB = 100)
        where TKey : notnull
    {
        var config = CacheConfiguration.Builder()
            .SetName(name)
            .SetMaxEntryCount(maxEntries)
            .SetMaxSizeInMB(maxSizeInMB)
            .SetEvictionPolicy(CacheEvictionPolicyType.LRU)
            .EnableStatistics()
            .EnablePerformanceMonitoring()
            .Build();

        return Instance.CreateInMemoryCache<TKey, TValue>(config);
    }

    /// <summary>
    /// Gets an existing cache by name.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="name">Cache name</param>
    /// <returns>The cache instance, or null if not found</returns>
    public static ICache<TKey, TValue>? GetCache<TKey, TValue>(string name)
        where TKey : notnull
    {
        return Instance.GetCache<TKey, TValue>(name);
    }

    /// <summary>
    /// Gets or creates a cache with default configuration.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="name">Cache name</param>
    /// <param name="maxEntries">Maximum number of entries</param>
    /// <param name="maxSizeInMB">Maximum size in megabytes</param>
    /// <returns>The cache instance</returns>
    public static ICache<TKey, TValue> GetOrCreateCache<TKey, TValue>(
        string name,
        long maxEntries = 10000,
        long maxSizeInMB = 100)
        where TKey : notnull
    {
        var existingCache = GetCache<TKey, TValue>(name);
        if (existingCache != null)
        {
            return existingCache;
        }

        return CreateCache<TKey, TValue>(name, maxEntries, maxSizeInMB);
    }
}
