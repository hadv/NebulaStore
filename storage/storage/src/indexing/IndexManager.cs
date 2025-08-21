using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Indexing;

/// <summary>
/// Index manager that coordinates multiple indexes with optimization and maintenance.
/// </summary>
public class IndexManager : IIndexManager
{
    private readonly ConcurrentDictionary<string, object> _indexes;
    private readonly Timer _maintenanceTimer;
    private readonly IndexManagerConfiguration _configuration;
    private volatile bool _isDisposed;

    public IndexManager(IndexManagerConfiguration? configuration = null)
    {
        _configuration = configuration ?? new IndexManagerConfiguration();
        _indexes = new ConcurrentDictionary<string, object>();

        // Set up maintenance timer
        if (_configuration.MaintenanceInterval > TimeSpan.Zero)
        {
            _maintenanceTimer = new Timer(PerformMaintenance, null, 
                _configuration.MaintenanceInterval, _configuration.MaintenanceInterval);
        }
    }

    public int IndexCount => _indexes.Count;

    public IIndex<TKey, TValue> GetOrCreateIndex<TKey, TValue>(string name, IndexConfiguration? configuration = null)
        where TKey : notnull
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        return (IIndex<TKey, TValue>)_indexes.GetOrAdd(name, _ =>
        {
            var indexConfig = configuration ?? new IndexConfiguration();
            
            return indexConfig.Type switch
            {
                IndexType.Hash => new HashIndex<TKey, TValue>(name, indexConfig),
                IndexType.BTree when typeof(TKey).GetInterface(nameof(IComparable<TKey>)) != null => 
                    new BTreeIndex<TKey, TValue>(name, indexConfig),
                IndexType.BTree => throw new ArgumentException($"B-tree index requires comparable keys, but {typeof(TKey).Name} is not comparable"),
                _ => new HashIndex<TKey, TValue>(name, indexConfig) // Default to hash index
            };
        });
    }

    public IIndex<TKey, TValue>? GetIndex<TKey, TValue>(string name)
        where TKey : notnull
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        return _indexes.TryGetValue(name, out var index) && index is IIndex<TKey, TValue> typedIndex ? typedIndex : null;
    }

    public bool RemoveIndex(string name)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        if (_indexes.TryRemove(name, out var index))
        {
            if (index is IDisposable disposableIndex)
            {
                disposableIndex.Dispose();
            }
            return true;
        }

        return false;
    }

    public IEnumerable<string> GetIndexNames()
    {
        ThrowIfDisposed();
        return _indexes.Keys.ToList();
    }

    public IndexManagerStatistics GetAggregateStatistics()
    {
        ThrowIfDisposed();

        var indexStats = new List<IIndexStatistics>();
        
        foreach (var index in _indexes.Values)
        {
            if (index is IIndex<object, object> genericIndex)
            {
                indexStats.Add(genericIndex.Statistics);
            }
        }

        return new IndexManagerStatistics(
            indexStats.Sum(s => s.TotalLookups),
            indexStats.Sum(s => s.TotalInsertions),
            indexStats.Sum(s => s.TotalDeletions),
            indexStats.Sum(s => s.TotalUpdates),
            indexStats.Count > 0 ? indexStats.Average(s => s.HitRatio) : 0.0,
            indexStats.Count > 0 ? indexStats.Average(s => s.AverageLookupTimeMicroseconds) : 0.0,
            indexStats.Sum(s => s.MemoryUsageBytes),
            indexStats.Count > 0 ? indexStats.Average(s => s.LoadFactor) : 0.0,
            _indexes.Count,
            DateTime.UtcNow
        );
    }

    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var rebuildTasks = new List<Task>();
        
        foreach (var index in _indexes.Values)
        {
            if (index is IIndex<object, object> genericIndex)
            {
                rebuildTasks.Add(genericIndex.RebuildAsync(cancellationToken));
            }
        }

        await Task.WhenAll(rebuildTasks);
    }

    public async Task<IndexMaintenanceResult> PerformMaintenanceAsync()
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var maintenanceActions = 0;
        var errors = 0;
        var memoryFreed = 0L;

        foreach (var kvp in _indexes.ToList())
        {
            try
            {
                if (kvp.Value is IIndex<object, object> index)
                {
                    var beforeMemory = index.Statistics.MemoryUsageBytes;
                    
                    // Check if rebuild is needed based on load factor or operation count
                    var stats = index.Statistics;
                    var totalOps = stats.TotalInsertions + stats.TotalDeletions + stats.TotalUpdates;
                    
                    if (stats.LoadFactor > _configuration.RebuildLoadFactorThreshold ||
                        totalOps > _configuration.RebuildOperationThreshold)
                    {
                        await index.RebuildAsync();
                        maintenanceActions++;
                        
                        var afterMemory = index.Statistics.MemoryUsageBytes;
                        memoryFreed += Math.Max(0, beforeMemory - afterMemory);
                    }
                }
            }
            catch
            {
                errors++;
            }
        }

        // Trigger garbage collection if significant memory was freed
        if (memoryFreed > _configuration.GCThresholdBytes)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        var duration = DateTime.UtcNow - startTime;
        
        return new IndexMaintenanceResult(
            maintenanceActions,
            errors,
            memoryFreed,
            duration,
            DateTime.UtcNow
        );
    }

    private void PerformMaintenance(object? state)
    {
        try
        {
            if (!_isDisposed)
            {
                _ = Task.Run(async () => await PerformMaintenanceAsync());
            }
        }
        catch
        {
            // Ignore maintenance errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(IndexManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _maintenanceTimer?.Dispose();

        // Dispose all indexes
        foreach (var index in _indexes.Values)
        {
            if (index is IDisposable disposableIndex)
            {
                disposableIndex.Dispose();
            }
        }

        _indexes.Clear();
    }
}

/// <summary>
/// Configuration for index manager.
/// </summary>
public class IndexManagerConfiguration
{
    /// <summary>
    /// Gets or sets the maintenance interval.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the load factor threshold for rebuilding indexes.
    /// </summary>
    public double RebuildLoadFactorThreshold { get; set; } = 0.9; // 90%

    /// <summary>
    /// Gets or sets the operation count threshold for rebuilding indexes.
    /// </summary>
    public long RebuildOperationThreshold { get; set; } = 1000000; // 1M operations

    /// <summary>
    /// Gets or sets the memory threshold for triggering garbage collection.
    /// </summary>
    public long GCThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets whether to enable automatic optimization.
    /// </summary>
    public bool EnableAutoOptimization { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return MaintenanceInterval > TimeSpan.Zero &&
               RebuildLoadFactorThreshold > 0 && RebuildLoadFactorThreshold <= 1.0 &&
               RebuildOperationThreshold > 0 &&
               GCThresholdBytes > 0;
    }
}

/// <summary>
/// Aggregate statistics for index manager.
/// </summary>
public class IndexManagerStatistics
{
    public IndexManagerStatistics(
        long totalLookups,
        long totalInsertions,
        long totalDeletions,
        long totalUpdates,
        double averageHitRatio,
        double averageLookupTimeMicroseconds,
        long totalMemoryUsageBytes,
        double averageLoadFactor,
        int indexCount,
        DateTime timestamp)
    {
        TotalLookups = totalLookups;
        TotalInsertions = totalInsertions;
        TotalDeletions = totalDeletions;
        TotalUpdates = totalUpdates;
        AverageHitRatio = averageHitRatio;
        AverageLookupTimeMicroseconds = averageLookupTimeMicroseconds;
        TotalMemoryUsageBytes = totalMemoryUsageBytes;
        AverageLoadFactor = averageLoadFactor;
        IndexCount = indexCount;
        Timestamp = timestamp;
    }

    public long TotalLookups { get; }
    public long TotalInsertions { get; }
    public long TotalDeletions { get; }
    public long TotalUpdates { get; }
    public double AverageHitRatio { get; }
    public double AverageLookupTimeMicroseconds { get; }
    public long TotalMemoryUsageBytes { get; }
    public double AverageLoadFactor { get; }
    public int IndexCount { get; }
    public DateTime Timestamp { get; }

    public long TotalOperations => TotalLookups + TotalInsertions + TotalDeletions + TotalUpdates;

    public override string ToString()
    {
        return $"IndexManager Statistics [{Timestamp:HH:mm:ss}]: " +
               $"Indexes={IndexCount}, Operations={TotalOperations:N0}, " +
               $"AvgHitRatio={AverageHitRatio:P1}, AvgLookupTime={AverageLookupTimeMicroseconds:F1}μs, " +
               $"Memory={TotalMemoryUsageBytes:N0} bytes, AvgLoadFactor={AverageLoadFactor:F2}";
    }
}

/// <summary>
/// Result of index maintenance operation.
/// </summary>
public class IndexMaintenanceResult
{
    public IndexMaintenanceResult(int actionsPerformed, int errors, long memoryFreedBytes, TimeSpan duration, DateTime timestamp)
    {
        ActionsPerformed = actionsPerformed;
        Errors = errors;
        MemoryFreedBytes = memoryFreedBytes;
        Duration = duration;
        Timestamp = timestamp;
    }

    public int ActionsPerformed { get; }
    public int Errors { get; }
    public long MemoryFreedBytes { get; }
    public TimeSpan Duration { get; }
    public DateTime Timestamp { get; }

    public bool IsSuccessful => Errors == 0;

    public override string ToString()
    {
        return $"Index Maintenance [{Timestamp:HH:mm:ss}]: " +
               $"Actions={ActionsPerformed}, Errors={Errors}, " +
               $"MemoryFreed={MemoryFreedBytes:N0} bytes, Duration={Duration.TotalMilliseconds:F0}ms";
    }
}
