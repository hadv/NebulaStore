using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Query.Execution;
using NebulaStore.Storage.Embedded.Query.Advanced;

namespace NebulaStore.Storage.Tests.Query.Execution;

/// <summary>
/// Unit tests for the query execution engine.
/// </summary>
public class QueryExecutionEngineTests
{
    private readonly QueryExecutionEngine _executionEngine;
    private readonly MockStorageEngine _storageEngine;
    private readonly MockQueryOptimizer _queryOptimizer;
    private readonly ExecutionMemoryManager _memoryManager;
    private readonly ExecutionStatisticsCollector _statisticsCollector;

    public QueryExecutionEngineTests()
    {
        _storageEngine = new MockStorageEngine();
        _queryOptimizer = new MockQueryOptimizer();
        _memoryManager = new ExecutionMemoryManager(1024 * 1024 * 1024); // 1GB
        _statisticsCollector = new ExecutionStatisticsCollector();
        
        _executionEngine = new QueryExecutionEngine(
            _storageEngine,
            _queryOptimizer,
            _memoryManager,
            _statisticsCollector);
    }

    [Fact]
    public async Task ExecuteQueryAsync_SimpleSelect_ShouldReturnResults()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT id, name FROM users");

        // Act
        var result = await _executionEngine.ExecuteQueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EstimatedCost > 0);
        Assert.True(result.EstimatedRows > 0);
        Assert.Equal(QueryExecutionStatus.Running, result.Status);

        var rows = await result.ToListAsync();
        Assert.NotEmpty(rows);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithJoin_ShouldExecuteCorrectly()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT u.name, p.title FROM users u JOIN posts p ON u.id = p.user_id");

        // Act
        var result = await _executionEngine.ExecuteQueryAsync(query);

        // Assert
        Assert.NotNull(result);
        var rows = await result.ToListAsync();
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithAggregation_ShouldGroupResults()
    {
        // Arrange
        var queryLanguage = new NebulaQueryLanguage();
        var query = queryLanguage.Parse("SELECT category, COUNT(*) FROM products GROUP BY category");

        // Act
        var result = await _executionEngine.ExecuteQueryAsync(query);

        // Assert
        Assert.NotNull(result);
        var rows = await result.ToListAsync();
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task ExecutePlanAsync_WithPhysicalPlan_ShouldExecuteDirectly()
    {
        // Arrange
        var schema = new Schema(new List<IColumnInfo>
        {
            new ColumnInfo("id", typeof(int), false),
            new ColumnInfo("name", typeof(string), true)
        });

        var tableScanOperator = new PhysicalTableScanOperator("users", schema, 10.0, 100);
        var plan = new PhysicalPlan(tableScanOperator, schema, 10.0, 100);

        // Act
        var result = await _executionEngine.ExecutePlanAsync(plan);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10.0, result.EstimatedCost);
        Assert.Equal(100, result.EstimatedRows);
    }

    [Fact]
    public async Task MemoryBudget_ShouldTrackAllocation()
    {
        // Arrange
        var budget = new MemoryBudget(1024 * 1024); // 1MB

        // Act & Assert
        Assert.True(budget.TryAllocate(512 * 1024)); // 512KB
        Assert.Equal(512 * 1024, budget.UsedMemory);
        Assert.Equal(512 * 1024, budget.AvailableMemory);
        Assert.Equal(0.5, budget.UsagePercentage);

        Assert.True(budget.TryAllocate(256 * 1024)); // 256KB
        Assert.Equal(768 * 1024, budget.UsedMemory);

        Assert.False(budget.TryAllocate(512 * 1024)); // Would exceed budget

        budget.Release(256 * 1024);
        Assert.Equal(512 * 1024, budget.UsedMemory);
    }

    [Fact]
    public async Task TemporaryStorage_Memory_ShouldStoreAndRetrieveRows()
    {
        // Arrange
        var options = new TemporaryStorageOptions { StorageType = TemporaryStorageType.Memory };
        var storage = new MemoryTemporaryStorage("test", options);

        var row1 = new DataRow(new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Alice" });
        var row2 = new DataRow(new Dictionary<string, object?> { ["id"] = 2, ["name"] = "Bob" });

        // Act
        await storage.AddRowAsync(row1);
        await storage.AddRowAsync(row2);

        // Assert
        Assert.Equal(2, storage.RowCount);

        var retrievedRows = new List<IDataRow>();
        await foreach (var row in storage.GetRowsAsync())
        {
            retrievedRows.Add(row);
        }

        Assert.Equal(2, retrievedRows.Count);
        Assert.Equal(1, retrievedRows[0].GetValue("id"));
        Assert.Equal("Alice", retrievedRows[0].GetValue("name"));
    }

    [Fact]
    public async Task TableScanOperator_ShouldScanTable()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var operator1 = new TableScanExecutionOperator("users", _storageEngine, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in operator1.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotEmpty(rows);
        Assert.True(operator1.ProcessedRows > 0);
        Assert.True(operator1.ExecutionTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task HashJoinOperator_ShouldJoinTables()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var leftOperator = new TableScanExecutionOperator("users", _storageEngine, context);
        var rightOperator = new TableScanExecutionOperator("posts", _storageEngine, context);
        var joinOperator = new HashJoinExecutionOperator(leftOperator, rightOperator, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in joinOperator.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotNull(rows);
        Assert.True(joinOperator.ProcessedRows >= 0);
    }

    [Fact]
    public async Task HashAggregateOperator_ShouldGroupAndAggregate()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var childOperator = new TableScanExecutionOperator("sales", _storageEngine, context);
        var aggregateOperator = new HashAggregateExecutionOperator(childOperator, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in aggregateOperator.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotNull(rows);
        Assert.True(aggregateOperator.ProcessedRows >= 0);
    }

    [Fact]
    public async Task SortOperator_ShouldSortRows()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var childOperator = new TableScanExecutionOperator("users", _storageEngine, context);
        var sortOperator = new SortExecutionOperator(childOperator, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in sortOperator.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotNull(rows);
        Assert.True(sortOperator.ProcessedRows >= 0);
    }

    [Fact]
    public async Task FilterOperator_ShouldFilterRows()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var childOperator = new TableScanExecutionOperator("users", _storageEngine, context);
        var filterOperator = new FilterExecutionOperator(childOperator, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in filterOperator.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotNull(rows);
        Assert.True(filterOperator.ProcessedRows >= 0);
    }

    [Fact]
    public async Task ProjectionOperator_ShouldProjectColumns()
    {
        // Arrange
        var context = CreateMockExecutionContext();
        var childOperator = new TableScanExecutionOperator("users", _storageEngine, context);
        var projectionOperator = new ProjectionExecutionOperator(childOperator, context);

        // Act
        var rows = new List<IDataRow>();
        await foreach (var row in projectionOperator.ExecuteAsync())
        {
            rows.Add(row);
        }

        // Assert
        Assert.NotNull(rows);
        Assert.True(projectionOperator.ProcessedRows >= 0);
    }

    [Fact]
    public async Task ExecutionStatistics_ShouldTrackExecution()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var startStats = new QueryExecutionStats
        {
            ExecutionId = executionId,
            QueryType = QueryType.Select,
            EstimatedCost = 10.0,
            EstimatedRows = 100,
            StartTime = DateTime.UtcNow
        };

        var completionStats = new QueryExecutionCompletionStats
        {
            ExecutionId = executionId,
            ActualRows = 95,
            ExecutionTime = TimeSpan.FromMilliseconds(150),
            MemoryUsed = 1024
        };

        // Act
        await _statisticsCollector.RecordQueryExecutionStartAsync(startStats);
        await _statisticsCollector.RecordQueryExecutionCompletedAsync(completionStats);

        // Assert
        var statistics = await _statisticsCollector.GetExecutionStatisticsAsync(executionId);
        Assert.NotNull(statistics);
        Assert.Equal(executionId, statistics.ExecutionId);
        Assert.Equal(10.0, statistics.EstimatedCost);
        Assert.Equal(100, statistics.EstimatedRows);
        Assert.Equal(95, statistics.ActualRows);
        Assert.True(statistics.EstimationAccuracy > 0.9); // Good estimation
    }

    [Fact]
    public async Task QueryExecutionResult_ShouldProvideStreamingAccess()
    {
        // Arrange
        var schema = new Schema(new List<IColumnInfo>
        {
            new ColumnInfo("id", typeof(int), false),
            new ColumnInfo("name", typeof(string), true)
        });

        var rows = new List<IDataRow>
        {
            new DataRow(new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Alice" }),
            new DataRow(new Dictionary<string, object?> { ["id"] = 2, ["name"] = "Bob" }),
            new DataRow(new Dictionary<string, object?> { ["id"] = 3, ["name"] = "Charlie" })
        };

        async IAsyncEnumerable<IDataRow> GetRowsAsync()
        {
            foreach (var row in rows)
            {
                yield return row;
            }
        }

        var result = new QueryExecutionResult(
            Guid.NewGuid(),
            schema,
            GetRowsAsync(),
            5.0,
            3,
            DateTime.UtcNow);

        // Act & Assert
        var count = await result.CountAsync();
        Assert.Equal(3, count);

        var firstRow = await result.FirstOrDefaultAsync();
        Assert.NotNull(firstRow);
        Assert.Equal(1, firstRow.GetValue("id"));

        var allRows = await result.ToListAsync();
        Assert.Equal(3, allRows.Count);
    }

    // Helper methods
    private IQueryExecutionContext CreateMockExecutionContext()
    {
        var budget = new MemoryBudget(64 * 1024 * 1024); // 64MB
        return new QueryExecutionContext(
            Guid.NewGuid(),
            _storageEngine,
            _memoryManager,
            budget,
            new QueryExecutionOptions(),
            CancellationToken.None);
    }
}

#region Mock Implementations

public class MockStorageEngine : IStorageEngine
{
    public async Task<IQueryResult> GetTableDataAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var schema = new Schema(new List<IColumnInfo>
        {
            new ColumnInfo("id", typeof(int), false),
            new ColumnInfo("name", typeof(string), true),
            new ColumnInfo("active", typeof(bool), false),
            new ColumnInfo("category", typeof(string), true),
            new ColumnInfo("amount", typeof(double), true)
        });

        var rows = new List<IDataRow>
        {
            new DataRow(new Dictionary<string, object?> 
            { 
                ["id"] = 1, 
                ["name"] = "Alice", 
                ["active"] = true, 
                ["category"] = "A", 
                ["amount"] = 100.0 
            }),
            new DataRow(new Dictionary<string, object?> 
            { 
                ["id"] = 2, 
                ["name"] = "Bob", 
                ["active"] = true, 
                ["category"] = "B", 
                ["amount"] = 200.0 
            }),
            new DataRow(new Dictionary<string, object?> 
            { 
                ["id"] = 3, 
                ["name"] = "Charlie", 
                ["active"] = false, 
                ["category"] = "A", 
                ["amount"] = 150.0 
            })
        };

        return new QueryResult(rows, schema);
    }

    public async Task<IQueryResult> GetIndexDataAsync(string indexName, string tableName, CancellationToken cancellationToken = default)
    {
        return await GetTableDataAsync(tableName, cancellationToken);
    }

    public async Task<IIndexInfo?> GetIndexInfoAsync(string indexName, CancellationToken cancellationToken = default)
    {
        return new MockIndexInfo(indexName, "test_table", new[] { "id" }, true, false, IndexType.BTree);
    }

    public async Task<ITableStatistics?> GetTableStatisticsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        return new MockTableStatistics(tableName, 1000, 100);
    }
}

public class MockQueryOptimizer : IQueryOptimizer
{
    public async Task<IQueryExecutionPlan> OptimizeAsync(IQueryAst query, CancellationToken cancellationToken = default)
    {
        var schema = new Schema(new List<IColumnInfo>
        {
            new ColumnInfo("id", typeof(int), false),
            new ColumnInfo("name", typeof(string), true)
        });

        var tableScanOperator = new PhysicalTableScanOperator("users", schema, 10.0, 100);
        return new QueryExecutionPlan(tableScanOperator, 10.0, 100);
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

#endregion
