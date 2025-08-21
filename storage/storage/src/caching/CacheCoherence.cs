using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for cache coherence management across multiple cache instances.
/// </summary>
public interface ICacheCoherenceManager<TKey, TValue> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Registers a cache instance for coherence management.
    /// </summary>
    /// <param name="cacheId">Unique identifier for the cache</param>
    /// <param name="cache">Cache instance to register</param>
    void RegisterCache(string cacheId, ICache<TKey, TValue> cache);

    /// <summary>
    /// Unregisters a cache instance from coherence management.
    /// </summary>
    /// <param name="cacheId">Cache identifier to unregister</param>
    void UnregisterCache(string cacheId);

    /// <summary>
    /// Notifies all registered caches about a cache operation.
    /// </summary>
    /// <param name="operation">Cache operation details</param>
    /// <param name="originCacheId">ID of the cache that originated the operation</param>
    Task NotifyOperationAsync(CacheOperation<TKey, TValue> operation, string originCacheId);

    /// <summary>
    /// Gets the coherence strategy being used.
    /// </summary>
    CacheCoherenceStrategy Strategy { get; }

    /// <summary>
    /// Gets statistics about coherence operations.
    /// </summary>
    ICacheCoherenceStatistics Statistics { get; }
}

/// <summary>
/// Default implementation of cache coherence manager.
/// </summary>
public class CacheCoherenceManager<TKey, TValue> : ICacheCoherenceManager<TKey, TValue>
    where TKey : notnull
{
    private readonly CacheCoherenceStrategy _strategy;
    private readonly ConcurrentDictionary<string, ICache<TKey, TValue>> _registeredCaches;
    private readonly CacheCoherenceStatistics _statistics;
    private readonly SemaphoreSlim _operationSemaphore;
    private volatile bool _isDisposed;

    public CacheCoherenceManager(CacheCoherenceStrategy strategy = CacheCoherenceStrategy.WriteThrough)
    {
        _strategy = strategy;
        _registeredCaches = new ConcurrentDictionary<string, ICache<TKey, TValue>>();
        _statistics = new CacheCoherenceStatistics();
        _operationSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
    }

    public CacheCoherenceStrategy Strategy => _strategy;

    public ICacheCoherenceStatistics Statistics => _statistics;

    public void RegisterCache(string cacheId, ICache<TKey, TValue> cache)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(cacheId)) throw new ArgumentException("Cache ID cannot be null or empty", nameof(cacheId));
        if (cache == null) throw new ArgumentNullException(nameof(cache));

        _registeredCaches.TryAdd(cacheId, cache);
        _statistics.RecordCacheRegistered();
    }

    public void UnregisterCache(string cacheId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(cacheId)) throw new ArgumentException("Cache ID cannot be null or empty", nameof(cacheId));

        if (_registeredCaches.TryRemove(cacheId, out _))
        {
            _statistics.RecordCacheUnregistered();
        }
    }

    public async Task NotifyOperationAsync(CacheOperation<TKey, TValue> operation, string originCacheId)
    {
        ThrowIfDisposed();
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (string.IsNullOrWhiteSpace(originCacheId)) throw new ArgumentException("Origin cache ID cannot be null or empty", nameof(originCacheId));

        await _operationSemaphore.WaitAsync();
        try
        {
            var startTime = DateTime.UtcNow;
            var targetCaches = _registeredCaches.Where(kvp => kvp.Key != originCacheId).ToList();

            if (targetCaches.Count == 0)
            {
                return; // No other caches to notify
            }

            switch (_strategy)
            {
                case CacheCoherenceStrategy.WriteThrough:
                    await HandleWriteThroughAsync(operation, targetCaches);
                    break;

                case CacheCoherenceStrategy.WriteBack:
                    await HandleWriteBackAsync(operation, targetCaches);
                    break;

                case CacheCoherenceStrategy.Invalidate:
                    await HandleInvalidateAsync(operation, targetCaches);
                    break;

                case CacheCoherenceStrategy.None:
                    // No coherence operations
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported coherence strategy: {_strategy}");
            }

            var duration = DateTime.UtcNow - startTime;
            _statistics.RecordOperation(operation.Type, targetCaches.Count, duration);
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    private async Task HandleWriteThroughAsync(CacheOperation<TKey, TValue> operation, List<KeyValuePair<string, ICache<TKey, TValue>>> targetCaches)
    {
        var tasks = targetCaches.Select(async kvp =>
        {
            try
            {
                var cache = kvp.Value;
                switch (operation.Type)
                {
                    case CacheOperationType.Put:
                        await cache.PutAsync(operation.Key, operation.Value!, operation.TimeToLive, operation.Priority);
                        break;

                    case CacheOperationType.Remove:
                        cache.Remove(operation.Key);
                        break;

                    case CacheOperationType.Clear:
                        cache.Clear();
                        break;
                }
                return true;
            }
            catch
            {
                return false; // Ignore individual cache failures
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task HandleWriteBackAsync(CacheOperation<TKey, TValue> operation, List<KeyValuePair<string, ICache<TKey, TValue>>> targetCaches)
    {
        // For write-back, we queue operations for later processing
        // This is a simplified implementation - in production, you'd want a proper queue
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100)); // Simulate delay
            await HandleWriteThroughAsync(operation, targetCaches);
        });
    }

    private async Task HandleInvalidateAsync(CacheOperation<TKey, TValue> operation, List<KeyValuePair<string, ICache<TKey, TValue>>> targetCaches)
    {
        var tasks = targetCaches.Select(async kvp =>
        {
            try
            {
                var cache = kvp.Value;
                switch (operation.Type)
                {
                    case CacheOperationType.Put:
                    case CacheOperationType.Remove:
                        cache.Remove(operation.Key);
                        break;

                    case CacheOperationType.Clear:
                        cache.Clear();
                        break;
                }
                return true;
            }
            catch
            {
                return false; // Ignore individual cache failures
            }
        });

        await Task.WhenAll(tasks);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CacheCoherenceManager<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _operationSemaphore.Dispose();
        _registeredCaches.Clear();
    }
}

/// <summary>
/// Represents a cache operation for coherence purposes.
/// </summary>
public class CacheOperation<TKey, TValue>
{
    public CacheOperation(CacheOperationType type, TKey key, TValue? value = default, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        Type = type;
        Key = key;
        Value = value;
        TimeToLive = timeToLive;
        Priority = priority;
        Timestamp = DateTime.UtcNow;
    }

    public CacheOperationType Type { get; }
    public TKey Key { get; }
    public TValue? Value { get; }
    public TimeSpan? TimeToLive { get; }
    public CacheEntryPriority Priority { get; }
    public DateTime Timestamp { get; }
}

/// <summary>
/// Enumeration of cache operation types.
/// </summary>
public enum CacheOperationType
{
    /// <summary>
    /// Put operation (insert or update).
    /// </summary>
    Put,

    /// <summary>
    /// Remove operation.
    /// </summary>
    Remove,

    /// <summary>
    /// Clear operation (remove all entries).
    /// </summary>
    Clear
}

/// <summary>
/// Interface for cache coherence statistics.
/// </summary>
public interface ICacheCoherenceStatistics
{
    /// <summary>
    /// Gets the number of registered caches.
    /// </summary>
    int RegisteredCacheCount { get; }

    /// <summary>
    /// Gets the total number of coherence operations performed.
    /// </summary>
    long TotalOperations { get; }

    /// <summary>
    /// Gets the number of put operations.
    /// </summary>
    long PutOperations { get; }

    /// <summary>
    /// Gets the number of remove operations.
    /// </summary>
    long RemoveOperations { get; }

    /// <summary>
    /// Gets the number of clear operations.
    /// </summary>
    long ClearOperations { get; }

    /// <summary>
    /// Gets the average operation duration in milliseconds.
    /// </summary>
    double AverageOperationDurationMs { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Implementation of cache coherence statistics.
/// </summary>
public class CacheCoherenceStatistics : ICacheCoherenceStatistics
{
    private long _registeredCacheCount;
    private long _totalOperations;
    private long _putOperations;
    private long _removeOperations;
    private long _clearOperations;
    private long _totalDurationMs;

    public int RegisteredCacheCount => (int)Interlocked.Read(ref _registeredCacheCount);

    public long TotalOperations => Interlocked.Read(ref _totalOperations);

    public long PutOperations => Interlocked.Read(ref _putOperations);

    public long RemoveOperations => Interlocked.Read(ref _removeOperations);

    public long ClearOperations => Interlocked.Read(ref _clearOperations);

    public double AverageOperationDurationMs
    {
        get
        {
            var operations = TotalOperations;
            return operations > 0 ? (double)Interlocked.Read(ref _totalDurationMs) / operations : 0.0;
        }
    }

    public void RecordCacheRegistered()
    {
        Interlocked.Increment(ref _registeredCacheCount);
    }

    public void RecordCacheUnregistered()
    {
        Interlocked.Decrement(ref _registeredCacheCount);
    }

    public void RecordOperation(CacheOperationType operationType, int targetCacheCount, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Add(ref _totalDurationMs, (long)duration.TotalMilliseconds);

        switch (operationType)
        {
            case CacheOperationType.Put:
                Interlocked.Increment(ref _putOperations);
                break;
            case CacheOperationType.Remove:
                Interlocked.Increment(ref _removeOperations);
                break;
            case CacheOperationType.Clear:
                Interlocked.Increment(ref _clearOperations);
                break;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalOperations, 0);
        Interlocked.Exchange(ref _putOperations, 0);
        Interlocked.Exchange(ref _removeOperations, 0);
        Interlocked.Exchange(ref _clearOperations, 0);
        Interlocked.Exchange(ref _totalDurationMs, 0);
        // Note: We don't reset registered cache count as it represents current state
    }
}
