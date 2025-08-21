using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Interface for batch I/O statistics.
/// </summary>
public interface IBatchIOStatistics
{
    /// <summary>
    /// Gets the total number of read batches executed.
    /// </summary>
    long TotalReadBatches { get; }

    /// <summary>
    /// Gets the total number of write batches executed.
    /// </summary>
    long TotalWriteBatches { get; }

    /// <summary>
    /// Gets the total number of read requests queued.
    /// </summary>
    long TotalReadRequestsQueued { get; }

    /// <summary>
    /// Gets the total number of write requests queued.
    /// </summary>
    long TotalWriteRequestsQueued { get; }

    /// <summary>
    /// Gets the average batch size for read operations.
    /// </summary>
    double AverageReadBatchSize { get; }

    /// <summary>
    /// Gets the average batch size for write operations.
    /// </summary>
    double AverageWriteBatchSize { get; }

    /// <summary>
    /// Gets the average batch execution time in milliseconds.
    /// </summary>
    double AverageBatchExecutionTimeMs { get; }

    /// <summary>
    /// Gets the batch success rate.
    /// </summary>
    double BatchSuccessRate { get; }

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    int CurrentQueueDepth { get; }

    /// <summary>
    /// Gets the batching efficiency (operations per batch).
    /// </summary>
    double BatchingEfficiency { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Thread-safe implementation of batch I/O statistics.
/// </summary>
public class BatchIOStatistics : IBatchIOStatistics
{
    private long _totalReadBatches;
    private long _totalWriteBatches;
    private long _totalReadRequestsQueued;
    private long _totalWriteRequestsQueued;
    private long _totalReadOperationsInBatches;
    private long _totalWriteOperationsInBatches;
    private long _totalBatchExecutionTimeMs;
    private long _totalSuccessfulBatches;
    private long _currentQueueDepth;

    public long TotalReadBatches => Interlocked.Read(ref _totalReadBatches);

    public long TotalWriteBatches => Interlocked.Read(ref _totalWriteBatches);

    public long TotalReadRequestsQueued => Interlocked.Read(ref _totalReadRequestsQueued);

    public long TotalWriteRequestsQueued => Interlocked.Read(ref _totalWriteRequestsQueued);

    public double AverageReadBatchSize
    {
        get
        {
            var batches = TotalReadBatches;
            return batches > 0 ? (double)Interlocked.Read(ref _totalReadOperationsInBatches) / batches : 0.0;
        }
    }

    public double AverageWriteBatchSize
    {
        get
        {
            var batches = TotalWriteBatches;
            return batches > 0 ? (double)Interlocked.Read(ref _totalWriteOperationsInBatches) / batches : 0.0;
        }
    }

    public double AverageBatchExecutionTimeMs
    {
        get
        {
            var totalBatches = TotalReadBatches + TotalWriteBatches;
            return totalBatches > 0 ? (double)Interlocked.Read(ref _totalBatchExecutionTimeMs) / totalBatches : 0.0;
        }
    }

    public double BatchSuccessRate
    {
        get
        {
            var totalBatches = TotalReadBatches + TotalWriteBatches;
            return totalBatches > 0 ? (double)Interlocked.Read(ref _totalSuccessfulBatches) / totalBatches : 0.0;
        }
    }

    public int CurrentQueueDepth => (int)Interlocked.Read(ref _currentQueueDepth);

    public double BatchingEfficiency
    {
        get
        {
            var totalRequests = TotalReadRequestsQueued + TotalWriteRequestsQueued;
            var totalBatches = TotalReadBatches + TotalWriteBatches;
            return totalBatches > 0 ? (double)totalRequests / totalBatches : 0.0;
        }
    }

    /// <summary>
    /// Records a request being queued.
    /// </summary>
    /// <param name="operationType">Type of operation</param>
    public void RecordRequestQueued(BatchIOOperationType operationType)
    {
        switch (operationType)
        {
            case BatchIOOperationType.Read:
                Interlocked.Increment(ref _totalReadRequestsQueued);
                break;
            case BatchIOOperationType.Write:
                Interlocked.Increment(ref _totalWriteRequestsQueued);
                break;
        }
        
        Interlocked.Increment(ref _currentQueueDepth);
    }

    /// <summary>
    /// Records a batch execution.
    /// </summary>
    /// <param name="operationType">Type of operation</param>
    /// <param name="result">Batch execution result</param>
    public void RecordBatchExecution(BatchIOOperationType operationType, BatchExecutionResult result)
    {
        switch (operationType)
        {
            case BatchIOOperationType.Read:
                Interlocked.Increment(ref _totalReadBatches);
                Interlocked.Add(ref _totalReadOperationsInBatches, result.TotalCount);
                break;
            case BatchIOOperationType.Write:
                Interlocked.Increment(ref _totalWriteBatches);
                Interlocked.Add(ref _totalWriteOperationsInBatches, result.TotalCount);
                break;
        }

        Interlocked.Add(ref _totalBatchExecutionTimeMs, (long)result.Duration.TotalMilliseconds);
        Interlocked.Add(ref _currentQueueDepth, -result.TotalCount);

        if (result.IsSuccess)
        {
            Interlocked.Increment(ref _totalSuccessfulBatches);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalReadBatches, 0);
        Interlocked.Exchange(ref _totalWriteBatches, 0);
        Interlocked.Exchange(ref _totalReadRequestsQueued, 0);
        Interlocked.Exchange(ref _totalWriteRequestsQueued, 0);
        Interlocked.Exchange(ref _totalReadOperationsInBatches, 0);
        Interlocked.Exchange(ref _totalWriteOperationsInBatches, 0);
        Interlocked.Exchange(ref _totalBatchExecutionTimeMs, 0);
        Interlocked.Exchange(ref _totalSuccessfulBatches, 0);
        // Note: We don't reset current queue depth as it represents current state
    }

    /// <summary>
    /// Gets a snapshot of all statistics.
    /// </summary>
    /// <returns>Statistics snapshot</returns>
    public BatchIOStatisticsSnapshot GetSnapshot()
    {
        return new BatchIOStatisticsSnapshot(
            TotalReadBatches,
            TotalWriteBatches,
            TotalReadRequestsQueued,
            TotalWriteRequestsQueued,
            AverageReadBatchSize,
            AverageWriteBatchSize,
            AverageBatchExecutionTimeMs,
            BatchSuccessRate,
            CurrentQueueDepth,
            BatchingEfficiency,
            DateTime.UtcNow
        );
    }
}

/// <summary>
/// Immutable snapshot of batch I/O statistics at a point in time.
/// </summary>
public class BatchIOStatisticsSnapshot
{
    public BatchIOStatisticsSnapshot(
        long totalReadBatches,
        long totalWriteBatches,
        long totalReadRequestsQueued,
        long totalWriteRequestsQueued,
        double averageReadBatchSize,
        double averageWriteBatchSize,
        double averageBatchExecutionTimeMs,
        double batchSuccessRate,
        int currentQueueDepth,
        double batchingEfficiency,
        DateTime timestamp)
    {
        TotalReadBatches = totalReadBatches;
        TotalWriteBatches = totalWriteBatches;
        TotalReadRequestsQueued = totalReadRequestsQueued;
        TotalWriteRequestsQueued = totalWriteRequestsQueued;
        AverageReadBatchSize = averageReadBatchSize;
        AverageWriteBatchSize = averageWriteBatchSize;
        AverageBatchExecutionTimeMs = averageBatchExecutionTimeMs;
        BatchSuccessRate = batchSuccessRate;
        CurrentQueueDepth = currentQueueDepth;
        BatchingEfficiency = batchingEfficiency;
        Timestamp = timestamp;
    }

    public long TotalReadBatches { get; }
    public long TotalWriteBatches { get; }
    public long TotalReadRequestsQueued { get; }
    public long TotalWriteRequestsQueued { get; }
    public double AverageReadBatchSize { get; }
    public double AverageWriteBatchSize { get; }
    public double AverageBatchExecutionTimeMs { get; }
    public double BatchSuccessRate { get; }
    public int CurrentQueueDepth { get; }
    public double BatchingEfficiency { get; }
    public DateTime Timestamp { get; }

    public long TotalBatches => TotalReadBatches + TotalWriteBatches;
    public long TotalRequestsQueued => TotalReadRequestsQueued + TotalWriteRequestsQueued;
    public double AverageOverallBatchSize => TotalBatches > 0 ? (double)TotalRequestsQueued / TotalBatches : 0.0;

    public override string ToString()
    {
        return $"BatchIO Statistics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
               $"Batches={TotalBatches:N0} (R:{TotalReadBatches:N0}, W:{TotalWriteBatches:N0}), " +
               $"Requests={TotalRequestsQueued:N0} (R:{TotalReadRequestsQueued:N0}, W:{TotalWriteRequestsQueued:N0}), " +
               $"AvgBatchSize={AverageOverallBatchSize:F1}, " +
               $"AvgExecTime={AverageBatchExecutionTimeMs:F1}ms, " +
               $"SuccessRate={BatchSuccessRate:P1}, " +
               $"QueueDepth={CurrentQueueDepth}, " +
               $"Efficiency={BatchingEfficiency:F1}";
    }
}

/// <summary>
/// Performance metrics for batch I/O operations.
/// </summary>
public class BatchIOPerformanceMetrics
{
    public BatchIOPerformanceMetrics(
        double batchThroughput,
        double averageLatency,
        double queueUtilization,
        double batchingRatio,
        double compressionRatio,
        DateTime timestamp)
    {
        BatchThroughput = batchThroughput;
        AverageLatency = averageLatency;
        QueueUtilization = queueUtilization;
        BatchingRatio = batchingRatio;
        CompressionRatio = compressionRatio;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the batch throughput (batches per second).
    /// </summary>
    public double BatchThroughput { get; }

    /// <summary>
    /// Gets the average latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; }

    /// <summary>
    /// Gets the queue utilization percentage.
    /// </summary>
    public double QueueUtilization { get; }

    /// <summary>
    /// Gets the batching ratio (operations per batch).
    /// </summary>
    public double BatchingRatio { get; }

    /// <summary>
    /// Gets the compression ratio for batched operations.
    /// </summary>
    public double CompressionRatio { get; }

    /// <summary>
    /// Gets the timestamp of these metrics.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"BatchIO Performance [{Timestamp:HH:mm:ss}]: " +
               $"Throughput={BatchThroughput:F1} batches/sec, " +
               $"Latency={AverageLatency:F1}ms, " +
               $"QueueUtil={QueueUtilization:P1}, " +
               $"BatchRatio={BatchingRatio:F1}, " +
               $"Compression={CompressionRatio:F2}x";
    }
}
