using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Query.Advanced;

namespace NebulaStore.Storage.Embedded.Query.Execution;

#region Core Execution Interfaces

/// <summary>
/// Interface for the main query execution engine.
/// </summary>
public interface IQueryExecutionEngine
{
    Task<IQueryExecutionResult> ExecuteQueryAsync(IQueryAst query, CancellationToken cancellationToken = default);
    Task<IQueryExecutionResult> ExecutePlanAsync(IPhysicalPlan plan, CancellationToken cancellationToken = default);
    Task CancelQueryAsync(Guid executionId);
    Task<IQueryExecutionStatistics> GetExecutionStatisticsAsync(Guid executionId);
}

/// <summary>
/// Interface for query execution context.
/// </summary>
public interface IQueryExecutionContext : IAsyncDisposable
{
    Guid ExecutionId { get; }
    IStorageEngine StorageEngine { get; }
    IExecutionMemoryManager MemoryManager { get; }
    IMemoryBudget MemoryBudget { get; }
    QueryExecutionOptions Options { get; }
    CancellationToken CancellationToken { get; }
    DateTime StartTime { get; }

    T? GetProperty<T>(string key);
    void SetProperty<T>(string key, T value);
    Task<ITemporaryStorage> CreateTemporaryStorageAsync(string name, TemporaryStorageOptions? options = null);
    ITemporaryStorage? GetTemporaryStorage(string name);
}

/// <summary>
/// Interface for physical execution operators.
/// </summary>
public interface IPhysicalExecutionOperator : IAsyncDisposable
{
    PhysicalOperatorType OperatorType { get; }
    IReadOnlyList<IPhysicalExecutionOperator> Children { get; }
    long ProcessedRows { get; }
    TimeSpan ExecutionTime { get; }
    long MemoryUsed { get; }

    IAsyncEnumerable<IDataRow> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for query execution results.
/// </summary>
public interface IQueryExecutionResult
{
    Guid ExecutionId { get; }
    ISchema Schema { get; }
    double EstimatedCost { get; }
    long EstimatedRows { get; }
    DateTime StartTime { get; }
    DateTime? EndTime { get; }
    QueryExecutionStatus Status { get; }
    string? ErrorMessage { get; }
    long? ActualRowCount { get; }
    TimeSpan? ActualExecutionTime { get; }

    IAsyncEnumerable<IDataRow> GetRowsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDataRow>> ToListAsync(CancellationToken cancellationToken = default);
    Task<IDataRow?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
    IQueryExecutionStatistics GetStatistics();
}

/// <summary>
/// Interface for query execution statistics.
/// </summary>
public interface IQueryExecutionStatistics
{
    Guid ExecutionId { get; }
    QueryExecutionStatus Status { get; }
    DateTime StartTime { get; }
    DateTime? EndTime { get; }
    double EstimatedCost { get; }
    long EstimatedRows { get; }
    long? ActualRows { get; }
    TimeSpan? ExecutionTime { get; }
    string? ErrorMessage { get; }
    double? EstimationAccuracy { get; }
}

#endregion

#region Memory Management Interfaces

/// <summary>
/// Interface for execution memory management.
/// </summary>
public interface IExecutionMemoryManager
{
    Task<IMemoryBudget> AllocateMemoryBudgetAsync(double estimatedCost, CancellationToken cancellationToken = default);
    Task ReleaseMemoryBudgetAsync(IMemoryBudget budget);
    Task CheckMemoryPressureAsync(IMemoryBudget budget, CancellationToken cancellationToken = default);
    Task<ITemporaryStorage> CreateTemporaryStorageAsync(string name, TemporaryStorageOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for memory budget tracking.
/// </summary>
public interface IMemoryBudget
{
    long TotalBudget { get; }
    long UsedMemory { get; }
    long AvailableMemory { get; }
    double UsagePercentage { get; }

    bool TryAllocate(long bytes);
    void Release(long bytes);
    bool CanAllocate(long bytes);
}

/// <summary>
/// Interface for temporary storage during query execution.
/// </summary>
public interface ITemporaryStorage : IAsyncDisposable
{
    string Name { get; }
    TemporaryStorageOptions Options { get; }
    long RowCount { get; }
    long EstimatedSize { get; }

    Task AddRowAsync(IDataRow row, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDataRow> GetRowsAsync(CancellationToken cancellationToken = default);
    Task ClearAsync();
}

#endregion

#region Statistics Collection Interfaces

/// <summary>
/// Interface for collecting execution statistics.
/// </summary>
public interface IExecutionStatisticsCollector
{
    Task RecordQueryExecutionStartAsync(QueryExecutionStats stats);
    Task RecordQueryExecutionCompletedAsync(QueryExecutionCompletionStats stats);
    Task RecordQueryExecutionErrorAsync(Guid executionId, Exception error);
    Task RecordQueryExecutionCancelledAsync(Guid executionId);
    Task<IQueryExecutionStatistics> GetExecutionStatisticsAsync(Guid executionId);
    Task<IReadOnlyList<IQueryExecutionStatistics>> GetAllExecutionStatisticsAsync();
    Task ClearStatisticsAsync(TimeSpan olderThan);
}

#endregion

#region Storage Engine Interfaces

/// <summary>
/// Interface for storage engine integration.
/// </summary>
public interface IStorageEngine
{
    Task<IQueryResult> GetTableDataAsync(string tableName, CancellationToken cancellationToken = default);
    Task<IQueryResult> GetIndexDataAsync(string indexName, string tableName, CancellationToken cancellationToken = default);
    Task<IIndexInfo?> GetIndexInfoAsync(string indexName, CancellationToken cancellationToken = default);
    Task<ITableStatistics?> GetTableStatisticsAsync(string tableName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for query results from storage.
/// </summary>
public interface IQueryResult
{
    ISchema Schema { get; }
    long RowCount { get; }
    IAsyncEnumerable<IDataRow> GetRowsAsync(CancellationToken cancellationToken = default);
}

#endregion

#region Data Row and Schema Interfaces

/// <summary>
/// Interface for data rows.
/// </summary>
public interface IDataRow
{
    ISchema Schema { get; }
    object? GetValue(string columnName);
    T? GetValue<T>(string columnName);
    bool HasValue(string columnName);
    IReadOnlyDictionary<string, object?> GetAllValues();
}

/// <summary>
/// Interface for schema definition.
/// </summary>
public interface ISchema
{
    IReadOnlyList<IColumnInfo> Columns { get; }
    IColumnInfo? GetColumn(string name);
    bool HasColumn(string name);
}

/// <summary>
/// Interface for column information.
/// </summary>
public interface IColumnInfo
{
    string Name { get; }
    Type DataType { get; }
    bool IsNullable { get; }
    string? TableName { get; }
    int? MaxLength { get; }
    bool IsPrimaryKey { get; }
    bool IsUnique { get; }
}

#endregion

#region Simple Implementations

/// <summary>
/// Simple data row implementation.
/// </summary>
public class DataRow : IDataRow
{
    private readonly Dictionary<string, object?> _values;
    private readonly ISchema _schema;

    public ISchema Schema => _schema;

    public DataRow(Dictionary<string, object?> values, ISchema? schema = null)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
        _schema = schema ?? CreateSchemaFromValues(values);
    }

    public object? GetValue(string columnName)
    {
        return _values.TryGetValue(columnName, out var value) ? value : null;
    }

    public T? GetValue<T>(string columnName)
    {
        var value = GetValue(columnName);
        return value is T typedValue ? typedValue : default;
    }

    public bool HasValue(string columnName)
    {
        return _values.ContainsKey(columnName);
    }

    public IReadOnlyDictionary<string, object?> GetAllValues()
    {
        return _values;
    }

    private static ISchema CreateSchemaFromValues(Dictionary<string, object?> values)
    {
        var columns = values.Select(kvp => new ColumnInfo(
            kvp.Key,
            kvp.Value?.GetType() ?? typeof(object),
            true,
            null)).ToList();

        return new Schema(columns);
    }
}

/// <summary>
/// Simple schema implementation.
/// </summary>
public class Schema : ISchema
{
    public IReadOnlyList<IColumnInfo> Columns { get; }

    public Schema(IReadOnlyList<IColumnInfo> columns)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
    }

    public IColumnInfo? GetColumn(string name)
    {
        return Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasColumn(string name)
    {
        return GetColumn(name) != null;
    }
}

/// <summary>
/// Simple column info implementation.
/// </summary>
public class ColumnInfo : IColumnInfo
{
    public string Name { get; }
    public Type DataType { get; }
    public bool IsNullable { get; }
    public string? TableName { get; }
    public int? MaxLength { get; }
    public bool IsPrimaryKey { get; }
    public bool IsUnique { get; }

    public ColumnInfo(
        string name,
        Type dataType,
        bool isNullable,
        string? tableName = null,
        int? maxLength = null,
        bool isPrimaryKey = false,
        bool isUnique = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        IsNullable = isNullable;
        TableName = tableName;
        MaxLength = maxLength;
        IsPrimaryKey = isPrimaryKey;
        IsUnique = isUnique;
    }
}

/// <summary>
/// Simple query result implementation.
/// </summary>
public class QueryResult : IQueryResult
{
    private readonly IReadOnlyList<IDataRow> _rows;

    public ISchema Schema { get; }
    public long RowCount => _rows.Count;

    public QueryResult(IReadOnlyList<IDataRow> rows, ISchema schema)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in _rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}

#endregion

#region Enums and Additional Types

/// <summary>
/// Physical operator types for execution.
/// </summary>
public enum PhysicalOperatorType
{
    TableScan,
    IndexScan,
    NestedLoopJoin,
    HashJoin,
    MergeJoin,
    HashAggregate,
    StreamAggregate,
    Sort,
    Filter,
    Projection,
    Union,
    Intersect,
    Except
}

/// <summary>
/// Query types.
/// </summary>
public enum QueryType
{
    Select,
    Insert,
    Update,
    Delete,
    Create,
    Drop,
    Alter
}

/// <summary>
/// Index types.
/// </summary>
public enum IndexType
{
    BTree,
    Hash,
    Bitmap,
    FullText
}

#endregion
