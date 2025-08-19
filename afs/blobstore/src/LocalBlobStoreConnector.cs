using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Local file system implementation of IBlobStoreConnector.
/// Stores blobs as numbered files in the local file system.
/// </summary>
public class LocalBlobStoreConnector : BlobStoreConnectorBase
{
    private readonly string _basePath;
    private readonly bool _useCache;
    private readonly Dictionary<string, bool> _directoryExistsCache = new();
    private readonly Dictionary<string, bool> _fileExistsCache = new();
    private readonly Dictionary<string, long> _fileSizeCache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the LocalBlobStoreConnector class.
    /// </summary>
    /// <param name="basePath">The base directory path for blob storage</param>
    /// <param name="useCache">Whether to use caching for performance</param>
    public LocalBlobStoreConnector(string basePath, bool useCache = true)
    {
        _basePath = Path.GetFullPath(basePath ?? throw new ArgumentNullException(nameof(basePath)));
        _useCache = useCache;

        // Ensure base directory exists
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Converts a blob store path to a local file system path.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <returns>The local file system path</returns>
    private string ToLocalPath(BlobStorePath path)
    {
        var pathElements = path.PathElements;
        var localPath = _basePath;
        
        foreach (var element in pathElements)
        {
            localPath = Path.Combine(localPath, element);
        }
        
        return localPath;
    }

    /// <summary>
    /// Gets all blob files for the specified file path.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>Enumerable of blob file paths</returns>
    private IEnumerable<string> GetBlobFiles(BlobStorePath file)
    {
        var localPath = ToLocalPath(file);
        var directory = Path.GetDirectoryName(localPath);
        if (directory == null || !Directory.Exists(directory))
            return Enumerable.Empty<string>();

        var fileName = Path.GetFileName(localPath);
        var prefix = $"{fileName}{NumberSuffixSeparator}";
        var pattern = $"{prefix}*";

        try
        {
            return Directory.GetFiles(directory, pattern)
                .Where(f => Regex.IsMatch(Path.GetFileName(f), $@"^{Regex.Escape(prefix)}\d+$"))
                .OrderBy(f => GetBlobNumber(f));
        }
        catch (DirectoryNotFoundException)
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Extracts the blob number from a blob file path.
    /// </summary>
    /// <param name="blobFilePath">The blob file path</param>
    /// <returns>The blob number</returns>
    private long GetBlobNumber(string blobFilePath)
    {
        var fileName = Path.GetFileName(blobFilePath);
        var lastDot = fileName.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDot >= 0 && long.TryParse(fileName.Substring(lastDot + 1), out var number))
            return number;
        return 0;
    }

    /// <summary>
    /// Gets the next blob number for a file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The next blob number</returns>
    private long GetNextBlobNumber(BlobStorePath file)
    {
        var blobFiles = GetBlobFiles(file);
        return blobFiles.Any() ? blobFiles.Max(GetBlobNumber) + 1 : 0;
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileSizeCache.TryGetValue(file.FullQualifiedName, out var cachedSize))
                    return cachedSize;
            }
        }

        var totalSize = GetBlobFiles(file).Sum(blobFile => new FileInfo(blobFile).Length);

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileSizeCache[file.FullQualifiedName] = totalSize;
            }
        }

        return totalSize;
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_directoryExistsCache.TryGetValue(directory.FullQualifiedName, out var cached))
                    return cached;
            }
        }

        var localPath = ToLocalPath(directory);
        var exists = Directory.Exists(localPath);

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _directoryExistsCache[directory.FullQualifiedName] = exists;
            }
        }

        return exists;
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileExistsCache.TryGetValue(file.FullQualifiedName, out var cached))
                    return cached;
            }
        }

        var exists = GetBlobFiles(file).Any();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache[file.FullQualifiedName] = exists;
            }
        }

        return exists;
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();

        var localPath = ToLocalPath(directory);
        if (!Directory.Exists(localPath))
            return;

        var directories = new HashSet<string>();
        var files = new HashSet<string>();

        // Visit subdirectories
        foreach (var subDir in Directory.GetDirectories(localPath))
        {
            var dirName = Path.GetFileName(subDir);
            directories.Add(dirName);
        }

        // Visit files (extract file names from blob files)
        foreach (var file in Directory.GetFiles(localPath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Contains(NumberSuffixSeparatorChar))
            {
                var baseName = RemoveNumberSuffix(fileName);
                files.Add(baseName);
            }
        }

        foreach (var dir in directories)
            visitor.VisitDirectory(directory, dir);

        foreach (var file in files)
            visitor.VisitFile(directory, file);
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();

        var localPath = ToLocalPath(directory);
        if (!Directory.Exists(localPath))
            return true;

        return !Directory.EnumerateFileSystemEntries(localPath).Any();
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();

        var localPath = ToLocalPath(directory);
        try
        {
            Directory.CreateDirectory(localPath);
            
            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _directoryExistsCache[directory.FullQualifiedName] = true;
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        // Files are created implicitly when data is written
        var directory = file.ParentPath;
        if (directory != null && directory is BlobStorePath parentPath)
        {
            return CreateDirectory(parentPath);
        }
        return true;
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        try
        {
            var blobFiles = GetBlobFiles(file).ToList();
            foreach (var blobFile in blobFiles)
            {
                File.Delete(blobFile);
            }

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _fileExistsCache.Remove(file.FullQualifiedName);
                    _fileSizeCache.Remove(file.FullQualifiedName);
                }
            }

            return blobFiles.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();

        var blobFiles = GetBlobFiles(file).ToList();
        if (!blobFiles.Any())
            return Array.Empty<byte>();

        var totalSize = blobFiles.Sum(f => new FileInfo(f).Length);
        var actualLength = length < 0 ? totalSize - offset : Math.Min(length, totalSize - offset);
        
        if (actualLength <= 0)
            return Array.Empty<byte>();

        var result = new byte[actualLength];
        var resultOffset = 0;
        var currentOffset = 0L;

        foreach (var blobFile in blobFiles)
        {
            var blobSize = new FileInfo(blobFile).Length;
            
            if (currentOffset + blobSize <= offset)
            {
                currentOffset += blobSize;
                continue;
            }

            var blobOffset = Math.Max(0, offset - currentOffset);
            var bytesToRead = Math.Min(blobSize - blobOffset, actualLength - resultOffset);

            using var stream = File.OpenRead(blobFile);
            stream.Seek(blobOffset, SeekOrigin.Begin);
            var bytesRead = stream.Read(result, resultOffset, (int)bytesToRead);
            
            resultOffset += bytesRead;
            currentOffset += blobSize;

            if (resultOffset >= actualLength)
                break;
        }

        return result;
    }

    public override long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length)
    {
        var data = ReadData(file, offset, length);
        var bytesToCopy = Math.Min(data.Length, targetBuffer.Length);
        Array.Copy(data, 0, targetBuffer, 0, bytesToCopy);
        return bytesToCopy;
    }

    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers)
    {
        EnsureNotDisposed();

        // Ensure parent directory exists
        var parentPath = file.ParentPath;
        if (parentPath != null && parentPath is BlobStorePath blobParentPath)
        {
            CreateDirectory(blobParentPath);
        }

        var blobNumber = GetNextBlobNumber(file);
        var localPath = ToLocalPath(file);
        var directory = Path.GetDirectoryName(localPath);
        var fileName = Path.GetFileName(localPath);
        var blobFileName = $"{fileName}{NumberSuffixSeparator}{blobNumber}";
        var blobFilePath = Path.Combine(directory!, blobFileName);

        long totalWritten = 0;
        using var stream = File.Create(blobFilePath);
        
        foreach (var buffer in sourceBuffers)
        {
            stream.Write(buffer, 0, buffer.Length);
            totalWritten += buffer.Length;
        }

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache[file.FullQualifiedName] = true;
                _fileSizeCache.TryGetValue(file.FullQualifiedName, out var currentSize);
                _fileSizeCache[file.FullQualifiedName] = currentSize + totalWritten;
            }
        }

        return totalWritten;
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();

        CopyFile(sourceFile, targetFile, 0, -1);
        DeleteFile(sourceFile);
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();

        var data = ReadData(sourceFile, offset, length);
        return WriteData(targetFile, new[] { data });
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();

        if (newLength == 0)
        {
            DeleteFile(file);
            return;
        }

        var data = ReadData(file, 0, newLength);
        DeleteFile(file);
        WriteData(file, new[] { data });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_cacheLock)
            {
                _directoryExistsCache.Clear();
                _fileExistsCache.Clear();
                _fileSizeCache.Clear();
            }
        }
        base.Dispose(disposing);
    }
}
