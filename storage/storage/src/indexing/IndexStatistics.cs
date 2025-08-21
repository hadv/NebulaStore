using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Indexing;

/// <summary>
/// Thread-safe implementation of index statistics.
/// </summary>
public class IndexStatistics : IIndexStatistics
{
    private readonly IndexConfiguration _configuration;
    private long _totalLookups;
    private long _totalInsertions;
    private long _totalDeletions;
    private long _totalUpdates;
    private long _successfulLookups;
    private long _totalLookupTimeMicroseconds;
    private long _totalInsertionTimeMicroseconds;
    private long _totalDeletionTimeMicroseconds;
    private long _totalUpdateTimeMicroseconds;
    private long _failedOperations;
    private DateTime _startTime;

    public IndexStatistics(IndexConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _startTime = DateTime.UtcNow;
    }

    public long TotalLookups => Interlocked.Read(ref _totalLookups);
    public long TotalInsertions => Interlocked.Read(ref _totalInsertions);
    public long TotalDeletions => Interlocked.Read(ref _totalDeletions);
    public long TotalUpdates => Interlocked.Read(ref _totalUpdates);

    public double HitRatio
    {
        get
        {
            var lookups = TotalLookups;
            var successful = Interlocked.Read(ref _successfulLookups);
            return lookups > 0 ? (double)successful / lookups : 0.0;
        }
    }

    public double AverageLookupTimeMicroseconds
    {
        get
        {
            var lookups = TotalLookups;
            return lookups > 0 ? (double)Interlocked.Read(ref _totalLookupTimeMicroseconds) / lookups : 0.0;
        }
    }

    public double AverageInsertionTimeMicroseconds
    {
        get
        {
            var insertions = TotalInsertions;
            return insertions > 0 ? (double)Interlocked.Read(ref _totalInsertionTimeMicroseconds) / insertions : 0.0;
        }
    }

    public long MemoryUsageBytes
    {
        get
        {
            // Rough estimation based on entry count
            // This could be made more accurate with actual memory measurement
            var entryCount = TotalInsertions - TotalDeletions;
            const long averageEntrySize = 64; // Estimated bytes per entry
            return Math.Max(0, entryCount * averageEntrySize);
        }
    }

    public double LoadFactor
    {
        get
        {
            // For hash-based indexes, this represents the ratio of entries to capacity
            var entryCount = TotalInsertions - TotalDeletions;
            var capacity = _configuration.InitialCapacity;
            return capacity > 0 ? (double)entryCount / capacity : 0.0;
        }
    }

    public int Depth
    {
        get
        {
            // For hash indexes, depth is not applicable, return 1
            // For tree indexes, this would represent the tree depth
            return 1;
        }
    }

    /// <summary>
    /// Records a lookup operation.
    /// </summary>
    /// <param name="duration">Operation duration</param>
    /// <param name="successful">Whether the lookup was successful</param>
    public void RecordLookup(TimeSpan duration, bool successful)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalLookups);
            Interlocked.Add(ref _totalLookupTimeMicroseconds, (long)duration.TotalMicroseconds);
            
            if (successful)
            {
                Interlocked.Increment(ref _successfulLookups);
            }
        }
    }

    /// <summary>
    /// Records an insertion operation.
    /// </summary>
    /// <param name="duration">Operation duration</param>
    public void RecordInsertion(TimeSpan duration)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalInsertions);
            Interlocked.Add(ref _totalInsertionTimeMicroseconds, (long)duration.TotalMicroseconds);
        }
    }

    /// <summary>
    /// Records a deletion operation.
    /// </summary>
    /// <param name="duration">Operation duration</param>
    public void RecordDeletion(TimeSpan duration)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalDeletions);
            Interlocked.Add(ref _totalDeletionTimeMicroseconds, (long)duration.TotalMicroseconds);
        }
    }

    /// <summary>
    /// Records an update operation.
    /// </summary>
    /// <param name="duration">Operation duration</param>
    public void RecordUpdate(TimeSpan duration)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalUpdates);
            Interlocked.Add(ref _totalUpdateTimeMicroseconds, (long)duration.TotalMicroseconds);
        }
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    public void RecordFailedOperation()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _failedOperations);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalLookups, 0);
        Interlocked.Exchange(ref _totalInsertions, 0);
        Interlocked.Exchange(ref _totalDeletions, 0);
        Interlocked.Exchange(ref _totalUpdates, 0);
        Interlocked.Exchange(ref _successfulLookups, 0);
        Interlocked.Exchange(ref _totalLookupTimeMicroseconds, 0);
        Interlocked.Exchange(ref _totalInsertionTimeMicroseconds, 0);
        Interlocked.Exchange(ref _totalDeletionTimeMicroseconds, 0);
        Interlocked.Exchange(ref _totalUpdateTimeMicroseconds, 0);
        Interlocked.Exchange(ref _failedOperations, 0);
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public IndexStatisticsSnapshot GetSnapshot()
    {
        return new IndexStatisticsSnapshot(
            TotalLookups,
            TotalInsertions,
            TotalDeletions,
            TotalUpdates,
            HitRatio,
            AverageLookupTimeMicroseconds,
            AverageInsertionTimeMicroseconds,
            MemoryUsageBytes,
            LoadFactor,
            Depth,
            Interlocked.Read(ref _failedOperations),
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of index statistics at a point in time.
/// </summary>
public class IndexStatisticsSnapshot
{
    public IndexStatisticsSnapshot(
        long totalLookups,
        long totalInsertions,
        long totalDeletions,
        long totalUpdates,
        double hitRatio,
        double averageLookupTimeMicroseconds,
        double averageInsertionTimeMicroseconds,
        long memoryUsageBytes,
        double loadFactor,
        int depth,
        long failedOperations,
        DateTime timestamp)
    {
        TotalLookups = totalLookups;
        TotalInsertions = totalInsertions;
        TotalDeletions = totalDeletions;
        TotalUpdates = totalUpdates;
        HitRatio = hitRatio;
        AverageLookupTimeMicroseconds = averageLookupTimeMicroseconds;
        AverageInsertionTimeMicroseconds = averageInsertionTimeMicroseconds;
        MemoryUsageBytes = memoryUsageBytes;
        LoadFactor = loadFactor;
        Depth = depth;
        FailedOperations = failedOperations;
        Timestamp = timestamp;
    }

    public long TotalLookups { get; }
    public long TotalInsertions { get; }
    public long TotalDeletions { get; }
    public long TotalUpdates { get; }
    public double HitRatio { get; }
    public double AverageLookupTimeMicroseconds { get; }
    public double AverageInsertionTimeMicroseconds { get; }
    public long MemoryUsageBytes { get; }
    public double LoadFactor { get; }
    public int Depth { get; }
    public long FailedOperations { get; }
    public DateTime Timestamp { get; }

    public long TotalOperations => TotalLookups + TotalInsertions + TotalDeletions + TotalUpdates;
    public long CurrentEntries => TotalInsertions - TotalDeletions;
    public double FailureRate => TotalOperations > 0 ? (double)FailedOperations / TotalOperations : 0.0;

    public override string ToString()
    {
        return $"Index Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Lookups={TotalLookups:N0} (Hit={HitRatio:P1}, {AverageLookupTimeMicroseconds:F1}μs avg), " +
               $"Insertions={TotalInsertions:N0} ({AverageInsertionTimeMicroseconds:F1}μs avg), " +
               $"Deletions={TotalDeletions:N0}, Updates={TotalUpdates:N0}, " +
               $"Entries={CurrentEntries:N0}, Memory={MemoryUsageBytes:N0} bytes, " +
               $"LoadFactor={LoadFactor:F2}, Failed={FailedOperations:N0} ({FailureRate:P2})";
    }
}

/// <summary>
/// Performance metrics for indexes.
/// </summary>
public class IndexPerformanceMetrics
{
    public IndexPerformanceMetrics(
        double operationsPerSecond,
        double averageLatencyMicroseconds,
        double throughputBytesPerSecond,
        double cacheEfficiency,
        double memoryEfficiency,
        DateTime timestamp)
    {
        OperationsPerSecond = operationsPerSecond;
        AverageLatencyMicroseconds = averageLatencyMicroseconds;
        ThroughputBytesPerSecond = throughputBytesPerSecond;
        CacheEfficiency = cacheEfficiency;
        MemoryEfficiency = memoryEfficiency;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the operations per second.
    /// </summary>
    public double OperationsPerSecond { get; }

    /// <summary>
    /// Gets the average latency in microseconds.
    /// </summary>
    public double AverageLatencyMicroseconds { get; }

    /// <summary>
    /// Gets the throughput in bytes per second.
    /// </summary>
    public double ThroughputBytesPerSecond { get; }

    /// <summary>
    /// Gets the cache efficiency (hit ratio).
    /// </summary>
    public double CacheEfficiency { get; }

    /// <summary>
    /// Gets the memory efficiency.
    /// </summary>
    public double MemoryEfficiency { get; }

    /// <summary>
    /// Gets the timestamp of these metrics.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Index Performance [{Timestamp:HH:mm:ss}]: " +
               $"OPS={OperationsPerSecond:F0}, " +
               $"Latency={AverageLatencyMicroseconds:F1}μs, " +
               $"Throughput={ThroughputBytesPerSecond:F0} bytes/sec, " +
               $"CacheEff={CacheEfficiency:P1}, " +
               $"MemEff={MemoryEfficiency:P1}";
    }
}
