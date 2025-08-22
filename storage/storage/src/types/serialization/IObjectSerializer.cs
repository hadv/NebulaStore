using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Serialization;

/// <summary>
/// Interface for object serialization in the storage system.
/// Handles serialization and deserialization of objects to/from binary format.
/// </summary>
public interface IObjectSerializer
{
    /// <summary>
    /// Serializes an object to binary format.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="writer">The binary writer to write to.</param>
    /// <param name="context">The serialization context.</param>
    void Serialize(object obj, IBinaryWriter writer, ISerializationContext context);

    /// <summary>
    /// Deserializes an object from binary format.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="context">The deserialization context.</param>
    /// <returns>The deserialized object.</returns>
    object Deserialize(IBinaryReader reader, IDeserializationContext context);

    /// <summary>
    /// Gets the size in bytes required to serialize the specified object.
    /// </summary>
    /// <param name="obj">The object to measure.</param>
    /// <param name="context">The serialization context.</param>
    /// <returns>The size in bytes required for serialization.</returns>
    long GetSerializedSize(object obj, ISerializationContext context);

    /// <summary>
    /// Gets a value indicating whether this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if this serializer can handle the type.</returns>
    bool CanHandle(Type type);
}

/// <summary>
/// Interface for serialization context.
/// </summary>
public interface ISerializationContext
{
    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the object ID registry.
    /// </summary>
    IObjectIdRegistry ObjectIdRegistry { get; }

    /// <summary>
    /// Registers an object and returns its object ID.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    /// <returns>The object ID.</returns>
    long RegisterObject(object obj);

    /// <summary>
    /// Gets the object ID for the specified object.
    /// </summary>
    /// <param name="obj">The object to get the ID for.</param>
    /// <returns>The object ID, or -1 if not registered.</returns>
    long GetObjectId(object obj);

    /// <summary>
    /// Gets a value indicating whether the specified object is already registered.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object is registered.</returns>
    bool IsObjectRegistered(object obj);
}

/// <summary>
/// Interface for deserialization context.
/// </summary>
public interface IDeserializationContext
{
    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the object registry.
    /// </summary>
    IObjectRegistry ObjectRegistry { get; }

    /// <summary>
    /// Registers an object with the specified object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="obj">The object to register.</param>
    void RegisterObject(long objectId, object obj);

    /// <summary>
    /// Gets an object by its object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <returns>The object, or null if not found.</returns>
    object? GetObject(long objectId);

    /// <summary>
    /// Creates a placeholder for an object that will be resolved later.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="type">The object type.</param>
    /// <returns>The placeholder object.</returns>
    object CreatePlaceholder(long objectId, Type type);

    /// <summary>
    /// Resolves all placeholders after deserialization is complete.
    /// </summary>
    void ResolvePlaceholders();
}

/// <summary>
/// Interface for object ID registry.
/// </summary>
public interface IObjectIdRegistry
{
    /// <summary>
    /// Gets the next available object ID.
    /// </summary>
    /// <returns>The next object ID.</returns>
    long GetNextObjectId();

    /// <summary>
    /// Registers an object with the specified object ID.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    /// <param name="objectId">The object ID.</param>
    void RegisterObject(object obj, long objectId);

    /// <summary>
    /// Gets the object ID for the specified object.
    /// </summary>
    /// <param name="obj">The object to get the ID for.</param>
    /// <returns>The object ID, or -1 if not registered.</returns>
    long GetObjectId(object obj);

    /// <summary>
    /// Gets a value indicating whether the specified object is registered.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object is registered.</returns>
    bool IsObjectRegistered(object obj);

    /// <summary>
    /// Clears all registered objects.
    /// </summary>
    void Clear();
}

/// <summary>
/// Interface for object registry.
/// </summary>
public interface IObjectRegistry
{
    /// <summary>
    /// Registers an object with the specified object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="obj">The object to register.</param>
    void RegisterObject(long objectId, object obj);

    /// <summary>
    /// Gets an object by its object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <returns>The object, or null if not found.</returns>
    object? GetObject(long objectId);

    /// <summary>
    /// Gets a value indicating whether an object with the specified ID is registered.
    /// </summary>
    /// <param name="objectId">The object ID to check.</param>
    /// <returns>True if an object with the ID is registered.</returns>
    bool IsObjectRegistered(long objectId);

    /// <summary>
    /// Clears all registered objects.
    /// </summary>
    void Clear();
}

/// <summary>
/// Interface for type-specific serializers.
/// </summary>
/// <typeparam name="T">The type this serializer handles.</typeparam>
public interface ITypeSerializer<T> : IObjectSerializer
{
    /// <summary>
    /// Serializes an object of type T to binary format.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="writer">The binary writer to write to.</param>
    /// <param name="context">The serialization context.</param>
    void Serialize(T obj, IBinaryWriter writer, ISerializationContext context);

    /// <summary>
    /// Deserializes an object of type T from binary format.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="context">The deserialization context.</param>
    /// <returns>The deserialized object.</returns>
    T Deserialize(IBinaryReader reader, IDeserializationContext context);

    /// <summary>
    /// Gets the size in bytes required to serialize the specified object.
    /// </summary>
    /// <param name="obj">The object to measure.</param>
    /// <param name="context">The serialization context.</param>
    /// <returns>The size in bytes required for serialization.</returns>
    long GetSerializedSize(T obj, ISerializationContext context);
}

/// <summary>
/// Interface for serializer registry.
/// </summary>
public interface ISerializerRegistry
{
    /// <summary>
    /// Registers a serializer for the specified type.
    /// </summary>
    /// <param name="type">The type to register the serializer for.</param>
    /// <param name="serializer">The serializer to register.</param>
    void RegisterSerializer(Type type, IObjectSerializer serializer);

    /// <summary>
    /// Gets a serializer for the specified type.
    /// </summary>
    /// <param name="type">The type to get a serializer for.</param>
    /// <returns>The serializer for the type, or null if not found.</returns>
    IObjectSerializer? GetSerializer(Type type);

    /// <summary>
    /// Gets a value indicating whether a serializer is registered for the specified type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if a serializer is registered for the type.</returns>
    bool HasSerializer(Type type);

    /// <summary>
    /// Registers built-in serializers for common .NET types.
    /// </summary>
    void RegisterBuiltInSerializers();

    /// <summary>
    /// Gets all registered serializers.
    /// </summary>
    /// <returns>All registered serializers.</returns>
    IEnumerable<KeyValuePair<Type, IObjectSerializer>> GetAllSerializers();
}

/// <summary>
/// Interface for object reference resolver.
/// </summary>
public interface IObjectReferenceResolver
{
    /// <summary>
    /// Resolves an object reference by object ID.
    /// </summary>
    /// <param name="objectId">The object ID to resolve.</param>
    /// <returns>The resolved object, or null if not found.</returns>
    object? ResolveReference(long objectId);

    /// <summary>
    /// Adds a pending reference to be resolved later.
    /// </summary>
    /// <param name="objectId">The object ID to resolve.</param>
    /// <param name="callback">The callback to invoke when the object is resolved.</param>
    void AddPendingReference(long objectId, Action<object> callback);

    /// <summary>
    /// Resolves all pending references.
    /// </summary>
    void ResolvePendingReferences();
}

/// <summary>
/// Serialization constants.
/// </summary>
public static class SerializationConstants
{
    /// <summary>
    /// Magic number for storage files.
    /// </summary>
    public const uint MagicNumber = 0x4E454253; // "NEBS" in ASCII

    /// <summary>
    /// Current serialization format version.
    /// </summary>
    public const ushort FormatVersion = 1;

    /// <summary>
    /// Null object reference marker.
    /// </summary>
    public const long NullReference = 0;

    /// <summary>
    /// Invalid object ID marker.
    /// </summary>
    public const long InvalidObjectId = -1;

    /// <summary>
    /// Root object ID.
    /// </summary>
    public const long RootObjectId = 1;
}

/// <summary>
/// Serialization type codes for built-in types.
/// </summary>
public enum SerializationTypeCode : byte
{
    Null = 0,
    Boolean = 1,
    Byte = 2,
    SByte = 3,
    Int16 = 4,
    UInt16 = 5,
    Int32 = 6,
    UInt32 = 7,
    Int64 = 8,
    UInt64 = 9,
    Single = 10,
    Double = 11,
    Decimal = 12,
    Char = 13,
    String = 14,
    DateTime = 15,
    DateTimeOffset = 16,
    TimeSpan = 17,
    Guid = 18,
    ByteArray = 19,
    ObjectReference = 20,
    TypeReference = 21,
    Array = 22,
    Object = 23
}
