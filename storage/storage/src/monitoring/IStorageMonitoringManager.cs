using System.Collections.Generic;

namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface for managing storage monitoring and metrics.
/// Provides access to all monitoring components.
/// </summary>
public interface IStorageMonitoringManager
{
    /// <summary>
    /// Gets the storage manager monitor.
    /// </summary>
    IStorageManagerMonitor StorageManagerMonitor { get; }

    /// <summary>
    /// Gets the entity cache summary monitor.
    /// </summary>
    IEntityCacheSummaryMonitor EntityCacheSummaryMonitor { get; }

    /// <summary>
    /// Gets the object registry monitor.
    /// </summary>
    IObjectRegistryMonitor ObjectRegistryMonitor { get; }

    /// <summary>
    /// Gets the entity cache monitors for all channels.
    /// </summary>
    IReadOnlyList<IEntityCacheMonitor> EntityCacheMonitors { get; }

    /// <summary>
    /// Gets the housekeeping monitors for all channels.
    /// </summary>
    IReadOnlyList<IStorageChannelHousekeepingMonitor> HousekeepingMonitors { get; }

    /// <summary>
    /// Gets all metric monitors.
    /// </summary>
    IReadOnlyList<IMetricMonitor> AllMonitors { get; }

    /// <summary>
    /// Gets a monitor by name.
    /// </summary>
    /// <param name="name">The monitor name</param>
    /// <returns>The monitor if found, null otherwise</returns>
    IMetricMonitor? GetMonitor(string name);

    /// <summary>
    /// Gets monitors of a specific type.
    /// </summary>
    /// <typeparam name="T">The monitor type</typeparam>
    /// <returns>Monitors of the specified type</returns>
    IEnumerable<T> GetMonitors<T>() where T : class, IMetricMonitor;
}
