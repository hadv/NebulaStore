using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Monitoring;

/// <summary>
/// Interface for comprehensive performance metrics collection and monitoring.
/// </summary>
public interface IPerformanceMetrics : IDisposable
{
    /// <summary>
    /// Gets the metrics collector name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether metrics collection is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Records a timing metric.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="duration">Duration to record</param>
    /// <param name="tags">Optional tags</param>
    void RecordTiming(string name, TimeSpan duration, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records a counter metric.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Optional tags</param>
    void RecordCounter(string name, long value = 1, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records a gauge metric.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Optional tags</param>
    void RecordGauge(string name, double value, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records a histogram metric.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Optional tags</param>
    void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Creates a timing scope that automatically records duration when disposed.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="tags">Optional tags</param>
    /// <returns>Timing scope</returns>
    ITimingScope StartTiming(string name, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Gets current metric values.
    /// </summary>
    /// <param name="name">Metric name (optional, null for all)</param>
    /// <returns>Current metric values</returns>
    IEnumerable<MetricValue> GetCurrentValues(string? name = null);

    /// <summary>
    /// Gets metric statistics over a time period.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="period">Time period</param>
    /// <returns>Metric statistics</returns>
    MetricStatistics GetStatistics(string name, TimeSpan period);

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    void Reset();

    /// <summary>
    /// Enables or disables metrics collection.
    /// </summary>
    /// <param name="enabled">Whether to enable collection</param>
    void SetEnabled(bool enabled);
}

/// <summary>
/// Interface for timing scopes.
/// </summary>
public interface ITimingScope : IDisposable
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the elapsed time.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets whether the scope is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Stops the timing and records the metric.
    /// </summary>
    void Stop();
}

/// <summary>
/// Interface for system resource monitoring.
/// </summary>
public interface ISystemResourceMonitor : IDisposable
{
    /// <summary>
    /// Gets the current CPU usage percentage.
    /// </summary>
    double CpuUsagePercent { get; }

    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    long MemoryUsageBytes { get; }

    /// <summary>
    /// Gets the current available memory in bytes.
    /// </summary>
    long AvailableMemoryBytes { get; }

    /// <summary>
    /// Gets the current disk I/O read rate in bytes per second.
    /// </summary>
    double DiskReadBytesPerSecond { get; }

    /// <summary>
    /// Gets the current disk I/O write rate in bytes per second.
    /// </summary>
    double DiskWriteBytesPerSecond { get; }

    /// <summary>
    /// Gets the current network I/O read rate in bytes per second.
    /// </summary>
    double NetworkReadBytesPerSecond { get; }

    /// <summary>
    /// Gets the current network I/O write rate in bytes per second.
    /// </summary>
    double NetworkWriteBytesPerSecond { get; }

    /// <summary>
    /// Gets the current thread count.
    /// </summary>
    int ThreadCount { get; }

    /// <summary>
    /// Gets the current handle count.
    /// </summary>
    int HandleCount { get; }

    /// <summary>
    /// Gets comprehensive system resource snapshot.
    /// </summary>
    /// <returns>System resource snapshot</returns>
    SystemResourceSnapshot GetSnapshot();

    /// <summary>
    /// Starts monitoring system resources.
    /// </summary>
    /// <param name="interval">Monitoring interval</param>
    void StartMonitoring(TimeSpan interval);

    /// <summary>
    /// Stops monitoring system resources.
    /// </summary>
    void StopMonitoring();
}

/// <summary>
/// Represents a metric value at a point in time.
/// </summary>
public class MetricValue
{
    public MetricValue(string name, MetricType type, double value, IDictionary<string, string>? tags, DateTime timestamp)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Value = value;
        Tags = tags ?? new Dictionary<string, string>();
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the metric type.
    /// </summary>
    public MetricType Type { get; }

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the metric tags.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

    /// <summary>
    /// Gets the timestamp when the metric was recorded.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var tagsStr = Tags.Count > 0 ? $" [{string.Join(", ", Tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]" : "";
        return $"{Name}{tagsStr}: {Value} ({Type}) @ {Timestamp:HH:mm:ss.fff}";
    }
}

/// <summary>
/// Represents metric statistics over a time period.
/// </summary>
public class MetricStatistics
{
    public MetricStatistics(
        string name,
        MetricType type,
        long count,
        double sum,
        double min,
        double max,
        double mean,
        double median,
        double percentile95,
        double percentile99,
        double standardDeviation,
        TimeSpan period,
        DateTime timestamp)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Count = count;
        Sum = sum;
        Min = min;
        Max = max;
        Mean = mean;
        Median = median;
        Percentile95 = percentile95;
        Percentile99 = percentile99;
        StandardDeviation = standardDeviation;
        Period = period;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the metric type.
    /// </summary>
    public MetricType Type { get; }

    /// <summary>
    /// Gets the number of data points.
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// Gets the sum of all values.
    /// </summary>
    public double Sum { get; }

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    public double Min { get; }

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    public double Max { get; }

    /// <summary>
    /// Gets the mean (average) value.
    /// </summary>
    public double Mean { get; }

    /// <summary>
    /// Gets the median value.
    /// </summary>
    public double Median { get; }

    /// <summary>
    /// Gets the 95th percentile value.
    /// </summary>
    public double Percentile95 { get; }

    /// <summary>
    /// Gets the 99th percentile value.
    /// </summary>
    public double Percentile99 { get; }

    /// <summary>
    /// Gets the standard deviation.
    /// </summary>
    public double StandardDeviation { get; }

    /// <summary>
    /// Gets the time period these statistics cover.
    /// </summary>
    public TimeSpan Period { get; }

    /// <summary>
    /// Gets the timestamp when these statistics were calculated.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the rate per second (for counters).
    /// </summary>
    public double RatePerSecond => Period.TotalSeconds > 0 ? Sum / Period.TotalSeconds : 0.0;

    public override string ToString()
    {
        return $"{Name} ({Type}) over {Period}: " +
               $"Count={Count:N0}, Mean={Mean:F2}, Min={Min:F2}, Max={Max:F2}, " +
               $"P95={Percentile95:F2}, P99={Percentile99:F2}, StdDev={StandardDeviation:F2}";
    }
}

/// <summary>
/// System resource snapshot.
/// </summary>
public class SystemResourceSnapshot
{
    public SystemResourceSnapshot(
        double cpuUsagePercent,
        long memoryUsageBytes,
        long availableMemoryBytes,
        double diskReadBytesPerSecond,
        double diskWriteBytesPerSecond,
        double networkReadBytesPerSecond,
        double networkWriteBytesPerSecond,
        int threadCount,
        int handleCount,
        DateTime timestamp)
    {
        CpuUsagePercent = cpuUsagePercent;
        MemoryUsageBytes = memoryUsageBytes;
        AvailableMemoryBytes = availableMemoryBytes;
        DiskReadBytesPerSecond = diskReadBytesPerSecond;
        DiskWriteBytesPerSecond = diskWriteBytesPerSecond;
        NetworkReadBytesPerSecond = networkReadBytesPerSecond;
        NetworkWriteBytesPerSecond = networkWriteBytesPerSecond;
        ThreadCount = threadCount;
        HandleCount = handleCount;
        Timestamp = timestamp;
    }

    public double CpuUsagePercent { get; }
    public long MemoryUsageBytes { get; }
    public long AvailableMemoryBytes { get; }
    public double DiskReadBytesPerSecond { get; }
    public double DiskWriteBytesPerSecond { get; }
    public double NetworkReadBytesPerSecond { get; }
    public double NetworkWriteBytesPerSecond { get; }
    public int ThreadCount { get; }
    public int HandleCount { get; }
    public DateTime Timestamp { get; }

    public long TotalMemoryBytes => MemoryUsageBytes + AvailableMemoryBytes;
    public double MemoryUsagePercent => TotalMemoryBytes > 0 ? (double)MemoryUsageBytes / TotalMemoryBytes * 100 : 0.0;
    public double TotalDiskBytesPerSecond => DiskReadBytesPerSecond + DiskWriteBytesPerSecond;
    public double TotalNetworkBytesPerSecond => NetworkReadBytesPerSecond + NetworkWriteBytesPerSecond;

    public override string ToString()
    {
        return $"System Resources [{Timestamp:HH:mm:ss}]: " +
               $"CPU={CpuUsagePercent:F1}%, Memory={MemoryUsagePercent:F1}% ({MemoryUsageBytes:N0}/{TotalMemoryBytes:N0} bytes), " +
               $"Disk={TotalDiskBytesPerSecond:F0} B/s, Network={TotalNetworkBytesPerSecond:F0} B/s, " +
               $"Threads={ThreadCount}, Handles={HandleCount}";
    }
}

/// <summary>
/// Enumeration of metric types.
/// </summary>
public enum MetricType
{
    /// <summary>
    /// Counter metric that only increases.
    /// </summary>
    Counter,

    /// <summary>
    /// Gauge metric that can increase or decrease.
    /// </summary>
    Gauge,

    /// <summary>
    /// Timing metric for measuring durations.
    /// </summary>
    Timing,

    /// <summary>
    /// Histogram metric for measuring distributions.
    /// </summary>
    Histogram
}

/// <summary>
/// Configuration for performance metrics collection.
/// </summary>
public class PerformanceMetricsConfiguration
{
    /// <summary>
    /// Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the metrics retention period.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the maximum number of metrics to retain.
    /// </summary>
    public int MaxMetrics { get; set; } = 100000;

    /// <summary>
    /// Gets or sets the metrics flush interval.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets whether to enable system resource monitoring.
    /// </summary>
    public bool EnableSystemResourceMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the system resource monitoring interval.
    /// </summary>
    public TimeSpan SystemResourceMonitoringInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether to enable high-precision timing.
    /// </summary>
    public bool EnableHighPrecisionTiming { get; set; } = true;

    /// <summary>
    /// Gets or sets the histogram bucket count.
    /// </summary>
    public int HistogramBucketCount { get; set; } = 50;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return RetentionPeriod > TimeSpan.Zero &&
               MaxMetrics > 0 &&
               FlushInterval > TimeSpan.Zero &&
               SystemResourceMonitoringInterval > TimeSpan.Zero &&
               HistogramBucketCount > 0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public PerformanceMetricsConfiguration Clone()
    {
        return new PerformanceMetricsConfiguration
        {
            Enabled = Enabled,
            RetentionPeriod = RetentionPeriod,
            MaxMetrics = MaxMetrics,
            FlushInterval = FlushInterval,
            EnableSystemResourceMonitoring = EnableSystemResourceMonitoring,
            SystemResourceMonitoringInterval = SystemResourceMonitoringInterval,
            EnableHighPrecisionTiming = EnableHighPrecisionTiming,
            HistogramBucketCount = HistogramBucketCount
        };
    }

    public override string ToString()
    {
        return $"PerformanceMetricsConfiguration[Enabled={Enabled}, " +
               $"Retention={RetentionPeriod}, MaxMetrics={MaxMetrics:N0}, " +
               $"FlushInterval={FlushInterval}, SystemMonitoring={EnableSystemResourceMonitoring}, " +
               $"HighPrecisionTiming={EnableHighPrecisionTiming}]";
    }
}
