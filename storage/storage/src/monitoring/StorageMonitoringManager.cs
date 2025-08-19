namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Default implementation of storage monitoring manager.
/// </summary>
public class StorageMonitoringManager : IStorageMonitoringManager
{
    private readonly List<IMetricMonitor> _allMonitors;
    private readonly List<IEntityCacheMonitor> _entityCacheMonitors;
    private readonly List<IStorageChannelHousekeepingMonitor> _housekeepingMonitors;

    /// <summary>
    /// Initializes a new instance of the StorageMonitoringManager class.
    /// </summary>
    /// <param name="storageManagerMonitor">The storage manager monitor</param>
    /// <param name="entityCacheSummaryMonitor">The entity cache summary monitor</param>
    /// <param name="objectRegistryMonitor">The object registry monitor</param>
    /// <param name="entityCacheMonitors">The entity cache monitors</param>
    /// <param name="housekeepingMonitors">The housekeeping monitors</param>
    public StorageMonitoringManager(
        IStorageManagerMonitor storageManagerMonitor,
        IEntityCacheSummaryMonitor entityCacheSummaryMonitor,
        IObjectRegistryMonitor objectRegistryMonitor,
        IEnumerable<IEntityCacheMonitor> entityCacheMonitors,
        IEnumerable<IStorageChannelHousekeepingMonitor> housekeepingMonitors)
    {
        StorageManagerMonitor = storageManagerMonitor ?? throw new System.ArgumentNullException(nameof(storageManagerMonitor));
        EntityCacheSummaryMonitor = entityCacheSummaryMonitor ?? throw new System.ArgumentNullException(nameof(entityCacheSummaryMonitor));
        ObjectRegistryMonitor = objectRegistryMonitor ?? throw new System.ArgumentNullException(nameof(objectRegistryMonitor));
        
        _entityCacheMonitors = new List<IEntityCacheMonitor>(entityCacheMonitors ?? throw new System.ArgumentNullException(nameof(entityCacheMonitors)));
        _housekeepingMonitors = new List<IStorageChannelHousekeepingMonitor>(housekeepingMonitors ?? throw new System.ArgumentNullException(nameof(housekeepingMonitors)));

        _allMonitors = new List<IMetricMonitor>();
        _allMonitors.Add(StorageManagerMonitor);
        _allMonitors.Add(EntityCacheSummaryMonitor);
        _allMonitors.Add(ObjectRegistryMonitor);
        _allMonitors.AddRange(_entityCacheMonitors);
        _allMonitors.AddRange(_housekeepingMonitors);
    }

    /// <summary>
    /// Gets the storage manager monitor.
    /// </summary>
    public IStorageManagerMonitor StorageManagerMonitor { get; }

    /// <summary>
    /// Gets the entity cache summary monitor.
    /// </summary>
    public IEntityCacheSummaryMonitor EntityCacheSummaryMonitor { get; }

    /// <summary>
    /// Gets the object registry monitor.
    /// </summary>
    public IObjectRegistryMonitor ObjectRegistryMonitor { get; }

    /// <summary>
    /// Gets the entity cache monitors for all channels.
    /// </summary>
    public IReadOnlyList<IEntityCacheMonitor> EntityCacheMonitors => _entityCacheMonitors;

    /// <summary>
    /// Gets the housekeeping monitors for all channels.
    /// </summary>
    public IReadOnlyList<IStorageChannelHousekeepingMonitor> HousekeepingMonitors => _housekeepingMonitors;

    /// <summary>
    /// Gets all metric monitors.
    /// </summary>
    public IReadOnlyList<IMetricMonitor> AllMonitors => _allMonitors;

    /// <summary>
    /// Gets a monitor by name.
    /// </summary>
    /// <param name="name">The monitor name</param>
    /// <returns>The monitor if found, null otherwise</returns>
    public IMetricMonitor? GetMonitor(string name)
    {
        return _allMonitors.Find(m => m.Name == name);
    }

    /// <summary>
    /// Gets monitors of a specific type.
    /// </summary>
    /// <typeparam name="T">The monitor type</typeparam>
    /// <returns>Monitors of the specified type</returns>
    public IEnumerable<T> GetMonitors<T>() where T : class, IMetricMonitor
    {
        foreach (var monitor in _allMonitors)
        {
            if (monitor is T typedMonitor)
            {
                yield return typedMonitor;
            }
        }
    }
}
