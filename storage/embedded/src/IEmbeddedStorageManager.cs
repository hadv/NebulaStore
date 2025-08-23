using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Storage.Monitoring;
using NebulaStore.GigaMap;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Main interface for embedded storage management.
/// Provides high-level operations for object persistence and retrieval.
/// </summary>
public interface IEmbeddedStorageManager : IDisposable
{
    /// <summary>
    /// Gets the root object of the specified type.
    /// Returns null if no root has been set.
    /// </summary>
    /// <typeparam name="T">The type of the root object</typeparam>
    /// <returns>The root object instance or null</returns>
    T? Root<T>();

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

    // Note: EclipseStore doesn't have a Query method - use direct object graph navigation instead
    // Example: root.Customers.Where(c => c.Age > 25) using LINQ on your object graph

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

    /// <summary>
    /// Gets the monitoring manager for this storage instance.
    /// </summary>
    /// <returns>The monitoring manager</returns>
    IStorageMonitoringManager GetMonitoringManager();

    // ========== GigaMap Integration ==========

    /// <summary>
    /// Creates a new GigaMap builder for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A GigaMap builder instance</returns>
    IGigaMapBuilder<T> CreateGigaMap<T>() where T : class;

    /// <summary>
    /// Gets an existing GigaMap for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>The GigaMap instance, or null if not found</returns>
    IGigaMap<T>? GetGigaMap<T>() where T : class;

    /// <summary>
    /// Registers a GigaMap instance with the storage manager for persistence.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="gigaMap">The GigaMap to register</param>
    void RegisterGigaMap<T>(IGigaMap<T> gigaMap) where T : class;

    /// <summary>
    /// Stores all registered GigaMaps to persistent storage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StoreGigaMapsAsync();

    // Eclipse Store compatibility methods

    /// <summary>
    /// Issues a full file check operation.
    /// </summary>
    void IssueFullFileCheck();

    /// <summary>
    /// Issues a file check operation with a time budget.
    /// </summary>
    /// <param name="timeBudget">Time budget for the operation</param>
    /// <returns>True if completed within budget</returns>
    bool IssueFileCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues a full cache check operation.
    /// </summary>
    void IssueFullCacheCheck();

    /// <summary>
    /// Issues a cache check operation with a time budget.
    /// </summary>
    /// <param name="timeBudget">Time budget for the operation</param>
    /// <returns>True if completed within budget</returns>
    bool IssueCacheCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues a full backup to the specified directory.
    /// </summary>
    /// <param name="targetDirectory">Target directory for backup</param>
    void IssueFullBackup(System.IO.DirectoryInfo targetDirectory);

    /// <summary>
    /// Creates storage statistics.
    /// </summary>
    /// <returns>Storage statistics</returns>
    IStorageStatistics CreateStorageStatistics();

    /// <summary>
    /// Exports channels to the specified directory.
    /// </summary>
    /// <param name="targetDirectory">Target directory</param>
    /// <param name="performGarbageCollection">Whether to perform garbage collection</param>
    void ExportChannels(System.IO.DirectoryInfo targetDirectory, bool performGarbageCollection = true);

    /// <summary>
    /// Imports files from the specified directory.
    /// </summary>
    /// <param name="importDirectory">Import directory</param>
    void ImportFiles(System.IO.DirectoryInfo importDirectory);

    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the database interface.
    /// </summary>
    IDatabase Database();

    /// <summary>
    /// Views the persistence roots.
    /// </summary>
    /// <returns>Persistence roots view</returns>
    IPersistenceRootsView ViewRoots();

    /// <summary>
    /// Creates a storage connection.
    /// </summary>
    /// <returns>Storage connection</returns>
    IStorageConnection CreateConnection();
}
