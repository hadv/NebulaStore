using System;
using System.IO;
using System.Text;

namespace NebulaStore.Storage.Embedded.Types.Serialization;

/// <summary>
/// Default implementation of IBinaryReader for storage operations.
/// Uses little-endian byte order for consistency across platforms.
/// </summary>
public class StorageBinaryReader : IBinaryReader
{
    #region Private Fields

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _buffer = new byte[16]; // Buffer for primitive types
    private bool _disposed = false;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageBinaryReader class.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    public StorageBinaryReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    #endregion

    #region IBinaryReader Implementation

    public long Position => _stream.Position;

    public Stream BaseStream => _stream;

    public bool ReadBoolean()
    {
        var value = _stream.ReadByte();
        if (value == -1)
            throw new EndOfStreamException();
        return value != 0;
    }

    public byte ReadByte()
    {
        var value = _stream.ReadByte();
        if (value == -1)
            throw new EndOfStreamException();
        return (byte)value;
    }

    public sbyte ReadSByte()
    {
        return (sbyte)ReadByte();
    }

    public short ReadInt16()
    {
        FillBuffer(2);
        return (short)(_buffer[0] | (_buffer[1] << 8));
    }

    public ushort ReadUInt16()
    {
        FillBuffer(2);
        return (ushort)(_buffer[0] | (_buffer[1] << 8));
    }

    public int ReadInt32()
    {
        FillBuffer(4);
        return _buffer[0] | (_buffer[1] << 8) | (_buffer[2] << 16) | (_buffer[3] << 24);
    }

    public uint ReadUInt32()
    {
        FillBuffer(4);
        return (uint)(_buffer[0] | (_buffer[1] << 8) | (_buffer[2] << 16) | (_buffer[3] << 24));
    }

    public long ReadInt64()
    {
        FillBuffer(8);
        uint lo = (uint)(_buffer[0] | (_buffer[1] << 8) | (_buffer[2] << 16) | (_buffer[3] << 24));
        uint hi = (uint)(_buffer[4] | (_buffer[5] << 8) | (_buffer[6] << 16) | (_buffer[7] << 24));
        return (long)((ulong)hi << 32) | lo;
    }

    public ulong ReadUInt64()
    {
        FillBuffer(8);
        uint lo = (uint)(_buffer[0] | (_buffer[1] << 8) | (_buffer[2] << 16) | (_buffer[3] << 24));
        uint hi = (uint)(_buffer[4] | (_buffer[5] << 8) | (_buffer[6] << 16) | (_buffer[7] << 24));
        return ((ulong)hi << 32) | lo;
    }

    public float ReadSingle()
    {
        FillBuffer(4);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(_buffer, 0, 4);
        }
        return BitConverter.ToSingle(_buffer, 0);
    }

    public double ReadDouble()
    {
        FillBuffer(8);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(_buffer, 0, 8);
        }
        return BitConverter.ToDouble(_buffer, 0);
    }

    public decimal ReadDecimal()
    {
        var bits = new int[4];
        bits[0] = ReadInt32();
        bits[1] = ReadInt32();
        bits[2] = ReadInt32();
        bits[3] = ReadInt32();
        return new decimal(bits);
    }

    public char ReadChar()
    {
        return (char)ReadUInt16();
    }

    public string? ReadString()
    {
        var length = ReadVarInt();
        if (length == -1)
            return null;

        if (length == 0)
            return string.Empty;

        var bytes = new byte[length];
        var bytesRead = ReadBytes(bytes, 0, (int)length);
        if (bytesRead != length)
            throw new EndOfStreamException();

        return Encoding.UTF8.GetString(bytes);
    }

    public DateTime ReadDateTime()
    {
        return DateTime.FromBinary(ReadInt64());
    }

    public DateTimeOffset ReadDateTimeOffset()
    {
        var ticks = ReadInt64();
        var offsetTicks = ReadInt64();
        return new DateTimeOffset(ticks, new TimeSpan(offsetTicks));
    }

    public TimeSpan ReadTimeSpan()
    {
        return new TimeSpan(ReadInt64());
    }

    public Guid ReadGuid()
    {
        FillBuffer(16);
        return new Guid(_buffer);
    }

    public byte[]? ReadByteArray()
    {
        var length = ReadVarInt();
        if (length == -1)
            return null;

        if (length == 0)
            return Array.Empty<byte>();

        var bytes = new byte[length];
        var bytesRead = ReadBytes(bytes, 0, (int)length);
        if (bytesRead != length)
            throw new EndOfStreamException();

        return bytes;
    }

    public int ReadBytes(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset >= buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        int totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            int bytesRead = _stream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
                break;
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }

    public long ReadObjectReference()
    {
        return ReadVarInt();
    }

    public long ReadTypeReference()
    {
        return ReadVarInt();
    }

    public long ReadVarInt()
    {
        var value = ReadVarUInt();
        return (long)(value >> 1) ^ -((long)value & 1); // ZigZag decoding
    }

    public ulong ReadVarUInt()
    {
        // LEB128 decoding
        ulong value = 0;
        int shift = 0;
        byte b;

        do
        {
            if (shift >= 64)
                throw new InvalidDataException("Variable-length integer is too long");

            b = ReadByte();
            value |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);

        return value;
    }

    #endregion

    #region Private Methods

    private void FillBuffer(int numBytes)
    {
        if (numBytes < 0 || numBytes > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(numBytes));

        int bytesRead = 0;
        while (bytesRead < numBytes)
        {
            int n = _stream.Read(_buffer, bytesRead, numBytes - bytesRead);
            if (n == 0)
                throw new EndOfStreamException();
            bytesRead += n;
        }
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
    /// Creates a new StorageBinaryReader for the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    /// <returns>A new StorageBinaryReader instance.</returns>
    public static StorageBinaryReader Create(Stream stream, bool leaveOpen = false)
    {
        return new StorageBinaryReader(stream, leaveOpen);
    }

    #endregion
}
