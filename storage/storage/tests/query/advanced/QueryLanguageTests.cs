using System;
using System.Linq;
using Xunit;
using NebulaStore.Storage.Embedded.Query.Advanced;

namespace NebulaStore.Storage.Tests.Query.Advanced;

/// <summary>
/// Unit tests for the NebulaStore query language implementation.
/// </summary>
public class QueryLanguageTests
{
    private readonly NebulaQueryLanguage _queryLanguage;

    public QueryLanguageTests()
    {
        _queryLanguage = new NebulaQueryLanguage();
    }

    [Fact]
    public void Parse_SimpleSelectQuery_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT id, name FROM users";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);
        Assert.Contains("users", ast.ReferencedTables);
        Assert.Contains("id", ast.ReferencedColumns);
        Assert.Contains("name", ast.ReferencedColumns);
    }

    [Fact]
    public void Parse_SelectWithWhere_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT * FROM products WHERE price > 100";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);
        Assert.Contains("products", ast.ReferencedTables);
        Assert.Contains("price", ast.ReferencedColumns);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.NotNull(selectNode.WhereClause);
        Assert.True(selectNode.SelectItems.First().IsWildcard);
    }

    [Fact]
    public void Parse_SelectWithJoin_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT u.name, p.title FROM users u JOIN posts p ON u.id = p.user_id";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);
        Assert.Contains("users", ast.ReferencedTables);
        Assert.Contains("posts", ast.ReferencedTables);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.Single(selectNode.JoinClauses);
        Assert.Equal(JoinType.Inner, selectNode.JoinClauses.First().JoinType);
    }

    [Fact]
    public void Parse_SelectWithGroupBy_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT category, COUNT(*) FROM products GROUP BY category";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.NotNull(selectNode.GroupByClause);
        Assert.Single(selectNode.GroupByClause.GroupByExpressions);
    }

    [Fact]
    public void Parse_SelectWithOrderBy_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT name, age FROM users ORDER BY age DESC, name ASC";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.NotNull(selectNode.OrderByClause);
        Assert.Equal(2, selectNode.OrderByClause.OrderByItems.Count);
        Assert.Equal(SortDirection.Descending, selectNode.OrderByClause.OrderByItems.First().Direction);
        Assert.Equal(SortDirection.Ascending, selectNode.OrderByClause.OrderByItems.Last().Direction);
    }

    [Fact]
    public void Parse_SelectWithLimit_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT * FROM users LIMIT 10 OFFSET 5";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.NotNull(selectNode.LimitClause);
        Assert.Equal(10, selectNode.LimitClause.Limit);
        Assert.Equal(5, selectNode.LimitClause.Offset);
    }

    [Fact]
    public void Parse_InsertQuery_ShouldSucceed()
    {
        // Arrange
        var query = "INSERT INTO users (name, email) VALUES ('John', 'john@example.com')";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Insert, ast.QueryType);

        var insertNode = ast.Root as IInsertQueryNode;
        Assert.NotNull(insertNode);
        Assert.Equal("users", insertNode.TableName);
        Assert.Equal(2, insertNode.Columns.Count);
        Assert.Contains("name", insertNode.Columns);
        Assert.Contains("email", insertNode.Columns);
        Assert.Single(insertNode.Values);
    }

    [Fact]
    public void Parse_UpdateQuery_ShouldSucceed()
    {
        // Arrange
        var query = "UPDATE users SET name = 'Jane', age = 30 WHERE id = 1";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Update, ast.QueryType);

        var updateNode = ast.Root as IUpdateQueryNode;
        Assert.NotNull(updateNode);
        Assert.Equal("users", updateNode.TableName);
        Assert.Equal(2, updateNode.Assignments.Count);
        Assert.NotNull(updateNode.WhereClause);
    }

    [Fact]
    public void Parse_DeleteQuery_ShouldSucceed()
    {
        // Arrange
        var query = "DELETE FROM users WHERE age < 18";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Delete, ast.QueryType);

        var deleteNode = ast.Root as IDeleteQueryNode;
        Assert.NotNull(deleteNode);
        Assert.Equal("users", deleteNode.TableName);
        Assert.NotNull(deleteNode.WhereClause);
    }

    [Fact]
    public void Parse_ComplexExpression_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT * FROM products WHERE (price > 100 AND category = 'electronics') OR discount > 0.5";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.NotNull(selectNode.WhereClause);

        // The where clause should contain a complex binary operation (OR)
        var whereCondition = selectNode.WhereClause.Condition;
        Assert.IsType<BinaryOperationNode>(whereCondition);
        var orOperation = whereCondition as IBinaryOperationNode;
        Assert.Equal(BinaryOperator.Or, orOperation!.Operator);
    }

    [Fact]
    public void Parse_FunctionCall_ShouldSucceed()
    {
        // Arrange
        var query = "SELECT COUNT(*), AVG(price), MAX(created_at) FROM products";

        // Act
        var ast = _queryLanguage.Parse(query);

        // Assert
        Assert.NotNull(ast);
        Assert.Equal(QueryType.Select, ast.QueryType);

        var selectNode = ast.Root as ISelectQueryNode;
        Assert.NotNull(selectNode);
        Assert.Equal(3, selectNode.SelectItems.Count);

        // All select items should be function calls
        foreach (var item in selectNode.SelectItems)
        {
            Assert.IsType<FunctionNode>(item.Expression);
            var function = item.Expression as IFunctionNode;
            Assert.True(function!.IsAggregate);
        }
    }

    [Fact]
    public void Parse_EmptyQuery_ShouldThrowException()
    {
        // Arrange
        var query = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _queryLanguage.Parse(query));
    }

    [Fact]
    public void Parse_InvalidSyntax_ShouldThrowException()
    {
        // Arrange
        var query = "SELECT FROM WHERE";

        // Act & Assert
        Assert.Throws<QueryParserException>(() => _queryLanguage.Parse(query));
    }

    [Fact]
    public void Validate_ValidQuery_ShouldReturnValid()
    {
        // Arrange
        var query = "SELECT id, name FROM users WHERE active = true";

        // Act
        var result = _queryLanguage.Validate(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidQuery_ShouldReturnErrors()
    {
        // Arrange
        var query = "SELECT FROM";

        // Act
        var result = _queryLanguage.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_SelectStar_ShouldReturnPerformanceWarning()
    {
        // Arrange
        var query = "SELECT * FROM users";

        // Act
        var result = _queryLanguage.Validate(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.WarningType == QueryWarningType.PerformanceWarning);
    }

    [Fact]
    public void SupportedFeatures_ShouldIncludeBasicFeatures()
    {
        // Act
        var features = _queryLanguage.SupportedFeatures;

        // Assert
        Assert.True(features.HasFlag(QueryLanguageFeatures.BasicSelect));
        Assert.True(features.HasFlag(QueryLanguageFeatures.Joins));
        Assert.True(features.HasFlag(QueryLanguageFeatures.Aggregations));
        Assert.True(features.HasFlag(QueryLanguageFeatures.Subqueries));
    }

    [Fact]
    public void Version_ShouldReturnValidVersion()
    {
        // Act
        var version = _queryLanguage.Version;

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.Matches(@"\d+\.\d+\.\d+", version);
    }
}
