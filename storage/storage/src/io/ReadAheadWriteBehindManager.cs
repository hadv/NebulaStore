using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Manager for read-ahead and write-behind strategies to improve I/O performance.
/// </summary>
public class ReadAheadWriteBehindManager : IDisposable
{
    private readonly IAsyncStorageOperations _storageOperations;
    private readonly ReadAheadWriteBehindConfiguration _configuration;
    private readonly ConcurrentDictionary<string, ReadAheadBuffer> _readAheadBuffers;
    private readonly ConcurrentQueue<WriteBehindOperation> _writeBehindQueue;
    private readonly Timer _writeBehindTimer;
    private readonly SemaphoreSlim _writeBehindSemaphore;
    private readonly ReadAheadWriteBehindStatistics _statistics;
    private volatile bool _isDisposed;

    public ReadAheadWriteBehindManager(
        IAsyncStorageOperations storageOperations,
        ReadAheadWriteBehindConfiguration? configuration = null)
    {
        _storageOperations = storageOperations ?? throw new ArgumentNullException(nameof(storageOperations));
        _configuration = configuration ?? new ReadAheadWriteBehindConfiguration();
        _readAheadBuffers = new ConcurrentDictionary<string, ReadAheadBuffer>();
        _writeBehindQueue = new ConcurrentQueue<WriteBehindOperation>();
        _writeBehindSemaphore = new SemaphoreSlim(1, 1);
        _statistics = new ReadAheadWriteBehindStatistics();

        // Set up write-behind timer
        _writeBehindTimer = new Timer(ProcessWriteBehindQueue, null, 
            _configuration.WriteBehindInterval, _configuration.WriteBehindInterval);
    }

    /// <summary>
    /// Gets read-ahead and write-behind statistics.
    /// </summary>
    public IReadAheadWriteBehindStatistics Statistics => _statistics;

    /// <summary>
    /// Reads data with read-ahead optimization.
    /// </summary>
    /// <param name="path">File path</param>
    /// <param name="offset">Offset to read from</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read data</returns>
    public async Task<byte[]> ReadWithReadAheadAsync(string path, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        // Check read-ahead buffer first
        if (_configuration.EnableReadAhead && _readAheadBuffers.TryGetValue(path, out var buffer))
        {
            var bufferedData = buffer.TryGetData(offset, length);
            if (bufferedData != null)
            {
                _statistics.RecordReadAheadHit();
                return bufferedData;
            }
        }

        _statistics.RecordReadAheadMiss();

        // Read the requested data
        var data = await _storageOperations.ReadAsync(path, offset, length, cancellationToken);

        // Trigger read-ahead if enabled
        if (_configuration.EnableReadAhead && data.Length == length)
        {
            _ = Task.Run(async () => await PerformReadAheadAsync(path, offset + length, cancellationToken));
        }

        return data;
    }

    /// <summary>
    /// Writes data with write-behind optimization.
    /// </summary>
    /// <param name="path">File path</param>
    /// <param name="data">Data to write</param>
    /// <param name="offset">Offset to write at</param>
    /// <param name="priority">Write priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteWithWriteBehindAsync(string path, byte[] data, long offset = -1, WritePriority priority = WritePriority.Normal, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (_configuration.EnableWriteBehind && priority != WritePriority.Immediate)
        {
            // Queue for write-behind processing
            var operation = new WriteBehindOperation(path, data, offset, priority);
            _writeBehindQueue.Enqueue(operation);
            _statistics.RecordWriteBehindQueued();

            // Trigger immediate processing if queue is full or high priority
            if (_writeBehindQueue.Count >= _configuration.WriteBehindQueueSize || priority == WritePriority.High)
            {
                _ = Task.Run(async () => await ProcessWriteBehindQueueAsync(cancellationToken));
            }
        }
        else
        {
            // Write immediately
            if (offset >= 0)
            {
                await _storageOperations.WriteAsync(path, data, offset, cancellationToken);
            }
            else
            {
                await _storageOperations.WriteAsync(path, data, cancellationToken);
            }
            _statistics.RecordImmediateWrite();
        }
    }

    /// <summary>
    /// Flushes all pending write-behind operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of operations flushed</returns>
    public async Task<int> FlushWriteBehindAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ProcessWriteBehindQueueAsync(cancellationToken);
    }

    /// <summary>
    /// Preloads data into read-ahead buffers.
    /// </summary>
    /// <param name="path">File path</param>
    /// <param name="offset">Starting offset</param>
    /// <param name="length">Length to preload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task PreloadAsync(string path, long offset, long length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        await PerformReadAheadAsync(path, offset, length, cancellationToken);
    }

    /// <summary>
    /// Clears read-ahead buffers for a specific path.
    /// </summary>
    /// <param name="path">File path</param>
    public void ClearReadAheadBuffer(string path)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));

        if (_readAheadBuffers.TryRemove(path, out var buffer))
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Clears all read-ahead buffers.
    /// </summary>
    public void ClearAllReadAheadBuffers()
    {
        ThrowIfDisposed();
        
        foreach (var buffer in _readAheadBuffers.Values)
        {
            buffer.Dispose();
        }
        _readAheadBuffers.Clear();
    }

    private async Task PerformReadAheadAsync(string path, long startOffset, CancellationToken cancellationToken = default)
    {
        try
        {
            var readAheadSize = _configuration.ReadAheadSize;
            var data = await _storageOperations.ReadAsync(path, startOffset, (int)Math.Min(readAheadSize, int.MaxValue), cancellationToken);
            
            if (data.Length > 0)
            {
                var buffer = _readAheadBuffers.GetOrAdd(path, _ => new ReadAheadBuffer(_configuration.ReadAheadBufferSize));
                buffer.AddData(startOffset, data);
                _statistics.RecordReadAheadPerformed(data.Length);
            }
        }
        catch
        {
            // Ignore read-ahead errors
            _statistics.RecordReadAheadError();
        }
    }

    private async Task PerformReadAheadAsync(string path, long startOffset, long length, CancellationToken cancellationToken = default)
    {
        try
        {
            var chunkSize = Math.Min(length, _configuration.ReadAheadSize);
            var currentOffset = startOffset;
            var remainingLength = length;

            while (remainingLength > 0 && !cancellationToken.IsCancellationRequested)
            {
                var currentChunkSize = (int)Math.Min(chunkSize, remainingLength);
                var data = await _storageOperations.ReadAsync(path, currentOffset, currentChunkSize, cancellationToken);
                
                if (data.Length == 0) break;

                var buffer = _readAheadBuffers.GetOrAdd(path, _ => new ReadAheadBuffer(_configuration.ReadAheadBufferSize));
                buffer.AddData(currentOffset, data);
                
                currentOffset += data.Length;
                remainingLength -= data.Length;
                
                _statistics.RecordReadAheadPerformed(data.Length);

                if (data.Length < currentChunkSize) break; // End of file
            }
        }
        catch
        {
            // Ignore read-ahead errors
            _statistics.RecordReadAheadError();
        }
    }

    private async void ProcessWriteBehindQueue(object? state)
    {
        try
        {
            await ProcessWriteBehindQueueAsync();
        }
        catch
        {
            // Ignore timer-triggered processing errors
        }
    }

    private async Task<int> ProcessWriteBehindQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!await _writeBehindSemaphore.WaitAsync(100, cancellationToken))
            return 0; // Another process is already running

        try
        {
            var processedCount = 0;
            var operations = new List<WriteBehindOperation>();

            // Collect operations to process
            var maxOperations = Math.Min(_configuration.WriteBehindBatchSize, _writeBehindQueue.Count);
            for (int i = 0; i < maxOperations && _writeBehindQueue.TryDequeue(out var operation); i++)
            {
                operations.Add(operation);
            }

            if (operations.Count == 0) return 0;

            // Sort by priority (high priority first)
            operations.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Process operations
            var tasks = operations.Select(async op =>
            {
                try
                {
                    if (op.Offset >= 0)
                    {
                        await _storageOperations.WriteAsync(op.Path, op.Data, op.Offset, cancellationToken);
                    }
                    else
                    {
                        await _storageOperations.WriteAsync(op.Path, op.Data, cancellationToken);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            processedCount = results.Count(r => r);
            
            _statistics.RecordWriteBehindProcessed(processedCount, operations.Count - processedCount);
            
            return processedCount;
        }
        finally
        {
            _writeBehindSemaphore.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ReadAheadWriteBehindManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _writeBehindTimer.Dispose();
        _writeBehindSemaphore.Dispose();

        // Flush remaining write-behind operations
        try
        {
            ProcessWriteBehindQueueAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore disposal errors
        }

        // Dispose read-ahead buffers
        foreach (var buffer in _readAheadBuffers.Values)
        {
            buffer.Dispose();
        }
        _readAheadBuffers.Clear();
    }
}

/// <summary>
/// Configuration for read-ahead and write-behind operations.
/// </summary>
public class ReadAheadWriteBehindConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable read-ahead.
    /// </summary>
    public bool EnableReadAhead { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable write-behind.
    /// </summary>
    public bool EnableWriteBehind { get; set; } = true;

    /// <summary>
    /// Gets or sets the read-ahead size in bytes.
    /// </summary>
    public long ReadAheadSize { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Gets or sets the read-ahead buffer size in bytes.
    /// </summary>
    public long ReadAheadBufferSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Gets or sets the write-behind interval.
    /// </summary>
    public TimeSpan WriteBehindInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the write-behind queue size.
    /// </summary>
    public int WriteBehindQueueSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the write-behind batch size.
    /// </summary>
    public int WriteBehindBatchSize { get; set; } = 100;
}

/// <summary>
/// Enumeration of write priorities.
/// </summary>
public enum WritePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Immediate = 3
}

/// <summary>
/// Represents a write-behind operation.
/// </summary>
internal class WriteBehindOperation
{
    public WriteBehindOperation(string path, byte[] data, long offset, WritePriority priority)
    {
        Path = path;
        Data = data;
        Offset = offset;
        Priority = priority;
        Timestamp = DateTime.UtcNow;
    }

    public string Path { get; }
    public byte[] Data { get; }
    public long Offset { get; }
    public WritePriority Priority { get; }
    public DateTime Timestamp { get; }
}

/// <summary>
/// Read-ahead buffer for caching data.
/// </summary>
internal class ReadAheadBuffer : IDisposable
{
    private readonly long _maxSize;
    private readonly ConcurrentDictionary<long, BufferSegment> _segments;
    private long _currentSize;
    private volatile bool _isDisposed;

    public ReadAheadBuffer(long maxSize)
    {
        _maxSize = maxSize;
        _segments = new ConcurrentDictionary<long, BufferSegment>();
    }

    public void AddData(long offset, byte[] data)
    {
        if (_isDisposed || data == null || data.Length == 0) return;

        var segment = new BufferSegment(offset, data);
        _segments.TryAdd(offset, segment);

        Interlocked.Add(ref _currentSize, data.Length);

        // Evict old segments if buffer is too large
        if (_currentSize > _maxSize)
        {
            EvictOldSegments();
        }
    }

    public byte[]? TryGetData(long offset, int length)
    {
        if (_isDisposed) return null;

        // Find segments that overlap with the requested range
        var overlappingSegments = _segments.Values
            .Where(s => s.Offset < offset + length && s.Offset + s.Data.Length > offset)
            .OrderBy(s => s.Offset)
            .ToList();

        if (overlappingSegments.Count == 0) return null;

        // Check if we have complete coverage
        var firstSegment = overlappingSegments[0];
        var lastSegment = overlappingSegments[overlappingSegments.Count - 1];

        if (firstSegment.Offset > offset ||
            lastSegment.Offset + lastSegment.Data.Length < offset + length)
        {
            return null; // Incomplete coverage
        }

        // Combine segments to create the requested data
        var result = new byte[length];
        var resultOffset = 0;

        foreach (var segment in overlappingSegments)
        {
            var segmentStart = Math.Max(segment.Offset, offset);
            var segmentEnd = Math.Min(segment.Offset + segment.Data.Length, offset + length);
            var segmentLength = (int)(segmentEnd - segmentStart);

            if (segmentLength > 0)
            {
                var sourceOffset = (int)(segmentStart - segment.Offset);
                var destOffset = (int)(segmentStart - offset);

                Array.Copy(segment.Data, sourceOffset, result, destOffset, segmentLength);
            }
        }

        return result;
    }

    private void EvictOldSegments()
    {
        var segments = _segments.Values.OrderBy(s => s.Timestamp).ToList();
        var targetSize = _maxSize * 3 / 4; // Evict to 75% capacity

        foreach (var segment in segments)
        {
            if (_currentSize <= targetSize) break;

            if (_segments.TryRemove(segment.Offset, out _))
            {
                Interlocked.Add(ref _currentSize, -segment.Data.Length);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _segments.Clear();
    }
}

/// <summary>
/// Represents a buffer segment.
/// </summary>
internal class BufferSegment
{
    public BufferSegment(long offset, byte[] data)
    {
        Offset = offset;
        Data = data;
        Timestamp = DateTime.UtcNow;
    }

    public long Offset { get; }
    public byte[] Data { get; }
    public DateTime Timestamp { get; }
}
