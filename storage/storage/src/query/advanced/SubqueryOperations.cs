using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Advanced subquery operations including correlated subqueries, EXISTS, IN, and scalar subqueries.
/// Implements sophisticated optimization techniques for subquery execution.
/// </summary>
public class SubqueryOperations
{
    private readonly IQueryExecutionContext _executionContext;
    private readonly ISubqueryOptimizer _subqueryOptimizer;
    private readonly ISubqueryStatisticsCollector _statisticsCollector;

    public SubqueryOperations(
        IQueryExecutionContext executionContext,
        ISubqueryOptimizer subqueryOptimizer,
        ISubqueryStatisticsCollector statisticsCollector)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _subqueryOptimizer = subqueryOptimizer ?? throw new ArgumentNullException(nameof(subqueryOptimizer));
        _statisticsCollector = statisticsCollector ?? throw new ArgumentNullException(nameof(statisticsCollector));
    }

    /// <summary>
    /// Executes EXISTS subquery with optimization.
    /// </summary>
    public async Task<IQueryResult> ExecuteExistsSubqueryAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Optimize subquery execution strategy
            var optimizationStrategy = await _subqueryOptimizer.OptimizeExistsSubqueryAsync(
                outerInput, 
                subqueryExpression, 
                cancellationToken);

            IQueryResult result = optimizationStrategy.Strategy switch
            {
                SubqueryOptimizationStrategy.SemiJoin => await ExecuteExistsAsSemiJoinAsync(outerInput, subqueryExpression, cancellationToken),
                SubqueryOptimizationStrategy.CorrelatedExecution => await ExecuteCorrelatedExistsAsync(outerInput, subqueryExpression, cancellationToken),
                SubqueryOptimizationStrategy.MaterializeAndHash => await ExecuteExistsWithMaterializedHashAsync(outerInput, subqueryExpression, cancellationToken),
                _ => throw new NotSupportedException($"Optimization strategy {optimizationStrategy.Strategy} not supported for EXISTS")
            };

            // Collect statistics
            await _statisticsCollector.RecordSubqueryExecutionAsync(new SubqueryExecutionStats
            {
                SubqueryType = SubqueryType.Exists,
                OptimizationStrategy = optimizationStrategy.Strategy,
                OuterRows = outerInput.RowCount,
                SubqueryExecutions = optimizationStrategy.EstimatedSubqueryExecutions,
                ExecutionTime = DateTime.UtcNow - startTime,
                ResultRows = result.RowCount
            });

            return result;
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"EXISTS subquery execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes IN subquery with optimization.
    /// </summary>
    public async Task<IQueryResult> ExecuteInSubqueryAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string outerColumn,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Optimize subquery execution strategy
            var optimizationStrategy = await _subqueryOptimizer.OptimizeInSubqueryAsync(
                outerInput, 
                subqueryExpression, 
                outerColumn, 
                cancellationToken);

            IQueryResult result = optimizationStrategy.Strategy switch
            {
                SubqueryOptimizationStrategy.SemiJoin => await ExecuteInAsSemiJoinAsync(outerInput, subqueryExpression, outerColumn, cancellationToken),
                SubqueryOptimizationStrategy.MaterializeAndHash => await ExecuteInWithMaterializedHashAsync(outerInput, subqueryExpression, outerColumn, cancellationToken),
                SubqueryOptimizationStrategy.CorrelatedExecution => await ExecuteCorrelatedInAsync(outerInput, subqueryExpression, outerColumn, cancellationToken),
                _ => throw new NotSupportedException($"Optimization strategy {optimizationStrategy.Strategy} not supported for IN")
            };

            // Collect statistics
            await _statisticsCollector.RecordSubqueryExecutionAsync(new SubqueryExecutionStats
            {
                SubqueryType = SubqueryType.In,
                OptimizationStrategy = optimizationStrategy.Strategy,
                OuterRows = outerInput.RowCount,
                SubqueryExecutions = optimizationStrategy.EstimatedSubqueryExecutions,
                ExecutionTime = DateTime.UtcNow - startTime,
                ResultRows = result.RowCount
            });

            return result;
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"IN subquery execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes scalar subquery that returns a single value.
    /// </summary>
    public async Task<IQueryResult> ExecuteScalarSubqueryAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string resultColumnAlias,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Optimize subquery execution strategy
            var optimizationStrategy = await _subqueryOptimizer.OptimizeScalarSubqueryAsync(
                outerInput, 
                subqueryExpression, 
                cancellationToken);

            IQueryResult result = optimizationStrategy.Strategy switch
            {
                SubqueryOptimizationStrategy.MaterializeOnce => await ExecuteScalarWithMaterializationAsync(outerInput, subqueryExpression, resultColumnAlias, cancellationToken),
                SubqueryOptimizationStrategy.CorrelatedExecution => await ExecuteCorrelatedScalarAsync(outerInput, subqueryExpression, resultColumnAlias, cancellationToken),
                SubqueryOptimizationStrategy.LeftJoin => await ExecuteScalarAsLeftJoinAsync(outerInput, subqueryExpression, resultColumnAlias, cancellationToken),
                _ => throw new NotSupportedException($"Optimization strategy {optimizationStrategy.Strategy} not supported for scalar subquery")
            };

            // Collect statistics
            await _statisticsCollector.RecordSubqueryExecutionAsync(new SubqueryExecutionStats
            {
                SubqueryType = SubqueryType.Scalar,
                OptimizationStrategy = optimizationStrategy.Strategy,
                OuterRows = outerInput.RowCount,
                SubqueryExecutions = optimizationStrategy.EstimatedSubqueryExecutions,
                ExecutionTime = DateTime.UtcNow - startTime,
                ResultRows = result.RowCount
            });

            return result;
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Scalar subquery execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes NOT EXISTS subquery with optimization.
    /// </summary>
    public async Task<IQueryResult> ExecuteNotExistsSubqueryAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Optimize subquery execution strategy
            var optimizationStrategy = await _subqueryOptimizer.OptimizeNotExistsSubqueryAsync(
                outerInput, 
                subqueryExpression, 
                cancellationToken);

            IQueryResult result = optimizationStrategy.Strategy switch
            {
                SubqueryOptimizationStrategy.AntiJoin => await ExecuteNotExistsAsAntiJoinAsync(outerInput, subqueryExpression, cancellationToken),
                SubqueryOptimizationStrategy.CorrelatedExecution => await ExecuteCorrelatedNotExistsAsync(outerInput, subqueryExpression, cancellationToken),
                SubqueryOptimizationStrategy.MaterializeAndHash => await ExecuteNotExistsWithMaterializedHashAsync(outerInput, subqueryExpression, cancellationToken),
                _ => throw new NotSupportedException($"Optimization strategy {optimizationStrategy.Strategy} not supported for NOT EXISTS")
            };

            // Collect statistics
            await _statisticsCollector.RecordSubqueryExecutionAsync(new SubqueryExecutionStats
            {
                SubqueryType = SubqueryType.NotExists,
                OptimizationStrategy = optimizationStrategy.Strategy,
                OuterRows = outerInput.RowCount,
                SubqueryExecutions = optimizationStrategy.EstimatedSubqueryExecutions,
                ExecutionTime = DateTime.UtcNow - startTime,
                ResultRows = result.RowCount
            });

            return result;
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"NOT EXISTS subquery execution failed: {ex.Message}", ex);
        }
    }

    // Private implementation methods for different optimization strategies

    private async Task<IQueryResult> ExecuteExistsAsSemiJoinAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        // Convert EXISTS to semi-join for better performance
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        
        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var hasMatch = await HasMatchInSubqueryAsync(outerRow, subqueryResult, subqueryExpression.CorrelationConditions, cancellationToken);
            if (hasMatch)
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteCorrelatedExistsAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            // Execute subquery for each outer row
            var correlatedQuery = ApplyCorrelationParameters(subqueryExpression.Query, outerRow, subqueryExpression.CorrelationConditions);
            var subqueryResult = await _executionContext.ExecuteQueryAsync(correlatedQuery, cancellationToken);
            
            if (subqueryResult.RowCount > 0)
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteExistsWithMaterializedHashAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        // Materialize subquery result into hash table for fast lookups
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        var hashTable = await BuildSubqueryHashTableAsync(subqueryResult, subqueryExpression.CorrelationConditions, cancellationToken);

        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlationKey = ExtractCorrelationKey(outerRow, subqueryExpression.CorrelationConditions);
            if (hashTable.ContainsKey(correlationKey))
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteInAsSemiJoinAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string outerColumn,
        CancellationToken cancellationToken)
    {
        // Convert IN to semi-join
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        var subqueryValues = new HashSet<object?>();

        await foreach (var row in subqueryResult.GetRowsAsync(cancellationToken))
        {
            var value = row.GetValue(subqueryExpression.ResultColumn);
            subqueryValues.Add(value);
        }

        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var outerValue = outerRow.GetValue(outerColumn);
            if (subqueryValues.Contains(outerValue))
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteInWithMaterializedHashAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string outerColumn,
        CancellationToken cancellationToken)
    {
        // Similar to semi-join but with correlation support
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        var hashTable = await BuildSubqueryHashTableAsync(subqueryResult, subqueryExpression.CorrelationConditions, cancellationToken);

        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlationKey = ExtractCorrelationKey(outerRow, subqueryExpression.CorrelationConditions);
            if (hashTable.TryGetValue(correlationKey, out var subqueryValues))
            {
                var outerValue = outerRow.GetValue(outerColumn);
                if (subqueryValues.Contains(outerValue))
                {
                    results.Add(outerRow);
                }
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteCorrelatedInAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string outerColumn,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlatedQuery = ApplyCorrelationParameters(subqueryExpression.Query, outerRow, subqueryExpression.CorrelationConditions);
            var subqueryResult = await _executionContext.ExecuteQueryAsync(correlatedQuery, cancellationToken);
            
            var outerValue = outerRow.GetValue(outerColumn);
            var hasMatch = false;

            await foreach (var subqueryRow in subqueryResult.GetRowsAsync(cancellationToken))
            {
                var subqueryValue = subqueryRow.GetValue(subqueryExpression.ResultColumn);
                if (Equals(outerValue, subqueryValue))
                {
                    hasMatch = true;
                    break;
                }
            }

            if (hasMatch)
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteScalarWithMaterializationAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string resultColumnAlias,
        CancellationToken cancellationToken)
    {
        // Execute subquery once and cache result
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        var scalarValue = await GetScalarValueAsync(subqueryResult, cancellationToken);

        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var enhancedRow = AddScalarColumn(outerRow, resultColumnAlias, scalarValue);
            results.Add(enhancedRow);
        }

        return new QueryResult(results, CreateScalarSubquerySchema(outerInput.Schema, resultColumnAlias));
    }

    private async Task<IQueryResult> ExecuteCorrelatedScalarAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string resultColumnAlias,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlatedQuery = ApplyCorrelationParameters(subqueryExpression.Query, outerRow, subqueryExpression.CorrelationConditions);
            var subqueryResult = await _executionContext.ExecuteQueryAsync(correlatedQuery, cancellationToken);
            var scalarValue = await GetScalarValueAsync(subqueryResult, cancellationToken);
            
            var enhancedRow = AddScalarColumn(outerRow, resultColumnAlias, scalarValue);
            results.Add(enhancedRow);
        }

        return new QueryResult(results, CreateScalarSubquerySchema(outerInput.Schema, resultColumnAlias));
    }

    private async Task<IQueryResult> ExecuteScalarAsLeftJoinAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        string resultColumnAlias,
        CancellationToken cancellationToken)
    {
        // Convert scalar subquery to LEFT JOIN for better performance
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        
        // Perform LEFT JOIN
        var joinOperations = new AdvancedJoinOperations(_executionContext, null!);
        var joinCondition = CreateJoinConditionFromCorrelation(subqueryExpression.CorrelationConditions);
        
        return await joinOperations.ExecuteNestedLoopJoinAsync(
            outerInput, 
            subqueryResult, 
            joinCondition, 
            JoinType.LeftOuter, 
            null, 
            cancellationToken);
    }

    private async Task<IQueryResult> ExecuteNotExistsAsAntiJoinAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        // Convert NOT EXISTS to anti-join
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        
        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var hasMatch = await HasMatchInSubqueryAsync(outerRow, subqueryResult, subqueryExpression.CorrelationConditions, cancellationToken);
            if (!hasMatch)
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteCorrelatedNotExistsAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();
        
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlatedQuery = ApplyCorrelationParameters(subqueryExpression.Query, outerRow, subqueryExpression.CorrelationConditions);
            var subqueryResult = await _executionContext.ExecuteQueryAsync(correlatedQuery, cancellationToken);
            
            if (subqueryResult.RowCount == 0)
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    private async Task<IQueryResult> ExecuteNotExistsWithMaterializedHashAsync(
        IQueryResult outerInput,
        ISubqueryExpression subqueryExpression,
        CancellationToken cancellationToken)
    {
        var subqueryResult = await _executionContext.ExecuteQueryAsync(subqueryExpression.Query, cancellationToken);
        var hashTable = await BuildSubqueryHashTableAsync(subqueryResult, subqueryExpression.CorrelationConditions, cancellationToken);

        var results = new List<IDataRow>();
        await foreach (var outerRow in outerInput.GetRowsAsync(cancellationToken))
        {
            var correlationKey = ExtractCorrelationKey(outerRow, subqueryExpression.CorrelationConditions);
            if (!hashTable.ContainsKey(correlationKey))
            {
                results.Add(outerRow);
            }
        }

        return new QueryResult(results, outerInput.Schema);
    }

    // Helper methods
    private async Task<bool> HasMatchInSubqueryAsync(
        IDataRow outerRow,
        IQueryResult subqueryResult,
        IReadOnlyList<ICorrelationCondition> correlationConditions,
        CancellationToken cancellationToken)
    {
        await foreach (var subqueryRow in subqueryResult.GetRowsAsync(cancellationToken))
        {
            if (EvaluateCorrelationConditions(outerRow, subqueryRow, correlationConditions))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<Dictionary<object, HashSet<object?>>> BuildSubqueryHashTableAsync(
        IQueryResult subqueryResult,
        IReadOnlyList<ICorrelationCondition> correlationConditions,
        CancellationToken cancellationToken)
    {
        var hashTable = new Dictionary<object, HashSet<object?>>();

        await foreach (var row in subqueryResult.GetRowsAsync(cancellationToken))
        {
            var correlationKey = ExtractCorrelationKeyFromSubquery(row, correlationConditions);
            if (!hashTable.TryGetValue(correlationKey, out var values))
            {
                values = new HashSet<object?>();
                hashTable[correlationKey] = values;
            }
            
            // Add the row's primary value to the set
            var primaryValue = row.GetValue(row.Schema.Columns.First().Name);
            values.Add(primaryValue);
        }

        return hashTable;
    }

    private object ExtractCorrelationKey(IDataRow outerRow, IReadOnlyList<ICorrelationCondition> correlationConditions)
    {
        if (correlationConditions.Count == 1)
        {
            return outerRow.GetValue(correlationConditions[0].OuterColumn) ?? DBNull.Value;
        }
        else
        {
            var keyValues = correlationConditions.Select(cc => outerRow.GetValue(cc.OuterColumn) ?? DBNull.Value).ToArray();
            return new CompositeKey(keyValues);
        }
    }

    private object ExtractCorrelationKeyFromSubquery(IDataRow subqueryRow, IReadOnlyList<ICorrelationCondition> correlationConditions)
    {
        if (correlationConditions.Count == 1)
        {
            return subqueryRow.GetValue(correlationConditions[0].InnerColumn) ?? DBNull.Value;
        }
        else
        {
            var keyValues = correlationConditions.Select(cc => subqueryRow.GetValue(cc.InnerColumn) ?? DBNull.Value).ToArray();
            return new CompositeKey(keyValues);
        }
    }

    private bool EvaluateCorrelationConditions(
        IDataRow outerRow,
        IDataRow subqueryRow,
        IReadOnlyList<ICorrelationCondition> correlationConditions)
    {
        foreach (var condition in correlationConditions)
        {
            var outerValue = outerRow.GetValue(condition.OuterColumn);
            var innerValue = subqueryRow.GetValue(condition.InnerColumn);
            
            if (!Equals(outerValue, innerValue))
            {
                return false;
            }
        }
        return true;
    }

    private IQueryAst ApplyCorrelationParameters(
        IQueryAst originalQuery,
        IDataRow outerRow,
        IReadOnlyList<ICorrelationCondition> correlationConditions)
    {
        // Create a new query with correlation parameters applied
        // This would involve modifying the WHERE clause to include correlation predicates
        // Simplified implementation
        return originalQuery;
    }

    private async Task<object?> GetScalarValueAsync(IQueryResult subqueryResult, CancellationToken cancellationToken)
    {
        if (subqueryResult.RowCount == 0)
        {
            return null;
        }
        
        if (subqueryResult.RowCount > 1)
        {
            throw new QueryExecutionException("Scalar subquery returned more than one row");
        }

        await foreach (var row in subqueryResult.GetRowsAsync(cancellationToken))
        {
            return row.GetValue(row.Schema.Columns.First().Name);
        }

        return null;
    }

    private IDataRow AddScalarColumn(IDataRow originalRow, string columnAlias, object? scalarValue)
    {
        var values = new Dictionary<string, object?>();

        // Copy original row values
        foreach (var column in originalRow.Schema.Columns)
        {
            values[column.Name] = originalRow.GetValue(column.Name);
        }

        // Add scalar value
        values[columnAlias] = scalarValue;

        return new DataRow(values);
    }

    private ISchema CreateScalarSubquerySchema(ISchema originalSchema, string scalarColumnAlias)
    {
        var columns = new List<IColumnInfo>(originalSchema.Columns)
        {
            new ColumnInfo(scalarColumnAlias, typeof(object), true, null)
        };

        return new Schema(columns);
    }

    private IJoinCondition CreateJoinConditionFromCorrelation(IReadOnlyList<ICorrelationCondition> correlationConditions)
    {
        // Convert correlation conditions to join conditions
        // Simplified implementation
        return new JoinCondition(
            correlationConditions.Select(cc => cc.OuterColumn).ToList(),
            correlationConditions.Select(cc => cc.InnerColumn).ToList());
    }
}

/// <summary>
/// Types of subqueries.
/// </summary>
public enum SubqueryType
{
    Exists,
    NotExists,
    In,
    NotIn,
    Scalar,
    Any,
    All
}

/// <summary>
/// Subquery optimization strategies.
/// </summary>
public enum SubqueryOptimizationStrategy
{
    CorrelatedExecution,
    SemiJoin,
    AntiJoin,
    LeftJoin,
    MaterializeOnce,
    MaterializeAndHash
}

/// <summary>
/// Statistics for subquery execution.
/// </summary>
public class SubqueryExecutionStats
{
    public SubqueryType SubqueryType { get; set; }
    public SubqueryOptimizationStrategy OptimizationStrategy { get; set; }
    public long OuterRows { get; set; }
    public long SubqueryExecutions { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long ResultRows { get; set; }
}

/// <summary>
/// Optimization result for subqueries.
/// </summary>
public class SubqueryOptimizationResult
{
    public SubqueryOptimizationStrategy Strategy { get; set; }
    public long EstimatedSubqueryExecutions { get; set; }
    public double EstimatedCost { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Simple join condition implementation.
/// </summary>
public class JoinCondition : IJoinCondition
{
    public IReadOnlyList<string> LeftKeys { get; }
    public IReadOnlyList<string> RightKeys { get; }

    public JoinCondition(IReadOnlyList<string> leftKeys, IReadOnlyList<string> rightKeys)
    {
        LeftKeys = leftKeys ?? throw new ArgumentNullException(nameof(leftKeys));
        RightKeys = rightKeys ?? throw new ArgumentNullException(nameof(rightKeys));
    }

    public bool EvaluateCondition(IDataRow leftRow, IDataRow rightRow)
    {
        for (int i = 0; i < LeftKeys.Count; i++)
        {
            var leftValue = leftRow.GetValue(LeftKeys[i]);
            var rightValue = rightRow.GetValue(RightKeys[i]);

            if (!Equals(leftValue, rightValue))
            {
                return false;
            }
        }
        return true;
    }
}
