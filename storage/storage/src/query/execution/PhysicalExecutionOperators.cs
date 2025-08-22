using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Execution;

/// <summary>
/// Base class for physical execution operators.
/// </summary>
public abstract class PhysicalExecutionOperatorBase : IPhysicalExecutionOperator
{
    protected readonly IQueryExecutionContext _context;
    protected readonly List<IPhysicalExecutionOperator> _children = new();

    public abstract PhysicalOperatorType OperatorType { get; }
    public IReadOnlyList<IPhysicalExecutionOperator> Children => _children;
    public long ProcessedRows { get; protected set; }
    public TimeSpan ExecutionTime { get; protected set; }
    public long MemoryUsed { get; protected set; }

    protected PhysicalExecutionOperatorBase(IQueryExecutionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public abstract IAsyncEnumerable<IDataRow> ExecuteAsync(CancellationToken cancellationToken = default);

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var child in _children)
        {
            await child.DisposeAsync();
        }
    }

    protected void AddChild(IPhysicalExecutionOperator child)
    {
        _children.Add(child);
    }
}

/// <summary>
/// Table scan execution operator.
/// </summary>
public class TableScanExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly string _tableName;
    private readonly IStorageEngine _storageEngine;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.TableScan;

    public TableScanExecutionOperator(string tableName, IStorageEngine storageEngine, IQueryExecutionContext context)
        : base(context)
    {
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _storageEngine = storageEngine ?? throw new ArgumentNullException(nameof(storageEngine));
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Get table data from storage engine
            var tableData = await _storageEngine.GetTableDataAsync(_tableName, cancellationToken);

            await foreach (var row in tableData.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessedRows++;
                yield return row;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }
}

/// <summary>
/// Index scan execution operator.
/// </summary>
public class IndexScanExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly string _indexName;
    private readonly string _tableName;
    private readonly IStorageEngine _storageEngine;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.IndexScan;

    public IndexScanExecutionOperator(string indexName, string tableName, IStorageEngine storageEngine, IQueryExecutionContext context)
        : base(context)
    {
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _storageEngine = storageEngine ?? throw new ArgumentNullException(nameof(storageEngine));
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Get index data from storage engine
            var indexData = await _storageEngine.GetIndexDataAsync(_indexName, _tableName, cancellationToken);

            await foreach (var row in indexData.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessedRows++;
                yield return row;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }
}

/// <summary>
/// Hash join execution operator.
/// </summary>
public class HashJoinExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _leftChild;
    private readonly IPhysicalExecutionOperator _rightChild;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.HashJoin;

    public HashJoinExecutionOperator(
        IPhysicalExecutionOperator leftChild,
        IPhysicalExecutionOperator rightChild,
        IQueryExecutionContext context)
        : base(context)
    {
        _leftChild = leftChild ?? throw new ArgumentNullException(nameof(leftChild));
        _rightChild = rightChild ?? throw new ArgumentNullException(nameof(rightChild));
        AddChild(_leftChild);
        AddChild(_rightChild);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Build phase: Create hash table from left input
            var hashTable = new Dictionary<object, List<IDataRow>>();
            var memoryUsed = 0L;

            await foreach (var leftRow in _leftChild.ExecuteAsync(cancellationToken))
            {
                var joinKey = ExtractJoinKey(leftRow);
                if (!hashTable.ContainsKey(joinKey))
                {
                    hashTable[joinKey] = new List<IDataRow>();
                }
                hashTable[joinKey].Add(leftRow);
                memoryUsed += EstimateRowSize(leftRow);

                // Check memory budget
                if (!_context.MemoryBudget.CanAllocate(memoryUsed))
                {
                    throw new OutOfMemoryException("Hash join exceeded memory budget during build phase");
                }
            }

            // Probe phase: Join with right input
            await foreach (var rightRow in _rightChild.ExecuteAsync(cancellationToken))
            {
                var joinKey = ExtractJoinKey(rightRow);
                if (hashTable.TryGetValue(joinKey, out var matchingRows))
                {
                    foreach (var leftRow in matchingRows)
                    {
                        var joinedRow = CreateJoinedRow(leftRow, rightRow);
                        ProcessedRows++;
                        yield return joinedRow;
                    }
                }
            }

            MemoryUsed = memoryUsed;
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private object ExtractJoinKey(IDataRow row)
    {
        // Simplified join key extraction
        return row.GetValue("id") ?? DBNull.Value;
    }

    private IDataRow CreateJoinedRow(IDataRow leftRow, IDataRow rightRow)
    {
        // Simplified row joining
        var values = new Dictionary<string, object?>();
        
        // Add left row values with prefix
        foreach (var column in leftRow.Schema.Columns)
        {
            values[$"left_{column.Name}"] = leftRow.GetValue(column.Name);
        }
        
        // Add right row values with prefix
        foreach (var column in rightRow.Schema.Columns)
        {
            values[$"right_{column.Name}"] = rightRow.GetValue(column.Name);
        }

        return new DataRow(values);
    }

    private long EstimateRowSize(IDataRow row)
    {
        return 100; // Simplified estimation
    }
}

/// <summary>
/// Nested loop join execution operator.
/// </summary>
public class NestedLoopJoinExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _leftChild;
    private readonly IPhysicalExecutionOperator _rightChild;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.NestedLoopJoin;

    public NestedLoopJoinExecutionOperator(
        IPhysicalExecutionOperator leftChild,
        IPhysicalExecutionOperator rightChild,
        IQueryExecutionContext context)
        : base(context)
    {
        _leftChild = leftChild ?? throw new ArgumentNullException(nameof(leftChild));
        _rightChild = rightChild ?? throw new ArgumentNullException(nameof(rightChild));
        AddChild(_leftChild);
        AddChild(_rightChild);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await foreach (var leftRow in _leftChild.ExecuteAsync(cancellationToken))
            {
                await foreach (var rightRow in _rightChild.ExecuteAsync(cancellationToken))
                {
                    if (EvaluateJoinCondition(leftRow, rightRow))
                    {
                        var joinedRow = CreateJoinedRow(leftRow, rightRow);
                        ProcessedRows++;
                        yield return joinedRow;
                    }
                }
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private bool EvaluateJoinCondition(IDataRow leftRow, IDataRow rightRow)
    {
        // Simplified join condition evaluation
        var leftKey = leftRow.GetValue("id");
        var rightKey = rightRow.GetValue("id");
        return Equals(leftKey, rightKey);
    }

    private IDataRow CreateJoinedRow(IDataRow leftRow, IDataRow rightRow)
    {
        var values = new Dictionary<string, object?>();
        
        foreach (var column in leftRow.Schema.Columns)
        {
            values[$"left_{column.Name}"] = leftRow.GetValue(column.Name);
        }
        
        foreach (var column in rightRow.Schema.Columns)
        {
            values[$"right_{column.Name}"] = rightRow.GetValue(column.Name);
        }

        return new DataRow(values);
    }
}

/// <summary>
/// Merge join execution operator.
/// </summary>
public class MergeJoinExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _leftChild;
    private readonly IPhysicalExecutionOperator _rightChild;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.MergeJoin;

    public MergeJoinExecutionOperator(
        IPhysicalExecutionOperator leftChild,
        IPhysicalExecutionOperator rightChild,
        IQueryExecutionContext context)
        : base(context)
    {
        _leftChild = leftChild ?? throw new ArgumentNullException(nameof(leftChild));
        _rightChild = rightChild ?? throw new ArgumentNullException(nameof(rightChild));
        AddChild(_leftChild);
        AddChild(_rightChild);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var leftEnumerator = _leftChild.ExecuteAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            var rightEnumerator = _rightChild.ExecuteAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

            var hasLeft = await leftEnumerator.MoveNextAsync();
            var hasRight = await rightEnumerator.MoveNextAsync();

            while (hasLeft && hasRight)
            {
                var leftKey = ExtractJoinKey(leftEnumerator.Current);
                var rightKey = ExtractJoinKey(rightEnumerator.Current);
                var comparison = CompareKeys(leftKey, rightKey);

                if (comparison == 0)
                {
                    // Keys match - join the rows
                    var joinedRow = CreateJoinedRow(leftEnumerator.Current, rightEnumerator.Current);
                    ProcessedRows++;
                    yield return joinedRow;
                    
                    hasLeft = await leftEnumerator.MoveNextAsync();
                    hasRight = await rightEnumerator.MoveNextAsync();
                }
                else if (comparison < 0)
                {
                    // Left key is smaller - advance left
                    hasLeft = await leftEnumerator.MoveNextAsync();
                }
                else
                {
                    // Right key is smaller - advance right
                    hasRight = await rightEnumerator.MoveNextAsync();
                }
            }

            await leftEnumerator.DisposeAsync();
            await rightEnumerator.DisposeAsync();
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private object ExtractJoinKey(IDataRow row)
    {
        return row.GetValue("id") ?? DBNull.Value;
    }

    private int CompareKeys(object leftKey, object rightKey)
    {
        if (leftKey == null && rightKey == null) return 0;
        if (leftKey == null) return -1;
        if (rightKey == null) return 1;

        if (leftKey is IComparable leftComparable && rightKey is IComparable)
        {
            return leftComparable.CompareTo(rightKey);
        }

        return leftKey.Equals(rightKey) ? 0 : leftKey.GetHashCode().CompareTo(rightKey.GetHashCode());
    }

    private IDataRow CreateJoinedRow(IDataRow leftRow, IDataRow rightRow)
    {
        var values = new Dictionary<string, object?>();
        
        foreach (var column in leftRow.Schema.Columns)
        {
            values[$"left_{column.Name}"] = leftRow.GetValue(column.Name);
        }
        
        foreach (var column in rightRow.Schema.Columns)
        {
            values[$"right_{column.Name}"] = rightRow.GetValue(column.Name);
        }

        return new DataRow(values);
    }
}

/// <summary>
/// Hash aggregate execution operator.
/// </summary>
public class HashAggregateExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _child;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.HashAggregate;

    public HashAggregateExecutionOperator(IPhysicalExecutionOperator child, IQueryExecutionContext context)
        : base(context)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        AddChild(_child);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var aggregateMap = new Dictionary<object, AggregateState>();

            await foreach (var row in _child.ExecuteAsync(cancellationToken))
            {
                var groupKey = ExtractGroupKey(row);
                
                if (!aggregateMap.TryGetValue(groupKey, out var state))
                {
                    state = new AggregateState();
                    aggregateMap[groupKey] = state;
                }

                state.ProcessRow(row);
            }

            foreach (var kvp in aggregateMap)
            {
                var resultRow = CreateAggregateResultRow(kvp.Key, kvp.Value);
                ProcessedRows++;
                yield return resultRow;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private object ExtractGroupKey(IDataRow row)
    {
        // Simplified group key extraction
        return row.GetValue("category") ?? "ALL";
    }

    private IDataRow CreateAggregateResultRow(object groupKey, AggregateState state)
    {
        var values = new Dictionary<string, object?>
        {
            ["group_key"] = groupKey,
            ["count"] = state.Count,
            ["sum"] = state.Sum,
            ["avg"] = state.Count > 0 ? state.Sum / state.Count : 0
        };

        return new DataRow(values);
    }

    private class AggregateState
    {
        public long Count { get; private set; }
        public double Sum { get; private set; }

        public void ProcessRow(IDataRow row)
        {
            Count++;
            if (row.GetValue("amount") is double amount)
            {
                Sum += amount;
            }
        }
    }
}

/// <summary>
/// Stream aggregate execution operator.
/// </summary>
public class StreamAggregateExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _child;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.StreamAggregate;

    public StreamAggregateExecutionOperator(IPhysicalExecutionOperator child, IQueryExecutionContext context)
        : base(context)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        AddChild(_child);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            object? currentGroupKey = null;
            var currentState = new AggregateState();

            await foreach (var row in _child.ExecuteAsync(cancellationToken))
            {
                var groupKey = ExtractGroupKey(row);

                if (currentGroupKey != null && !currentGroupKey.Equals(groupKey))
                {
                    // Finalize current group
                    var resultRow = CreateAggregateResultRow(currentGroupKey, currentState);
                    ProcessedRows++;
                    yield return resultRow;

                    // Start new group
                    currentState = new AggregateState();
                }

                currentGroupKey = groupKey;
                currentState.ProcessRow(row);
            }

            // Finalize last group
            if (currentGroupKey != null)
            {
                var resultRow = CreateAggregateResultRow(currentGroupKey, currentState);
                ProcessedRows++;
                yield return resultRow;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private object ExtractGroupKey(IDataRow row)
    {
        return row.GetValue("category") ?? "ALL";
    }

    private IDataRow CreateAggregateResultRow(object groupKey, AggregateState state)
    {
        var values = new Dictionary<string, object?>
        {
            ["group_key"] = groupKey,
            ["count"] = state.Count,
            ["sum"] = state.Sum,
            ["avg"] = state.Count > 0 ? state.Sum / state.Count : 0
        };

        return new DataRow(values);
    }

    private class AggregateState
    {
        public long Count { get; private set; }
        public double Sum { get; private set; }

        public void ProcessRow(IDataRow row)
        {
            Count++;
            if (row.GetValue("amount") is double amount)
            {
                Sum += amount;
            }
        }
    }
}

/// <summary>
/// Sort execution operator.
/// </summary>
public class SortExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _child;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.Sort;

    public SortExecutionOperator(IPhysicalExecutionOperator child, IQueryExecutionContext context)
        : base(context)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        AddChild(_child);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var rows = new List<IDataRow>();

            // Collect all rows
            await foreach (var row in _child.ExecuteAsync(cancellationToken))
            {
                rows.Add(row);
            }

            // Sort rows
            rows.Sort((x, y) => CompareRows(x, y));

            // Return sorted rows
            foreach (var row in rows)
            {
                ProcessedRows++;
                yield return row;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private int CompareRows(IDataRow x, IDataRow y)
    {
        // Simplified row comparison
        var xValue = x.GetValue("id");
        var yValue = y.GetValue("id");

        if (xValue == null && yValue == null) return 0;
        if (xValue == null) return -1;
        if (yValue == null) return 1;

        if (xValue is IComparable xComparable && yValue is IComparable)
        {
            return xComparable.CompareTo(yValue);
        }

        return xValue.Equals(yValue) ? 0 : xValue.GetHashCode().CompareTo(yValue.GetHashCode());
    }
}

/// <summary>
/// Filter execution operator.
/// </summary>
public class FilterExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _child;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.Filter;

    public FilterExecutionOperator(IPhysicalExecutionOperator child, IQueryExecutionContext context)
        : base(context)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        AddChild(_child);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await foreach (var row in _child.ExecuteAsync(cancellationToken))
            {
                if (EvaluateFilter(row))
                {
                    ProcessedRows++;
                    yield return row;
                }
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private bool EvaluateFilter(IDataRow row)
    {
        // Simplified filter evaluation
        var value = row.GetValue("active");
        return value is bool active && active;
    }
}

/// <summary>
/// Projection execution operator.
/// </summary>
public class ProjectionExecutionOperator : PhysicalExecutionOperatorBase
{
    private readonly IPhysicalExecutionOperator _child;

    public override PhysicalOperatorType OperatorType => PhysicalOperatorType.Projection;

    public ProjectionExecutionOperator(IPhysicalExecutionOperator child, IQueryExecutionContext context)
        : base(context)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        AddChild(_child);
    }

    public override async IAsyncEnumerable<IDataRow> ExecuteAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await foreach (var row in _child.ExecuteAsync(cancellationToken))
            {
                var projectedRow = ProjectRow(row);
                ProcessedRows++;
                yield return projectedRow;
            }
        }
        finally
        {
            ExecutionTime = DateTime.UtcNow - startTime;
        }
    }

    private IDataRow ProjectRow(IDataRow row)
    {
        // Simplified projection - select specific columns
        var values = new Dictionary<string, object?>();
        
        if (row.Schema.Columns.Any(c => c.Name == "id"))
        {
            values["id"] = row.GetValue("id");
        }
        
        if (row.Schema.Columns.Any(c => c.Name == "name"))
        {
            values["name"] = row.GetValue("name");
        }

        return new DataRow(values);
    }
}
