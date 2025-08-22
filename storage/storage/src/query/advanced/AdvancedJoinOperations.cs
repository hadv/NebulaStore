using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Advanced join operations implementation with sophisticated algorithms.
/// Supports hash joins, merge joins, nested loop joins, and index-based joins.
/// </summary>
public class AdvancedJoinOperations
{
    private readonly IQueryExecutionContext _executionContext;
    private readonly IJoinStatisticsCollector _statisticsCollector;

    public AdvancedJoinOperations(
        IQueryExecutionContext executionContext,
        IJoinStatisticsCollector statisticsCollector)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _statisticsCollector = statisticsCollector ?? throw new ArgumentNullException(nameof(statisticsCollector));
    }

    /// <summary>
    /// Executes a hash join operation with build and probe phases.
    /// </summary>
    public async Task<IQueryResult> ExecuteHashJoinAsync(
        IQueryResult leftInput,
        IQueryResult rightInput,
        IJoinCondition joinCondition,
        JoinType joinType,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Phase 1: Build hash table from smaller relation
            var (buildInput, probeInput, buildOnLeft) = DetermineBuildProbeInputs(leftInput, rightInput);
            var hashTable = await BuildHashTableAsync(buildInput, joinCondition, buildOnLeft, cancellationToken);

            // Phase 2: Probe hash table with larger relation
            var results = await ProbeHashTableAsync(
                probeInput, 
                hashTable, 
                joinCondition, 
                joinType, 
                buildOnLeft, 
                cancellationToken);

            // Collect statistics
            await _statisticsCollector.RecordJoinExecutionAsync(new JoinExecutionStats
            {
                JoinType = joinType,
                Algorithm = JoinAlgorithm.HashJoin,
                LeftRows = leftInput.RowCount,
                RightRows = rightInput.RowCount,
                ResultRows = results.Count(),
                ExecutionTime = DateTime.UtcNow - startTime,
                MemoryUsed = EstimateHashTableMemory(hashTable)
            });

            return new QueryResult(results, CreateJoinSchema(leftInput.Schema, rightInput.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Hash join execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a merge join operation on pre-sorted inputs.
    /// </summary>
    public async Task<IQueryResult> ExecuteMergeJoinAsync(
        IQueryResult leftInput,
        IQueryResult rightInput,
        IJoinCondition joinCondition,
        JoinType joinType,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Ensure inputs are sorted on join keys
            var sortedLeft = await EnsureSortedAsync(leftInput, joinCondition.LeftKeys, cancellationToken);
            var sortedRight = await EnsureSortedAsync(rightInput, joinCondition.RightKeys, cancellationToken);

            // Perform merge join
            var results = await PerformMergeJoinAsync(
                sortedLeft, 
                sortedRight, 
                joinCondition, 
                joinType, 
                cancellationToken);

            // Collect statistics
            await _statisticsCollector.RecordJoinExecutionAsync(new JoinExecutionStats
            {
                JoinType = joinType,
                Algorithm = JoinAlgorithm.MergeJoin,
                LeftRows = leftInput.RowCount,
                RightRows = rightInput.RowCount,
                ResultRows = results.Count(),
                ExecutionTime = DateTime.UtcNow - startTime,
                MemoryUsed = 0 // Merge join uses minimal memory
            });

            return new QueryResult(results, CreateJoinSchema(leftInput.Schema, rightInput.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Merge join execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a nested loop join with optional index optimization.
    /// </summary>
    public async Task<IQueryResult> ExecuteNestedLoopJoinAsync(
        IQueryResult leftInput,
        IQueryResult rightInput,
        IJoinCondition joinCondition,
        JoinType joinType,
        IIndexInfo? rightIndex = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var results = new List<IDataRow>();

            await foreach (var leftRow in leftInput.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rightIndex != null)
                {
                    // Index nested loop join
                    var matchingRightRows = await FindMatchingRowsWithIndexAsync(
                        leftRow, 
                        rightInput, 
                        rightIndex, 
                        joinCondition, 
                        cancellationToken);

                    foreach (var rightRow in matchingRightRows)
                    {
                        if (EvaluateJoinCondition(leftRow, rightRow, joinCondition))
                        {
                            results.Add(CreateJoinedRow(leftRow, rightRow, joinType));
                        }
                    }
                }
                else
                {
                    // Standard nested loop join
                    await foreach (var rightRow in rightInput.GetRowsAsync(cancellationToken))
                    {
                        if (EvaluateJoinCondition(leftRow, rightRow, joinCondition))
                        {
                            results.Add(CreateJoinedRow(leftRow, rightRow, joinType));
                        }
                    }
                }

                // Handle outer join cases
                if (joinType == JoinType.LeftOuter && !results.Any(r => ReferenceEquals(r.GetValue("left_row"), leftRow)))
                {
                    results.Add(CreateJoinedRow(leftRow, null, joinType));
                }
            }

            // Collect statistics
            await _statisticsCollector.RecordJoinExecutionAsync(new JoinExecutionStats
            {
                JoinType = joinType,
                Algorithm = rightIndex != null ? JoinAlgorithm.IndexNestedLoop : JoinAlgorithm.NestedLoop,
                LeftRows = leftInput.RowCount,
                RightRows = rightInput.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                MemoryUsed = EstimateRowListMemory(results)
            });

            return new QueryResult(results, CreateJoinSchema(leftInput.Schema, rightInput.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Nested loop join execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a cross join (Cartesian product) operation.
    /// </summary>
    public async Task<IQueryResult> ExecuteCrossJoinAsync(
        IQueryResult leftInput,
        IQueryResult rightInput,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var results = new List<IDataRow>();

            await foreach (var leftRow in leftInput.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await foreach (var rightRow in rightInput.GetRowsAsync(cancellationToken))
                {
                    results.Add(CreateJoinedRow(leftRow, rightRow, JoinType.Inner));
                }
            }

            // Collect statistics
            await _statisticsCollector.RecordJoinExecutionAsync(new JoinExecutionStats
            {
                JoinType = JoinType.Cross,
                Algorithm = JoinAlgorithm.NestedLoop,
                LeftRows = leftInput.RowCount,
                RightRows = rightInput.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                MemoryUsed = EstimateRowListMemory(results)
            });

            return new QueryResult(results, CreateJoinSchema(leftInput.Schema, rightInput.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Cross join execution failed: {ex.Message}", ex);
        }
    }

    // Private helper methods
    private (IQueryResult build, IQueryResult probe, bool buildOnLeft) DetermineBuildProbeInputs(
        IQueryResult leftInput, 
        IQueryResult rightInput)
    {
        // Use smaller relation as build input for better memory efficiency
        if (leftInput.RowCount <= rightInput.RowCount)
        {
            return (leftInput, rightInput, true);
        }
        else
        {
            return (rightInput, leftInput, false);
        }
    }

    private async Task<Dictionary<object, List<IDataRow>>> BuildHashTableAsync(
        IQueryResult buildInput,
        IJoinCondition joinCondition,
        bool buildOnLeft,
        CancellationToken cancellationToken)
    {
        var hashTable = new Dictionary<object, List<IDataRow>>();
        var joinKeys = buildOnLeft ? joinCondition.LeftKeys : joinCondition.RightKeys;

        await foreach (var row in buildInput.GetRowsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keyValue = ExtractJoinKeyValue(row, joinKeys);
            if (keyValue != null)
            {
                if (!hashTable.ContainsKey(keyValue))
                {
                    hashTable[keyValue] = new List<IDataRow>();
                }
                hashTable[keyValue].Add(row);
            }
        }

        return hashTable;
    }

    private async Task<IEnumerable<IDataRow>> ProbeHashTableAsync(
        IQueryResult probeInput,
        Dictionary<object, List<IDataRow>> hashTable,
        IJoinCondition joinCondition,
        JoinType joinType,
        bool buildOnLeft,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        var joinKeys = buildOnLeft ? joinCondition.RightKeys : joinCondition.LeftKeys;

        await foreach (var probeRow in probeInput.GetRowsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keyValue = ExtractJoinKeyValue(probeRow, joinKeys);
            var hasMatch = false;

            if (keyValue != null && hashTable.TryGetValue(keyValue, out var matchingRows))
            {
                foreach (var buildRow in matchingRows)
                {
                    var leftRow = buildOnLeft ? buildRow : probeRow;
                    var rightRow = buildOnLeft ? probeRow : buildRow;

                    if (EvaluateJoinCondition(leftRow, rightRow, joinCondition))
                    {
                        results.Add(CreateJoinedRow(leftRow, rightRow, joinType));
                        hasMatch = true;
                    }
                }
            }

            // Handle outer join cases
            if (!hasMatch && (joinType == JoinType.LeftOuter || joinType == JoinType.FullOuter))
            {
                var leftRow = buildOnLeft ? null : probeRow;
                var rightRow = buildOnLeft ? probeRow : null;
                results.Add(CreateJoinedRow(leftRow, rightRow, joinType));
            }
        }

        return results;
    }

    private async Task<IQueryResult> EnsureSortedAsync(
        IQueryResult input,
        IReadOnlyList<string> sortKeys,
        CancellationToken cancellationToken)
    {
        // Check if input is already sorted
        if (IsAlreadySorted(input, sortKeys))
        {
            return input;
        }

        // Sort the input
        var rows = new List<IDataRow>();
        await foreach (var row in input.GetRowsAsync(cancellationToken))
        {
            rows.Add(row);
        }

        var sortedRows = rows.OrderBy(row => ExtractJoinKeyValue(row, sortKeys)).ToList();
        return new QueryResult(sortedRows, input.Schema);
    }

    private async Task<IEnumerable<IDataRow>> PerformMergeJoinAsync(
        IQueryResult leftInput,
        IQueryResult rightInput,
        IJoinCondition joinCondition,
        JoinType joinType,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        var leftEnumerator = leftInput.GetRowsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        var rightEnumerator = rightInput.GetRowsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        var hasLeftRow = await leftEnumerator.MoveNextAsync();
        var hasRightRow = await rightEnumerator.MoveNextAsync();

        while (hasLeftRow && hasRightRow)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leftKey = ExtractJoinKeyValue(leftEnumerator.Current, joinCondition.LeftKeys);
            var rightKey = ExtractJoinKeyValue(rightEnumerator.Current, joinCondition.RightKeys);

            var comparison = CompareJoinKeys(leftKey, rightKey);

            if (comparison == 0)
            {
                // Keys match - join the rows
                if (EvaluateJoinCondition(leftEnumerator.Current, rightEnumerator.Current, joinCondition))
                {
                    results.Add(CreateJoinedRow(leftEnumerator.Current, rightEnumerator.Current, joinType));
                }
                hasLeftRow = await leftEnumerator.MoveNextAsync();
                hasRightRow = await rightEnumerator.MoveNextAsync();
            }
            else if (comparison < 0)
            {
                // Left key is smaller - advance left
                if (joinType == JoinType.LeftOuter || joinType == JoinType.FullOuter)
                {
                    results.Add(CreateJoinedRow(leftEnumerator.Current, null, joinType));
                }
                hasLeftRow = await leftEnumerator.MoveNextAsync();
            }
            else
            {
                // Right key is smaller - advance right
                if (joinType == JoinType.RightOuter || joinType == JoinType.FullOuter)
                {
                    results.Add(CreateJoinedRow(null, rightEnumerator.Current, joinType));
                }
                hasRightRow = await rightEnumerator.MoveNextAsync();
            }
        }

        // Handle remaining rows for outer joins
        while (hasLeftRow && (joinType == JoinType.LeftOuter || joinType == JoinType.FullOuter))
        {
            results.Add(CreateJoinedRow(leftEnumerator.Current, null, joinType));
            hasLeftRow = await leftEnumerator.MoveNextAsync();
        }

        while (hasRightRow && (joinType == JoinType.RightOuter || joinType == JoinType.FullOuter))
        {
            results.Add(CreateJoinedRow(null, rightEnumerator.Current, joinType));
            hasRightRow = await rightEnumerator.MoveNextAsync();
        }

        await leftEnumerator.DisposeAsync();
        await rightEnumerator.DisposeAsync();

        return results;
    }

    private object? ExtractJoinKeyValue(IDataRow row, IReadOnlyList<string> keyColumns)
    {
        if (keyColumns.Count == 1)
        {
            return row.GetValue(keyColumns[0]);
        }
        else
        {
            // Composite key - create tuple
            var keyValues = keyColumns.Select(col => row.GetValue(col)).ToArray();
            return new CompositeKey(keyValues);
        }
    }

    private bool EvaluateJoinCondition(IDataRow leftRow, IDataRow rightRow, IJoinCondition joinCondition)
    {
        // Evaluate additional join predicates beyond key equality
        return joinCondition.EvaluateCondition(leftRow, rightRow);
    }

    private IDataRow CreateJoinedRow(IDataRow? leftRow, IDataRow? rightRow, JoinType joinType)
    {
        var joinedValues = new Dictionary<string, object?>();

        // Add left row values
        if (leftRow != null)
        {
            foreach (var column in leftRow.Schema.Columns)
            {
                joinedValues[$"left_{column.Name}"] = leftRow.GetValue(column.Name);
            }
        }

        // Add right row values
        if (rightRow != null)
        {
            foreach (var column in rightRow.Schema.Columns)
            {
                joinedValues[$"right_{column.Name}"] = rightRow.GetValue(column.Name);
            }
        }

        return new DataRow(joinedValues);
    }

    private ISchema CreateJoinSchema(ISchema leftSchema, ISchema rightSchema)
    {
        var columns = new List<IColumnInfo>();

        // Add left columns with prefix
        foreach (var column in leftSchema.Columns)
        {
            columns.Add(new ColumnInfo($"left_{column.Name}", column.DataType, column.IsNullable, column.TableName));
        }

        // Add right columns with prefix
        foreach (var column in rightSchema.Columns)
        {
            columns.Add(new ColumnInfo($"right_{column.Name}", column.DataType, column.IsNullable, column.TableName));
        }

        return new Schema(columns);
    }

    private bool IsAlreadySorted(IQueryResult input, IReadOnlyList<string> sortKeys)
    {
        // Simplified check - in practice would examine query plan or metadata
        return false;
    }

    private int CompareJoinKeys(object? leftKey, object? rightKey)
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

    private async Task<IEnumerable<IDataRow>> FindMatchingRowsWithIndexAsync(
        IDataRow leftRow,
        IQueryResult rightInput,
        IIndexInfo rightIndex,
        IJoinCondition joinCondition,
        CancellationToken cancellationToken)
    {
        // Use index to find matching rows efficiently
        var keyValue = ExtractJoinKeyValue(leftRow, joinCondition.LeftKeys);
        if (keyValue == null) return Enumerable.Empty<IDataRow>();

        // This would integrate with the actual index implementation
        return await _executionContext.FindRowsByIndexAsync(rightIndex, keyValue, cancellationToken);
    }

    private long EstimateHashTableMemory(Dictionary<object, List<IDataRow>> hashTable)
    {
        // Rough estimation of hash table memory usage
        return hashTable.Sum(kvp => kvp.Value.Count * 100); // 100 bytes per row estimate
    }

    private long EstimateRowListMemory(List<IDataRow> rows)
    {
        return rows.Count * 100; // 100 bytes per row estimate
    }
}

/// <summary>
/// Composite key for multi-column joins.
/// </summary>
public class CompositeKey : IEquatable<CompositeKey>, IComparable<CompositeKey>
{
    private readonly object?[] _values;
    private readonly int _hashCode;

    public CompositeKey(object?[] values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
        _hashCode = ComputeHashCode();
    }

    public bool Equals(CompositeKey? other)
    {
        if (other == null || _values.Length != other._values.Length)
            return false;

        for (int i = 0; i < _values.Length; i++)
        {
            if (!Equals(_values[i], other._values[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as CompositeKey);

    public override int GetHashCode() => _hashCode;

    public int CompareTo(CompositeKey? other)
    {
        if (other == null) return 1;

        var minLength = Math.Min(_values.Length, other._values.Length);
        for (int i = 0; i < minLength; i++)
        {
            var comparison = CompareValues(_values[i], other._values[i]);
            if (comparison != 0) return comparison;
        }

        return _values.Length.CompareTo(other._values.Length);
    }

    private int ComputeHashCode()
    {
        var hash = 17;
        foreach (var value in _values)
        {
            hash = hash * 31 + (value?.GetHashCode() ?? 0);
        }
        return hash;
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        if (left is IComparable leftComparable && right is IComparable)
        {
            return leftComparable.CompareTo(right);
        }

        return left.Equals(right) ? 0 : left.GetHashCode().CompareTo(right.GetHashCode());
    }
}
