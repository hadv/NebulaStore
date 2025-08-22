using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Implementation of the NebulaStore query language.
/// Provides SQL-like query capabilities with advanced features.
/// </summary>
public class NebulaQueryLanguage : IQueryLanguage
{
    private static readonly QueryLanguageFeatures DefaultFeatures = 
        QueryLanguageFeatures.BasicSelect |
        QueryLanguageFeatures.Joins |
        QueryLanguageFeatures.Subqueries |
        QueryLanguageFeatures.Aggregations |
        QueryLanguageFeatures.WindowFunctions |
        QueryLanguageFeatures.FullTextSearch;

    /// <summary>
    /// Gets the supported query language features.
    /// </summary>
    public QueryLanguageFeatures SupportedFeatures { get; }

    /// <summary>
    /// Gets the query language version.
    /// </summary>
    public string Version => "1.0.0";

    /// <summary>
    /// Initializes a new instance of the NebulaQueryLanguage class.
    /// </summary>
    /// <param name="supportedFeatures">The features to support (optional)</param>
    public NebulaQueryLanguage(QueryLanguageFeatures? supportedFeatures = null)
    {
        SupportedFeatures = supportedFeatures ?? DefaultFeatures;
    }

    /// <summary>
    /// Parses a query string into an abstract syntax tree.
    /// </summary>
    /// <param name="queryString">The query string to parse</param>
    /// <returns>The parsed query AST</returns>
    /// <exception cref="QuerySyntaxException">Thrown when the query has syntax errors</exception>
    public IQueryAst Parse(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            throw new ArgumentException("Query string cannot be null or empty", nameof(queryString));

        try
        {
            // Tokenize the query string
            var lexer = new QueryLexer(queryString);
            var tokens = lexer.Tokenize();

            // Parse tokens into AST
            var parser = new QueryParser(tokens);
            var ast = parser.Parse();

            // Validate the AST against supported features
            ValidateAstFeatures(ast);

            return ast;
        }
        catch (QuerySyntaxException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new QuerySyntaxException($"Failed to parse query: {ex.Message}", new QueryPosition(1, 1, 0), ex);
        }
    }

    /// <summary>
    /// Validates a query string for syntax correctness.
    /// </summary>
    /// <param name="queryString">The query string to validate</param>
    /// <returns>Validation result with errors if any</returns>
    public QueryValidationResult Validate(string queryString)
    {
        var errors = new List<QueryError>();
        var warnings = new List<QueryWarning>();

        if (string.IsNullOrWhiteSpace(queryString))
        {
            errors.Add(new QueryError
            {
                Message = "Query string cannot be null or empty",
                Position = new QueryPosition(1, 1, 0),
                ErrorType = QueryErrorType.SyntaxError
            });
            return new QueryValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        try
        {
            // Try to parse the query
            var ast = Parse(queryString);
            
            // Perform semantic validation
            var semanticValidator = new QuerySemanticValidator();
            var semanticResult = semanticValidator.Validate(ast);
            
            errors.AddRange(semanticResult.Errors);
            warnings.AddRange(semanticResult.Warnings);

            // Check for performance warnings
            var performanceAnalyzer = new QueryPerformanceAnalyzer();
            var performanceWarnings = performanceAnalyzer.AnalyzePerformance(ast);
            warnings.AddRange(performanceWarnings);

            return new QueryValidationResult 
            { 
                IsValid = errors.Count == 0, 
                Errors = errors, 
                Warnings = warnings 
            };
        }
        catch (QuerySyntaxException ex)
        {
            errors.Add(new QueryError
            {
                Message = ex.Message,
                Position = ex.Position,
                ErrorType = ex.ErrorType
            });
            return new QueryValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }
        catch (Exception ex)
        {
            errors.Add(new QueryError
            {
                Message = $"Unexpected error during validation: {ex.Message}",
                Position = new QueryPosition(1, 1, 0),
                ErrorType = QueryErrorType.Other
            });
            return new QueryValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }
    }

    private void ValidateAstFeatures(IQueryAst ast)
    {
        var featureValidator = new QueryFeatureValidator(SupportedFeatures);
        featureValidator.ValidateFeatures(ast);
    }
}

/// <summary>
/// Validates that query features are supported.
/// </summary>
internal class QueryFeatureValidator
{
    private readonly QueryLanguageFeatures _supportedFeatures;

    public QueryFeatureValidator(QueryLanguageFeatures supportedFeatures)
    {
        _supportedFeatures = supportedFeatures;
    }

    public void ValidateFeatures(IQueryAst ast)
    {
        var visitor = new FeatureValidationVisitor(_supportedFeatures);
        ast.Accept(visitor);
    }

    private class FeatureValidationVisitor : IQueryAstVisitor<object?>
    {
        private readonly QueryLanguageFeatures _supportedFeatures;

        public FeatureValidationVisitor(QueryLanguageFeatures supportedFeatures)
        {
            _supportedFeatures = supportedFeatures;
        }

        public object? VisitSelectQuery(ISelectQueryNode node)
        {
            if (!_supportedFeatures.HasFlag(QueryLanguageFeatures.BasicSelect))
                throw new QuerySyntaxException("SELECT queries are not supported", node.Position, QueryErrorType.SemanticError);

            if (node.JoinClauses.Any() && !_supportedFeatures.HasFlag(QueryLanguageFeatures.Joins))
                throw new QuerySyntaxException("JOIN operations are not supported", node.Position, QueryErrorType.SemanticError);

            // Check for aggregate functions
            var hasAggregates = HasAggregateFunction(node);
            if (hasAggregates && !_supportedFeatures.HasFlag(QueryLanguageFeatures.Aggregations))
                throw new QuerySyntaxException("Aggregate functions are not supported", node.Position, QueryErrorType.SemanticError);

            // Check for window functions
            var hasWindowFunctions = HasWindowFunction(node);
            if (hasWindowFunctions && !_supportedFeatures.HasFlag(QueryLanguageFeatures.WindowFunctions))
                throw new QuerySyntaxException("Window functions are not supported", node.Position, QueryErrorType.SemanticError);

            // Check for subqueries
            var hasSubqueries = HasSubquery(node);
            if (hasSubqueries && !_supportedFeatures.HasFlag(QueryLanguageFeatures.Subqueries))
                throw new QuerySyntaxException("Subqueries are not supported", node.Position, QueryErrorType.SemanticError);

            return null;
        }

        public object? VisitInsertQuery(IInsertQueryNode node)
        {
            if (node.SelectQuery != null)
            {
                VisitSelectQuery(node.SelectQuery);
            }
            return null;
        }

        public object? VisitUpdateQuery(IUpdateQueryNode node) => null;

        public object? VisitDeleteQuery(IDeleteQueryNode node) => null;

        public object? VisitFromClause(IFromClauseNode node) => null;

        public object? VisitWhereClause(IWhereClauseNode node) => null;

        public object? VisitJoinClause(IJoinClauseNode node)
        {
            if (!_supportedFeatures.HasFlag(QueryLanguageFeatures.Joins))
                throw new QuerySyntaxException("JOIN operations are not supported", node.Position, QueryErrorType.SemanticError);
            return null;
        }

        public object? VisitOrderByClause(IOrderByClauseNode node) => null;

        public object? VisitGroupByClause(IGroupByClauseNode node)
        {
            if (!_supportedFeatures.HasFlag(QueryLanguageFeatures.Aggregations))
                throw new QuerySyntaxException("GROUP BY is not supported without aggregation support", node.Position, QueryErrorType.SemanticError);
            return null;
        }

        public object? VisitHavingClause(IHavingClauseNode node)
        {
            if (!_supportedFeatures.HasFlag(QueryLanguageFeatures.Aggregations))
                throw new QuerySyntaxException("HAVING clause is not supported without aggregation support", node.Position, QueryErrorType.SemanticError);
            return null;
        }

        private bool HasAggregateFunction(ISelectQueryNode node)
        {
            return node.SelectItems.Any(item => ContainsAggregateFunction(item.Expression)) ||
                   (node.HavingClause != null && ContainsAggregateFunction(node.HavingClause.Condition));
        }

        private bool HasWindowFunction(ISelectQueryNode node)
        {
            return node.SelectItems.Any(item => ContainsWindowFunction(item.Expression));
        }

        private bool HasSubquery(ISelectQueryNode node)
        {
            return node.FromClause?.TableReferences.Any(tr => tr.SubQuery != null) == true;
        }

        private bool ContainsAggregateFunction(IExpressionNode expression)
        {
            return expression switch
            {
                IFunctionNode func => func.IsAggregate || func.Arguments.Any(ContainsAggregateFunction),
                IBinaryOperationNode binary => ContainsAggregateFunction(binary.Left) || ContainsAggregateFunction(binary.Right),
                IUnaryOperationNode unary => ContainsAggregateFunction(unary.Operand),
                _ => false
            };
        }

        private bool ContainsWindowFunction(IExpressionNode expression)
        {
            return expression switch
            {
                IFunctionNode func => func.IsWindow || func.Arguments.Any(ContainsWindowFunction),
                IBinaryOperationNode binary => ContainsWindowFunction(binary.Left) || ContainsWindowFunction(binary.Right),
                IUnaryOperationNode unary => ContainsWindowFunction(unary.Operand),
                _ => false
            };
        }
    }
}

/// <summary>
/// Performs semantic validation of queries.
/// </summary>
internal class QuerySemanticValidator
{
    public QueryValidationResult Validate(IQueryAst ast)
    {
        var errors = new List<QueryError>();
        var warnings = new List<QueryWarning>();

        // Perform basic semantic checks
        ValidateTableReferences(ast, errors);
        ValidateColumnReferences(ast, errors, warnings);
        ValidateAggregateUsage(ast, errors);

        return new QueryValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void ValidateTableReferences(IQueryAst ast, List<QueryError> errors)
    {
        // Basic validation - in a real implementation, this would check against schema
        var referencedTables = ast.ReferencedTables;
        foreach (var table in referencedTables)
        {
            if (string.IsNullOrWhiteSpace(table))
            {
                errors.Add(new QueryError
                {
                    Message = "Empty table name is not allowed",
                    Position = new QueryPosition(1, 1, 0),
                    ErrorType = QueryErrorType.UnknownTable
                });
            }
        }
    }

    private void ValidateColumnReferences(IQueryAst ast, List<QueryError> errors, List<QueryWarning> warnings)
    {
        // Basic validation - in a real implementation, this would check against schema
        var referencedColumns = ast.ReferencedColumns;
        foreach (var column in referencedColumns)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                errors.Add(new QueryError
                {
                    Message = "Empty column name is not allowed",
                    Position = new QueryPosition(1, 1, 0),
                    ErrorType = QueryErrorType.UnknownColumn
                });
            }
        }
    }

    private void ValidateAggregateUsage(IQueryAst ast, List<QueryError> errors)
    {
        // Validate proper usage of aggregate functions with GROUP BY
        // This is a simplified implementation
    }
}

/// <summary>
/// Analyzes queries for performance issues.
/// </summary>
internal class QueryPerformanceAnalyzer
{
    public IReadOnlyList<QueryWarning> AnalyzePerformance(IQueryAst ast)
    {
        var warnings = new List<QueryWarning>();

        // Check for potential performance issues
        CheckForCartesianProduct(ast, warnings);
        CheckForMissingIndexes(ast, warnings);
        CheckForSelectStar(ast, warnings);

        return warnings;
    }

    private void CheckForCartesianProduct(IQueryAst ast, List<QueryWarning> warnings)
    {
        if (ast.Root is ISelectQueryNode selectNode)
        {
            var hasMultipleTables = selectNode.FromClause?.TableReferences.Count > 1;
            var hasJoins = selectNode.JoinClauses.Any();
            var hasWhereClause = selectNode.WhereClause != null;

            if (hasMultipleTables && !hasJoins && !hasWhereClause)
            {
                warnings.Add(new QueryWarning
                {
                    Message = "Potential cartesian product detected - consider adding JOIN conditions",
                    Position = selectNode.Position,
                    WarningType = QueryWarningType.PerformanceWarning
                });
            }
        }
    }

    private void CheckForMissingIndexes(IQueryAst ast, List<QueryWarning> warnings)
    {
        // In a real implementation, this would check against available indexes
        // For now, just warn about WHERE clauses without indexes
        if (ast.Root is ISelectQueryNode selectNode && selectNode.WhereClause != null)
        {
            warnings.Add(new QueryWarning
            {
                Message = "Consider adding indexes for WHERE clause conditions",
                Position = selectNode.WhereClause.Position,
                WarningType = QueryWarningType.PerformanceWarning
            });
        }
    }

    private void CheckForSelectStar(IQueryAst ast, List<QueryWarning> warnings)
    {
        if (ast.Root is ISelectQueryNode selectNode)
        {
            var hasSelectStar = selectNode.SelectItems.Any(item => item.IsWildcard);
            if (hasSelectStar)
            {
                warnings.Add(new QueryWarning
                {
                    Message = "SELECT * may impact performance - consider selecting specific columns",
                    Position = selectNode.Position,
                    WarningType = QueryWarningType.PerformanceWarning
                });
            }
        }
    }
}
