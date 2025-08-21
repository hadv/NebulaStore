using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Interface for cache warming strategies.
/// </summary>
public interface ICacheWarmingStrategy<TKey, TValue>
{
    /// <summary>
    /// Gets the name of this warming strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines which data should be preloaded into the cache.
    /// </summary>
    /// <param name="dataSource">Source of data for warming</param>
    /// <param name="maxItems">Maximum number of items to warm</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Data to preload into cache</returns>
    Task<IEnumerable<KeyValuePair<TKey, TValue>>> SelectWarmupDataAsync(
        ICacheWarmingDataSource<TKey, TValue> dataSource,
        int maxItems,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for cache warming data sources.
/// </summary>
public interface ICacheWarmingDataSource<TKey, TValue>
{
    /// <summary>
    /// Gets the most frequently accessed items.
    /// </summary>
    /// <param name="count">Number of items to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Most frequently accessed items</returns>
    Task<IEnumerable<KeyValuePair<TKey, TValue>>> GetMostAccessedAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently accessed items.
    /// </summary>
    /// <param name="count">Number of items to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Most recently accessed items</returns>
    Task<IEnumerable<KeyValuePair<TKey, TValue>>> GetMostRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items based on custom criteria.
    /// </summary>
    /// <param name="selector">Custom selection function</param>
    /// <param name="count">Number of items to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected items</returns>
    Task<IEnumerable<KeyValuePair<TKey, TValue>>> GetCustomAsync(
        Func<KeyValuePair<TKey, TValue>, bool> selector,
        int count,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Most frequently accessed warming strategy.
/// </summary>
public class MostAccessedWarmingStrategy<TKey, TValue> : ICacheWarmingStrategy<TKey, TValue>
{
    public string Name => "MostAccessed";

    public async Task<IEnumerable<KeyValuePair<TKey, TValue>>> SelectWarmupDataAsync(
        ICacheWarmingDataSource<TKey, TValue> dataSource,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return await dataSource.GetMostAccessedAsync(maxItems, cancellationToken);
    }
}

/// <summary>
/// Most recently accessed warming strategy.
/// </summary>
public class MostRecentWarmingStrategy<TKey, TValue> : ICacheWarmingStrategy<TKey, TValue>
{
    public string Name => "MostRecent";

    public async Task<IEnumerable<KeyValuePair<TKey, TValue>>> SelectWarmupDataAsync(
        ICacheWarmingDataSource<TKey, TValue> dataSource,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return await dataSource.GetMostRecentAsync(maxItems, cancellationToken);
    }
}

/// <summary>
/// Custom warming strategy with user-defined selection logic.
/// </summary>
public class CustomWarmingStrategy<TKey, TValue> : ICacheWarmingStrategy<TKey, TValue>
{
    private readonly Func<KeyValuePair<TKey, TValue>, bool> _selector;

    public CustomWarmingStrategy(Func<KeyValuePair<TKey, TValue>, bool> selector, string? name = null)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        Name = name ?? "Custom";
    }

    public string Name { get; }

    public async Task<IEnumerable<KeyValuePair<TKey, TValue>>> SelectWarmupDataAsync(
        ICacheWarmingDataSource<TKey, TValue> dataSource,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return await dataSource.GetCustomAsync(_selector, maxItems, cancellationToken);
    }
}

/// <summary>
/// Cache warming manager that coordinates warming operations.
/// </summary>
public class CacheWarmingManager<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly ICache<TKey, TValue> _cache;
    private readonly ICacheWarmingStrategy<TKey, TValue> _strategy;
    private readonly ICacheWarmingDataSource<TKey, TValue> _dataSource;
    private readonly CacheWarmingConfiguration _configuration;
    private readonly Timer? _warmingTimer;
    private volatile bool _isWarming;
    private volatile bool _isDisposed;

    public CacheWarmingManager(
        ICache<TKey, TValue> cache,
        ICacheWarmingStrategy<TKey, TValue> strategy,
        ICacheWarmingDataSource<TKey, TValue> dataSource,
        CacheWarmingConfiguration? configuration = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _configuration = configuration ?? new CacheWarmingConfiguration();

        // Set up periodic warming if enabled
        if (_configuration.EnablePeriodicWarming && _configuration.WarmingInterval > TimeSpan.Zero)
        {
            _warmingTimer = new Timer(PerformPeriodicWarming, null, _configuration.InitialDelay, _configuration.WarmingInterval);
        }
    }

    /// <summary>
    /// Gets whether a warming operation is currently in progress.
    /// </summary>
    public bool IsWarming => _isWarming;

    /// <summary>
    /// Gets the warming strategy name.
    /// </summary>
    public string StrategyName => _strategy.Name;

    /// <summary>
    /// Performs cache warming operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items warmed</returns>
    public async Task<int> WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isWarming)
        {
            return 0; // Already warming
        }

        _isWarming = true;
        try
        {
            var startTime = DateTime.UtcNow;
            var maxItems = _configuration.MaxWarmupItems;
            var timeoutCts = new CancellationTokenSource(_configuration.MaxWarmingTime);
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var warmupData = await _strategy.SelectWarmupDataAsync(_dataSource, maxItems, combinedCts.Token);
            var warmupList = warmupData.ToList();

            if (warmupList.Count == 0)
            {
                return 0;
            }

            // Warm cache in batches to avoid overwhelming the system
            var batchSize = _configuration.WarmingBatchSize;
            var warmedCount = 0;

            for (int i = 0; i < warmupList.Count; i += batchSize)
            {
                combinedCts.Token.ThrowIfCancellationRequested();

                var batch = warmupList.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(async kvp =>
                {
                    try
                    {
                        await _cache.PutAsync(kvp.Key, kvp.Value, 
                            priority: CacheEntryPriority.High, 
                            cancellationToken: combinedCts.Token);
                        return 1;
                    }
                    catch
                    {
                        return 0; // Ignore individual item failures
                    }
                });

                var batchResults = await Task.WhenAll(batchTasks);
                warmedCount += batchResults.Sum();

                // Add delay between batches if configured
                if (_configuration.BatchDelay > TimeSpan.Zero && i + batchSize < warmupList.Count)
                {
                    await Task.Delay(_configuration.BatchDelay, combinedCts.Token);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            OnWarmingCompleted(new CacheWarmingCompletedEventArgs(warmedCount, warmupList.Count, duration));

            return warmedCount;
        }
        catch (OperationCanceledException)
        {
            OnWarmingCancelled(new CacheWarmingCancelledEventArgs("Warming operation was cancelled"));
            return 0;
        }
        catch (Exception ex)
        {
            OnWarmingFailed(new CacheWarmingFailedEventArgs(ex));
            return 0;
        }
        finally
        {
            _isWarming = false;
        }
    }

    /// <summary>
    /// Event raised when cache warming completes successfully.
    /// </summary>
    public event EventHandler<CacheWarmingCompletedEventArgs>? WarmingCompleted;

    /// <summary>
    /// Event raised when cache warming is cancelled.
    /// </summary>
    public event EventHandler<CacheWarmingCancelledEventArgs>? WarmingCancelled;

    /// <summary>
    /// Event raised when cache warming fails.
    /// </summary>
    public event EventHandler<CacheWarmingFailedEventArgs>? WarmingFailed;

    private async void PerformPeriodicWarming(object? state)
    {
        try
        {
            await WarmCacheAsync();
        }
        catch
        {
            // Ignore periodic warming errors
        }
    }

    private void OnWarmingCompleted(CacheWarmingCompletedEventArgs args)
    {
        WarmingCompleted?.Invoke(this, args);
    }

    private void OnWarmingCancelled(CacheWarmingCancelledEventArgs args)
    {
        WarmingCancelled?.Invoke(this, args);
    }

    private void OnWarmingFailed(CacheWarmingFailedEventArgs args)
    {
        WarmingFailed?.Invoke(this, args);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CacheWarmingManager<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _warmingTimer?.Dispose();
    }
}

/// <summary>
/// Configuration for cache warming operations.
/// </summary>
public class CacheWarmingConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of items to warm up.
    /// </summary>
    public int MaxWarmupItems { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum time to spend on warming.
    /// </summary>
    public TimeSpan MaxWarmingTime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the batch size for warming operations.
    /// </summary>
    public int WarmingBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the delay between warming batches.
    /// </summary>
    public TimeSpan BatchDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Gets or sets whether to enable periodic warming.
    /// </summary>
    public bool EnablePeriodicWarming { get; set; } = false;

    /// <summary>
    /// Gets or sets the interval for periodic warming.
    /// </summary>
    public TimeSpan WarmingInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the initial delay before starting periodic warming.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Event arguments for cache warming completion.
/// </summary>
public class CacheWarmingCompletedEventArgs : EventArgs
{
    public CacheWarmingCompletedEventArgs(int warmedCount, int totalCount, TimeSpan duration)
    {
        WarmedCount = warmedCount;
        TotalCount = totalCount;
        Duration = duration;
    }

    public int WarmedCount { get; }
    public int TotalCount { get; }
    public TimeSpan Duration { get; }
    public double SuccessRate => TotalCount > 0 ? (double)WarmedCount / TotalCount : 0.0;
}

/// <summary>
/// Event arguments for cache warming cancellation.
/// </summary>
public class CacheWarmingCancelledEventArgs : EventArgs
{
    public CacheWarmingCancelledEventArgs(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}

/// <summary>
/// Event arguments for cache warming failure.
/// </summary>
public class CacheWarmingFailedEventArgs : EventArgs
{
    public CacheWarmingFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}
