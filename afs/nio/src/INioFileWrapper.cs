using System;
using System.IO;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO file wrappers that provide file channel access.
/// </summary>
public interface INioFileWrapper : INioItemWrapper, IDisposable
{
    /// <summary>
    /// Gets the file stream (equivalent to Java's FileChannel).
    /// </summary>
    FileStream? FileStream { get; }
    
    /// <summary>
    /// Gets the user context associated with this file.
    /// </summary>
    object? User { get; }
    
    /// <summary>
    /// Retires this file wrapper, preventing further operations.
    /// </summary>
    /// <returns>True if the file was retired, false if already retired</returns>
    bool Retire();
    
    /// <summary>
    /// Gets a value indicating whether this file wrapper is retired.
    /// </summary>
    bool IsRetired { get; }
    
    /// <summary>
    /// Gets a value indicating whether the file stream is open.
    /// </summary>
    bool IsStreamOpen { get; }
    
    /// <summary>
    /// Checks if the file stream is open and validates the file is not retired.
    /// </summary>
    /// <returns>True if the stream is open</returns>
    bool CheckStreamOpen();
    
    /// <summary>
    /// Ensures the file stream is open, opening it if necessary.
    /// </summary>
    /// <returns>The open file stream</returns>
    FileStream EnsureOpenStream();
    
    /// <summary>
    /// Ensures the file stream is open with specific options.
    /// </summary>
    /// <param name="mode">The file mode</param>
    /// <param name="access">The file access</param>
    /// <param name="share">The file share mode</param>
    /// <returns>The open file stream</returns>
    FileStream EnsureOpenStream(FileMode mode, FileAccess access, FileShare share);
    
    /// <summary>
    /// Opens the file stream.
    /// </summary>
    /// <returns>True if the stream was opened, false if already open</returns>
    bool OpenStream();
    
    /// <summary>
    /// Opens the file stream with specific options.
    /// </summary>
    /// <param name="mode">The file mode</param>
    /// <param name="access">The file access</param>
    /// <param name="share">The file share mode</param>
    /// <returns>True if the stream was opened, false if already open</returns>
    bool OpenStream(FileMode mode, FileAccess access, FileShare share);
    
    /// <summary>
    /// Reopens the file stream with specific options.
    /// </summary>
    /// <param name="mode">The file mode</param>
    /// <param name="access">The file access</param>
    /// <param name="share">The file share mode</param>
    /// <returns>True if the stream was reopened</returns>
    bool ReopenStream(FileMode mode, FileAccess access, FileShare share);
    
    /// <summary>
    /// Closes the file stream.
    /// </summary>
    /// <returns>True if the stream was closed, false if already closed</returns>
    bool CloseStream();
}

