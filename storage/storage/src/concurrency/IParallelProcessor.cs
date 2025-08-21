using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Interface for high-performance parallel processing with deadlock prevention.
/// </summary>
public interface IParallelProcessor : IDisposable
{
    /// <summary>
    /// Gets the processor name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the maximum degree of parallelism.
    /// </summary>
    int MaxDegreeOfParallelism { get; }

    /// <summary>
    /// Gets whether deadlock detection is enabled.
    /// </summary>
    bool DeadlockDetectionEnabled { get; }

    /// <summary>
    /// Gets parallel processing statistics.
    /// </summary>
    IParallelProcessingStatistics Statistics { get; }

    /// <summary>
    /// Processes items in parallel with optimal load balancing.
    /// </summary>
    /// <typeparam name="T">Type of items to process</typeparam>
    /// <param name="items">Items to process</param>
    /// <param name="processor">Processing function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing results</returns>
    Task<ParallelProcessingResult<T>> ProcessAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> processor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes items in parallel with result collection.
    /// </summary>
    /// <typeparam name="TInput">Type of input items</typeparam>
    /// <typeparam name="TOutput">Type of output results</typeparam>
    /// <param name="items">Items to process</param>
    /// <param name="processor">Processing function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing results</returns>
    Task<ParallelProcessingResult<TInput, TOutput>> ProcessWithResultsAsync<TInput, TOutput>(
        IEnumerable<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> processor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes items in batches with parallel execution.
    /// </summary>
    /// <typeparam name="T">Type of items to process</typeparam>
    /// <param name="items">Items to process</param>
    /// <param name="batchSize">Size of each batch</param>
    /// <param name="batchProcessor">Batch processing function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing results</returns>
    Task<ParallelProcessingResult<T>> ProcessBatchesAsync<T>(
        IEnumerable<T> items,
        int batchSize,
        Func<IEnumerable<T>, CancellationToken, Task> batchProcessor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple operations in parallel with dependency management.
    /// </summary>
    /// <param name="operations">Operations to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution results</returns>
    Task<ParallelExecutionResult> ExecuteWithDependenciesAsync(
        IEnumerable<IParallelOperation> operations,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for deadlock detection and prevention.
/// </summary>
public interface IDeadlockDetector : IDisposable
{
    /// <summary>
    /// Gets the detector name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether detection is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets deadlock detection statistics.
    /// </summary>
    IDeadlockDetectionStatistics Statistics { get; }

    /// <summary>
    /// Registers a resource for deadlock monitoring.
    /// </summary>
    /// <param name="resourceId">Resource identifier</param>
    /// <param name="resourceType">Type of resource</param>
    void RegisterResource(string resourceId, string resourceType);

    /// <summary>
    /// Records a resource acquisition attempt.
    /// </summary>
    /// <param name="threadId">Thread identifier</param>
    /// <param name="resourceId">Resource identifier</param>
    /// <param name="timeout">Acquisition timeout</param>
    /// <returns>Acquisition token</returns>
    IResourceAcquisitionToken AcquireResource(int threadId, string resourceId, TimeSpan timeout);

    /// <summary>
    /// Records a resource release.
    /// </summary>
    /// <param name="token">Acquisition token</param>
    void ReleaseResource(IResourceAcquisitionToken token);

    /// <summary>
    /// Performs deadlock detection analysis.
    /// </summary>
    /// <returns>Detected deadlocks</returns>
    IEnumerable<Deadlock> DetectDeadlocks();

    /// <summary>
    /// Resolves a detected deadlock.
    /// </summary>
    /// <param name="deadlock">Deadlock to resolve</param>
    /// <returns>Resolution result</returns>
    DeadlockResolutionResult ResolveDeadlock(Deadlock deadlock);

    /// <summary>
    /// Event fired when a deadlock is detected.
    /// </summary>
    event EventHandler<DeadlockDetectedEventArgs> DeadlockDetected;
}

/// <summary>
/// Interface for parallel operations with dependencies.
/// </summary>
public interface IParallelOperation
{
    /// <summary>
    /// Gets the operation identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the operation name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the operation dependencies.
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// Gets the estimated execution time.
    /// </summary>
    TimeSpan EstimatedExecutionTime { get; }

    /// <summary>
    /// Executes the operation.
    /// </summary>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    Task<OperationResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for parallel processing statistics.
/// </summary>
public interface IParallelProcessingStatistics
{
    /// <summary>
    /// Gets the total number of parallel operations executed.
    /// </summary>
    long TotalOperations { get; }

    /// <summary>
    /// Gets the total number of items processed.
    /// </summary>
    long TotalItemsProcessed { get; }

    /// <summary>
    /// Gets the average parallelism achieved.
    /// </summary>
    double AverageParallelism { get; }

    /// <summary>
    /// Gets the average processing time per item in milliseconds.
    /// </summary>
    double AverageProcessingTimeMs { get; }

    /// <summary>
    /// Gets the throughput in items per second.
    /// </summary>
    double ThroughputItemsPerSecond { get; }

    /// <summary>
    /// Gets the efficiency ratio (actual vs theoretical maximum).
    /// </summary>
    double EfficiencyRatio { get; }

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    long FailedOperations { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Interface for deadlock detection statistics.
/// </summary>
public interface IDeadlockDetectionStatistics
{
    /// <summary>
    /// Gets the total number of resource acquisitions.
    /// </summary>
    long TotalAcquisitions { get; }

    /// <summary>
    /// Gets the total number of resource releases.
    /// </summary>
    long TotalReleases { get; }

    /// <summary>
    /// Gets the total number of deadlocks detected.
    /// </summary>
    long TotalDeadlocksDetected { get; }

    /// <summary>
    /// Gets the total number of deadlocks resolved.
    /// </summary>
    long TotalDeadlocksResolved { get; }

    /// <summary>
    /// Gets the average resource hold time in milliseconds.
    /// </summary>
    double AverageResourceHoldTimeMs { get; }

    /// <summary>
    /// Gets the current number of active resource acquisitions.
    /// </summary>
    int ActiveAcquisitions { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Parallel processing result.
/// </summary>
/// <typeparam name="T">Type of processed items</typeparam>
public class ParallelProcessingResult<T>
{
    public ParallelProcessingResult(
        IEnumerable<T> processedItems,
        IEnumerable<T> failedItems,
        TimeSpan totalDuration,
        double averageParallelism,
        int maxConcurrency)
    {
        ProcessedItems = processedItems?.ToList() ?? new List<T>();
        FailedItems = failedItems?.ToList() ?? new List<T>();
        TotalDuration = totalDuration;
        AverageParallelism = averageParallelism;
        MaxConcurrency = maxConcurrency;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the successfully processed items.
    /// </summary>
    public IReadOnlyList<T> ProcessedItems { get; }

    /// <summary>
    /// Gets the items that failed processing.
    /// </summary>
    public IReadOnlyList<T> FailedItems { get; }

    /// <summary>
    /// Gets the total processing duration.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the average parallelism achieved.
    /// </summary>
    public double AverageParallelism { get; }

    /// <summary>
    /// Gets the maximum concurrency reached.
    /// </summary>
    public int MaxConcurrency { get; }

    /// <summary>
    /// Gets the processing timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the total number of items.
    /// </summary>
    public int TotalItems => ProcessedItems.Count + FailedItems.Count;

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate => TotalItems > 0 ? (double)ProcessedItems.Count / TotalItems : 0.0;

    /// <summary>
    /// Gets the throughput in items per second.
    /// </summary>
    public double ThroughputItemsPerSecond => TotalDuration.TotalSeconds > 0 ? ProcessedItems.Count / TotalDuration.TotalSeconds : 0.0;

    public override string ToString()
    {
        return $"Parallel Processing Result: {ProcessedItems.Count}/{TotalItems} items " +
               $"({SuccessRate:P1} success), {ThroughputItemsPerSecond:F0} items/s, " +
               $"Avg Parallelism: {AverageParallelism:F1}, Duration: {TotalDuration.TotalMilliseconds:F0}ms";
    }
}

/// <summary>
/// Parallel processing result with output collection.
/// </summary>
/// <typeparam name="TInput">Type of input items</typeparam>
/// <typeparam name="TOutput">Type of output results</typeparam>
public class ParallelProcessingResult<TInput, TOutput> : ParallelProcessingResult<TInput>
{
    public ParallelProcessingResult(
        IEnumerable<TInput> processedItems,
        IEnumerable<TInput> failedItems,
        IEnumerable<TOutput> results,
        TimeSpan totalDuration,
        double averageParallelism,
        int maxConcurrency)
        : base(processedItems, failedItems, totalDuration, averageParallelism, maxConcurrency)
    {
        Results = results?.ToList() ?? new List<TOutput>();
    }

    /// <summary>
    /// Gets the processing results.
    /// </summary>
    public IReadOnlyList<TOutput> Results { get; }

    public override string ToString()
    {
        return base.ToString() + $", {Results.Count} results";
    }
}

/// <summary>
/// Configuration for parallel processing.
/// </summary>
public class ParallelProcessingConfiguration
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to enable work stealing.
    /// </summary>
    public bool EnableWorkStealing { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable deadlock detection.
    /// </summary>
    public bool EnableDeadlockDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets the deadlock detection interval.
    /// </summary>
    public TimeSpan DeadlockDetectionInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the default operation timeout.
    /// </summary>
    public TimeSpan DefaultOperationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return MaxDegreeOfParallelism > 0 &&
               DeadlockDetectionInterval > TimeSpan.Zero &&
               DefaultOperationTimeout > TimeSpan.Zero;
    }

    public override string ToString()
    {
        return $"ParallelProcessingConfiguration[MaxParallelism={MaxDegreeOfParallelism}, " +
               $"WorkStealing={EnableWorkStealing}, DeadlockDetection={EnableDeadlockDetection}, " +
               $"DetectionInterval={DeadlockDetectionInterval}, Statistics={EnableStatistics}]";
    }
}
