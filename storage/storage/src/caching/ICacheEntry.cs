using System;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for cache entries with metadata and lifecycle management.
/// </summary>
public interface ICacheEntry<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Gets the cache entry key.
    /// </summary>
    TKey Key { get; }

    /// <summary>
    /// Gets or sets the cached value.
    /// </summary>
    TValue Value { get; set; }

    /// <summary>
    /// Gets the creation time of this cache entry.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the last access time of this cache entry.
    /// </summary>
    DateTime LastAccessedAt { get; }

    /// <summary>
    /// Gets the last modification time of this cache entry.
    /// </summary>
    DateTime LastModifiedAt { get; }

    /// <summary>
    /// Gets the access count for this cache entry.
    /// </summary>
    long AccessCount { get; }

    /// <summary>
    /// Gets the size of this cache entry in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets the time-to-live for this cache entry.
    /// </summary>
    TimeSpan? TimeToLive { get; }

    /// <summary>
    /// Gets whether this cache entry has expired.
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// Gets whether this cache entry is dirty (modified but not persisted).
    /// </summary>
    bool IsDirty { get; set; }

    /// <summary>
    /// Gets the priority of this cache entry for eviction purposes.
    /// </summary>
    CacheEntryPriority Priority { get; set; }

    /// <summary>
    /// Marks this cache entry as accessed, updating access statistics.
    /// </summary>
    void MarkAccessed();

    /// <summary>
    /// Marks this cache entry as modified, updating modification time.
    /// </summary>
    void MarkModified();

    /// <summary>
    /// Refreshes the expiration time based on the current TTL.
    /// </summary>
    void RefreshExpiration();

    /// <summary>
    /// Gets the cache entry metadata.
    /// </summary>
    ICacheEntryMetadata GetMetadata();
}

/// <summary>
/// Interface for cache entry metadata.
/// </summary>
public interface ICacheEntryMetadata
{
    /// <summary>
    /// Gets the entry creation time.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the last access time.
    /// </summary>
    DateTime LastAccessedAt { get; }

    /// <summary>
    /// Gets the last modification time.
    /// </summary>
    DateTime LastModifiedAt { get; }

    /// <summary>
    /// Gets the access count.
    /// </summary>
    long AccessCount { get; }

    /// <summary>
    /// Gets the size in bytes.
    /// </summary>
    long SizeInBytes { get; }

    /// <summary>
    /// Gets the priority.
    /// </summary>
    CacheEntryPriority Priority { get; }

    /// <summary>
    /// Gets whether the entry is dirty.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Gets whether the entry is expired.
    /// </summary>
    bool IsExpired { get; }
}

/// <summary>
/// Enumeration of cache entry priorities for eviction.
/// </summary>
public enum CacheEntryPriority
{
    /// <summary>
    /// Low priority - first to be evicted.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority - default priority.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority - less likely to be evicted.
    /// </summary>
    High = 2,

    /// <summary>
    /// Never evict - should not be evicted unless explicitly removed.
    /// </summary>
    NeverEvict = 3
}
