using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Thread-safe implementation of memory-mapped file statistics.
/// </summary>
public class MemoryMappedFileStatistics : IMemoryMappedFileStatistics
{
    private long _totalReadOperations;
    private long _totalWriteOperations;
    private long _totalBytesRead;
    private long _totalBytesWritten;
    private long _activeViews;
    private long _totalViewsCreated;
    private long _pageFaults;
    private long _totalAccessTimeMicroseconds;
    private long _totalAccesses;
    private long _memoryUsage;

    public long TotalReadOperations => Interlocked.Read(ref _totalReadOperations);

    public long TotalWriteOperations => Interlocked.Read(ref _totalWriteOperations);

    public long TotalBytesRead => Interlocked.Read(ref _totalBytesRead);

    public long TotalBytesWritten => Interlocked.Read(ref _totalBytesWritten);

    public int ActiveViews => (int)Interlocked.Read(ref _activeViews);

    public long TotalViewsCreated => Interlocked.Read(ref _totalViewsCreated);

    public long PageFaults => Interlocked.Read(ref _pageFaults);

    public double AverageAccessTimeMicroseconds
    {
        get
        {
            var accesses = Interlocked.Read(ref _totalAccesses);
            return accesses > 0 ? (double)Interlocked.Read(ref _totalAccessTimeMicroseconds) / accesses : 0.0;
        }
    }

    public long MemoryUsage => Interlocked.Read(ref _memoryUsage);

    /// <summary>
    /// Records a read operation.
    /// </summary>
    /// <param name="bytesRead">Number of bytes read</param>
    public void RecordReadOperation(long bytesRead)
    {
        Interlocked.Increment(ref _totalReadOperations);
        Interlocked.Add(ref _totalBytesRead, bytesRead);
    }

    /// <summary>
    /// Records a write operation.
    /// </summary>
    /// <param name="bytesWritten">Number of bytes written</param>
    public void RecordWriteOperation(long bytesWritten)
    {
        Interlocked.Increment(ref _totalWriteOperations);
        Interlocked.Add(ref _totalBytesWritten, bytesWritten);
    }

    /// <summary>
    /// Records a view creation.
    /// </summary>
    public void RecordViewCreated()
    {
        Interlocked.Increment(ref _activeViews);
        Interlocked.Increment(ref _totalViewsCreated);
    }

    /// <summary>
    /// Records a view disposal.
    /// </summary>
    public void RecordViewDisposed()
    {
        Interlocked.Decrement(ref _activeViews);
    }

    /// <summary>
    /// Records a page fault.
    /// </summary>
    public void RecordPageFault()
    {
        Interlocked.Increment(ref _pageFaults);
    }

    /// <summary>
    /// Records a memory access.
    /// </summary>
    /// <param name="accessTime">Access time</param>
    public void RecordAccess(TimeSpan accessTime)
    {
        Interlocked.Increment(ref _totalAccesses);
        Interlocked.Add(ref _totalAccessTimeMicroseconds, (long)accessTime.TotalMicroseconds);
    }

    /// <summary>
    /// Updates memory usage.
    /// </summary>
    /// <param name="memoryUsage">Current memory usage in bytes</param>
    public void UpdateMemoryUsage(long memoryUsage)
    {
        Interlocked.Exchange(ref _memoryUsage, memoryUsage);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalReadOperations, 0);
        Interlocked.Exchange(ref _totalWriteOperations, 0);
        Interlocked.Exchange(ref _totalBytesRead, 0);
        Interlocked.Exchange(ref _totalBytesWritten, 0);
        Interlocked.Exchange(ref _totalViewsCreated, 0);
        Interlocked.Exchange(ref _pageFaults, 0);
        Interlocked.Exchange(ref _totalAccessTimeMicroseconds, 0);
        Interlocked.Exchange(ref _totalAccesses, 0);
        // Note: We don't reset active views and memory usage as they represent current state
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public MemoryMappedFileStatisticsSnapshot GetSnapshot()
    {
        return new MemoryMappedFileStatisticsSnapshot(
            TotalReadOperations,
            TotalWriteOperations,
            TotalBytesRead,
            TotalBytesWritten,
            ActiveViews,
            TotalViewsCreated,
            PageFaults,
            AverageAccessTimeMicroseconds,
            MemoryUsage,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of memory-mapped file statistics at a point in time.
/// </summary>
public class MemoryMappedFileStatisticsSnapshot
{
    public MemoryMappedFileStatisticsSnapshot(
        long totalReadOperations,
        long totalWriteOperations,
        long totalBytesRead,
        long totalBytesWritten,
        int activeViews,
        long totalViewsCreated,
        long pageFaults,
        double averageAccessTimeMicroseconds,
        long memoryUsage,
        DateTime timestamp)
    {
        TotalReadOperations = totalReadOperations;
        TotalWriteOperations = totalWriteOperations;
        TotalBytesRead = totalBytesRead;
        TotalBytesWritten = totalBytesWritten;
        ActiveViews = activeViews;
        TotalViewsCreated = totalViewsCreated;
        PageFaults = pageFaults;
        AverageAccessTimeMicroseconds = averageAccessTimeMicroseconds;
        MemoryUsage = memoryUsage;
        Timestamp = timestamp;
    }

    public long TotalReadOperations { get; }
    public long TotalWriteOperations { get; }
    public long TotalBytesRead { get; }
    public long TotalBytesWritten { get; }
    public int ActiveViews { get; }
    public long TotalViewsCreated { get; }
    public long PageFaults { get; }
    public double AverageAccessTimeMicroseconds { get; }
    public long MemoryUsage { get; }
    public DateTime Timestamp { get; }

    public long TotalOperations => TotalReadOperations + TotalWriteOperations;
    public long TotalBytes => TotalBytesRead + TotalBytesWritten;
    public double PageFaultRate => TotalOperations > 0 ? (double)PageFaults / TotalOperations : 0.0;
    public double ViewUtilization => TotalViewsCreated > 0 ? (double)ActiveViews / TotalViewsCreated : 0.0;

    public override string ToString()
    {
        return $"MemoryMappedFile Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Operations={TotalOperations:N0} (R:{TotalReadOperations:N0}, W:{TotalWriteOperations:N0}), " +
               $"Bytes={TotalBytes:N0} (R:{TotalBytesRead:N0}, W:{TotalBytesWritten:N0}), " +
               $"Views={ActiveViews}/{TotalViewsCreated:N0} ({ViewUtilization:P1}), " +
               $"PageFaults={PageFaults:N0} ({PageFaultRate:P2}), " +
               $"AvgAccess={AverageAccessTimeMicroseconds:F1}μs, " +
               $"Memory={MemoryUsage:N0} bytes";
    }
}

/// <summary>
/// Performance metrics for memory-mapped file operations.
/// </summary>
public class MemoryMappedFilePerformanceMetrics
{
    public MemoryMappedFilePerformanceMetrics(
        double throughputBytesPerSecond,
        double iopsRead,
        double iopsWrite,
        double averageLatencyMicroseconds,
        double pageFaultRate,
        double memoryEfficiency,
        double viewCacheHitRate,
        DateTime timestamp)
    {
        ThroughputBytesPerSecond = throughputBytesPerSecond;
        IOPSRead = iopsRead;
        IOPSWrite = iopsWrite;
        AverageLatencyMicroseconds = averageLatencyMicroseconds;
        PageFaultRate = pageFaultRate;
        MemoryEfficiency = memoryEfficiency;
        ViewCacheHitRate = viewCacheHitRate;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the throughput in bytes per second.
    /// </summary>
    public double ThroughputBytesPerSecond { get; }

    /// <summary>
    /// Gets the read operations per second.
    /// </summary>
    public double IOPSRead { get; }

    /// <summary>
    /// Gets the write operations per second.
    /// </summary>
    public double IOPSWrite { get; }

    /// <summary>
    /// Gets the average latency in microseconds.
    /// </summary>
    public double AverageLatencyMicroseconds { get; }

    /// <summary>
    /// Gets the page fault rate.
    /// </summary>
    public double PageFaultRate { get; }

    /// <summary>
    /// Gets the memory efficiency ratio.
    /// </summary>
    public double MemoryEfficiency { get; }

    /// <summary>
    /// Gets the view cache hit rate.
    /// </summary>
    public double ViewCacheHitRate { get; }

    /// <summary>
    /// Gets the timestamp of these metrics.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the total IOPS.
    /// </summary>
    public double TotalIOPS => IOPSRead + IOPSWrite;

    public override string ToString()
    {
        return $"MemoryMappedFile Performance [{Timestamp:HH:mm:ss}]: " +
               $"Throughput={ThroughputBytesPerSecond:F0} bytes/sec, " +
               $"IOPS={TotalIOPS:F0} (R:{IOPSRead:F0}, W:{IOPSWrite:F0}), " +
               $"Latency={AverageLatencyMicroseconds:F1}μs, " +
               $"PageFaults={PageFaultRate:P2}, " +
               $"MemEff={MemoryEfficiency:P1}, " +
               $"CacheHit={ViewCacheHitRate:P1}";
    }
}
