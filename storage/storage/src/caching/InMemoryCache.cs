using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// High-performance in-memory cache implementation with thread-safe concurrent access.
/// Uses ConcurrentDictionary for lock-free reads and writes where possible.
/// </summary>
public class InMemoryCache<TKey, TValue> : AbstractCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, ICacheEntry<TKey, TValue>> _entries;
    private readonly ReaderWriterLockSlim _evictionLock;
    private volatile long _currentSizeInBytes;

    public InMemoryCache(
        string name,
        long maxCapacity = 10000,
        long maxSizeInBytes = 100 * 1024 * 1024, // 100MB default
        ICacheEvictionPolicy<TKey, TValue>? evictionPolicy = null,
        TimeSpan? cleanupInterval = null)
        : base(
            name,
            maxCapacity,
            maxSizeInBytes,
            evictionPolicy ?? new LruEvictionPolicy<TKey, TValue>(),
            cleanupInterval ?? TimeSpan.FromMinutes(5))
    {
        _entries = new ConcurrentDictionary<TKey, ICacheEntry<TKey, TValue>>();
        _evictionLock = new ReaderWriterLockSlim();
        _currentSizeInBytes = 0;
    }

    public override long Count => _entries.Count;

    public override long SizeInBytes => Interlocked.Read(ref _currentSizeInBytes);

    public override double HitRatio => _statistics.HitRatio;

    protected override TValue? GetInternal(TKey key)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            // Check if entry is expired
            if (entry.IsExpired)
            {
                // Remove expired entry
                if (_entries.TryRemove(key, out var removedEntry))
                {
                    Interlocked.Add(ref _currentSizeInBytes, -removedEntry.SizeInBytes);
                    _statistics.RecordEntryRemoved(removedEntry.SizeInBytes);
                    removedEntry.Dispose();
                }
                return default;
            }

            // Mark as accessed and notify eviction policy
            entry.MarkAccessed();
            _evictionPolicy.OnEntryAccessed(entry);
            
            return entry.Value;
        }

        return default;
    }

    protected override async Task<TValue?> GetInternalAsync(TKey key, CancellationToken cancellationToken)
    {
        // For in-memory cache, async is the same as sync
        await Task.Yield(); // Yield to allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();
        
        return GetInternal(key);
    }

    protected override void PutInternal(ICacheEntry<TKey, TValue> entry)
    {
        var key = entry.Key;
        
        // Use AddOrUpdate for atomic operation
        var addedEntry = _entries.AddOrUpdate(
            key,
            entry,
            (k, existingEntry) =>
            {
                // Update existing entry
                var oldSize = existingEntry.SizeInBytes;
                existingEntry.Value = entry.Value;
                existingEntry.MarkModified();
                
                // Update size tracking
                var sizeDelta = existingEntry.SizeInBytes - oldSize;
                Interlocked.Add(ref _currentSizeInBytes, sizeDelta);
                
                return existingEntry;
            });

        // If this was a new entry, update size tracking
        if (ReferenceEquals(addedEntry, entry))
        {
            Interlocked.Add(ref _currentSizeInBytes, entry.SizeInBytes);
        }
    }

    protected override async Task PutInternalAsync(ICacheEntry<TKey, TValue> entry, CancellationToken cancellationToken)
    {
        // For in-memory cache, async is the same as sync
        await Task.Yield(); // Yield to allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();
        
        PutInternal(entry);
    }

    protected override ICacheEntry<TKey, TValue>? RemoveInternal(TKey key)
    {
        if (_entries.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref _currentSizeInBytes, -entry.SizeInBytes);
            return entry;
        }
        return null;
    }

    protected override IEnumerable<ICacheEntry<TKey, TValue>> GetAllEntries()
    {
        return _entries.Values.ToList(); // Create snapshot to avoid enumeration issues
    }

    protected override IEnumerable<ICacheEntry<TKey, TValue>> GetExpiredEntries()
    {
        return _entries.Values.Where(entry => entry.IsExpired).ToList();
    }

    public override bool ContainsKey(TKey key)
    {
        return _entries.ContainsKey(key) && !_entries[key].IsExpired;
    }

    public override IEnumerable<TKey> GetKeys()
    {
        return _entries.Keys.ToList(); // Create snapshot
    }

    public override void Clear()
    {
        _evictionLock.EnterWriteLock();
        try
        {
            // Dispose all entries
            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }
            
            _entries.Clear();
            Interlocked.Exchange(ref _currentSizeInBytes, 0);
            _statistics.UpdateCurrentStats(0, 0);
        }
        finally
        {
            _evictionLock.ExitWriteLock();
        }
    }

    public override ICacheEntryMetadata? GetEntryMetadata(TKey key)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            return entry.GetMetadata();
        }
        return null;
    }

    protected override void CheckAndPerformEviction()
    {
        // Use read lock first to check if eviction is needed
        _evictionLock.EnterReadLock();
        try
        {
            if (Count <= MaxCapacity && SizeInBytes <= MaxSizeInBytes)
                return;
        }
        finally
        {
            _evictionLock.ExitReadLock();
        }

        // Eviction is needed, acquire write lock
        _evictionLock.EnterWriteLock();
        try
        {
            // Double-check after acquiring write lock
            if (Count <= MaxCapacity && SizeInBytes <= MaxSizeInBytes)
                return;

            var currentSize = SizeInBytes;
            var targetSize = (long)(MaxSizeInBytes * 0.8); // Evict to 80% capacity
            var targetCount = (long)(MaxCapacity * 0.8); // Evict to 80% capacity

            var allEntries = GetAllEntries().ToList();
            var targetReduction = Math.Max(0, currentSize - targetSize);
            var targetEvictionCount = Math.Max(0, (int)(Count - targetCount));

            var toEvict = _evictionPolicy.SelectForEviction(allEntries, targetEvictionCount, targetReduction).ToList();

            foreach (var entry in toEvict)
            {
                if (_entries.TryRemove(entry.Key, out var removedEntry))
                {
                    Interlocked.Add(ref _currentSizeInBytes, -removedEntry.SizeInBytes);
                    _evictionPolicy.OnEntryRemoved(removedEntry);
                    _statistics.RecordEntryRemoved(removedEntry.SizeInBytes);
                    removedEntry.Dispose();
                }
            }

            if (toEvict.Count > 0)
            {
                _statistics.RecordEviction(toEvict.Count);
            }
        }
        finally
        {
            _evictionLock.ExitWriteLock();
        }
    }

    public override void Dispose()
    {
        if (_isDisposed)
            return;

        base.Dispose();
        _evictionLock.Dispose();
    }

    /// <summary>
    /// Gets cache performance metrics.
    /// </summary>
    /// <returns>Performance metrics snapshot</returns>
    public CachePerformanceMetrics GetPerformanceMetrics()
    {
        var stats = _statistics.GetSnapshot();
        return new CachePerformanceMetrics(
            Name,
            stats.CurrentEntryCount,
            stats.MaxEntryCount,
            stats.CurrentSizeInBytes,
            stats.MaxSizeInBytes,
            stats.HitRatio,
            stats.AverageAccessTimeMs,
            stats.EvictionCount,
            stats.ExpiredCount,
            GetMemoryPressure(),
            stats.Timestamp
        );
    }

    private double GetMemoryPressure()
    {
        var sizeRatio = (double)SizeInBytes / MaxSizeInBytes;
        var countRatio = (double)Count / MaxCapacity;
        return Math.Max(sizeRatio, countRatio);
    }
}

/// <summary>
/// Performance metrics for cache monitoring.
/// </summary>
public class CachePerformanceMetrics
{
    public CachePerformanceMetrics(
        string cacheName,
        long currentEntryCount,
        long maxEntryCount,
        long currentSizeInBytes,
        long maxSizeInBytes,
        double hitRatio,
        double averageAccessTimeMs,
        long evictionCount,
        long expiredCount,
        double memoryPressure,
        DateTime timestamp)
    {
        CacheName = cacheName;
        CurrentEntryCount = currentEntryCount;
        MaxEntryCount = maxEntryCount;
        CurrentSizeInBytes = currentSizeInBytes;
        MaxSizeInBytes = maxSizeInBytes;
        HitRatio = hitRatio;
        AverageAccessTimeMs = averageAccessTimeMs;
        EvictionCount = evictionCount;
        ExpiredCount = expiredCount;
        MemoryPressure = memoryPressure;
        Timestamp = timestamp;
    }

    public string CacheName { get; }
    public long CurrentEntryCount { get; }
    public long MaxEntryCount { get; }
    public long CurrentSizeInBytes { get; }
    public long MaxSizeInBytes { get; }
    public double HitRatio { get; }
    public double AverageAccessTimeMs { get; }
    public long EvictionCount { get; }
    public long ExpiredCount { get; }
    public double MemoryPressure { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Cache '{CacheName}' [{Timestamp:HH:mm:ss}]: " +
               $"Entries={CurrentEntryCount:N0}/{MaxEntryCount:N0} ({CurrentEntryCount * 100.0 / MaxEntryCount:F1}%), " +
               $"Size={CurrentSizeInBytes:N0}/{MaxSizeInBytes:N0} bytes ({CurrentSizeInBytes * 100.0 / MaxSizeInBytes:F1}%), " +
               $"HitRatio={HitRatio:P2}, AvgAccess={AverageAccessTimeMs:F2}ms, " +
               $"Pressure={MemoryPressure:P1}, Evictions={EvictionCount:N0}";
    }
}
