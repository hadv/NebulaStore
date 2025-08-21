using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// High-performance batch I/O operations for improved throughput and reduced overhead.
/// </summary>
public class BatchIOOperations : IDisposable
{
    private readonly IAsyncStorageOperations _storageOperations;
    private readonly BatchIOConfiguration _configuration;
    private readonly ConcurrentQueue<BatchIORequest> _pendingRequests;
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _batchSemaphore;
    private readonly BatchIOStatistics _statistics;
    private volatile bool _isDisposed;

    public BatchIOOperations(IAsyncStorageOperations storageOperations, BatchIOConfiguration? configuration = null)
    {
        _storageOperations = storageOperations ?? throw new ArgumentNullException(nameof(storageOperations));
        _configuration = configuration ?? new BatchIOConfiguration();
        _pendingRequests = new ConcurrentQueue<BatchIORequest>();
        _batchSemaphore = new SemaphoreSlim(1, 1);
        _statistics = new BatchIOStatistics();

        // Set up batch processing timer
        _batchTimer = new Timer(ProcessBatches, null, _configuration.BatchInterval, _configuration.BatchInterval);
    }

    /// <summary>
    /// Gets batch I/O statistics.
    /// </summary>
    public IBatchIOStatistics Statistics => _statistics;

    /// <summary>
    /// Queues a read operation for batch processing.
    /// </summary>
    /// <param name="path">Path to read from</param>
    /// <param name="offset">Offset to start reading from</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="priority">Operation priority</param>
    /// <returns>Task that completes when the read operation finishes</returns>
    public Task<byte[]> QueueReadAsync(string path, long offset = 0, int length = -1, int priority = 0)
    {
        ThrowIfDisposed();

        var request = new BatchIORequest
        {
            Type = BatchIOOperationType.Read,
            Path = path,
            Offset = offset,
            Length = length,
            Priority = priority,
            CompletionSource = new TaskCompletionSource<byte[]>()
        };

        _pendingRequests.Enqueue(request);
        _statistics.RecordRequestQueued(BatchIOOperationType.Read);

        // Trigger immediate processing if batch is full
        if (_pendingRequests.Count >= _configuration.MaxBatchSize)
        {
            _ = Task.Run(ProcessBatchesImmediate);
        }

        return request.CompletionSource.Task;
    }

    /// <summary>
    /// Queues a write operation for batch processing.
    /// </summary>
    /// <param name="path">Path to write to</param>
    /// <param name="data">Data to write</param>
    /// <param name="offset">Offset to start writing at</param>
    /// <param name="priority">Operation priority</param>
    /// <returns>Task that completes when the write operation finishes</returns>
    public Task QueueWriteAsync(string path, byte[] data, long offset = -1, int priority = 0)
    {
        ThrowIfDisposed();

        var request = new BatchIORequest
        {
            Type = BatchIOOperationType.Write,
            Path = path,
            Data = data,
            Offset = offset,
            Priority = priority,
            CompletionSource = new TaskCompletionSource<byte[]>()
        };

        _pendingRequests.Enqueue(request);
        _statistics.RecordRequestQueued(BatchIOOperationType.Write);

        // Trigger immediate processing if batch is full
        if (_pendingRequests.Count >= _configuration.MaxBatchSize)
        {
            _ = Task.Run(ProcessBatchesImmediate);
        }

        return request.CompletionSource.Task;
    }

    /// <summary>
    /// Executes a batch of read operations.
    /// </summary>
    /// <param name="readRequests">Read requests to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch execution result</returns>
    public async Task<BatchExecutionResult> ExecuteReadBatchAsync(IEnumerable<AsyncReadRequest> readRequests, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var requests = readRequests.ToList();
        if (requests.Count == 0)
            return new BatchExecutionResult(0, 0, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        try
        {
            // Group requests by path for better I/O efficiency
            var groupedRequests = requests.GroupBy(r => r.Path).ToList();
            
            var tasks = groupedRequests.Select(async group =>
            {
                var pathRequests = group.OrderBy(r => r.Offset).ToList();
                var results = await _storageOperations.ReadBatchAsync(pathRequests, cancellationToken);
                
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                        Interlocked.Increment(ref successCount);
                }
                
                return results;
            });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var result = new BatchExecutionResult(successCount, requests.Count, stopwatch.Elapsed);
            _statistics.RecordBatchExecution(BatchIOOperationType.Read, result);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new BatchExecutionResult(successCount, requests.Count, stopwatch.Elapsed, ex);
            _statistics.RecordBatchExecution(BatchIOOperationType.Read, result);
            throw;
        }
    }

    /// <summary>
    /// Executes a batch of write operations.
    /// </summary>
    /// <param name="writeRequests">Write requests to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch execution result</returns>
    public async Task<BatchExecutionResult> ExecuteWriteBatchAsync(IEnumerable<AsyncWriteRequest> writeRequests, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var requests = writeRequests.ToList();
        if (requests.Count == 0)
            return new BatchExecutionResult(0, 0, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        try
        {
            // Group requests by path for better I/O efficiency
            var groupedRequests = requests.GroupBy(r => r.Path).ToList();
            
            var tasks = groupedRequests.Select(async group =>
            {
                var pathRequests = group.OrderBy(r => r.Offset).ToList();
                var results = await _storageOperations.WriteBatchAsync(pathRequests, cancellationToken);
                
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                        Interlocked.Increment(ref successCount);
                }
                
                return results;
            });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var result = new BatchExecutionResult(successCount, requests.Count, stopwatch.Elapsed);
            _statistics.RecordBatchExecution(BatchIOOperationType.Write, result);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new BatchExecutionResult(successCount, requests.Count, stopwatch.Elapsed, ex);
            _statistics.RecordBatchExecution(BatchIOOperationType.Write, result);
            throw;
        }
    }

    /// <summary>
    /// Forces immediate processing of all pending batches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of operations processed</returns>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ProcessBatchesImmediate(cancellationToken);
    }

    private async void ProcessBatches(object? state)
    {
        try
        {
            await ProcessBatchesImmediate();
        }
        catch
        {
            // Ignore timer-triggered processing errors
        }
    }

    private async Task<int> ProcessBatchesImmediate(CancellationToken cancellationToken = default)
    {
        if (!await _batchSemaphore.WaitAsync(100, cancellationToken))
            return 0; // Another batch is already processing

        try
        {
            var processedCount = 0;
            var readRequests = new List<BatchIORequest>();
            var writeRequests = new List<BatchIORequest>();

            // Collect pending requests
            var batchSize = Math.Min(_configuration.MaxBatchSize, _pendingRequests.Count);
            for (int i = 0; i < batchSize && _pendingRequests.TryDequeue(out var request); i++)
            {
                if (request.Type == BatchIOOperationType.Read)
                    readRequests.Add(request);
                else
                    writeRequests.Add(request);
            }

            // Process read batch
            if (readRequests.Count > 0)
            {
                await ProcessReadBatch(readRequests, cancellationToken);
                processedCount += readRequests.Count;
            }

            // Process write batch
            if (writeRequests.Count > 0)
            {
                await ProcessWriteBatch(writeRequests, cancellationToken);
                processedCount += writeRequests.Count;
            }

            return processedCount;
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    private async Task ProcessReadBatch(List<BatchIORequest> requests, CancellationToken cancellationToken)
    {
        // Sort by priority (higher priority first)
        requests.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var asyncRequests = requests.Select(r => new AsyncReadRequest(r.Path, r.Offset, r.Length, r.Priority)).ToList();
        
        try
        {
            var results = await _storageOperations.ReadBatchAsync(asyncRequests, cancellationToken);
            var resultDict = results.ToDictionary(r => r.RequestId);

            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var asyncRequest = asyncRequests[i];
                
                if (resultDict.TryGetValue(asyncRequest.RequestId, out var result))
                {
                    if (result.IsSuccess && result.Data != null)
                    {
                        request.CompletionSource.SetResult(result.Data);
                    }
                    else
                    {
                        request.CompletionSource.SetException(result.Error ?? new InvalidOperationException("Read operation failed"));
                    }
                }
                else
                {
                    request.CompletionSource.SetException(new InvalidOperationException("Read result not found"));
                }
            }
        }
        catch (Exception ex)
        {
            // Set exception for all pending requests
            foreach (var request in requests)
            {
                request.CompletionSource.SetException(ex);
            }
        }
    }

    private async Task ProcessWriteBatch(List<BatchIORequest> requests, CancellationToken cancellationToken)
    {
        // Sort by priority (higher priority first)
        requests.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var asyncRequests = requests.Select(r => new AsyncWriteRequest(r.Path, r.Data!, r.Offset, r.Priority)).ToList();
        
        try
        {
            var results = await _storageOperations.WriteBatchAsync(asyncRequests, cancellationToken);
            var resultDict = results.ToDictionary(r => r.RequestId);

            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var asyncRequest = asyncRequests[i];
                
                if (resultDict.TryGetValue(asyncRequest.RequestId, out var result))
                {
                    if (result.IsSuccess)
                    {
                        request.CompletionSource.SetResult(Array.Empty<byte>());
                    }
                    else
                    {
                        request.CompletionSource.SetException(result.Error ?? new InvalidOperationException("Write operation failed"));
                    }
                }
                else
                {
                    request.CompletionSource.SetException(new InvalidOperationException("Write result not found"));
                }
            }
        }
        catch (Exception ex)
        {
            // Set exception for all pending requests
            foreach (var request in requests)
            {
                request.CompletionSource.SetException(ex);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(BatchIOOperations));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _batchTimer.Dispose();
        _batchSemaphore.Dispose();

        // Complete any pending requests with cancellation
        while (_pendingRequests.TryDequeue(out var request))
        {
            request.CompletionSource.SetCanceled();
        }
    }
}

/// <summary>
/// Configuration for batch I/O operations.
/// </summary>
public class BatchIOConfiguration
{
    /// <summary>
    /// Gets or sets the maximum batch size.
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the batch processing interval.
    /// </summary>
    public TimeSpan BatchInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Gets or sets the maximum wait time for a batch to fill.
    /// </summary>
    public TimeSpan MaxBatchWaitTime { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets whether to enable adaptive batching based on load.
    /// </summary>
    public bool EnableAdaptiveBatching { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum batch size for adaptive batching.
    /// </summary>
    public int MinBatchSize { get; set; } = 10;
}

/// <summary>
/// Represents a batch I/O request.
/// </summary>
internal class BatchIORequest
{
    public BatchIOOperationType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public byte[]? Data { get; set; }
    public long Offset { get; set; }
    public int Length { get; set; }
    public int Priority { get; set; }
    public TaskCompletionSource<byte[]> CompletionSource { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Enumeration of batch I/O operation types.
/// </summary>
public enum BatchIOOperationType
{
    Read,
    Write
}

/// <summary>
/// Represents the result of a batch execution.
/// </summary>
public class BatchExecutionResult
{
    public BatchExecutionResult(int successCount, int totalCount, TimeSpan duration, Exception? error = null)
    {
        SuccessCount = successCount;
        TotalCount = totalCount;
        Duration = duration;
        Error = error;
        IsSuccess = error == null && successCount == totalCount;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public int SuccessCount { get; }

    /// <summary>
    /// Gets the total number of operations.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the batch execution duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the error that occurred (null if successful).
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets whether the batch execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the result timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the failure count.
    /// </summary>
    public int FailureCount => TotalCount - SuccessCount;

    /// <summary>
    /// Gets the success rate.
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0.0;

    /// <summary>
    /// Gets the operations per second.
    /// </summary>
    public double OperationsPerSecond => Duration.TotalSeconds > 0 ? TotalCount / Duration.TotalSeconds : 0.0;

    public override string ToString()
    {
        return $"BatchResult[Success={SuccessCount}/{TotalCount} ({SuccessRate:P1}), " +
               $"Duration={Duration.TotalMilliseconds:F0}ms, OPS={OperationsPerSecond:F0}]";
    }
}
