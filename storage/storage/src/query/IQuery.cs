using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NebulaStore.Storage.Embedded.Query;

/// <summary>
/// Base interface for all queries.
/// </summary>
public interface IQuery
{
    /// <summary>
    /// Gets the query ID.
    /// </summary>
    string QueryId { get; }

    /// <summary>
    /// Gets the query type.
    /// </summary>
    QueryType Type { get; }

    /// <summary>
    /// Gets the target entity type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Gets the query creation timestamp.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the estimated complexity of the query.
    /// </summary>
    QueryComplexity Complexity { get; }
}

/// <summary>
/// Generic interface for typed queries.
/// </summary>
/// <typeparam name="T">Type of query results</typeparam>
public interface IQuery<T> : IQuery
{
    /// <summary>
    /// Gets the result type.
    /// </summary>
    Type ResultType { get; }
}

/// <summary>
/// Interface for queries with filtering capabilities.
/// </summary>
/// <typeparam name="T">Type of entities being queried</typeparam>
public interface IFilterableQuery<T> : IQuery<T>
{
    /// <summary>
    /// Gets the filter expressions.
    /// </summary>
    IReadOnlyList<Expression<Func<T, bool>>> Filters { get; }

    /// <summary>
    /// Adds a filter to the query.
    /// </summary>
    /// <param name="filter">Filter expression</param>
    /// <returns>New query with the filter applied</returns>
    IFilterableQuery<T> Where(Expression<Func<T, bool>> filter);
}

/// <summary>
/// Interface for queries with ordering capabilities.
/// </summary>
/// <typeparam name="T">Type of entities being queried</typeparam>
public interface IOrderableQuery<T> : IQuery<T>
{
    /// <summary>
    /// Gets the ordering expressions.
    /// </summary>
    IReadOnlyList<OrderExpression<T>> OrderBy { get; }

    /// <summary>
    /// Adds ascending ordering to the query.
    /// </summary>
    /// <typeparam name="TKey">Type of the ordering key</typeparam>
    /// <param name="keySelector">Key selector expression</param>
    /// <returns>New query with ordering applied</returns>
    IOrderableQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Adds descending ordering to the query.
    /// </summary>
    /// <typeparam name="TKey">Type of the ordering key</typeparam>
    /// <param name="keySelector">Key selector expression</param>
    /// <returns>New query with ordering applied</returns>
    IOrderableQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
}

/// <summary>
/// Interface for queries with pagination capabilities.
/// </summary>
/// <typeparam name="T">Type of entities being queried</typeparam>
public interface IPaginableQuery<T> : IQuery<T>
{
    /// <summary>
    /// Gets the skip count.
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Gets the take count.
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Skips the specified number of elements.
    /// </summary>
    /// <param name="count">Number of elements to skip</param>
    /// <returns>New query with skip applied</returns>
    IPaginableQuery<T> Skip(int count);

    /// <summary>
    /// Takes the specified number of elements.
    /// </summary>
    /// <param name="count">Number of elements to take</param>
    /// <returns>New query with take applied</returns>
    IPaginableQuery<T> Take(int count);
}

/// <summary>
/// Interface for queries with projection capabilities.
/// </summary>
/// <typeparam name="T">Type of source entities</typeparam>
public interface IProjectableQuery<T> : IQuery<T>
{
    /// <summary>
    /// Projects the query results to a different type.
    /// </summary>
    /// <typeparam name="TResult">Type of projected results</typeparam>
    /// <param name="selector">Projection expression</param>
    /// <returns>New query with projection applied</returns>
    IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
}

/// <summary>
/// Interface for query execution context.
/// </summary>
public interface IQueryExecutionContext
{
    /// <summary>
    /// Gets the storage context.
    /// </summary>
    object StorageContext { get; }

    /// <summary>
    /// Gets the index manager.
    /// </summary>
    object IndexManager { get; }

    /// <summary>
    /// Gets the cache manager.
    /// </summary>
    object CacheManager { get; }

    /// <summary>
    /// Gets the execution parameters.
    /// </summary>
    IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    System.Threading.CancellationToken CancellationToken { get; }
}

/// <summary>
/// Enumeration of query types.
/// </summary>
public enum QueryType
{
    /// <summary>
    /// Select query for retrieving data.
    /// </summary>
    Select,

    /// <summary>
    /// Insert query for adding data.
    /// </summary>
    Insert,

    /// <summary>
    /// Update query for modifying data.
    /// </summary>
    Update,

    /// <summary>
    /// Delete query for removing data.
    /// </summary>
    Delete,

    /// <summary>
    /// Count query for counting records.
    /// </summary>
    Count,

    /// <summary>
    /// Exists query for checking existence.
    /// </summary>
    Exists,

    /// <summary>
    /// Aggregate query for calculations.
    /// </summary>
    Aggregate
}

/// <summary>
/// Enumeration of query complexity levels.
/// </summary>
public enum QueryComplexity
{
    /// <summary>
    /// Simple query with basic operations.
    /// </summary>
    Simple,

    /// <summary>
    /// Moderate query with some complexity.
    /// </summary>
    Moderate,

    /// <summary>
    /// Complex query with multiple operations.
    /// </summary>
    Complex,

    /// <summary>
    /// Very complex query requiring optimization.
    /// </summary>
    VeryComplex
}

/// <summary>
/// Represents an ordering expression.
/// </summary>
/// <typeparam name="T">Type of entity being ordered</typeparam>
public class OrderExpression<T>
{
    public OrderExpression(Expression expression, bool ascending)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Ascending = ascending;
    }

    /// <summary>
    /// Gets the ordering expression.
    /// </summary>
    public Expression Expression { get; }

    /// <summary>
    /// Gets whether the ordering is ascending.
    /// </summary>
    public bool Ascending { get; }

    /// <summary>
    /// Gets whether the ordering is descending.
    /// </summary>
    public bool Descending => !Ascending;

    public override string ToString()
    {
        return $"{Expression} {(Ascending ? "ASC" : "DESC")}";
    }
}

/// <summary>
/// Configuration for query optimization.
/// </summary>
public class QueryOptimizationConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable query plan caching.
    /// </summary>
    public bool EnablePlanCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of cached plans.
    /// </summary>
    public int MaxCachedPlans { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the plan cache TTL.
    /// </summary>
    public TimeSpan PlanCacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether to enable result caching.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of cached results.
    /// </summary>
    public int MaxCachedResults { get; set; } = 500;

    /// <summary>
    /// Gets or sets the result cache TTL.
    /// </summary>
    public TimeSpan ResultCacheTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets whether to enable index usage optimization.
    /// </summary>
    public bool EnableIndexOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable parallel execution.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the parallel execution threshold.
    /// </summary>
    public int ParallelExecutionThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return MaxCachedPlans > 0 &&
               PlanCacheTtl > TimeSpan.Zero &&
               MaxCachedResults > 0 &&
               ResultCacheTtl > TimeSpan.Zero &&
               ParallelExecutionThreshold > 0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public QueryOptimizationConfiguration Clone()
    {
        return new QueryOptimizationConfiguration
        {
            EnablePlanCaching = EnablePlanCaching,
            MaxCachedPlans = MaxCachedPlans,
            PlanCacheTtl = PlanCacheTtl,
            EnableResultCaching = EnableResultCaching,
            MaxCachedResults = MaxCachedResults,
            ResultCacheTtl = ResultCacheTtl,
            EnableIndexOptimization = EnableIndexOptimization,
            EnableParallelExecution = EnableParallelExecution,
            ParallelExecutionThreshold = ParallelExecutionThreshold,
            EnableStatistics = EnableStatistics
        };
    }

    public override string ToString()
    {
        return $"QueryOptimizationConfiguration[PlanCaching={EnablePlanCaching} (Max={MaxCachedPlans}, TTL={PlanCacheTtl}), " +
               $"ResultCaching={EnableResultCaching} (Max={MaxCachedResults}, TTL={ResultCacheTtl}), " +
               $"IndexOptimization={EnableIndexOptimization}, ParallelExecution={EnableParallelExecution}, " +
               $"Statistics={EnableStatistics}]";
    }
}
