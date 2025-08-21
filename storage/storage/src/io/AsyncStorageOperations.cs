using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// High-performance asynchronous storage operations implementation with proper cancellation support and error handling.
/// </summary>
public class AsyncStorageOperations : IAsyncStorageOperations
{
    private readonly AsyncIOConfiguration _configuration;
    private readonly AsyncIOStatistics _statistics;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ConcurrentQueue<PendingOperation> _pendingOperations;
    private readonly Timer? _flushTimer;
    private volatile bool _isDisposed;

    public AsyncStorageOperations(AsyncIOConfiguration? configuration = null)
    {
        _configuration = configuration ?? new AsyncIOConfiguration();
        _statistics = new AsyncIOStatistics();
        _concurrencyLimiter = new SemaphoreSlim(_configuration.MaxConcurrentOperations);
        _pendingOperations = new ConcurrentQueue<PendingOperation>();

        // Set up automatic flush timer if configured
        if (_configuration.AutoFlushInterval > TimeSpan.Zero)
        {
            _flushTimer = new Timer(AutoFlush, null, _configuration.AutoFlushInterval, _configuration.AutoFlushInterval);
        }
    }

    public IAsyncIOStatistics Statistics => _statistics;

    public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Read);

            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            
            stopwatch.Stop();
            _statistics.RecordReadOperation(data.Length, stopwatch.Elapsed);
            
            return data;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to read from path: {path}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task<byte[]> ReadAsync(string path, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Read);

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, _configuration.BufferSize, FileOptions.SequentialScan);
            fileStream.Seek(offset, SeekOrigin.Begin);
            
            var buffer = new byte[length];
            var totalBytesRead = 0;
            
            while (totalBytesRead < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var bytesRead = await fileStream.ReadAsync(buffer, totalBytesRead, length - totalBytesRead, cancellationToken);
                if (bytesRead == 0)
                    break; // End of file
                
                totalBytesRead += bytesRead;
            }

            // Resize buffer if we read less than requested
            if (totalBytesRead < length)
            {
                Array.Resize(ref buffer, totalBytesRead);
            }

            stopwatch.Stop();
            _statistics.RecordReadOperation(totalBytesRead, stopwatch.Elapsed);
            
            return buffer;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to read from path: {path} at offset {offset}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (data == null) throw new ArgumentNullException(nameof(data));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Write);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(path, data, cancellationToken);
            
            stopwatch.Stop();
            _statistics.RecordWriteOperation(data.Length, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to write to path: {path}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task WriteAsync(string path, byte[] data, long offset, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Write);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, _configuration.BufferSize);
            fileStream.Seek(offset, SeekOrigin.Begin);
            await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
            
            if (_configuration.AutoFlush)
            {
                await fileStream.FlushAsync(cancellationToken);
            }

            stopwatch.Stop();
            _statistics.RecordWriteOperation(data.Length, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to write to path: {path} at offset {offset}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task AppendAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));
        if (data == null) throw new ArgumentNullException(nameof(data));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Write);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, _configuration.BufferSize);
            await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
            
            if (_configuration.AutoFlush)
            {
                await fileStream.FlushAsync(cancellationToken);
            }

            stopwatch.Stop();
            _statistics.RecordWriteOperation(data.Length, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to append to path: {path}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _statistics.RecordOperationStart(AsyncIOOperationType.Delete);

            await Task.Run(() =>
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken);

            stopwatch.Stop();
            _statistics.RecordDeleteOperation(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw new AsyncIOException($"Failed to delete path: {path}", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));

        return await Task.Run(() => File.Exists(path), cancellationToken);
    }

    public async Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty", nameof(path));

        return await Task.Run(() =>
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }, cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Process any pending operations
        var pendingOps = new List<PendingOperation>();
        while (_pendingOperations.TryDequeue(out var op))
        {
            pendingOps.Add(op);
        }

        if (pendingOps.Count > 0)
        {
            var tasks = pendingOps.Select(op => ProcessPendingOperation(op, cancellationToken));
            await Task.WhenAll(tasks);
        }
    }

    public async Task<IEnumerable<AsyncReadResult>> ReadBatchAsync(IEnumerable<AsyncReadRequest> requests, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (requests == null) throw new ArgumentNullException(nameof(requests));

        var requestList = requests.ToList();
        if (requestList.Count == 0)
            return Enumerable.Empty<AsyncReadResult>();

        // Sort by priority (higher priority first)
        requestList.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var tasks = requestList.Select(async request =>
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var data = request.Length > 0 
                    ? await ReadAsync(request.Path, request.Offset, request.Length, cancellationToken)
                    : await ReadAsync(request.Path, cancellationToken);
                stopwatch.Stop();
                
                return new AsyncReadResult(request.RequestId, data, duration: stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                return new AsyncReadResult(request.RequestId, null, ex);
            }
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<IEnumerable<AsyncWriteResult>> WriteBatchAsync(IEnumerable<AsyncWriteRequest> requests, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (requests == null) throw new ArgumentNullException(nameof(requests));

        var requestList = requests.ToList();
        if (requestList.Count == 0)
            return Enumerable.Empty<AsyncWriteResult>();

        // Sort by priority (higher priority first)
        requestList.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var tasks = requestList.Select(async request =>
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                if (request.Append)
                {
                    await AppendAsync(request.Path, request.Data, cancellationToken);
                }
                else if (request.Offset >= 0)
                {
                    await WriteAsync(request.Path, request.Data, request.Offset, cancellationToken);
                }
                else
                {
                    await WriteAsync(request.Path, request.Data, cancellationToken);
                }
                
                stopwatch.Stop();
                return new AsyncWriteResult(request.RequestId, request.Data.Length, duration: stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                return new AsyncWriteResult(request.RequestId, 0, ex);
            }
        });

        return await Task.WhenAll(tasks);
    }

    private async Task ProcessPendingOperation(PendingOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            switch (operation.Type)
            {
                case AsyncIOOperationType.Read:
                    // Process pending read operation
                    break;
                case AsyncIOOperationType.Write:
                    // Process pending write operation
                    break;
                case AsyncIOOperationType.Delete:
                    // Process pending delete operation
                    break;
            }
        }
        catch
        {
            // Ignore errors in pending operations
        }
    }

    private void AutoFlush(object? state)
    {
        try
        {
            _ = Task.Run(async () => await FlushAsync());
        }
        catch
        {
            // Ignore auto-flush errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(AsyncStorageOperations));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _flushTimer?.Dispose();
        _concurrencyLimiter.Dispose();
    }
}

/// <summary>
/// Configuration for asynchronous I/O operations.
/// </summary>
public class AsyncIOConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent operations.
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount * 4;

    /// <summary>
    /// Gets or sets the buffer size for file operations.
    /// </summary>
    public int BufferSize { get; set; } = 64 * 1024; // 64KB

    /// <summary>
    /// Gets or sets whether to automatically flush writes.
    /// </summary>
    public bool AutoFlush { get; set; } = false;

    /// <summary>
    /// Gets or sets the automatic flush interval.
    /// </summary>
    public TimeSpan AutoFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the operation timeout.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Represents a pending I/O operation.
/// </summary>
internal class PendingOperation
{
    public AsyncIOOperationType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte[]? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Enumeration of asynchronous I/O operation types.
/// </summary>
public enum AsyncIOOperationType
{
    Read,
    Write,
    Delete,
    Flush
}

/// <summary>
/// Exception thrown by asynchronous I/O operations.
/// </summary>
public class AsyncIOException : Exception
{
    public AsyncIOException(string message) : base(message) { }
    public AsyncIOException(string message, Exception innerException) : base(message, innerException) { }
}
