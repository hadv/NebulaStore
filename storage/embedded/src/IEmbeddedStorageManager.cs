using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Main interface for embedded storage management.
/// Provides high-level operations for object persistence and retrieval.
/// </summary>
public interface IEmbeddedStorageManager : IDisposable
{
    /// <summary>
    /// Gets the root object of the specified type.
    /// Creates a new instance if no root exists.
    /// </summary>
    /// <typeparam name="T">The type of the root object</typeparam>
    /// <returns>The root object instance</returns>
    T Root<T>() where T : new();

    /// <summary>
    /// Sets a new root object instance.
    /// </summary>
    /// <param name="newRoot">The new root object</param>
    /// <returns>The set root object</returns>
    object SetRoot(object newRoot);

    /// <summary>
    /// Stores the root object and returns its object ID.
    /// </summary>
    /// <returns>The object ID of the stored root</returns>
    long StoreRoot();

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
    /// Queries for objects of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to query for</typeparam>
    /// <returns>Enumerable of objects of the specified type</returns>
    IEnumerable<T> Query<T>();

    /// <summary>
    /// Creates a new storer instance for batch operations.
    /// </summary>
    /// <returns>A new storer instance</returns>
    IStorer CreateStorer();

    /// <summary>
    /// Starts the storage manager.
    /// </summary>
    /// <returns>This storage manager instance for method chaining</returns>
    IEmbeddedStorageManager Start();

    /// <summary>
    /// Shuts down the storage manager.
    /// </summary>
    /// <returns>True if shutdown was successful</returns>
    bool Shutdown();

    /// <summary>
    /// Gets a value indicating whether the storage manager is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the storage manager is accepting tasks.
    /// </summary>
    bool IsAcceptingTasks { get; }

    /// <summary>
    /// Gets a value indicating whether the storage manager is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    IEmbeddedStorageConfiguration Configuration { get; }

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
    /// Creates a backup of the storage.
    /// </summary>
    /// <param name="backupDirectory">Directory to store the backup</param>
    Task CreateBackupAsync(string backupDirectory);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    /// <returns>Storage statistics</returns>
    IStorageStatistics GetStatistics();
}
