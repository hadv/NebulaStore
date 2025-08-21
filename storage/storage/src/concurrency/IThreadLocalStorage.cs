using System;
using System.Collections.Generic;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Interface for high-performance thread-local storage with automatic cleanup and monitoring.
/// </summary>
/// <typeparam name="T">Type of data stored per thread</typeparam>
public interface IThreadLocalStorage<T> : IDisposable
{
    /// <summary>
    /// Gets the name of this thread-local storage.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current thread's value.
    /// </summary>
    T Value { get; set; }

    /// <summary>
    /// Gets whether the current thread has a value.
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// Gets the number of threads that have values.
    /// </summary>
    int ThreadCount { get; }

    /// <summary>
    /// Gets thread-local storage statistics.
    /// </summary>
    IThreadLocalStatistics Statistics { get; }

    /// <summary>
    /// Gets the current thread's value or creates it using the factory.
    /// </summary>
    /// <param name="valueFactory">Factory to create value if not exists</param>
    /// <returns>Thread-local value</returns>
    T GetOrCreate(Func<T> valueFactory);

    /// <summary>
    /// Tries to get the current thread's value.
    /// </summary>
    /// <param name="value">The thread-local value if it exists</param>
    /// <returns>True if the thread has a value, false otherwise</returns>
    bool TryGetValue(out T value);

    /// <summary>
    /// Removes the current thread's value.
    /// </summary>
    /// <returns>True if a value was removed, false if no value existed</returns>
    bool RemoveValue();

    /// <summary>
    /// Gets all values from all threads.
    /// </summary>
    /// <returns>Collection of all thread-local values</returns>
    IEnumerable<T> GetAllValues();

    /// <summary>
    /// Clears all thread-local values.
    /// </summary>
    void Clear();

    /// <summary>
    /// Performs cleanup of values from terminated threads.
    /// </summary>
    /// <returns>Number of values cleaned up</returns>
    int CleanupDeadThreads();
}

/// <summary>
/// Interface for thread-local storage statistics.
/// </summary>
public interface IThreadLocalStatistics
{
    /// <summary>
    /// Gets the total number of value accesses.
    /// </summary>
    long TotalAccesses { get; }

    /// <summary>
    /// Gets the total number of value creations.
    /// </summary>
    long TotalCreations { get; }

    /// <summary>
    /// Gets the total number of value removals.
    /// </summary>
    long TotalRemovals { get; }

    /// <summary>
    /// Gets the current number of active threads.
    /// </summary>
    int ActiveThreads { get; }

    /// <summary>
    /// Gets the peak number of active threads.
    /// </summary>
    int PeakActiveThreads { get; }

    /// <summary>
    /// Gets the average value lifetime in milliseconds.
    /// </summary>
    double AverageValueLifetimeMs { get; }

    /// <summary>
    /// Gets the memory usage estimate in bytes.
    /// </summary>
    long EstimatedMemoryUsage { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Interface for work-stealing queue operations.
/// </summary>
/// <typeparam name="T">Type of work items</typeparam>
public interface IWorkStealingQueue<T> : IDisposable
{
    /// <summary>
    /// Gets the queue name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the approximate number of items in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets whether the queue is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets work-stealing statistics.
    /// </summary>
    IWorkStealingStatistics Statistics { get; }

    /// <summary>
    /// Pushes a work item to the local end of the queue (LIFO for owner thread).
    /// </summary>
    /// <param name="item">Work item to push</param>
    void LocalPush(T item);

    /// <summary>
    /// Attempts to pop a work item from the local end (LIFO for owner thread).
    /// </summary>
    /// <param name="result">The popped work item if successful</param>
    /// <returns>True if an item was popped, false if the queue was empty</returns>
    bool LocalTryPop(out T result);

    /// <summary>
    /// Attempts to steal a work item from the global end (FIFO for stealing threads).
    /// </summary>
    /// <param name="result">The stolen work item if successful</param>
    /// <returns>True if an item was stolen, false if the queue was empty</returns>
    bool TrySteal(out T result);

    /// <summary>
    /// Gets the current load factor (0.0 to 1.0).
    /// </summary>
    double LoadFactor { get; }

    /// <summary>
    /// Gets whether this queue is a good candidate for stealing.
    /// </summary>
    bool IsStealingCandidate { get; }
}

/// <summary>
/// Interface for work-stealing statistics.
/// </summary>
public interface IWorkStealingStatistics
{
    /// <summary>
    /// Gets the total number of local pushes.
    /// </summary>
    long TotalLocalPushes { get; }

    /// <summary>
    /// Gets the total number of local pops.
    /// </summary>
    long TotalLocalPops { get; }

    /// <summary>
    /// Gets the total number of steal attempts.
    /// </summary>
    long TotalStealAttempts { get; }

    /// <summary>
    /// Gets the total number of successful steals.
    /// </summary>
    long TotalSuccessfulSteals { get; }

    /// <summary>
    /// Gets the steal success ratio.
    /// </summary>
    double StealSuccessRatio { get; }

    /// <summary>
    /// Gets the average queue utilization.
    /// </summary>
    double AverageUtilization { get; }

    /// <summary>
    /// Gets the peak queue size.
    /// </summary>
    int PeakQueueSize { get; }

    /// <summary>
    /// Gets the contention level.
    /// </summary>
    double ContentionLevel { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Configuration for thread-local storage.
/// </summary>
public class ThreadLocalConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable automatic cleanup of dead threads.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Gets or sets the cleanup interval.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track value lifetimes.
    /// </summary>
    public bool TrackValueLifetimes { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of threads to track.
    /// </summary>
    public int MaxTrackedThreads { get; set; } = 1000;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return CleanupInterval > TimeSpan.Zero &&
               MaxTrackedThreads > 0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public ThreadLocalConfiguration Clone()
    {
        return new ThreadLocalConfiguration
        {
            EnableAutoCleanup = EnableAutoCleanup,
            CleanupInterval = CleanupInterval,
            EnableStatistics = EnableStatistics,
            TrackValueLifetimes = TrackValueLifetimes,
            MaxTrackedThreads = MaxTrackedThreads
        };
    }

    public override string ToString()
    {
        return $"ThreadLocalConfiguration[AutoCleanup={EnableAutoCleanup}, " +
               $"CleanupInterval={CleanupInterval}, Statistics={EnableStatistics}, " +
               $"TrackLifetimes={TrackValueLifetimes}, MaxThreads={MaxTrackedThreads}]";
    }
}

/// <summary>
/// Configuration for work-stealing queues.
/// </summary>
public class WorkStealingConfiguration
{
    /// <summary>
    /// Gets or sets the initial queue capacity.
    /// </summary>
    public int InitialCapacity { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum queue capacity.
    /// </summary>
    public int MaxCapacity { get; set; } = 65536;

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the load factor threshold for stealing.
    /// </summary>
    public double StealingThreshold { get; set; } = 0.5; // 50%

    /// <summary>
    /// Gets or sets whether to enable dynamic resizing.
    /// </summary>
    public bool EnableDynamicResizing { get; set; } = true;

    /// <summary>
    /// Gets or sets the resize factor.
    /// </summary>
    public double ResizeFactor { get; set; } = 2.0;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return InitialCapacity > 0 &&
               MaxCapacity >= InitialCapacity &&
               StealingThreshold > 0 && StealingThreshold <= 1.0 &&
               ResizeFactor > 1.0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public WorkStealingConfiguration Clone()
    {
        return new WorkStealingConfiguration
        {
            InitialCapacity = InitialCapacity,
            MaxCapacity = MaxCapacity,
            EnableStatistics = EnableStatistics,
            StealingThreshold = StealingThreshold,
            EnableDynamicResizing = EnableDynamicResizing,
            ResizeFactor = ResizeFactor
        };
    }

    public override string ToString()
    {
        return $"WorkStealingConfiguration[InitialCapacity={InitialCapacity}, " +
               $"MaxCapacity={MaxCapacity}, StealingThreshold={StealingThreshold:P1}, " +
               $"DynamicResizing={EnableDynamicResizing}, Statistics={EnableStatistics}]";
    }
}
