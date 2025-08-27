using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Represents the type of transaction log entry following Eclipse Store patterns.
/// </summary>
public enum TransactionLogEntryType : byte
{
    /// <summary>
    /// Store operation - storing new or updated objects.
    /// </summary>
    Store = 1,

    /// <summary>
    /// Create operation - creating new storage files.
    /// </summary>
    Create = 2,

    /// <summary>
    /// Transfer operation - moving data between files.
    /// </summary>
    Transfer = 3,

    /// <summary>
    /// Delete operation - deleting storage files.
    /// </summary>
    Delete = 4,

    /// <summary>
    /// Truncate operation - truncating storage files.
    /// </summary>
    Truncate = 5,

    /// <summary>
    /// Commit marker - marks successful transaction completion.
    /// </summary>
    Commit = 6,

    /// <summary>
    /// Rollback marker - marks transaction rollback.
    /// </summary>
    Rollback = 7
}

/// <summary>
/// Base class for transaction log entries following Eclipse Store patterns.
/// Provides ACID compliance and crash recovery capabilities.
/// </summary>
public abstract class TransactionLogEntry
{
    /// <summary>
    /// Gets the entry type.
    /// </summary>
    public abstract TransactionLogEntryType EntryType { get; }

    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    public long TransactionId { get; }

    /// <summary>
    /// Gets the timestamp when this entry was created.
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    public int ChannelIndex { get; }

    /// <summary>
    /// Gets the sequence number within the transaction.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Initializes a new instance of the TransactionLogEntry class.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="sequenceNumber">The sequence number.</param>
    protected TransactionLogEntry(long transactionId, long timestamp, int channelIndex, long sequenceNumber)
    {
        TransactionId = transactionId;
        Timestamp = timestamp;
        ChannelIndex = channelIndex;
        SequenceNumber = sequenceNumber;
    }

    /// <summary>
    /// Serializes the entry to bytes for storage.
    /// </summary>
    /// <returns>The serialized entry data.</returns>
    public abstract byte[] Serialize();

    /// <summary>
    /// Gets the size of the entry when serialized.
    /// </summary>
    /// <returns>The serialized size in bytes.</returns>
    public abstract int GetSerializedSize();
}

/// <summary>
/// Transaction log entry for store operations.
/// </summary>
public class StoreTransactionLogEntry : TransactionLogEntry
{
    public override TransactionLogEntryType EntryType => TransactionLogEntryType.Store;

    /// <summary>
    /// Gets the data file number.
    /// </summary>
    public long DataFileNumber { get; }

    /// <summary>
    /// Gets the offset in the data file.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the length of the stored data.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the object IDs that were stored.
    /// </summary>
    public IReadOnlyList<long> ObjectIds { get; }

    /// <summary>
    /// Initializes a new instance of the StoreTransactionLogEntry class.
    /// </summary>
    public StoreTransactionLogEntry(
        long transactionId,
        long timestamp,
        int channelIndex,
        long sequenceNumber,
        long dataFileNumber,
        long offset,
        long length,
        IReadOnlyList<long> objectIds)
        : base(transactionId, timestamp, channelIndex, sequenceNumber)
    {
        DataFileNumber = dataFileNumber;
        Offset = offset;
        Length = length;
        ObjectIds = objectIds ?? throw new ArgumentNullException(nameof(objectIds));
    }

    public override byte[] Serialize()
    {
        var buffer = new List<byte>();

        // Entry type
        buffer.Add((byte)EntryType);

        // Transaction metadata
        buffer.AddRange(BitConverter.GetBytes(TransactionId));
        buffer.AddRange(BitConverter.GetBytes(Timestamp));
        buffer.AddRange(BitConverter.GetBytes(ChannelIndex));
        buffer.AddRange(BitConverter.GetBytes(SequenceNumber));

        // Store-specific data
        buffer.AddRange(BitConverter.GetBytes(DataFileNumber));
        buffer.AddRange(BitConverter.GetBytes(Offset));
        buffer.AddRange(BitConverter.GetBytes(Length));

        // Object IDs
        buffer.AddRange(BitConverter.GetBytes(ObjectIds.Count));
        foreach (var objectId in ObjectIds)
        {
            buffer.AddRange(BitConverter.GetBytes(objectId));
        }

        return buffer.ToArray();
    }

    public override int GetSerializedSize()
    {
        return 1 + // EntryType
               8 + // TransactionId
               8 + // Timestamp
               4 + // ChannelIndex
               8 + // SequenceNumber
               8 + // DataFileNumber
               8 + // Offset
               8 + // Length
               4 + // ObjectIds count
               (ObjectIds.Count * 8); // ObjectIds
    }
}

/// <summary>
/// Transaction log entry for file creation operations.
/// </summary>
public class CreateTransactionLogEntry : TransactionLogEntry
{
    public override TransactionLogEntryType EntryType => TransactionLogEntryType.Create;

    /// <summary>
    /// Gets the data file number that was created.
    /// </summary>
    public long DataFileNumber { get; }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the CreateTransactionLogEntry class.
    /// </summary>
    public CreateTransactionLogEntry(
        long transactionId,
        long timestamp,
        int channelIndex,
        long sequenceNumber,
        long dataFileNumber,
        string filePath)
        : base(transactionId, timestamp, channelIndex, sequenceNumber)
    {
        DataFileNumber = dataFileNumber;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public override byte[] Serialize()
    {
        var buffer = new List<byte>();

        // Entry type
        buffer.Add((byte)EntryType);

        // Transaction metadata
        buffer.AddRange(BitConverter.GetBytes(TransactionId));
        buffer.AddRange(BitConverter.GetBytes(Timestamp));
        buffer.AddRange(BitConverter.GetBytes(ChannelIndex));
        buffer.AddRange(BitConverter.GetBytes(SequenceNumber));

        // Create-specific data
        buffer.AddRange(BitConverter.GetBytes(DataFileNumber));

        // File path
        var pathBytes = System.Text.Encoding.UTF8.GetBytes(FilePath);
        buffer.AddRange(BitConverter.GetBytes(pathBytes.Length));
        buffer.AddRange(pathBytes);

        return buffer.ToArray();
    }

    public override int GetSerializedSize()
    {
        var pathBytes = System.Text.Encoding.UTF8.GetBytes(FilePath);
        return 1 + // EntryType
               8 + // TransactionId
               8 + // Timestamp
               4 + // ChannelIndex
               8 + // SequenceNumber
               8 + // DataFileNumber
               4 + // Path length
               pathBytes.Length; // Path bytes
    }
}

/// <summary>
/// Transaction log entry for commit operations.
/// </summary>
public class CommitTransactionLogEntry : TransactionLogEntry
{
    public override TransactionLogEntryType EntryType => TransactionLogEntryType.Commit;

    /// <summary>
    /// Gets the number of operations in this transaction.
    /// </summary>
    public int OperationCount { get; }

    /// <summary>
    /// Initializes a new instance of the CommitTransactionLogEntry class.
    /// </summary>
    public CommitTransactionLogEntry(
        long transactionId,
        long timestamp,
        int channelIndex,
        long sequenceNumber,
        int operationCount)
        : base(transactionId, timestamp, channelIndex, sequenceNumber)
    {
        OperationCount = operationCount;
    }

    public override byte[] Serialize()
    {
        var buffer = new List<byte>();

        // Entry type
        buffer.Add((byte)EntryType);

        // Transaction metadata
        buffer.AddRange(BitConverter.GetBytes(TransactionId));
        buffer.AddRange(BitConverter.GetBytes(Timestamp));
        buffer.AddRange(BitConverter.GetBytes(ChannelIndex));
        buffer.AddRange(BitConverter.GetBytes(SequenceNumber));

        // Commit-specific data
        buffer.AddRange(BitConverter.GetBytes(OperationCount));

        return buffer.ToArray();
    }

    public override int GetSerializedSize()
    {
        return 1 + // EntryType
               8 + // TransactionId
               8 + // Timestamp
               4 + // ChannelIndex
               8 + // SequenceNumber
               4; // OperationCount
    }
}