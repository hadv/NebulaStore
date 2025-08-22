using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Query.Advanced;

namespace NebulaStore.Storage.Embedded.Query.Execution;

/// <summary>
/// Main query execution engine that orchestrates the execution of optimized query plans.
/// Provides operator pipeline execution, memory management, and result streaming.
/// </summary>
public class QueryExecutionEngine : IQueryExecutionEngine
{
    private readonly IStorageEngine _storageEngine;
    private readonly IQueryOptimizer _queryOptimizer;
    private readonly IExecutionMemoryManager _memoryManager;
    private readonly IExecutionStatisticsCollector _statisticsCollector;
    private readonly QueryExecutionOptions _options;

    public QueryExecutionEngine(
        IStorageEngine storageEngine,
        IQueryOptimizer queryOptimizer,
        IExecutionMemoryManager memoryManager,
        IExecutionStatisticsCollector statisticsCollector,
        QueryExecutionOptions? options = null)
    {
        _storageEngine = storageEngine ?? throw new ArgumentNullException(nameof(storageEngine));
        _queryOptimizer = queryOptimizer ?? throw new ArgumentNullException(nameof(queryOptimizer));
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _statisticsCollector = statisticsCollector ?? throw new ArgumentNullException(nameof(statisticsCollector));
        _options = options ?? new QueryExecutionOptions();
    }

    /// <summary>
    /// Executes a query and returns results as an async enumerable.
    /// </summary>
    public async Task<IQueryExecutionResult> ExecuteQueryAsync(
        IQueryAst query,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        try
        {
            // Phase 1: Query Optimization
            var optimizedPlan = await _queryOptimizer.OptimizeAsync(query, cancellationToken);

            // Phase 2: Create Execution Context
            var executionContext = await CreateExecutionContextAsync(executionId, optimizedPlan, cancellationToken);

            // Phase 3: Build Operator Pipeline
            var rootOperator = await BuildOperatorPipelineAsync(optimizedPlan.RootOperator, executionContext, cancellationToken);

            // Phase 4: Execute Pipeline
            var resultStream = ExecuteOperatorPipelineAsync(rootOperator, executionContext, cancellationToken);

            // Phase 5: Create Execution Result
            var executionResult = new QueryExecutionResult(
                executionId,
                optimizedPlan.OutputSchema,
                resultStream,
                optimizedPlan.EstimatedCost,
                optimizedPlan.EstimatedRows,
                startTime);

            // Collect execution statistics
            await _statisticsCollector.RecordQueryExecutionStartAsync(new QueryExecutionStats
            {
                ExecutionId = executionId,
                QueryType = query.QueryType,
                EstimatedCost = optimizedPlan.EstimatedCost,
                EstimatedRows = optimizedPlan.EstimatedRows,
                StartTime = startTime
            });

            return executionResult;
        }
        catch (Exception ex)
        {
            await _statisticsCollector.RecordQueryExecutionErrorAsync(executionId, ex);
            throw new QueryExecutionException($"Query execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a pre-optimized physical plan.
    /// </summary>
    public async Task<IQueryExecutionResult> ExecutePlanAsync(
        IPhysicalPlan plan,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        try
        {
            // Create Execution Context
            var executionContext = await CreateExecutionContextAsync(executionId, plan, cancellationToken);

            // Build Operator Pipeline
            var rootOperator = await BuildOperatorPipelineAsync(plan.RootOperator, executionContext, cancellationToken);

            // Execute Pipeline
            var resultStream = ExecuteOperatorPipelineAsync(rootOperator, executionContext, cancellationToken);

            // Create Execution Result
            var executionResult = new QueryExecutionResult(
                executionId,
                plan.OutputSchema,
                resultStream,
                plan.EstimatedCost,
                plan.EstimatedRows,
                startTime);

            return executionResult;
        }
        catch (Exception ex)
        {
            await _statisticsCollector.RecordQueryExecutionErrorAsync(executionId, ex);
            throw new QueryExecutionException($"Plan execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels a running query execution.
    /// </summary>
    public async Task CancelQueryAsync(Guid executionId)
    {
        // Implementation would track active executions and cancel them
        await _statisticsCollector.RecordQueryExecutionCancelledAsync(executionId);
    }

    /// <summary>
    /// Gets execution statistics for a query.
    /// </summary>
    public async Task<IQueryExecutionStatistics> GetExecutionStatisticsAsync(Guid executionId)
    {
        return await _statisticsCollector.GetExecutionStatisticsAsync(executionId);
    }

    // Private implementation methods

    private async Task<IQueryExecutionContext> CreateExecutionContextAsync(
        Guid executionId,
        IPhysicalPlan plan,
        CancellationToken cancellationToken)
    {
        var memoryBudget = await _memoryManager.AllocateMemoryBudgetAsync(plan.EstimatedCost, cancellationToken);

        return new QueryExecutionContext(
            executionId,
            _storageEngine,
            _memoryManager,
            memoryBudget,
            _options,
            cancellationToken);
    }

    private async Task<IPhysicalExecutionOperator> BuildOperatorPipelineAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        return logicalOperator.OperatorType switch
        {
            PhysicalOperatorType.TableScan => await BuildTableScanOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.IndexScan => await BuildIndexScanOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.NestedLoopJoin => await BuildNestedLoopJoinOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.HashJoin => await BuildHashJoinOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.MergeJoin => await BuildMergeJoinOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.HashAggregate => await BuildHashAggregateOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.StreamAggregate => await BuildStreamAggregateOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.Sort => await BuildSortOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.Filter => await BuildFilterOperatorAsync(logicalOperator, context, cancellationToken),
            PhysicalOperatorType.Projection => await BuildProjectionOperatorAsync(logicalOperator, context, cancellationToken),
            _ => throw new NotSupportedException($"Operator type {logicalOperator.OperatorType} not supported")
        };
    }

    private async IAsyncEnumerable<IDataRow> ExecuteOperatorPipelineAsync(
        IPhysicalExecutionOperator rootOperator,
        IQueryExecutionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rowCount = 0L;
        var startTime = DateTime.UtcNow;

        try
        {
            await foreach (var row in rootOperator.ExecuteAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Apply memory pressure checks
                if (rowCount % _options.MemoryCheckInterval == 0)
                {
                    await _memoryManager.CheckMemoryPressureAsync(context.MemoryBudget, cancellationToken);
                }

                // Apply row limit if specified
                if (_options.MaxRowsPerQuery.HasValue && rowCount >= _options.MaxRowsPerQuery.Value)
                {
                    throw new QueryExecutionException($"Query exceeded maximum row limit of {_options.MaxRowsPerQuery.Value}");
                }

                rowCount++;
                yield return row;
            }

            // Record successful completion
            await _statisticsCollector.RecordQueryExecutionCompletedAsync(new QueryExecutionCompletionStats
            {
                ExecutionId = context.ExecutionId,
                ActualRows = rowCount,
                ExecutionTime = DateTime.UtcNow - startTime,
                MemoryUsed = context.MemoryBudget.UsedMemory
            });
        }
        finally
        {
            // Cleanup resources
            await rootOperator.DisposeAsync();
            await _memoryManager.ReleaseMemoryBudgetAsync(context.MemoryBudget);
        }
    }

    // Operator builder methods

    private async Task<IPhysicalExecutionOperator> BuildTableScanOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var tableName = logicalOperator.Properties.TryGetValue("TableName", out var name) ? name?.ToString() : null;
        if (string.IsNullOrEmpty(tableName))
            throw new QueryExecutionException("TableScan operator missing TableName property");

        return new TableScanExecutionOperator(tableName, _storageEngine, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildIndexScanOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var indexName = logicalOperator.Properties.TryGetValue("IndexName", out var name) ? name?.ToString() : null;
        var tableName = logicalOperator.Properties.TryGetValue("TableName", out var table) ? table?.ToString() : null;

        if (string.IsNullOrEmpty(indexName) || string.IsNullOrEmpty(tableName))
            throw new QueryExecutionException("IndexScan operator missing IndexName or TableName property");

        return new IndexScanExecutionOperator(indexName, tableName, _storageEngine, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildNestedLoopJoinOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var leftChild = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        var rightChild = await BuildOperatorPipelineAsync(logicalOperator.Children[1], context, cancellationToken);

        return new NestedLoopJoinExecutionOperator(leftChild, rightChild, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildHashJoinOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var leftChild = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        var rightChild = await BuildOperatorPipelineAsync(logicalOperator.Children[1], context, cancellationToken);

        return new HashJoinExecutionOperator(leftChild, rightChild, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildMergeJoinOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var leftChild = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        var rightChild = await BuildOperatorPipelineAsync(logicalOperator.Children[1], context, cancellationToken);

        return new MergeJoinExecutionOperator(leftChild, rightChild, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildHashAggregateOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var child = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        return new HashAggregateExecutionOperator(child, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildStreamAggregateOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var child = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        return new StreamAggregateExecutionOperator(child, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildSortOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var child = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        return new SortExecutionOperator(child, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildFilterOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var child = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        return new FilterExecutionOperator(child, context);
    }

    private async Task<IPhysicalExecutionOperator> BuildProjectionOperatorAsync(
        IPhysicalOperator logicalOperator,
        IQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var child = await BuildOperatorPipelineAsync(logicalOperator.Children[0], context, cancellationToken);
        return new ProjectionExecutionOperator(child, context);
    }
}

/// <summary>
/// Options for query execution engine.
/// </summary>
public class QueryExecutionOptions
{
    public int MemoryCheckInterval { get; set; } = 1000;
    public long? MaxRowsPerQuery { get; set; } = null;
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool EnableParallelExecution { get; set; } = true;
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public long MaxMemoryPerQuery { get; set; } = 1024 * 1024 * 1024; // 1GB
}

/// <summary>
/// Exception thrown during query execution.
/// </summary>
public class QueryExecutionException : Exception
{
    public QueryExecutionException(string message) : base(message) { }
    public QueryExecutionException(string message, Exception innerException) : base(message, innerException) { }
}
