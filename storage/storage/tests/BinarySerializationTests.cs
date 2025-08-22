using System;
using System.IO;
using System.Text;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Serialization;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Unit tests for binary serialization components.
/// </summary>
public class BinarySerializationTests
{
    [Fact]
    public void StorageBinaryWriter_CanWritePrimitiveTypes()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = StorageBinaryWriter.Create(stream);

        // Act
        writer.WriteBoolean(true);
        writer.WriteByte(255);
        writer.WriteSByte(-128);
        writer.WriteInt16(-32768);
        writer.WriteUInt16(65535);
        writer.WriteInt32(-2147483648);
        writer.WriteUInt32(4294967295);
        writer.WriteInt64(-9223372036854775808);
        writer.WriteUInt64(18446744073709551615);
        writer.WriteSingle(3.14159f);
        writer.WriteDouble(2.718281828459045);
        writer.WriteDecimal(123.456789m);
        writer.WriteChar('A');
        writer.WriteString("Hello, World!");
        writer.WriteDateTime(new DateTime(2023, 12, 25, 10, 30, 45));
        writer.WriteGuid(Guid.Parse("12345678-1234-5678-9abc-123456789abc"));

        // Assert
        Assert.True(stream.Length > 0);
        Assert.Equal(stream.Length, writer.Position);
    }

    [Fact]
    public void StorageBinaryReader_CanReadPrimitiveTypes()
    {
        // Arrange
        var testBoolean = true;
        var testByte = (byte)255;
        var testSByte = (sbyte)-128;
        var testInt16 = (short)-32768;
        var testUInt16 = (ushort)65535;
        var testInt32 = -2147483648;
        var testUInt32 = 4294967295U;
        var testInt64 = -9223372036854775808L;
        var testUInt64 = 18446744073709551615UL;
        var testSingle = 3.14159f;
        var testDouble = 2.718281828459045;
        var testDecimal = 123.456789m;
        var testChar = 'A';
        var testString = "Hello, World!";
        var testDateTime = new DateTime(2023, 12, 25, 10, 30, 45);
        var testGuid = Guid.Parse("12345678-1234-5678-9abc-123456789abc");

        using var stream = new MemoryStream();
        
        // Write data
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteBoolean(testBoolean);
            writer.WriteByte(testByte);
            writer.WriteSByte(testSByte);
            writer.WriteInt16(testInt16);
            writer.WriteUInt16(testUInt16);
            writer.WriteInt32(testInt32);
            writer.WriteUInt32(testUInt32);
            writer.WriteInt64(testInt64);
            writer.WriteUInt64(testUInt64);
            writer.WriteSingle(testSingle);
            writer.WriteDouble(testDouble);
            writer.WriteDecimal(testDecimal);
            writer.WriteChar(testChar);
            writer.WriteString(testString);
            writer.WriteDateTime(testDateTime);
            writer.WriteGuid(testGuid);
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read data back
        using var reader = StorageBinaryReader.Create(stream, true);

        // Assert
        Assert.Equal(testBoolean, reader.ReadBoolean());
        Assert.Equal(testByte, reader.ReadByte());
        Assert.Equal(testSByte, reader.ReadSByte());
        Assert.Equal(testInt16, reader.ReadInt16());
        Assert.Equal(testUInt16, reader.ReadUInt16());
        Assert.Equal(testInt32, reader.ReadInt32());
        Assert.Equal(testUInt32, reader.ReadUInt32());
        Assert.Equal(testInt64, reader.ReadInt64());
        Assert.Equal(testUInt64, reader.ReadUInt64());
        Assert.Equal(testSingle, reader.ReadSingle());
        Assert.Equal(testDouble, reader.ReadDouble());
        Assert.Equal(testDecimal, reader.ReadDecimal());
        Assert.Equal(testChar, reader.ReadChar());
        Assert.Equal(testString, reader.ReadString());
        Assert.Equal(testDateTime, reader.ReadDateTime());
        Assert.Equal(testGuid, reader.ReadGuid());
    }

    [Fact]
    public void CanWriteAndReadNullString()
    {
        // Arrange
        using var stream = new MemoryStream();
        
        // Write null string
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteString(null);
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read null string back
        using var reader = StorageBinaryReader.Create(stream, true);
        var result = reader.ReadString();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CanWriteAndReadEmptyString()
    {
        // Arrange
        using var stream = new MemoryStream();
        
        // Write empty string
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteString(string.Empty);
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read empty string back
        using var reader = StorageBinaryReader.Create(stream, true);
        var result = reader.ReadString();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CanWriteAndReadByteArray()
    {
        // Arrange
        var testArray = new byte[] { 1, 2, 3, 4, 5, 255, 0, 128 };
        using var stream = new MemoryStream();
        
        // Write byte array
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteByteArray(testArray);
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read byte array back
        using var reader = StorageBinaryReader.Create(stream, true);
        var result = reader.ReadByteArray();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testArray.Length, result.Length);
        Assert.Equal(testArray, result);
    }

    [Fact]
    public void CanWriteAndReadNullByteArray()
    {
        // Arrange
        using var stream = new MemoryStream();
        
        // Write null byte array
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteByteArray(null);
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read null byte array back
        using var reader = StorageBinaryReader.Create(stream, true);
        var result = reader.ReadByteArray();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CanWriteAndReadVarInt()
    {
        // Arrange
        var testValues = new long[] { 0, 1, -1, 127, -128, 128, -129, 16383, -16384, 16384, -16385, long.MaxValue, long.MinValue };
        using var stream = new MemoryStream();
        
        // Write variable integers
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            foreach (var value in testValues)
            {
                writer.WriteVarInt(value);
            }
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read variable integers back
        using var reader = StorageBinaryReader.Create(stream, true);
        var results = new long[testValues.Length];
        for (int i = 0; i < testValues.Length; i++)
        {
            results[i] = reader.ReadVarInt();
        }

        // Assert
        Assert.Equal(testValues, results);
    }

    [Fact]
    public void CanWriteAndReadVarUInt()
    {
        // Arrange
        var testValues = new ulong[] { 0, 1, 127, 128, 16383, 16384, ulong.MaxValue };
        using var stream = new MemoryStream();
        
        // Write variable unsigned integers
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            foreach (var value in testValues)
            {
                writer.WriteVarUInt(value);
            }
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read variable unsigned integers back
        using var reader = StorageBinaryReader.Create(stream, true);
        var results = new ulong[testValues.Length];
        for (int i = 0; i < testValues.Length; i++)
        {
            results[i] = reader.ReadVarUInt();
        }

        // Assert
        Assert.Equal(testValues, results);
    }

    [Fact]
    public void CanWriteAndReadObjectReferences()
    {
        // Arrange
        var testObjectIds = new long[] { 0, 1, 1000, -1, long.MaxValue };
        using var stream = new MemoryStream();
        
        // Write object references
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            foreach (var objectId in testObjectIds)
            {
                writer.WriteObjectReference(objectId);
            }
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read object references back
        using var reader = StorageBinaryReader.Create(stream, true);
        var results = new long[testObjectIds.Length];
        for (int i = 0; i < testObjectIds.Length; i++)
        {
            results[i] = reader.ReadObjectReference();
        }

        // Assert
        Assert.Equal(testObjectIds, results);
    }

    [Fact]
    public void CanWriteAndReadTypeReferences()
    {
        // Arrange
        var testTypeIds = new long[] { 1, 2, 3, 1000, 9999 };
        using var stream = new MemoryStream();
        
        // Write type references
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            foreach (var typeId in testTypeIds)
            {
                writer.WriteTypeReference(typeId);
            }
        }

        // Reset stream position
        stream.Position = 0;

        // Act - Read type references back
        using var reader = StorageBinaryReader.Create(stream, true);
        var results = new long[testTypeIds.Length];
        for (int i = 0; i < testTypeIds.Length; i++)
        {
            results[i] = reader.ReadTypeReference();
        }

        // Assert
        Assert.Equal(testTypeIds, results);
    }

    [Fact]
    public void WriterPosition_UpdatesCorrectly()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = StorageBinaryWriter.Create(stream);

        // Act & Assert
        Assert.Equal(0, writer.Position);
        
        writer.WriteInt32(42);
        Assert.Equal(4, writer.Position);
        
        writer.WriteString("test");
        Assert.True(writer.Position > 4); // String includes length prefix
        
        var positionAfterString = writer.Position;
        writer.WriteBoolean(true);
        Assert.Equal(positionAfterString + 1, writer.Position);
    }

    [Fact]
    public void ReaderPosition_UpdatesCorrectly()
    {
        // Arrange
        using var stream = new MemoryStream();
        
        // Write some data
        using (var writer = StorageBinaryWriter.Create(stream, true))
        {
            writer.WriteInt32(42);
            writer.WriteString("test");
            writer.WriteBoolean(true);
        }

        // Reset stream position
        stream.Position = 0;

        // Act & Assert
        using var reader = StorageBinaryReader.Create(stream);
        
        Assert.Equal(0, reader.Position);
        
        reader.ReadInt32();
        Assert.Equal(4, reader.Position);
        
        reader.ReadString();
        var positionAfterString = reader.Position;
        Assert.True(positionAfterString > 4);
        
        reader.ReadBoolean();
        Assert.Equal(positionAfterString + 1, reader.Position);
    }

    [Fact]
    public void ReadingBeyondStream_ThrowsEndOfStreamException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        using var reader = StorageBinaryReader.Create(stream);

        // Act - Read all available bytes
        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte();

        // Assert - Reading beyond should throw
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }

    [Fact]
    public void FlushWriter_DoesNotThrow()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = StorageBinaryWriter.Create(stream);

        // Act & Assert - Should not throw
        writer.WriteInt32(42);
        writer.Flush();
        
        Assert.True(stream.Length > 0);
    }
}
