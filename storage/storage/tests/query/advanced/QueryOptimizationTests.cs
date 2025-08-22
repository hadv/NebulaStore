using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Query.Advanced;

namespace NebulaStore.Storage.Tests.Query.Advanced;

/// <summary>
/// Unit tests for the query optimization engine.
/// </summary>
public class QueryOptimizationTests
{
    private readonly CostBasedOptimizer _optimizer;
    private readonly MockStatisticsProvider _statisticsProvider;
    private readonly MockIndexProvider _indexProvider;
    private readonly DefaultCostModel _costModel;

    public QueryOptimizationTests()
    {
        _statisticsProvider = new MockStatisticsProvider();
        _indexProvider = new MockIndexProvider();
        _costModel = new DefaultCostModel();
        _optimizer = new CostBasedOptimizer(_statisticsProvider, _indexProvider, _costModel);
    }

    [Fact]
    public async Task OptimizeAsync_SimpleSelectQuery_ShouldGenerateExecutionPlan()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT id, name FROM users WHERE age > 25");

        // Act
        var plan = await _optimizer.OptimizeAsync(query);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.EstimatedCost > 0);
        Assert.True(plan.EstimatedRows > 0);
        Assert.NotNull(plan.RootOperator);
        Assert.NotEmpty(plan.PlanText);
    }

    [Fact]
    public async Task OptimizeAsync_JoinQuery_ShouldConsiderJoinAlgorithms()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT u.name, p.title FROM users u JOIN posts p ON u.id = p.user_id");

        // Setup mock statistics for join optimization
        _statisticsProvider.SetTableStatistics("users", new MockTableStatistics("users", 10000, 100));
        _statisticsProvider.SetTableStatistics("posts", new MockTableStatistics("posts", 50000, 150));

        // Act
        var plan = await _optimizer.OptimizeAsync(query);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.EstimatedCost > 0);
        // The plan should contain some form of join operation or table scan
        Assert.True(plan.PlanText.Contains("TableScan") || plan.PlanText.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OptimizeAsync_AggregateQuery_ShouldChooseOptimalAggregationStrategy()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT category, COUNT(*), AVG(price) FROM products GROUP BY category");

        // Setup mock statistics
        _statisticsProvider.SetTableStatistics("products", new MockTableStatistics("products", 100000, 200));

        // Act
        var plan = await _optimizer.OptimizeAsync(query);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.EstimatedCost > 0);
        // The plan should contain some form of aggregation or table scan
        Assert.True(plan.PlanText.Contains("TableScan") || plan.PlanText.Contains("AGGREGATE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OptimizeAsync_QueryWithIndex_ShouldConsiderIndexUsage()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT * FROM users WHERE email = 'test@example.com'");

        // Setup mock index
        var emailIndex = new MockIndexInfo("idx_users_email", "users", new[] { "email" }, true, false, IndexType.BTree);
        _indexProvider.SetIndexes("users", new[] { emailIndex });
        _statisticsProvider.SetIndexStatistics("idx_users_email", new MockIndexStatistics("idx_users_email", 100, 0.001));

        // Act
        var plan = await _optimizer.OptimizeAsync(query);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.EstimatedCost > 0);
    }

    [Fact]
    public async Task CostModel_EstimateTableScanCost_ShouldReturnReasonableCost()
    {
        // Arrange
        var tableStats = new MockTableStatistics("test_table", 10000, 100);

        // Act
        var cost = _costModel.EstimateTableScanCost(tableStats);

        // Assert
        Assert.True(cost > 0);
        Assert.True(cost < 1000); // Should be reasonable for 10K rows
    }

    [Fact]
    public async Task CostModel_EstimateJoinCost_ShouldVaryByAlgorithm()
    {
        // Arrange
        long leftRows = 1000;
        long rightRows = 5000;
        double selectivity = 0.1;

        // Act
        var nestedLoopCost = _costModel.EstimateJoinCost(JoinAlgorithm.NestedLoop, leftRows, rightRows, selectivity);
        var hashJoinCost = _costModel.EstimateJoinCost(JoinAlgorithm.HashJoin, leftRows, rightRows, selectivity);
        var mergeJoinCost = _costModel.EstimateJoinCost(JoinAlgorithm.MergeJoin, leftRows, rightRows, selectivity);

        // Assert
        Assert.True(nestedLoopCost > 0);
        Assert.True(hashJoinCost > 0);
        Assert.True(mergeJoinCost > 0);
        
        // Nested loop should be most expensive for large datasets
        Assert.True(nestedLoopCost > hashJoinCost);
        Assert.True(nestedLoopCost > mergeJoinCost);
    }

    [Fact]
    public async Task ParallelOptimizer_LargeTableScan_ShouldEnableParallelism()
    {
        // Arrange
        var tableStats = new MockTableStatistics("large_table", 1000000, 200); // 1M rows
        _statisticsProvider.SetTableStatistics("large_table", tableStats);

        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT * FROM large_table WHERE status = 'active'");

        var options = new QueryOptimizerOptions { EnableParallelOptimization = true };
        var optimizer = new CostBasedOptimizer(_statisticsProvider, _indexProvider, _costModel, options);

        // Act
        var plan = await optimizer.OptimizeAsync(query);

        // Assert
        Assert.NotNull(plan);
        Assert.True(plan.EstimatedCost > 0);
    }

    [Fact]
    public void PhysicalPlanGenerator_ShouldGenerateMultipleAlternatives()
    {
        // Arrange
        var context = new QueryOptimizationContext(CreateMockQuery());
        var generator = new PhysicalPlanGenerator(_indexProvider, _statisticsProvider, _costModel, context);
        var logicalPlan = CreateMockLogicalPlan();

        // Act
        var plans = generator.GenerateAlternativePlansAsync(logicalPlan).Result;

        // Assert
        Assert.NotNull(plans);
        Assert.NotEmpty(plans);
    }

    [Fact]
    public void DefaultCostModel_EstimateIOCost_ShouldDifferentiateSequentialVsRandom()
    {
        // Arrange
        long pages = 1000;

        // Act
        var sequentialCost = _costModel.EstimateIOCost(pages, sequential: true);
        var randomCost = _costModel.EstimateIOCost(pages, sequential: false);

        // Assert
        Assert.True(sequentialCost > 0);
        Assert.True(randomCost > 0);
        Assert.True(randomCost > sequentialCost); // Random I/O should be more expensive
    }

    [Fact]
    public void DefaultCostModel_EstimateSortCost_ShouldIncreaseWithDataSize()
    {
        // Arrange & Act
        var smallSortCost = _costModel.EstimateSortCost(1000, 50);
        var largeSortCost = _costModel.EstimateSortCost(100000, 50);

        // Assert
        Assert.True(smallSortCost > 0);
        Assert.True(largeSortCost > 0);
        Assert.True(largeSortCost > smallSortCost);
    }

    [Fact]
    public void QueryOptimizationContext_ShouldStoreAndRetrieveStatistics()
    {
        // Arrange
        var query = CreateMockQuery();
        var context = new QueryOptimizationContext(query);
        var tableStats = new MockTableStatistics("test_table", 5000, 120);

        // Act
        context.TableStatistics["test_table"] = tableStats;
        var retrievedStats = context.GetTableStatistics("test_table");

        // Assert
        Assert.NotNull(retrievedStats);
        Assert.Equal(5000, retrievedStats.RowCount);
        Assert.Equal(120, retrievedStats.AverageRowSize);
    }

    // Helper methods and mock classes
    private IQueryAst CreateMockQuery()
    {
        var queryLanguage = new NebulaQueryLanguage();
        return queryLanguage.Parse("SELECT id FROM test_table");
    }

    private ILogicalPlan CreateMockLogicalPlan()
    {
        var schema = new Schema(new List<IColumnInfo>
        {
            new ColumnInfo("id", typeof(int), false, "test_table")
        });
        var rootOperator = new LogicalTableScanOperator("test_table", schema);
        return new LogicalPlan(rootOperator, schema);
    }
}

#region Mock Implementations

public class MockStatisticsProvider : IStatisticsProvider
{
    private readonly Dictionary<string, ITableStatistics> _tableStats = new();
    private readonly Dictionary<string, IColumnStatistics> _columnStats = new();
    private readonly Dictionary<string, IIndexStatistics> _indexStats = new();

    public void SetTableStatistics(string tableName, ITableStatistics stats)
    {
        _tableStats[tableName] = stats;
    }

    public void SetIndexStatistics(string indexName, IIndexStatistics stats)
    {
        _indexStats[indexName] = stats;
    }

    public Task<ITableStatistics?> GetTableStatisticsAsync(string tableName)
    {
        _tableStats.TryGetValue(tableName, out var stats);
        return Task.FromResult(stats);
    }

    public Task<IColumnStatistics?> GetColumnStatisticsAsync(string tableName, string columnName)
    {
        _columnStats.TryGetValue($"{tableName}.{columnName}", out var stats);
        return Task.FromResult(stats);
    }

    public Task<IIndexStatistics?> GetIndexStatisticsAsync(string indexName)
    {
        _indexStats.TryGetValue(indexName, out var stats);
        return Task.FromResult(stats);
    }
}

public class MockIndexProvider : IIndexProvider
{
    private readonly Dictionary<string, IReadOnlyList<IIndexInfo>> _indexes = new();

    public void SetIndexes(string tableName, IReadOnlyList<IIndexInfo> indexes)
    {
        _indexes[tableName] = indexes;
    }

    public Task<IReadOnlyList<IIndexInfo>> GetIndexesAsync(string tableName)
    {
        _indexes.TryGetValue(tableName, out var indexes);
        return Task.FromResult(indexes ?? Array.Empty<IIndexInfo>());
    }

    public Task<IIndexInfo?> GetBestIndexAsync(string tableName, IExpressionNode predicate)
    {
        var indexes = _indexes.TryGetValue(tableName, out var tableIndexes) ? tableIndexes : Array.Empty<IIndexInfo>();
        return Task.FromResult(indexes.FirstOrDefault());
    }
}

public class MockTableStatistics : ITableStatistics
{
    public string TableName { get; }
    public long RowCount { get; }
    public int AverageRowSize { get; }
    public DateTime LastUpdated { get; } = DateTime.UtcNow;

    public MockTableStatistics(string tableName, long rowCount, int averageRowSize)
    {
        TableName = tableName;
        RowCount = rowCount;
        AverageRowSize = averageRowSize;
    }
}

public class MockIndexStatistics : IIndexStatistics
{
    public string IndexName { get; }
    public long PageCount { get; }
    public double Selectivity { get; }
    public int AverageKeyLength { get; } = 50;

    public MockIndexStatistics(string indexName, long pageCount, double selectivity)
    {
        IndexName = indexName;
        PageCount = pageCount;
        Selectivity = selectivity;
    }
}

public class MockIndexInfo : IIndexInfo
{
    public string Name { get; }
    public string TableName { get; }
    public IReadOnlyList<string> Columns { get; }
    public bool IsUnique { get; }
    public bool IsClustered { get; }
    public IndexType IndexType { get; }

    public MockIndexInfo(string name, string tableName, IReadOnlyList<string> columns, bool isUnique, bool isClustered, IndexType indexType)
    {
        Name = name;
        TableName = tableName;
        Columns = columns;
        IsUnique = isUnique;
        IsClustered = isClustered;
        IndexType = indexType;
    }
}

#endregion

/// <summary>
/// Unit tests for advanced query operations.
/// </summary>
public class AdvancedQueryOperationsTests
{
    private readonly AdvancedJoinOperations _joinOperations;
    private readonly AdvancedAggregationOperations _aggregationOperations;
    private readonly SubqueryOperations _subqueryOperations;
    private readonly FullTextSearchOperations _fullTextOperations;
    private readonly MockQueryExecutionContext _executionContext;

    public AdvancedQueryOperationsTests()
    {
        _executionContext = new MockQueryExecutionContext();
        _joinOperations = new AdvancedJoinOperations(_executionContext, new MockJoinStatisticsCollector());
        _aggregationOperations = new AdvancedAggregationOperations(_executionContext, new MockAggregationStatisticsCollector());
        _subqueryOperations = new SubqueryOperations(_executionContext, new MockSubqueryOptimizer(), new MockSubqueryStatisticsCollector());
        _fullTextOperations = new FullTextSearchOperations(
            new MockTextIndexProvider(),
            new MockTextAnalyzer(),
            new MockFullTextStatisticsCollector());
    }

    [Fact]
    public async Task ExecuteHashJoin_ShouldJoinTablesCorrectly()
    {
        // Arrange
        var leftInput = CreateMockQueryResult("users", new[] { ("id", 1), ("name", "Alice") });
        var rightInput = CreateMockQueryResult("orders", new[] { ("user_id", 1), ("amount", 100.0) });
        var joinCondition = new JoinCondition(new[] { "id" }, new[] { "user_id" });

        // Act
        var result = await _joinOperations.ExecuteHashJoinAsync(leftInput, rightInput, joinCondition, JoinType.Inner);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RowCount > 0);
        Assert.Contains("left_", result.Schema.Columns.First().Name);
        Assert.Contains("right_", result.Schema.Columns.Last().Name);
    }

    [Fact]
    public async Task ExecuteWindowFunctions_ShouldCalculateRowNumbers()
    {
        // Arrange
        var input = CreateMockQueryResult("sales", new[] { ("amount", 100), ("region", "North") });
        var windowFunctions = new List<IWindowFunction>
        {
            new MockWindowFunction("row_num", WindowFunctionType.RowNumber)
        };

        // Act
        var result = await _aggregationOperations.ExecuteWindowFunctionsAsync(input, windowFunctions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Schema.Columns.Any(c => c.Name == "row_num"));
    }

    [Fact]
    public async Task ExecuteHashAggregation_ShouldGroupAndAggregate()
    {
        // Arrange
        var input = CreateMockQueryResult("sales", new[] { ("region", "North"), ("amount", 100) });
        var groupByColumns = new List<string> { "region" };
        var aggregateFunctions = new List<IAggregateFunction>
        {
            new MockAggregateFunction("total_amount", AggregateFunctionType.Sum, "amount")
        };

        // Act
        var result = await _aggregationOperations.ExecuteHashAggregationAsync(input, groupByColumns, aggregateFunctions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Schema.Columns.Any(c => c.Name == "region"));
        Assert.True(result.Schema.Columns.Any(c => c.Name == "total_amount"));
    }

    [Fact]
    public async Task ExecuteExistsSubquery_ShouldFilterCorrectly()
    {
        // Arrange
        var outerInput = CreateMockQueryResult("customers", new[] { ("id", 1), ("name", "Alice") });
        var subqueryExpression = new MockSubqueryExpression("SELECT 1 FROM orders WHERE customer_id = customers.id");

        // Act
        var result = await _subqueryOperations.ExecuteExistsSubqueryAsync(outerInput, subqueryExpression);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(outerInput.Schema.Columns.Count, result.Schema.Columns.Count);
    }

    [Fact]
    public async Task ExecuteFullTextSearch_ShouldFindRelevantResults()
    {
        // Arrange
        var input = CreateMockQueryResult("documents", new[] { ("title", "Advanced Database Systems"), ("content", "Full-text search capabilities") });

        // Act
        var result = await _fullTextOperations.ExecuteFullTextSearchAsync(input, "content", "search capabilities");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Schema.Columns.Any(c => c.Name == "_relevance_score"));
    }

    [Fact]
    public async Task ExecutePhraseSearch_ShouldFindExactPhrases()
    {
        // Arrange
        var input = CreateMockQueryResult("documents", new[] { ("content", "database management system") });

        // Act
        var result = await _fullTextOperations.ExecutePhraseSearchAsync(input, "content", "management system");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Schema.Columns.Any(c => c.Name == "_relevance_score"));
    }

    [Fact]
    public async Task ExecuteFuzzySearch_ShouldFindSimilarTerms()
    {
        // Arrange
        var input = CreateMockQueryResult("products", new[] { ("name", "Database") });

        // Act
        var result = await _fullTextOperations.ExecuteFuzzySearchAsync(input, "name", "Databse", maxEditDistance: 1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Schema.Columns.Any(c => c.Name == "_relevance_score"));
    }

    // Helper methods
    private IQueryResult CreateMockQueryResult(string tableName, params (string column, object value)[] data)
    {
        var columns = data.Select(d => new ColumnInfo(d.column, d.value.GetType(), true, tableName)).ToList();
        var schema = new Schema(columns);

        var values = data.ToDictionary(d => d.column, d => d.value);
        var row = new DataRow(values);

        return new QueryResult(new[] { row }, schema);
    }
}

#region Advanced Mock Implementations

public class MockQueryExecutionContext : IQueryExecutionContext
{
    public Task<IQueryResult> ExecuteQueryAsync(IQueryAst query, CancellationToken cancellationToken = default)
    {
        // Return empty result for simplicity
        var schema = new Schema(new List<IColumnInfo>());
        return Task.FromResult<IQueryResult>(new QueryResult(new List<IDataRow>(), schema));
    }

    public Task<IEnumerable<IDataRow>> FindRowsByIndexAsync(IIndexInfo index, object keyValue, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<IDataRow>());
    }
}

public class MockJoinStatisticsCollector : IJoinStatisticsCollector
{
    public Task RecordJoinExecutionAsync(JoinExecutionStats stats)
    {
        return Task.CompletedTask;
    }
}

public class MockAggregationStatisticsCollector : IAggregationStatisticsCollector
{
    public Task RecordAggregationExecutionAsync(AggregationExecutionStats stats)
    {
        return Task.CompletedTask;
    }
}

public class MockSubqueryOptimizer : ISubqueryOptimizer
{
    public Task<SubqueryOptimizationResult> OptimizeExistsSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SubqueryOptimizationResult
        {
            Strategy = SubqueryOptimizationStrategy.CorrelatedExecution,
            EstimatedSubqueryExecutions = outerInput.RowCount,
            EstimatedCost = 1.0
        });
    }

    public Task<SubqueryOptimizationResult> OptimizeInSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, string outerColumn, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SubqueryOptimizationResult
        {
            Strategy = SubqueryOptimizationStrategy.MaterializeAndHash,
            EstimatedSubqueryExecutions = 1,
            EstimatedCost = 1.0
        });
    }

    public Task<SubqueryOptimizationResult> OptimizeScalarSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SubqueryOptimizationResult
        {
            Strategy = SubqueryOptimizationStrategy.MaterializeOnce,
            EstimatedSubqueryExecutions = 1,
            EstimatedCost = 1.0
        });
    }

    public Task<SubqueryOptimizationResult> OptimizeNotExistsSubqueryAsync(IQueryResult outerInput, ISubqueryExpression subqueryExpression, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SubqueryOptimizationResult
        {
            Strategy = SubqueryOptimizationStrategy.AntiJoin,
            EstimatedSubqueryExecutions = 1,
            EstimatedCost = 1.0
        });
    }
}

public class MockSubqueryStatisticsCollector : ISubqueryStatisticsCollector
{
    public Task RecordSubqueryExecutionAsync(SubqueryExecutionStats stats)
    {
        return Task.CompletedTask;
    }
}

public class MockTextIndexProvider : ITextIndexProvider
{
    public Task<bool> HasTextIndexAsync(string column, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<ITextIndex> GetTextIndexAsync(string column, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITextIndex>(new MockTextIndex());
    }
}

public class MockTextIndex : ITextIndex
{
    public Task<IEnumerable<long>> FindRowsContainingTermAsync(string term, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<long>());
    }
}

public class MockTextAnalyzer : ITextAnalyzer
{
    public Task<IAnalyzedQuery> AnalyzeQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var terms = query.Split(' ').Select(t => new QueryTerm { Text = t, Weight = 1.0, DocumentFrequency = 1 }).ToList();
        return Task.FromResult<IAnalyzedQuery>(new AnalyzedQuery { Terms = terms });
    }

    public Task<IAnalyzedQuery> AnalyzePhraseAsync(string phrase, CancellationToken cancellationToken = default)
    {
        return AnalyzeQueryAsync(phrase, cancellationToken);
    }

    public List<string> ExtractTerms(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}

public class MockFullTextStatisticsCollector : IFullTextStatisticsCollector
{
    public Task RecordFullTextSearchAsync(FullTextSearchStats stats)
    {
        return Task.CompletedTask;
    }
}

public class MockWindowFunction : IWindowFunction
{
    public string Alias { get; }
    public WindowFunctionType FunctionType { get; }
    public IReadOnlyList<string> PartitionBy { get; } = new List<string>();
    public IReadOnlyList<IOrderByClause> OrderBy { get; } = new List<IOrderByClause>();
    public string ColumnName { get; } = string.Empty;
    public int? Offset { get; } = null;
    public object? DefaultValue { get; } = null;

    public MockWindowFunction(string alias, WindowFunctionType functionType)
    {
        Alias = alias;
        FunctionType = functionType;
    }
}

public class MockAggregateFunction : IAggregateFunction
{
    public string Alias { get; }
    public AggregateFunctionType FunctionType { get; }
    public string ColumnName { get; }

    public MockAggregateFunction(string alias, AggregateFunctionType functionType, string columnName)
    {
        Alias = alias;
        FunctionType = functionType;
        ColumnName = columnName;
    }

    public object? CreateInitialState() => 0;

    public object? ProcessValue(object? currentState, IDataRow row)
    {
        var value = row.GetValue(ColumnName);
        if (value is int intValue && currentState is int currentInt)
        {
            return currentInt + intValue;
        }
        return currentState;
    }

    public object? GetResult(object? state) => state;
}

public class MockSubqueryExpression : ISubqueryExpression
{
    public IQueryAst Query { get; }
    public IReadOnlyList<ICorrelationCondition> CorrelationConditions { get; } = new List<ICorrelationCondition>();
    public string ResultColumn { get; } = "result";

    public MockSubqueryExpression(string queryText)
    {
        var queryLanguage = new NebulaQueryLanguage();
        Query = queryLanguage.Parse(queryText);
    }
}

public class AnalyzedQuery : IAnalyzedQuery
{
    public List<QueryTerm> Terms { get; set; } = new();
}

public class QueryTerm
{
    public string Text { get; set; } = string.Empty;
    public double Weight { get; set; }
    public int DocumentFrequency { get; set; }
}

public enum AggregateFunctionType
{
    Sum,
    Count,
    Avg,
    Min,
    Max
}

#endregion
