using System;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Configuration for cache instances.
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Gets or sets the cache name.
    /// </summary>
    public string Name { get; set; } = "DefaultCache";

    /// <summary>
    /// Gets or sets the maximum number of entries in the cache.
    /// </summary>
    public long MaxEntryCount { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the maximum size of the cache in bytes.
    /// </summary>
    public long MaxSizeInBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets the eviction policy type.
    /// </summary>
    public CacheEvictionPolicyType EvictionPolicy { get; set; } = CacheEvictionPolicyType.LRU;

    /// <summary>
    /// Gets or sets the cleanup interval for expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default time-to-live for cache entries.
    /// </summary>
    public TimeSpan? DefaultTimeToLive { get; set; }

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable performance monitoring.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the eviction threshold as a percentage of max capacity.
    /// When this threshold is reached, eviction will be triggered.
    /// </summary>
    public double EvictionThreshold { get; set; } = 0.9; // 90%

    /// <summary>
    /// Gets or sets the target capacity after eviction as a percentage of max capacity.
    /// </summary>
    public double EvictionTarget { get; set; } = 0.8; // 80%

    /// <summary>
    /// Gets or sets whether to enable cache warming on startup.
    /// </summary>
    public bool EnableCacheWarming { get; set; } = false;

    /// <summary>
    /// Gets or sets the cache warming strategy.
    /// </summary>
    public CacheWarmingStrategy WarmingStrategy { get; set; } = CacheWarmingStrategy.None;

    /// <summary>
    /// Gets or sets the maximum time to spend on cache warming.
    /// </summary>
    public TimeSpan MaxWarmingTime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Validates the cache configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               MaxEntryCount > 0 &&
               MaxSizeInBytes > 0 &&
               CleanupInterval > TimeSpan.Zero &&
               EvictionThreshold > 0 && EvictionThreshold <= 1.0 &&
               EvictionTarget > 0 && EvictionTarget <= 1.0 &&
               EvictionTarget < EvictionThreshold &&
               MaxWarmingTime > TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public CacheConfiguration Clone()
    {
        return new CacheConfiguration
        {
            Name = Name,
            MaxEntryCount = MaxEntryCount,
            MaxSizeInBytes = MaxSizeInBytes,
            EvictionPolicy = EvictionPolicy,
            CleanupInterval = CleanupInterval,
            DefaultTimeToLive = DefaultTimeToLive,
            EnableStatistics = EnableStatistics,
            EnablePerformanceMonitoring = EnablePerformanceMonitoring,
            EvictionThreshold = EvictionThreshold,
            EvictionTarget = EvictionTarget,
            EnableCacheWarming = EnableCacheWarming,
            WarmingStrategy = WarmingStrategy,
            MaxWarmingTime = MaxWarmingTime
        };
    }

    /// <summary>
    /// Creates a builder for cache configuration.
    /// </summary>
    /// <returns>A new configuration builder</returns>
    public static CacheConfigurationBuilder Builder() => new();

    public override string ToString()
    {
        return $"CacheConfiguration[Name={Name}, MaxEntries={MaxEntryCount:N0}, " +
               $"MaxSize={MaxSizeInBytes:N0} bytes, Policy={EvictionPolicy}, " +
               $"CleanupInterval={CleanupInterval}, TTL={DefaultTimeToLive}, " +
               $"Stats={EnableStatistics}, Monitoring={EnablePerformanceMonitoring}]";
    }
}

/// <summary>
/// Builder for cache configuration.
/// </summary>
public class CacheConfigurationBuilder
{
    private readonly CacheConfiguration _config = new();

    public CacheConfigurationBuilder SetName(string name)
    {
        _config.Name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    public CacheConfigurationBuilder SetMaxEntryCount(long maxEntryCount)
    {
        _config.MaxEntryCount = maxEntryCount > 0 ? maxEntryCount : throw new ArgumentOutOfRangeException(nameof(maxEntryCount));
        return this;
    }

    public CacheConfigurationBuilder SetMaxSizeInBytes(long maxSizeInBytes)
    {
        _config.MaxSizeInBytes = maxSizeInBytes > 0 ? maxSizeInBytes : throw new ArgumentOutOfRangeException(nameof(maxSizeInBytes));
        return this;
    }

    public CacheConfigurationBuilder SetMaxSizeInMB(long maxSizeInMB)
    {
        return SetMaxSizeInBytes(maxSizeInMB * 1024 * 1024);
    }

    public CacheConfigurationBuilder SetEvictionPolicy(CacheEvictionPolicyType policy)
    {
        _config.EvictionPolicy = policy;
        return this;
    }

    public CacheConfigurationBuilder SetCleanupInterval(TimeSpan interval)
    {
        _config.CleanupInterval = interval > TimeSpan.Zero ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
        return this;
    }

    public CacheConfigurationBuilder SetDefaultTimeToLive(TimeSpan? timeToLive)
    {
        _config.DefaultTimeToLive = timeToLive;
        return this;
    }

    public CacheConfigurationBuilder EnableStatistics(bool enable = true)
    {
        _config.EnableStatistics = enable;
        return this;
    }

    public CacheConfigurationBuilder EnablePerformanceMonitoring(bool enable = true)
    {
        _config.EnablePerformanceMonitoring = enable;
        return this;
    }

    public CacheConfigurationBuilder SetEvictionThresholds(double threshold, double target)
    {
        if (threshold <= 0 || threshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (target <= 0 || target > 1.0)
            throw new ArgumentOutOfRangeException(nameof(target));
        if (target >= threshold)
            throw new ArgumentException("Target must be less than threshold");

        _config.EvictionThreshold = threshold;
        _config.EvictionTarget = target;
        return this;
    }

    public CacheConfigurationBuilder EnableCacheWarming(CacheWarmingStrategy strategy = CacheWarmingStrategy.MostAccessed, TimeSpan? maxWarmingTime = null)
    {
        _config.EnableCacheWarming = true;
        _config.WarmingStrategy = strategy;
        if (maxWarmingTime.HasValue)
            _config.MaxWarmingTime = maxWarmingTime.Value;
        return this;
    }

    public CacheConfiguration Build()
    {
        if (!_config.IsValid())
            throw new InvalidOperationException("Invalid cache configuration");
        
        return _config.Clone();
    }
}

/// <summary>
/// Enumeration of cache eviction policy types.
/// </summary>
public enum CacheEvictionPolicyType
{
    /// <summary>
    /// Least Recently Used eviction policy.
    /// </summary>
    LRU,

    /// <summary>
    /// Least Frequently Used eviction policy.
    /// </summary>
    LFU,

    /// <summary>
    /// Time-based eviction policy (expires oldest entries first).
    /// </summary>
    TimeBased,

    /// <summary>
    /// Custom eviction policy.
    /// </summary>
    Custom
}

/// <summary>
/// Enumeration of cache warming strategies.
/// </summary>
public enum CacheWarmingStrategy
{
    /// <summary>
    /// No cache warming.
    /// </summary>
    None,

    /// <summary>
    /// Warm cache with most frequently accessed items.
    /// </summary>
    MostAccessed,

    /// <summary>
    /// Warm cache with most recently accessed items.
    /// </summary>
    MostRecent,

    /// <summary>
    /// Warm cache with items based on custom logic.
    /// </summary>
    Custom
}
