using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Interface for blob store connectors that handle concrete I/O operations.
/// All operations must be implemented thread-safe.
/// </summary>
public interface IBlobStoreConnector : IDisposable
{
    /// <summary>
    /// Gets the size of the specified file in bytes.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The file size in bytes</returns>
    long GetFileSize(BlobStorePath file);

    /// <summary>
    /// Checks if the specified directory exists.
    /// </summary>
    /// <param name="directory">The directory path</param>
    /// <returns>True if the directory exists</returns>
    bool DirectoryExists(BlobStorePath directory);

    /// <summary>
    /// Checks if the specified file exists.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>True if the file exists</returns>
    bool FileExists(BlobStorePath file);

    /// <summary>
    /// Visits all children of the specified directory.
    /// </summary>
    /// <param name="directory">The directory to visit</param>
    /// <param name="visitor">The visitor to handle each child</param>
    void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor);

    /// <summary>
    /// Checks if the specified directory is empty.
    /// </summary>
    /// <param name="directory">The directory path</param>
    /// <returns>True if the directory is empty</returns>
    bool IsEmpty(BlobStorePath directory);

    /// <summary>
    /// Creates the specified directory.
    /// </summary>
    /// <param name="directory">The directory path</param>
    /// <returns>True if the directory was created successfully</returns>
    bool CreateDirectory(BlobStorePath directory);

    /// <summary>
    /// Creates the specified file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>True if the file was created successfully</returns>
    bool CreateFile(BlobStorePath file);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>True if the file was deleted successfully</returns>
    bool DeleteFile(BlobStorePath file);

    /// <summary>
    /// Reads data from the specified file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="offset">The offset to start reading from</param>
    /// <param name="length">The number of bytes to read, or -1 to read all</param>
    /// <returns>The read data as a byte array</returns>
    byte[] ReadData(BlobStorePath file, long offset, long length);

    /// <summary>
    /// Reads data from the specified file into the target buffer.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="targetBuffer">The buffer to read data into</param>
    /// <param name="offset">The offset to start reading from</param>
    /// <param name="length">The number of bytes to read</param>
    /// <returns>The number of bytes actually read</returns>
    long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length);

    /// <summary>
    /// Writes data to the specified file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="sourceBuffers">The data to write</param>
    /// <returns>The number of bytes written</returns>
    long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers);

    /// <summary>
    /// Moves a file from source to target location.
    /// </summary>
    /// <param name="sourceFile">The source file path</param>
    /// <param name="targetFile">The target file path</param>
    void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile);

    /// <summary>
    /// Copies data from source file to target file.
    /// </summary>
    /// <param name="sourceFile">The source file path</param>
    /// <param name="targetFile">The target file path</param>
    /// <param name="offset">The offset to start copying from</param>
    /// <param name="length">The number of bytes to copy, or -1 to copy all</param>
    /// <returns>The number of bytes copied</returns>
    long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length);

    /// <summary>
    /// Truncates the specified file to the new length.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="newLength">The new file length</param>
    void TruncateFile(BlobStorePath file, long newLength);
}

/// <summary>
/// Interface for visiting blob store paths during directory traversal.
/// </summary>
public interface IBlobStorePathVisitor
{
    /// <summary>
    /// Called when a directory is visited.
    /// </summary>
    /// <param name="parent">The parent directory</param>
    /// <param name="directoryName">The name of the directory</param>
    void VisitDirectory(BlobStorePath parent, string directoryName);

    /// <summary>
    /// Called when a file is visited.
    /// </summary>
    /// <param name="parent">The parent directory</param>
    /// <param name="fileName">The name of the file</param>
    void VisitFile(BlobStorePath parent, string fileName);
}

/// <summary>
/// Abstract base implementation of IBlobStoreConnector with common functionality.
/// </summary>
public abstract class BlobStoreConnectorBase : IBlobStoreConnector
{
    protected const string NumberSuffixSeparator = ".";
    protected const char NumberSuffixSeparatorChar = '.';
    
    private bool _disposed;

    /// <summary>
    /// Ensures the connector is not disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the connector is disposed</exception>
    protected void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Converts a container key to a directory path.
    /// </summary>
    /// <param name="directory">The directory path</param>
    /// <returns>The container key</returns>
    protected static string ToContainerKey(BlobStorePath directory)
    {
        // Container keys have a trailing /
        var elements = directory.PathElements.Skip(1); // Skip container
        return string.Join(BlobStorePath.Separator, elements) + BlobStorePath.Separator;
    }

    /// <summary>
    /// Converts a file path and number to a blob key.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="number">The blob number</param>
    /// <returns>The blob key</returns>
    protected static string ToBlobKey(BlobStorePath file, long number)
    {
        return ToBlobKeyPrefix(file) + number.ToString();
    }

    /// <summary>
    /// Gets the blob key prefix for a file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The blob key prefix</returns>
    protected static string ToBlobKeyPrefix(BlobStorePath file)
    {
        var elements = file.PathElements.Skip(1); // Skip container
        return string.Join(BlobStorePath.Separator, elements) + NumberSuffixSeparator;
    }

    /// <summary>
    /// Removes the number suffix from a key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The key without number suffix</returns>
    protected static string RemoveNumberSuffix(string key)
    {
        var lastDot = key.LastIndexOf(NumberSuffixSeparatorChar);
        return lastDot >= 0 ? key.Substring(0, lastDot) : key;
    }

    /// <summary>
    /// Checks if a key represents a blob (not a directory).
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key is a blob key</returns>
    protected static bool IsBlobKey(string key)
    {
        return !IsDirectoryKey(key);
    }

    /// <summary>
    /// Checks if a key represents a directory.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key is a directory key</returns>
    protected static bool IsDirectoryKey(string key)
    {
        return key.EndsWith(BlobStorePath.Separator);
    }

    public abstract long GetFileSize(BlobStorePath file);
    public abstract bool DirectoryExists(BlobStorePath directory);
    public abstract bool FileExists(BlobStorePath file);
    public abstract void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor);
    public abstract bool IsEmpty(BlobStorePath directory);
    public abstract bool CreateDirectory(BlobStorePath directory);
    public abstract bool CreateFile(BlobStorePath file);
    public abstract bool DeleteFile(BlobStorePath file);
    public abstract byte[] ReadData(BlobStorePath file, long offset, long length);
    public abstract long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length);
    public abstract long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers);
    public abstract void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile);
    public abstract long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length);
    public abstract void TruncateFile(BlobStorePath file, long newLength);

    /// <summary>
    /// Disposes the connector.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Dispose(true);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Disposes the connector resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        // Override in derived classes
    }
}
