using System;
using System.IO;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO file system.
/// </summary>
public interface INioFileSystem : IDisposable
{
    /// <summary>
    /// Gets the I/O handler for this file system.
    /// </summary>
    INioIoHandler IoHandler { get; }
    
    /// <summary>
    /// Gets the default protocol for this file system.
    /// </summary>
    string DefaultProtocol { get; }
    
    /// <summary>
    /// Wraps a blob store path for reading.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <returns>A readable file wrapper</returns>
    INioReadableFile WrapForReading(BlobStorePath path, object? user = null);
    
    /// <summary>
    /// Wraps a blob store path for writing.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <returns>A writable file wrapper</returns>
    INioWritableFile WrapForWriting(BlobStorePath path, object? user = null);
    
    /// <summary>
    /// Converts a readable file to a writable file.
    /// </summary>
    /// <param name="file">The readable file</param>
    /// <returns>A writable file wrapper</returns>
    INioWritableFile ConvertToWriting(INioReadableFile file);
    
    /// <summary>
    /// Converts a writable file to a readable file.
    /// </summary>
    /// <param name="file">The writable file</param>
    /// <returns>A readable file wrapper</returns>
    INioReadableFile ConvertToReading(INioWritableFile file);
    
    /// <summary>
    /// Resolves a path string to path elements.
    /// </summary>
    /// <param name="fullPath">The full path string</param>
    /// <returns>The path elements</returns>
    string[] ResolvePath(string fullPath);
}

/// <summary>
/// Default implementation of NIO file system.
/// </summary>
public class NioFileSystem : INioFileSystem
{
    private readonly string _defaultProtocol;
    private readonly INioIoHandler _ioHandler;
    private bool _disposed;

    /// <summary>
    /// Gets the default protocol string for file URIs.
    /// </summary>
    public const string DefaultFileProtocol = "file:///";

    /// <summary>
    /// Creates a new NIO file system with default settings.
    /// </summary>
    /// <returns>A new file system instance</returns>
    public static NioFileSystem New()
    {
        return New(DefaultFileProtocol);
    }

    /// <summary>
    /// Creates a new NIO file system with a custom protocol.
    /// </summary>
    /// <param name="defaultProtocol">The default protocol</param>
    /// <returns>A new file system instance</returns>
    public static NioFileSystem New(string defaultProtocol)
    {
        return New(defaultProtocol, NioIoHandler.New());
    }

    /// <summary>
    /// Creates a new NIO file system with a custom I/O handler.
    /// </summary>
    /// <param name="ioHandler">The I/O handler</param>
    /// <returns>A new file system instance</returns>
    public static NioFileSystem New(INioIoHandler ioHandler)
    {
        return New(DefaultFileProtocol, ioHandler);
    }

    /// <summary>
    /// Creates a new NIO file system with custom settings.
    /// </summary>
    /// <param name="defaultProtocol">The default protocol</param>
    /// <param name="ioHandler">The I/O handler</param>
    /// <returns>A new file system instance</returns>
    public static NioFileSystem New(string defaultProtocol, INioIoHandler ioHandler)
    {
        return new NioFileSystem(
            defaultProtocol ?? throw new ArgumentNullException(nameof(defaultProtocol)),
            ioHandler ?? throw new ArgumentNullException(nameof(ioHandler)));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NioFileSystem"/> class.
    /// </summary>
    /// <param name="defaultProtocol">The default protocol</param>
    /// <param name="ioHandler">The I/O handler</param>
    protected NioFileSystem(string defaultProtocol, INioIoHandler ioHandler)
    {
        _defaultProtocol = defaultProtocol ?? throw new ArgumentNullException(nameof(defaultProtocol));
        _ioHandler = ioHandler ?? throw new ArgumentNullException(nameof(ioHandler));
    }

    /// <inheritdoc/>
    public string DefaultProtocol => _defaultProtocol;

    /// <inheritdoc/>
    public INioIoHandler IoHandler => _ioHandler;

    /// <inheritdoc/>
    public string[] ResolvePath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return Array.Empty<string>();
        }

        // Handle home directory expansion
        var resolved = fullPath;
        if (fullPath.StartsWith("~/") || fullPath.StartsWith("~\\"))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            resolved = System.IO.Path.Combine(homeDir, fullPath.Substring(2));
        }

        // Split path by directory separator
        var separators = new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
        return resolved.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <inheritdoc/>
    public INioReadableFile WrapForReading(BlobStorePath path, object? user = null)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var fsPath = _ioHandler.ToPath(path);
        return NioReadableFile<object>.New(path, user, fsPath);
    }

    /// <inheritdoc/>
    public INioWritableFile WrapForWriting(BlobStorePath path, object? user = null)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var fsPath = _ioHandler.ToPath(path);
        return NioWritableFile<object>.New(path, user, fsPath);
    }

    /// <inheritdoc/>
    public INioWritableFile ConvertToWriting(INioReadableFile file)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        // Close the readable file's stream
        var actuallyClosedStream = file.CloseStream();

        // Create a new writable file
        var writableFile = NioWritableFile<object>.New(file.BlobPath, file.User, file.Path, null);

        // Replicate opened stream if necessary
        if (actuallyClosedStream)
        {
            writableFile.EnsureOpenStream();
        }

        return writableFile;
    }

    /// <inheritdoc/>
    public INioReadableFile ConvertToReading(INioWritableFile file)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        // Close the writable file's stream
        var actuallyClosedStream = file.CloseStream();

        // Create a new readable file
        var readableFile = NioReadableFile<object>.New(file.BlobPath, file.User, file.Path, null);

        // Replicate opened stream if necessary
        if (actuallyClosedStream)
        {
            readableFile.EnsureOpenStream();
        }

        return readableFile;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this file system.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _ioHandler?.Dispose();
        }

        _disposed = true;
    }
}

