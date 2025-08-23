using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types.Files;

/// <summary>
/// Default implementation of IStorageLiveDataFile representing a storage data file.
/// </summary>
public class StorageLiveDataFile : IStorageLiveDataFile
{
    #region Private Fields

    private readonly long _number;
    private readonly int _channelIndex;
    private readonly string _filePath;
    private readonly object _lock = new object();
    private readonly ConcurrentDictionary<IStorageFileUser, bool> _users = new();

    private FileStream? _fileStream;
    private long _totalLength;
    private long _dataLength;
    private IStorageEntity? _firstEntity;
    private IStorageEntity? _lastEntity;
    private bool _disposed = false;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageLiveDataFile class.
    /// </summary>
    /// <param name="number">The file number.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="filePath">The file path.</param>
    public StorageLiveDataFile(long number, int channelIndex, string filePath)
    {
        _number = number;
        _channelIndex = channelIndex;
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    #endregion

    #region IStorageLiveDataFile Implementation

    public long Number => _number;

    public int ChannelIndex => _channelIndex;

    public long TotalLength => Interlocked.Read(ref _totalLength);

    public long DataLength => Interlocked.Read(ref _dataLength);

    public bool HasContent => DataLength > 0;

    public bool HasUsers => !_users.IsEmpty;

    public string Identifier => $"channel_{_channelIndex:D3}_file_{_number:D6}.dat";

    public long Size
    {
        get
        {
            try
            {
                return new FileInfo(_filePath).Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    public bool Exists => File.Exists(_filePath);

    public bool IsEmpty => Size == 0;

    public int ReadBytes(byte[] buffer, long position)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        lock (_lock)
        {
            EnsureFileStreamOpen();
            
            if (_fileStream == null)
                return 0;

            _fileStream.Seek(position, SeekOrigin.Begin);
            return _fileStream.Read(buffer, 0, buffer.Length);
        }
    }

    public void IncreaseContentLength(long length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Interlocked.Add(ref _dataLength, length);
        Interlocked.Add(ref _totalLength, length);
    }

    public void Truncate(long length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_lock)
        {
            EnsureFileStreamOpen();
            
            if (_fileStream != null)
            {
                _fileStream.SetLength(length);
                _fileStream.Flush();
            }

            Interlocked.Exchange(ref _totalLength, length);
            Interlocked.Exchange(ref _dataLength, Math.Min(DataLength, length));
        }
    }

    public void CopyTo(Stream target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        lock (_lock)
        {
            EnsureFileStreamOpen();
            
            if (_fileStream != null)
            {
                _fileStream.Seek(0, SeekOrigin.Begin);
                _fileStream.CopyTo(target);
            }
        }
    }

    public bool NeedsRetirement(IStorageDataFileEvaluator evaluator)
    {
        if (evaluator == null)
            throw new ArgumentNullException(nameof(evaluator));

        return evaluator.NeedsDissolving(this);
    }

    public void RegisterUsage(IStorageFileUser user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        _users.TryAdd(user, true);
    }

    public void UnregisterUsage(IStorageFileUser user, Exception? cause)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        _users.TryRemove(user, out _);
    }

    public void AppendEntry(IStorageEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        lock (_lock)
        {
            // Add to the end of the file chain
            if (_lastEntity != null)
            {
                _lastEntity.FileNext = entity;
            }
            else
            {
                _firstEntity = entity;
            }

            _lastEntity = entity;
            entity.FileNext = null;
        }
    }

    public void RemoveHeadBoundChain(IStorageEntity newHead, long removedLength)
    {
        if (newHead == null)
            throw new ArgumentNullException(nameof(newHead));
        if (removedLength < 0)
            throw new ArgumentOutOfRangeException(nameof(removedLength));

        lock (_lock)
        {
            _firstEntity = newHead;
            Interlocked.Add(ref _dataLength, -removedLength);
        }
    }

    public void AddChainToTail(IStorageEntity first, IStorageEntity last)
    {
        if (first == null)
            throw new ArgumentNullException(nameof(first));
        if (last == null)
            throw new ArgumentNullException(nameof(last));

        lock (_lock)
        {
            if (_lastEntity != null)
            {
                _lastEntity.FileNext = first;
            }
            else
            {
                _firstEntity = first;
            }

            _lastEntity = last;
        }
    }

    #endregion

    #region IStorageFile Implementation

    public void EnsureExists()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create empty file
                using var fs = File.Create(_filePath);
                fs.Flush();
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _fileStream?.Close();
            _fileStream = null;
        }
    }

    public void Delete()
    {
        lock (_lock)
        {
            Close();
            
            if (File.Exists(_filePath))
            {
                try
                {
                    File.Delete(_filePath);
                }
                catch (Exception ex)
                {
                    throw new StorageFileException($"Failed to delete file {_filePath}", _filePath, ex);
                }
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Writes data to the file at the current position.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <returns>The number of bytes written.</returns>
    public long WriteData(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        lock (_lock)
        {
            EnsureFileStreamOpen();
            
            if (_fileStream == null)
                throw new StorageFileException($"Failed to open file stream for {_filePath}", _filePath);

            _fileStream.Seek(0, SeekOrigin.End);
            _fileStream.Write(data, 0, data.Length);
            _fileStream.Flush();

            IncreaseContentLength(data.Length);
            return data.Length;
        }
    }

    /// <summary>
    /// Writes data to the file at the specified position.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="position">The position to write at.</param>
    /// <returns>The number of bytes written.</returns>
    public long WriteDataAt(byte[] data, long position)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        lock (_lock)
        {
            EnsureFileStreamOpen();
            
            if (_fileStream == null)
                throw new StorageFileException($"Failed to open file stream for {_filePath}", _filePath);

            _fileStream.Seek(position, SeekOrigin.Begin);
            _fileStream.Write(data, 0, data.Length);
            _fileStream.Flush();

            // Update total length if we wrote beyond the current end
            var newLength = position + data.Length;
            if (newLength > TotalLength)
            {
                Interlocked.Exchange(ref _totalLength, newLength);
            }

            return data.Length;
        }
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Flushes and synchronizes the file to disk.
    /// </summary>
    public void FlushAndSync()
    {
        lock (_lock)
        {
            if (_fileStream != null)
            {
                _fileStream.Flush(true); // Force flush to disk
            }
        }
    }

    /// <summary>
    /// Commits the current state of the file.
    /// </summary>
    public void CommitState()
    {
        lock (_lock)
        {
            // Update committed state - in a real implementation this would
            // update internal state tracking for rollback purposes
            FlushAndSync();
        }
    }

    /// <summary>
    /// Resets the file to its last committed state.
    /// </summary>
    public void ResetToLastCommittedState()
    {
        lock (_lock)
        {
            // In a real implementation, this would restore the file
            // to its last committed state
            if (_fileStream != null)
            {
                _fileStream.Flush();
            }
        }
    }

    /// <summary>
    /// Truncates the file to the specified length.
    /// </summary>
    /// <param name="length">The length to truncate to.</param>
    public void TruncateToLength(long length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_lock)
        {
            EnsureFileStreamOpen();

            if (_fileStream != null)
            {
                _fileStream.SetLength(length);
                _fileStream.Flush(true);

                // Update internal length tracking
                Interlocked.Exchange(ref _totalLength, length);
                Interlocked.Exchange(ref _dataLength, Math.Min(_dataLength, length));
            }
        }
    }

    #endregion

    #region Private Methods

    private void EnsureFileStreamOpen()
    {
        if (_fileStream == null)
        {
            EnsureExists();
            _fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            
            // Update total length from actual file size
            Interlocked.Exchange(ref _totalLength, _fileStream.Length);
        }
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Close();
                _users.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageLiveDataFile instance.
    /// </summary>
    /// <param name="number">The file number.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="storageDirectory">The storage directory.</param>
    /// <returns>A new StorageLiveDataFile instance.</returns>
    public static StorageLiveDataFile Create(long number, int channelIndex, string storageDirectory)
    {
        var fileName = $"channel_{channelIndex:D3}_file_{number:D6}.dat";
        var filePath = Path.Combine(storageDirectory, fileName);
        return new StorageLiveDataFile(number, channelIndex, filePath);
    }

    #endregion

    #region Overrides

    public override string ToString()
    {
        return $"StorageLiveDataFile[Number={_number}, Channel={_channelIndex}, Size={Size}, DataLength={DataLength}]";
    }

    #endregion
}
