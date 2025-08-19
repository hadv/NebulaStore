namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface that provides monitoring and metrics for a storage entity cache instance.
/// Replaces the Java EntityCacheMonitorMBean interface.
/// </summary>
[MonitorDescription("Provides monitoring and metrics data of a StorageEntityCache instance.")]
public interface IEntityCacheMonitor : IMetricMonitor
{
    /// <summary>
    /// Gets the channel index.
    /// </summary>
    [MonitorDescription("The channel index")]
    int ChannelIndex { get; }

    /// <summary>
    /// Gets the timestamp of the last start of a cache sweep.
    /// </summary>
    [MonitorDescription("Timestamp of the last start of a cache sweep in ms.")]
    long LastSweepStart { get; }

    /// <summary>
    /// Gets the timestamp of the last end of a cache sweep.
    /// </summary>
    [MonitorDescription("Timestamp of the last end of a cache sweep in ms.")]
    long LastSweepEnd { get; }

    /// <summary>
    /// Gets the number of entries in the channel's entity cache.
    /// </summary>
    [MonitorDescription("The number of entries in the channels entity cache.")]
    long EntityCount { get; }

    /// <summary>
    /// Gets the used cache size in bytes.
    /// </summary>
    [MonitorDescription("The used cache size in bytes.")]
    long UsedCacheSize { get; }
}
