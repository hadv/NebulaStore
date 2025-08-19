namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface that provides monitoring and metrics of storage channel housekeeping operations.
/// Replaces the Java StorageChannelHousekeepingMonitorMBean interface.
/// </summary>
[MonitorDescription("Provides monitoring and metrics data of a storage channel.")]
public interface IStorageChannelHousekeepingMonitor : IMetricMonitor
{
    /// <summary>
    /// Gets the result of the last housekeeping cache check cycle.
    /// </summary>
    [MonitorDescription("Result of the last housekeeping cache check.")]
    bool EntityCacheCheckResult { get; }

    /// <summary>
    /// Gets the starting time of the last housekeeping cache check cycle.
    /// </summary>
    [MonitorDescription("Starting time of the last housekeeping cache check in ms since 1970.")]
    long EntityCacheCheckStartTime { get; }

    /// <summary>
    /// Gets the duration of the last housekeeping cache check cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Duration of the last housekeeping cache check cycle in ns")]
    long EntityCacheCheckDuration { get; }

    /// <summary>
    /// Gets the time budget of the last housekeeping cache check cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Time budget of the last housekeeping cache check cycle in ns.")]
    long EntityCacheCheckBudget { get; }

    /// <summary>
    /// Gets the result of the last housekeeping garbage collection cycle.
    /// </summary>
    [MonitorDescription("Result of the last housekeeping garbage collection cycle.")]
    bool GarbageCollectionResult { get; }

    /// <summary>
    /// Gets the starting time of the last housekeeping garbage collection cycle.
    /// </summary>
    [MonitorDescription("Starting time of the last housekeeping garbage collection cycle in ms since 1970.")]
    long GarbageCollectionStartTime { get; }

    /// <summary>
    /// Gets the duration of the last housekeeping garbage collection cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Duration of the last housekeeping garbage collection cycle in ns")]
    long GarbageCollectionDuration { get; }

    /// <summary>
    /// Gets the time budget of the last housekeeping garbage collection cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Time budget of the last housekeeping garbage collection cycle in ns.")]
    long GarbageCollectionBudget { get; }

    /// <summary>
    /// Gets the result of the last housekeeping file cleanup cycle.
    /// </summary>
    [MonitorDescription("Result of the last housekeeping file cleanup cycle.")]
    bool FileCleanupCheckResult { get; }

    /// <summary>
    /// Gets the starting time of the last housekeeping file cleanup cycle.
    /// </summary>
    [MonitorDescription("Starting time of the last housekeeping file cleanup cycle in ms since 1970.")]
    long FileCleanupCheckStartTime { get; }

    /// <summary>
    /// Gets the duration of the last housekeeping file cleanup cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Duration of the last housekeeping file cleanup cycle in ns")]
    long FileCleanupCheckDuration { get; }

    /// <summary>
    /// Gets the time budget of the last housekeeping file cleanup cycle in nanoseconds.
    /// </summary>
    [MonitorDescription("Time budget of the last housekeeping garbage collection cycle in ns.")]
    long FileCleanupCheckBudget { get; }
}
