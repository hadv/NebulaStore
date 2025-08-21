using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Monitoring;

/// <summary>
/// Represents an alert in the system.
/// </summary>
public class Alert
{
    public Alert(
        string id,
        string name,
        string message,
        AlertSeverity severity,
        string metricName,
        double currentValue,
        double threshold,
        DateTime triggeredAt,
        string? source = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Severity = severity;
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        CurrentValue = currentValue;
        Threshold = threshold;
        TriggeredAt = triggeredAt;
        Source = source;
    }

    /// <summary>
    /// Gets the alert ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the alert name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the alert message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the alert severity.
    /// </summary>
    public AlertSeverity Severity { get; }

    /// <summary>
    /// Gets the metric name that triggered the alert.
    /// </summary>
    public string MetricName { get; }

    /// <summary>
    /// Gets the current metric value.
    /// </summary>
    public double CurrentValue { get; }

    /// <summary>
    /// Gets the threshold value.
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets when the alert was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; }

    /// <summary>
    /// Gets the alert source.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets how long the alert has been active.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - TriggeredAt;

    public override string ToString()
    {
        return $"[{Severity}] {Name}: {Message} (Value: {CurrentValue:F2}, Threshold: {Threshold:F2}) - {Duration.TotalMinutes:F1}m ago";
    }
}

/// <summary>
/// Implementation of an alert rule.
/// </summary>
public class AlertRule : IAlertRule
{
    public AlertRule(
        string id,
        string name,
        string metricName,
        AlertCondition condition,
        double threshold,
        AlertSeverity severity,
        bool isEnabled = true,
        double? secondaryThreshold = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        Condition = condition;
        Threshold = threshold;
        Severity = severity;
        IsEnabled = isEnabled;
        SecondaryThreshold = secondaryThreshold;
    }

    public string Id { get; }
    public string Name { get; }
    public string MetricName { get; }
    public AlertCondition Condition { get; }
    public AlertSeverity Severity { get; }
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the primary threshold value.
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets the secondary threshold value (for Between/Outside conditions).
    /// </summary>
    public double? SecondaryThreshold { get; }

    public bool Evaluate(double value)
    {
        if (!IsEnabled) return false;

        return Condition switch
        {
            AlertCondition.GreaterThan => value > Threshold,
            AlertCondition.LessThan => value < Threshold,
            AlertCondition.Equals => Math.Abs(value - Threshold) < 0.001, // Small epsilon for floating point comparison
            AlertCondition.Between => SecondaryThreshold.HasValue && value >= Threshold && value <= SecondaryThreshold.Value,
            AlertCondition.Outside => SecondaryThreshold.HasValue && (value < Threshold || value > SecondaryThreshold.Value),
            _ => false
        };
    }

    public string GetAlertMessage(double value)
    {
        var conditionText = Condition switch
        {
            AlertCondition.GreaterThan => $"exceeded threshold of {Threshold:F2}",
            AlertCondition.LessThan => $"fell below threshold of {Threshold:F2}",
            AlertCondition.Equals => $"equals threshold of {Threshold:F2}",
            AlertCondition.Between => $"is between {Threshold:F2} and {SecondaryThreshold:F2}",
            AlertCondition.Outside => $"is outside range {Threshold:F2} to {SecondaryThreshold:F2}",
            _ => "met unknown condition"
        };

        return $"Metric '{MetricName}' {conditionText}. Current value: {value:F2}";
    }

    public override string ToString()
    {
        var conditionText = Condition switch
        {
            AlertCondition.Between => $"{Condition} {Threshold:F2} and {SecondaryThreshold:F2}",
            AlertCondition.Outside => $"{Condition} {Threshold:F2} to {SecondaryThreshold:F2}",
            _ => $"{Condition} {Threshold:F2}"
        };

        return $"AlertRule '{Name}' [{Severity}]: {MetricName} {conditionText} (Enabled: {IsEnabled})";
    }
}

/// <summary>
/// Represents a detected bottleneck in the system.
/// </summary>
public class Bottleneck
{
    public Bottleneck(
        string id,
        string name,
        BottleneckType type,
        string description,
        double severity,
        string component,
        IEnumerable<string> affectedMetrics,
        DateTime detectedAt,
        string? recommendation = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Severity = Math.Max(0, Math.Min(1, severity)); // Clamp between 0 and 1
        Component = component ?? throw new ArgumentNullException(nameof(component));
        AffectedMetrics = affectedMetrics?.ToList() ?? new List<string>();
        DetectedAt = detectedAt;
        Recommendation = recommendation;
    }

    /// <summary>
    /// Gets the bottleneck ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the bottleneck name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the bottleneck type.
    /// </summary>
    public BottleneckType Type { get; }

    /// <summary>
    /// Gets the bottleneck description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the bottleneck severity (0.0 to 1.0).
    /// </summary>
    public double Severity { get; }

    /// <summary>
    /// Gets the affected component.
    /// </summary>
    public string Component { get; }

    /// <summary>
    /// Gets the affected metrics.
    /// </summary>
    public IReadOnlyList<string> AffectedMetrics { get; }

    /// <summary>
    /// Gets when the bottleneck was detected.
    /// </summary>
    public DateTime DetectedAt { get; }

    /// <summary>
    /// Gets the recommendation for resolving the bottleneck.
    /// </summary>
    public string? Recommendation { get; }

    /// <summary>
    /// Gets how long the bottleneck has been active.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - DetectedAt;

    public override string ToString()
    {
        var severityText = Severity switch
        {
            >= 0.8 => "Critical",
            >= 0.6 => "High",
            >= 0.4 => "Medium",
            >= 0.2 => "Low",
            _ => "Minimal"
        };

        return $"[{severityText}] {Name} in {Component}: {Description} " +
               $"(Severity: {Severity:P1}, Duration: {Duration.TotalMinutes:F1}m)";
    }
}

/// <summary>
/// Enumeration of bottleneck types.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// CPU bottleneck.
    /// </summary>
    CPU,

    /// <summary>
    /// Memory bottleneck.
    /// </summary>
    Memory,

    /// <summary>
    /// Disk I/O bottleneck.
    /// </summary>
    DiskIO,

    /// <summary>
    /// Network I/O bottleneck.
    /// </summary>
    NetworkIO,

    /// <summary>
    /// Lock contention bottleneck.
    /// </summary>
    LockContention,

    /// <summary>
    /// Cache miss bottleneck.
    /// </summary>
    CacheMiss,

    /// <summary>
    /// Index performance bottleneck.
    /// </summary>
    IndexPerformance,

    /// <summary>
    /// Query performance bottleneck.
    /// </summary>
    QueryPerformance,

    /// <summary>
    /// Thread pool bottleneck.
    /// </summary>
    ThreadPool,

    /// <summary>
    /// Garbage collection bottleneck.
    /// </summary>
    GarbageCollection
}

/// <summary>
/// Interface for bottleneck detection rules.
/// </summary>
public interface IBottleneckDetectionRule
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
    /// Gets the bottleneck type this rule detects.
    /// </summary>
    BottleneckType BottleneckType { get; }

    /// <summary>
    /// Gets whether the rule is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Analyzes metrics to detect bottlenecks.
    /// </summary>
    /// <param name="metrics">Metrics to analyze</param>
    /// <returns>Detected bottlenecks</returns>
    IEnumerable<Bottleneck> Analyze(IEnumerable<MetricValue> metrics);
}

/// <summary>
/// Event arguments for alert triggered events.
/// </summary>
public class AlertTriggeredEventArgs : EventArgs
{
    public AlertTriggeredEventArgs(Alert alert)
    {
        Alert = alert ?? throw new ArgumentNullException(nameof(alert));
    }

    /// <summary>
    /// Gets the triggered alert.
    /// </summary>
    public Alert Alert { get; }
}

/// <summary>
/// Event arguments for dashboard data updated events.
/// </summary>
public class DashboardDataUpdatedEventArgs : EventArgs
{
    public DashboardDataUpdatedEventArgs(DashboardData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the updated dashboard data.
    /// </summary>
    public DashboardData Data { get; }
}

/// <summary>
/// Event arguments for bottleneck detected events.
/// </summary>
public class BottleneckDetectedEventArgs : EventArgs
{
    public BottleneckDetectedEventArgs(Bottleneck bottleneck)
    {
        Bottleneck = bottleneck ?? throw new ArgumentNullException(nameof(bottleneck));
    }

    /// <summary>
    /// Gets the detected bottleneck.
    /// </summary>
    public Bottleneck Bottleneck { get; }
}

/// <summary>
/// Simple bottleneck detection rule for CPU usage.
/// </summary>
public class CpuBottleneckDetectionRule : IBottleneckDetectionRule
{
    private readonly double _threshold;

    public CpuBottleneckDetectionRule(string id, string name, double threshold = 0.8, bool isEnabled = true)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _threshold = threshold;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Name { get; }
    public BottleneckType BottleneckType => BottleneckType.CPU;
    public bool IsEnabled { get; }

    public IEnumerable<Bottleneck> Analyze(IEnumerable<MetricValue> metrics)
    {
        if (!IsEnabled) yield break;

        var cpuMetrics = metrics.Where(m => m.Name.Contains("cpu", StringComparison.OrdinalIgnoreCase) && 
                                           m.Name.Contains("usage", StringComparison.OrdinalIgnoreCase))
                               .ToList();

        foreach (var metric in cpuMetrics)
        {
            var utilization = metric.Value / 100.0; // Assuming CPU is reported as percentage
            
            if (utilization > _threshold)
            {
                var severity = Math.Min(1.0, (utilization - _threshold) / (1.0 - _threshold));
                
                yield return new Bottleneck(
                    $"cpu-bottleneck-{Guid.NewGuid():N}",
                    "High CPU Usage",
                    BottleneckType.CPU,
                    $"CPU usage is {utilization:P1}, which exceeds the threshold of {_threshold:P1}",
                    severity,
                    "CPU",
                    new[] { metric.Name },
                    DateTime.UtcNow,
                    "Consider optimizing CPU-intensive operations or scaling horizontally"
                );
            }
        }
    }

    public override string ToString()
    {
        return $"CpuBottleneckDetectionRule '{Name}': Threshold={_threshold:P1} (Enabled: {IsEnabled})";
    }
}
