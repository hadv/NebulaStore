using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Interface for memory-mapped file operations with efficient virtual memory management.
/// </summary>
public interface IMemoryMappedFile : IDisposable
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the current file size.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets the memory-mapped file capacity.
    /// </summary>
    long Capacity { get; }

    /// <summary>
    /// Gets whether the file is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets whether the file is currently mapped.
    /// </summary>
    bool IsMapped { get; }

    /// <summary>
    /// Creates a view accessor for the specified range.
    /// </summary>
    /// <param name="offset">Offset in the file</param>
    /// <param name="size">Size of the view</param>
    /// <param name="access">Access mode</param>
    /// <returns>Memory-mapped view accessor</returns>
    IMemoryMappedViewAccessor CreateViewAccessor(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite);

    /// <summary>
    /// Creates a view stream for the specified range.
    /// </summary>
    /// <param name="offset">Offset in the file</param>
    /// <param name="size">Size of the view</param>
    /// <param name="access">Access mode</param>
    /// <returns>Memory-mapped view stream</returns>
    Stream CreateViewStream(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite);

    /// <summary>
    /// Reads data from the specified offset.
    /// </summary>
    /// <param name="offset">Offset to read from</param>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="bufferOffset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to read</param>
    /// <returns>Number of bytes read</returns>
    int Read(long offset, byte[] buffer, int bufferOffset, int count);

    /// <summary>
    /// Writes data to the specified offset.
    /// </summary>
    /// <param name="offset">Offset to write to</param>
    /// <param name="buffer">Buffer to write from</param>
    /// <param name="bufferOffset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to write</param>
    void Write(long offset, byte[] buffer, int bufferOffset, int count);

    /// <summary>
    /// Reads data asynchronously from the specified offset.
    /// </summary>
    /// <param name="offset">Offset to read from</param>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="bufferOffset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of bytes read</returns>
    Task<int> ReadAsync(long offset, byte[] buffer, int bufferOffset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data asynchronously to the specified offset.
    /// </summary>
    /// <param name="offset">Offset to write to</param>
    /// <param name="buffer">Buffer to write from</param>
    /// <param name="bufferOffset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(long offset, byte[] buffer, int bufferOffset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expands the file to the specified size.
    /// </summary>
    /// <param name="newSize">New file size</param>
    void Expand(long newSize);

    /// <summary>
    /// Flushes all changes to disk.
    /// </summary>
    void Flush();

    /// <summary>
    /// Flushes all changes to disk asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets memory-mapped file statistics.
    /// </summary>
    IMemoryMappedFileStatistics Statistics { get; }
}

/// <summary>
/// Interface for memory-mapped view accessor with enhanced functionality.
/// </summary>
public interface IMemoryMappedViewAccessor : IDisposable
{
    /// <summary>
    /// Gets the offset of this view in the file.
    /// </summary>
    long Offset { get; }

    /// <summary>
    /// Gets the size of this view.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets the access mode of this view.
    /// </summary>
    MemoryMappedFileAccess Access { get; }

    /// <summary>
    /// Gets whether this view is valid.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Reads a byte at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <returns>Byte value</returns>
    byte ReadByte(long position);

    /// <summary>
    /// Writes a byte at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <param name="value">Byte value</param>
    void WriteByte(long position, byte value);

    /// <summary>
    /// Reads an array of bytes from the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="offset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to read</param>
    /// <returns>Number of bytes read</returns>
    int ReadArray(long position, byte[] buffer, int offset, int count);

    /// <summary>
    /// Writes an array of bytes to the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <param name="buffer">Buffer to write from</param>
    /// <param name="offset">Offset in the buffer</param>
    /// <param name="count">Number of bytes to write</param>
    void WriteArray(long position, byte[] buffer, int offset, int count);

    /// <summary>
    /// Reads a 32-bit integer at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <returns>Integer value</returns>
    int ReadInt32(long position);

    /// <summary>
    /// Writes a 32-bit integer at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <param name="value">Integer value</param>
    void WriteInt32(long position, int value);

    /// <summary>
    /// Reads a 64-bit integer at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <returns>Long value</returns>
    long ReadInt64(long position);

    /// <summary>
    /// Writes a 64-bit integer at the specified position.
    /// </summary>
    /// <param name="position">Position within the view</param>
    /// <param name="value">Long value</param>
    void WriteInt64(long position, long value);

    /// <summary>
    /// Flushes changes in this view to disk.
    /// </summary>
    void Flush();
}

/// <summary>
/// Interface for memory-mapped file statistics.
/// </summary>
public interface IMemoryMappedFileStatistics
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
    /// Gets the number of active views.
    /// </summary>
    int ActiveViews { get; }

    /// <summary>
    /// Gets the total number of views created.
    /// </summary>
    long TotalViewsCreated { get; }

    /// <summary>
    /// Gets the number of page faults.
    /// </summary>
    long PageFaults { get; }

    /// <summary>
    /// Gets the average access time in microseconds.
    /// </summary>
    double AverageAccessTimeMicroseconds { get; }

    /// <summary>
    /// Gets the memory usage in bytes.
    /// </summary>
    long MemoryUsage { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Configuration for memory-mapped files.
/// </summary>
public class MemoryMappedFileConfiguration
{
    /// <summary>
    /// Gets or sets the default view size.
    /// </summary>
    public long DefaultViewSize { get; set; } = 64 * 1024 * 1024; // 64MB

    /// <summary>
    /// Gets or sets the maximum number of concurrent views.
    /// </summary>
    public int MaxConcurrentViews { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable automatic view management.
    /// </summary>
    public bool EnableAutoViewManagement { get; set; } = true;

    /// <summary>
    /// Gets or sets the view cache size.
    /// </summary>
    public int ViewCacheSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to enable read-ahead.
    /// </summary>
    public bool EnableReadAhead { get; set; } = true;

    /// <summary>
    /// Gets or sets the read-ahead size.
    /// </summary>
    public long ReadAheadSize { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Gets or sets whether to enable write-behind.
    /// </summary>
    public bool EnableWriteBehind { get; set; } = true;

    /// <summary>
    /// Gets or sets the write-behind interval.
    /// </summary>
    public TimeSpan WriteBehindInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the initial file size for new files.
    /// </summary>
    public long InitialFileSize { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Gets or sets the file growth factor.
    /// </summary>
    public double FileGrowthFactor { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the maximum file size.
    /// </summary>
    public long MaxFileSize { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB
}
