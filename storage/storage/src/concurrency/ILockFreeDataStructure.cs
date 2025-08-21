using System;
using System.Collections.Generic;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Interface for lock-free data structures with high-performance concurrent access.
/// </summary>
/// <typeparam name="T">Type of elements stored</typeparam>
public interface ILockFreeDataStructure<T>
{
    /// <summary>
    /// Gets the current count of elements (approximate for lock-free structures).
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets whether the data structure is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets performance statistics for the data structure.
    /// </summary>
    ILockFreeStatistics Statistics { get; }
}

/// <summary>
/// Interface for lock-free queue operations.
/// </summary>
/// <typeparam name="T">Type of elements in the queue</typeparam>
public interface ILockFreeQueue<T> : ILockFreeDataStructure<T>
{
    /// <summary>
    /// Enqueues an item to the queue.
    /// </summary>
    /// <param name="item">Item to enqueue</param>
    void Enqueue(T item);

    /// <summary>
    /// Attempts to dequeue an item from the queue.
    /// </summary>
    /// <param name="result">The dequeued item if successful</param>
    /// <returns>True if an item was dequeued, false if the queue was empty</returns>
    bool TryDequeue(out T result);

    /// <summary>
    /// Attempts to peek at the front item without removing it.
    /// </summary>
    /// <param name="result">The front item if successful</param>
    /// <returns>True if an item was peeked, false if the queue was empty</returns>
    bool TryPeek(out T result);
}

/// <summary>
/// Interface for lock-free stack operations.
/// </summary>
/// <typeparam name="T">Type of elements in the stack</typeparam>
public interface ILockFreeStack<T> : ILockFreeDataStructure<T>
{
    /// <summary>
    /// Pushes an item onto the stack.
    /// </summary>
    /// <param name="item">Item to push</param>
    void Push(T item);

    /// <summary>
    /// Attempts to pop an item from the stack.
    /// </summary>
    /// <param name="result">The popped item if successful</param>
    /// <returns>True if an item was popped, false if the stack was empty</returns>
    bool TryPop(out T result);

    /// <summary>
    /// Attempts to peek at the top item without removing it.
    /// </summary>
    /// <param name="result">The top item if successful</param>
    /// <returns>True if an item was peeked, false if the stack was empty</returns>
    bool TryPeek(out T result);
}

/// <summary>
/// Interface for lock-free dictionary operations.
/// </summary>
/// <typeparam name="TKey">Type of keys</typeparam>
/// <typeparam name="TValue">Type of values</typeparam>
public interface ILockFreeDictionary<TKey, TValue> : ILockFreeDataStructure<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    /// <summary>
    /// Gets all keys in the dictionary.
    /// </summary>
    IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets all values in the dictionary.
    /// </summary>
    IEnumerable<TValue> Values { get; }

    /// <summary>
    /// Attempts to get a value for the specified key.
    /// </summary>
    /// <param name="key">Key to look up</param>
    /// <param name="value">The value if found</param>
    /// <returns>True if the key was found, false otherwise</returns>
    bool TryGetValue(TKey key, out TValue value);

    /// <summary>
    /// Attempts to add a key-value pair.
    /// </summary>
    /// <param name="key">Key to add</param>
    /// <param name="value">Value to add</param>
    /// <returns>True if the pair was added, false if the key already exists</returns>
    bool TryAdd(TKey key, TValue value);

    /// <summary>
    /// Attempts to update a value for an existing key.
    /// </summary>
    /// <param name="key">Key to update</param>
    /// <param name="newValue">New value</param>
    /// <param name="comparisonValue">Expected current value</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue);

    /// <summary>
    /// Attempts to remove a key-value pair.
    /// </summary>
    /// <param name="key">Key to remove</param>
    /// <param name="value">The removed value if successful</param>
    /// <returns>True if the pair was removed, false if the key was not found</returns>
    bool TryRemove(TKey key, out TValue value);

    /// <summary>
    /// Adds or updates a key-value pair.
    /// </summary>
    /// <param name="key">Key to add or update</param>
    /// <param name="addValue">Value to add if key doesn't exist</param>
    /// <param name="updateValueFactory">Factory to create new value if key exists</param>
    /// <returns>The final value</returns>
    TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory);

    /// <summary>
    /// Gets or adds a value for the specified key.
    /// </summary>
    /// <param name="key">Key to get or add</param>
    /// <param name="valueFactory">Factory to create value if key doesn't exist</param>
    /// <returns>The value</returns>
    TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory);

    /// <summary>
    /// Checks if the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">Key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    bool ContainsKey(TKey key);
}

/// <summary>
/// Interface for lock-free statistics.
/// </summary>
public interface ILockFreeStatistics
{
    /// <summary>
    /// Gets the total number of operations performed.
    /// </summary>
    long TotalOperations { get; }

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    long SuccessfulOperations { get; }

    /// <summary>
    /// Gets the number of failed operations (due to contention).
    /// </summary>
    long FailedOperations { get; }

    /// <summary>
    /// Gets the number of retry attempts.
    /// </summary>
    long RetryAttempts { get; }

    /// <summary>
    /// Gets the success ratio.
    /// </summary>
    double SuccessRatio { get; }

    /// <summary>
    /// Gets the average retry count per operation.
    /// </summary>
    double AverageRetryCount { get; }

    /// <summary>
    /// Gets the contention level (0.0 to 1.0).
    /// </summary>
    double ContentionLevel { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Configuration for lock-free data structures.
/// </summary>
public class LockFreeConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts for CAS operations.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 100;

    /// <summary>
    /// Gets or sets the backoff strategy for retries.
    /// </summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Gets or sets the initial backoff delay in microseconds.
    /// </summary>
    public int InitialBackoffMicroseconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum backoff delay in microseconds.
    /// </summary>
    public int MaxBackoffMicroseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable contention monitoring.
    /// </summary>
    public bool EnableContentionMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the contention monitoring window size.
    /// </summary>
    public int ContentionWindowSize { get; set; } = 1000;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return MaxRetryAttempts > 0 &&
               InitialBackoffMicroseconds >= 0 &&
               MaxBackoffMicroseconds >= InitialBackoffMicroseconds &&
               ContentionWindowSize > 0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public LockFreeConfiguration Clone()
    {
        return new LockFreeConfiguration
        {
            MaxRetryAttempts = MaxRetryAttempts,
            BackoffStrategy = BackoffStrategy,
            InitialBackoffMicroseconds = InitialBackoffMicroseconds,
            MaxBackoffMicroseconds = MaxBackoffMicroseconds,
            EnableStatistics = EnableStatistics,
            EnableContentionMonitoring = EnableContentionMonitoring,
            ContentionWindowSize = ContentionWindowSize
        };
    }

    public override string ToString()
    {
        return $"LockFreeConfiguration[MaxRetries={MaxRetryAttempts}, " +
               $"Backoff={BackoffStrategy}, Statistics={EnableStatistics}, " +
               $"ContentionMonitoring={EnableContentionMonitoring}]";
    }
}

/// <summary>
/// Enumeration of backoff strategies for retry operations.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// No backoff - immediate retry.
    /// </summary>
    None,

    /// <summary>
    /// Linear backoff - delay increases linearly.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff - delay doubles each time.
    /// </summary>
    Exponential,

    /// <summary>
    /// Random backoff - random delay within bounds.
    /// </summary>
    Random
}
