using System;
using System.Collections.Generic;

namespace NebulaStore.Core.Storage;

/// <summary>
/// Foundation interface for building embedded storage managers.
/// Provides a builder pattern for configuring all aspects of storage.
/// </summary>
public interface IEmbeddedStorageFoundation
{
    /// <summary>
    /// Sets the storage configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetConfiguration(IEmbeddedStorageConfiguration configuration);

    /// <summary>
    /// Sets the root object instance.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetRoot(object root);

    /// <summary>
    /// Sets the root object supplier function.
    /// </summary>
    /// <param name="rootSupplier">Function that supplies the root object</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetRootSupplier(Func<object> rootSupplier);

    /// <summary>
    /// Registers a custom type handler.
    /// </summary>
    /// <param name="typeHandler">The type handler to register</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation RegisterTypeHandler(ITypeHandler typeHandler);

    /// <summary>
    /// Registers multiple custom type handlers.
    /// </summary>
    /// <param name="typeHandlers">The type handlers to register</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation RegisterTypeHandlers(IEnumerable<ITypeHandler> typeHandlers);

    /// <summary>
    /// Sets the type evaluator for determining persistable types.
    /// </summary>
    /// <param name="typeEvaluator">The type evaluator function</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetTypeEvaluator(Func<Type, bool> typeEvaluator);

    /// <summary>
    /// Gets the current storage configuration.
    /// </summary>
    /// <returns>The storage configuration</returns>
    IEmbeddedStorageConfiguration GetConfiguration();

    /// <summary>
    /// Creates an embedded storage manager without starting it.
    /// </summary>
    /// <returns>A new embedded storage manager instance</returns>
    IEmbeddedStorageManager CreateEmbeddedStorageManager();

    /// <summary>
    /// Creates an embedded storage manager with an explicit root object without starting it.
    /// </summary>
    /// <param name="explicitRoot">The explicit root object</param>
    /// <returns>A new embedded storage manager instance</returns>
    IEmbeddedStorageManager CreateEmbeddedStorageManager(object? explicitRoot);

    /// <summary>
    /// Creates and starts an embedded storage manager.
    /// </summary>
    /// <returns>A started embedded storage manager instance</returns>
    IEmbeddedStorageManager Start();

    /// <summary>
    /// Creates and starts an embedded storage manager with an explicit root object.
    /// </summary>
    /// <param name="explicitRoot">The explicit root object</param>
    /// <returns>A started embedded storage manager instance</returns>
    IEmbeddedStorageManager Start(object? explicitRoot);
}

/// <summary>
/// Interface for type handlers that manage serialization of specific types.
/// </summary>
public interface ITypeHandler
{
    /// <summary>
    /// Gets the type this handler manages.
    /// </summary>
    Type HandledType { get; }

    /// <summary>
    /// Gets the type ID for this handler.
    /// </summary>
    long TypeId { get; }

    /// <summary>
    /// Serializes an object instance to bytes.
    /// </summary>
    /// <param name="instance">The object instance to serialize</param>
    /// <returns>The serialized bytes</returns>
    byte[] Serialize(object instance);

    /// <summary>
    /// Deserializes bytes to an object instance.
    /// </summary>
    /// <param name="data">The serialized data</param>
    /// <returns>The deserialized object instance</returns>
    object Deserialize(byte[] data);

    /// <summary>
    /// Gets the serialized length of an object instance.
    /// </summary>
    /// <param name="instance">The object instance</param>
    /// <returns>The serialized length in bytes</returns>
    long GetSerializedLength(object instance);

    /// <summary>
    /// Gets a value indicating whether this type handler can handle the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if this handler can handle the type</returns>
    bool CanHandle(Type type);
}

/// <summary>
/// Interface for type handler registries.
/// </summary>
public interface ITypeHandlerRegistry
{
    /// <summary>
    /// Registers a type handler.
    /// </summary>
    /// <param name="typeHandler">The type handler to register</param>
    void RegisterTypeHandler(ITypeHandler typeHandler);

    /// <summary>
    /// Gets a type handler for the specified type.
    /// </summary>
    /// <param name="type">The type to get a handler for</param>
    /// <returns>The type handler, or null if none found</returns>
    ITypeHandler? GetTypeHandler(Type type);

    /// <summary>
    /// Gets a type handler for the specified type ID.
    /// </summary>
    /// <param name="typeId">The type ID to get a handler for</param>
    /// <returns>The type handler, or null if none found</returns>
    ITypeHandler? GetTypeHandler(long typeId);

    /// <summary>
    /// Gets all registered type handlers.
    /// </summary>
    /// <returns>All registered type handlers</returns>
    IEnumerable<ITypeHandler> GetAllTypeHandlers();
}
