using System;
using System.IO;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Base class for NIO file wrappers.
/// </summary>
/// <typeparam name="TUser">The type of user context</typeparam>
public abstract class NioFileWrapperBase<TUser> : INioFileWrapper
{
    private readonly object _mutex = new();
    private string? _path;
    private FileStream? _fileStream;
    private readonly BlobStorePath _blobPath;
    private readonly TUser? _user;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NioFileWrapperBase{TUser}"/> class.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <param name="fileStream">The optional file stream</param>
    protected NioFileWrapperBase(
        BlobStorePath blobPath,
        TUser? user,
        string path,
        FileStream? fileStream = null)
    {
        _blobPath = blobPath ?? throw new ArgumentNullException(nameof(blobPath));
        _user = user;
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _fileStream = fileStream;
        EnsurePositionAtFileEnd();
    }

    /// <summary>
    /// Gets the mutex for thread synchronization.
    /// </summary>
    protected object Mutex => _mutex;

    /// <inheritdoc/>
    public string Path
    {
        get
        {
            lock (_mutex)
            {
                ValidateIsNotRetired();
                return _path!;
            }
        }
    }

    /// <inheritdoc/>
    public BlobStorePath BlobPath => _blobPath;

    /// <inheritdoc/>
    public FileStream? FileStream
    {
        get
        {
            lock (_mutex)
            {
                ValidateIsNotRetired();
                return _fileStream;
            }
        }
    }

    /// <inheritdoc/>
    public object? User => _user;

    /// <inheritdoc/>
    public bool IsRetired
    {
        get
        {
            lock (_mutex)
            {
                return _path == null;
            }
        }
    }

    /// <inheritdoc/>
    public bool Retire()
    {
        lock (_mutex)
        {
            if (_path == null)
            {
                return false;
            }

            _path = null;
            return true;
        }
    }

    /// <summary>
    /// Validates that this file wrapper is not retired.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the file is retired</exception>
    protected void ValidateIsNotRetired()
    {
        if (!IsRetired)
        {
            return;
        }

        throw new InvalidOperationException(
            $"File is retired: {GetType().Name}(\"{_blobPath.FullQualifiedName}\").");
    }

    /// <inheritdoc/>
    public bool IsStreamOpen
    {
        get
        {
            lock (_mutex)
            {
                return _fileStream != null && _fileStream.CanRead;
            }
        }
    }

    /// <inheritdoc/>
    public bool CheckStreamOpen()
    {
        lock (_mutex)
        {
            ValidateIsNotRetired();
            return IsStreamOpen;
        }
    }

    /// <inheritdoc/>
    public FileStream EnsureOpenStream()
    {
        lock (_mutex)
        {
            ValidateIsNotRetired();
            OpenStream(GetDefaultFileMode(), GetDefaultFileAccess(), GetDefaultFileShare());
            return _fileStream!;
        }
    }

    /// <inheritdoc/>
    public FileStream EnsureOpenStream(FileMode mode, FileAccess access, FileShare share)
    {
        lock (_mutex)
        {
            ValidateIsNotRetired();
            OpenStream(mode, access, share);
            return _fileStream!;
        }
    }

    /// <inheritdoc/>
    public bool OpenStream()
    {
        lock (_mutex)
        {
            return OpenStream(GetDefaultFileMode(), GetDefaultFileAccess(), GetDefaultFileShare());
        }
    }

    /// <inheritdoc/>
    public bool OpenStream(FileMode mode, FileAccess access, FileShare share)
    {
        lock (_mutex)
        {
            if (CheckStreamOpen())
            {
                return false;
            }

            ValidateOpenOptions(mode, access, share);

            try
            {
                var fileStream = new FileStream(_path!, mode, access, share);
                InternalSetFileStream(fileStream);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to open file stream: {_path}", ex);
            }

            return true;
        }
    }

    /// <inheritdoc/>
    public bool ReopenStream(FileMode mode, FileAccess access, FileShare share)
    {
        lock (_mutex)
        {
            CloseStream();
            return OpenStream(mode, access, share);
        }
    }

    /// <inheritdoc/>
    public bool CloseStream()
    {
        lock (_mutex)
        {
            if (!IsStreamOpen)
            {
                return false;
            }

            EnsureClearedFileStreamField();
            return true;
        }
    }

    /// <summary>
    /// Gets the default file mode for this file wrapper.
    /// </summary>
    /// <returns>The default file mode</returns>
    protected abstract FileMode GetDefaultFileMode();

    /// <summary>
    /// Gets the default file access for this file wrapper.
    /// </summary>
    /// <returns>The default file access</returns>
    protected abstract FileAccess GetDefaultFileAccess();

    /// <summary>
    /// Gets the default file share mode for this file wrapper.
    /// </summary>
    /// <returns>The default file share mode</returns>
    protected abstract FileShare GetDefaultFileShare();

    /// <summary>
    /// Validates the open options for this file wrapper.
    /// </summary>
    /// <param name="mode">The file mode</param>
    /// <param name="access">The file access</param>
    /// <param name="share">The file share mode</param>
    protected abstract void ValidateOpenOptions(FileMode mode, FileAccess access, FileShare share);

    private void InternalSetFileStream(FileStream fileStream)
    {
        _fileStream = fileStream;
        EnsurePositionAtFileEnd();
    }

    private void EnsurePositionAtFileEnd()
    {
        if (_fileStream == null)
        {
            return;
        }

        try
        {
            var fileSize = _fileStream.Length;
            if (_fileStream.Position != fileSize)
            {
                _fileStream.Position = fileSize;
            }
        }
        catch (Exception ex)
        {
            EnsureClearedFileStreamField(ex);
            throw new IOException("Failed to position stream at file end", ex);
        }
    }

    private void EnsureClearedFileStreamField(Exception? cause = null)
    {
        var fs = _fileStream;
        _fileStream = null;
        
        try
        {
            fs?.Dispose();
        }
        catch (Exception ex)
        {
            if (cause != null)
            {
                throw new AggregateException(cause, ex);
            }
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this file wrapper.
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
            lock (_mutex)
            {
                EnsureClearedFileStreamField();
            }
        }

        _disposed = true;
    }
}

