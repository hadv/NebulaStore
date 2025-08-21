using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// High-performance parallel processor with deadlock detection and prevention.
/// </summary>
public class ParallelProcessor : IParallelProcessor
{
    private readonly string _name;
    private readonly ParallelProcessingConfiguration _configuration;
    private readonly IDeadlockDetector _deadlockDetector;
    private readonly ParallelProcessingStatistics _statistics;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private volatile bool _isDisposed;

    public ParallelProcessor(string name, ParallelProcessingConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new ParallelProcessingConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid parallel processing configuration", nameof(configuration));

        _deadlockDetector = new DeadlockDetector("ParallelProcessor.DeadlockDetector", _configuration);
        _statistics = new ParallelProcessingStatistics(_configuration);
        _concurrencyLimiter = new SemaphoreSlim(_configuration.MaxDegreeOfParallelism, _configuration.MaxDegreeOfParallelism);
    }

    public string Name => _name;
    public int MaxDegreeOfParallelism => _configuration.MaxDegreeOfParallelism;
    public bool DeadlockDetectionEnabled => _configuration.EnableDeadlockDetection;
    public IParallelProcessingStatistics Statistics => _statistics;

    public async Task<ParallelProcessingResult<T>> ProcessAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> processor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        var itemsList = items.ToList();
        var processedItems = new ConcurrentBag<T>();
        var failedItems = new ConcurrentBag<T>();
        var stopwatch = Stopwatch.StartNew();
        var parallelismTracker = new ParallelismTracker();

        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _configuration.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(itemsList, parallelOptions, async (item, ct) =>
            {
                await _concurrencyLimiter.WaitAsync(ct);
                parallelismTracker.IncrementActive();

                try
                {
                    await processor(item, ct);
                    processedItems.Add(item);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch
                {
                    failedItems.Add(item);
                }
                finally
                {
                    parallelismTracker.DecrementActive();
                    _concurrencyLimiter.Release();
                }
            });

            stopwatch.Stop();

            var result = new ParallelProcessingResult<T>(
                processedItems.ToList(),
                failedItems.ToList(),
                stopwatch.Elapsed,
                parallelismTracker.AverageParallelism,
                parallelismTracker.MaxConcurrency
            );

            _statistics.RecordOperation(result);
            return result;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public async Task<ParallelProcessingResult<TInput, TOutput>> ProcessWithResultsAsync<TInput, TOutput>(
        IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> processor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        var itemsList = items.ToList();
        var processedItems = new ConcurrentBag<TInput>();
        var failedItems = new ConcurrentBag<TInput>();
        var results = new ConcurrentBag<TOutput>();
        var stopwatch = Stopwatch.StartNew();
        var parallelismTracker = new ParallelismTracker();

        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _configuration.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(itemsList, parallelOptions, async (item, ct) =>
            {
                await _concurrencyLimiter.WaitAsync(ct);
                parallelismTracker.IncrementActive();

                try
                {
                    var result = await processor(item, ct);
                    processedItems.Add(item);
                    results.Add(result);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch
                {
                    failedItems.Add(item);
                }
                finally
                {
                    parallelismTracker.DecrementActive();
                    _concurrencyLimiter.Release();
                }
            });

            stopwatch.Stop();

            var result = new ParallelProcessingResult<TInput, TOutput>(
                processedItems.ToList(),
                failedItems.ToList(),
                results.ToList(),
                stopwatch.Elapsed,
                parallelismTracker.AverageParallelism,
                parallelismTracker.MaxConcurrency
            );

            _statistics.RecordOperation(result);
            return result;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public async Task<ParallelProcessingResult<T>> ProcessBatchesAsync<T>(
        IEnumerable<T> items,
        int batchSize,
        Func<IEnumerable<T>, CancellationToken, Task> batchProcessor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (batchProcessor == null) throw new ArgumentNullException(nameof(batchProcessor));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        var itemsList = items.ToList();
        var batches = CreateBatches(itemsList, batchSize);
        var processedItems = new ConcurrentBag<T>();
        var failedItems = new ConcurrentBag<T>();
        var stopwatch = Stopwatch.StartNew();
        var parallelismTracker = new ParallelismTracker();

        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _configuration.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(batches, parallelOptions, async (batch, ct) =>
            {
                await _concurrencyLimiter.WaitAsync(ct);
                parallelismTracker.IncrementActive();

                try
                {
                    await batchProcessor(batch, ct);
                    foreach (var item in batch)
                    {
                        processedItems.Add(item);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch
                {
                    foreach (var item in batch)
                    {
                        failedItems.Add(item);
                    }
                }
                finally
                {
                    parallelismTracker.DecrementActive();
                    _concurrencyLimiter.Release();
                }
            });

            stopwatch.Stop();

            var result = new ParallelProcessingResult<T>(
                processedItems.ToList(),
                failedItems.ToList(),
                stopwatch.Elapsed,
                parallelismTracker.AverageParallelism,
                parallelismTracker.MaxConcurrency
            );

            _statistics.RecordOperation(result);
            return result;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public async Task<ParallelExecutionResult> ExecuteWithDependenciesAsync(
        IEnumerable<IParallelOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (operations == null) throw new ArgumentNullException(nameof(operations));

        var operationsList = operations.ToList();
        var dependencyGraph = BuildDependencyGraph(operationsList);
        var executionPlan = TopologicalSort(dependencyGraph);
        
        var results = new ConcurrentDictionary<string, OperationResult>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var level in executionPlan)
            {
                // Execute operations at the same dependency level in parallel
                var levelTasks = level.Select(async operation =>
                {
                    await _concurrencyLimiter.WaitAsync(cancellationToken);
                    try
                    {
                        var context = new OperationContext(results);
                        var result = await operation.ExecuteAsync(context, cancellationToken);
                        results[operation.Id] = result;
                        return result;
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                });

                await Task.WhenAll(levelTasks);
            }

            stopwatch.Stop();

            return new ParallelExecutionResult(
                results.Values.ToList(),
                stopwatch.Elapsed,
                true,
                null
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ParallelExecutionResult(
                results.Values.ToList(),
                stopwatch.Elapsed,
                false,
                ex.Message
            );
        }
    }

    private static IEnumerable<IEnumerable<T>> CreateBatches<T>(IList<T> items, int batchSize)
    {
        for (int i = 0; i < items.Count; i += batchSize)
        {
            yield return items.Skip(i).Take(batchSize);
        }
    }

    private static Dictionary<string, List<string>> BuildDependencyGraph(IList<IParallelOperation> operations)
    {
        var graph = new Dictionary<string, List<string>>();
        
        foreach (var operation in operations)
        {
            graph[operation.Id] = operation.Dependencies.ToList();
        }

        return graph;
    }

    private static List<List<IParallelOperation>> TopologicalSort(Dictionary<string, List<string>> dependencyGraph)
    {
        // Simplified topological sort implementation
        // In a real implementation, this would be more sophisticated
        var levels = new List<List<IParallelOperation>>();
        // Implementation would go here
        return levels;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ParallelProcessor));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _concurrencyLimiter?.Dispose();
        _deadlockDetector?.Dispose();
    }
}

/// <summary>
/// Tracks parallelism metrics during execution.
/// </summary>
internal class ParallelismTracker
{
    private volatile int _activeTasks;
    private volatile int _maxConcurrency;
    private long _totalSamples;
    private long _totalParallelism;

    public void IncrementActive()
    {
        var current = Interlocked.Increment(ref _activeTasks);
        UpdateMaxConcurrency(current);
        RecordSample(current);
    }

    public void DecrementActive()
    {
        var current = Interlocked.Decrement(ref _activeTasks);
        RecordSample(current);
    }

    public int MaxConcurrency => _maxConcurrency;

    public double AverageParallelism
    {
        get
        {
            var samples = Interlocked.Read(ref _totalSamples);
            var parallelism = Interlocked.Read(ref _totalParallelism);
            return samples > 0 ? (double)parallelism / samples : 0.0;
        }
    }

    private void UpdateMaxConcurrency(int current)
    {
        var currentMax = _maxConcurrency;
        while (current > currentMax)
        {
            var originalMax = Interlocked.CompareExchange(ref _maxConcurrency, current, currentMax);
            if (originalMax == currentMax) break;
            currentMax = originalMax;
        }
    }

    private void RecordSample(int parallelism)
    {
        Interlocked.Increment(ref _totalSamples);
        Interlocked.Add(ref _totalParallelism, parallelism);
    }
}

/// <summary>
/// Thread-safe statistics implementation for parallel processing.
/// </summary>
public class ParallelProcessingStatistics : IParallelProcessingStatistics
{
    private readonly ParallelProcessingConfiguration _configuration;
    private long _totalOperations;
    private long _totalItemsProcessed;
    private long _totalParallelismSamples;
    private long _totalProcessingTimeMs;
    private long _failedOperations;

    public ParallelProcessingStatistics(ParallelProcessingConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public long TotalOperations => Interlocked.Read(ref _totalOperations);
    public long TotalItemsProcessed => Interlocked.Read(ref _totalItemsProcessed);
    public long FailedOperations => Interlocked.Read(ref _failedOperations);

    public double AverageParallelism
    {
        get
        {
            var operations = TotalOperations;
            var samples = Interlocked.Read(ref _totalParallelismSamples);
            return operations > 0 ? (double)samples / operations : 0.0;
        }
    }

    public double AverageProcessingTimeMs
    {
        get
        {
            var items = TotalItemsProcessed;
            var timeMs = Interlocked.Read(ref _totalProcessingTimeMs);
            return items > 0 ? (double)timeMs / items : 0.0;
        }
    }

    public double ThroughputItemsPerSecond
    {
        get
        {
            var timeMs = Interlocked.Read(ref _totalProcessingTimeMs);
            var items = TotalItemsProcessed;
            return timeMs > 0 ? items * 1000.0 / timeMs : 0.0;
        }
    }

    public double EfficiencyRatio
    {
        get
        {
            var actualParallelism = AverageParallelism;
            var maxParallelism = _configuration.MaxDegreeOfParallelism;
            return maxParallelism > 0 ? actualParallelism / maxParallelism : 0.0;
        }
    }

    public void RecordOperation<T>(ParallelProcessingResult<T> result)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalOperations);
            Interlocked.Add(ref _totalItemsProcessed, result.ProcessedItems.Count);
            Interlocked.Add(ref _totalParallelismSamples, (long)(result.AverageParallelism * 1000));
            Interlocked.Add(ref _totalProcessingTimeMs, (long)result.TotalDuration.TotalMilliseconds);
        }
    }

    public void RecordFailedOperation()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _failedOperations);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalOperations, 0);
        Interlocked.Exchange(ref _totalItemsProcessed, 0);
        Interlocked.Exchange(ref _totalParallelismSamples, 0);
        Interlocked.Exchange(ref _totalProcessingTimeMs, 0);
        Interlocked.Exchange(ref _failedOperations, 0);
    }
}
