using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// High-performance memory-mapped file implementation with efficient virtual memory management.
/// </summary>
public class MemoryMappedFile : IMemoryMappedFile
{
    private readonly string _filePath;
    private readonly MemoryMappedFileConfiguration _configuration;
    private readonly MemoryMappedFileStatistics _statistics;
    private readonly ConcurrentDictionary<long, WeakReference<IMemoryMappedViewAccessor>> _viewCache;
    private readonly ReaderWriterLockSlim _fileLock;
    private readonly Timer? _writeBehindTimer;
    
    private System.IO.MemoryMappedFiles.MemoryMappedFile? _mmf;
    private FileStream? _fileStream;
    private long _size;
    private long _capacity;
    private bool _isReadOnly;
    private volatile bool _isDisposed;

    public MemoryMappedFile(string filePath, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, MemoryMappedFileConfiguration? configuration = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _configuration = configuration ?? new MemoryMappedFileConfiguration();
        _statistics = new MemoryMappedFileStatistics();
        _viewCache = new ConcurrentDictionary<long, WeakReference<IMemoryMappedViewAccessor>>();
        _fileLock = new ReaderWriterLockSlim();
        _isReadOnly = access == MemoryMappedFileAccess.Read;

        // Set up write-behind timer if enabled
        if (_configuration.EnableWriteBehind && !_isReadOnly)
        {
            _writeBehindTimer = new Timer(WriteBehindCallback, null, _configuration.WriteBehindInterval, _configuration.WriteBehindInterval);
        }

        Initialize(access);
    }

    public string FilePath => _filePath;
    public long Size => Interlocked.Read(ref _size);
    public long Capacity => Interlocked.Read(ref _capacity);
    public bool IsReadOnly => _isReadOnly;
    public bool IsMapped => _mmf != null;
    public IMemoryMappedFileStatistics Statistics => _statistics;

    public IMemoryMappedViewAccessor CreateViewAccessor(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
    {
        ThrowIfDisposed();
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (offset + size > Capacity) throw new ArgumentOutOfRangeException("View extends beyond file capacity");

        // Check cache first
        var cacheKey = (offset << 32) | (size & 0xFFFFFFFF);
        if (_viewCache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out var cachedAccessor))
        {
            if (cachedAccessor.IsValid)
            {
                return cachedAccessor;
            }
            else
            {
                _viewCache.TryRemove(cacheKey, out _);
            }
        }

        _fileLock.EnterReadLock();
        try
        {
            if (_mmf == null)
                throw new InvalidOperationException("Memory-mapped file is not initialized");

            var mmvAccessor = _mmf.CreateViewAccessor(offset, size, access);
            var accessor = new MemoryMappedViewAccessor(mmvAccessor, offset, size, access, _statistics);
            
            _statistics.RecordViewCreated();

            // Cache the accessor if caching is enabled
            if (_configuration.EnableAutoViewManagement && _viewCache.Count < _configuration.ViewCacheSize)
            {
                _viewCache.TryAdd(cacheKey, new WeakReference<IMemoryMappedViewAccessor>(accessor));
            }

            return accessor;
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    public Stream CreateViewStream(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
    {
        ThrowIfDisposed();
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (offset + size > Capacity) throw new ArgumentOutOfRangeException("View extends beyond file capacity");

        _fileLock.EnterReadLock();
        try
        {
            if (_mmf == null)
                throw new InvalidOperationException("Memory-mapped file is not initialized");

            return _mmf.CreateViewStream(offset, size, access);
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    public int Read(long offset, byte[] buffer, int bufferOffset, int count)
    {
        ThrowIfDisposed();
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (bufferOffset < 0 || bufferOffset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
        if (count < 0 || bufferOffset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

        using var accessor = CreateViewAccessor(offset, Math.Min(count, Capacity - offset), MemoryMappedFileAccess.Read);
        var bytesRead = accessor.ReadArray(0, buffer, bufferOffset, count);
        
        _statistics.RecordReadOperation(bytesRead);
        return bytesRead;
    }

    public void Write(long offset, byte[] buffer, int bufferOffset, int count)
    {
        ThrowIfDisposed();
        if (_isReadOnly) throw new InvalidOperationException("Cannot write to read-only file");
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (bufferOffset < 0 || bufferOffset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
        if (count < 0 || bufferOffset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

        // Expand file if necessary
        if (offset + count > Capacity)
        {
            Expand(offset + count);
        }

        using var accessor = CreateViewAccessor(offset, count, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, buffer, bufferOffset, count);
        
        _statistics.RecordWriteOperation(count);
    }

    public async Task<int> ReadAsync(long offset, byte[] buffer, int bufferOffset, int count, CancellationToken cancellationToken = default)
    {
        // For memory-mapped files, async operations are typically not much different from sync
        // since the OS handles paging automatically. We'll use Task.Run to avoid blocking.
        return await Task.Run(() => Read(offset, buffer, bufferOffset, count), cancellationToken);
    }

    public async Task WriteAsync(long offset, byte[] buffer, int bufferOffset, int count, CancellationToken cancellationToken = default)
    {
        // For memory-mapped files, async operations are typically not much different from sync
        await Task.Run(() => Write(offset, buffer, bufferOffset, count), cancellationToken);
    }

    public void Expand(long newSize)
    {
        ThrowIfDisposed();
        if (_isReadOnly) throw new InvalidOperationException("Cannot expand read-only file");
        if (newSize <= Capacity) return;
        if (newSize > _configuration.MaxFileSize) throw new ArgumentOutOfRangeException(nameof(newSize), "New size exceeds maximum file size");

        _fileLock.EnterWriteLock();
        try
        {
            // Calculate new capacity with growth factor
            var targetCapacity = Math.Max(newSize, (long)(Capacity * _configuration.FileGrowthFactor));
            targetCapacity = Math.Min(targetCapacity, _configuration.MaxFileSize);

            // Dispose current memory-mapped file
            _mmf?.Dispose();
            
            // Expand the underlying file
            _fileStream?.SetLength(targetCapacity);
            
            // Create new memory-mapped file with expanded size
            _mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(
                _fileStream!, 
                null, 
                targetCapacity, 
                _isReadOnly ? MemoryMappedFileAccess.Read : MemoryMappedFileAccess.ReadWrite, 
                HandleInheritability.None, 
                false);

            Interlocked.Exchange(ref _capacity, targetCapacity);
            Interlocked.Exchange(ref _size, newSize);

            // Clear view cache since views are now invalid
            _viewCache.Clear();
        }
        finally
        {
            _fileLock.ExitWriteLock();
        }
    }

    public void Flush()
    {
        ThrowIfDisposed();
        if (_isReadOnly) return;

        _fileLock.EnterReadLock();
        try
        {
            _fileStream?.Flush();
            
            // Flush all active views
            foreach (var weakRef in _viewCache.Values)
            {
                if (weakRef.TryGetTarget(out var accessor))
                {
                    accessor.Flush();
                }
            }
        }
        finally
        {
            _fileLock.ExitReadLock();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_isReadOnly) return;

        await Task.Run(() => Flush(), cancellationToken);
    }

    private void Initialize(MemoryMappedFileAccess access)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open or create the file
        var fileMode = File.Exists(_filePath) ? FileMode.Open : FileMode.Create;
        var fileAccess = _isReadOnly ? FileAccess.Read : FileAccess.ReadWrite;
        var fileShare = _isReadOnly ? FileShare.Read : FileShare.Read;

        _fileStream = new FileStream(_filePath, fileMode, fileAccess, fileShare);
        
        // Set initial size if creating new file
        if (_fileStream.Length == 0 && !_isReadOnly)
        {
            _fileStream.SetLength(_configuration.InitialFileSize);
        }

        var fileSize = _fileStream.Length;
        var capacity = Math.Max(fileSize, _configuration.InitialFileSize);

        // Create memory-mapped file
        _mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(
            _fileStream, 
            null, 
            capacity, 
            access, 
            HandleInheritability.None, 
            false);

        Interlocked.Exchange(ref _size, fileSize);
        Interlocked.Exchange(ref _capacity, capacity);
    }

    private void WriteBehindCallback(object? state)
    {
        try
        {
            if (!_isDisposed && !_isReadOnly)
            {
                Flush();
            }
        }
        catch
        {
            // Ignore write-behind errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MemoryMappedFile));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _writeBehindTimer?.Dispose();

        _fileLock.EnterWriteLock();
        try
        {
            // Dispose all cached views
            foreach (var weakRef in _viewCache.Values)
            {
                if (weakRef.TryGetTarget(out var accessor))
                {
                    accessor.Dispose();
                }
            }
            _viewCache.Clear();

            _mmf?.Dispose();
            _fileStream?.Dispose();
        }
        finally
        {
            _fileLock.ExitWriteLock();
            _fileLock.Dispose();
        }
    }
}
