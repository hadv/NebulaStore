using System;
using System.Diagnostics;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Monitoring;

/// <summary>
/// System resource monitor for tracking CPU, memory, disk, and network usage.
/// </summary>
public class SystemResourceMonitor : ISystemResourceMonitor
{
    private readonly Process _currentProcess;
    private readonly Timer? _monitoringTimer;
    private volatile bool _isMonitoring;
    private volatile bool _isDisposed;

    // Cached values - using Interlocked for thread-safe access to long values
    private double _cpuUsagePercent;
    private long _memoryUsageBytes;
    private long _availableMemoryBytes;
    private double _diskReadBytesPerSecond;
    private double _diskWriteBytesPerSecond;
    private double _networkReadBytesPerSecond;
    private double _networkWriteBytesPerSecond;
    private int _threadCount;
    private int _handleCount;

    private readonly object _lockObject = new();

    public SystemResourceMonitor()
    {
        _currentProcess = Process.GetCurrentProcess();

        // Initialize with default values
        lock (_lockObject)
        {
            _cpuUsagePercent = 0.0;
            _memoryUsageBytes = 0;
            _availableMemoryBytes = GC.GetTotalMemory(false);
            _diskReadBytesPerSecond = 0.0;
            _diskWriteBytesPerSecond = 0.0;
            _networkReadBytesPerSecond = 0.0;
            _networkWriteBytesPerSecond = 0.0;
            _threadCount = Environment.ProcessorCount;
            _handleCount = 0;
        }

        UpdateMetrics();
    }

    public double CpuUsagePercent
    {
        get { lock (_lockObject) return _cpuUsagePercent; }
    }

    public long MemoryUsageBytes
    {
        get { return Interlocked.Read(ref _memoryUsageBytes); }
    }

    public long AvailableMemoryBytes
    {
        get { return Interlocked.Read(ref _availableMemoryBytes); }
    }

    public double DiskReadBytesPerSecond
    {
        get { lock (_lockObject) return _diskReadBytesPerSecond; }
    }

    public double DiskWriteBytesPerSecond
    {
        get { lock (_lockObject) return _diskWriteBytesPerSecond; }
    }

    public double NetworkReadBytesPerSecond
    {
        get { lock (_lockObject) return _networkReadBytesPerSecond; }
    }

    public double NetworkWriteBytesPerSecond
    {
        get { lock (_lockObject) return _networkWriteBytesPerSecond; }
    }

    public int ThreadCount
    {
        get { return _threadCount; }
    }

    public int HandleCount
    {
        get { return _handleCount; }
    }

    public SystemResourceSnapshot GetSnapshot()
    {
        UpdateMetrics();
        
        return new SystemResourceSnapshot(
            _cpuUsagePercent,
            _memoryUsageBytes,
            _availableMemoryBytes,
            _diskReadBytesPerSecond,
            _diskWriteBytesPerSecond,
            _networkReadBytesPerSecond,
            _networkWriteBytesPerSecond,
            _threadCount,
            _handleCount,
            DateTime.UtcNow
        );
    }

    public void StartMonitoring(TimeSpan interval)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SystemResourceMonitor));
        if (_isMonitoring) return;

        _isMonitoring = true;
        _monitoringTimer?.Change(interval, interval);
    }

    public void StopMonitoring()
    {
        if (_isDisposed) return;

        _isMonitoring = false;
        _monitoringTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateMetrics()
    {
        try
        {
            if (_isDisposed) return;

            // Update process-specific metrics
            _currentProcess.Refresh();

            lock (_lockObject)
            {
                Interlocked.Exchange(ref _memoryUsageBytes, _currentProcess.WorkingSet64);
                _threadCount = _currentProcess.Threads.Count;
                _handleCount = _currentProcess.HandleCount;

                // Estimate CPU usage based on process metrics
                _cpuUsagePercent = Math.Min(100.0, _currentProcess.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100);

                // Estimate available memory
                var totalMemory = GC.GetTotalMemory(false);
                Interlocked.Exchange(ref _availableMemoryBytes, Math.Max(0, totalMemory));

                // For disk and network metrics, we'll use placeholder values
                // In a real implementation, these would come from OS-specific APIs
                _diskReadBytesPerSecond = 0;
                _diskWriteBytesPerSecond = 0;
                _networkReadBytesPerSecond = 0;
                _networkWriteBytesPerSecond = 0;
            }
        }
        catch
        {
            // Ignore errors in metric collection
        }
    }

    private void MonitoringTimerCallback(object? state)
    {
        if (_isMonitoring && !_isDisposed)
        {
            UpdateMetrics();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        StopMonitoring();
        
        _monitoringTimer?.Dispose();
        _currentProcess?.Dispose();
    }
}

/// <summary>
/// Enhanced system resource monitor with additional metrics and alerting.
/// </summary>
public class EnhancedSystemResourceMonitor : ISystemResourceMonitor
{
    private readonly SystemResourceMonitor _baseMonitor;
    private readonly IPerformanceMetrics _metrics;
    private readonly Timer _alertingTimer;
    private readonly SystemResourceThresholds _thresholds;
    private volatile bool _isDisposed;

    public EnhancedSystemResourceMonitor(
        IPerformanceMetrics metrics, 
        SystemResourceThresholds? thresholds = null)
    {
        _baseMonitor = new SystemResourceMonitor();
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _thresholds = thresholds ?? new SystemResourceThresholds();

        // Set up alerting timer
        _alertingTimer = new Timer(CheckThresholds, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public double CpuUsagePercent => _baseMonitor.CpuUsagePercent;
    public long MemoryUsageBytes => _baseMonitor.MemoryUsageBytes;
    public long AvailableMemoryBytes => _baseMonitor.AvailableMemoryBytes;
    public double DiskReadBytesPerSecond => _baseMonitor.DiskReadBytesPerSecond;
    public double DiskWriteBytesPerSecond => _baseMonitor.DiskWriteBytesPerSecond;
    public double NetworkReadBytesPerSecond => _baseMonitor.NetworkReadBytesPerSecond;
    public double NetworkWriteBytesPerSecond => _baseMonitor.NetworkWriteBytesPerSecond;
    public int ThreadCount => _baseMonitor.ThreadCount;
    public int HandleCount => _baseMonitor.HandleCount;

    public SystemResourceSnapshot GetSnapshot()
    {
        var snapshot = _baseMonitor.GetSnapshot();
        
        // Record metrics
        _metrics.RecordGauge("system.cpu.usage_percent", snapshot.CpuUsagePercent);
        _metrics.RecordGauge("system.memory.usage_bytes", snapshot.MemoryUsageBytes);
        _metrics.RecordGauge("system.memory.available_bytes", snapshot.AvailableMemoryBytes);
        _metrics.RecordGauge("system.memory.usage_percent", snapshot.MemoryUsagePercent);
        _metrics.RecordGauge("system.disk.read_bytes_per_second", snapshot.DiskReadBytesPerSecond);
        _metrics.RecordGauge("system.disk.write_bytes_per_second", snapshot.DiskWriteBytesPerSecond);
        _metrics.RecordGauge("system.network.read_bytes_per_second", snapshot.NetworkReadBytesPerSecond);
        _metrics.RecordGauge("system.network.write_bytes_per_second", snapshot.NetworkWriteBytesPerSecond);
        _metrics.RecordGauge("system.threads.count", snapshot.ThreadCount);
        _metrics.RecordGauge("system.handles.count", snapshot.HandleCount);

        return snapshot;
    }

    public void StartMonitoring(TimeSpan interval)
    {
        _baseMonitor.StartMonitoring(interval);
    }

    public void StopMonitoring()
    {
        _baseMonitor.StopMonitoring();
    }

    private void CheckThresholds(object? state)
    {
        try
        {
            if (_isDisposed) return;

            var snapshot = GetSnapshot();

            // Check CPU threshold
            if (snapshot.CpuUsagePercent > _thresholds.CpuUsagePercentThreshold)
            {
                _metrics.RecordCounter("system.alerts.cpu_high", 1, 
                    new Dictionary<string, string> { { "value", snapshot.CpuUsagePercent.ToString("F1") } });
            }

            // Check memory threshold
            if (snapshot.MemoryUsagePercent > _thresholds.MemoryUsagePercentThreshold)
            {
                _metrics.RecordCounter("system.alerts.memory_high", 1,
                    new Dictionary<string, string> { { "value", snapshot.MemoryUsagePercent.ToString("F1") } });
            }

            // Check disk I/O threshold
            var totalDiskIO = snapshot.TotalDiskBytesPerSecond;
            if (totalDiskIO > _thresholds.DiskIOBytesPerSecondThreshold)
            {
                _metrics.RecordCounter("system.alerts.disk_io_high", 1,
                    new Dictionary<string, string> { { "value", totalDiskIO.ToString("F0") } });
            }

            // Check thread count threshold
            if (snapshot.ThreadCount > _thresholds.ThreadCountThreshold)
            {
                _metrics.RecordCounter("system.alerts.thread_count_high", 1,
                    new Dictionary<string, string> { { "value", snapshot.ThreadCount.ToString() } });
            }
        }
        catch
        {
            // Ignore threshold checking errors
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _alertingTimer?.Dispose();
        _baseMonitor?.Dispose();
    }
}

/// <summary>
/// Thresholds for system resource alerting.
/// </summary>
public class SystemResourceThresholds
{
    /// <summary>
    /// Gets or sets the CPU usage percentage threshold.
    /// </summary>
    public double CpuUsagePercentThreshold { get; set; } = 80.0; // 80%

    /// <summary>
    /// Gets or sets the memory usage percentage threshold.
    /// </summary>
    public double MemoryUsagePercentThreshold { get; set; } = 85.0; // 85%

    /// <summary>
    /// Gets or sets the disk I/O bytes per second threshold.
    /// </summary>
    public double DiskIOBytesPerSecondThreshold { get; set; } = 100 * 1024 * 1024; // 100 MB/s

    /// <summary>
    /// Gets or sets the thread count threshold.
    /// </summary>
    public int ThreadCountThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the handle count threshold.
    /// </summary>
    public int HandleCountThreshold { get; set; } = 10000;

    public override string ToString()
    {
        return $"SystemResourceThresholds[CPU={CpuUsagePercentThreshold:F1}%, " +
               $"Memory={MemoryUsagePercentThreshold:F1}%, " +
               $"DiskIO={DiskIOBytesPerSecondThreshold:F0} B/s, " +
               $"Threads={ThreadCountThreshold}, Handles={HandleCountThreshold}]";
    }
}
