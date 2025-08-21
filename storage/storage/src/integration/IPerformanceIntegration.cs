using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Caching;
using NebulaStore.Storage.Embedded.Configuration;
using NebulaStore.Storage.Embedded.Indexing;
using NebulaStore.Storage.Embedded.Memory;
using NebulaStore.Storage.Embedded.Monitoring;

namespace NebulaStore.Storage.Embedded.Integration;

/// <summary>
/// Interface for integrating performance optimizations with existing NebulaStore systems.
/// </summary>
public interface IPerformanceIntegration : IDisposable
{
    /// <summary>
    /// Gets the integration name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the integration status.
    /// </summary>
    IntegrationStatus Status { get; }

    /// <summary>
    /// Gets the performance configuration.
    /// </summary>
    IPerformanceConfiguration Configuration { get; }

    /// <summary>
    /// Gets the cache manager.
    /// </summary>
    ICacheManager CacheManager { get; }

    /// <summary>
    /// Gets the index manager.
    /// </summary>
    IIndexManager IndexManager { get; }

    /// <summary>
    /// Gets the buffer manager.
    /// </summary>
    IBufferManager BufferManager { get; }

    /// <summary>
    /// Gets the performance metrics.
    /// </summary>
    IPerformanceMetrics Metrics { get; }

    /// <summary>
    /// Gets the performance tuner.
    /// </summary>
    IPerformanceTuner Tuner { get; }

    /// <summary>
    /// Initializes the performance integration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the performance optimization systems.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the performance optimization systems.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Integrates with the storage manager.
    /// </summary>
    /// <param name="storageManager">Storage manager to integrate with</param>
    void IntegrateWithStorageManager(object storageManager);

    /// <summary>
    /// Integrates with the entity manager.
    /// </summary>
    /// <param name="entityManager">Entity manager to integrate with</param>
    void IntegrateWithEntityManager(object entityManager);

    /// <summary>
    /// Integrates with the transaction system.
    /// </summary>
    /// <param name="transactionManager">Transaction manager to integrate with</param>
    void IntegrateWithTransactionManager(object transactionManager);

    /// <summary>
    /// Integrates with the garbage collection system.
    /// </summary>
    /// <param name="gcManager">GC manager to integrate with</param>
    void IntegrateWithGCManager(object gcManager);

    /// <summary>
    /// Gets performance statistics for all integrated systems.
    /// </summary>
    /// <returns>Integrated performance statistics</returns>
    IntegratedPerformanceStatistics GetPerformanceStatistics();

    /// <summary>
    /// Performs a health check on all performance systems.
    /// </summary>
    /// <returns>Health check result</returns>
    Task<PerformanceHealthCheckResult> PerformHealthCheckAsync();

    /// <summary>
    /// Event fired when integration status changes.
    /// </summary>
    event EventHandler<IntegrationStatusChangedEventArgs> StatusChanged;
}

/// <summary>
/// Interface for performance-aware storage operations.
/// </summary>
public interface IPerformanceAwareStorage
{
    /// <summary>
    /// Performs a storage operation with performance optimization.
    /// </summary>
    /// <typeparam name="T">Type of operation result</typeparam>
    /// <param name="operation">Operation to perform</param>
    /// <param name="context">Performance context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    Task<T> ExecuteWithPerformanceOptimizationAsync<T>(
        Func<IPerformanceContext, Task<T>> operation,
        IPerformanceContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes storage layout based on access patterns.
    /// </summary>
    /// <param name="accessPatterns">Access pattern analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OptimizeStorageLayoutAsync(
        IEnumerable<AccessPattern> accessPatterns,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for performance context.
/// </summary>
public interface IPerformanceContext
{
    /// <summary>
    /// Gets the operation ID.
    /// </summary>
    string OperationId { get; }

    /// <summary>
    /// Gets the operation type.
    /// </summary>
    string OperationType { get; }

    /// <summary>
    /// Gets the cache manager.
    /// </summary>
    ICacheManager CacheManager { get; }

    /// <summary>
    /// Gets the index manager.
    /// </summary>
    IIndexManager IndexManager { get; }

    /// <summary>
    /// Gets the buffer manager.
    /// </summary>
    IBufferManager BufferManager { get; }

    /// <summary>
    /// Gets the performance metrics.
    /// </summary>
    IPerformanceMetrics Metrics { get; }

    /// <summary>
    /// Gets the operation metadata.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Creates a timing scope for the operation.
    /// </summary>
    /// <param name="name">Timing name</param>
    /// <returns>Timing scope</returns>
    ITimingScope StartTiming(string name);

    /// <summary>
    /// Records a performance metric.
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Metric value</param>
    /// <param name="tags">Optional tags</param>
    void RecordMetric(string name, double value, IDictionary<string, string>? tags = null);
}

/// <summary>
/// Enumeration of integration status.
/// </summary>
public enum IntegrationStatus
{
    /// <summary>
    /// Integration is not initialized.
    /// </summary>
    NotInitialized,

    /// <summary>
    /// Integration is initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Integration is initialized but not started.
    /// </summary>
    Initialized,

    /// <summary>
    /// Integration is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Integration is running.
    /// </summary>
    Running,

    /// <summary>
    /// Integration is stopping.
    /// </summary>
    Stopping,

    /// <summary>
    /// Integration is stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Integration has encountered an error.
    /// </summary>
    Error
}

/// <summary>
/// Integrated performance statistics.
/// </summary>
public class IntegratedPerformanceStatistics
{
    public IntegratedPerformanceStatistics(
        CacheManagerStatistics cacheStatistics,
        IndexManagerStatistics indexStatistics,
        ObjectPoolManagerStatistics poolStatistics,
        SystemResourceSnapshot systemResources,
        DateTime timestamp)
    {
        CacheStatistics = cacheStatistics ?? throw new ArgumentNullException(nameof(cacheStatistics));
        IndexStatistics = indexStatistics ?? throw new ArgumentNullException(nameof(indexStatistics));
        PoolStatistics = poolStatistics ?? throw new ArgumentNullException(nameof(poolStatistics));
        SystemResources = systemResources ?? throw new ArgumentNullException(nameof(systemResources));
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the cache statistics.
    /// </summary>
    public CacheManagerStatistics CacheStatistics { get; }

    /// <summary>
    /// Gets the index statistics.
    /// </summary>
    public IndexManagerStatistics IndexStatistics { get; }

    /// <summary>
    /// Gets the object pool statistics.
    /// </summary>
    public ObjectPoolManagerStatistics PoolStatistics { get; }

    /// <summary>
    /// Gets the system resource snapshot.
    /// </summary>
    public SystemResourceSnapshot SystemResources { get; }

    /// <summary>
    /// Gets the timestamp of these statistics.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the overall performance score (0.0 to 1.0).
    /// </summary>
    public double OverallPerformanceScore
    {
        get
        {
            var cacheScore = CacheStatistics.AverageHitRatio;
            var indexScore = IndexStatistics.AverageHitRatio;
            var poolScore = PoolStatistics.AverageUtilization;
            var systemScore = 1.0 - (SystemResources.CpuUsagePercent / 100.0 + SystemResources.MemoryUsagePercent / 100.0) / 2.0;

            return (cacheScore + indexScore + poolScore + systemScore) / 4.0;
        }
    }

    public override string ToString()
    {
        return $"Integrated Performance [{Timestamp:HH:mm:ss}]: " +
               $"Overall Score={OverallPerformanceScore:P1}, " +
               $"Cache Hit={CacheStatistics.AverageHitRatio:P1}, " +
               $"Index Hit={IndexStatistics.AverageHitRatio:P1}, " +
               $"Pool Util={PoolStatistics.AverageUtilization:P1}, " +
               $"CPU={SystemResources.CpuUsagePercent:F1}%, " +
               $"Memory={SystemResources.MemoryUsagePercent:F1}%";
    }
}

/// <summary>
/// Performance health check result.
/// </summary>
public class PerformanceHealthCheckResult
{
    public PerformanceHealthCheckResult(
        bool isHealthy,
        IEnumerable<HealthCheckItem> items,
        DateTime timestamp)
    {
        IsHealthy = isHealthy;
        Items = items?.ToList() ?? new List<HealthCheckItem>();
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets whether all systems are healthy.
    /// </summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// Gets the individual health check items.
    /// </summary>
    public IReadOnlyList<HealthCheckItem> Items { get; }

    /// <summary>
    /// Gets the health check timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the number of healthy items.
    /// </summary>
    public int HealthyCount => Items.Count(i => i.IsHealthy);

    /// <summary>
    /// Gets the number of unhealthy items.
    /// </summary>
    public int UnhealthyCount => Items.Count(i => !i.IsHealthy);

    public override string ToString()
    {
        return $"Performance Health Check [{Timestamp:HH:mm:ss}]: " +
               $"Overall={IsHealthy}, Healthy={HealthyCount}, Unhealthy={UnhealthyCount}";
    }
}

/// <summary>
/// Individual health check item.
/// </summary>
public class HealthCheckItem
{
    public HealthCheckItem(string name, bool isHealthy, string? message = null, Exception? exception = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsHealthy = isHealthy;
        Message = message;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the health check item name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether the item is healthy.
    /// </summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// Gets the health check message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets any exception that occurred.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the health check timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = IsHealthy ? "Healthy" : "Unhealthy";
        var details = Message ?? Exception?.Message ?? "";
        return $"{Name}: {status}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}");
    }
}

/// <summary>
/// Access pattern for storage optimization.
/// </summary>
public class AccessPattern
{
    public AccessPattern(
        string entityType,
        string accessType,
        int frequency,
        TimeSpan averageLatency,
        long dataSize,
        DateTime lastAccessed)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        AccessType = accessType ?? throw new ArgumentNullException(nameof(accessType));
        Frequency = frequency;
        AverageLatency = averageLatency;
        DataSize = dataSize;
        LastAccessed = lastAccessed;
    }

    /// <summary>
    /// Gets the entity type.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the access type (read, write, query, etc.).
    /// </summary>
    public string AccessType { get; }

    /// <summary>
    /// Gets the access frequency.
    /// </summary>
    public int Frequency { get; }

    /// <summary>
    /// Gets the average latency.
    /// </summary>
    public TimeSpan AverageLatency { get; }

    /// <summary>
    /// Gets the data size.
    /// </summary>
    public long DataSize { get; }

    /// <summary>
    /// Gets when the entity was last accessed.
    /// </summary>
    public DateTime LastAccessed { get; }

    /// <summary>
    /// Gets the access priority score.
    /// </summary>
    public double PriorityScore
    {
        get
        {
            var frequencyScore = Math.Log10(Math.Max(1, Frequency));
            var recencyScore = 1.0 / Math.Max(1, (DateTime.UtcNow - LastAccessed).TotalHours);
            var sizeScore = 1.0 / Math.Max(1, Math.Log10(Math.Max(1, DataSize)));
            
            return (frequencyScore + recencyScore + sizeScore) / 3.0;
        }
    }

    public override string ToString()
    {
        return $"AccessPattern[{EntityType}:{AccessType}]: " +
               $"Freq={Frequency}, Latency={AverageLatency.TotalMilliseconds:F1}ms, " +
               $"Size={DataSize:N0} bytes, Priority={PriorityScore:F2}";
    }
}

/// <summary>
/// Event arguments for integration status changes.
/// </summary>
public class IntegrationStatusChangedEventArgs : EventArgs
{
    public IntegrationStatusChangedEventArgs(IntegrationStatus oldStatus, IntegrationStatus newStatus)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the old status.
    /// </summary>
    public IntegrationStatus OldStatus { get; }

    /// <summary>
    /// Gets the new status.
    /// </summary>
    public IntegrationStatus NewStatus { get; }

    /// <summary>
    /// Gets when the status changed.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Integration status changed: {OldStatus} -> {NewStatus} @ {Timestamp:HH:mm:ss}";
    }
}
