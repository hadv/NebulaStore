using System;

namespace NebulaStore.Storage;

/// <summary>
/// Interface for storage foundation that provides the framework for creating and configuring storage instances.
/// The foundation is responsible for setting up the storage environment and creating storage managers.
/// </summary>
public interface IStorageFoundation
{
    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    IStorageConfiguration Configuration { get; }

    /// <summary>
    /// Gets the database instance associated with this foundation.
    /// </summary>
    IDatabase Database { get; }

    /// <summary>
    /// Sets the storage configuration.
    /// </summary>
    /// <param name="configuration">The configuration to set</param>
    /// <returns>This foundation instance for method chaining</returns>
    IStorageFoundation SetConfiguration(IStorageConfiguration configuration);

    /// <summary>
    /// Sets the database instance.
    /// </summary>
    /// <param name="database">The database to set</param>
    /// <returns>This foundation instance for method chaining</returns>
    IStorageFoundation SetDatabase(IDatabase database);

    /// <summary>
    /// Creates and starts a storage manager with the specified root object.
    /// </summary>
    /// <param name="root">The root object for the storage</param>
    /// <returns>A started storage manager instance</returns>
    IStorageManager Start(object? root = null);

    /// <summary>
    /// Creates a storage manager without starting it.
    /// </summary>
    /// <param name="root">The root object for the storage</param>
    /// <returns>A storage manager instance that needs to be started manually</returns>
    IStorageManager CreateStorageManager(object? root = null);
}

/// <summary>
/// Enhanced storage foundation interface with advanced type system features.
/// Extends the basic storage foundation with enhanced type management capabilities.
/// </summary>
public interface IEnhancedStorageFoundation : IStorageFoundation
{
    /// <summary>
    /// Gets the enhanced storage type dictionary with advanced features.
    /// </summary>
    IEnhancedStorageTypeDictionary EnhancedTypeDictionary { get; }

    /// <summary>
    /// Gets the type-in-file manager for type-to-file mapping.
    /// </summary>
    TypeInFileManager TypeInFileManager { get; }
}

/// <summary>
/// Interface for storage manager that extends storage connection with management capabilities.
/// The storage manager is the main interface for interacting with the storage system.
/// </summary>
public interface IStorageManager : IStorageConnection, IDatabasePart
{
    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    IStorageConfiguration Configuration { get; }

    /// <summary>
    /// Gets the database instance this manager belongs to.
    /// </summary>
    IDatabase Database { get; }

    /// <summary>
    /// Gets the storage type dictionary that manages type metadata.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }



    /// <summary>
    /// Gets a value indicating whether the storage manager is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the storage manager is accepting tasks.
    /// </summary>
    bool IsAcceptingTasks { get; }

    /// <summary>
    /// Starts the storage manager.
    /// </summary>
    /// <returns>This storage manager instance for method chaining</returns>
    IStorageManager Start();

    /// <summary>
    /// Shuts down the storage manager.
    /// </summary>
    /// <returns>True if shutdown was successful</returns>
    bool Shutdown();

    /// <summary>
    /// Gets the root object of the persistent object graph.
    /// </summary>
    /// <returns>The root object</returns>
    object? Root();

    /// <summary>
    /// Sets the root object of the persistent object graph.
    /// </summary>
    /// <param name="newRoot">The new root object</param>
    /// <returns>The new root object for method chaining</returns>
    object? SetRoot(object? newRoot);

    /// <summary>
    /// Stores the root object using the default storing logic.
    /// </summary>
    /// <returns>The root object's ID</returns>
    long StoreRoot();

    /// <summary>
    /// Creates a new storage connection.
    /// </summary>
    /// <returns>A new storage connection instance</returns>
    IStorageConnection CreateConnection();
}

/// <summary>
/// Interface for storage type dictionary that manages type metadata and mappings.
/// </summary>
public interface IStorageTypeDictionary
{
    /// <summary>
    /// Gets the number of types currently registered.
    /// </summary>
    int TypeCount { get; }

    /// <summary>
    /// Registers a type with the dictionary.
    /// </summary>
    /// <param name="type">The type to register</param>
    /// <returns>The type ID assigned to the type</returns>
    long RegisterType(Type type);

    /// <summary>
    /// Gets the type ID for the specified type.
    /// </summary>
    /// <param name="type">The type to get the ID for</param>
    /// <returns>The type ID, or -1 if not found</returns>
    long GetTypeId(Type type);

    /// <summary>
    /// Gets the type for the specified type ID.
    /// </summary>
    /// <param name="typeId">The type ID to get the type for</param>
    /// <returns>The type, or null if not found</returns>
    Type? GetType(long typeId);

    /// <summary>
    /// Checks if a type is registered.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is registered</returns>
    bool IsTypeRegistered(Type type);

    /// <summary>
    /// Checks if a type ID is registered.
    /// </summary>
    /// <param name="typeId">The type ID to check</param>
    /// <returns>True if the type ID is registered</returns>
    bool IsTypeIdRegistered(long typeId);
}

/// <summary>
/// Static utility class for creating storage foundations.
/// </summary>
public static class StorageFoundations
{
    /// <summary>
    /// Creates a new storage foundation with default configuration.
    /// </summary>
    /// <returns>A new storage foundation instance</returns>
    public static IStorageFoundation New()
    {
        return new StorageFoundation();
    }

    /// <summary>
    /// Creates a new storage foundation with the specified configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration to use</param>
    /// <returns>A new storage foundation instance</returns>
    public static IStorageFoundation New(IStorageConfiguration configuration)
    {
        return new StorageFoundation(configuration);
    }

    /// <summary>
    /// Creates a new storage foundation with the specified database.
    /// </summary>
    /// <param name="database">The database to use</param>
    /// <returns>A new storage foundation instance</returns>
    public static IStorageFoundation New(IDatabase database)
    {
        return new StorageFoundation(database);
    }

    /// <summary>
    /// Creates a new storage foundation with the specified configuration and database.
    /// </summary>
    /// <param name="configuration">The storage configuration to use</param>
    /// <param name="database">The database to use</param>
    /// <returns>A new storage foundation instance</returns>
    public static IStorageFoundation New(IStorageConfiguration configuration, IDatabase database)
    {
        return new StorageFoundation(configuration, database);
    }
}
