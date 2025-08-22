using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

#region Join Interfaces

/// <summary>
/// Interface for join conditions.
/// </summary>
public interface IJoinCondition
{
    IReadOnlyList<string> LeftKeys { get; }
    IReadOnlyList<string> RightKeys { get; }
    bool EvaluateCondition(IDataRow leftRow, IDataRow rightRow);
}

/// <summary>
/// Interface for collecting join execution statistics.
/// </summary>
public interface IJoinStatisticsCollector
{
    Task RecordJoinExecutionAsync(JoinExecutionStats stats);
}

/// <summary>
/// Statistics for join execution.
/// </summary>
public class JoinExecutionStats
{
    public JoinType JoinType { get; set; }
    public JoinAlgorithm Algorithm { get; set; }
    public long LeftRows { get; set; }
    public long RightRows { get; set; }
    public long ResultRows { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsed { get; set; }
}

#endregion

#region Aggregation Interfaces

/// <summary>
/// Interface for window functions.
/// </summary>
public interface IWindowFunction
{
    string Alias { get; }
    WindowFunctionType FunctionType { get; }
    IReadOnlyList<string> PartitionBy { get; }
    IReadOnlyList<IOrderByClause> OrderBy { get; }
    string ColumnName { get; }
    int? Offset { get; }
    object? DefaultValue { get; }
}

/// <summary>
/// Interface for aggregate functions.
/// </summary>
public interface IAggregateFunction
{
    string Alias { get; }
    AggregateFunctionType FunctionType { get; }
    string ColumnName { get; }
    object? CreateInitialState();
    object? ProcessValue(object? currentState, IDataRow row);
    object? GetResult(object? state);
}

/// <summary>
/// Interface for analytical functions.
/// </summary>
public interface IAnalyticalFunction
{
    string Alias { get; }
    AnalyticalFunctionType FunctionType { get; }
    IReadOnlyList<IOrderByClause> OrderBy { get; }
}

/// <summary>
/// Interface for ORDER BY clauses.
/// </summary>
public interface IOrderByClause
{
    string ColumnName { get; }
    bool IsDescending { get; }
}

/// <summary>
/// Interface for collecting aggregation execution statistics.
/// </summary>
public interface IAggregationStatisticsCollector
{
    Task RecordAggregationExecutionAsync(AggregationExecutionStats stats);
}

/// <summary>
/// Statistics for aggregation execution.
/// </summary>
public class AggregationExecutionStats
{
    public AggregationType AggregationType { get; set; }
    public long InputRows { get; set; }
    public long OutputRows { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public int PartitionCount { get; set; }
    public int GroupCount { get; set; }
    public int FunctionCount { get; set; }
    public int GroupingSetCount { get; set; }
}

/// <summary>
/// Aggregate function types.
/// </summary>
public enum AggregateFunctionType
{
    Count,
    Sum,
    Avg,
    Min,
    Max,
    StdDev,
    Variance
}

#endregion

#region Subquery Interfaces

/// <summary>
/// Interface for subquery expressions.
/// </summary>
public interface ISubqueryExpression
{
    IQueryAst Query { get; }
    IReadOnlyList<ICorrelationCondition> CorrelationConditions { get; }
    string ResultColumn { get; }
}

/// <summary>
/// Interface for correlation conditions in subqueries.
/// </summary>
public interface ICorrelationCondition
{
    string OuterColumn { get; }
    string InnerColumn { get; }
    ComparisonOperator Operator { get; }
}

/// <summary>
/// Interface for subquery optimization.
/// </summary>
public interface ISubqueryOptimizer
{
    Task<SubqueryOptimizationResult> OptimizeExistsSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default);
    Task<SubqueryOptimizationResult> OptimizeInSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, string outerColumn, CancellationToken cancellationToken = default);
    Task<SubqueryOptimizationResult> OptimizeScalarSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default);
    Task<SubqueryOptimizationResult> OptimizeNotExistsSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for collecting subquery execution statistics.
/// </summary>
public interface ISubqueryStatisticsCollector
{
    Task RecordSubqueryExecutionAsync(SubqueryExecutionStats stats);
}

/// <summary>
/// Comparison operators for correlation conditions.
/// </summary>
public enum ComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual
}

#endregion

#region Full-Text Search Interfaces

/// <summary>
/// Interface for text index providers.
/// </summary>
public interface ITextIndexProvider
{
    Task<bool> HasTextIndexAsync(string column, CancellationToken cancellationToken = default);
    Task<ITextIndex> GetTextIndexAsync(string column, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for text indexes.
/// </summary>
public interface ITextIndex
{
    Task<IEnumerable<long>> FindRowsContainingTermAsync(string term, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for text analyzers.
/// </summary>
public interface ITextAnalyzer
{
    Task<IAnalyzedQuery> AnalyzeQueryAsync(string query, CancellationToken cancellationToken = default);
    Task<IAnalyzedQuery> AnalyzePhraseAsync(string phrase, CancellationToken cancellationToken = default);
    List<string> ExtractTerms(string text);
}

/// <summary>
/// Interface for analyzed queries.
/// </summary>
public interface IAnalyzedQuery
{
    List<QueryTerm> Terms { get; }
}

/// <summary>
/// Interface for boolean queries.
/// </summary>
public interface IBooleanQuery
{
    bool ContainsTerm(string term);
    int GetTermCount();
}

/// <summary>
/// Interface for collecting full-text search statistics.
/// </summary>
public interface IFullTextStatisticsCollector
{
    Task RecordFullTextSearchAsync(FullTextSearchStats stats);
}

/// <summary>
/// Query term with metadata.
/// </summary>
public class QueryTerm
{
    public string Text { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public int DocumentFrequency { get; set; } = 1;
}

#endregion

#region Query Execution Context

/// <summary>
/// Interface for query execution context.
/// </summary>
public interface IQueryExecutionContext
{
    Task<IQueryResult> ExecuteQueryAsync(IQueryAst query, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDataRow>> FindRowsByIndexAsync(IIndexInfo index, object keyValue, CancellationToken cancellationToken = default);
}

#endregion





#region Simple Implementations

/// <summary>
/// Simple ORDER BY clause implementation.
/// </summary>
public class OrderByClause : IOrderByClause
{
    public string ColumnName { get; }
    public bool IsDescending { get; }

    public OrderByClause(string columnName, bool isDescending = false)
    {
        ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        IsDescending = isDescending;
    }
}

/// <summary>
/// Simple correlation condition implementation.
/// </summary>
public class CorrelationCondition : ICorrelationCondition
{
    public string OuterColumn { get; }
    public string InnerColumn { get; }
    public ComparisonOperator Operator { get; }

    public CorrelationCondition(string outerColumn, string innerColumn, ComparisonOperator op = ComparisonOperator.Equal)
    {
        OuterColumn = outerColumn ?? throw new ArgumentNullException(nameof(outerColumn));
        InnerColumn = innerColumn ?? throw new ArgumentNullException(nameof(innerColumn));
        Operator = op;
    }
}

/// <summary>
/// Simple analyzed query implementation.
/// </summary>
public class AnalyzedQuery : IAnalyzedQuery
{
    public List<QueryTerm> Terms { get; set; } = new();
}

/// <summary>
/// Extension method to get values from composite keys.
/// </summary>
public static class CompositeKeyExtensions
{
    public static object?[] GetValues(this CompositeKey compositeKey)
    {
        // Use reflection to access private field
        var field = typeof(CompositeKey).GetField("_values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (object?[])(field?.GetValue(compositeKey) ?? Array.Empty<object>());
    }
}

#endregion
