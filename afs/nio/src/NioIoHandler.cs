using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO I/O handler.
/// </summary>
public interface INioIoHandler : IDisposable
{
    /// <summary>
    /// Casts a readable file to NIO readable file.
    /// </summary>
    /// <param name="file">The readable file</param>
    /// <returns>The NIO readable file</returns>
    INioReadableFile CastReadableFile(IBlobStoreReadableFile file);
    
    /// <summary>
    /// Casts a writable file to NIO writable file.
    /// </summary>
    /// <param name="file">The writable file</param>
    /// <returns>The NIO writable file</returns>
    INioWritableFile CastWritableFile(IBlobStoreWritableFile file);
    
    /// <summary>
    /// Converts a blob store path to a file system path.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <returns>The file system path</returns>
    string ToPath(BlobStorePath path);
    
    /// <summary>
    /// Converts path elements to a file system path.
    /// </summary>
    /// <param name="pathElements">The path elements</param>
    /// <returns>The file system path</returns>
    string ToPath(params string[] pathElements);
    
    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>True if the file exists</returns>
    bool FileExists(BlobStorePath path);
    
    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>True if the directory exists</returns>
    bool DirectoryExists(BlobStorePath path);
    
    /// <summary>
    /// Gets the size of a file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>The file size in bytes</returns>
    long GetFileSize(BlobStorePath path);
    
    /// <summary>
    /// Lists items in a directory.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>The list of item names</returns>
    IEnumerable<string> ListItems(BlobStorePath path);
    
    /// <summary>
    /// Lists directories in a directory.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>The list of directory names</returns>
    IEnumerable<string> ListDirectories(BlobStorePath path);
    
    /// <summary>
    /// Lists files in a directory.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>The list of file names</returns>
    IEnumerable<string> ListFiles(BlobStorePath path);
    
    /// <summary>
    /// Creates a directory.
    /// </summary>
    /// <param name="path">The directory path</param>
    void CreateDirectory(BlobStorePath path);
    
    /// <summary>
    /// Creates a file.
    /// </summary>
    /// <param name="path">The file path</param>
    void CreateFile(BlobStorePath path);
    
    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>True if the file was deleted</returns>
    bool DeleteFile(BlobStorePath path);
    
    /// <summary>
    /// Reads bytes from a file.
    /// </summary>
    /// <param name="file">The readable file</param>
    /// <returns>The read bytes</returns>
    byte[] ReadBytes(INioReadableFile file);
    
    /// <summary>
    /// Reads bytes from a file at a specific position.
    /// </summary>
    /// <param name="file">The readable file</param>
    /// <param name="position">The position to read from</param>
    /// <param name="length">The number of bytes to read</param>
    /// <returns>The read bytes</returns>
    byte[] ReadBytes(INioReadableFile file, long position, long length);
    
    /// <summary>
    /// Writes bytes to a file.
    /// </summary>
    /// <param name="file">The writable file</param>
    /// <param name="data">The data to write</param>
    /// <returns>The number of bytes written</returns>
    long WriteBytes(INioWritableFile file, byte[] data);
    
    /// <summary>
    /// Copies a file.
    /// </summary>
    /// <param name="source">The source file</param>
    /// <param name="target">The target file</param>
    /// <returns>The number of bytes copied</returns>
    long CopyFile(INioReadableFile source, INioWritableFile target);
    
    /// <summary>
    /// Moves a file.
    /// </summary>
    /// <param name="source">The source file</param>
    /// <param name="target">The target file</param>
    void MoveFile(INioWritableFile source, INioWritableFile target);
    
    /// <summary>
    /// Truncates a file to a specific size.
    /// </summary>
    /// <param name="file">The writable file</param>
    /// <param name="newSize">The new size</param>
    void TruncateFile(INioWritableFile file, long newSize);
}

/// <summary>
/// Default implementation of NIO I/O handler.
/// </summary>
public class NioIoHandler : INioIoHandler
{
    private readonly INioPathResolver _pathResolver;
    private bool _disposed;

    /// <summary>
    /// Creates a new NIO I/O handler.
    /// </summary>
    /// <returns>A new I/O handler instance</returns>
    public static NioIoHandler New()
    {
        return new NioIoHandler(NioPathResolver.New());
    }

    /// <summary>
    /// Creates a new NIO I/O handler with a custom path resolver.
    /// </summary>
    /// <param name="pathResolver">The path resolver</param>
    /// <returns>A new I/O handler instance</returns>
    public static NioIoHandler New(INioPathResolver pathResolver)
    {
        return new NioIoHandler(pathResolver ?? throw new ArgumentNullException(nameof(pathResolver)));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NioIoHandler"/> class.
    /// </summary>
    /// <param name="pathResolver">The path resolver</param>
    protected NioIoHandler(INioPathResolver pathResolver)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
    }

    /// <inheritdoc/>
    public INioReadableFile CastReadableFile(IBlobStoreReadableFile file)
    {
        if (file is INioReadableFile nioFile)
        {
            return nioFile;
        }

        throw new ArgumentException(
            $"File is not a NIO readable file: {file?.GetType().Name ?? "null"}",
            nameof(file));
    }

    /// <inheritdoc/>
    public INioWritableFile CastWritableFile(IBlobStoreWritableFile file)
    {
        if (file is INioWritableFile nioFile)
        {
            return nioFile;
        }

        throw new ArgumentException(
            $"File is not a NIO writable file: {file?.GetType().Name ?? "null"}",
            nameof(file));
    }

    /// <inheritdoc/>
    public string ToPath(BlobStorePath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return _pathResolver.ResolvePath(path.PathElements);
    }

    /// <inheritdoc/>
    public string ToPath(params string[] pathElements)
    {
        return _pathResolver.ResolvePath(pathElements);
    }

    /// <inheritdoc/>
    public bool FileExists(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        return File.Exists(fsPath);
    }

    /// <inheritdoc/>
    public bool DirectoryExists(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        return Directory.Exists(fsPath);
    }

    /// <inheritdoc/>
    public long GetFileSize(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        var fileInfo = new FileInfo(fsPath);
        return fileInfo.Exists ? fileInfo.Length : 0;
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListItems(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        if (!Directory.Exists(fsPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFileSystemEntries(fsPath)
            .Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListDirectories(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        if (!Directory.Exists(fsPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateDirectories(fsPath)
            .Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListFiles(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        if (!Directory.Exists(fsPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(fsPath)
            .Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    /// <inheritdoc/>
    public void CreateDirectory(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        Directory.CreateDirectory(fsPath);
    }

    /// <inheritdoc/>
    public void CreateFile(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        
        // Ensure parent directory exists
        var parentDir = System.IO.Path.GetDirectoryName(fsPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // Create empty file
        using var fs = File.Create(fsPath);
    }

    /// <inheritdoc/>
    public bool DeleteFile(BlobStorePath path)
    {
        var fsPath = ToPath(path);
        if (!File.Exists(fsPath))
        {
            return false;
        }

        File.Delete(fsPath);
        return true;
    }

    /// <inheritdoc/>
    public byte[] ReadBytes(INioReadableFile file)
    {
        var stream = file.EnsureOpenStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <inheritdoc/>
    public byte[] ReadBytes(INioReadableFile file, long position, long length)
    {
        var stream = file.EnsureOpenStream();

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be non-negative");
        }

        if (length < 0)
        {
            length = stream.Length - position;
        }

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[length];
        stream.Position = position;
        var bytesRead = stream.Read(buffer, 0, (int)length);

        if (bytesRead < length)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return buffer;
    }

    /// <inheritdoc/>
    public long WriteBytes(INioWritableFile file, byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return 0;
        }

        var stream = file.EnsureOpenStream();
        stream.Write(data, 0, data.Length);
        stream.Flush();
        return data.Length;
    }

    /// <inheritdoc/>
    public long CopyFile(INioReadableFile source, INioWritableFile target)
    {
        var sourceStream = source.EnsureOpenStream();
        var targetStream = target.EnsureOpenStream();

        var initialPosition = sourceStream.Position;
        sourceStream.Position = 0;

        sourceStream.CopyTo(targetStream);
        targetStream.Flush();

        var bytesCopied = sourceStream.Length;
        sourceStream.Position = initialPosition;

        return bytesCopied;
    }

    /// <inheritdoc/>
    public void MoveFile(INioWritableFile source, INioWritableFile target)
    {
        // Close streams before moving
        source.CloseStream();
        target.CloseStream();

        var sourcePath = source.Path;
        var targetPath = target.Path;

        // Ensure target directory exists
        var targetDir = System.IO.Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Move(sourcePath, targetPath, overwrite: true);
    }

    /// <inheritdoc/>
    public void TruncateFile(INioWritableFile file, long newSize)
    {
        if (newSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newSize), "New size must be non-negative");
        }

        var stream = file.EnsureOpenStream();
        stream.SetLength(newSize);
        stream.Flush();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this I/O handler.
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
            // No resources to dispose currently
        }

        _disposed = true;
    }
}
