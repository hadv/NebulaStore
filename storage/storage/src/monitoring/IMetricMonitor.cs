namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Base interface for metric monitors.
/// Replaces the Java MetricMonitor interface functionality.
/// </summary>
public interface IMetricMonitor
{
    /// <summary>
    /// Gets the name of this monitor for identification purposes.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Attribute to provide descriptions for monitoring properties and methods.
/// Replaces the Java @MonitorDescription annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
public class MonitorDescriptionAttribute : Attribute
{
    /// <summary>
    /// Gets the description of the monitored element.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the MonitorDescriptionAttribute class.
    /// </summary>
    /// <param name="description">The description of the monitored element</param>
    public MonitorDescriptionAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}
