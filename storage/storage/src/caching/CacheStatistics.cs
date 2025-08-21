using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Thread-safe implementation of cache statistics.
/// </summary>
public class CacheStatistics : ICacheStatistics
{
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private long _expiredCount;
    private long _currentSizeInBytes;
    private long _currentEntryCount;
    private long _totalAccessTimeMs;
    private readonly long _maxSizeInBytes;
    private readonly long _maxEntryCount;

    public CacheStatistics(long maxSizeInBytes, long maxEntryCount)
    {
        _maxSizeInBytes = maxSizeInBytes;
        _maxEntryCount = maxEntryCount;
    }

    public long HitCount => Interlocked.Read(ref _hitCount);

    public long MissCount => Interlocked.Read(ref _missCount);

    public long RequestCount => HitCount + MissCount;

    public double HitRatio
    {
        get
        {
            var requests = RequestCount;
            return requests == 0 ? 0.0 : (double)HitCount / requests;
        }
    }

    public long EvictionCount => Interlocked.Read(ref _evictionCount);

    public long ExpiredCount => Interlocked.Read(ref _expiredCount);

    public double AverageAccessTimeMs
    {
        get
        {
            var requests = RequestCount;
            return requests == 0 ? 0.0 : (double)Interlocked.Read(ref _totalAccessTimeMs) / requests;
        }
    }

    public long CurrentSizeInBytes => Interlocked.Read(ref _currentSizeInBytes);

    public long MaxSizeInBytes => _maxSizeInBytes;

    public long CurrentEntryCount => Interlocked.Read(ref _currentEntryCount);

    public long MaxEntryCount => _maxEntryCount;

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    /// <param name="accessTimeMs">Access time in milliseconds</param>
    public void RecordHit(long accessTimeMs = 0)
    {
        Interlocked.Increment(ref _hitCount);
        if (accessTimeMs > 0)
        {
            Interlocked.Add(ref _totalAccessTimeMs, accessTimeMs);
        }
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    /// <param name="accessTimeMs">Access time in milliseconds</param>
    public void RecordMiss(long accessTimeMs = 0)
    {
        Interlocked.Increment(ref _missCount);
        if (accessTimeMs > 0)
        {
            Interlocked.Add(ref _totalAccessTimeMs, accessTimeMs);
        }
    }

    /// <summary>
    /// Records an eviction.
    /// </summary>
    /// <param name="count">Number of entries evicted</param>
    public void RecordEviction(long count = 1)
    {
        Interlocked.Add(ref _evictionCount, count);
    }

    /// <summary>
    /// Records expired entries removal.
    /// </summary>
    /// <param name="count">Number of expired entries removed</param>
    public void RecordExpired(long count = 1)
    {
        Interlocked.Add(ref _expiredCount, count);
    }

    /// <summary>
    /// Records an entry addition.
    /// </summary>
    /// <param name="sizeInBytes">Size of the added entry</param>
    public void RecordEntryAdded(long sizeInBytes)
    {
        Interlocked.Increment(ref _currentEntryCount);
        Interlocked.Add(ref _currentSizeInBytes, sizeInBytes);
    }

    /// <summary>
    /// Records an entry removal.
    /// </summary>
    /// <param name="sizeInBytes">Size of the removed entry</param>
    public void RecordEntryRemoved(long sizeInBytes)
    {
        Interlocked.Decrement(ref _currentEntryCount);
        Interlocked.Add(ref _currentSizeInBytes, -sizeInBytes);
    }

    /// <summary>
    /// Updates the current size and entry count.
    /// </summary>
    /// <param name="sizeInBytes">Current size in bytes</param>
    /// <param name="entryCount">Current entry count</param>
    public void UpdateCurrentStats(long sizeInBytes, long entryCount)
    {
        Interlocked.Exchange(ref _currentSizeInBytes, sizeInBytes);
        Interlocked.Exchange(ref _currentEntryCount, entryCount);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
        Interlocked.Exchange(ref _evictionCount, 0);
        Interlocked.Exchange(ref _expiredCount, 0);
        Interlocked.Exchange(ref _totalAccessTimeMs, 0);
        // Note: We don't reset current size and entry count as they represent current state
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public CacheStatisticsSnapshot GetSnapshot()
    {
        return new CacheStatisticsSnapshot(
            HitCount,
            MissCount,
            RequestCount,
            HitRatio,
            EvictionCount,
            ExpiredCount,
            AverageAccessTimeMs,
            CurrentSizeInBytes,
            MaxSizeInBytes,
            CurrentEntryCount,
            MaxEntryCount,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of cache statistics at a point in time.
/// </summary>
public class CacheStatisticsSnapshot
{
    public CacheStatisticsSnapshot(
        long hitCount,
        long missCount,
        long requestCount,
        double hitRatio,
        long evictionCount,
        long expiredCount,
        double averageAccessTimeMs,
        long currentSizeInBytes,
        long maxSizeInBytes,
        long currentEntryCount,
        long maxEntryCount,
        DateTime timestamp)
    {
        HitCount = hitCount;
        MissCount = missCount;
        RequestCount = requestCount;
        HitRatio = hitRatio;
        EvictionCount = evictionCount;
        ExpiredCount = expiredCount;
        AverageAccessTimeMs = averageAccessTimeMs;
        CurrentSizeInBytes = currentSizeInBytes;
        MaxSizeInBytes = maxSizeInBytes;
        CurrentEntryCount = currentEntryCount;
        MaxEntryCount = maxEntryCount;
        Timestamp = timestamp;
    }

    public long HitCount { get; }
    public long MissCount { get; }
    public long RequestCount { get; }
    public double HitRatio { get; }
    public long EvictionCount { get; }
    public long ExpiredCount { get; }
    public double AverageAccessTimeMs { get; }
    public long CurrentSizeInBytes { get; }
    public long MaxSizeInBytes { get; }
    public long CurrentEntryCount { get; }
    public long MaxEntryCount { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Cache Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Hits={HitCount}, Misses={MissCount}, HitRatio={HitRatio:P2}, " +
               $"Entries={CurrentEntryCount}/{MaxEntryCount}, " +
               $"Size={CurrentSizeInBytes:N0}/{MaxSizeInBytes:N0} bytes, " +
               $"Evictions={EvictionCount}, Expired={ExpiredCount}, " +
               $"AvgAccessTime={AverageAccessTimeMs:F2}ms";
    }
}
