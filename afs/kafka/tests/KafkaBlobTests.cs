using System;
using Xunit;

namespace NebulaStore.Afs.Kafka.Tests;

/// <summary>
/// Unit tests for KafkaBlob.
/// </summary>
public class KafkaBlobTests
{
    [Fact]
    public void New_ValidParameters_CreatesBlob()
    {
        // Arrange & Act
        var blob = KafkaBlob.New("test-topic", 0, 100, 0, 999);

        // Assert
        Assert.Equal("test-topic", blob.Topic);
        Assert.Equal(0, blob.Partition);
        Assert.Equal(100, blob.Offset);
        Assert.Equal(0, blob.Start);
        Assert.Equal(999, blob.End);
        Assert.Equal(1000, blob.Size);
    }

    [Fact]
    public void New_NullTopic_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New(null!, 0, 0, 0, 100));
    }

    [Fact]
    public void New_EmptyTopic_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New("", 0, 0, 0, 100));
    }

    [Fact]
    public void New_NegativePartition_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New("topic", -1, 0, 0, 100));
    }

    [Fact]
    public void New_NegativeOffset_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New("topic", 0, -1, 0, 100));
    }

    [Fact]
    public void New_NegativeStart_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New("topic", 0, 0, -1, 100));
    }

    [Fact]
    public void New_EndBeforeStart_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.New("topic", 0, 0, 100, 50));
    }

    [Fact]
    public void Size_CalculatesCorrectly()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.Equal(100, blob.Size);
    }

    [Fact]
    public void ToBytes_SerializesCorrectly()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 5, 1000, 2000, 2999);

        // Act
        var bytes = blob.ToBytes();

        // Assert
        Assert.Equal(28, bytes.Length);
    }

    [Fact]
    public void FromBytes_DeserializesCorrectly()
    {
        // Arrange
        var original = KafkaBlob.New("topic", 5, 1000, 2000, 2999);
        var bytes = original.ToBytes();

        // Act
        var deserialized = KafkaBlob.FromBytes("topic", bytes);

        // Assert
        Assert.Equal(original.Topic, deserialized.Topic);
        Assert.Equal(original.Partition, deserialized.Partition);
        Assert.Equal(original.Offset, deserialized.Offset);
        Assert.Equal(original.Start, deserialized.Start);
        Assert.Equal(original.End, deserialized.End);
        Assert.Equal(original.Size, deserialized.Size);
    }

    [Fact]
    public void FromBytes_InvalidLength_ThrowsArgumentException()
    {
        // Arrange
        var invalidBytes = new byte[20];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.FromBytes("topic", invalidBytes));
    }

    [Fact]
    public void FromBytes_NullBytes_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaBlob.FromBytes("topic", null!));
    }

    [Fact]
    public void Contains_PositionInRange_ReturnsTrue()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.True(blob.Contains(100));
        Assert.True(blob.Contains(150));
        Assert.True(blob.Contains(199));
    }

    [Fact]
    public void Contains_PositionOutOfRange_ReturnsFalse()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.False(blob.Contains(99));
        Assert.False(blob.Contains(200));
    }

    [Fact]
    public void Overlaps_RangeOverlaps_ReturnsTrue()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.True(blob.Overlaps(50, 150));   // Overlaps start
        Assert.True(blob.Overlaps(150, 250));  // Overlaps end
        Assert.True(blob.Overlaps(100, 199));  // Exact match
        Assert.True(blob.Overlaps(120, 180));  // Contained within
        Assert.True(blob.Overlaps(50, 250));   // Contains blob
    }

    [Fact]
    public void Overlaps_RangeDoesNotOverlap_ReturnsFalse()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.False(blob.Overlaps(0, 99));    // Before
        Assert.False(blob.Overlaps(200, 300)); // After
    }

    [Fact]
    public void GetBlobOffset_ValidPosition_ReturnsCorrectOffset()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.Equal(0, blob.GetBlobOffset(100));
        Assert.Equal(50, blob.GetBlobOffset(150));
        Assert.Equal(99, blob.GetBlobOffset(199));
    }

    [Fact]
    public void GetBlobOffset_InvalidPosition_ThrowsArgumentException()
    {
        // Arrange
        var blob = KafkaBlob.New("topic", 0, 0, 100, 199);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => blob.GetBlobOffset(99));
        Assert.Throws<ArgumentException>(() => blob.GetBlobOffset(200));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var blob = KafkaBlob.New("test-topic", 5, 1000, 2000, 2999);

        // Act
        var str = blob.ToString();

        // Assert
        Assert.Contains("test-topic", str);
        Assert.Contains("5", str);
        Assert.Contains("1000", str);
        Assert.Contains("2000", str);
        Assert.Contains("2999", str);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 1)]
    [InlineData(0, 100, 0, 999, 1000)]
    [InlineData(1, 200, 1000, 1999, 1000)]
    [InlineData(2, 300, 2000, 3999, 2000)]
    public void RoundTrip_Serialization_PreservesData(int partition, long offset, long start, long end, long expectedSize)
    {
        // Arrange
        var original = KafkaBlob.New("topic", partition, offset, start, end);

        // Act
        var bytes = original.ToBytes();
        var deserialized = KafkaBlob.FromBytes("topic", bytes);

        // Assert
        Assert.Equal(original.Partition, deserialized.Partition);
        Assert.Equal(original.Offset, deserialized.Offset);
        Assert.Equal(original.Start, deserialized.Start);
        Assert.Equal(original.End, deserialized.End);
        Assert.Equal(expectedSize, deserialized.Size);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var blob1 = KafkaBlob.New("topic", 0, 100, 0, 999);
        var blob2 = KafkaBlob.New("topic", 0, 100, 0, 999);

        // Act & Assert
        Assert.Equal(blob1, blob2);
        Assert.True(blob1 == blob2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var blob1 = KafkaBlob.New("topic", 0, 100, 0, 999);
        var blob2 = KafkaBlob.New("topic", 0, 101, 0, 999);

        // Act & Assert
        Assert.NotEqual(blob1, blob2);
        Assert.True(blob1 != blob2);
    }
}

