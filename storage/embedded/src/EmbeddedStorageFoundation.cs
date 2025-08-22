using System;
using System.Collections.Generic;
using System.Linq;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of the embedded storage foundation.
/// Provides a builder pattern for configuring embedded storage managers.
/// </summary>
public class EmbeddedStorageFoundation : IEmbeddedStorageFoundation
{
    private IEmbeddedStorageConfiguration? _configuration;
    private object? _root;
    private Func<object>? _rootSupplier;
    private readonly List<ITypeHandler> _typeHandlers = new();
    private Func<Type, bool>? _typeEvaluator;

    /// <summary>
    /// Creates a new embedded storage foundation instance.
    /// </summary>
    /// <returns>A new foundation instance</returns>
    public static IEmbeddedStorageFoundation New() => new EmbeddedStorageFoundation();

    public IEmbeddedStorageFoundation SetConfiguration(IEmbeddedStorageConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    public IEmbeddedStorageFoundation SetRoot(object root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _rootSupplier = null; // Clear supplier if explicit root is set
        return this;
    }

    public IEmbeddedStorageFoundation SetRootSupplier(Func<object> rootSupplier)
    {
        _rootSupplier = rootSupplier ?? throw new ArgumentNullException(nameof(rootSupplier));
        _root = null; // Clear explicit root if supplier is set
        return this;
    }

    public IEmbeddedStorageFoundation RegisterTypeHandler(ITypeHandler typeHandler)
    {
        if (typeHandler == null)
            throw new ArgumentNullException(nameof(typeHandler));

        _typeHandlers.Add(typeHandler);
        return this;
    }

    public IEmbeddedStorageFoundation RegisterTypeHandlers(IEnumerable<ITypeHandler> typeHandlers)
    {
        if (typeHandlers == null)
            throw new ArgumentNullException(nameof(typeHandlers));

        _typeHandlers.AddRange(typeHandlers);
        return this;
    }

    public IEmbeddedStorageFoundation SetTypeEvaluator(Func<Type, bool> typeEvaluator)
    {
        _typeEvaluator = typeEvaluator ?? throw new ArgumentNullException(nameof(typeEvaluator));
        return this;
    }

    public IEmbeddedStorageConfiguration GetConfiguration()
    {
        return _configuration ??= EmbeddedStorageConfiguration.Default();
    }

    public IEmbeddedStorageManager CreateEmbeddedStorageManager()
    {
        return CreateEmbeddedStorageManager(null);
    }

    public IEmbeddedStorageManager CreateEmbeddedStorageManager(object? explicitRoot)
    {
        var configuration = GetConfiguration();
        
        // Determine the root object
        object? rootObject = explicitRoot ?? _root ?? _rootSupplier?.Invoke();

        // Create type handler registry
        var typeHandlerRegistry = new TypeHandlerRegistry();
        foreach (var handler in _typeHandlers)
        {
            typeHandlerRegistry.RegisterTypeHandler(handler);
        }

        // Create storage connection (AFS or traditional)
        IStorageConnection connection = configuration.UseAfs
            ? new AfsStorageConnection(configuration, typeHandlerRegistry)
            : CreateBasicStorageConnection(configuration);

        // Create the storage manager
        var manager = new EmbeddedStorageManager(configuration, connection, rootObject, _typeEvaluator);

        return manager;
    }

    private IStorageConnection CreateBasicStorageConnection(IEmbeddedStorageConfiguration configuration)
    {
        // Convert embedded configuration to storage configuration
        var storageConfig = new BasicStorageConfiguration(configuration);

        // Create a basic storage manager and return its connection
        var storageManager = StorageManager.Create(storageConfig);
        return storageManager.CreateConnection();
    }

    public IEmbeddedStorageManager Start()
    {
        return Start(null);
    }

    public IEmbeddedStorageManager Start(object? explicitRoot)
    {
        var manager = CreateEmbeddedStorageManager(explicitRoot);
        return manager.Start();
    }
}

/// <summary>
/// Adapter to convert IEmbeddedStorageConfiguration to IStorageConfiguration
/// </summary>
internal class BasicStorageConfiguration : IStorageConfiguration
{
    private readonly IEmbeddedStorageConfiguration _embeddedConfig;

    public BasicStorageConfiguration(IEmbeddedStorageConfiguration embeddedConfig)
    {
        _embeddedConfig = embeddedConfig ?? throw new ArgumentNullException(nameof(embeddedConfig));
    }

    public string StorageDirectory => _embeddedConfig.StorageDirectory;
    public int ChannelCount => _embeddedConfig.ChannelCount;
    public TimeSpan HousekeepingInterval => TimeSpan.FromMilliseconds(_embeddedConfig.HousekeepingIntervalMs);
    public long HousekeepingTimeBudget => _embeddedConfig.HousekeepingTimeBudgetNs;
    public long EntityCacheThreshold => _embeddedConfig.EntityCacheThreshold;
    public TimeSpan EntityCacheTimeout => TimeSpan.FromMilliseconds(_embeddedConfig.EntityCacheTimeoutMs);
    public long DataFileMinimumSize => _embeddedConfig.DataFileMinimumSize;
    public long DataFileMaximumSize => _embeddedConfig.DataFileMaximumSize;
    public bool UseAfs => _embeddedConfig.UseAfs;
}
