using System;
using System.IO;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Interface for blob store readable files.
/// </summary>
public interface IBlobStoreReadableFile : IDisposable
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    BlobStorePath Path { get; }

    /// <summary>
    /// Gets the user context.
    /// </summary>
    object? User { get; }

    /// <summary>
    /// Gets a value indicating whether the file handle is open.
    /// </summary>
    bool IsHandleOpen { get; }

    /// <summary>
    /// Opens the file handle for reading.
    /// </summary>
    /// <returns>True if the handle was opened successfully</returns>
    bool OpenHandle();

    /// <summary>
    /// Closes the file handle.
    /// </summary>
    /// <returns>True if the handle was closed successfully</returns>
    bool CloseHandle();

    /// <summary>
    /// Ensures the handle is open and returns this instance.
    /// </summary>
    /// <returns>This instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the handle cannot be opened</exception>
    IBlobStoreReadableFile EnsureOpenHandle();

    /// <summary>
    /// Gets the size of the file.
    /// </summary>
    /// <returns>The file size in bytes</returns>
    long Size();

    /// <summary>
    /// Reads all data from the file.
    /// </summary>
    /// <returns>The file data</returns>
    byte[] ReadBytes();

    /// <summary>
    /// Reads data from the file starting at the specified position.
    /// </summary>
    /// <param name="position">The position to start reading from</param>
    /// <returns>The file data</returns>
    byte[] ReadBytes(long position);

    /// <summary>
    /// Reads data from the file.
    /// </summary>
    /// <param name="position">The position to start reading from</param>
    /// <param name="length">The number of bytes to read</param>
    /// <returns>The file data</returns>
    byte[] ReadBytes(long position, long length);
}

/// <summary>
/// Interface for blob store writable files.
/// </summary>
public interface IBlobStoreWritableFile : IDisposable
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    BlobStorePath Path { get; }

    /// <summary>
    /// Gets the user context.
    /// </summary>
    object? User { get; }

    /// <summary>
    /// Gets a value indicating whether the file handle is open.
    /// </summary>
    bool IsHandleOpen { get; }

    /// <summary>
    /// Opens the file handle for writing.
    /// </summary>
    /// <returns>True if the handle was opened successfully</returns>
    bool OpenHandle();

    /// <summary>
    /// Closes the file handle.
    /// </summary>
    /// <returns>True if the handle was closed successfully</returns>
    bool CloseHandle();

    /// <summary>
    /// Ensures the handle is open and returns this instance.
    /// </summary>
    /// <returns>This instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the handle cannot be opened</exception>
    IBlobStoreWritableFile EnsureOpenHandle();

    /// <summary>
    /// Writes data to the file.
    /// </summary>
    /// <param name="data">The data to write</param>
    /// <returns>The number of bytes written</returns>
    long WriteBytes(byte[] data);

    /// <summary>
    /// Truncates the file to the specified length.
    /// </summary>
    /// <param name="newLength">The new file length</param>
    void Truncate(long newLength);
}

/// <summary>
/// Implementation of IBlobStoreReadableFile.
/// </summary>
public class BlobStoreReadableFile : IBlobStoreReadableFile
{
    private readonly BlobStorePath _path;
    private readonly object? _user;
    private bool _handleOpen;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BlobStoreReadableFile class.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="user">The user context</param>
    public BlobStoreReadableFile(BlobStorePath path, object? user = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _user = user;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public BlobStorePath Path => _path;

    /// <summary>
    /// Gets the user context.
    /// </summary>
    public object? User => _user;

    /// <summary>
    /// Gets a value indicating whether the file handle is open.
    /// </summary>
    public bool IsHandleOpen => _handleOpen && !_disposed;

    public bool OpenHandle()
    {
        if (_disposed)
            return false;

        _handleOpen = true;
        return true;
    }

    public bool CloseHandle()
    {
        if (_disposed)
            return false;

        _handleOpen = false;
        return true;
    }

    public IBlobStoreReadableFile EnsureOpenHandle()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);

        if (!_handleOpen && !OpenHandle())
            throw new InvalidOperationException("Cannot open file handle");

        return this;
    }

    public long Size()
    {
        EnsureOpenHandle();
        // This would typically delegate to the I/O handler
        // For now, return 0 as a placeholder
        return 0;
    }

    public byte[] ReadBytes()
    {
        return ReadBytes(0, -1);
    }

    public byte[] ReadBytes(long position)
    {
        return ReadBytes(position, -1);
    }

    public byte[] ReadBytes(long position, long length)
    {
        EnsureOpenHandle();
        // This would typically delegate to the I/O handler
        // For now, return empty array as a placeholder
        return Array.Empty<byte>();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseHandle();
            _disposed = true;
        }
    }
}

/// <summary>
/// Implementation of IBlobStoreWritableFile.
/// </summary>
public class BlobStoreWritableFile : IBlobStoreWritableFile
{
    private readonly BlobStorePath _path;
    private readonly object? _user;
    private bool _handleOpen;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BlobStoreWritableFile class.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="user">The user context</param>
    public BlobStoreWritableFile(BlobStorePath path, object? user = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _user = user;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public BlobStorePath Path => _path;

    /// <summary>
    /// Gets the user context.
    /// </summary>
    public object? User => _user;

    /// <summary>
    /// Gets a value indicating whether the file handle is open.
    /// </summary>
    public bool IsHandleOpen => _handleOpen && !_disposed;

    public bool OpenHandle()
    {
        if (_disposed)
            return false;

        _handleOpen = true;
        return true;
    }

    public bool CloseHandle()
    {
        if (_disposed)
            return false;

        _handleOpen = false;
        return true;
    }

    public IBlobStoreWritableFile EnsureOpenHandle()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);

        if (!_handleOpen && !OpenHandle())
            throw new InvalidOperationException("Cannot open file handle");

        return this;
    }

    public long WriteBytes(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        EnsureOpenHandle();
        // This would typically delegate to the I/O handler
        // For now, return the data length as a placeholder
        return data.Length;
    }

    public void Truncate(long newLength)
    {
        if (newLength < 0)
            throw new ArgumentOutOfRangeException(nameof(newLength), "New length cannot be negative");

        EnsureOpenHandle();
        // This would typically delegate to the I/O handler
        // For now, this is a no-op placeholder
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseHandle();
            _disposed = true;
        }
    }
}
