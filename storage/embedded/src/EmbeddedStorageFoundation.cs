using System;
using System.Collections.Generic;
using System.Linq;
using NebulaStore.Storage.EmbeddedConfiguration;

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

        // Create storage connection
        var connection = new StorageConnection(configuration, typeHandlerRegistry);

        // Create the storage manager
        var manager = new EmbeddedStorageManager(configuration, connection, rootObject, _typeEvaluator);

        return manager;
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
/// Default implementation of type handler registry.
/// </summary>
public class TypeHandlerRegistry : ITypeHandlerRegistry
{
    private readonly Dictionary<Type, ITypeHandler> _typeHandlers = new();
    private readonly Dictionary<long, ITypeHandler> _typeIdHandlers = new();

    public void RegisterTypeHandler(ITypeHandler typeHandler)
    {
        if (typeHandler == null)
            throw new ArgumentNullException(nameof(typeHandler));

        _typeHandlers[typeHandler.HandledType] = typeHandler;
        _typeIdHandlers[typeHandler.TypeId] = typeHandler;
    }

    public ITypeHandler? GetTypeHandler(Type type)
    {
        _typeHandlers.TryGetValue(type, out var handler);
        return handler;
    }

    public ITypeHandler? GetTypeHandler(long typeId)
    {
        _typeIdHandlers.TryGetValue(typeId, out var handler);
        return handler;
    }

    public IEnumerable<ITypeHandler> GetAllTypeHandlers()
    {
        return _typeHandlers.Values.ToList();
    }
}
