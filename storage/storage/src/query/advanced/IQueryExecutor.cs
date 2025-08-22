using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Interface for executing parsed queries against the storage engine.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a query and returns the results.
    /// </summary>
    /// <param name="query">The parsed query AST</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query execution result</returns>
    Task<IQueryResult> ExecuteAsync(IQueryAst query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query with parameters and returns the results.
    /// </summary>
    /// <param name="query">The parsed query AST</param>
    /// <param name="parameters">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query execution result</returns>
    Task<IQueryResult> ExecuteAsync(IQueryAst query, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explains the execution plan for a query without executing it.
    /// </summary>
    /// <param name="query">The parsed query AST</param>
    /// <returns>Query execution plan</returns>
    Task<IQueryExecutionPlan> ExplainAsync(IQueryAst query);

    /// <summary>
    /// Gets the supported query features for this executor.
    /// </summary>
    QueryLanguageFeatures SupportedFeatures { get; }
}

/// <summary>
/// Represents the result of a query execution.
/// </summary>
public interface IQueryResult : IDisposable
{
    /// <summary>
    /// Gets the result type.
    /// </summary>
    QueryResultType ResultType { get; }

    /// <summary>
    /// Gets the number of rows affected (for DML operations).
    /// </summary>
    int RowsAffected { get; }

    /// <summary>
    /// Gets the column metadata (for SELECT queries).
    /// </summary>
    IReadOnlyList<IColumnMetadata> Columns { get; }

    /// <summary>
    /// Gets the result rows (for SELECT queries).
    /// </summary>
    IAsyncEnumerable<IDataRow> Rows { get; }

    /// <summary>
    /// Gets the execution statistics.
    /// </summary>
    IQueryExecutionStatistics Statistics { get; }

    /// <summary>
    /// Gets any warnings generated during execution.
    /// </summary>
    IReadOnlyList<QueryWarning> Warnings { get; }
}

/// <summary>
/// Represents a query execution plan.
/// </summary>
public interface IQueryExecutionPlan
{
    /// <summary>
    /// Gets the root operator of the execution plan.
    /// </summary>
    IQueryOperator RootOperator { get; }

    /// <summary>
    /// Gets the estimated cost of the plan.
    /// </summary>
    double EstimatedCost { get; }

    /// <summary>
    /// Gets the estimated number of rows.
    /// </summary>
    long EstimatedRows { get; }

    /// <summary>
    /// Gets the plan as a human-readable string.
    /// </summary>
    string PlanText { get; }

    /// <summary>
    /// Gets the plan as a tree structure.
    /// </summary>
    IQueryPlanNode PlanTree { get; }
}

/// <summary>
/// Represents a query operator in the execution plan.
/// </summary>
public interface IQueryOperator
{
    /// <summary>
    /// Gets the operator type.
    /// </summary>
    QueryOperatorType OperatorType { get; }

    /// <summary>
    /// Gets the operator name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the child operators.
    /// </summary>
    IReadOnlyList<IQueryOperator> Children { get; }

    /// <summary>
    /// Gets the operator properties.
    /// </summary>
    IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets the estimated cost.
    /// </summary>
    double EstimatedCost { get; }

    /// <summary>
    /// Gets the estimated number of rows.
    /// </summary>
    long EstimatedRows { get; }
}

/// <summary>
/// Represents a node in the query plan tree.
/// </summary>
public interface IQueryPlanNode
{
    /// <summary>
    /// Gets the node operator.
    /// </summary>
    IQueryOperator Operator { get; }

    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    IReadOnlyList<IQueryPlanNode> Children { get; }

    /// <summary>
    /// Gets the node depth in the tree.
    /// </summary>
    int Depth { get; }
}

/// <summary>
/// Represents column metadata.
/// </summary>
public interface IColumnMetadata
{
    /// <summary>
    /// Gets the column name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the column type.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets whether the column allows null values.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Gets the column alias if any.
    /// </summary>
    string? Alias { get; }

    /// <summary>
    /// Gets the source table name.
    /// </summary>
    string? TableName { get; }
}

/// <summary>
/// Represents a data row in query results.
/// </summary>
public interface IDataRow
{
    /// <summary>
    /// Gets the number of columns in the row.
    /// </summary>
    int ColumnCount { get; }

    /// <summary>
    /// Gets the value at the specified column index.
    /// </summary>
    /// <param name="index">Column index</param>
    /// <returns>Column value</returns>
    object? this[int index] { get; }

    /// <summary>
    /// Gets the value for the specified column name.
    /// </summary>
    /// <param name="columnName">Column name</param>
    /// <returns>Column value</returns>
    object? this[string columnName] { get; }

    /// <summary>
    /// Gets all values in the row.
    /// </summary>
    /// <returns>Array of column values</returns>
    object?[] GetValues();

    /// <summary>
    /// Checks if the specified column is null.
    /// </summary>
    /// <param name="index">Column index</param>
    /// <returns>True if the column is null</returns>
    bool IsNull(int index);

    /// <summary>
    /// Checks if the specified column is null.
    /// </summary>
    /// <param name="columnName">Column name</param>
    /// <returns>True if the column is null</returns>
    bool IsNull(string columnName);
}

/// <summary>
/// Represents query execution statistics.
/// </summary>
public interface IQueryExecutionStatistics
{
    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets the compilation time.
    /// </summary>
    TimeSpan CompilationTime { get; }

    /// <summary>
    /// Gets the number of rows processed.
    /// </summary>
    long RowsProcessed { get; }

    /// <summary>
    /// Gets the number of logical reads.
    /// </summary>
    long LogicalReads { get; }

    /// <summary>
    /// Gets the number of physical reads.
    /// </summary>
    long PhysicalReads { get; }

    /// <summary>
    /// Gets the peak memory usage.
    /// </summary>
    long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets the CPU time used.
    /// </summary>
    TimeSpan CpuTime { get; }
}

/// <summary>
/// Query result types.
/// </summary>
public enum QueryResultType
{
    /// <summary>
    /// SELECT query result with rows.
    /// </summary>
    Rows,

    /// <summary>
    /// DML operation result (INSERT, UPDATE, DELETE).
    /// </summary>
    RowsAffected,

    /// <summary>
    /// DDL operation result (CREATE, DROP, ALTER).
    /// </summary>
    Schema,

    /// <summary>
    /// Empty result.
    /// </summary>
    Empty
}

/// <summary>
/// Query operator types.
/// </summary>
public enum QueryOperatorType
{
    // Scan operators
    TableScan,
    IndexScan,
    IndexSeek,

    // Join operators
    NestedLoopJoin,
    HashJoin,
    MergeJoin,

    // Aggregation operators
    HashAggregate,
    StreamAggregate,
    GroupBy,

    // Sorting operators
    Sort,
    TopN,

    // Filtering operators
    Filter,
    Select,

    // Other operators
    Projection,
    Union,
    Intersect,
    Except,
    Distinct,
    Limit,
    Subquery,
    WindowFunction
}
