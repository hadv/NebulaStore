using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types.Memory;

/// <summary>
/// Manages memory allocation and caching following Eclipse Store patterns.
/// Provides entity cache management, memory-mapped file handling, and cache eviction policies.
/// </summary>
public class MemoryManager : IDisposable
{
    #region Private Fields

    private readonly long _cacheThreshold;
    private readonly TimeSpan _cacheTimeout;
    private readonly ConcurrentDictionary<long, CacheEntry> _cache;
    private readonly Timer _evictionTimer;
    private readonly object _lock = new();

    private long _currentCacheSize;
    private long _totalAllocations;
    private long _totalEvictions;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the MemoryManager class.
    /// </summary>
    /// <param name="cacheThreshold">The cache size threshold in bytes.</param>
    /// <param name="cacheTimeout">The cache entry timeout.</param>
    public MemoryManager(long cacheThreshold, TimeSpan cacheTimeout)
    {
        _cacheThreshold = cacheThreshold;
        _cacheTimeout = cacheTimeout;
        _cache = new ConcurrentDictionary<long, CacheEntry>();

        // Start eviction timer - runs every minute
        _evictionTimer = new Timer(PerformEviction, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    public long CurrentCacheSize => Interlocked.Read(ref _currentCacheSize);

    /// <summary>
    /// Gets the cache threshold in bytes.
    /// </summary>
    public long CacheThreshold => _cacheThreshold;

    /// <summary>
    /// Gets the total number of allocations.
    /// </summary>
    public long TotalAllocations => Interlocked.Read(ref _totalAllocations);

    /// <summary>
    /// Gets the total number of evictions.
    /// </summary>
    public long TotalEvictions => Interlocked.Read(ref _totalEvictions);

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    public int CacheEntryCount => _cache.Count;

    /// <summary>
    /// Gets the cache utilization percentage.
    /// </summary>
    public double CacheUtilization => _cacheThreshold > 0 ? (double)CurrentCacheSize / _cacheThreshold * 100 : 0;

    #endregion

    #region Public Methods

    /// <summary>
    /// Allocates memory for an entity following Eclipse Store patterns.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="size">The size to allocate.</param>
    /// <returns>The allocated memory address.</returns>
    public IntPtr AllocateEntityMemory(long entityId, long size)
    {
        ThrowIfDisposed();

        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));

        // Check if we need to evict before allocating
        if (CurrentCacheSize + size > _cacheThreshold)
        {
            PerformEviction(null);
        }

        // Allocate memory
        var address = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)size);

        // Create cache entry
        var entry = new CacheEntry
        {
            EntityId = entityId,
            Address = address,
            Size = size,
            CreatedTime = DateTime.UtcNow,
            LastAccessTime = DateTime.UtcNow
        };

        // Initialize access count
        entry.Touch();

        // Add to cache
        if (_cache.TryAdd(entityId, entry))
        {
            Interlocked.Add(ref _currentCacheSize, size);
            Interlocked.Increment(ref _totalAllocations);
        }
        else
        {
            // Entity already exists, free the allocated memory
            System.Runtime.InteropServices.Marshal.FreeHGlobal(address);

            // Return existing entry's address
            if (_cache.TryGetValue(entityId, out var existing))
            {
                existing.Touch();
                return existing.Address;
            }

            throw new InvalidOperationException($"Failed to allocate memory for entity {entityId}");
        }

        return address;
    }

    /// <summary>
    /// Gets cached memory for an entity following Eclipse Store patterns.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The cached memory address, or IntPtr.Zero if not cached.</returns>
    public IntPtr GetEntityMemory(long entityId)
    {
        ThrowIfDisposed();

        if (_cache.TryGetValue(entityId, out var entry))
        {
            entry.Touch();
            return entry.Address;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Releases memory for an entity following Eclipse Store patterns.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>True if memory was released, false if entity was not cached.</returns>
    public bool ReleaseEntityMemory(long entityId)
    {
        ThrowIfDisposed();

        if (_cache.TryRemove(entityId, out var entry))
        {
            entry.Dispose();
            Interlocked.Add(ref _currentCacheSize, -entry.Size);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs cache eviction based on Eclipse Store policies.
    /// </summary>
    /// <param name="targetSize">The target size to free, or null for automatic eviction.</param>
    /// <returns>The amount of memory freed.</returns>
    public long PerformEviction(long? targetSize = null)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var target = targetSize ?? (CurrentCacheSize - _cacheThreshold * 0.8); // Evict to 80% of threshold
            if (target <= 0)
                return 0;

            var freedMemory = 0L;
            var evictionCandidates = new List<CacheEntry>();

            // Collect eviction candidates based on Eclipse Store LRU + timeout policy
            var cutoffTime = DateTime.UtcNow - _cacheTimeout;

            foreach (var entry in _cache.Values)
            {
                // Evict if expired or if we need space and it's least recently used
                if (entry.LastAccessTime < cutoffTime || freedMemory < target)
                {
                    evictionCandidates.Add(entry);
                }
            }

            // Sort by last access time (oldest first)
            evictionCandidates.Sort((a, b) => a.LastAccessTime.CompareTo(b.LastAccessTime));

            // Evict entries until we reach target
            foreach (var entry in evictionCandidates)
            {
                if (freedMemory >= target)
                    break;

                if (_cache.TryRemove(entry.EntityId, out var removed))
                {
                    removed.Dispose();
                    freedMemory += removed.Size;
                    Interlocked.Add(ref _currentCacheSize, -removed.Size);
                    Interlocked.Increment(ref _totalEvictions);
                }
            }

            return freedMemory;
        }
    }

    /// <summary>
    /// Clears all cached memory following Eclipse Store patterns.
    /// </summary>
    /// <returns>The amount of memory freed.</returns>
    public long ClearCache()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var freedMemory = 0L;

            foreach (var entry in _cache.Values)
            {
                entry.Dispose();
                freedMemory += entry.Size;
            }

            _cache.Clear();
            Interlocked.Exchange(ref _currentCacheSize, 0);

            return freedMemory;
        }
    }

    /// <summary>
    /// Gets memory statistics following Eclipse Store patterns.
    /// </summary>
    /// <returns>The memory statistics.</returns>
    public MemoryStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new MemoryStatistics
        {
            CurrentCacheSize = CurrentCacheSize,
            CacheThreshold = _cacheThreshold,
            CacheEntryCount = CacheEntryCount,
            CacheUtilization = CacheUtilization,
            TotalAllocations = TotalAllocations,
            TotalEvictions = TotalEvictions,
            CacheTimeout = _cacheTimeout
        };
    }

    /// <summary>
    /// Checks if an entity is cached.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>True if the entity is cached, false otherwise.</returns>
    public bool IsEntityCached(long entityId)
    {
        ThrowIfDisposed();
        return _cache.ContainsKey(entityId);
    }

    /// <summary>
    /// Gets the size of a cached entity.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The size of the cached entity, or 0 if not cached.</returns>
    public long GetEntitySize(long entityId)
    {
        ThrowIfDisposed();

        if (_cache.TryGetValue(entityId, out var entry))
        {
            return entry.Size;
        }

        return 0;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Timer callback for periodic eviction.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void PerformEviction(object? state)
    {
        try
        {
            if (!_disposed)
            {
                PerformEviction();
            }
        }
        catch
        {
            // Ignore eviction errors to prevent timer from stopping
        }
    }

    /// <summary>
    /// Throws if the manager is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryManager));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the memory manager and releases all cached memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;

            _evictionTimer?.Dispose();

            // Free all cached memory
            foreach (var entry in _cache.Values)
            {
                entry.Dispose();
            }

            _cache.Clear();
            Interlocked.Exchange(ref _currentCacheSize, 0);
        }
    }

    #endregion
}

/// <summary>
/// Represents a cache entry following Eclipse Store patterns.
/// </summary>
internal class CacheEntry : IDisposable
{
    public long EntityId { get; set; }
    public IntPtr Address { get; set; }
    public long Size { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastAccessTime { get; set; }

    private long _accessCount;
    private bool _disposed;

    /// <summary>
    /// Gets the access count.
    /// </summary>
    public long AccessCount => _accessCount;

    /// <summary>
    /// Updates the last access time and increments access count.
    /// </summary>
    public void Touch()
    {
        if (!_disposed)
        {
            LastAccessTime = DateTime.UtcNow;
            Interlocked.Increment(ref _accessCount);
        }
    }

    /// <summary>
    /// Disposes the cache entry and frees its memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (Address != IntPtr.Zero)
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(Address);
            Address = IntPtr.Zero;
        }
    }
}