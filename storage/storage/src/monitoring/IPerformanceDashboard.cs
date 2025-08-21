using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Monitoring;

/// <summary>
/// Interface for real-time performance dashboards with alerting capabilities.
/// </summary>
public interface IPerformanceDashboard : IDisposable
{
    /// <summary>
    /// Gets the dashboard name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the dashboard is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the current dashboard state.
    /// </summary>
    DashboardState State { get; }

    /// <summary>
    /// Starts the dashboard monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the dashboard monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current dashboard data.
    /// </summary>
    /// <returns>Dashboard data</returns>
    DashboardData GetCurrentData();

    /// <summary>
    /// Gets historical dashboard data.
    /// </summary>
    /// <param name="period">Time period</param>
    /// <returns>Historical dashboard data</returns>
    IEnumerable<DashboardDataPoint> GetHistoricalData(TimeSpan period);

    /// <summary>
    /// Adds a widget to the dashboard.
    /// </summary>
    /// <param name="widget">Widget to add</param>
    void AddWidget(IDashboardWidget widget);

    /// <summary>
    /// Removes a widget from the dashboard.
    /// </summary>
    /// <param name="widgetId">Widget ID to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveWidget(string widgetId);

    /// <summary>
    /// Gets all widgets in the dashboard.
    /// </summary>
    /// <returns>Collection of widgets</returns>
    IEnumerable<IDashboardWidget> GetWidgets();

    /// <summary>
    /// Adds an alert rule to the dashboard.
    /// </summary>
    /// <param name="rule">Alert rule to add</param>
    void AddAlertRule(IAlertRule rule);

    /// <summary>
    /// Removes an alert rule from the dashboard.
    /// </summary>
    /// <param name="ruleId">Alert rule ID to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveAlertRule(string ruleId);

    /// <summary>
    /// Gets all active alerts.
    /// </summary>
    /// <returns>Collection of active alerts</returns>
    IEnumerable<Alert> GetActiveAlerts();

    /// <summary>
    /// Event fired when an alert is triggered.
    /// </summary>
    event EventHandler<AlertTriggeredEventArgs> AlertTriggered;

    /// <summary>
    /// Event fired when dashboard data is updated.
    /// </summary>
    event EventHandler<DashboardDataUpdatedEventArgs> DataUpdated;
}

/// <summary>
/// Interface for dashboard widgets.
/// </summary>
public interface IDashboardWidget
{
    /// <summary>
    /// Gets the widget ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the widget title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the widget type.
    /// </summary>
    WidgetType Type { get; }

    /// <summary>
    /// Gets the widget configuration.
    /// </summary>
    WidgetConfiguration Configuration { get; }

    /// <summary>
    /// Updates the widget with new data.
    /// </summary>
    /// <param name="data">New data</param>
    void UpdateData(object data);

    /// <summary>
    /// Gets the current widget data.
    /// </summary>
    /// <returns>Current widget data</returns>
    WidgetData GetData();

    /// <summary>
    /// Refreshes the widget data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for alert rules.
/// </summary>
public interface IAlertRule
{
    /// <summary>
    /// Gets the rule ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the rule name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the metric name to monitor.
    /// </summary>
    string MetricName { get; }

    /// <summary>
    /// Gets the alert condition.
    /// </summary>
    AlertCondition Condition { get; }

    /// <summary>
    /// Gets the alert severity.
    /// </summary>
    AlertSeverity Severity { get; }

    /// <summary>
    /// Gets whether the rule is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the rule against the provided metric value.
    /// </summary>
    /// <param name="value">Metric value to evaluate</param>
    /// <returns>True if the rule is triggered, false otherwise</returns>
    bool Evaluate(double value);

    /// <summary>
    /// Gets the alert message for the current condition.
    /// </summary>
    /// <param name="value">Current metric value</param>
    /// <returns>Alert message</returns>
    string GetAlertMessage(double value);
}

/// <summary>
/// Interface for bottleneck detection.
/// </summary>
public interface IBottleneckDetector : IDisposable
{
    /// <summary>
    /// Gets the detector name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether bottleneck detection is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Analyzes current metrics for bottlenecks.
    /// </summary>
    /// <param name="metrics">Current metrics</param>
    /// <returns>Detected bottlenecks</returns>
    IEnumerable<Bottleneck> AnalyzeBottlenecks(IEnumerable<MetricValue> metrics);

    /// <summary>
    /// Gets historical bottleneck analysis.
    /// </summary>
    /// <param name="period">Time period to analyze</param>
    /// <returns>Historical bottlenecks</returns>
    IEnumerable<Bottleneck> GetHistoricalBottlenecks(TimeSpan period);

    /// <summary>
    /// Adds a bottleneck detection rule.
    /// </summary>
    /// <param name="rule">Detection rule</param>
    void AddDetectionRule(IBottleneckDetectionRule rule);

    /// <summary>
    /// Removes a bottleneck detection rule.
    /// </summary>
    /// <param name="ruleId">Rule ID</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveDetectionRule(string ruleId);

    /// <summary>
    /// Event fired when a bottleneck is detected.
    /// </summary>
    event EventHandler<BottleneckDetectedEventArgs> BottleneckDetected;
}

/// <summary>
/// Dashboard state enumeration.
/// </summary>
public enum DashboardState
{
    /// <summary>
    /// Dashboard is stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Dashboard is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Dashboard is running.
    /// </summary>
    Running,

    /// <summary>
    /// Dashboard is stopping.
    /// </summary>
    Stopping,

    /// <summary>
    /// Dashboard has encountered an error.
    /// </summary>
    Error
}

/// <summary>
/// Widget type enumeration.
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// Line chart widget.
    /// </summary>
    LineChart,

    /// <summary>
    /// Bar chart widget.
    /// </summary>
    BarChart,

    /// <summary>
    /// Gauge widget.
    /// </summary>
    Gauge,

    /// <summary>
    /// Counter widget.
    /// </summary>
    Counter,

    /// <summary>
    /// Table widget.
    /// </summary>
    Table,

    /// <summary>
    /// Text widget.
    /// </summary>
    Text
}

/// <summary>
/// Alert severity enumeration.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert.
    /// </summary>
    Info,

    /// <summary>
    /// Warning alert.
    /// </summary>
    Warning,

    /// <summary>
    /// Error alert.
    /// </summary>
    Error,

    /// <summary>
    /// Critical alert.
    /// </summary>
    Critical
}

/// <summary>
/// Alert condition enumeration.
/// </summary>
public enum AlertCondition
{
    /// <summary>
    /// Value is greater than threshold.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Value is less than threshold.
    /// </summary>
    LessThan,

    /// <summary>
    /// Value equals threshold.
    /// </summary>
    Equals,

    /// <summary>
    /// Value is between two thresholds.
    /// </summary>
    Between,

    /// <summary>
    /// Value is outside two thresholds.
    /// </summary>
    Outside
}

/// <summary>
/// Dashboard data container.
/// </summary>
public class DashboardData
{
    public DashboardData(
        IEnumerable<WidgetData> widgets,
        IEnumerable<Alert> activeAlerts,
        IEnumerable<Bottleneck> bottlenecks,
        DateTime timestamp)
    {
        Widgets = widgets?.ToList() ?? new List<WidgetData>();
        ActiveAlerts = activeAlerts?.ToList() ?? new List<Alert>();
        Bottlenecks = bottlenecks?.ToList() ?? new List<Bottleneck>();
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the widget data.
    /// </summary>
    public IReadOnlyList<WidgetData> Widgets { get; }

    /// <summary>
    /// Gets the active alerts.
    /// </summary>
    public IReadOnlyList<Alert> ActiveAlerts { get; }

    /// <summary>
    /// Gets the detected bottlenecks.
    /// </summary>
    public IReadOnlyList<Bottleneck> Bottlenecks { get; }

    /// <summary>
    /// Gets the timestamp of this data.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Dashboard Data [{Timestamp:HH:mm:ss}]: " +
               $"Widgets={Widgets.Count}, Alerts={ActiveAlerts.Count}, Bottlenecks={Bottlenecks.Count}";
    }
}

/// <summary>
/// Dashboard data point for historical data.
/// </summary>
public class DashboardDataPoint
{
    public DashboardDataPoint(string metricName, double value, DateTime timestamp)
    {
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        Value = value;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string MetricName { get; }

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"{MetricName}: {Value} @ {Timestamp:HH:mm:ss}";
    }
}

/// <summary>
/// Widget data container.
/// </summary>
public class WidgetData
{
    public WidgetData(string widgetId, string title, WidgetType type, object data, DateTime timestamp)
    {
        WidgetId = widgetId ?? throw new ArgumentNullException(nameof(widgetId));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Type = type;
        Data = data;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the widget ID.
    /// </summary>
    public string WidgetId { get; }

    /// <summary>
    /// Gets the widget title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the widget type.
    /// </summary>
    public WidgetType Type { get; }

    /// <summary>
    /// Gets the widget data.
    /// </summary>
    public object Data { get; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Widget '{Title}' ({Type}): {Data} @ {Timestamp:HH:mm:ss}";
    }
}

/// <summary>
/// Widget configuration.
/// </summary>
public class WidgetConfiguration
{
    public WidgetConfiguration(
        TimeSpan refreshInterval,
        IDictionary<string, object>? properties = null)
    {
        RefreshInterval = refreshInterval;
        Properties = properties ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets the widget refresh interval.
    /// </summary>
    public TimeSpan RefreshInterval { get; }

    /// <summary>
    /// Gets the widget properties.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; }

    public override string ToString()
    {
        return $"WidgetConfiguration[RefreshInterval={RefreshInterval}, Properties={Properties.Count}]";
    }
}
