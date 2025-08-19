namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Represents a persistence object registry for monitoring purposes.
/// This is a placeholder interface that will be implemented when the actual object registry system is developed.
/// </summary>
public interface IPersistenceObjectRegistry
{
    /// <summary>
    /// Gets the capacity of the object registry.
    /// </summary>
    long Capacity { get; }

    /// <summary>
    /// Gets the current size (number of registered objects) of the object registry.
    /// </summary>
    long Size { get; }
}

/// <summary>
/// Monitors object registry metrics and provides monitoring data.
/// Replaces the Java ObjectRegistryMonitor class.
/// </summary>
public class ObjectRegistryMonitor : IObjectRegistryMonitor
{
    private readonly WeakReference<IPersistenceObjectRegistry> _persistenceObjectRegistry;

    /// <summary>
    /// Initializes a new instance of the ObjectRegistryMonitor class.
    /// </summary>
    /// <param name="persistenceObjectRegistry">The persistence object registry to monitor</param>
    public ObjectRegistryMonitor(IPersistenceObjectRegistry persistenceObjectRegistry)
    {
        _persistenceObjectRegistry = new WeakReference<IPersistenceObjectRegistry>(
            persistenceObjectRegistry ?? throw new ArgumentNullException(nameof(persistenceObjectRegistry)));
    }

    /// <summary>
    /// Gets the name of this monitor.
    /// </summary>
    public string Name => "name=ObjectRegistry";

    /// <summary>
    /// Gets the number of registered objects (size of object registry).
    /// </summary>
    public long Size
    {
        get
        {
            if (_persistenceObjectRegistry.TryGetTarget(out var registry))
            {
                return registry.Size;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the reserved size (number of objects) of the object registry.
    /// </summary>
    public long Capacity
    {
        get
        {
            if (_persistenceObjectRegistry.TryGetTarget(out var registry))
            {
                return registry.Capacity;
            }
            return 0;
        }
    }
}
