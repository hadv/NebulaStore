using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Default implementation of IStorageTypeDictionary that manages type handlers and type mappings.
/// </summary>
public class StorageTypeDictionary : IStorageTypeDictionary
{
    #region Private Fields

    private readonly ConcurrentDictionary<Type, ITypeHandler> _typeToHandler;
    private readonly ConcurrentDictionary<long, ITypeHandler> _typeIdToHandler;
    private readonly ConcurrentDictionary<string, ITypeHandler> _typeNameToHandler;
    private readonly object _lock = new object();
    private long _nextTypeId = 1;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageTypeDictionary class.
    /// </summary>
    public StorageTypeDictionary()
    {
        _typeToHandler = new ConcurrentDictionary<Type, ITypeHandler>();
        _typeIdToHandler = new ConcurrentDictionary<long, ITypeHandler>();
        _typeNameToHandler = new ConcurrentDictionary<string, ITypeHandler>();
        
        // Register built-in types
        RegisterBuiltInTypes();
    }

    #endregion

    #region IStorageTypeDictionary Implementation

    public ITypeHandler? GetTypeHandler(Type type)
    {
        if (type == null)
            return null;

        // Try to get existing handler
        if (_typeToHandler.TryGetValue(type, out var handler))
        {
            return handler;
        }

        // Auto-register the type if not found
        return RegisterType(type);
    }

    public ITypeHandler? GetTypeHandlerByTypeId(long typeId)
    {
        _typeIdToHandler.TryGetValue(typeId, out var handler);
        return handler;
    }

    public IEnumerable<ITypeHandler> GetAllTypeHandlers()
    {
        return _typeToHandler.Values.ToList();
    }

    public void RegisterTypeHandler(Type type, ITypeHandler handler)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            // Check for conflicts
            if (_typeToHandler.ContainsKey(type))
            {
                var existing = _typeToHandler[type];
                if (existing.TypeId != handler.TypeId)
                {
                    throw new InvalidOperationException(
                        $"Type {type.FullName} is already registered with different type ID. " +
                        $"Existing: {existing.TypeId}, New: {handler.TypeId}");
                }
                return; // Already registered with same ID
            }

            if (_typeIdToHandler.ContainsKey(handler.TypeId))
            {
                var existing = _typeIdToHandler[handler.TypeId];
                if (existing.HandledType != type)
                {
                    throw new InvalidOperationException(
                        $"Type ID {handler.TypeId} is already registered for different type. " +
                        $"Existing: {existing.HandledType.FullName}, New: {type.FullName}");
                }
                return; // Already registered
            }

            // Register the handler
            _typeToHandler[type] = handler;
            _typeIdToHandler[handler.TypeId] = handler;
            _typeNameToHandler[handler.HandledType.FullName ?? handler.HandledType.Name] = handler;

            // Update next type ID if necessary
            if (handler.TypeId >= _nextTypeId)
            {
                _nextTypeId = handler.TypeId + 1;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets a type handler by type ID.
    /// </summary>
    /// <param name="typeId">The type ID.</param>
    /// <returns>The type handler, or null if not found.</returns>
    public ITypeHandler? GetTypeHandler(long typeId)
    {
        _typeIdToHandler.TryGetValue(typeId, out var handler);
        return handler;
    }

    /// <summary>
    /// Gets a type handler by type name.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>The type handler, or null if not found.</returns>
    public ITypeHandler? GetTypeHandler(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        _typeNameToHandler.TryGetValue(typeName, out var handler);
        return handler;
    }

    /// <summary>
    /// Registers a type and returns its handler.
    /// </summary>
    /// <param name="type">The type to register.</param>
    /// <returns>The type handler for the registered type.</returns>
    public ITypeHandler RegisterType(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        // Check if already registered
        if (_typeToHandler.TryGetValue(type, out var existingHandler))
        {
            return existingHandler;
        }

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_typeToHandler.TryGetValue(type, out existingHandler))
            {
                return existingHandler;
            }

            // Create new handler with next available type ID
            var typeId = _nextTypeId++;
            var handler = new TypeHandler(type, typeId);

            // Register the handler
            _typeToHandler[type] = handler;
            _typeIdToHandler[typeId] = handler;
            _typeNameToHandler[handler.TypeName] = handler;

            return handler;
        }
    }

    /// <summary>
    /// Gets the count of registered types.
    /// </summary>
    public int TypeCount => _typeToHandler.Count;

    /// <summary>
    /// Checks if a type is registered.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is registered.</returns>
    public bool IsTypeRegistered(Type type)
    {
        return type != null && _typeToHandler.ContainsKey(type);
    }

    /// <summary>
    /// Checks if a type ID is registered.
    /// </summary>
    /// <param name="typeId">The type ID to check.</param>
    /// <returns>True if the type ID is registered.</returns>
    public bool IsTypeIdRegistered(long typeId)
    {
        return _typeIdToHandler.ContainsKey(typeId);
    }

    /// <summary>
    /// Gets all registered types.
    /// </summary>
    /// <returns>All registered types.</returns>
    public IEnumerable<Type> GetAllTypes()
    {
        return _typeToHandler.Keys.ToList();
    }

    /// <summary>
    /// Gets all registered type IDs.
    /// </summary>
    /// <returns>All registered type IDs.</returns>
    public IEnumerable<long> GetAllTypeIds()
    {
        return _typeIdToHandler.Keys.ToList();
    }

    /// <summary>
    /// Clears all registered types.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _typeToHandler.Clear();
            _typeIdToHandler.Clear();
            _typeNameToHandler.Clear();
            _nextTypeId = 1;
            RegisterBuiltInTypes();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Registers built-in .NET types with predefined type IDs.
    /// </summary>
    private void RegisterBuiltInTypes()
    {
        // Register fundamental types with fixed IDs to ensure consistency
        var builtInTypes = new[]
        {
            (typeof(object), 1L),
            (typeof(string), 2L),
            (typeof(int), 3L),
            (typeof(long), 4L),
            (typeof(bool), 5L),
            (typeof(byte), 6L),
            (typeof(short), 7L),
            (typeof(float), 8L),
            (typeof(double), 9L),
            (typeof(decimal), 10L),
            (typeof(DateTime), 11L),
            (typeof(DateTimeOffset), 12L),
            (typeof(TimeSpan), 13L),
            (typeof(Guid), 14L),
            (typeof(char), 15L),
            (typeof(sbyte), 16L),
            (typeof(ushort), 17L),
            (typeof(uint), 18L),
            (typeof(ulong), 19L),
            (typeof(byte[]), 20L),
            (typeof(int[]), 21L),
            (typeof(string[]), 22L),
            (typeof(object[]), 23L)
        };

        foreach (var (type, typeId) in builtInTypes)
        {
            var handler = new TypeHandler(type, typeId);
            _typeToHandler[type] = handler;
            _typeIdToHandler[typeId] = handler;
            _typeNameToHandler[handler.TypeName] = handler;
        }

        // Set next type ID after built-in types
        _nextTypeId = 1000; // Start user types at 1000
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageTypeDictionary instance.
    /// </summary>
    /// <returns>A new StorageTypeDictionary instance.</returns>
    public static StorageTypeDictionary New()
    {
        return new StorageTypeDictionary();
    }

    #endregion

    #region Overrides

    public override string ToString()
    {
        return $"StorageTypeDictionary[Types={TypeCount}, NextTypeId={_nextTypeId}]";
    }

    #endregion
}
