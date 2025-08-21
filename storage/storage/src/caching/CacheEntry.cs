using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Default implementation of cache entry with metadata and lifecycle management.
/// </summary>
public class CacheEntry<TKey, TValue> : ICacheEntry<TKey, TValue>
{
    private readonly object _lock = new();
    private TValue _value;
    private DateTime _lastAccessedAt;
    private DateTime _lastModifiedAt;
    private long _accessCount;
    private bool _isDirty;
    private CacheEntryPriority _priority;
    private bool _isDisposed;

    public CacheEntry(TKey key, TValue value, TimeSpan? timeToLive = null, CacheEntryPriority priority = CacheEntryPriority.Normal)
    {
        Key = key;
        _value = value;
        TimeToLive = timeToLive;
        _priority = priority;
        
        var now = DateTime.UtcNow;
        CreatedAt = now;
        _lastAccessedAt = now;
        _lastModifiedAt = now;
        _accessCount = 0;
        _isDirty = false;

        // Calculate size estimate
        SizeInBytes = EstimateSize(key, value);
    }

    public TKey Key { get; }

    public TValue Value
    {
        get
        {
            lock (_lock)
            {
                MarkAccessed();
                return _value;
            }
        }
        set
        {
            lock (_lock)
            {
                _value = value;
                MarkModified();
                SizeInBytes = EstimateSize(Key, value);
            }
        }
    }

    public DateTime CreatedAt { get; }

    public DateTime LastAccessedAt
    {
        get
        {
            lock (_lock)
            {
                return _lastAccessedAt;
            }
        }
    }

    public DateTime LastModifiedAt
    {
        get
        {
            lock (_lock)
            {
                return _lastModifiedAt;
            }
        }
    }

    public long AccessCount
    {
        get
        {
            lock (_lock)
            {
                return _accessCount;
            }
        }
    }

    public long SizeInBytes { get; private set; }

    public TimeSpan? TimeToLive { get; }

    public bool IsExpired
    {
        get
        {
            if (TimeToLive == null)
                return false;

            return DateTime.UtcNow - CreatedAt > TimeToLive.Value;
        }
    }

    public bool IsDirty
    {
        get
        {
            lock (_lock)
            {
                return _isDirty;
            }
        }
        set
        {
            lock (_lock)
            {
                _isDirty = value;
            }
        }
    }

    public CacheEntryPriority Priority
    {
        get
        {
            lock (_lock)
            {
                return _priority;
            }
        }
        set
        {
            lock (_lock)
            {
                _priority = value;
            }
        }
    }

    public void MarkAccessed()
    {
        lock (_lock)
        {
            _lastAccessedAt = DateTime.UtcNow;
            Interlocked.Increment(ref _accessCount);
        }
    }

    public void MarkModified()
    {
        lock (_lock)
        {
            _lastModifiedAt = DateTime.UtcNow;
            _isDirty = true;
        }
    }

    public void RefreshExpiration()
    {
        // For this implementation, we don't support refreshing expiration
        // as it's based on creation time. This could be enhanced to support
        // sliding expiration in the future.
    }

    public ICacheEntryMetadata GetMetadata()
    {
        lock (_lock)
        {
            return new CacheEntryMetadata(
                CreatedAt,
                _lastAccessedAt,
                _lastModifiedAt,
                _accessCount,
                SizeInBytes,
                _priority,
                _isDirty,
                IsExpired
            );
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        lock (_lock)
        {
            if (_isDisposed)
                return;

            // Dispose value if it implements IDisposable
            if (_value is IDisposable disposableValue)
            {
                disposableValue.Dispose();
            }

            _isDisposed = true;
        }
    }

    private static long EstimateSize(TKey key, TValue value)
    {
        // Basic size estimation - this could be made more sophisticated
        long size = 0;

        // Estimate key size
        if (key != null)
        {
            if (key is string keyStr)
                size += keyStr.Length * 2; // Unicode characters
            else
                size += 64; // Rough estimate for other types
        }

        // Estimate value size
        if (value != null)
        {
            if (value is string valueStr)
                size += valueStr.Length * 2;
            else if (value is byte[] byteArray)
                size += byteArray.Length;
            else
                size += 256; // Rough estimate for complex objects
        }

        // Add overhead for the cache entry itself
        size += 128;

        return size;
    }
}

/// <summary>
/// Implementation of cache entry metadata.
/// </summary>
public class CacheEntryMetadata : ICacheEntryMetadata
{
    public CacheEntryMetadata(
        DateTime createdAt,
        DateTime lastAccessedAt,
        DateTime lastModifiedAt,
        long accessCount,
        long sizeInBytes,
        CacheEntryPriority priority,
        bool isDirty,
        bool isExpired)
    {
        CreatedAt = createdAt;
        LastAccessedAt = lastAccessedAt;
        LastModifiedAt = lastModifiedAt;
        AccessCount = accessCount;
        SizeInBytes = sizeInBytes;
        Priority = priority;
        IsDirty = isDirty;
        IsExpired = isExpired;
    }

    public DateTime CreatedAt { get; }
    public DateTime LastAccessedAt { get; }
    public DateTime LastModifiedAt { get; }
    public long AccessCount { get; }
    public long SizeInBytes { get; }
    public CacheEntryPriority Priority { get; }
    public bool IsDirty { get; }
    public bool IsExpired { get; }
}
