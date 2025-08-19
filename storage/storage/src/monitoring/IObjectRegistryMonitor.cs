namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface that provides monitoring and metrics for a persistence object registry instance.
/// Replaces the Java ObjectRegistryMonitorMBean interface.
/// </summary>
[MonitorDescription("Provides object registry related data.")]
public interface IObjectRegistryMonitor : IMetricMonitor
{
    /// <summary>
    /// Gets the number of registered objects (size of object registry).
    /// </summary>
    [MonitorDescription("The number of currently registered objects.")]
    long Size { get; }

    /// <summary>
    /// Gets the reserved size (number of objects) of the object registry.
    /// </summary>
    [MonitorDescription("The reserved size(number of objects) of the object registry.")]
    long Capacity { get; }
}
