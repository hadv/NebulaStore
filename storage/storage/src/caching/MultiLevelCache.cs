using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Multi-level cache that combines L1 (in-memory) and L2 (disk-based) caches.
/// Provides automatic promotion/demotion between cache levels based on access patterns.
/// </summary>
public class MultiLevelCache<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private readonly ICache<TKey, TValue> _l1Cache;
    private readonly ICache<TKey, TValue> _l2Cache;
    private readonly MultiLevelCacheConfiguration _configuration;
    private readonly CacheStatistics _statistics;
    private readonly Timer? _promotionTimer;
    private volatile bool _isDisposed;

    public MultiLevelCache(
        ICache<TKey, TValue> l1Cache,
        ICache<TKey, TValue> l2Cache,
        MultiLevelCacheConfiguration? configuration = null)
    {
        _l1Cache = l1Cache ?? throw new ArgumentNullException(nameof(l1Cache));
        _l2Cache = l2Cache ?? throw new ArgumentNullException(nameof(l2Cache));
        _configuration = configuration ?? new MultiLevelCacheConfiguration();
        _statistics = new CacheStatistics(_l1Cache.MaxSizeInBytes + _l2Cache.MaxSizeInBytes, _l1Cache.MaxCapacity + _l2Cache.MaxCapacity);

        // Set up promotion timer if enabled
        if (_configuration.EnableAutoPromotion && _configuration.PromotionInterval > TimeSpan.Zero)
        {
            _promotionTimer = new Timer(PerformAutoPromotion, null, _configuration.PromotionInterval, _configuration.PromotionInterval);
        }
    }

    public string Name => $"MultiLevel[{_l1Cache.Name},{_l2Cache.Name}]";

    public long Count => _l1Cache.Count + _l2Cache.Count;

    public long MaxCapacity => _l1Cache.MaxCapacity + _l2Cache.MaxCapacity;

    public long SizeInBytes => _l1Cache.SizeInBytes + _l2Cache.SizeInBytes;

    public long MaxSizeInBytes => _l1Cache.MaxSizeInBytes + _l2Cache.MaxSizeInBytes;

    public double HitRatio => _statistics.HitRatio;

    public ICacheStatistics Statistics => _statistics;

    public TValue? Get(TKey key)
    {
        ThrowIfDisposed();

        // Try L1 cache first
        var value = _l1Cache.Get(key);
        if (value != null)
        {
            _statistics.RecordHit();
            return value;
        }

        // Try L2 cache
        value = _l2Cache.Get(key);
        if (value != null)
        {
            _statistics.RecordHit();
            
            // Promote to L1 if configured
            if (_configuration.EnableAutoPromotion)
            {
                PromoteToL1(key, value);
            }
            
            return value;
        }

        _statistics.RecordMiss();
        return default;
    }

    public async Task<TValue?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        // Try L1 cache first
        var value = await _l1Cache.GetAsync(key, cancellationToken);
        if (value != null)
        {
            _statistics.RecordHit();
            return value;
        }

        // Try L2 cache
        value = await _l2Cache.GetAsync(key, cancellationToken);
        if (value != null)
        {
            _statistics.RecordHit();
            
            // Promote to L1 if configured
            if (_configuration.EnableAutoPromotion)
            {
                await PromoteToL1Async(key, value, cancellationToken);
            }
            
            return value;
        }

        _statistics.RecordMiss();
        return default;
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        ThrowIfDisposed();

        // Try L1 cache first
        if (_l1Cache.TryGet(key, out value))
        {
            _statistics.RecordHit();
            return true;
        }

        // Try L2 cache
        if (_l2Cache.TryGet(key, out value))
        {
            _statistics.RecordHit();
            
            // Promote to L1 if configured
            if (_configuration.EnableAutoPromotion && value != null)
            {
                PromoteToL1(key, value);
            }
            
            return true;
        }

        _statistics.RecordMiss();
        value = default;
        return false;
    }

    public Dictionary<TKey, TValue> GetMany(IEnumerable<TKey> keys)
    {
        ThrowIfDisposed();
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        var result = new Dictionary<TKey, TValue>();
        var remainingKeys = new List<TKey>();

        // Try L1 cache first
        foreach (var key in keys)
        {
            var value = _l1Cache.Get(key);
            if (value != null)
            {
                result[key] = value;
                _statistics.RecordHit();
            }
            else
            {
                remainingKeys.Add(key);
            }
        }

        // Try L2 cache for remaining keys
        if (remainingKeys.Count > 0)
        {
            var l2Results = _l2Cache.GetMany(remainingKeys);
            foreach (var kvp in l2Results)
            {
                result[kvp.Key] = kvp.Value;
                _statistics.RecordHit();
                
                // Promote to L1 if configured
                if (_configuration.EnableAutoPromotion)
                {
                    PromoteToL1(kvp.Key, kvp.Value);
                }
            }

            // Record misses for keys not found in either cache
            var foundKeys = l2Results.Keys.ToHashSet();
            foreach (var key in remainingKeys)
            {
                if (!foundKeys.Contains(key))
                {
                    _statistics.RecordMiss();
                }
            }
        }

        return result;
    }

    public async Task<Dictionary<TKey, TValue>> GetManyAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        var result = new Dictionary<TKey, TValue>();
        var remainingKeys = new List<TKey>();

        // Try L1 cache first
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await _l1Cache.GetAsync(key, cancellationToken);
            if (value != null)
            {
                result[key] = value;
                _statistics.RecordHit();
            }
            else
            {
                remainingKeys.Add(key);
            }
        }

        // Try L2 cache for remaining keys
        if (remainingKeys.Count > 0)
        {
            var l2Results = await _l2Cache.GetManyAsync(remainingKeys, cancellationToken);
            foreach (var kvp in l2Results)
            {
                result[kvp.Key] = kvp.Value;
                _statistics.RecordHit();
                
                // Promote to L1 if configured
                if (_configuration.EnableAutoPromotion)
                {
                    await PromoteToL1Async(kvp.Key, kvp.Value, cancellationToken);
                }
            }

            // Record misses for keys not found in either cache
            var foundKeys = l2Results.Keys.ToHashSet();
            foreach (var key in remainingKeys)
            {
                if (!foundKeys.Contains(key))
                {
                    _statistics.RecordMiss();
                }
            }
        }

        return result;
    }

    public void Put(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        // Always put in L1 cache first
        _l1Cache.Put(key, value, timeToLive, priority);
        
        // Put in L2 cache based on configuration
        if (_configuration.WriteThrough || priority >= _configuration.L2WriteThreshold)
        {
            _l2Cache.Put(key, value, timeToLive, priority);
        }

        _statistics.RecordEntryAdded(EstimateSize(key, value));
    }

    public async Task PutAsync(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        // Always put in L1 cache first
        await _l1Cache.PutAsync(key, value, timeToLive, priority, cancellationToken);
        
        // Put in L2 cache based on configuration
        if (_configuration.WriteThrough || priority >= _configuration.L2WriteThreshold)
        {
            await _l2Cache.PutAsync(key, value, timeToLive, priority, cancellationToken);
        }

        _statistics.RecordEntryAdded(EstimateSize(key, value));
    }

    public void PutMany(IEnumerable<KeyValuePair<TKey, TValue>> items, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (items == null) throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            Put(item.Key, item.Value, timeToLive, priority);
        }
    }

    public bool PutIfAbsent(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        // Check if key exists in either cache
        if (ContainsKey(key))
            return false;

        Put(key, value, timeToLive, priority);
        return true;
    }

    public bool Remove(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));

        var removedFromL1 = _l1Cache.Remove(key);
        var removedFromL2 = _l2Cache.Remove(key);

        if (removedFromL1 || removedFromL2)
        {
            _statistics.RecordEntryRemoved(EstimateSize(key, default!));
            return true;
        }

        return false;
    }

    public int RemoveMany(IEnumerable<TKey> keys)
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

    public bool ContainsKey(TKey key)
    {
        ThrowIfDisposed();
        return _l1Cache.ContainsKey(key) || _l2Cache.ContainsKey(key);
    }

    public IEnumerable<TKey> GetKeys()
    {
        ThrowIfDisposed();
        return _l1Cache.GetKeys().Concat(_l2Cache.GetKeys()).Distinct();
    }

    public void Clear()
    {
        ThrowIfDisposed();
        _l1Cache.Clear();
        _l2Cache.Clear();
        _statistics.UpdateCurrentStats(0, 0);
    }

    public int ClearExpired()
    {
        ThrowIfDisposed();
        var l1Expired = _l1Cache.ClearExpired();
        var l2Expired = _l2Cache.ClearExpired();
        var totalExpired = l1Expired + l2Expired;
        
        if (totalExpired > 0)
        {
            _statistics.RecordExpired(totalExpired);
        }
        
        return totalExpired;
    }

    public int Evict(long targetSize)
    {
        ThrowIfDisposed();
        
        var currentSize = SizeInBytes;
        if (currentSize <= targetSize)
            return 0;

        // First try to evict from L1 cache
        var l1TargetSize = Math.Min(targetSize / 2, _l1Cache.SizeInBytes);
        var l1Evicted = _l1Cache.Evict(l1TargetSize);

        // Then evict from L2 cache if needed
        var remainingReduction = Math.Max(0, currentSize - targetSize - (l1Evicted * EstimateSize(default!, default!)));
        var l2Evicted = remainingReduction > 0 ? _l2Cache.Evict(_l2Cache.SizeInBytes - remainingReduction) : 0;

        var totalEvicted = l1Evicted + l2Evicted;
        if (totalEvicted > 0)
        {
            _statistics.RecordEviction(totalEvicted);
        }

        return totalEvicted;
    }

    public async Task WarmupAsync(IEnumerable<KeyValuePair<TKey, TValue>> warmupData)
    {
        ThrowIfDisposed();
        if (warmupData == null) throw new ArgumentNullException(nameof(warmupData));

        // Warm up both caches
        await _l1Cache.WarmupAsync(warmupData);
        await _l2Cache.WarmupAsync(warmupData);
    }

    public ICacheEntryMetadata? GetEntryMetadata(TKey key)
    {
        ThrowIfDisposed();
        
        // Try L1 first, then L2
        return _l1Cache.GetEntryMetadata(key) ?? _l2Cache.GetEntryMetadata(key);
    }

    private void PromoteToL1(TKey key, TValue value)
    {
        try
        {
            _l1Cache.Put(key, value, priority: CacheEntryPriority.High);
        }
        catch
        {
            // Ignore promotion errors
        }
    }

    private async Task PromoteToL1Async(TKey key, TValue value, CancellationToken cancellationToken)
    {
        try
        {
            await _l1Cache.PutAsync(key, value, priority: CacheEntryPriority.High, cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore promotion errors
        }
    }

    private void PerformAutoPromotion(object? state)
    {
        try
        {
            // Implement auto-promotion logic based on access patterns
            // This is a simplified implementation
            var l2Keys = _l2Cache.GetKeys().Take(_configuration.MaxPromotionBatchSize);
            foreach (var key in l2Keys)
            {
                var metadata = _l2Cache.GetEntryMetadata(key);
                if (metadata != null && metadata.AccessCount >= _configuration.PromotionAccessThreshold)
                {
                    var value = _l2Cache.Get(key);
                    if (value != null)
                    {
                        PromoteToL1(key, value);
                    }
                }
            }
        }
        catch
        {
            // Ignore auto-promotion errors
        }
    }

    private static long EstimateSize(TKey key, TValue value)
    {
        // Basic size estimation
        return 256; // Rough estimate
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MultiLevelCache<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _promotionTimer?.Dispose();
        _l1Cache.Dispose();
        _l2Cache.Dispose();
    }
}
