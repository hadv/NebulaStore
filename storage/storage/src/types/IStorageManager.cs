using System;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Central managing type for a native .NET database's storage layer.
/// 
/// For all intents and purposes, an IStorageManager instance represents the storage of a database in the
/// .NET application that uses it. It is used for starting and stopping storage management threads,
/// calling storage-level utility functionality like clearing the low-level data cache, cleaning up / condensing
/// storage files or calling the storage-level garbage collector to remove data that has become unreachable in the
/// entity graph. This type also allows querying the used IStorageConfiguration or the
/// IStorageTypeDictionary that defines the persistent structure of all handled types.
/// 
/// For most cases, only the methods Root, SetRoot, Start and Shutdown are important. Everything else is used for
/// more or less advanced purposes and should only be used with good knowledge about the effects caused by it.
/// 
/// An IStorageManager instance is also implicitly an IStorageConnection, so that developers don't
/// need to care about connections at all if a single connection suffices.
/// </summary>
public interface IStorageManager : IStorageController, IStorageConnection, IDatabasePart
{
    /// <summary>
    /// Returns the IStorageConfiguration used to initialize this IStorageManager instance.
    /// </summary>
    /// <returns>The used configuration.</returns>
    IStorageConfiguration Configuration { get; }

    /// <summary>
    /// Returns the IStorageTypeDictionary that contains a complete list of types currently known to /
    /// handled by the storage represented by this IStorageManager instance. This list grows dynamically
    /// as so far unknown types are discovered, analyzed, mapped and added on the fly by a store.
    /// </summary>
    /// <returns>The current IStorageTypeDictionary.</returns>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Starts the storage manager.
    /// </summary>
    /// <returns>This storage manager instance for fluent usage.</returns>
    new IStorageManager Start();

    /// <summary>
    /// Shuts down the storage manager.
    /// </summary>
    /// <returns>True if shutdown was successful, false otherwise.</returns>
    new bool Shutdown();

    /// <summary>
    /// Creates a new IStorageConnection instance. See the type description for details.
    /// Note that while it makes sense on an architectural level to have a connecting mechanism between
    /// application logic and storage level, there is currently no need to create additional connections beyond the
    /// intrinsic one held inside an IStorageManager instance. Just use it instead.
    /// </summary>
    /// <returns>A new IStorageConnection instance.</returns>
    IStorageConnection CreateConnection();

    /// <summary>
    /// Returns the persistent object graph's root object, without specific typing.
    /// 
    /// If a specifically typed root instance reference is desired, it is preferable to hold a properly typed constant
    /// reference to it and let the storage initialization use that instance as the root.
    /// 
    /// Example:
    /// <code>
    /// static readonly MyAppRoot ROOT = new MyAppRoot();
    /// static readonly IStorageManager STORAGE = EmbeddedStorage.Start(ROOT);
    /// </code>
    /// </summary>
    /// <returns>The persistent object graph's root object.</returns>
    object? Root();

    /// <summary>
    /// Sets the passed instance as the new root for the persistent object graph.
    /// Note that this will replace the old root instance, potentially resulting in wiping the whole database.
    /// </summary>
    /// <param name="newRoot">The new root instance to be set.</param>
    /// <returns>The passed newRoot to allow fluent usage of this method.</returns>
    object SetRoot(object newRoot);

    /// <summary>
    /// Stores the registered root instance (as returned by Root()) by using the default storing logic
    /// by calling CreateStorer() to create the IStorer to be used.
    /// Depending on the storer logic, storing the root instance can cause many other objects to be stored, as well.
    /// For example for the default behavior (as implemented in CreateLazyStorer()), all recursively referenced
    /// instances that are not yet known to the persistent context are stored as well.
    /// </summary>
    /// <returns>The root instance's objectId.</returns>
    long StoreRoot();

    /// <summary>
    /// Returns a read-only view on all technical root instances registered in this IStorageManager instance.
    /// </summary>
    /// <returns>A new IPersistenceRootsView instance allowing to iterate all technical root instances.</returns>
    IPersistenceRootsView ViewRoots();

    /// <summary>
    /// Returns the IDatabase instance this IStorageManager is associated with.
    /// See its description for details.
    /// </summary>
    /// <returns>The associated IDatabase instance.</returns>
    IDatabase Database();

    /// <summary>
    /// Alias for Database().DatabaseName().
    /// </summary>
    new string DatabaseName { get; }
}

/// <summary>
/// Interface for storage controller functionality.
/// </summary>
public interface IStorageController
{
    /// <summary>
    /// Starts the storage controller.
    /// </summary>
    /// <returns>This storage controller instance.</returns>
    IStorageController Start();

    /// <summary>
    /// Shuts down the storage controller.
    /// </summary>
    /// <returns>True if shutdown was successful, false otherwise.</returns>
    bool Shutdown();

    /// <summary>
    /// Gets a value indicating whether the storage controller is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the storage controller is shutting down.
    /// </summary>
    bool IsShuttingDown { get; }
}

/// <summary>
/// Interface for database part functionality.
/// </summary>
public interface IDatabasePart
{
    /// <summary>
    /// Gets the database name.
    /// </summary>
    string DatabaseName { get; }
}

/// <summary>
/// Interface for storage configuration.
/// </summary>
public interface IStorageConfiguration
{
    /// <summary>
    /// Gets the storage directory.
    /// </summary>
    string StorageDirectory { get; }

    /// <summary>
    /// Gets the channel count.
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// Gets the housekeeping interval.
    /// </summary>
    TimeSpan HousekeepingInterval { get; }

    /// <summary>
    /// Gets the entity cache threshold.
    /// </summary>
    long EntityCacheThreshold { get; }

    /// <summary>
    /// Gets the entity cache timeout.
    /// </summary>
    TimeSpan EntityCacheTimeout { get; }
}

/// <summary>
/// Interface for storage type dictionary.
/// </summary>
public interface IStorageTypeDictionary
{
    /// <summary>
    /// Gets the type handler for the specified type.
    /// </summary>
    /// <param name="type">The type to get the handler for.</param>
    /// <returns>The type handler for the specified type.</returns>
    ITypeHandler? GetTypeHandler(Type type);

    /// <summary>
    /// Gets all registered type handlers.
    /// </summary>
    /// <returns>All registered type handlers.</returns>
    IEnumerable<ITypeHandler> GetAllTypeHandlers();

    /// <summary>
    /// Registers a type handler for the specified type.
    /// </summary>
    /// <param name="type">The type to register the handler for.</param>
    /// <param name="handler">The type handler to register.</param>
    void RegisterTypeHandler(Type type, ITypeHandler handler);
}

/// <summary>
/// Interface for persistence roots view.
/// </summary>
public interface IPersistenceRootsView
{
    /// <summary>
    /// Gets the default root reference.
    /// </summary>
    /// <returns>The default root reference.</returns>
    object? RootReference();

    /// <summary>
    /// Gets all root references.
    /// </summary>
    /// <returns>All root references.</returns>
    IEnumerable<object> AllRootReferences();
}

/// <summary>
/// Interface for database.
/// </summary>
public interface IDatabase
{
    /// <summary>
    /// Gets the database name.
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Gets the storage manager.
    /// </summary>
    IStorageManager StorageManager { get; }
}

/// <summary>
/// Interface for persistence manager.
/// </summary>
public interface IPersistenceManager
{
    /// <summary>
    /// Stores the specified object.
    /// </summary>
    /// <param name="instance">The object to store.</param>
    /// <returns>The object ID of the stored object.</returns>
    long Store(object instance);

    /// <summary>
    /// Stores all specified objects.
    /// </summary>
    /// <param name="instances">The objects to store.</param>
    /// <returns>The object IDs of the stored objects.</returns>
    long[] StoreAll(params object[] instances);

    /// <summary>
    /// Gets the object with the specified object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <returns>The object with the specified object ID.</returns>
    object GetObject(long objectId);

    /// <summary>
    /// Creates a lazy storer.
    /// </summary>
    /// <returns>A lazy storer.</returns>
    IStorer CreateLazyStorer();

    /// <summary>
    /// Creates a storer.
    /// </summary>
    /// <returns>A storer.</returns>
    IStorer CreateStorer();

    /// <summary>
    /// Creates an eager storer.
    /// </summary>
    /// <returns>An eager storer.</returns>
    IStorer CreateEagerStorer();

    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the object registry.
    /// </summary>
    IPersistenceObjectRegistry ObjectRegistry { get; }

    /// <summary>
    /// Gets the roots view.
    /// </summary>
    IPersistenceRootsView ViewRoots();
}

/// <summary>
/// Interface for persistence object registry.
/// </summary>
public interface IPersistenceObjectRegistry
{
    /// <summary>
    /// Looks up the object ID for the specified object.
    /// </summary>
    /// <param name="instance">The object to look up.</param>
    /// <returns>The object ID for the specified object.</returns>
    long LookupObjectId(object instance);

    /// <summary>
    /// Registers the specified object with the specified object ID.
    /// </summary>
    /// <param name="instance">The object to register.</param>
    /// <param name="objectId">The object ID to register.</param>
    void RegisterObject(object instance, long objectId);

    /// <summary>
    /// Consolidates the registry.
    /// </summary>
    void Consolidate();
}

/// <summary>
/// Interface for storage statistics.
/// </summary>
public interface IStorageStatistics
{
    /// <summary>
    /// Gets the total file count.
    /// </summary>
    int TotalFileCount { get; }

    /// <summary>
    /// Gets the total file size.
    /// </summary>
    long TotalFileSize { get; }

    /// <summary>
    /// Gets the live data size.
    /// </summary>
    long LiveDataSize { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    DateTime CreationTimestamp { get; }
}
