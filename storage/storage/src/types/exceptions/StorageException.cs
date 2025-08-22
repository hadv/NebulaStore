using System;

namespace NebulaStore.Storage.Embedded.Types.Exceptions;

/// <summary>
/// Base exception type for all storage-related exceptions.
/// Check usages of this type, replace by better typed exceptions.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class.
    /// </summary>
    public StorageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageException(Exception innerException) : base(innerException?.Message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage configuration is invalid.
/// </summary>
public class StorageConfigurationException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class.
    /// </summary>
    public StorageConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageConfigurationException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage operation fails.
/// </summary>
public class StorageOperationException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class.
    /// </summary>
    public StorageOperationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageOperationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageOperationException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage file operation fails.
/// </summary>
public class StorageFileException : StorageException
{
    /// <summary>
    /// Gets the file path that caused the exception.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class.
    /// </summary>
    public StorageFileException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageFileException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message and file path.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="filePath">The file path that caused the exception.</param>
    public StorageFileException(string message, string filePath) : base(message)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message, file path, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="filePath">The file path that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(string message, string filePath, Exception innerException) : base(message, innerException)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Exception thrown when a storage backup operation fails.
/// </summary>
public class StorageBackupException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class.
    /// </summary>
    public StorageBackupException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageBackupException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageBackupException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageBackupException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage housekeeping operation fails.
/// </summary>
public class StorageHousekeepingException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class.
    /// </summary>
    public StorageHousekeepingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageHousekeepingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageHousekeepingException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageHousekeepingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when there is insufficient buffer space for a storage operation.
/// </summary>
public class InsufficientBufferSpaceException : StorageException
{
    /// <summary>
    /// Gets the required buffer size.
    /// </summary>
    public long RequiredSize { get; }

    /// <summary>
    /// Gets the available buffer size.
    /// </summary>
    public long AvailableSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientBufferSpaceException"/> class.
    /// </summary>
    /// <param name="requiredSize">The required buffer size.</param>
    /// <param name="availableSize">The available buffer size.</param>
    public InsufficientBufferSpaceException(long requiredSize, long availableSize)
        : base($"Insufficient buffer space. Required: {requiredSize}, Available: {availableSize}")
    {
        RequiredSize = requiredSize;
        AvailableSize = availableSize;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientBufferSpaceException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="requiredSize">The required buffer size.</param>
    /// <param name="availableSize">The available buffer size.</param>
    public InsufficientBufferSpaceException(string message, long requiredSize, long availableSize)
        : base(message)
    {
        RequiredSize = requiredSize;
        AvailableSize = availableSize;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientBufferSpaceException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="requiredSize">The required buffer size.</param>
    /// <param name="availableSize">The available buffer size.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InsufficientBufferSpaceException(string message, long requiredSize, long availableSize, Exception innerException)
        : base(message, innerException)
    {
        RequiredSize = requiredSize;
        AvailableSize = availableSize;
    }
}

/// <summary>
/// Exception thrown when a backup operation fails due to channel index issues.
/// </summary>
public class StorageExceptionBackupChannelIndex : StorageBackupException
{
    /// <summary>
    /// Gets the invalid channel index.
    /// </summary>
    public int ChannelIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupChannelIndex"/> class.
    /// </summary>
    /// <param name="channelIndex">The invalid channel index.</param>
    public StorageExceptionBackupChannelIndex(int channelIndex)
        : base($"Invalid backup channel index: {channelIndex}")
    {
        ChannelIndex = channelIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupChannelIndex"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="channelIndex">The invalid channel index.</param>
    public StorageExceptionBackupChannelIndex(string message, int channelIndex)
        : base(message)
    {
        ChannelIndex = channelIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupChannelIndex"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="channelIndex">The invalid channel index.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionBackupChannelIndex(string message, int channelIndex, Exception innerException)
        : base(message, innerException)
    {
        ChannelIndex = channelIndex;
    }
}

/// <summary>
/// Exception thrown when a backup copying operation fails.
/// </summary>
public class StorageExceptionBackupCopying : StorageBackupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupCopying"/> class.
    /// </summary>
    public StorageExceptionBackupCopying()
        : base("Backup copying operation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupCopying"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionBackupCopying(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupCopying"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionBackupCopying(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when backup is disabled but a backup operation is attempted.
/// </summary>
public class StorageExceptionBackupDisabled : StorageBackupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupDisabled"/> class.
    /// </summary>
    public StorageExceptionBackupDisabled()
        : base("Backup operations are disabled")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionBackupDisabled"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionBackupDisabled(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when commit size exceeds the allowed limit.
/// </summary>
public class StorageExceptionCommitSizeExceeded : StorageOperationException
{
    /// <summary>
    /// Gets the actual commit size.
    /// </summary>
    public long ActualSize { get; }

    /// <summary>
    /// Gets the maximum allowed commit size.
    /// </summary>
    public long MaximumSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionCommitSizeExceeded"/> class.
    /// </summary>
    /// <param name="actualSize">The actual commit size.</param>
    /// <param name="maximumSize">The maximum allowed commit size.</param>
    public StorageExceptionCommitSizeExceeded(long actualSize, long maximumSize)
        : base($"Commit size exceeded. Actual: {actualSize}, Maximum: {maximumSize}")
    {
        ActualSize = actualSize;
        MaximumSize = maximumSize;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionCommitSizeExceeded"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="actualSize">The actual commit size.</param>
    /// <param name="maximumSize">The maximum allowed commit size.</param>
    public StorageExceptionCommitSizeExceeded(string message, long actualSize, long maximumSize)
        : base(message)
    {
        ActualSize = actualSize;
        MaximumSize = maximumSize;
    }
}

/// <summary>
/// Exception thrown when storage consistency is violated.
/// </summary>
public class StorageExceptionConsistency : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionConsistency"/> class.
    /// </summary>
    public StorageExceptionConsistency()
        : base("Storage consistency violation detected")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionConsistency"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionConsistency(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionConsistency"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionConsistency(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when storage initialization fails.
/// </summary>
public class StorageExceptionInitialization : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionInitialization"/> class.
    /// </summary>
    public StorageExceptionInitialization()
        : base("Storage initialization failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionInitialization"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionInitialization(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionInitialization"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionInitialization(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when storage is not running but an operation requires it to be running.
/// </summary>
public class StorageExceptionNotRunning : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionNotRunning"/> class.
    /// </summary>
    public StorageExceptionNotRunning()
        : base("Storage is not running")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionNotRunning"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionNotRunning(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when an I/O operation fails during storage operations.
/// </summary>
public class StorageExceptionIo : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIo"/> class.
    /// </summary>
    public StorageExceptionIo()
        : base("Storage I/O operation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIo"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionIo(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIo"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionIo(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an I/O reading operation fails during storage operations.
/// </summary>
public class StorageExceptionIoReading : StorageExceptionIo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoReading"/> class.
    /// </summary>
    public StorageExceptionIoReading()
        : base("Storage I/O reading operation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoReading"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionIoReading(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoReading"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionIoReading(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an I/O writing operation fails during storage operations.
/// </summary>
public class StorageExceptionIoWriting : StorageExceptionIo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWriting"/> class.
    /// </summary>
    public StorageExceptionIoWriting()
        : base("Storage I/O writing operation failed")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWriting"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageExceptionIoWriting(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWriting"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionIoWriting(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when writing a chunk fails during storage operations.
/// </summary>
public class StorageExceptionIoWritingChunk : StorageExceptionIoWriting
{
    /// <summary>
    /// Gets the chunk identifier that failed to write.
    /// </summary>
    public long ChunkId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWritingChunk"/> class.
    /// </summary>
    /// <param name="chunkId">The chunk identifier that failed to write.</param>
    public StorageExceptionIoWritingChunk(long chunkId)
        : base($"Failed to write storage chunk: {chunkId}")
    {
        ChunkId = chunkId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWritingChunk"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="chunkId">The chunk identifier that failed to write.</param>
    public StorageExceptionIoWritingChunk(string message, long chunkId) : base(message)
    {
        ChunkId = chunkId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageExceptionIoWritingChunk"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="chunkId">The chunk identifier that failed to write.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageExceptionIoWritingChunk(string message, long chunkId, Exception innerException) : base(message, innerException)
    {
        ChunkId = chunkId;
    }
}
