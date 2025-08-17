using System;

namespace NebulaStore.Core.Storage;

/// <summary>
/// Interface for storing objects in batch operations.
/// Provides efficient bulk storage capabilities.
/// </summary>
public interface IStorer : IDisposable
{
    /// <summary>
    /// Stores the specified object and returns its object ID.
    /// </summary>
    /// <param name="obj">The object to store</param>
    /// <returns>The object ID of the stored object</returns>
    long Store(object obj);

    /// <summary>
    /// Stores multiple objects and returns their object IDs.
    /// </summary>
    /// <param name="objects">The objects to store</param>
    /// <returns>Array of object IDs</returns>
    long[] StoreAll(params object[] objects);

    /// <summary>
    /// Commits all pending store operations.
    /// </summary>
    /// <returns>The number of objects committed</returns>
    long Commit();

    /// <summary>
    /// Gets the number of objects currently pending storage.
    /// </summary>
    long PendingObjectCount { get; }

    /// <summary>
    /// Gets a value indicating whether this storer has pending operations.
    /// </summary>
    bool HasPendingOperations { get; }

    /// <summary>
    /// Skips storing the specified object (marks it as already stored).
    /// </summary>
    /// <param name="obj">The object to skip</param>
    /// <returns>This storer instance for method chaining</returns>
    IStorer Skip(object obj);

    /// <summary>
    /// Ensures the specified object is stored (forces storage even if already stored).
    /// </summary>
    /// <param name="obj">The object to ensure is stored</param>
    /// <returns>The object ID of the stored object</returns>
    long Ensure(object obj);
}

/// <summary>
/// Interface for storage statistics.
/// </summary>
public interface IStorageStatistics
{
    /// <summary>
    /// Gets the total number of stored objects.
    /// </summary>
    long TotalObjectCount { get; }

    /// <summary>
    /// Gets the total storage size in bytes.
    /// </summary>
    long TotalStorageSize { get; }

    /// <summary>
    /// Gets the number of data files.
    /// </summary>
    int DataFileCount { get; }

    /// <summary>
    /// Gets the number of transaction files.
    /// </summary>
    int TransactionFileCount { get; }

    /// <summary>
    /// Gets the live data length in bytes.
    /// </summary>
    long LiveDataLength { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    DateTime CreationTime { get; }

    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    DateTime LastModificationTime { get; }
}

/// <summary>
/// Interface for storage connections.
/// Manages the connection to the underlying storage system.
/// </summary>
public interface IStorageConnection : IDisposable
{
    /// <summary>
    /// Creates a new storer instance.
    /// </summary>
    /// <returns>A new storer instance</returns>
    IStorer CreateStorer();

    /// <summary>
    /// Gets a value indicating whether this connection is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Issues a full garbage collection.
    /// </summary>
    void IssueFullGarbageCollection();

    /// <summary>
    /// Issues a garbage collection with the specified time budget.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if garbage collection completed within the time budget</returns>
    bool IssueGarbageCollection(long timeBudgetNanos);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    /// <returns>Storage statistics</returns>
    IStorageStatistics GetStatistics();
}
