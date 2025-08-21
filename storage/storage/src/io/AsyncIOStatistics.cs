using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Thread-safe implementation of asynchronous I/O statistics.
/// </summary>
public class AsyncIOStatistics : IAsyncIOStatistics
{
    private long _totalReadOperations;
    private long _totalWriteOperations;
    private long _totalBytesRead;
    private long _totalBytesWritten;
    private long _totalReadLatencyMs;
    private long _totalWriteLatencyMs;
    private long _failedOperations;
    private long _pendingOperations;
    private DateTime _startTime;
    private readonly object _lock = new();

    public AsyncIOStatistics()
    {
        _startTime = DateTime.UtcNow;
    }

    public long TotalReadOperations => Interlocked.Read(ref _totalReadOperations);

    public long TotalWriteOperations => Interlocked.Read(ref _totalWriteOperations);

    public long TotalBytesRead => Interlocked.Read(ref _totalBytesRead);

    public long TotalBytesWritten => Interlocked.Read(ref _totalBytesWritten);

    public double AverageReadLatencyMs
    {
        get
        {
            var operations = TotalReadOperations;
            return operations > 0 ? (double)Interlocked.Read(ref _totalReadLatencyMs) / operations : 0.0;
        }
    }

    public double AverageWriteLatencyMs
    {
        get
        {
            var operations = TotalWriteOperations;
            return operations > 0 ? (double)Interlocked.Read(ref _totalWriteLatencyMs) / operations : 0.0;
        }
    }

    public int PendingOperations => (int)Interlocked.Read(ref _pendingOperations);

    public long FailedOperations => Interlocked.Read(ref _failedOperations);

    public double ReadThroughputBytesPerSecond
    {
        get
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalBytesRead / elapsed.TotalSeconds : 0.0;
        }
    }

    public double WriteThroughputBytesPerSecond
    {
        get
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalBytesWritten / elapsed.TotalSeconds : 0.0;
        }
    }

    /// <summary>
    /// Records the start of an operation.
    /// </summary>
    /// <param name="operationType">Type of operation</param>
    public void RecordOperationStart(AsyncIOOperationType operationType)
    {
        Interlocked.Increment(ref _pendingOperations);
    }

    /// <summary>
    /// Records a completed read operation.
    /// </summary>
    /// <param name="bytesRead">Number of bytes read</param>
    /// <param name="duration">Operation duration</param>
    public void RecordReadOperation(long bytesRead, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalReadOperations);
        Interlocked.Add(ref _totalBytesRead, bytesRead);
        Interlocked.Add(ref _totalReadLatencyMs, (long)duration.TotalMilliseconds);
        Interlocked.Decrement(ref _pendingOperations);
    }

    /// <summary>
    /// Records a completed write operation.
    /// </summary>
    /// <param name="bytesWritten">Number of bytes written</param>
    /// <param name="duration">Operation duration</param>
    public void RecordWriteOperation(long bytesWritten, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalWriteOperations);
        Interlocked.Add(ref _totalBytesWritten, bytesWritten);
        Interlocked.Add(ref _totalWriteLatencyMs, (long)duration.TotalMilliseconds);
        Interlocked.Decrement(ref _pendingOperations);
    }

    /// <summary>
    /// Records a completed delete operation.
    /// </summary>
    /// <param name="duration">Operation duration</param>
    public void RecordDeleteOperation(TimeSpan duration)
    {
        // Delete operations don't have bytes, but we track them for completeness
        Interlocked.Decrement(ref _pendingOperations);
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    public void RecordFailedOperation()
    {
        Interlocked.Increment(ref _failedOperations);
        Interlocked.Decrement(ref _pendingOperations);
    }

    public void Reset()
    {
        lock (_lock)
        {
            Interlocked.Exchange(ref _totalReadOperations, 0);
            Interlocked.Exchange(ref _totalWriteOperations, 0);
            Interlocked.Exchange(ref _totalBytesRead, 0);
            Interlocked.Exchange(ref _totalBytesWritten, 0);
            Interlocked.Exchange(ref _totalReadLatencyMs, 0);
            Interlocked.Exchange(ref _totalWriteLatencyMs, 0);
            Interlocked.Exchange(ref _failedOperations, 0);
            // Note: We don't reset pending operations as they represent current state
            _startTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public AsyncIOStatisticsSnapshot GetSnapshot()
    {
        return new AsyncIOStatisticsSnapshot(
            TotalReadOperations,
            TotalWriteOperations,
            TotalBytesRead,
            TotalBytesWritten,
            AverageReadLatencyMs,
            AverageWriteLatencyMs,
            PendingOperations,
            FailedOperations,
            ReadThroughputBytesPerSecond,
            WriteThroughputBytesPerSecond,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of async I/O statistics at a point in time.
/// </summary>
public class AsyncIOStatisticsSnapshot
{
    public AsyncIOStatisticsSnapshot(
        long totalReadOperations,
        long totalWriteOperations,
        long totalBytesRead,
        long totalBytesWritten,
        double averageReadLatencyMs,
        double averageWriteLatencyMs,
        int pendingOperations,
        long failedOperations,
        double readThroughputBytesPerSecond,
        double writeThroughputBytesPerSecond,
        DateTime timestamp)
    {
        TotalReadOperations = totalReadOperations;
        TotalWriteOperations = totalWriteOperations;
        TotalBytesRead = totalBytesRead;
        TotalBytesWritten = totalBytesWritten;
        AverageReadLatencyMs = averageReadLatencyMs;
        AverageWriteLatencyMs = averageWriteLatencyMs;
        PendingOperations = pendingOperations;
        FailedOperations = failedOperations;
        ReadThroughputBytesPerSecond = readThroughputBytesPerSecond;
        WriteThroughputBytesPerSecond = writeThroughputBytesPerSecond;
        Timestamp = timestamp;
    }

    public long TotalReadOperations { get; }
    public long TotalWriteOperations { get; }
    public long TotalBytesRead { get; }
    public long TotalBytesWritten { get; }
    public double AverageReadLatencyMs { get; }
    public double AverageWriteLatencyMs { get; }
    public int PendingOperations { get; }
    public long FailedOperations { get; }
    public double ReadThroughputBytesPerSecond { get; }
    public double WriteThroughputBytesPerSecond { get; }
    public DateTime Timestamp { get; }

    public long TotalOperations => TotalReadOperations + TotalWriteOperations;
    public long TotalBytes => TotalBytesRead + TotalBytesWritten;
    public double OverallThroughputBytesPerSecond => ReadThroughputBytesPerSecond + WriteThroughputBytesPerSecond;
    public double FailureRate => TotalOperations > 0 ? (double)FailedOperations / TotalOperations : 0.0;

    public override string ToString()
    {
        return $"AsyncIO Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Reads={TotalReadOperations:N0} ({TotalBytesRead:N0} bytes, {AverageReadLatencyMs:F2}ms avg), " +
               $"Writes={TotalWriteOperations:N0} ({TotalBytesWritten:N0} bytes, {AverageWriteLatencyMs:F2}ms avg), " +
               $"Pending={PendingOperations}, Failed={FailedOperations:N0} ({FailureRate:P2}), " +
               $"Throughput={OverallThroughputBytesPerSecond:F0} bytes/sec";
    }
}

/// <summary>
/// Performance metrics for async I/O operations.
/// </summary>
public class AsyncIOPerformanceMetrics
{
    public AsyncIOPerformanceMetrics(
        double iopsRead,
        double iopsWrite,
        double latencyP50Ms,
        double latencyP95Ms,
        double latencyP99Ms,
        double queueDepth,
        double cpuUtilization,
        DateTime timestamp)
    {
        IOPSRead = iopsRead;
        IOPSWrite = iopsWrite;
        LatencyP50Ms = latencyP50Ms;
        LatencyP95Ms = latencyP95Ms;
        LatencyP99Ms = latencyP99Ms;
        QueueDepth = queueDepth;
        CpuUtilization = cpuUtilization;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the read operations per second.
    /// </summary>
    public double IOPSRead { get; }

    /// <summary>
    /// Gets the write operations per second.
    /// </summary>
    public double IOPSWrite { get; }

    /// <summary>
    /// Gets the 50th percentile latency in milliseconds.
    /// </summary>
    public double LatencyP50Ms { get; }

    /// <summary>
    /// Gets the 95th percentile latency in milliseconds.
    /// </summary>
    public double LatencyP95Ms { get; }

    /// <summary>
    /// Gets the 99th percentile latency in milliseconds.
    /// </summary>
    public double LatencyP99Ms { get; }

    /// <summary>
    /// Gets the average queue depth.
    /// </summary>
    public double QueueDepth { get; }

    /// <summary>
    /// Gets the CPU utilization percentage.
    /// </summary>
    public double CpuUtilization { get; }

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
        return $"AsyncIO Performance [{Timestamp:HH:mm:ss}]: " +
               $"IOPS={TotalIOPS:F0} (R:{IOPSRead:F0}, W:{IOPSWrite:F0}), " +
               $"Latency P50/P95/P99={LatencyP50Ms:F1}/{LatencyP95Ms:F1}/{LatencyP99Ms:F1}ms, " +
               $"QueueDepth={QueueDepth:F1}, CPU={CpuUtilization:P1}";
    }
}
