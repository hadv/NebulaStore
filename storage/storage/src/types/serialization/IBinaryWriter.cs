using System;
using System.IO;

namespace NebulaStore.Storage.Embedded.Types.Serialization;

/// <summary>
/// Interface for binary writing operations in the storage system.
/// Provides methods for writing primitive types and object references.
/// </summary>
public interface IBinaryWriter : IDisposable
{
    /// <summary>
    /// Gets the current position in the output stream.
    /// </summary>
    long Position { get; }

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    Stream BaseStream { get; }

    /// <summary>
    /// Writes a boolean value.
    /// </summary>
    /// <param name="value">The boolean value to write.</param>
    void WriteBoolean(bool value);

    /// <summary>
    /// Writes a byte value.
    /// </summary>
    /// <param name="value">The byte value to write.</param>
    void WriteByte(byte value);

    /// <summary>
    /// Writes a signed byte value.
    /// </summary>
    /// <param name="value">The signed byte value to write.</param>
    void WriteSByte(sbyte value);

    /// <summary>
    /// Writes a 16-bit signed integer.
    /// </summary>
    /// <param name="value">The 16-bit signed integer to write.</param>
    void WriteInt16(short value);

    /// <summary>
    /// Writes a 16-bit unsigned integer.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer to write.</param>
    void WriteUInt16(ushort value);

    /// <summary>
    /// Writes a 32-bit signed integer.
    /// </summary>
    /// <param name="value">The 32-bit signed integer to write.</param>
    void WriteInt32(int value);

    /// <summary>
    /// Writes a 32-bit unsigned integer.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer to write.</param>
    void WriteUInt32(uint value);

    /// <summary>
    /// Writes a 64-bit signed integer.
    /// </summary>
    /// <param name="value">The 64-bit signed integer to write.</param>
    void WriteInt64(long value);

    /// <summary>
    /// Writes a 64-bit unsigned integer.
    /// </summary>
    /// <param name="value">The 64-bit unsigned integer to write.</param>
    void WriteUInt64(ulong value);

    /// <summary>
    /// Writes a single-precision floating-point number.
    /// </summary>
    /// <param name="value">The single-precision floating-point number to write.</param>
    void WriteSingle(float value);

    /// <summary>
    /// Writes a double-precision floating-point number.
    /// </summary>
    /// <param name="value">The double-precision floating-point number to write.</param>
    void WriteDouble(double value);

    /// <summary>
    /// Writes a decimal value.
    /// </summary>
    /// <param name="value">The decimal value to write.</param>
    void WriteDecimal(decimal value);

    /// <summary>
    /// Writes a character.
    /// </summary>
    /// <param name="value">The character to write.</param>
    void WriteChar(char value);

    /// <summary>
    /// Writes a string value with length prefix.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    void WriteString(string? value);

    /// <summary>
    /// Writes a DateTime value.
    /// </summary>
    /// <param name="value">The DateTime value to write.</param>
    void WriteDateTime(DateTime value);

    /// <summary>
    /// Writes a DateTimeOffset value.
    /// </summary>
    /// <param name="value">The DateTimeOffset value to write.</param>
    void WriteDateTimeOffset(DateTimeOffset value);

    /// <summary>
    /// Writes a TimeSpan value.
    /// </summary>
    /// <param name="value">The TimeSpan value to write.</param>
    void WriteTimeSpan(TimeSpan value);

    /// <summary>
    /// Writes a Guid value.
    /// </summary>
    /// <param name="value">The Guid value to write.</param>
    void WriteGuid(Guid value);

    /// <summary>
    /// Writes a byte array with length prefix.
    /// </summary>
    /// <param name="value">The byte array to write.</param>
    void WriteByteArray(byte[]? value);

    /// <summary>
    /// Writes raw bytes without length prefix.
    /// </summary>
    /// <param name="buffer">The buffer containing bytes to write.</param>
    /// <param name="offset">The offset in the buffer.</param>
    /// <param name="count">The number of bytes to write.</param>
    void WriteBytes(byte[] buffer, int offset, int count);

    /// <summary>
    /// Writes an object reference (object ID).
    /// </summary>
    /// <param name="objectId">The object ID to write.</param>
    void WriteObjectReference(long objectId);

    /// <summary>
    /// Writes a type reference (type ID).
    /// </summary>
    /// <param name="typeId">The type ID to write.</param>
    void WriteTypeReference(long typeId);

    /// <summary>
    /// Writes a variable-length integer using LEB128 encoding.
    /// </summary>
    /// <param name="value">The integer value to write.</param>
    void WriteVarInt(long value);

    /// <summary>
    /// Writes a variable-length unsigned integer using LEB128 encoding.
    /// </summary>
    /// <param name="value">The unsigned integer value to write.</param>
    void WriteVarUInt(ulong value);

    /// <summary>
    /// Flushes the underlying stream.
    /// </summary>
    void Flush();
}

/// <summary>
/// Interface for binary reading operations in the storage system.
/// Provides methods for reading primitive types and object references.
/// </summary>
public interface IBinaryReader : IDisposable
{
    /// <summary>
    /// Gets the current position in the input stream.
    /// </summary>
    long Position { get; }

    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    Stream BaseStream { get; }

    /// <summary>
    /// Reads a boolean value.
    /// </summary>
    /// <returns>The boolean value read from the stream.</returns>
    bool ReadBoolean();

    /// <summary>
    /// Reads a byte value.
    /// </summary>
    /// <returns>The byte value read from the stream.</returns>
    byte ReadByte();

    /// <summary>
    /// Reads a signed byte value.
    /// </summary>
    /// <returns>The signed byte value read from the stream.</returns>
    sbyte ReadSByte();

    /// <summary>
    /// Reads a 16-bit signed integer.
    /// </summary>
    /// <returns>The 16-bit signed integer read from the stream.</returns>
    short ReadInt16();

    /// <summary>
    /// Reads a 16-bit unsigned integer.
    /// </summary>
    /// <returns>The 16-bit unsigned integer read from the stream.</returns>
    ushort ReadUInt16();

    /// <summary>
    /// Reads a 32-bit signed integer.
    /// </summary>
    /// <returns>The 32-bit signed integer read from the stream.</returns>
    int ReadInt32();

    /// <summary>
    /// Reads a 32-bit unsigned integer.
    /// </summary>
    /// <returns>The 32-bit unsigned integer read from the stream.</returns>
    uint ReadUInt32();

    /// <summary>
    /// Reads a 64-bit signed integer.
    /// </summary>
    /// <returns>The 64-bit signed integer read from the stream.</returns>
    long ReadInt64();

    /// <summary>
    /// Reads a 64-bit unsigned integer.
    /// </summary>
    /// <returns>The 64-bit unsigned integer read from the stream.</returns>
    ulong ReadUInt64();

    /// <summary>
    /// Reads a single-precision floating-point number.
    /// </summary>
    /// <returns>The single-precision floating-point number read from the stream.</returns>
    float ReadSingle();

    /// <summary>
    /// Reads a double-precision floating-point number.
    /// </summary>
    /// <returns>The double-precision floating-point number read from the stream.</returns>
    double ReadDouble();

    /// <summary>
    /// Reads a decimal value.
    /// </summary>
    /// <returns>The decimal value read from the stream.</returns>
    decimal ReadDecimal();

    /// <summary>
    /// Reads a character.
    /// </summary>
    /// <returns>The character read from the stream.</returns>
    char ReadChar();

    /// <summary>
    /// Reads a string value with length prefix.
    /// </summary>
    /// <returns>The string value read from the stream.</returns>
    string? ReadString();

    /// <summary>
    /// Reads a DateTime value.
    /// </summary>
    /// <returns>The DateTime value read from the stream.</returns>
    DateTime ReadDateTime();

    /// <summary>
    /// Reads a DateTimeOffset value.
    /// </summary>
    /// <returns>The DateTimeOffset value read from the stream.</returns>
    DateTimeOffset ReadDateTimeOffset();

    /// <summary>
    /// Reads a TimeSpan value.
    /// </summary>
    /// <returns>The TimeSpan value read from the stream.</returns>
    TimeSpan ReadTimeSpan();

    /// <summary>
    /// Reads a Guid value.
    /// </summary>
    /// <returns>The Guid value read from the stream.</returns>
    Guid ReadGuid();

    /// <summary>
    /// Reads a byte array with length prefix.
    /// </summary>
    /// <returns>The byte array read from the stream.</returns>
    byte[]? ReadByteArray();

    /// <summary>
    /// Reads raw bytes into a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="offset">The offset in the buffer.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    int ReadBytes(byte[] buffer, int offset, int count);

    /// <summary>
    /// Reads an object reference (object ID).
    /// </summary>
    /// <returns>The object ID read from the stream.</returns>
    long ReadObjectReference();

    /// <summary>
    /// Reads a type reference (type ID).
    /// </summary>
    /// <returns>The type ID read from the stream.</returns>
    long ReadTypeReference();

    /// <summary>
    /// Reads a variable-length integer using LEB128 encoding.
    /// </summary>
    /// <returns>The integer value read from the stream.</returns>
    long ReadVarInt();

    /// <summary>
    /// Reads a variable-length unsigned integer using LEB128 encoding.
    /// </summary>
    /// <returns>The unsigned integer value read from the stream.</returns>
    ulong ReadVarUInt();
}
