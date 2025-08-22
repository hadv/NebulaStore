using System;
using System.IO;
using System.Text;

namespace NebulaStore.Storage.Embedded.Types.Serialization;

/// <summary>
/// Default implementation of IBinaryWriter for storage operations.
/// Uses little-endian byte order for consistency across platforms.
/// </summary>
public class StorageBinaryWriter : IBinaryWriter
{
    #region Private Fields

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _buffer = new byte[16]; // Buffer for primitive types
    private bool _disposed = false;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageBinaryWriter class.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    public StorageBinaryWriter(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    #endregion

    #region IBinaryWriter Implementation

    public long Position => _stream.Position;

    public Stream BaseStream => _stream;

    public void WriteBoolean(bool value)
    {
        _buffer[0] = value ? (byte)1 : (byte)0;
        _stream.Write(_buffer, 0, 1);
    }

    public void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteSByte(sbyte value)
    {
        _stream.WriteByte((byte)value);
    }

    public void WriteInt16(short value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _stream.Write(_buffer, 0, 2);
    }

    public void WriteUInt16(ushort value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _stream.Write(_buffer, 0, 2);
    }

    public void WriteInt32(int value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _buffer[2] = (byte)(value >> 16);
        _buffer[3] = (byte)(value >> 24);
        _stream.Write(_buffer, 0, 4);
    }

    public void WriteUInt32(uint value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _buffer[2] = (byte)(value >> 16);
        _buffer[3] = (byte)(value >> 24);
        _stream.Write(_buffer, 0, 4);
    }

    public void WriteInt64(long value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _buffer[2] = (byte)(value >> 16);
        _buffer[3] = (byte)(value >> 24);
        _buffer[4] = (byte)(value >> 32);
        _buffer[5] = (byte)(value >> 40);
        _buffer[6] = (byte)(value >> 48);
        _buffer[7] = (byte)(value >> 56);
        _stream.Write(_buffer, 0, 8);
    }

    public void WriteUInt64(ulong value)
    {
        _buffer[0] = (byte)value;
        _buffer[1] = (byte)(value >> 8);
        _buffer[2] = (byte)(value >> 16);
        _buffer[3] = (byte)(value >> 24);
        _buffer[4] = (byte)(value >> 32);
        _buffer[5] = (byte)(value >> 40);
        _buffer[6] = (byte)(value >> 48);
        _buffer[7] = (byte)(value >> 56);
        _stream.Write(_buffer, 0, 8);
    }

    public void WriteSingle(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _stream.Write(bytes, 0, 4);
    }

    public void WriteDouble(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _stream.Write(bytes, 0, 8);
    }

    public void WriteDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        WriteInt32(bits[0]);
        WriteInt32(bits[1]);
        WriteInt32(bits[2]);
        WriteInt32(bits[3]);
    }

    public void WriteChar(char value)
    {
        WriteUInt16(value);
    }

    public void WriteString(string? value)
    {
        if (value == null)
        {
            WriteVarInt(-1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteDateTime(DateTime value)
    {
        WriteInt64(value.ToBinary());
    }

    public void WriteDateTimeOffset(DateTimeOffset value)
    {
        WriteInt64(value.Ticks);
        WriteInt64(value.Offset.Ticks);
    }

    public void WriteTimeSpan(TimeSpan value)
    {
        WriteInt64(value.Ticks);
    }

    public void WriteGuid(Guid value)
    {
        var bytes = value.ToByteArray();
        _stream.Write(bytes, 0, 16);
    }

    public void WriteByteArray(byte[]? value)
    {
        if (value == null)
        {
            WriteVarInt(-1);
            return;
        }

        WriteVarInt(value.Length);
        _stream.Write(value, 0, value.Length);
    }

    public void WriteBytes(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    public void WriteObjectReference(long objectId)
    {
        WriteVarInt(objectId);
    }

    public void WriteTypeReference(long typeId)
    {
        WriteVarInt(typeId);
    }

    public void WriteVarInt(long value)
    {
        WriteVarUInt((ulong)((value << 1) ^ (value >> 63))); // ZigZag encoding
    }

    public void WriteVarUInt(ulong value)
    {
        // LEB128 encoding
        while (value >= 0x80)
        {
            _stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        _stream.WriteByte((byte)value);
    }

    public void Flush()
    {
        _stream.Flush();
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && !_leaveOpen)
            {
                _stream?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageBinaryWriter for the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    /// <returns>A new StorageBinaryWriter instance.</returns>
    public static StorageBinaryWriter Create(Stream stream, bool leaveOpen = false)
    {
        return new StorageBinaryWriter(stream, leaveOpen);
    }

    #endregion
}
