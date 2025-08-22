using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Represents a query language interface for advanced query operations.
/// Provides SQL-like query capabilities with support for complex operations.
/// </summary>
public interface IQueryLanguage
{
    /// <summary>
    /// Parses a query string into an abstract syntax tree.
    /// </summary>
    /// <param name="queryString">The query string to parse</param>
    /// <returns>The parsed query AST</returns>
    /// <exception cref="QuerySyntaxException">Thrown when the query has syntax errors</exception>
    IQueryAst Parse(string queryString);

    /// <summary>
    /// Validates a query string for syntax correctness.
    /// </summary>
    /// <param name="queryString">The query string to validate</param>
    /// <returns>Validation result with errors if any</returns>
    QueryValidationResult Validate(string queryString);

    /// <summary>
    /// Gets the supported query language features.
    /// </summary>
    QueryLanguageFeatures SupportedFeatures { get; }

    /// <summary>
    /// Gets the query language version.
    /// </summary>
    string Version { get; }
}

/// <summary>
/// Represents an abstract syntax tree for a parsed query.
/// </summary>
public interface IQueryAst
{
    /// <summary>
    /// Gets the root node of the AST.
    /// </summary>
    IQueryNode Root { get; }

    /// <summary>
    /// Gets the query type (SELECT, INSERT, UPDATE, DELETE, etc.).
    /// </summary>
    QueryType QueryType { get; }

    /// <summary>
    /// Gets the tables/entities referenced in the query.
    /// </summary>
    IReadOnlyList<string> ReferencedTables { get; }

    /// <summary>
    /// Gets the columns/properties referenced in the query.
    /// </summary>
    IReadOnlyList<string> ReferencedColumns { get; }

    /// <summary>
    /// Accepts a visitor for AST traversal.
    /// </summary>
    /// <typeparam name="T">The visitor result type</typeparam>
    /// <param name="visitor">The visitor to accept</param>
    /// <returns>The visitor result</returns>
    T Accept<T>(IQueryAstVisitor<T> visitor);
}

/// <summary>
/// Represents a node in the query AST.
/// </summary>
public interface IQueryNode
{
    /// <summary>
    /// Gets the node type.
    /// </summary>
    QueryNodeType NodeType { get; }

    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    IReadOnlyList<IQueryNode> Children { get; }

    /// <summary>
    /// Gets the node value if applicable.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Gets the source position in the original query string.
    /// </summary>
    QueryPosition Position { get; }

    /// <summary>
    /// Accepts a visitor for node processing.
    /// </summary>
    /// <typeparam name="T">The visitor result type</typeparam>
    /// <param name="visitor">The visitor to accept</param>
    /// <returns>The visitor result</returns>
    T Accept<T>(IQueryNodeVisitor<T> visitor);
}

/// <summary>
/// Visitor interface for AST traversal.
/// </summary>
/// <typeparam name="T">The result type</typeparam>
public interface IQueryAstVisitor<out T>
{
    T VisitSelectQuery(ISelectQueryNode node);
    T VisitInsertQuery(IInsertQueryNode node);
    T VisitUpdateQuery(IUpdateQueryNode node);
    T VisitDeleteQuery(IDeleteQueryNode node);
    T VisitFromClause(IFromClauseNode node);
    T VisitWhereClause(IWhereClauseNode node);
    T VisitJoinClause(IJoinClauseNode node);
    T VisitOrderByClause(IOrderByClauseNode node);
    T VisitGroupByClause(IGroupByClauseNode node);
    T VisitHavingClause(IHavingClauseNode node);
}

/// <summary>
/// Visitor interface for individual node processing.
/// </summary>
/// <typeparam name="T">The result type</typeparam>
public interface IQueryNodeVisitor<out T>
{
    T VisitNode(IQueryNode node);
    T VisitExpression(IExpressionNode node);
    T VisitLiteral(ILiteralNode node);
    T VisitIdentifier(IIdentifierNode node);
    T VisitFunction(IFunctionNode node);
    T VisitBinaryOperation(IBinaryOperationNode node);
    T VisitUnaryOperation(IUnaryOperationNode node);
}

/// <summary>
/// Represents query validation result.
/// </summary>
public class QueryValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<QueryError> Errors { get; init; } = Array.Empty<QueryError>();
    public IReadOnlyList<QueryWarning> Warnings { get; init; } = Array.Empty<QueryWarning>();
}

/// <summary>
/// Represents a query error.
/// </summary>
public class QueryError
{
    public string Message { get; init; } = string.Empty;
    public QueryPosition Position { get; init; }
    public QueryErrorType ErrorType { get; init; }
    public string? Suggestion { get; init; }
}

/// <summary>
/// Represents a query warning.
/// </summary>
public class QueryWarning
{
    public string Message { get; init; } = string.Empty;
    public QueryPosition Position { get; init; }
    public QueryWarningType WarningType { get; init; }
}

/// <summary>
/// Represents a position in the query string.
/// </summary>
public readonly struct QueryPosition
{
    public int Line { get; init; }
    public int Column { get; init; }
    public int Offset { get; init; }
    public int Length { get; init; }

    public QueryPosition(int line, int column, int offset, int length = 1)
    {
        Line = line;
        Column = column;
        Offset = offset;
        Length = length;
    }

    public override string ToString() => $"Line {Line}, Column {Column}";
}

/// <summary>
/// Supported query language features.
/// </summary>
[Flags]
public enum QueryLanguageFeatures
{
    None = 0,
    BasicSelect = 1 << 0,
    Joins = 1 << 1,
    Subqueries = 1 << 2,
    Aggregations = 1 << 3,
    WindowFunctions = 1 << 4,
    FullTextSearch = 1 << 5,
    CommonTableExpressions = 1 << 6,
    StoredProcedures = 1 << 7,
    Transactions = 1 << 8,
    All = int.MaxValue
}

/// <summary>
/// Query types supported by the language.
/// </summary>
public enum QueryType
{
    Select,
    Insert,
    Update,
    Delete,
    CreateTable,
    DropTable,
    CreateIndex,
    DropIndex,
    Transaction,
    Unknown
}

/// <summary>
/// Query node types in the AST.
/// </summary>
public enum QueryNodeType
{
    // Query types
    SelectQuery,
    InsertQuery,
    UpdateQuery,
    DeleteQuery,
    
    // Clauses
    FromClause,
    WhereClause,
    JoinClause,
    OrderByClause,
    GroupByClause,
    HavingClause,
    
    // Expressions
    Expression,
    Literal,
    Identifier,
    Function,
    BinaryOperation,
    UnaryOperation,
    
    // Other
    Unknown
}

/// <summary>
/// Query error types.
/// </summary>
public enum QueryErrorType
{
    SyntaxError,
    SemanticError,
    TypeMismatch,
    UnknownTable,
    UnknownColumn,
    AmbiguousColumn,
    InvalidFunction,
    InvalidOperator,
    Other
}

/// <summary>
/// Query warning types.
/// </summary>
public enum QueryWarningType
{
    PerformanceWarning,
    DeprecatedFeature,
    PotentialIssue,
    Other
}

/// <summary>
/// Exception thrown for query syntax errors.
/// </summary>
public class QuerySyntaxException : Exception
{
    public QueryPosition Position { get; }
    public QueryErrorType ErrorType { get; }

    public QuerySyntaxException(string message, QueryPosition position, QueryErrorType errorType = QueryErrorType.SyntaxError)
        : base(message)
    {
        Position = position;
        ErrorType = errorType;
    }

    public QuerySyntaxException(string message, QueryPosition position, Exception innerException, QueryErrorType errorType = QueryErrorType.SyntaxError)
        : base(message, innerException)
    {
        Position = position;
        ErrorType = errorType;
    }
}
