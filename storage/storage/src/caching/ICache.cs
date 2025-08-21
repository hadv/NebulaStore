using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for high-performance caching with advanced features.
/// </summary>
public interface ICache<TKey, TValue> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Gets the cache name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    long Count { get; }

    /// <summary>
    /// Gets the maximum capacity of the cache.
    /// </summary>
    long MaxCapacity { get; }

    /// <summary>
    /// Gets the current size of the cache in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets the maximum size of the cache in bytes.
    /// </summary>
    long MaxSizeInBytes { get; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    double HitRatio { get; }

    /// <summary>
    /// Gets the cache statistics.
    /// </summary>
    ICacheStatistics Statistics { get; }

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <returns>The cached value, or default if not found</returns>
    TValue? Get(TKey key);

    /// <summary>
    /// Gets a value from the cache asynchronously.
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached value, or default if not found</returns>
    Task<TValue?> GetAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to get a value from the cache.
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The cached value if found</param>
    /// <returns>True if the value was found, false otherwise</returns>
    bool TryGet(TKey key, out TValue? value);

    /// <summary>
    /// Gets multiple values from the cache.
    /// </summary>
    /// <param name="keys">The keys to look up</param>
    /// <returns>Dictionary of found key-value pairs</returns>
    Dictionary<TKey, TValue> GetMany(IEnumerable<TKey> keys);

    /// <summary>
    /// Gets multiple values from the cache asynchronously.
    /// </summary>
    /// <param name="keys">The keys to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of found key-value pairs</returns>
    Task<Dictionary<TKey, TValue>> GetManyAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a value into the cache.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    /// <param name="timeToLive">Optional time-to-live</param>
    /// <param name="priority">Cache entry priority</param>
    void Put(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal);

    /// <summary>
    /// Puts a value into the cache asynchronously.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    /// <param name="timeToLive">Optional time-to-live</param>
    /// <param name="priority">Cache entry priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PutAsync(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts multiple values into the cache.
    /// </summary>
    /// <param name="items">The key-value pairs to cache</param>
    /// <param name="timeToLive">Optional time-to-live</param>
    /// <param name="priority">Cache entry priority</param>
    void PutMany(IEnumerable<KeyValuePair<TKey, TValue>> items, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal);

    /// <summary>
    /// Puts a value into the cache if the key doesn't exist.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    /// <param name="timeToLive">Optional time-to-live</param>
    /// <param name="priority">Cache entry priority</param>
    /// <returns>True if the value was added, false if key already exists</returns>
    bool PutIfAbsent(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The key to remove</param>
    /// <returns>True if the key was found and removed</returns>
    bool Remove(TKey key);

    /// <summary>
    /// Removes multiple values from the cache.
    /// </summary>
    /// <param name="keys">The keys to remove</param>
    /// <returns>Number of keys that were found and removed</returns>
    int RemoveMany(IEnumerable<TKey> keys);

    /// <summary>
    /// Checks if the cache contains a key.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key exists in the cache</returns>
    bool ContainsKey(TKey key);

    /// <summary>
    /// Gets all keys in the cache.
    /// </summary>
    /// <returns>Collection of all keys</returns>
    IEnumerable<TKey> GetKeys();

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Clears expired entries from the cache.
    /// </summary>
    /// <returns>Number of expired entries removed</returns>
    int ClearExpired();

    /// <summary>
    /// Forces eviction of entries to free up space.
    /// </summary>
    /// <param name="targetSize">Target size after eviction</param>
    /// <returns>Number of entries evicted</returns>
    int Evict(long targetSize);

    /// <summary>
    /// Warms up the cache with the specified data.
    /// </summary>
    /// <param name="warmupData">Data to preload into cache</param>
    Task WarmupAsync(IEnumerable<KeyValuePair<TKey, TValue>> warmupData);

    /// <summary>
    /// Gets cache entry metadata for a key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>Cache entry metadata, or null if not found</returns>
    ICacheEntryMetadata? GetEntryMetadata(TKey key);
}

/// <summary>
/// Interface for cache statistics.
/// </summary>
public interface ICacheStatistics
{
    /// <summary>
    /// Gets the total number of cache hits.
    /// </summary>
    long HitCount { get; }

    /// <summary>
    /// Gets the total number of cache misses.
    /// </summary>
    long MissCount { get; }

    /// <summary>
    /// Gets the total number of cache requests.
    /// </summary>
    long RequestCount { get; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    double HitRatio { get; }

    /// <summary>
    /// Gets the total number of evictions.
    /// </summary>
    long EvictionCount { get; }

    /// <summary>
    /// Gets the total number of expired entries removed.
    /// </summary>
    long ExpiredCount { get; }

    /// <summary>
    /// Gets the average access time in milliseconds.
    /// </summary>
    double AverageAccessTimeMs { get; }

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    long CurrentSizeInBytes { get; }

    /// <summary>
    /// Gets the maximum cache size in bytes.
    /// </summary>
    long MaxSizeInBytes { get; }

    /// <summary>
    /// Gets the current entry count.
    /// </summary>
    long CurrentEntryCount { get; }

    /// <summary>
    /// Gets the maximum entry count.
    /// </summary>
    long MaxEntryCount { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}
