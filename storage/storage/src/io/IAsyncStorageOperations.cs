using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Interface for asynchronous storage operations with high performance and proper cancellation support.
/// </summary>
public interface IAsyncStorageOperations : IDisposable
{
    /// <summary>
    /// Asynchronously reads data from the specified path.
    /// </summary>
    /// <param name="path">Path to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read data</returns>
    Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads data from the specified path with offset and length.
    /// </summary>
    /// <param name="path">Path to read from</param>
    /// <param name="offset">Offset to start reading from</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read data</returns>
    Task<byte[]> ReadAsync(string path, long offset, int length, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously writes data to the specified path.
    /// </summary>
    /// <param name="path">Path to write to</param>
    /// <param name="data">Data to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously writes data to the specified path with offset.
    /// </summary>
    /// <param name="path">Path to write to</param>
    /// <param name="data">Data to write</param>
    /// <param name="offset">Offset to start writing at</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(string path, byte[] data, long offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously appends data to the specified path.
    /// </summary>
    /// <param name="path">Path to append to</param>
    /// <param name="data">Data to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AppendAsync(string path, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes the file at the specified path.
    /// </summary>
    /// <param name="path">Path to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if a file exists at the specified path.
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the size of the file at the specified path.
    /// </summary>
    /// <param name="path">Path to get size for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File size in bytes</returns>
    Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously flushes all pending operations to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs batch read operations.
    /// </summary>
    /// <param name="requests">Read requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read results</returns>
    Task<IEnumerable<AsyncReadResult>> ReadBatchAsync(IEnumerable<AsyncReadRequest> requests, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs batch write operations.
    /// </summary>
    /// <param name="requests">Write requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Write results</returns>
    Task<IEnumerable<AsyncWriteResult>> WriteBatchAsync(IEnumerable<AsyncWriteRequest> requests, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets I/O performance statistics.
    /// </summary>
    IAsyncIOStatistics Statistics { get; }
}

/// <summary>
/// Represents an asynchronous read request.
/// </summary>
public class AsyncReadRequest
{
    public AsyncReadRequest(string path, long offset = 0, int length = -1, int priority = 0)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Offset = offset >= 0 ? offset : throw new ArgumentOutOfRangeException(nameof(offset));
        Length = length;
        Priority = priority;
        RequestId = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the unique request identifier.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the file path to read from.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the offset to start reading from.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the number of bytes to read (-1 for entire file).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the request priority (higher values = higher priority).
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets the request timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Represents an asynchronous write request.
/// </summary>
public class AsyncWriteRequest
{
    public AsyncWriteRequest(string path, byte[] data, long offset = -1, int priority = 0, bool append = false)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Offset = offset;
        Priority = priority;
        Append = append;
        RequestId = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the unique request identifier.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the file path to write to.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the data to write.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the offset to start writing at (-1 for end of file).
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the request priority (higher values = higher priority).
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets whether this is an append operation.
    /// </summary>
    public bool Append { get; }

    /// <summary>
    /// Gets the request timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Represents the result of an asynchronous read operation.
/// </summary>
public class AsyncReadResult
{
    public AsyncReadResult(Guid requestId, byte[]? data, Exception? error = null, TimeSpan? duration = null)
    {
        RequestId = requestId;
        Data = data;
        Error = error;
        Duration = duration ?? TimeSpan.Zero;
        IsSuccess = error == null;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the request identifier this result corresponds to.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the read data (null if error occurred).
    /// </summary>
    public byte[]? Data { get; }

    /// <summary>
    /// Gets the error that occurred (null if successful).
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the operation duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the result timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Represents the result of an asynchronous write operation.
/// </summary>
public class AsyncWriteResult
{
    public AsyncWriteResult(Guid requestId, long bytesWritten = 0, Exception? error = null, TimeSpan? duration = null)
    {
        RequestId = requestId;
        BytesWritten = bytesWritten;
        Error = error;
        Duration = duration ?? TimeSpan.Zero;
        IsSuccess = error == null;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the request identifier this result corresponds to.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the number of bytes written.
    /// </summary>
    public long BytesWritten { get; }

    /// <summary>
    /// Gets the error that occurred (null if successful).
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the operation duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the result timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Interface for asynchronous I/O statistics.
/// </summary>
public interface IAsyncIOStatistics
{
    /// <summary>
    /// Gets the total number of read operations.
    /// </summary>
    long TotalReadOperations { get; }

    /// <summary>
    /// Gets the total number of write operations.
    /// </summary>
    long TotalWriteOperations { get; }

    /// <summary>
    /// Gets the total bytes read.
    /// </summary>
    long TotalBytesRead { get; }

    /// <summary>
    /// Gets the total bytes written.
    /// </summary>
    long TotalBytesWritten { get; }

    /// <summary>
    /// Gets the average read latency in milliseconds.
    /// </summary>
    double AverageReadLatencyMs { get; }

    /// <summary>
    /// Gets the average write latency in milliseconds.
    /// </summary>
    double AverageWriteLatencyMs { get; }

    /// <summary>
    /// Gets the current number of pending operations.
    /// </summary>
    int PendingOperations { get; }

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    long FailedOperations { get; }

    /// <summary>
    /// Gets the read throughput in bytes per second.
    /// </summary>
    double ReadThroughputBytesPerSecond { get; }

    /// <summary>
    /// Gets the write throughput in bytes per second.
    /// </summary>
    double WriteThroughputBytesPerSecond { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}
