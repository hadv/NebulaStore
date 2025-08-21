using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// Thread-safe implementation of object pool statistics.
/// </summary>
public class ObjectPoolStatistics : IObjectPoolStatistics
{
    private readonly int _maxCapacity;
    private long _totalCreated;
    private long _totalRetrieved;
    private long _totalReturned;
    private long _totalDiscarded;
    private long _peakSize;
    private long _peakInUse;
    private long _currentSize;
    private long _currentInUse;

    public ObjectPoolStatistics(int maxCapacity)
    {
        _maxCapacity = maxCapacity;
    }

    public long TotalCreated => Interlocked.Read(ref _totalCreated);

    public long TotalRetrieved => Interlocked.Read(ref _totalRetrieved);

    public long TotalReturned => Interlocked.Read(ref _totalReturned);

    public long TotalDiscarded => Interlocked.Read(ref _totalDiscarded);

    public double HitRatio
    {
        get
        {
            var retrieved = TotalRetrieved;
            var created = TotalCreated;
            var totalRequests = retrieved + created;
            return totalRequests > 0 ? (double)retrieved / totalRequests : 0.0;
        }
    }

    public double Utilization
    {
        get
        {
            var currentSize = Interlocked.Read(ref _currentSize);
            var currentInUse = Interlocked.Read(ref _currentInUse);
            var totalAvailable = currentSize + currentInUse;
            return totalAvailable > 0 ? (double)currentInUse / totalAvailable : 0.0;
        }
    }

    public int PeakSize => (int)Interlocked.Read(ref _peakSize);

    public int PeakInUse => (int)Interlocked.Read(ref _peakInUse);

    /// <summary>
    /// Records an object creation.
    /// </summary>
    public void RecordCreated()
    {
        Interlocked.Increment(ref _totalCreated);
    }

    /// <summary>
    /// Records an object retrieval from the pool.
    /// </summary>
    public void RecordRetrieved()
    {
        Interlocked.Increment(ref _totalRetrieved);
        UpdateInUseCount(1);
    }

    /// <summary>
    /// Records an object return to the pool.
    /// </summary>
    public void RecordReturned()
    {
        Interlocked.Increment(ref _totalReturned);
        UpdateInUseCount(-1);
        UpdatePoolSize(1);
    }

    /// <summary>
    /// Records an object being discarded.
    /// </summary>
    public void RecordDiscarded()
    {
        Interlocked.Increment(ref _totalDiscarded);
    }

    /// <summary>
    /// Records multiple objects being discarded.
    /// </summary>
    /// <param name="count">Number of objects discarded</param>
    public void RecordDiscarded(int count)
    {
        Interlocked.Add(ref _totalDiscarded, count);
    }

    /// <summary>
    /// Updates the current pool size.
    /// </summary>
    /// <param name="delta">Change in pool size</param>
    public void UpdatePoolSize(int delta)
    {
        var newSize = Interlocked.Add(ref _currentSize, delta);
        UpdatePeakSize((int)newSize);
    }

    /// <summary>
    /// Updates the current in-use count.
    /// </summary>
    /// <param name="delta">Change in in-use count</param>
    public void UpdateInUseCount(int delta)
    {
        var newInUse = Interlocked.Add(ref _currentInUse, delta);
        UpdatePeakInUse((int)newInUse);
    }

    private void UpdatePeakSize(int currentSize)
    {
        var currentPeak = _peakSize;
        while (currentSize > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakSize, currentSize, currentPeak);
            if (originalPeak == currentPeak)
                break;
            currentPeak = originalPeak;
        }
    }

    private void UpdatePeakInUse(int currentInUse)
    {
        var currentPeak = _peakInUse;
        while (currentInUse > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakInUse, currentInUse, currentPeak);
            if (originalPeak == currentPeak)
                break;
            currentPeak = originalPeak;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalCreated, 0);
        Interlocked.Exchange(ref _totalRetrieved, 0);
        Interlocked.Exchange(ref _totalReturned, 0);
        Interlocked.Exchange(ref _totalDiscarded, 0);
        Interlocked.Exchange(ref _peakSize, 0);
        Interlocked.Exchange(ref _peakInUse, 0);
        // Note: We don't reset current size and in-use count as they represent current state
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public ObjectPoolStatisticsSnapshot GetSnapshot()
    {
        return new ObjectPoolStatisticsSnapshot(
            TotalCreated,
            TotalRetrieved,
            TotalReturned,
            TotalDiscarded,
            HitRatio,
            Utilization,
            PeakSize,
            PeakInUse,
            (int)Interlocked.Read(ref _currentSize),
            (int)Interlocked.Read(ref _currentInUse),
            _maxCapacity,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of object pool statistics at a point in time.
/// </summary>
public class ObjectPoolStatisticsSnapshot
{
    public ObjectPoolStatisticsSnapshot(
        long totalCreated,
        long totalRetrieved,
        long totalReturned,
        long totalDiscarded,
        double hitRatio,
        double utilization,
        int peakSize,
        int peakInUse,
        int currentSize,
        int currentInUse,
        int maxCapacity,
        DateTime timestamp)
    {
        TotalCreated = totalCreated;
        TotalRetrieved = totalRetrieved;
        TotalReturned = totalReturned;
        TotalDiscarded = totalDiscarded;
        HitRatio = hitRatio;
        Utilization = utilization;
        PeakSize = peakSize;
        PeakInUse = peakInUse;
        CurrentSize = currentSize;
        CurrentInUse = currentInUse;
        MaxCapacity = maxCapacity;
        Timestamp = timestamp;
    }

    public long TotalCreated { get; }
    public long TotalRetrieved { get; }
    public long TotalReturned { get; }
    public long TotalDiscarded { get; }
    public double HitRatio { get; }
    public double Utilization { get; }
    public int PeakSize { get; }
    public int PeakInUse { get; }
    public int CurrentSize { get; }
    public int CurrentInUse { get; }
    public int MaxCapacity { get; }
    public DateTime Timestamp { get; }

    public long TotalRequests => TotalCreated + TotalRetrieved;
    public long TotalObjects => TotalCreated;
    public double DiscardRate => TotalObjects > 0 ? (double)TotalDiscarded / TotalObjects : 0.0;
    public double CapacityUtilization => MaxCapacity > 0 ? (double)CurrentSize / MaxCapacity : 0.0;

    public override string ToString()
    {
        return $"ObjectPool Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Created={TotalCreated:N0}, Retrieved={TotalRetrieved:N0}, " +
               $"Returned={TotalReturned:N0}, Discarded={TotalDiscarded:N0}, " +
               $"HitRatio={HitRatio:P1}, Utilization={Utilization:P1}, " +
               $"Size={CurrentSize}/{MaxCapacity} (Peak={PeakSize}), " +
               $"InUse={CurrentInUse} (Peak={PeakInUse})";
    }
}

/// <summary>
/// Performance metrics for object pools.
/// </summary>
public class ObjectPoolPerformanceMetrics
{
    public ObjectPoolPerformanceMetrics(
        double allocationRate,
        double poolEfficiency,
        double memoryPressure,
        double averageObjectLifetime,
        double gcPressureReduction,
        DateTime timestamp)
    {
        AllocationRate = allocationRate;
        PoolEfficiency = poolEfficiency;
        MemoryPressure = memoryPressure;
        AverageObjectLifetime = averageObjectLifetime;
        GCPressureReduction = gcPressureReduction;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the allocation rate (objects per second).
    /// </summary>
    public double AllocationRate { get; }

    /// <summary>
    /// Gets the pool efficiency (0.0 to 1.0).
    /// </summary>
    public double PoolEfficiency { get; }

    /// <summary>
    /// Gets the memory pressure (0.0 to 1.0).
    /// </summary>
    public double MemoryPressure { get; }

    /// <summary>
    /// Gets the average object lifetime in seconds.
    /// </summary>
    public double AverageObjectLifetime { get; }

    /// <summary>
    /// Gets the GC pressure reduction percentage.
    /// </summary>
    public double GCPressureReduction { get; }

    /// <summary>
    /// Gets the timestamp of these metrics.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"ObjectPool Performance [{Timestamp:HH:mm:ss}]: " +
               $"AllocRate={AllocationRate:F1}/sec, " +
               $"Efficiency={PoolEfficiency:P1}, " +
               $"MemPressure={MemoryPressure:P1}, " +
               $"AvgLifetime={AverageObjectLifetime:F1}s, " +
               $"GCReduction={GCPressureReduction:P1}";
    }
}
