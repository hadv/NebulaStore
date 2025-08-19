namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Monitors entity cache summary metrics and provides aggregated monitoring data.
/// Replaces the Java EntityCacheSummaryMonitor class.
/// </summary>
public class EntityCacheSummaryMonitor : IEntityCacheSummaryMonitor
{
    private readonly IReadOnlyList<EntityCacheMonitor> _cacheMonitors;

    /// <summary>
    /// Initializes a new instance of the EntityCacheSummaryMonitor class.
    /// </summary>
    /// <param name="cacheMonitors">The entity cache monitors to aggregate</param>
    public EntityCacheSummaryMonitor(IEnumerable<EntityCacheMonitor> cacheMonitors)
    {
        _cacheMonitors = (cacheMonitors ?? throw new ArgumentNullException(nameof(cacheMonitors))).ToList();
    }

    /// <summary>
    /// Initializes a new instance of the EntityCacheSummaryMonitor class.
    /// </summary>
    /// <param name="cacheMonitors">The entity cache monitors to aggregate</param>
    public EntityCacheSummaryMonitor(params EntityCacheMonitor[] cacheMonitors)
        : this((IEnumerable<EntityCacheMonitor>)cacheMonitors)
    {
    }

    /// <summary>
    /// Gets the name of this monitor.
    /// </summary>
    public string Name => "name=EntityCacheSummary";

    /// <summary>
    /// Gets the aggregated used cache size from all channel entity caches in bytes.
    /// </summary>
    public long UsedCacheSize
    {
        get
        {
            lock (_cacheMonitors)
            {
                return _cacheMonitors.Sum(monitor => monitor.UsedCacheSize);
            }
        }
    }

    /// <summary>
    /// Gets the number of entries aggregated from all channel entity caches.
    /// </summary>
    public long EntityCount
    {
        get
        {
            lock (_cacheMonitors)
            {
                return _cacheMonitors.Sum(monitor => monitor.EntityCount);
            }
        }
    }
}
