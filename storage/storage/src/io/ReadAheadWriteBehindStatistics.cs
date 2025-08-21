using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Interface for read-ahead and write-behind statistics.
/// </summary>
public interface IReadAheadWriteBehindStatistics
{
    /// <summary>
    /// Gets the total number of read-ahead hits.
    /// </summary>
    long ReadAheadHits { get; }

    /// <summary>
    /// Gets the total number of read-ahead misses.
    /// </summary>
    long ReadAheadMisses { get; }

    /// <summary>
    /// Gets the read-ahead hit ratio.
    /// </summary>
    double ReadAheadHitRatio { get; }

    /// <summary>
    /// Gets the total number of read-ahead operations performed.
    /// </summary>
    long ReadAheadOperations { get; }

    /// <summary>
    /// Gets the total bytes read ahead.
    /// </summary>
    long ReadAheadBytes { get; }

    /// <summary>
    /// Gets the number of read-ahead errors.
    /// </summary>
    long ReadAheadErrors { get; }

    /// <summary>
    /// Gets the total number of write-behind operations queued.
    /// </summary>
    long WriteBehindQueued { get; }

    /// <summary>
    /// Gets the total number of write-behind operations processed.
    /// </summary>
    long WriteBehindProcessed { get; }

    /// <summary>
    /// Gets the total number of write-behind operations failed.
    /// </summary>
    long WriteBehindFailed { get; }

    /// <summary>
    /// Gets the total number of immediate writes.
    /// </summary>
    long ImmediateWrites { get; }

    /// <summary>
    /// Gets the write-behind success ratio.
    /// </summary>
    double WriteBehindSuccessRatio { get; }

    /// <summary>
    /// Gets the current write-behind queue depth.
    /// </summary>
    int WriteBehindQueueDepth { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Thread-safe implementation of read-ahead and write-behind statistics.
/// </summary>
public class ReadAheadWriteBehindStatistics : IReadAheadWriteBehindStatistics
{
    private long _readAheadHits;
    private long _readAheadMisses;
    private long _readAheadOperations;
    private long _readAheadBytes;
    private long _readAheadErrors;
    private long _writeBehindQueued;
    private long _writeBehindProcessed;
    private long _writeBehindFailed;
    private long _immediateWrites;
    private long _writeBehindQueueDepth;

    public long ReadAheadHits => Interlocked.Read(ref _readAheadHits);

    public long ReadAheadMisses => Interlocked.Read(ref _readAheadMisses);

    public double ReadAheadHitRatio
    {
        get
        {
            var hits = ReadAheadHits;
            var misses = ReadAheadMisses;
            var total = hits + misses;
            return total > 0 ? (double)hits / total : 0.0;
        }
    }

    public long ReadAheadOperations => Interlocked.Read(ref _readAheadOperations);

    public long ReadAheadBytes => Interlocked.Read(ref _readAheadBytes);

    public long ReadAheadErrors => Interlocked.Read(ref _readAheadErrors);

    public long WriteBehindQueued => Interlocked.Read(ref _writeBehindQueued);

    public long WriteBehindProcessed => Interlocked.Read(ref _writeBehindProcessed);

    public long WriteBehindFailed => Interlocked.Read(ref _writeBehindFailed);

    public long ImmediateWrites => Interlocked.Read(ref _immediateWrites);

    public double WriteBehindSuccessRatio
    {
        get
        {
            var processed = WriteBehindProcessed;
            var failed = WriteBehindFailed;
            var total = processed + failed;
            return total > 0 ? (double)processed / total : 0.0;
        }
    }

    public int WriteBehindQueueDepth => (int)Interlocked.Read(ref _writeBehindQueueDepth);

    /// <summary>
    /// Records a read-ahead hit.
    /// </summary>
    public void RecordReadAheadHit()
    {
        Interlocked.Increment(ref _readAheadHits);
    }

    /// <summary>
    /// Records a read-ahead miss.
    /// </summary>
    public void RecordReadAheadMiss()
    {
        Interlocked.Increment(ref _readAheadMisses);
    }

    /// <summary>
    /// Records a read-ahead operation.
    /// </summary>
    /// <param name="bytesRead">Number of bytes read ahead</param>
    public void RecordReadAheadPerformed(long bytesRead)
    {
        Interlocked.Increment(ref _readAheadOperations);
        Interlocked.Add(ref _readAheadBytes, bytesRead);
    }

    /// <summary>
    /// Records a read-ahead error.
    /// </summary>
    public void RecordReadAheadError()
    {
        Interlocked.Increment(ref _readAheadErrors);
    }

    /// <summary>
    /// Records a write-behind operation being queued.
    /// </summary>
    public void RecordWriteBehindQueued()
    {
        Interlocked.Increment(ref _writeBehindQueued);
        Interlocked.Increment(ref _writeBehindQueueDepth);
    }

    /// <summary>
    /// Records write-behind operations being processed.
    /// </summary>
    /// <param name="processed">Number of operations processed successfully</param>
    /// <param name="failed">Number of operations that failed</param>
    public void RecordWriteBehindProcessed(int processed, int failed)
    {
        Interlocked.Add(ref _writeBehindProcessed, processed);
        Interlocked.Add(ref _writeBehindFailed, failed);
        Interlocked.Add(ref _writeBehindQueueDepth, -(processed + failed));
    }

    /// <summary>
    /// Records an immediate write operation.
    /// </summary>
    public void RecordImmediateWrite()
    {
        Interlocked.Increment(ref _immediateWrites);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _readAheadHits, 0);
        Interlocked.Exchange(ref _readAheadMisses, 0);
        Interlocked.Exchange(ref _readAheadOperations, 0);
        Interlocked.Exchange(ref _readAheadBytes, 0);
        Interlocked.Exchange(ref _readAheadErrors, 0);
        Interlocked.Exchange(ref _writeBehindQueued, 0);
        Interlocked.Exchange(ref _writeBehindProcessed, 0);
        Interlocked.Exchange(ref _writeBehindFailed, 0);
        Interlocked.Exchange(ref _immediateWrites, 0);
        // Note: We don't reset queue depth as it represents current state
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public ReadAheadWriteBehindStatisticsSnapshot GetSnapshot()
    {
        return new ReadAheadWriteBehindStatisticsSnapshot(
            ReadAheadHits,
            ReadAheadMisses,
            ReadAheadHitRatio,
            ReadAheadOperations,
            ReadAheadBytes,
            ReadAheadErrors,
            WriteBehindQueued,
            WriteBehindProcessed,
            WriteBehindFailed,
            ImmediateWrites,
            WriteBehindSuccessRatio,
            WriteBehindQueueDepth,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of read-ahead and write-behind statistics at a point in time.
/// </summary>
public class ReadAheadWriteBehindStatisticsSnapshot
{
    public ReadAheadWriteBehindStatisticsSnapshot(
        long readAheadHits,
        long readAheadMisses,
        double readAheadHitRatio,
        long readAheadOperations,
        long readAheadBytes,
        long readAheadErrors,
        long writeBehindQueued,
        long writeBehindProcessed,
        long writeBehindFailed,
        long immediateWrites,
        double writeBehindSuccessRatio,
        int writeBehindQueueDepth,
        DateTime timestamp)
    {
        ReadAheadHits = readAheadHits;
        ReadAheadMisses = readAheadMisses;
        ReadAheadHitRatio = readAheadHitRatio;
        ReadAheadOperations = readAheadOperations;
        ReadAheadBytes = readAheadBytes;
        ReadAheadErrors = readAheadErrors;
        WriteBehindQueued = writeBehindQueued;
        WriteBehindProcessed = writeBehindProcessed;
        WriteBehindFailed = writeBehindFailed;
        ImmediateWrites = immediateWrites;
        WriteBehindSuccessRatio = writeBehindSuccessRatio;
        WriteBehindQueueDepth = writeBehindQueueDepth;
        Timestamp = timestamp;
    }

    public long ReadAheadHits { get; }
    public long ReadAheadMisses { get; }
    public double ReadAheadHitRatio { get; }
    public long ReadAheadOperations { get; }
    public long ReadAheadBytes { get; }
    public long ReadAheadErrors { get; }
    public long WriteBehindQueued { get; }
    public long WriteBehindProcessed { get; }
    public long WriteBehindFailed { get; }
    public long ImmediateWrites { get; }
    public double WriteBehindSuccessRatio { get; }
    public int WriteBehindQueueDepth { get; }
    public DateTime Timestamp { get; }

    public long TotalReadRequests => ReadAheadHits + ReadAheadMisses;
    public long TotalWriteOperations => WriteBehindQueued + ImmediateWrites;
    public double ReadAheadEfficiency => ReadAheadOperations > 0 ? (double)ReadAheadBytes / ReadAheadOperations : 0.0;

    public override string ToString()
    {
        return $"ReadAhead/WriteBehind Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"ReadAhead: Hits={ReadAheadHits:N0}/{TotalReadRequests:N0} ({ReadAheadHitRatio:P1}), " +
               $"Operations={ReadAheadOperations:N0}, Bytes={ReadAheadBytes:N0}, Errors={ReadAheadErrors:N0}, " +
               $"WriteBehind: Queued={WriteBehindQueued:N0}, Processed={WriteBehindProcessed:N0}, " +
               $"Failed={WriteBehindFailed:N0} ({WriteBehindSuccessRatio:P1}), " +
               $"QueueDepth={WriteBehindQueueDepth}, Immediate={ImmediateWrites:N0}";
    }
}

/// <summary>
/// Performance metrics for read-ahead and write-behind operations.
/// </summary>
public class ReadAheadWriteBehindPerformanceMetrics
{
    public ReadAheadWriteBehindPerformanceMetrics(
        double readAheadEffectiveness,
        double writeBehindLatencyReduction,
        double averageQueueTime,
        double bufferUtilization,
        double ioReductionRatio,
        DateTime timestamp)
    {
        ReadAheadEffectiveness = readAheadEffectiveness;
        WriteBehindLatencyReduction = writeBehindLatencyReduction;
        AverageQueueTime = averageQueueTime;
        BufferUtilization = bufferUtilization;
        IOReductionRatio = ioReductionRatio;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the read-ahead effectiveness (0.0 to 1.0).
    /// </summary>
    public double ReadAheadEffectiveness { get; }

    /// <summary>
    /// Gets the write-behind latency reduction percentage.
    /// </summary>
    public double WriteBehindLatencyReduction { get; }

    /// <summary>
    /// Gets the average queue time in milliseconds.
    /// </summary>
    public double AverageQueueTime { get; }

    /// <summary>
    /// Gets the buffer utilization percentage.
    /// </summary>
    public double BufferUtilization { get; }

    /// <summary>
    /// Gets the I/O reduction ratio.
    /// </summary>
    public double IOReductionRatio { get; }

    /// <summary>
    /// Gets the timestamp of these metrics.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"ReadAhead/WriteBehind Performance [{Timestamp:HH:mm:ss}]: " +
               $"ReadAheadEff={ReadAheadEffectiveness:P1}, " +
               $"WriteBehindLatRed={WriteBehindLatencyReduction:P1}, " +
               $"AvgQueueTime={AverageQueueTime:F1}ms, " +
               $"BufferUtil={BufferUtilization:P1}, " +
               $"IOReduction={IOReductionRatio:P1}";
    }
}
