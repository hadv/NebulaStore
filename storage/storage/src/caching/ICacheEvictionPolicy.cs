using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for cache eviction policies.
/// </summary>
public interface ICacheEvictionPolicy<TKey, TValue>
{
    /// <summary>
    /// Gets the name of this eviction policy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects entries to evict based on the policy.
    /// </summary>
    /// <param name="entries">Available cache entries</param>
    /// <param name="targetEvictionCount">Target number of entries to evict</param>
    /// <param name="targetSizeReduction">Target size reduction in bytes</param>
    /// <returns>Entries selected for eviction</returns>
    IEnumerable<ICacheEntry<TKey, TValue>> SelectForEviction(
        IEnumerable<ICacheEntry<TKey, TValue>> entries,
        int targetEvictionCount,
        long targetSizeReduction);

    /// <summary>
    /// Called when an entry is accessed to update policy state.
    /// </summary>
    /// <param name="entry">The accessed entry</param>
    void OnEntryAccessed(ICacheEntry<TKey, TValue> entry);

    /// <summary>
    /// Called when an entry is added to update policy state.
    /// </summary>
    /// <param name="entry">The added entry</param>
    void OnEntryAdded(ICacheEntry<TKey, TValue> entry);

    /// <summary>
    /// Called when an entry is removed to update policy state.
    /// </summary>
    /// <param name="entry">The removed entry</param>
    void OnEntryRemoved(ICacheEntry<TKey, TValue> entry);
}

/// <summary>
/// Least Recently Used (LRU) eviction policy.
/// </summary>
public class LruEvictionPolicy<TKey, TValue> : ICacheEvictionPolicy<TKey, TValue>
{
    public string Name => "LRU";

    public IEnumerable<ICacheEntry<TKey, TValue>> SelectForEviction(
        IEnumerable<ICacheEntry<TKey, TValue>> entries,
        int targetEvictionCount,
        long targetSizeReduction)
    {
        var sortedEntries = new List<ICacheEntry<TKey, TValue>>(entries);
        
        // Sort by last accessed time (oldest first) and priority
        sortedEntries.Sort((a, b) =>
        {
            // Never evict entries have highest priority
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority != CacheEntryPriority.NeverEvict)
                return 1;
            if (b.Priority == CacheEntryPriority.NeverEvict && a.Priority != CacheEntryPriority.NeverEvict)
                return -1;
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority == CacheEntryPriority.NeverEvict)
                return 0;

            // Compare by priority first
            var priorityComparison = a.Priority.CompareTo(b.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            // Then by last accessed time (oldest first)
            return a.LastAccessedAt.CompareTo(b.LastAccessedAt);
        });

        var selected = new List<ICacheEntry<TKey, TValue>>();
        long totalSizeReduction = 0;

        foreach (var entry in sortedEntries)
        {
            if (entry.Priority == CacheEntryPriority.NeverEvict)
                continue;

            selected.Add(entry);
            totalSizeReduction += entry.SizeInBytes;

            if (selected.Count >= targetEvictionCount || totalSizeReduction >= targetSizeReduction)
                break;
        }

        return selected;
    }

    public void OnEntryAccessed(ICacheEntry<TKey, TValue> entry)
    {
        // LRU doesn't need to track additional state
    }

    public void OnEntryAdded(ICacheEntry<TKey, TValue> entry)
    {
        // LRU doesn't need to track additional state
    }

    public void OnEntryRemoved(ICacheEntry<TKey, TValue> entry)
    {
        // LRU doesn't need to track additional state
    }
}

/// <summary>
/// Least Frequently Used (LFU) eviction policy.
/// </summary>
public class LfuEvictionPolicy<TKey, TValue> : ICacheEvictionPolicy<TKey, TValue>
{
    public string Name => "LFU";

    public IEnumerable<ICacheEntry<TKey, TValue>> SelectForEviction(
        IEnumerable<ICacheEntry<TKey, TValue>> entries,
        int targetEvictionCount,
        long targetSizeReduction)
    {
        var sortedEntries = new List<ICacheEntry<TKey, TValue>>(entries);
        
        // Sort by access count (least accessed first) and priority
        sortedEntries.Sort((a, b) =>
        {
            // Never evict entries have highest priority
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority != CacheEntryPriority.NeverEvict)
                return 1;
            if (b.Priority == CacheEntryPriority.NeverEvict && a.Priority != CacheEntryPriority.NeverEvict)
                return -1;
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority == CacheEntryPriority.NeverEvict)
                return 0;

            // Compare by priority first
            var priorityComparison = a.Priority.CompareTo(b.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            // Then by access count (least accessed first)
            return a.AccessCount.CompareTo(b.AccessCount);
        });

        var selected = new List<ICacheEntry<TKey, TValue>>();
        long totalSizeReduction = 0;

        foreach (var entry in sortedEntries)
        {
            if (entry.Priority == CacheEntryPriority.NeverEvict)
                continue;

            selected.Add(entry);
            totalSizeReduction += entry.SizeInBytes;

            if (selected.Count >= targetEvictionCount || totalSizeReduction >= targetSizeReduction)
                break;
        }

        return selected;
    }

    public void OnEntryAccessed(ICacheEntry<TKey, TValue> entry)
    {
        // LFU doesn't need to track additional state beyond what's in the entry
    }

    public void OnEntryAdded(ICacheEntry<TKey, TValue> entry)
    {
        // LFU doesn't need to track additional state
    }

    public void OnEntryRemoved(ICacheEntry<TKey, TValue> entry)
    {
        // LFU doesn't need to track additional state
    }
}

/// <summary>
/// Time-based eviction policy that evicts expired entries first.
/// </summary>
public class TimeBasedEvictionPolicy<TKey, TValue> : ICacheEvictionPolicy<TKey, TValue>
{
    public string Name => "TimeBased";

    public IEnumerable<ICacheEntry<TKey, TValue>> SelectForEviction(
        IEnumerable<ICacheEntry<TKey, TValue>> entries,
        int targetEvictionCount,
        long targetSizeReduction)
    {
        var sortedEntries = new List<ICacheEntry<TKey, TValue>>(entries);
        
        // Sort by expiration status and creation time
        sortedEntries.Sort((a, b) =>
        {
            // Never evict entries have highest priority
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority != CacheEntryPriority.NeverEvict)
                return 1;
            if (b.Priority == CacheEntryPriority.NeverEvict && a.Priority != CacheEntryPriority.NeverEvict)
                return -1;
            if (a.Priority == CacheEntryPriority.NeverEvict && b.Priority == CacheEntryPriority.NeverEvict)
                return 0;

            // Expired entries first
            if (a.IsExpired && !b.IsExpired)
                return -1;
            if (b.IsExpired && !a.IsExpired)
                return 1;

            // Compare by priority
            var priorityComparison = a.Priority.CompareTo(b.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            // Then by creation time (oldest first)
            return a.CreatedAt.CompareTo(b.CreatedAt);
        });

        var selected = new List<ICacheEntry<TKey, TValue>>();
        long totalSizeReduction = 0;

        foreach (var entry in sortedEntries)
        {
            if (entry.Priority == CacheEntryPriority.NeverEvict && !entry.IsExpired)
                continue;

            selected.Add(entry);
            totalSizeReduction += entry.SizeInBytes;

            if (selected.Count >= targetEvictionCount || totalSizeReduction >= targetSizeReduction)
                break;
        }

        return selected;
    }

    public void OnEntryAccessed(ICacheEntry<TKey, TValue> entry)
    {
        // Time-based doesn't need to track additional state
    }

    public void OnEntryAdded(ICacheEntry<TKey, TValue> entry)
    {
        // Time-based doesn't need to track additional state
    }

    public void OnEntryRemoved(ICacheEntry<TKey, TValue> entry)
    {
        // Time-based doesn't need to track additional state
    }
}
