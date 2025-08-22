using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Execution;

/// <summary>
/// Result of query execution with streaming capabilities and metadata.
/// </summary>
public class QueryExecutionResult : IQueryExecutionResult
{
    private readonly IAsyncEnumerable<IDataRow> _resultStream;
    private long? _actualRowCount;
    private TimeSpan? _actualExecutionTime;

    public Guid ExecutionId { get; }
    public ISchema Schema { get; }
    public double EstimatedCost { get; }
    public long EstimatedRows { get; }
    public DateTime StartTime { get; }
    public DateTime? EndTime { get; private set; }
    public QueryExecutionStatus Status { get; private set; } = QueryExecutionStatus.Running;
    public string? ErrorMessage { get; private set; }

    public long? ActualRowCount => _actualRowCount;
    public TimeSpan? ActualExecutionTime => _actualExecutionTime;

    public QueryExecutionResult(
        Guid executionId,
        ISchema schema,
        IAsyncEnumerable<IDataRow> resultStream,
        double estimatedCost,
        long estimatedRows,
        DateTime startTime)
    {
        ExecutionId = executionId;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _resultStream = resultStream ?? throw new ArgumentNullException(nameof(resultStream));
        EstimatedCost = estimatedCost;
        EstimatedRows = estimatedRows;
        StartTime = startTime;
    }

    /// <summary>
    /// Gets the result rows as an async enumerable.
    /// </summary>
    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rowCount = 0L;
        var startTime = DateTime.UtcNow;

        try
        {
            await foreach (var row in _resultStream.WithCancellation(cancellationToken))
            {
                rowCount++;
                yield return row;
            }

            // Mark as completed successfully
            Status = QueryExecutionStatus.Completed;
            EndTime = DateTime.UtcNow;
            _actualRowCount = rowCount;
            _actualExecutionTime = EndTime.Value - StartTime;
        }
        catch (OperationCanceledException)
        {
            Status = QueryExecutionStatus.Cancelled;
            EndTime = DateTime.UtcNow;
            _actualRowCount = rowCount;
            _actualExecutionTime = EndTime.Value - StartTime;
            throw;
        }
        catch (Exception ex)
        {
            Status = QueryExecutionStatus.Failed;
            EndTime = DateTime.UtcNow;
            ErrorMessage = ex.Message;
            _actualRowCount = rowCount;
            _actualExecutionTime = EndTime.Value - StartTime;
            throw;
        }
    }

    /// <summary>
    /// Materializes all results into a list.
    /// </summary>
    public async Task<IReadOnlyList<IDataRow>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<IDataRow>();
        await foreach (var row in GetRowsAsync(cancellationToken))
        {
            results.Add(row);
        }
        return results;
    }

    /// <summary>
    /// Gets the first row or null if no rows.
    /// </summary>
    public async Task<IDataRow?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var row in GetRowsAsync(cancellationToken))
        {
            return row;
        }
        return null;
    }

    /// <summary>
    /// Counts the total number of rows.
    /// </summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var count = 0L;
        await foreach (var _ in GetRowsAsync(cancellationToken))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Gets execution statistics.
    /// </summary>
    public IQueryExecutionStatistics GetStatistics()
    {
        return new QueryExecutionStatistics
        {
            ExecutionId = ExecutionId,
            Status = Status,
            StartTime = StartTime,
            EndTime = EndTime,
            EstimatedCost = EstimatedCost,
            EstimatedRows = EstimatedRows,
            ActualRows = _actualRowCount,
            ExecutionTime = _actualExecutionTime,
            ErrorMessage = ErrorMessage
        };
    }
}

/// <summary>
/// Statistics for query execution.
/// </summary>
public class QueryExecutionStatistics : IQueryExecutionStatistics
{
    public Guid ExecutionId { get; set; }
    public QueryExecutionStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double EstimatedCost { get; set; }
    public long EstimatedRows { get; set; }
    public long? ActualRows { get; set; }
    public TimeSpan? ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    public double? EstimationAccuracy => 
        ActualRows.HasValue && EstimatedRows > 0 
            ? Math.Min(ActualRows.Value, EstimatedRows) / (double)Math.Max(ActualRows.Value, EstimatedRows)
            : null;
}

/// <summary>
/// Statistics collector for query execution.
/// </summary>
public class ExecutionStatisticsCollector : IExecutionStatisticsCollector
{
    private readonly Dictionary<Guid, QueryExecutionStats> _executionStats = new();
    private readonly Dictionary<Guid, QueryExecutionCompletionStats> _completionStats = new();
    private readonly object _lock = new();

    public async Task RecordQueryExecutionStartAsync(QueryExecutionStats stats)
    {
        lock (_lock)
        {
            _executionStats[stats.ExecutionId] = stats;
        }
    }

    public async Task RecordQueryExecutionCompletedAsync(QueryExecutionCompletionStats stats)
    {
        lock (_lock)
        {
            _completionStats[stats.ExecutionId] = stats;
        }
    }

    public async Task RecordQueryExecutionErrorAsync(Guid executionId, Exception error)
    {
        lock (_lock)
        {
            if (_executionStats.TryGetValue(executionId, out var stats))
            {
                stats.ErrorMessage = error.Message;
                stats.EndTime = DateTime.UtcNow;
            }
        }
    }

    public async Task RecordQueryExecutionCancelledAsync(Guid executionId)
    {
        lock (_lock)
        {
            if (_executionStats.TryGetValue(executionId, out var stats))
            {
                stats.EndTime = DateTime.UtcNow;
            }
        }
    }

    public async Task<IQueryExecutionStatistics> GetExecutionStatisticsAsync(Guid executionId)
    {
        lock (_lock)
        {
            var hasExecution = _executionStats.TryGetValue(executionId, out var executionStats);
            var hasCompletion = _completionStats.TryGetValue(executionId, out var completionStats);

            if (!hasExecution)
            {
                throw new ArgumentException($"No execution statistics found for execution ID {executionId}");
            }

            return new QueryExecutionStatistics
            {
                ExecutionId = executionId,
                Status = executionStats.EndTime.HasValue 
                    ? (string.IsNullOrEmpty(executionStats.ErrorMessage) ? QueryExecutionStatus.Completed : QueryExecutionStatus.Failed)
                    : QueryExecutionStatus.Running,
                StartTime = executionStats.StartTime,
                EndTime = executionStats.EndTime ?? completionStats?.CompletionTime,
                EstimatedCost = executionStats.EstimatedCost,
                EstimatedRows = executionStats.EstimatedRows,
                ActualRows = completionStats?.ActualRows,
                ExecutionTime = completionStats?.ExecutionTime,
                ErrorMessage = executionStats.ErrorMessage
            };
        }
    }

    public async Task<IReadOnlyList<IQueryExecutionStatistics>> GetAllExecutionStatisticsAsync()
    {
        lock (_lock)
        {
            var results = new List<IQueryExecutionStatistics>();

            foreach (var kvp in _executionStats)
            {
                var executionId = kvp.Key;
                var executionStats = kvp.Value;
                var hasCompletion = _completionStats.TryGetValue(executionId, out var completionStats);

                results.Add(new QueryExecutionStatistics
                {
                    ExecutionId = executionId,
                    Status = executionStats.EndTime.HasValue 
                        ? (string.IsNullOrEmpty(executionStats.ErrorMessage) ? QueryExecutionStatus.Completed : QueryExecutionStatus.Failed)
                        : QueryExecutionStatus.Running,
                    StartTime = executionStats.StartTime,
                    EndTime = executionStats.EndTime ?? completionStats?.CompletionTime,
                    EstimatedCost = executionStats.EstimatedCost,
                    EstimatedRows = executionStats.EstimatedRows,
                    ActualRows = completionStats?.ActualRows,
                    ExecutionTime = completionStats?.ExecutionTime,
                    ErrorMessage = executionStats.ErrorMessage
                });
            }

            return results;
        }
    }

    public async Task ClearStatisticsAsync(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.UtcNow - olderThan;

        lock (_lock)
        {
            var toRemove = _executionStats
                .Where(kvp => kvp.Value.StartTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var executionId in toRemove)
            {
                _executionStats.Remove(executionId);
                _completionStats.Remove(executionId);
            }
        }
    }
}

/// <summary>
/// Query execution status enumeration.
/// </summary>
public enum QueryExecutionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Statistics for query execution start.
/// </summary>
public class QueryExecutionStats
{
    public Guid ExecutionId { get; set; }
    public QueryType QueryType { get; set; }
    public double EstimatedCost { get; set; }
    public long EstimatedRows { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Statistics for query execution completion.
/// </summary>
public class QueryExecutionCompletionStats
{
    public Guid ExecutionId { get; set; }
    public long ActualRows { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsed { get; set; }
    public DateTime CompletionTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Buffered query result that materializes results for multiple enumerations.
/// </summary>
public class BufferedQueryResult : IQueryExecutionResult
{
    private readonly List<IDataRow> _materializedRows;
    private readonly QueryExecutionStatistics _statistics;

    public Guid ExecutionId => _statistics.ExecutionId;
    public ISchema Schema { get; }
    public double EstimatedCost => _statistics.EstimatedCost;
    public long EstimatedRows => _statistics.EstimatedRows;
    public DateTime StartTime => _statistics.StartTime;
    public DateTime? EndTime => _statistics.EndTime;
    public QueryExecutionStatus Status => _statistics.Status;
    public string? ErrorMessage => _statistics.ErrorMessage;
    public long? ActualRowCount => _materializedRows.Count;
    public TimeSpan? ActualExecutionTime => _statistics.ExecutionTime;

    public BufferedQueryResult(ISchema schema, IReadOnlyList<IDataRow> rows, QueryExecutionStatistics statistics)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _materializedRows = new List<IDataRow>(rows ?? throw new ArgumentNullException(nameof(rows)));
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in _materializedRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    public async Task<IReadOnlyList<IDataRow>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return _materializedRows.AsReadOnly();
    }

    public async Task<IDataRow?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return _materializedRows.FirstOrDefault();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return _materializedRows.Count;
    }

    public IQueryExecutionStatistics GetStatistics()
    {
        return _statistics;
    }
}

/// <summary>
/// Extension methods for query execution results.
/// </summary>
public static class QueryExecutionResultExtensions
{
    /// <summary>
    /// Buffers the result for multiple enumerations.
    /// </summary>
    public static async Task<BufferedQueryResult> BufferAsync(this IQueryExecutionResult result, CancellationToken cancellationToken = default)
    {
        var rows = await result.ToListAsync(cancellationToken);
        return new BufferedQueryResult(result.Schema, rows, (QueryExecutionStatistics)result.GetStatistics());
    }

    /// <summary>
    /// Takes only the first N rows.
    /// </summary>
    public static async IAsyncEnumerable<IDataRow> TakeAsync(
        this IQueryExecutionResult result, 
        int count, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var taken = 0;
        await foreach (var row in result.GetRowsAsync(cancellationToken))
        {
            if (taken >= count) break;
            yield return row;
            taken++;
        }
    }

    /// <summary>
    /// Skips the first N rows.
    /// </summary>
    public static async IAsyncEnumerable<IDataRow> SkipAsync(
        this IQueryExecutionResult result, 
        int count, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var skipped = 0;
        await foreach (var row in result.GetRowsAsync(cancellationToken))
        {
            if (skipped < count)
            {
                skipped++;
                continue;
            }
            yield return row;
        }
    }

    /// <summary>
    /// Filters rows based on a predicate.
    /// </summary>
    public static async IAsyncEnumerable<IDataRow> WhereAsync(
        this IQueryExecutionResult result, 
        Func<IDataRow, bool> predicate, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in result.GetRowsAsync(cancellationToken))
        {
            if (predicate(row))
            {
                yield return row;
            }
        }
    }

    /// <summary>
    /// Projects rows to a different type.
    /// </summary>
    public static async IAsyncEnumerable<T> SelectAsync<T>(
        this IQueryExecutionResult result, 
        Func<IDataRow, T> selector, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in result.GetRowsAsync(cancellationToken))
        {
            yield return selector(row);
        }
    }
}
