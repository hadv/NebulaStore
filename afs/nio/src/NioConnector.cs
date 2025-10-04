using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// NIO connector that implements blob store connector interface using local file system.
/// </summary>
public class NioConnector : IBlobStoreConnector
{
    private readonly string _baseDirectory;
    private readonly INioIoHandler _ioHandler;
    private readonly bool _useCache;
    private bool _disposed;

    /// <summary>
    /// Creates a new NIO connector.
    /// </summary>
    /// <param name="baseDirectory">The base directory for storage</param>
    /// <param name="useCache">Whether to use caching</param>
    /// <returns>A new NIO connector instance</returns>
    public static NioConnector New(string baseDirectory, bool useCache = false)
    {
        return new NioConnector(baseDirectory, useCache);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NioConnector"/> class.
    /// </summary>
    /// <param name="baseDirectory">The base directory for storage</param>
    /// <param name="useCache">Whether to use caching</param>
    public NioConnector(string baseDirectory, bool useCache = false)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be null or empty", nameof(baseDirectory));
        }

        _baseDirectory = Path.GetFullPath(baseDirectory);
        _useCache = useCache;
        _ioHandler = NioIoHandler.New();

        // Ensure base directory exists
        Directory.CreateDirectory(_baseDirectory);
    }

    /// <summary>
    /// Gets the full file system path for a blob store path.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <returns>The full file system path</returns>
    private string GetFullPath(BlobStorePath path)
    {
        var relativePath = _ioHandler.ToPath(path);
        return Path.Combine(_baseDirectory, relativePath);
    }

    /// <inheritdoc/>
    public bool FileExists(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);
        return File.Exists(fullPath);
    }

    /// <inheritdoc/>
    public bool DirectoryExists(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);
        return Directory.Exists(fullPath);
    }

    /// <inheritdoc/>
    public bool CreateDirectory(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (Directory.Exists(fullPath))
        {
            return false;
        }

        Directory.CreateDirectory(fullPath);
        return true;
    }

    /// <inheritdoc/>
    public bool CreateFile(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (File.Exists(fullPath))
        {
            return false;
        }

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // Create empty file
        using var fs = File.Create(fullPath);
        return true;
    }

    /// <inheritdoc/>
    public bool DeleteFile(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);
        
        if (!File.Exists(fullPath))
        {
            return false;
        }

        File.Delete(fullPath);
        return true;
    }

    /// <inheritdoc/>
    public byte[] ReadData(BlobStorePath path, long offset, long length)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path.FullQualifiedName}", fullPath);
        }

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (offset < 0)
        {
            offset = 0;
        }

        if (length < 0)
        {
            length = fs.Length - offset;
        }

        if (offset >= fs.Length)
        {
            return Array.Empty<byte>();
        }

        var actualLength = Math.Min(length, fs.Length - offset);
        var buffer = new byte[actualLength];

        fs.Position = offset;
        var bytesRead = fs.Read(buffer, 0, (int)actualLength);

        if (bytesRead < actualLength)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return buffer;
    }

    /// <inheritdoc/>
    public long ReadData(BlobStorePath path, byte[] targetBuffer, long offset, long length)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path.FullQualifiedName}", fullPath);
        }

        if (targetBuffer == null)
        {
            throw new ArgumentNullException(nameof(targetBuffer));
        }

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (offset < 0)
        {
            offset = 0;
        }

        if (length < 0)
        {
            length = fs.Length - offset;
        }

        if (offset >= fs.Length)
        {
            return 0;
        }

        var actualLength = Math.Min(length, fs.Length - offset);
        actualLength = Math.Min(actualLength, targetBuffer.Length);

        fs.Position = offset;
        return fs.Read(targetBuffer, 0, (int)actualLength);
    }

    /// <inheritdoc/>
    public long WriteData(BlobStorePath path, IEnumerable<byte[]> dataChunks)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        // Ensure parent directory exists
        var parentDir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        long totalBytesWritten = 0;
        using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        foreach (var chunk in dataChunks)
        {
            if (chunk != null && chunk.Length > 0)
            {
                fs.Write(chunk, 0, chunk.Length);
                totalBytesWritten += chunk.Length;
            }
        }

        fs.Flush();
        return totalBytesWritten;
    }

    /// <summary>
    /// Writes data to a file (convenience method).
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="data">The data to write</param>
    public void WriteData(BlobStorePath path, byte[] data)
    {
        WriteData(path, new[] { data });
    }

    /// <inheritdoc/>
    public long GetFileSize(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);
        
        if (!File.Exists(fullPath))
        {
            return 0;
        }

        var fileInfo = new FileInfo(fullPath);
        return fileInfo.Length;
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListFiles(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(fullPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListDirectories(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateDirectories(fullPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    /// <inheritdoc/>
    public void MoveFile(BlobStorePath sourcePath, BlobStorePath targetPath)
    {
        EnsureNotDisposed();
        var sourceFullPath = GetFullPath(sourcePath);
        var targetFullPath = GetFullPath(targetPath);

        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath.FullQualifiedName}", sourceFullPath);
        }

        // Ensure target directory exists
        var targetDir = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Move(sourceFullPath, targetFullPath, overwrite: true);
    }

    /// <inheritdoc/>
    public long CopyFile(BlobStorePath sourcePath, BlobStorePath targetPath, long offset, long length)
    {
        EnsureNotDisposed();
        var sourceFullPath = GetFullPath(sourcePath);
        var targetFullPath = GetFullPath(targetPath);

        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath.FullQualifiedName}", sourceFullPath);
        }

        // Ensure target directory exists
        var targetDir = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        using var sourceStream = new FileStream(sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var targetStream = new FileStream(targetFullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        if (offset < 0)
        {
            offset = 0;
        }

        if (length < 0)
        {
            length = sourceStream.Length - offset;
        }

        if (offset >= sourceStream.Length)
        {
            return 0;
        }

        var actualLength = Math.Min(length, sourceStream.Length - offset);
        sourceStream.Position = offset;

        var buffer = new byte[81920]; // 80KB buffer
        long totalCopied = 0;
        long remaining = actualLength;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var bytesRead = sourceStream.Read(buffer, 0, toRead);

            if (bytesRead == 0)
            {
                break;
            }

            targetStream.Write(buffer, 0, bytesRead);
            totalCopied += bytesRead;
            remaining -= bytesRead;
        }

        targetStream.Flush();
        return totalCopied;
    }

    /// <summary>
    /// Copies a file (convenience method that copies the entire file).
    /// </summary>
    /// <param name="sourcePath">The source file path</param>
    /// <param name="targetPath">The target file path</param>
    public void CopyFile(BlobStorePath sourcePath, BlobStorePath targetPath)
    {
        CopyFile(sourcePath, targetPath, 0, -1);
    }

    /// <inheritdoc/>
    public void VisitChildren(BlobStorePath path, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();

        if (visitor == null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        // Visit directories
        foreach (var dir in Directory.EnumerateDirectories(fullPath))
        {
            var dirName = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(dirName))
            {
                visitor.VisitDirectory(path, dirName);
            }
        }

        // Visit files
        foreach (var file in Directory.EnumerateFiles(fullPath))
        {
            var fileName = Path.GetFileName(file);
            if (!string.IsNullOrEmpty(fileName))
            {
                visitor.VisitFile(path, fileName);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEmpty(BlobStorePath path)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return true;
        }

        return !Directory.EnumerateFileSystemEntries(fullPath).Any();
    }

    /// <inheritdoc/>
    public void TruncateFile(BlobStorePath path, long newLength)
    {
        EnsureNotDisposed();
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path.FullQualifiedName}", fullPath);
        }

        if (newLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newLength), "New length must be non-negative");
        }

        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.SetLength(newLength);
        fs.Flush();
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this connector.
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

