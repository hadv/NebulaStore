using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Abstract base class for cache implementations providing common functionality.
/// </summary>
public abstract class AbstractCache<TKey, TValue> : ICache<TKey, TValue>
{
    protected readonly string _name;
    protected readonly long _maxCapacity;
    protected readonly long _maxSizeInBytes;
    protected readonly ICacheEvictionPolicy<TKey, TValue> _evictionPolicy;
    protected readonly CacheStatistics _statistics;
    protected readonly Timer? _cleanupTimer;
    protected readonly object _lock = new();
    protected volatile bool _isDisposed;

    protected AbstractCache(
        string name,
        long maxCapacity,
        long maxSizeInBytes,
        ICacheEvictionPolicy<TKey, TValue> evictionPolicy,
        TimeSpan? cleanupInterval = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _maxCapacity = maxCapacity > 0 ? maxCapacity : throw new ArgumentOutOfRangeException(nameof(maxCapacity));
        _maxSizeInBytes = maxSizeInBytes > 0 ? maxSizeInBytes : throw new ArgumentOutOfRangeException(nameof(maxSizeInBytes));
        _evictionPolicy = evictionPolicy ?? throw new ArgumentNullException(nameof(evictionPolicy));
        _statistics = new CacheStatistics(maxSizeInBytes, maxCapacity);

        // Set up cleanup timer if interval is specified
        if (cleanupInterval.HasValue && cleanupInterval.Value > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(PerformCleanup, null, cleanupInterval.Value, cleanupInterval.Value);
        }
    }

    public string Name => _name;
    public long MaxCapacity => _maxCapacity;
    public long MaxSizeInBytes => _maxSizeInBytes;
    public ICacheStatistics Statistics => _statistics;

    public abstract long Count { get; }
    public abstract long SizeInBytes { get; }
    public abstract double HitRatio { get; }

    public virtual TValue? Get(TKey key)
    {
        ThrowIfDisposed();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = GetInternal(key);
            if (result != null)
            {
                _statistics.RecordHit(stopwatch.ElapsedMilliseconds);
                return result;
            }
            else
            {
                _statistics.RecordMiss(stopwatch.ElapsedMilliseconds);
                return default;
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public virtual async Task<TValue?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await GetInternalAsync(key, cancellationToken);
            if (result != null)
            {
                _statistics.RecordHit(stopwatch.ElapsedMilliseconds);
                return result;
            }
            else
            {
                _statistics.RecordMiss(stopwatch.ElapsedMilliseconds);
                return default;
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public virtual bool TryGet(TKey key, out TValue? value)
    {
        ThrowIfDisposed();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            value = GetInternal(key);
            var found = value != null;
            
            if (found)
                _statistics.RecordHit(stopwatch.ElapsedMilliseconds);
            else
                _statistics.RecordMiss(stopwatch.ElapsedMilliseconds);

            return found;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public virtual Dictionary<TKey, TValue> GetMany(IEnumerable<TKey> keys)
    {
        ThrowIfDisposed();
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        var result = new Dictionary<TKey, TValue>();
        foreach (var key in keys)
        {
            var value = Get(key);
            if (value != null)
            {
                result[key] = value;
            }
        }
        return result;
    }

    public virtual async Task<Dictionary<TKey, TValue>> GetManyAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        var result = new Dictionary<TKey, TValue>();
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await GetAsync(key, cancellationToken);
            if (value != null)
            {
                result[key] = value;
            }
        }
        return result;
    }

    public virtual void Put(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        var entry = new CacheEntry<TKey, TValue>(key, value, timeToLive, priority);
        PutInternal(entry);
        _evictionPolicy.OnEntryAdded(entry);
        _statistics.RecordEntryAdded(entry.SizeInBytes);

        // Check if eviction is needed
        CheckAndPerformEviction();
    }

    public virtual async Task PutAsync(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        var entry = new CacheEntry<TKey, TValue>(key, value, timeToLive, priority);
        await PutInternalAsync(entry, cancellationToken);
        _evictionPolicy.OnEntryAdded(entry);
        _statistics.RecordEntryAdded(entry.SizeInBytes);

        // Check if eviction is needed
        CheckAndPerformEviction();
    }

    public virtual void PutMany(IEnumerable<KeyValuePair<TKey, TValue>> items, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (items == null) throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            Put(item.Key, item.Value, timeToLive, priority);
        }
    }

    public virtual bool PutIfAbsent(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        if (ContainsKey(key))
            return false;

        Put(key, value, timeToLive, priority);
        return true;
    }

    public virtual bool Remove(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));

        var entry = RemoveInternal(key);
        if (entry != null)
        {
            _evictionPolicy.OnEntryRemoved(entry);
            _statistics.RecordEntryRemoved(entry.SizeInBytes);
            entry.Dispose();
            return true;
        }
        return false;
    }

    public virtual int RemoveMany(IEnumerable<TKey> keys)
    {
        ThrowIfDisposed();
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        int removedCount = 0;
        foreach (var key in keys)
        {
            if (Remove(key))
                removedCount++;
        }
        return removedCount;
    }

    public virtual int ClearExpired()
    {
        ThrowIfDisposed();
        var expiredEntries = GetExpiredEntries().ToList();
        
        foreach (var entry in expiredEntries)
        {
            Remove(entry.Key);
        }

        if (expiredEntries.Count > 0)
        {
            _statistics.RecordExpired(expiredEntries.Count);
        }

        return expiredEntries.Count;
    }

    public virtual int Evict(long targetSize)
    {
        ThrowIfDisposed();
        
        var currentSize = SizeInBytes;
        if (currentSize <= targetSize)
            return 0;

        var targetReduction = currentSize - targetSize;
        var allEntries = GetAllEntries();
        var toEvict = _evictionPolicy.SelectForEviction(allEntries, int.MaxValue, targetReduction).ToList();

        foreach (var entry in toEvict)
        {
            Remove(entry.Key);
        }

        if (toEvict.Count > 0)
        {
            _statistics.RecordEviction(toEvict.Count);
        }

        return toEvict.Count;
    }

    public virtual async Task WarmupAsync(IEnumerable<KeyValuePair<TKey, TValue>> warmupData)
    {
        ThrowIfDisposed();
        if (warmupData == null) throw new ArgumentNullException(nameof(warmupData));

        foreach (var item in warmupData)
        {
            await PutAsync(item.Key, item.Value, priority: CacheEntryPriority.High);
        }
    }

    public abstract bool ContainsKey(TKey key);
    public abstract IEnumerable<TKey> GetKeys();
    public abstract void Clear();
    public abstract ICacheEntryMetadata? GetEntryMetadata(TKey key);

    // Protected abstract methods for subclasses to implement
    protected abstract TValue? GetInternal(TKey key);
    protected abstract Task<TValue?> GetInternalAsync(TKey key, CancellationToken cancellationToken);
    protected abstract void PutInternal(ICacheEntry<TKey, TValue> entry);
    protected abstract Task PutInternalAsync(ICacheEntry<TKey, TValue> entry, CancellationToken cancellationToken);
    protected abstract ICacheEntry<TKey, TValue>? RemoveInternal(TKey key);
    protected abstract IEnumerable<ICacheEntry<TKey, TValue>> GetAllEntries();
    protected abstract IEnumerable<ICacheEntry<TKey, TValue>> GetExpiredEntries();

    protected virtual void CheckAndPerformEviction()
    {
        if (Count > MaxCapacity || SizeInBytes > MaxSizeInBytes)
        {
            var targetSize = (long)(MaxSizeInBytes * 0.8); // Evict to 80% capacity
            Evict(targetSize);
        }
    }

    protected virtual void PerformCleanup(object? state)
    {
        try
        {
            ClearExpired();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    protected void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public virtual void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cleanupTimer?.Dispose();
        Clear();
    }
}
