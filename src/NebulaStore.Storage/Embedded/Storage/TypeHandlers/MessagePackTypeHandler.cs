using System;
using MessagePack;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Generic type handler that uses MessagePack for serialization.
/// Can be used as a fallback for types that don't have specialized handlers.
/// </summary>
/// <typeparam name="T">The type to handle</typeparam>
public class MessagePackTypeHandler<T> : ITypeHandler
{
    private readonly long _typeId;

    /// <summary>
    /// Initializes a new instance of the MessagePackTypeHandler class.
    /// </summary>
    /// <param name="typeId">The unique type ID for this handler</param>
    public MessagePackTypeHandler(long typeId)
    {
        _typeId = typeId;
    }

    public Type HandledType => typeof(T);

    public long TypeId => _typeId;

    public byte[] Serialize(object instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (instance is not T typedInstance)
            throw new ArgumentException($"Instance must be of type {typeof(T).Name}", nameof(instance));

        return MessagePackSerializer.Serialize(typedInstance);
    }

    public object Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        return MessagePackSerializer.Deserialize<T>(data)!;
    }

    public long GetSerializedLength(object instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        // For MessagePack, we need to serialize to get the exact length
        // This could be optimized with a custom implementation
        return Serialize(instance).Length;
    }

    public bool CanHandle(Type type)
    {
        return type == typeof(T) || type.IsAssignableFrom(typeof(T));
    }
}

/// <summary>
/// Factory for creating MessagePack type handlers.
/// </summary>
public static class MessagePackTypeHandlerFactory
{
    private static long _nextTypeId = 1000; // Start from 1000 to avoid conflicts with built-in types

    /// <summary>
    /// Creates a new MessagePack type handler for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a handler for</typeparam>
    /// <returns>A new type handler instance</returns>
    public static ITypeHandler Create<T>()
    {
        return new MessagePackTypeHandler<T>(GetNextTypeId());
    }

    /// <summary>
    /// Creates a new MessagePack type handler for the specified type with a specific type ID.
    /// </summary>
    /// <typeparam name="T">The type to create a handler for</typeparam>
    /// <param name="typeId">The type ID to use</param>
    /// <returns>A new type handler instance</returns>
    public static ITypeHandler Create<T>(long typeId)
    {
        return new MessagePackTypeHandler<T>(typeId);
    }

    private static long GetNextTypeId()
    {
        return System.Threading.Interlocked.Increment(ref _nextTypeId);
    }
}

/// <summary>
/// Built-in type handlers for common .NET types.
/// </summary>
public static class BuiltInTypeHandlers
{
    /// <summary>
    /// Gets a type handler for strings.
    /// </summary>
    public static ITypeHandler String => new StringTypeHandler();

    /// <summary>
    /// Gets a type handler for integers.
    /// </summary>
    public static ITypeHandler Int32 => new Int32TypeHandler();

    /// <summary>
    /// Gets a type handler for long integers.
    /// </summary>
    public static ITypeHandler Int64 => new Int64TypeHandler();

    /// <summary>
    /// Gets a type handler for decimals.
    /// </summary>
    public static ITypeHandler Decimal => new DecimalTypeHandler();

    /// <summary>
    /// Gets a type handler for DateTime.
    /// </summary>
    public static ITypeHandler DateTime => new DateTimeTypeHandler();

    /// <summary>
    /// Gets all built-in type handlers.
    /// </summary>
    /// <returns>An array of all built-in type handlers</returns>
    public static ITypeHandler[] GetAll()
    {
        return new[]
        {
            String,
            Int32,
            Int64,
            Decimal,
            DateTime
        };
    }
}

/// <summary>
/// Specialized type handler for strings.
/// </summary>
internal class StringTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(string);
    public long TypeId => 1;

    public byte[] Serialize(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string", nameof(instance));

        return System.Text.Encoding.UTF8.GetBytes(str);
    }

    public object Deserialize(byte[] data)
    {
        return System.Text.Encoding.UTF8.GetString(data);
    }

    public long GetSerializedLength(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string", nameof(instance));

        return System.Text.Encoding.UTF8.GetByteCount(str);
    }

    public bool CanHandle(Type type) => type == typeof(string);
}

/// <summary>
/// Specialized type handler for 32-bit integers.
/// </summary>
internal class Int32TypeHandler : ITypeHandler
{
    public Type HandledType => typeof(int);
    public long TypeId => 2;

    public byte[] Serialize(object instance)
    {
        if (instance is not int value)
            throw new ArgumentException("Instance must be an int", nameof(instance));

        return BitConverter.GetBytes(value);
    }

    public object Deserialize(byte[] data)
    {
        return BitConverter.ToInt32(data, 0);
    }

    public long GetSerializedLength(object instance) => sizeof(int);

    public bool CanHandle(Type type) => type == typeof(int);
}

/// <summary>
/// Specialized type handler for 64-bit integers.
/// </summary>
internal class Int64TypeHandler : ITypeHandler
{
    public Type HandledType => typeof(long);
    public long TypeId => 3;

    public byte[] Serialize(object instance)
    {
        if (instance is not long value)
            throw new ArgumentException("Instance must be a long", nameof(instance));

        return BitConverter.GetBytes(value);
    }

    public object Deserialize(byte[] data)
    {
        return BitConverter.ToInt64(data, 0);
    }

    public long GetSerializedLength(object instance) => sizeof(long);

    public bool CanHandle(Type type) => type == typeof(long);
}

/// <summary>
/// Specialized type handler for decimals.
/// </summary>
internal class DecimalTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(decimal);
    public long TypeId => 4;

    public byte[] Serialize(object instance)
    {
        if (instance is not decimal value)
            throw new ArgumentException("Instance must be a decimal", nameof(instance));

        return MessagePackSerializer.Serialize(value);
    }

    public object Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<decimal>(data);
    }

    public long GetSerializedLength(object instance)
    {
        return Serialize(instance).Length;
    }

    public bool CanHandle(Type type) => type == typeof(decimal);
}

/// <summary>
/// Specialized type handler for DateTime.
/// </summary>
internal class DateTimeTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(DateTime);
    public long TypeId => 5;

    public byte[] Serialize(object instance)
    {
        if (instance is not DateTime value)
            throw new ArgumentException("Instance must be a DateTime", nameof(instance));

        return BitConverter.GetBytes(value.ToBinary());
    }

    public object Deserialize(byte[] data)
    {
        var binary = BitConverter.ToInt64(data, 0);
        return DateTime.FromBinary(binary);
    }

    public long GetSerializedLength(object instance) => sizeof(long);

    public bool CanHandle(Type type) => type == typeof(DateTime);
}
