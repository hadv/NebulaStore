using System;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Configuration for multi-level cache behavior.
/// </summary>
public class MultiLevelCacheConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable automatic promotion from L2 to L1 cache.
    /// </summary>
    public bool EnableAutoPromotion { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for automatic promotion checks.
    /// </summary>
    public TimeSpan PromotionInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the access count threshold for promoting entries from L2 to L1.
    /// </summary>
    public long PromotionAccessThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of entries to promote in a single batch.
    /// </summary>
    public int MaxPromotionBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to use write-through caching (write to both L1 and L2 simultaneously).
    /// </summary>
    public bool WriteThrough { get; set; } = false;

    /// <summary>
    /// Gets or sets the priority threshold for writing to L2 cache.
    /// Entries with priority >= this threshold will be written to L2.
    /// </summary>
    public CacheEntryPriority L2WriteThreshold { get; set; } = CacheEntryPriority.Normal;

    /// <summary>
    /// Gets or sets whether to enable automatic demotion from L1 to L2 cache.
    /// </summary>
    public bool EnableAutoDemotion { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for automatic demotion checks.
    /// </summary>
    public TimeSpan DemotionInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the age threshold for demoting entries from L1 to L2.
    /// Entries older than this threshold may be demoted.
    /// </summary>
    public TimeSpan DemotionAgeThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of entries to demote in a single batch.
    /// </summary>
    public int MaxDemotionBatchSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets the L1 cache utilization threshold for triggering demotion.
    /// When L1 cache utilization exceeds this percentage, demotion will be triggered.
    /// </summary>
    public double L1UtilizationThreshold { get; set; } = 0.85; // 85%

    /// <summary>
    /// Gets or sets whether to enable cache coherence between levels.
    /// When enabled, updates to one level will be propagated to other levels.
    /// </summary>
    public bool EnableCacheCoherence { get; set; } = true;

    /// <summary>
    /// Gets or sets the coherence strategy.
    /// </summary>
    public CacheCoherenceStrategy CoherenceStrategy { get; set; } = CacheCoherenceStrategy.WriteThrough;

    /// <summary>
    /// Gets or sets whether to enable performance monitoring for the multi-level cache.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the performance monitoring interval.
    /// </summary>
    public TimeSpan PerformanceMonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return PromotionInterval > TimeSpan.Zero &&
               PromotionAccessThreshold > 0 &&
               MaxPromotionBatchSize > 0 &&
               DemotionInterval > TimeSpan.Zero &&
               DemotionAgeThreshold > TimeSpan.Zero &&
               MaxDemotionBatchSize > 0 &&
               L1UtilizationThreshold > 0 && L1UtilizationThreshold <= 1.0 &&
               PerformanceMonitoringInterval > TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public MultiLevelCacheConfiguration Clone()
    {
        return new MultiLevelCacheConfiguration
        {
            EnableAutoPromotion = EnableAutoPromotion,
            PromotionInterval = PromotionInterval,
            PromotionAccessThreshold = PromotionAccessThreshold,
            MaxPromotionBatchSize = MaxPromotionBatchSize,
            WriteThrough = WriteThrough,
            L2WriteThreshold = L2WriteThreshold,
            EnableAutoDemotion = EnableAutoDemotion,
            DemotionInterval = DemotionInterval,
            DemotionAgeThreshold = DemotionAgeThreshold,
            MaxDemotionBatchSize = MaxDemotionBatchSize,
            L1UtilizationThreshold = L1UtilizationThreshold,
            EnableCacheCoherence = EnableCacheCoherence,
            CoherenceStrategy = CoherenceStrategy,
            EnablePerformanceMonitoring = EnablePerformanceMonitoring,
            PerformanceMonitoringInterval = PerformanceMonitoringInterval
        };
    }

    /// <summary>
    /// Creates a builder for multi-level cache configuration.
    /// </summary>
    /// <returns>A new configuration builder</returns>
    public static MultiLevelCacheConfigurationBuilder Builder() => new();

    public override string ToString()
    {
        return $"MultiLevelCacheConfiguration[AutoPromotion={EnableAutoPromotion}, " +
               $"PromotionInterval={PromotionInterval}, WriteThrough={WriteThrough}, " +
               $"AutoDemotion={EnableAutoDemotion}, Coherence={EnableCacheCoherence}]";
    }
}

/// <summary>
/// Builder for multi-level cache configuration.
/// </summary>
public class MultiLevelCacheConfigurationBuilder
{
    private readonly MultiLevelCacheConfiguration _config = new();

    public MultiLevelCacheConfigurationBuilder EnableAutoPromotion(bool enable = true)
    {
        _config.EnableAutoPromotion = enable;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetPromotionInterval(TimeSpan interval)
    {
        _config.PromotionInterval = interval > TimeSpan.Zero ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetPromotionThreshold(long accessThreshold)
    {
        _config.PromotionAccessThreshold = accessThreshold > 0 ? accessThreshold : throw new ArgumentOutOfRangeException(nameof(accessThreshold));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetPromotionBatchSize(int batchSize)
    {
        _config.MaxPromotionBatchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder EnableWriteThrough(bool enable = true)
    {
        _config.WriteThrough = enable;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetL2WriteThreshold(CacheEntryPriority threshold)
    {
        _config.L2WriteThreshold = threshold;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder EnableAutoDemotion(bool enable = true)
    {
        _config.EnableAutoDemotion = enable;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetDemotionInterval(TimeSpan interval)
    {
        _config.DemotionInterval = interval > TimeSpan.Zero ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetDemotionAgeThreshold(TimeSpan threshold)
    {
        _config.DemotionAgeThreshold = threshold > TimeSpan.Zero ? threshold : throw new ArgumentOutOfRangeException(nameof(threshold));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetDemotionBatchSize(int batchSize)
    {
        _config.MaxDemotionBatchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));
        return this;
    }

    public MultiLevelCacheConfigurationBuilder SetL1UtilizationThreshold(double threshold)
    {
        if (threshold <= 0 || threshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        _config.L1UtilizationThreshold = threshold;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder EnableCacheCoherence(bool enable = true, CacheCoherenceStrategy strategy = CacheCoherenceStrategy.WriteThrough)
    {
        _config.EnableCacheCoherence = enable;
        _config.CoherenceStrategy = strategy;
        return this;
    }

    public MultiLevelCacheConfigurationBuilder EnablePerformanceMonitoring(bool enable = true, TimeSpan? interval = null)
    {
        _config.EnablePerformanceMonitoring = enable;
        if (interval.HasValue)
            _config.PerformanceMonitoringInterval = interval.Value;
        return this;
    }

    public MultiLevelCacheConfiguration Build()
    {
        if (!_config.IsValid())
            throw new InvalidOperationException("Invalid multi-level cache configuration");
        
        return _config.Clone();
    }
}

/// <summary>
/// Enumeration of cache coherence strategies.
/// </summary>
public enum CacheCoherenceStrategy
{
    /// <summary>
    /// Write-through: Updates are written to all cache levels simultaneously.
    /// </summary>
    WriteThrough,

    /// <summary>
    /// Write-back: Updates are written to the current level and propagated later.
    /// </summary>
    WriteBack,

    /// <summary>
    /// Invalidate: Updates invalidate entries in other levels.
    /// </summary>
    Invalidate,

    /// <summary>
    /// No coherence: Each level operates independently.
    /// </summary>
    None
}
