using System;
using System.IO;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Static utility class containing static pseudo-constructor methods and various utility methods 
/// to set up and start an embedded object graph database.
/// 
/// In the simplest case, the following call is enough to set up and start an embedded object graph database:
/// <code>
/// var storage = EmbeddedStorageFactory.Start();
/// </code>
/// Anything beyond that is optimization and customization. As it should be.
/// </summary>
public static class EmbeddedStorageFactory
{
    /// <summary>
    /// Creates an instance of an IEmbeddedStorageFoundation default implementation without any assembly parts set.
    /// </summary>
    /// <returns>A new IEmbeddedStorageFoundation instance.</returns>
    public static IEmbeddedStorageFoundation CreateFoundation()
    {
        return new EmbeddedStorageFoundation();
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageConnectionFoundation instance using default values.
    /// </summary>
    /// <returns>A new IEmbeddedStorageConnectionFoundation instance.</returns>
    public static IEmbeddedStorageConnectionFoundation ConnectionFoundation()
    {
        return new EmbeddedStorageConnectionFoundation();
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageConnectionFoundation instance using the specified directory.
    /// </summary>
    /// <param name="directory">The directory where the type dictionary information will be stored.</param>
    /// <returns>A new IEmbeddedStorageConnectionFoundation instance.</returns>
    public static IEmbeddedStorageConnectionFoundation ConnectionFoundation(DirectoryInfo directory)
    {
        return new EmbeddedStorageConnectionFoundation()
            .SetStorageDirectory(directory);
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageFoundation instance using default storage directory and default values.
    /// </summary>
    /// <returns>A new all-default IEmbeddedStorageFoundation instance.</returns>
    public static IEmbeddedStorageFoundation Foundation()
    {
        return Foundation(GetDefaultStorageDirectory());
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageFoundation instance using the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path where the storage will be located.</param>
    /// <returns>A new IEmbeddedStorageFoundation instance using the specified storage directory.</returns>
    public static IEmbeddedStorageFoundation Foundation(string directoryPath)
    {
        return Foundation(new DirectoryInfo(directoryPath));
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageFoundation instance using the specified directory.
    /// </summary>
    /// <param name="directory">The directory where the storage will be located.</param>
    /// <returns>A new IEmbeddedStorageFoundation instance using the specified storage directory.</returns>
    public static IEmbeddedStorageFoundation Foundation(DirectoryInfo directory)
    {
        return Foundation(CreateConfiguration(directory));
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageFoundation instance using the specified configuration.
    /// </summary>
    /// <param name="configuration">The IStorageConfiguration to be used.</param>
    /// <returns>A new IEmbeddedStorageFoundation instance using the specified configuration.</returns>
    public static IEmbeddedStorageFoundation Foundation(IStorageConfiguration configuration)
    {
        return CreateFoundation()
            .SetConfiguration(configuration);
    }

    /// <summary>
    /// Creates a new IEmbeddedStorageFoundation instance using the specified configuration and connection foundation.
    /// </summary>
    /// <param name="configuration">The IStorageConfiguration to be used.</param>
    /// <param name="connectionFoundation">The IEmbeddedStorageConnectionFoundation instance to be used for creating new connections.</param>
    /// <returns>A new IEmbeddedStorageFoundation instance using the specified configuration.</returns>
    public static IEmbeddedStorageFoundation Foundation(IStorageConfiguration configuration, IEmbeddedStorageConnectionFoundation connectionFoundation)
    {
        return Foundation(configuration)
            .SetConnectionFoundation(connectionFoundation);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using purely default values.
    /// </summary>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start()
    {
        return Start((object?)null);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path where the storage will be located.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(string directoryPath)
    {
        return Start(null, directoryPath);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified directory.
    /// </summary>
    /// <param name="directory">The directory where the storage will be located.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(DirectoryInfo directory)
    {
        return Start(null, directory);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified configuration.
    /// </summary>
    /// <param name="configuration">The IStorageConfiguration to be used.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(IStorageConfiguration configuration)
    {
        return Start(null, configuration);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified root object.
    /// </summary>
    /// <param name="root">The explicitly defined root instance of the persistent entity graph.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(object? root)
    {
        return CreateAndStartStorageManager(Foundation(), root);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified root object and directory path.
    /// </summary>
    /// <param name="root">The explicitly defined root instance of the persistent entity graph.</param>
    /// <param name="directoryPath">The directory path where the storage will be located.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(object? root, string directoryPath)
    {
        return Start(root, new DirectoryInfo(directoryPath));
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified root object and directory.
    /// </summary>
    /// <param name="root">The explicitly defined root instance of the persistent entity graph.</param>
    /// <param name="directory">The directory where the storage will be located.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(object? root, DirectoryInfo directory)
    {
        return CreateAndStartStorageManager(Foundation(directory), root);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified root object and configuration.
    /// </summary>
    /// <param name="root">The explicitly defined root instance of the persistent entity graph.</param>
    /// <param name="configuration">The IStorageConfiguration to be used.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(object? root, IStorageConfiguration configuration)
    {
        return CreateAndStartStorageManager(Foundation(configuration), root);
    }

    /// <summary>
    /// Convenience method to configure, create and start an IStorageManager using the specified root object, configuration, and connection foundation.
    /// </summary>
    /// <param name="root">The explicitly defined root instance of the persistent entity graph.</param>
    /// <param name="configuration">The IStorageConfiguration to be used.</param>
    /// <param name="connectionFoundation">The IEmbeddedStorageConnectionFoundation to be used instead of a generically created one.</param>
    /// <returns>An IStorageManager instance connected to an actively running database.</returns>
    public static IStorageManager Start(object? root, IStorageConfiguration configuration, IEmbeddedStorageConnectionFoundation connectionFoundation)
    {
        return CreateAndStartStorageManager(Foundation(configuration, connectionFoundation), root);
    }

    /// <summary>
    /// Gets the default storage directory.
    /// </summary>
    /// <returns>The default storage directory.</returns>
    public static DirectoryInfo GetDefaultStorageDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultPath = Path.Combine(appDataPath, "NebulaStore", "storage");
        return new DirectoryInfo(defaultPath);
    }

    /// <summary>
    /// Creates a default storage configuration for the specified directory.
    /// </summary>
    /// <param name="directory">The storage directory.</param>
    /// <returns>A new IStorageConfiguration instance.</returns>
    public static IStorageConfiguration CreateConfiguration(DirectoryInfo directory)
    {
        return new StorageConfiguration
        {
            StorageDirectory = directory.FullName,
            ChannelCount = Environment.ProcessorCount,
            HousekeepingInterval = TimeSpan.FromMinutes(1),
            EntityCacheThreshold = 1_000_000_000, // 1 GB
            EntityCacheTimeout = TimeSpan.FromDays(1)
        };
    }

    /// <summary>
    /// Utility method to encapsulate the code to create and start an IStorageManager.
    /// </summary>
    /// <param name="foundation">The IEmbeddedStorageFoundation to be used.</param>
    /// <param name="root">The persistent entity graph's root instance, potentially null.</param>
    /// <returns>A newly created and started IStorageManager instance.</returns>
    private static IStorageManager CreateAndStartStorageManager(IEmbeddedStorageFoundation foundation, object? root)
    {
        var storageManager = foundation.CreateEmbeddedStorageManager(root);
        storageManager.Start();
        return storageManager;
    }
}

/// <summary>
/// Interface for embedded storage foundations.
/// </summary>
public interface IEmbeddedStorageFoundation
{
    /// <summary>
    /// Sets the storage configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration.</param>
    /// <returns>This foundation instance for fluent usage.</returns>
    IEmbeddedStorageFoundation SetConfiguration(IStorageConfiguration configuration);

    /// <summary>
    /// Sets the connection foundation.
    /// </summary>
    /// <param name="connectionFoundation">The connection foundation.</param>
    /// <returns>This foundation instance for fluent usage.</returns>
    IEmbeddedStorageFoundation SetConnectionFoundation(IEmbeddedStorageConnectionFoundation connectionFoundation);

    /// <summary>
    /// Creates an embedded storage manager with the specified root object.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <returns>A new embedded storage manager instance.</returns>
    IStorageManager CreateEmbeddedStorageManager(object? root);
}

/// <summary>
/// Interface for embedded storage connection foundations.
/// </summary>
public interface IEmbeddedStorageConnectionFoundation
{
    /// <summary>
    /// Sets the storage directory.
    /// </summary>
    /// <param name="directory">The storage directory.</param>
    /// <returns>This connection foundation instance for fluent usage.</returns>
    IEmbeddedStorageConnectionFoundation SetStorageDirectory(DirectoryInfo directory);

    /// <summary>
    /// Creates a storage connection.
    /// </summary>
    /// <returns>A new storage connection instance.</returns>
    IStorageConnection CreateConnection();
}

/// <summary>
/// Default implementation of embedded storage foundation.
/// </summary>
internal class EmbeddedStorageFoundation : IEmbeddedStorageFoundation
{
    private IStorageConfiguration? _configuration;
    private IEmbeddedStorageConnectionFoundation? _connectionFoundation;

    public IEmbeddedStorageFoundation SetConfiguration(IStorageConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    public IEmbeddedStorageFoundation SetConnectionFoundation(IEmbeddedStorageConnectionFoundation connectionFoundation)
    {
        _connectionFoundation = connectionFoundation ?? throw new ArgumentNullException(nameof(connectionFoundation));
        return this;
    }

    public IStorageManager CreateEmbeddedStorageManager(object? root)
    {
        var configuration = _configuration ?? EmbeddedStorageFactory.CreateConfiguration(EmbeddedStorageFactory.GetDefaultStorageDirectory());
        var connectionFoundation = _connectionFoundation ?? EmbeddedStorageFactory.ConnectionFoundation();

        // Create the actual storage manager implementation
        var storageManager = StorageManager.Create(configuration);

        // Set the root object if provided
        if (root != null)
        {
            storageManager.SetRoot(root);
        }

        return storageManager;
    }
}

/// <summary>
/// Default implementation of embedded storage connection foundation.
/// </summary>
internal class EmbeddedStorageConnectionFoundation : IEmbeddedStorageConnectionFoundation
{
    private DirectoryInfo? _storageDirectory;

    public IEmbeddedStorageConnectionFoundation SetStorageDirectory(DirectoryInfo directory)
    {
        _storageDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
        return this;
    }

    public IStorageConnection CreateConnection()
    {
        // Create a basic storage connection
        var configuration = EmbeddedStorageFactory.CreateConfiguration(_storageDirectory ?? EmbeddedStorageFactory.GetDefaultStorageDirectory());
        var storageManager = StorageManager.Create(configuration);
        return storageManager.CreateConnection();
    }
}

/// <summary>
/// Default implementation of storage configuration.
/// </summary>
internal class StorageConfiguration : IStorageConfiguration
{
    public string StorageDirectory { get; set; } = string.Empty;
    public int ChannelCount { get; set; } = 1;
    public TimeSpan HousekeepingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public long EntityCacheThreshold { get; set; } = 1_000_000_000;
    public TimeSpan EntityCacheTimeout { get; set; } = TimeSpan.FromDays(1);
}
