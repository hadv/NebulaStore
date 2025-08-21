using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query;

/// <summary>
/// Interface for query optimization and execution planning.
/// </summary>
public interface IQueryOptimizer : IDisposable
{
    /// <summary>
    /// Gets the optimizer name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets query optimization statistics.
    /// </summary>
    IQueryOptimizerStatistics Statistics { get; }

    /// <summary>
    /// Optimizes a query and creates an execution plan.
    /// </summary>
    /// <param name="query">Query to optimize</param>
    /// <returns>Optimized execution plan</returns>
    IQueryExecutionPlan OptimizeQuery(IQuery query);

    /// <summary>
    /// Executes a query with optimization.
    /// </summary>
    /// <typeparam name="T">Type of query results</typeparam>
    /// <param name="query">Query to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query results</returns>
    Task<IQueryResult<T>> ExecuteQueryAsync<T>(IQuery<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes query patterns and updates optimization strategies.
    /// </summary>
    /// <param name="queries">Historical queries to analyze</param>
    void AnalyzeQueryPatterns(IEnumerable<IQuery> queries);

    /// <summary>
    /// Gets optimization suggestions for a query.
    /// </summary>
    /// <param name="query">Query to analyze</param>
    /// <returns>Optimization suggestions</returns>
    IEnumerable<QueryOptimizationSuggestion> GetOptimizationSuggestions(IQuery query);

    /// <summary>
    /// Clears the query plan cache.
    /// </summary>
    void ClearPlanCache();
}

/// <summary>
/// Interface for query execution plans.
/// </summary>
public interface IQueryExecutionPlan
{
    /// <summary>
    /// Gets the plan ID.
    /// </summary>
    string PlanId { get; }

    /// <summary>
    /// Gets the original query.
    /// </summary>
    IQuery Query { get; }

    /// <summary>
    /// Gets the execution steps.
    /// </summary>
    IReadOnlyList<IExecutionStep> Steps { get; }

    /// <summary>
    /// Gets the estimated cost of execution.
    /// </summary>
    double EstimatedCost { get; }

    /// <summary>
    /// Gets the estimated execution time in milliseconds.
    /// </summary>
    double EstimatedExecutionTimeMs { get; }

    /// <summary>
    /// Gets whether the plan uses indexes.
    /// </summary>
    bool UsesIndexes { get; }

    /// <summary>
    /// Gets the plan creation timestamp.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Executes the plan.
    /// </summary>
    /// <typeparam name="T">Type of results</typeparam>
    /// <param name="context">Execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution results</returns>
    Task<IQueryResult<T>> ExecuteAsync<T>(IQueryExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for query execution steps.
/// </summary>
public interface IExecutionStep
{
    /// <summary>
    /// Gets the step type.
    /// </summary>
    ExecutionStepType Type { get; }

    /// <summary>
    /// Gets the step description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the estimated cost of this step.
    /// </summary>
    double EstimatedCost { get; }

    /// <summary>
    /// Gets the estimated rows processed by this step.
    /// </summary>
    long EstimatedRows { get; }

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="context">Execution context</param>
    /// <param name="input">Input data from previous step</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Step results</returns>
    Task<IStepResult> ExecuteAsync(IQueryExecutionContext context, IStepResult? input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for query results.
/// </summary>
/// <typeparam name="T">Type of result items</typeparam>
public interface IQueryResult<T> : IDisposable
{
    /// <summary>
    /// Gets the result items.
    /// </summary>
    IEnumerable<T> Items { get; }

    /// <summary>
    /// Gets the total count of items.
    /// </summary>
    long TotalCount { get; }

    /// <summary>
    /// Gets whether there are more results available.
    /// </summary>
    bool HasMore { get; }

    /// <summary>
    /// Gets the execution statistics.
    /// </summary>
    QueryExecutionStatistics ExecutionStatistics { get; }

    /// <summary>
    /// Gets the next page of results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next page of results</returns>
    Task<IQueryResult<T>> GetNextPageAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for step execution results.
/// </summary>
public interface IStepResult : IDisposable
{
    /// <summary>
    /// Gets the result data.
    /// </summary>
    object? Data { get; }

    /// <summary>
    /// Gets the number of rows processed.
    /// </summary>
    long RowsProcessed { get; }

    /// <summary>
    /// Gets the execution time.
    /// </summary>
    TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets whether the step was successful.
    /// </summary>
    bool IsSuccessful { get; }

    /// <summary>
    /// Gets any error that occurred.
    /// </summary>
    Exception? Error { get; }
}

/// <summary>
/// Interface for query optimizer statistics.
/// </summary>
public interface IQueryOptimizerStatistics
{
    /// <summary>
    /// Gets the total number of queries optimized.
    /// </summary>
    long TotalQueriesOptimized { get; }

    /// <summary>
    /// Gets the total number of queries executed.
    /// </summary>
    long TotalQueriesExecuted { get; }

    /// <summary>
    /// Gets the cache hit ratio for execution plans.
    /// </summary>
    double PlanCacheHitRatio { get; }

    /// <summary>
    /// Gets the average optimization time in microseconds.
    /// </summary>
    double AverageOptimizationTimeMicroseconds { get; }

    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    double AverageExecutionTimeMs { get; }

    /// <summary>
    /// Gets the percentage of queries that use indexes.
    /// </summary>
    double IndexUsagePercentage { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Enumeration of execution step types.
/// </summary>
public enum ExecutionStepType
{
    /// <summary>
    /// Table scan operation.
    /// </summary>
    TableScan,

    /// <summary>
    /// Index seek operation.
    /// </summary>
    IndexSeek,

    /// <summary>
    /// Index scan operation.
    /// </summary>
    IndexScan,

    /// <summary>
    /// Filter operation.
    /// </summary>
    Filter,

    /// <summary>
    /// Sort operation.
    /// </summary>
    Sort,

    /// <summary>
    /// Join operation.
    /// </summary>
    Join,

    /// <summary>
    /// Aggregation operation.
    /// </summary>
    Aggregate,

    /// <summary>
    /// Projection operation.
    /// </summary>
    Project,

    /// <summary>
    /// Limit operation.
    /// </summary>
    Limit
}

/// <summary>
/// Query optimization suggestion.
/// </summary>
public class QueryOptimizationSuggestion
{
    public QueryOptimizationSuggestion(
        string type,
        string description,
        double potentialImprovement,
        string recommendation)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        PotentialImprovement = potentialImprovement;
        Recommendation = recommendation ?? throw new ArgumentNullException(nameof(recommendation));
    }

    /// <summary>
    /// Gets the suggestion type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the suggestion description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the potential performance improvement (0.0 to 1.0).
    /// </summary>
    public double PotentialImprovement { get; }

    /// <summary>
    /// Gets the recommendation text.
    /// </summary>
    public string Recommendation { get; }

    public override string ToString()
    {
        return $"{Type}: {Description} (Potential improvement: {PotentialImprovement:P1}) - {Recommendation}";
    }
}

/// <summary>
/// Query execution statistics.
/// </summary>
public class QueryExecutionStatistics
{
    public QueryExecutionStatistics(
        TimeSpan totalExecutionTime,
        TimeSpan optimizationTime,
        long rowsProcessed,
        long rowsReturned,
        bool usedCache,
        bool usedIndexes,
        int stepsExecuted)
    {
        TotalExecutionTime = totalExecutionTime;
        OptimizationTime = optimizationTime;
        RowsProcessed = rowsProcessed;
        RowsReturned = rowsReturned;
        UsedCache = usedCache;
        UsedIndexes = usedIndexes;
        StepsExecuted = stepsExecuted;
    }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the optimization time.
    /// </summary>
    public TimeSpan OptimizationTime { get; }

    /// <summary>
    /// Gets the number of rows processed.
    /// </summary>
    public long RowsProcessed { get; }

    /// <summary>
    /// Gets the number of rows returned.
    /// </summary>
    public long RowsReturned { get; }

    /// <summary>
    /// Gets whether the query used cache.
    /// </summary>
    public bool UsedCache { get; }

    /// <summary>
    /// Gets whether the query used indexes.
    /// </summary>
    public bool UsedIndexes { get; }

    /// <summary>
    /// Gets the number of execution steps.
    /// </summary>
    public int StepsExecuted { get; }

    /// <summary>
    /// Gets the selectivity ratio.
    /// </summary>
    public double Selectivity => RowsProcessed > 0 ? (double)RowsReturned / RowsProcessed : 0.0;

    public override string ToString()
    {
        return $"Query Execution: {TotalExecutionTime.TotalMilliseconds:F1}ms total " +
               $"({OptimizationTime.TotalMicroseconds:F0}μs optimization), " +
               $"Processed={RowsProcessed:N0}, Returned={RowsReturned:N0} " +
               $"(Selectivity={Selectivity:P2}), Steps={StepsExecuted}, " +
               $"Cache={UsedCache}, Indexes={UsedIndexes}";
    }
}
