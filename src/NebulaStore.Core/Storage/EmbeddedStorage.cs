using System;
using System.IO;

namespace NebulaStore.Core.Storage;

/// <summary>
/// Static utility class for creating and configuring embedded storage managers.
/// Provides convenient factory methods for common storage scenarios.
/// </summary>
public static class EmbeddedStorage
{
    /// <summary>
    /// Default storage directory name.
    /// </summary>
    public const string DefaultStorageDirectory = "storage";

    /// <summary>
    /// Creates a new embedded storage foundation with default settings.
    /// </summary>
    /// <returns>A new foundation instance</returns>
    public static IEmbeddedStorageFoundation CreateFoundation()
    {
        return EmbeddedStorageFoundation.New();
    }

    /// <summary>
    /// Creates a new embedded storage foundation with the specified storage directory.
    /// </summary>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A new foundation instance</returns>
    public static IEmbeddedStorageFoundation Foundation(string storageDirectory)
    {
        if (string.IsNullOrEmpty(storageDirectory))
            throw new ArgumentException("Storage directory cannot be null or empty", nameof(storageDirectory));

        var configuration = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(storageDirectory)
            .Build();

        return CreateFoundation().SetConfiguration(configuration);
    }

    /// <summary>
    /// Creates a new embedded storage foundation with the specified configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>A new foundation instance</returns>
    public static IEmbeddedStorageFoundation Foundation(IEmbeddedStorageConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        return CreateFoundation().SetConfiguration(configuration);
    }

    /// <summary>
    /// Creates a new embedded storage foundation with the specified configuration builder.
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder</param>
    /// <returns>A new foundation instance</returns>
    public static IEmbeddedStorageFoundation Foundation(IEmbeddedStorageConfigurationBuilder configurationBuilder)
    {
        if (configurationBuilder == null)
            throw new ArgumentNullException(nameof(configurationBuilder));

        return Foundation(configurationBuilder.Build());
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with default settings.
    /// </summary>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start()
    {
        return Start((object?)null);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified storage directory.
    /// </summary>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(string storageDirectory)
    {
        return Start(null, storageDirectory);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(IEmbeddedStorageConfiguration configuration)
    {
        return Start(null, configuration);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified configuration builder.
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(IEmbeddedStorageConfigurationBuilder configurationBuilder)
    {
        return Start(null, configurationBuilder);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified root object.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(object? root)
    {
        return CreateFoundation().Start(root);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified root object and storage directory.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(object? root, string storageDirectory)
    {
        return Foundation(storageDirectory).Start(root);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified root object and configuration.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(object? root, IEmbeddedStorageConfiguration configuration)
    {
        return Foundation(configuration).Start(root);
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with the specified root object and configuration builder.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="configurationBuilder">The configuration builder</param>
    /// <returns>A started storage manager instance</returns>
    public static IEmbeddedStorageManager Start(object? root, IEmbeddedStorageConfigurationBuilder configurationBuilder)
    {
        return Foundation(configurationBuilder).Start(root);
    }

    /// <summary>
    /// Gets the default storage directory path.
    /// </summary>
    /// <returns>The default storage directory path</returns>
    public static string GetDefaultStorageDirectory()
    {
        return Path.Combine(Environment.CurrentDirectory, DefaultStorageDirectory);
    }

    /// <summary>
    /// Creates a configuration builder with default settings.
    /// </summary>
    /// <returns>A new configuration builder</returns>
    public static IEmbeddedStorageConfigurationBuilder ConfigurationBuilder()
    {
        return EmbeddedStorageConfiguration.New();
    }

    /// <summary>
    /// Creates a configuration builder with the specified storage directory.
    /// </summary>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A new configuration builder</returns>
    public static IEmbeddedStorageConfigurationBuilder ConfigurationBuilder(string storageDirectory)
    {
        return EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(storageDirectory);
    }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    /// <returns>A default configuration instance</returns>
    public static IEmbeddedStorageConfiguration DefaultConfiguration()
    {
        return EmbeddedStorageConfiguration.Default();
    }

    /// <summary>
    /// Creates a configuration with the specified storage directory.
    /// </summary>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A configuration instance</returns>
    public static IEmbeddedStorageConfiguration Configuration(string storageDirectory)
    {
        return EmbeddedStorageConfiguration.WithStorageDirectory(storageDirectory);
    }
}
