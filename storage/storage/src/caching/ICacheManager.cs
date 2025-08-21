using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for cache management operations.
/// </summary>
public interface ICacheManager : IDisposable
{
    /// <summary>
    /// Gets the cache manager name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the cache manager is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheManagerStatistics Statistics { get; }

    /// <summary>
    /// Initializes the cache manager.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the cache manager.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the cache manager.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if removed, false if not found</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached values.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms the cache with specified keys.
    /// </summary>
    /// <param name="keys">Keys to warm</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WarmCacheAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache health information.
    /// </summary>
    /// <returns>Cache health status</returns>
    CacheHealthStatus GetHealthStatus();
}

/// <summary>
/// Cache manager statistics.
/// </summary>
public class CacheManagerStatistics
{
    public CacheManagerStatistics(
        long totalRequests,
        long cacheHits,
        long cacheMisses,
        double averageResponseTimeMs,
        long totalMemoryUsage,
        int activeCaches)
    {
        TotalRequests = totalRequests;
        CacheHits = cacheHits;
        CacheMisses = cacheMisses;
        AverageResponseTimeMs = averageResponseTimeMs;
        TotalMemoryUsage = totalMemoryUsage;
        ActiveCaches = activeCaches;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the total number of cache requests.
    /// </summary>
    public long TotalRequests { get; }

    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public long CacheHits { get; }

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public long CacheMisses { get; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0.0;

    /// <summary>
    /// Gets the average hit ratio (alias for HitRatio for compatibility).
    /// </summary>
    public double AverageHitRatio => HitRatio;

    /// <summary>
    /// Gets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTimeMs { get; }

    /// <summary>
    /// Gets the total memory usage in bytes.
    /// </summary>
    public long TotalMemoryUsage { get; }

    /// <summary>
    /// Gets the number of active caches.
    /// </summary>
    public int ActiveCaches { get; }

    /// <summary>
    /// Gets the timestamp when statistics were captured.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"CacheManagerStatistics[Requests={TotalRequests:N0}, " +
               $"HitRatio={HitRatio:P2}, AvgResponseTime={AverageResponseTimeMs:F1}ms, " +
               $"MemoryUsage={TotalMemoryUsage:N0} bytes, ActiveCaches={ActiveCaches}]";
    }
}

/// <summary>
/// Cache health status.
/// </summary>
public class CacheHealthStatus
{
    public CacheHealthStatus(bool isHealthy, string status, IEnumerable<string> issues)
    {
        IsHealthy = isHealthy;
        Status = status ?? throw new ArgumentNullException(nameof(status));
        Issues = issues?.ToList() ?? new List<string>();
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets whether the cache is healthy.
    /// </summary>
    public bool IsHealthy { get; }

    /// <summary>
    /// Gets the health status description.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets any health issues.
    /// </summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>
    /// Gets the timestamp when health was checked.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var healthStatus = IsHealthy ? "HEALTHY" : "UNHEALTHY";
        return $"CacheHealth[{healthStatus}]: {Status} " +
               (Issues.Count > 0 ? $"({Issues.Count} issues)" : "");
    }
}
