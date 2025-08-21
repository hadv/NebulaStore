using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Monitoring;

/// <summary>
/// High-performance metrics collector with comprehensive monitoring capabilities.
/// </summary>
public class PerformanceMetricsCollector : IPerformanceMetrics
{
    private readonly string _name;
    private readonly PerformanceMetricsConfiguration _configuration;
    private readonly ConcurrentDictionary<string, MetricContainer> _metrics;
    private readonly Timer _flushTimer;
    private readonly Timer _cleanupTimer;
    private volatile bool _isEnabled;
    private volatile bool _isDisposed;

    public PerformanceMetricsCollector(string name, PerformanceMetricsConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new PerformanceMetricsConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid performance metrics configuration", nameof(configuration));

        _metrics = new ConcurrentDictionary<string, MetricContainer>();
        _isEnabled = _configuration.Enabled;

        // Set up flush timer
        _flushTimer = new Timer(FlushMetrics, null, _configuration.FlushInterval, _configuration.FlushInterval);

        // Set up cleanup timer
        _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public string Name => _name;
    public bool IsEnabled => _isEnabled;

    public void RecordTiming(string name, TimeSpan duration, IDictionary<string, string>? tags = null)
    {
        if (!_isEnabled || _isDisposed) return;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        var container = GetOrCreateMetricContainer(name, MetricType.Timing);
        var value = new MetricValue(name, MetricType.Timing, duration.TotalMilliseconds, tags, DateTime.UtcNow);
        container.AddValue(value);
    }

    public void RecordCounter(string name, long value = 1, IDictionary<string, string>? tags = null)
    {
        if (!_isEnabled || _isDisposed) return;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        var container = GetOrCreateMetricContainer(name, MetricType.Counter);
        var metricValue = new MetricValue(name, MetricType.Counter, value, tags, DateTime.UtcNow);
        container.AddValue(metricValue);
    }

    public void RecordGauge(string name, double value, IDictionary<string, string>? tags = null)
    {
        if (!_isEnabled || _isDisposed) return;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        var container = GetOrCreateMetricContainer(name, MetricType.Gauge);
        var metricValue = new MetricValue(name, MetricType.Gauge, value, tags, DateTime.UtcNow);
        container.AddValue(metricValue);
    }

    public void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null)
    {
        if (!_isEnabled || _isDisposed) return;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        var container = GetOrCreateMetricContainer(name, MetricType.Histogram);
        var metricValue = new MetricValue(name, MetricType.Histogram, value, tags, DateTime.UtcNow);
        container.AddValue(metricValue);
    }

    public ITimingScope StartTiming(string name, IDictionary<string, string>? tags = null)
    {
        if (!_isEnabled || _isDisposed) return new NullTimingScope(name);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        return new TimingScope(this, name, tags);
    }

    public IEnumerable<MetricValue> GetCurrentValues(string? name = null)
    {
        if (_isDisposed) return Enumerable.Empty<MetricValue>();

        if (name != null)
        {
            return _metrics.TryGetValue(name, out var container) ? container.GetCurrentValues() : Enumerable.Empty<MetricValue>();
        }

        return _metrics.Values.SelectMany(container => container.GetCurrentValues()).ToList();
    }

    public MetricStatistics GetStatistics(string name, TimeSpan period)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(PerformanceMetricsCollector));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty", nameof(name));

        if (_metrics.TryGetValue(name, out var container))
        {
            return container.GetStatistics(period);
        }

        // Return empty statistics if metric doesn't exist
        return new MetricStatistics(name, MetricType.Counter, 0, 0, 0, 0, 0, 0, 0, 0, 0, period, DateTime.UtcNow);
    }

    public void Reset()
    {
        if (_isDisposed) return;

        foreach (var container in _metrics.Values)
        {
            container.Reset();
        }
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }

    private MetricContainer GetOrCreateMetricContainer(string name, MetricType type)
    {
        return _metrics.GetOrAdd(name, _ => new MetricContainer(name, type, _configuration));
    }

    private void FlushMetrics(object? state)
    {
        try
        {
            if (_isDisposed) return;

            // In a real implementation, this would flush metrics to external systems
            // For now, we'll just trigger cleanup of old values
            foreach (var container in _metrics.Values)
            {
                container.FlushOldValues();
            }
        }
        catch
        {
            // Ignore flush errors
        }
    }

    private void CleanupOldMetrics(object? state)
    {
        try
        {
            if (_isDisposed) return;

            var cutoffTime = DateTime.UtcNow - _configuration.RetentionPeriod;
            var metricsToRemove = new List<string>();

            foreach (var kvp in _metrics)
            {
                var container = kvp.Value;
                container.RemoveOldValues(cutoffTime);

                // Remove empty containers
                if (container.IsEmpty)
                {
                    metricsToRemove.Add(kvp.Key);
                }
            }

            // Remove empty metric containers
            foreach (var metricName in metricsToRemove)
            {
                _metrics.TryRemove(metricName, out _);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _flushTimer?.Dispose();
        _cleanupTimer?.Dispose();

        foreach (var container in _metrics.Values)
        {
            container.Dispose();
        }

        _metrics.Clear();
    }
}

/// <summary>
/// Container for storing and managing metric values.
/// </summary>
internal class MetricContainer : IDisposable
{
    private readonly string _name;
    private readonly MetricType _type;
    private readonly PerformanceMetricsConfiguration _configuration;
    private readonly ConcurrentQueue<MetricValue> _values;
    private readonly ReaderWriterLockSlim _lock;
    private volatile int _count;

    public MetricContainer(string name, MetricType type, PerformanceMetricsConfiguration configuration)
    {
        _name = name;
        _type = type;
        _configuration = configuration;
        _values = new ConcurrentQueue<MetricValue>();
        _lock = new ReaderWriterLockSlim();
    }

    public bool IsEmpty => _count == 0;

    public void AddValue(MetricValue value)
    {
        _values.Enqueue(value);
        Interlocked.Increment(ref _count);

        // Limit the number of stored values
        while (_count > _configuration.MaxMetrics)
        {
            if (_values.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
            else
            {
                break;
            }
        }
    }

    public IEnumerable<MetricValue> GetCurrentValues()
    {
        return _values.ToList();
    }

    public MetricStatistics GetStatistics(TimeSpan period)
    {
        _lock.EnterReadLock();
        try
        {
            var cutoffTime = DateTime.UtcNow - period;
            var relevantValues = _values.Where(v => v.Timestamp >= cutoffTime).Select(v => v.Value).ToList();

            if (relevantValues.Count == 0)
            {
                return new MetricStatistics(_name, _type, 0, 0, 0, 0, 0, 0, 0, 0, 0, period, DateTime.UtcNow);
            }

            var count = relevantValues.Count;
            var sum = relevantValues.Sum();
            var min = relevantValues.Min();
            var max = relevantValues.Max();
            var mean = sum / count;

            var sortedValues = relevantValues.OrderBy(v => v).ToList();
            var median = GetPercentile(sortedValues, 0.5);
            var p95 = GetPercentile(sortedValues, 0.95);
            var p99 = GetPercentile(sortedValues, 0.99);

            var variance = relevantValues.Sum(v => Math.Pow(v - mean, 2)) / count;
            var standardDeviation = Math.Sqrt(variance);

            return new MetricStatistics(_name, _type, count, sum, min, max, mean, median, p95, p99, standardDeviation, period, DateTime.UtcNow);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void FlushOldValues()
    {
        // In a real implementation, this would flush values to external storage
        // For now, we'll just ensure we don't exceed the maximum count
        while (_count > _configuration.MaxMetrics / 2) // Keep only half when flushing
        {
            if (_values.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
            else
            {
                break;
            }
        }
    }

    public void RemoveOldValues(DateTime cutoffTime)
    {
        var tempValues = new List<MetricValue>();
        
        // Collect values newer than cutoff
        while (_values.TryDequeue(out var value))
        {
            Interlocked.Decrement(ref _count);
            
            if (value.Timestamp >= cutoffTime)
            {
                tempValues.Add(value);
            }
        }

        // Re-enqueue the values we want to keep
        foreach (var value in tempValues)
        {
            _values.Enqueue(value);
            Interlocked.Increment(ref _count);
        }
    }

    public void Reset()
    {
        while (_values.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }

    private static double GetPercentile(IList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        var index = percentile * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}

/// <summary>
/// Timing scope implementation that records duration when disposed.
/// </summary>
internal class TimingScope : ITimingScope
{
    private readonly PerformanceMetricsCollector _collector;
    private readonly string _name;
    private readonly IDictionary<string, string>? _tags;
    private readonly Stopwatch _stopwatch;
    private bool _isActive;

    public TimingScope(PerformanceMetricsCollector collector, string name, IDictionary<string, string>? tags)
    {
        _collector = collector;
        _name = name;
        _tags = tags;
        _stopwatch = Stopwatch.StartNew();
        _isActive = true;
    }

    public string Name => _name;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public bool IsActive => _isActive;

    public void Stop()
    {
        if (!_isActive) return;

        _stopwatch.Stop();
        _isActive = false;
        _collector.RecordTiming(_name, _stopwatch.Elapsed, _tags);
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Null timing scope for when metrics are disabled.
/// </summary>
internal class NullTimingScope : ITimingScope
{
    public NullTimingScope(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public TimeSpan Elapsed => TimeSpan.Zero;
    public bool IsActive => false;

    public void Stop() { }
    public void Dispose() { }
}
