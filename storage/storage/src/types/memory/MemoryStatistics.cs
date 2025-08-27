using System;

namespace NebulaStore.Storage.Embedded.Types.Memory;

/// <summary>
/// Contains memory management statistics following Eclipse Store patterns.
/// </summary>
public class MemoryStatistics
{
    /// <summary>
    /// Gets or sets the current cache size in bytes.
    /// </summary>
    public long CurrentCacheSize { get; set; }

    /// <summary>
    /// Gets or sets the cache threshold in bytes.
    /// </summary>
    public long CacheThreshold { get; set; }

    /// <summary>
    /// Gets or sets the number of cache entries.
    /// </summary>
    public int CacheEntryCount { get; set; }

    /// <summary>
    /// Gets or sets the cache utilization percentage.
    /// </summary>
    public double CacheUtilization { get; set; }

    /// <summary>
    /// Gets or sets the total number of allocations.
    /// </summary>
    public long TotalAllocations { get; set; }

    /// <summary>
    /// Gets or sets the total number of evictions.
    /// </summary>
    public long TotalEvictions { get; set; }

    /// <summary>
    /// Gets or sets the cache timeout.
    /// </summary>
    public TimeSpan CacheTimeout { get; set; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double CacheHitRatio => TotalAllocations > 0 ? 1.0 - ((double)TotalEvictions / TotalAllocations) : 0.0;

    /// <summary>
    /// Gets the average cache entry size.
    /// </summary>
    public double AverageCacheEntrySize => CacheEntryCount > 0 ? (double)CurrentCacheSize / CacheEntryCount : 0.0;

    /// <summary>
    /// Gets a value indicating whether the cache is under pressure.
    /// </summary>
    public bool IsUnderPressure => CacheUtilization > 90.0;

    /// <summary>
    /// Gets a summary of the memory statistics.
    /// </summary>
    public string Summary => $"Cache: {CurrentCacheSize:N0} bytes ({CacheUtilization:F1}%), " +
                           $"Entries: {CacheEntryCount:N0}, " +
                           $"Hit Ratio: {CacheHitRatio:P1}, " +
                           $"Allocations: {TotalAllocations:N0}, " +
                           $"Evictions: {TotalEvictions:N0}";

    /// <summary>
    /// Returns a string representation of the memory statistics.
    /// </summary>
    /// <returns>A string representation of the memory statistics.</returns>
    public override string ToString()
    {
        return $"MemoryStatistics: {Summary}";
    }
}