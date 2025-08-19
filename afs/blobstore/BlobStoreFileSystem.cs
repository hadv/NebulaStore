using System;
using System.IO;
using NebulaStore.Afs.Types;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Interface for blob store file systems.
/// Provides file system operations over blob storage.
/// </summary>
public interface IBlobStoreFileSystem : IDisposable
{
    /// <summary>
    /// Gets the I/O handler for this file system.
    /// </summary>
    IBlobStoreIoHandler IoHandler { get; }

    /// <summary>
    /// Creates a readable file wrapper.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="user">The user context</param>
    /// <returns>A readable file wrapper</returns>
    IBlobStoreReadableFile WrapForReading(BlobStorePath path, object? user = null);

    /// <summary>
    /// Creates a writable file wrapper.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="user">The user context</param>
    /// <returns>A writable file wrapper</returns>
    IBlobStoreWritableFile WrapForWriting(BlobStorePath path, object? user = null);

    /// <summary>
    /// Converts a writable file to a readable file.
    /// </summary>
    /// <param name="writableFile">The writable file</param>
    /// <returns>A readable file wrapper</returns>
    IBlobStoreReadableFile ConvertToReading(IBlobStoreWritableFile writableFile);

    /// <summary>
    /// Converts a readable file to a writable file.
    /// </summary>
    /// <param name="readableFile">The readable file</param>
    /// <returns>A writable file wrapper</returns>
    IBlobStoreWritableFile ConvertToWriting(IBlobStoreReadableFile readableFile);

    /// <summary>
    /// Derives a file identifier from name and type.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="fileType">The file type/extension</param>
    /// <returns>The file identifier</returns>
    string DeriveFileIdentifier(string fileName, string fileType);

    /// <summary>
    /// Derives the file name from an identifier.
    /// </summary>
    /// <param name="fileIdentifier">The file identifier</param>
    /// <returns>The file name</returns>
    string DeriveFileName(string fileIdentifier);

    /// <summary>
    /// Derives the file type from an identifier.
    /// </summary>
    /// <param name="fileIdentifier">The file identifier</param>
    /// <returns>The file type</returns>
    string DeriveFileType(string fileIdentifier);
}

/// <summary>
/// Default implementation of IBlobStoreFileSystem.
/// </summary>
public class BlobStoreFileSystem : IBlobStoreFileSystem
{
    private readonly IBlobStoreIoHandler _ioHandler;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BlobStoreFileSystem class.
    /// </summary>
    /// <param name="ioHandler">The I/O handler</param>
    public BlobStoreFileSystem(IBlobStoreIoHandler ioHandler)
    {
        _ioHandler = ioHandler ?? throw new ArgumentNullException(nameof(ioHandler));
    }

    /// <summary>
    /// Gets the I/O handler for this file system.
    /// </summary>
    public IBlobStoreIoHandler IoHandler => _ioHandler;

    /// <summary>
    /// Creates a new blob store file system with the specified connector.
    /// </summary>
    /// <param name="connector">The blob store connector</param>
    /// <returns>A new file system instance</returns>
    public static IBlobStoreFileSystem New(IBlobStoreConnector connector)
    {
        var ioHandler = new BlobStoreIoHandler(connector);
        return new BlobStoreFileSystem(ioHandler);
    }

    /// <summary>
    /// Creates a new blob store file system with the specified I/O handler.
    /// </summary>
    /// <param name="ioHandler">The I/O handler</param>
    /// <returns>A new file system instance</returns>
    public static IBlobStoreFileSystem New(IBlobStoreIoHandler ioHandler)
    {
        return new BlobStoreFileSystem(ioHandler);
    }

    /// <summary>
    /// Converts path elements to a BlobStorePath.
    /// </summary>
    /// <param name="pathElements">The path elements</param>
    /// <returns>A BlobStorePath instance</returns>
    public static BlobStorePath ToPath(params string[] pathElements)
    {
        return BlobStorePath.New(pathElements);
    }

    public IBlobStoreReadableFile WrapForReading(BlobStorePath path, object? user = null)
    {
        EnsureNotDisposed();
        return new BlobStoreReadableFile(path, user);
    }

    public IBlobStoreWritableFile WrapForWriting(BlobStorePath path, object? user = null)
    {
        EnsureNotDisposed();
        return new BlobStoreWritableFile(path, user);
    }

    public IBlobStoreReadableFile ConvertToReading(IBlobStoreWritableFile writableFile)
    {
        EnsureNotDisposed();
        return new BlobStoreReadableFile(writableFile.Path, writableFile.User);
    }

    public IBlobStoreWritableFile ConvertToWriting(IBlobStoreReadableFile readableFile)
    {
        EnsureNotDisposed();
        return new BlobStoreWritableFile(readableFile.Path, readableFile.User);
    }

    public string DeriveFileIdentifier(string fileName, string fileType)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        if (string.IsNullOrEmpty(fileType))
            return fileName;

        return fileType.StartsWith(".") ? fileName + fileType : fileName + "." + fileType;
    }

    public string DeriveFileName(string fileIdentifier)
    {
        if (string.IsNullOrEmpty(fileIdentifier))
            throw new ArgumentException("File identifier cannot be null or empty", nameof(fileIdentifier));

        var lastDot = fileIdentifier.LastIndexOf('.');
        return lastDot >= 0 ? fileIdentifier.Substring(0, lastDot) : fileIdentifier;
    }

    public string DeriveFileType(string fileIdentifier)
    {
        if (string.IsNullOrEmpty(fileIdentifier))
            throw new ArgumentException("File identifier cannot be null or empty", nameof(fileIdentifier));

        var lastDot = fileIdentifier.LastIndexOf('.');
        return lastDot >= 0 ? fileIdentifier.Substring(lastDot + 1) : string.Empty;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ioHandler?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for blob store I/O handlers.
/// </summary>
public interface IBlobStoreIoHandler : IDisposable
{
    /// <summary>
    /// Gets the blob store connector.
    /// </summary>
    IBlobStoreConnector Connector { get; }

    /// <summary>
    /// Gets the size of the specified file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>The file size in bytes</returns>
    long GetFileSize(BlobStorePath path);

    /// <summary>
    /// Checks if the specified file exists.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>True if the file exists</returns>
    bool FileExists(BlobStorePath path);

    /// <summary>
    /// Checks if the specified directory exists.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>True if the directory exists</returns>
    bool DirectoryExists(BlobStorePath path);

    /// <summary>
    /// Creates the specified directory.
    /// </summary>
    /// <param name="path">The directory path</param>
    void CreateDirectory(BlobStorePath path);

    /// <summary>
    /// Creates the specified file.
    /// </summary>
    /// <param name="path">The file path</param>
    void CreateFile(BlobStorePath path);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>True if the file was deleted</returns>
    bool DeleteFile(BlobStorePath path);

    /// <summary>
    /// Reads data from the specified file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="offset">The offset to start reading from</param>
    /// <param name="length">The number of bytes to read</param>
    /// <returns>The read data</returns>
    byte[] ReadData(BlobStorePath path, long offset, long length);

    /// <summary>
    /// Writes data to the specified file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="data">The data to write</param>
    /// <returns>The number of bytes written</returns>
    long WriteData(BlobStorePath path, byte[] data);
}

/// <summary>
/// Default implementation of IBlobStoreIoHandler.
/// </summary>
public class BlobStoreIoHandler : IBlobStoreIoHandler
{
    private readonly IBlobStoreConnector _connector;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BlobStoreIoHandler class.
    /// </summary>
    /// <param name="connector">The blob store connector</param>
    public BlobStoreIoHandler(IBlobStoreConnector connector)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    /// <summary>
    /// Gets the blob store connector.
    /// </summary>
    public IBlobStoreConnector Connector => _connector;

    public long GetFileSize(BlobStorePath path)
    {
        EnsureNotDisposed();
        return _connector.GetFileSize(path);
    }

    public bool FileExists(BlobStorePath path)
    {
        EnsureNotDisposed();
        return _connector.FileExists(path);
    }

    public bool DirectoryExists(BlobStorePath path)
    {
        EnsureNotDisposed();
        return _connector.DirectoryExists(path);
    }

    public void CreateDirectory(BlobStorePath path)
    {
        EnsureNotDisposed();
        _connector.CreateDirectory(path);
    }

    public void CreateFile(BlobStorePath path)
    {
        EnsureNotDisposed();
        _connector.CreateFile(path);
    }

    public bool DeleteFile(BlobStorePath path)
    {
        EnsureNotDisposed();
        return _connector.DeleteFile(path);
    }

    public byte[] ReadData(BlobStorePath path, long offset, long length)
    {
        EnsureNotDisposed();
        return _connector.ReadData(path, offset, length);
    }

    public long WriteData(BlobStorePath path, byte[] data)
    {
        EnsureNotDisposed();
        return _connector.WriteData(path, new[] { data });
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connector?.Dispose();
            _disposed = true;
        }
    }
}
