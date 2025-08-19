namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface that provides a summary of monitoring and metrics for all storage entity cache instances.
/// Replaces the Java EntityCacheSummaryMonitorMBean interface.
/// </summary>
[MonitorDescription("Provides a summary of all storage channels entity caches.")]
public interface IEntityCacheSummaryMonitor : IMetricMonitor
{
    /// <summary>
    /// Gets the aggregated used cache size from all channel entity caches in bytes.
    /// </summary>
    [MonitorDescription("The total size of all channel entity caches in bytes.")]
    long UsedCacheSize { get; }

    /// <summary>
    /// Gets the number of entries aggregated from all channel entity caches.
    /// </summary>
    [MonitorDescription("The number of entries aggregated from all channel entity caches.")]
    long EntityCount { get; }
}
