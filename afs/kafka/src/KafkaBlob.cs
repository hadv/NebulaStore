using System;

namespace NebulaStore.Afs.Kafka;

/// <summary>
/// Represents metadata for a blob stored in Kafka.
/// Each blob corresponds to a Kafka message containing a chunk of file data.
/// </summary>
/// <remarks>
/// Blobs are serialized to 28 bytes for storage in the index topic:
/// - partition (4 bytes)
/// - offset (8 bytes)
/// - start position (8 bytes)
/// - end position (8 bytes)
/// </remarks>
public record KafkaBlob
{
    /// <summary>
    /// Gets the Kafka topic name where this blob is stored.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the Kafka partition number.
    /// </summary>
    public int Partition { get; init; }

    /// <summary>
    /// Gets the Kafka offset within the partition.
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Gets the logical start position of this blob within the file.
    /// </summary>
    public long Start { get; init; }

    /// <summary>
    /// Gets the logical end position of this blob within the file (inclusive).
    /// </summary>
    public long End { get; init; }

    /// <summary>
    /// Gets the size of this blob in bytes.
    /// </summary>
    public long Size => End - Start + 1;

    /// <summary>
    /// Creates a new KafkaBlob instance.
    /// </summary>
    /// <param name="topic">The Kafka topic name</param>
    /// <param name="partition">The partition number</param>
    /// <param name="offset">The offset within the partition</param>
    /// <param name="start">The logical start position in the file</param>
    /// <param name="end">The logical end position in the file (inclusive)</param>
    /// <returns>A new KafkaBlob instance</returns>
    /// <exception cref="ArgumentException">Thrown if parameters are invalid</exception>
    public static KafkaBlob New(string topic, int partition, long offset, long start, long end)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty", nameof(topic));
        
        if (partition < 0)
            throw new ArgumentException("Partition must be non-negative", nameof(partition));
        
        if (offset < 0)
            throw new ArgumentException("Offset must be non-negative", nameof(offset));
        
        if (start < 0)
            throw new ArgumentException("Start position must be non-negative", nameof(start));
        
        if (end < start)
            throw new ArgumentException("End position must be >= start position", nameof(end));

        return new KafkaBlob
        {
            Topic = topic,
            Partition = partition,
            Offset = offset,
            Start = start,
            End = end
        };
    }

    /// <summary>
    /// Serializes this blob to a 28-byte array for storage in the index topic.
    /// </summary>
    /// <returns>A 28-byte array containing the blob metadata</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[28];
        
        // Write partition (4 bytes, big-endian)
        bytes[0] = (byte)(Partition >> 24);
        bytes[1] = (byte)(Partition >> 16);
        bytes[2] = (byte)(Partition >> 8);
        bytes[3] = (byte)Partition;
        
        // Write offset (8 bytes, big-endian)
        bytes[4] = (byte)(Offset >> 56);
        bytes[5] = (byte)(Offset >> 48);
        bytes[6] = (byte)(Offset >> 40);
        bytes[7] = (byte)(Offset >> 32);
        bytes[8] = (byte)(Offset >> 24);
        bytes[9] = (byte)(Offset >> 16);
        bytes[10] = (byte)(Offset >> 8);
        bytes[11] = (byte)Offset;
        
        // Write start (8 bytes, big-endian)
        bytes[12] = (byte)(Start >> 56);
        bytes[13] = (byte)(Start >> 48);
        bytes[14] = (byte)(Start >> 40);
        bytes[15] = (byte)(Start >> 32);
        bytes[16] = (byte)(Start >> 24);
        bytes[17] = (byte)(Start >> 16);
        bytes[18] = (byte)(Start >> 8);
        bytes[19] = (byte)Start;
        
        // Write end (8 bytes, big-endian)
        bytes[20] = (byte)(End >> 56);
        bytes[21] = (byte)(End >> 48);
        bytes[22] = (byte)(End >> 40);
        bytes[23] = (byte)(End >> 32);
        bytes[24] = (byte)(End >> 24);
        bytes[25] = (byte)(End >> 16);
        bytes[26] = (byte)(End >> 8);
        bytes[27] = (byte)End;
        
        return bytes;
    }

    /// <summary>
    /// Deserializes a KafkaBlob from a 28-byte array.
    /// </summary>
    /// <param name="topic">The topic name for this blob</param>
    /// <param name="bytes">The 28-byte array containing blob metadata</param>
    /// <returns>A KafkaBlob instance</returns>
    /// <exception cref="ArgumentException">Thrown if bytes array is not 28 bytes</exception>
    public static KafkaBlob FromBytes(string topic, byte[] bytes)
    {
        if (bytes == null || bytes.Length != 28)
            throw new ArgumentException("Blob metadata must be exactly 28 bytes", nameof(bytes));

        // Read partition (4 bytes, big-endian)
        int partition = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        
        // Read offset (8 bytes, big-endian)
        long offset = ((long)bytes[4] << 56) | ((long)bytes[5] << 48) | ((long)bytes[6] << 40) | ((long)bytes[7] << 32) |
                      ((long)bytes[8] << 24) | ((long)bytes[9] << 16) | ((long)bytes[10] << 8) | bytes[11];
        
        // Read start (8 bytes, big-endian)
        long start = ((long)bytes[12] << 56) | ((long)bytes[13] << 48) | ((long)bytes[14] << 40) | ((long)bytes[15] << 32) |
                     ((long)bytes[16] << 24) | ((long)bytes[17] << 16) | ((long)bytes[18] << 8) | bytes[19];
        
        // Read end (8 bytes, big-endian)
        long end = ((long)bytes[20] << 56) | ((long)bytes[21] << 48) | ((long)bytes[22] << 40) | ((long)bytes[23] << 32) |
                   ((long)bytes[24] << 24) | ((long)bytes[25] << 16) | ((long)bytes[26] << 8) | bytes[27];

        return new KafkaBlob
        {
            Topic = topic,
            Partition = partition,
            Offset = offset,
            Start = start,
            End = end
        };
    }

    /// <summary>
    /// Checks if this blob contains the specified file position.
    /// </summary>
    /// <param name="position">The file position to check</param>
    /// <returns>True if this blob contains the position</returns>
    public bool Contains(long position)
    {
        return position >= Start && position <= End;
    }

    /// <summary>
    /// Checks if this blob overlaps with the specified range.
    /// </summary>
    /// <param name="rangeStart">The start of the range</param>
    /// <param name="rangeEnd">The end of the range (inclusive)</param>
    /// <returns>True if this blob overlaps with the range</returns>
    public bool Overlaps(long rangeStart, long rangeEnd)
    {
        return Start <= rangeEnd && End >= rangeStart;
    }

    /// <summary>
    /// Gets the offset within this blob for a given file position.
    /// </summary>
    /// <param name="filePosition">The file position</param>
    /// <returns>The offset within this blob's data</returns>
    public long GetBlobOffset(long filePosition)
    {
        if (!Contains(filePosition))
            throw new ArgumentException($"Position {filePosition} is not within blob range [{Start}, {End}]", nameof(filePosition));
        
        return filePosition - Start;
    }

    public override string ToString()
    {
        return $"KafkaBlob[Topic={Topic}, Partition={Partition}, Offset={Offset}, Range=[{Start}, {End}], Size={Size}]";
    }
}

