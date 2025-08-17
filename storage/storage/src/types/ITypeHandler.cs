using System;
using System.Collections.Generic;

namespace NebulaStore.Storage;

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
    /// Determines whether this handler can handle the specified type.
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
