using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Specific query node interfaces for different query types and clauses.
/// </summary>

#region Query Nodes

/// <summary>
/// Represents a SELECT query node.
/// </summary>
public interface ISelectQueryNode : IQueryNode
{
    IReadOnlyList<ISelectItemNode> SelectItems { get; }
    IFromClauseNode? FromClause { get; }
    IWhereClauseNode? WhereClause { get; }
    IReadOnlyList<IJoinClauseNode> JoinClauses { get; }
    IGroupByClauseNode? GroupByClause { get; }
    IHavingClauseNode? HavingClause { get; }
    IOrderByClauseNode? OrderByClause { get; }
    ILimitClauseNode? LimitClause { get; }
    bool IsDistinct { get; }
}

/// <summary>
/// Represents an INSERT query node.
/// </summary>
public interface IInsertQueryNode : IQueryNode
{
    string TableName { get; }
    IReadOnlyList<string> Columns { get; }
    IReadOnlyList<IReadOnlyList<IExpressionNode>> Values { get; }
    ISelectQueryNode? SelectQuery { get; }
}

/// <summary>
/// Represents an UPDATE query node.
/// </summary>
public interface IUpdateQueryNode : IQueryNode
{
    string TableName { get; }
    IReadOnlyList<IAssignmentNode> Assignments { get; }
    IWhereClauseNode? WhereClause { get; }
}

/// <summary>
/// Represents a DELETE query node.
/// </summary>
public interface IDeleteQueryNode : IQueryNode
{
    string TableName { get; }
    IWhereClauseNode? WhereClause { get; }
}

#endregion

#region Clause Nodes

/// <summary>
/// Represents a FROM clause node.
/// </summary>
public interface IFromClauseNode : IQueryNode
{
    IReadOnlyList<ITableReferenceNode> TableReferences { get; }
}

/// <summary>
/// Represents a WHERE clause node.
/// </summary>
public interface IWhereClauseNode : IQueryNode
{
    IExpressionNode Condition { get; }
}

/// <summary>
/// Represents a JOIN clause node.
/// </summary>
public interface IJoinClauseNode : IQueryNode
{
    JoinType JoinType { get; }
    ITableReferenceNode Table { get; }
    IExpressionNode? OnCondition { get; }
    IReadOnlyList<string> UsingColumns { get; }
}

/// <summary>
/// Represents an ORDER BY clause node.
/// </summary>
public interface IOrderByClauseNode : IQueryNode
{
    IReadOnlyList<IOrderByItemNode> OrderByItems { get; }
}

/// <summary>
/// Represents a GROUP BY clause node.
/// </summary>
public interface IGroupByClauseNode : IQueryNode
{
    IReadOnlyList<IExpressionNode> GroupByExpressions { get; }
}

/// <summary>
/// Represents a HAVING clause node.
/// </summary>
public interface IHavingClauseNode : IQueryNode
{
    IExpressionNode Condition { get; }
}

/// <summary>
/// Represents a LIMIT clause node.
/// </summary>
public interface ILimitClauseNode : IQueryNode
{
    int? Limit { get; }
    int? Offset { get; }
}

#endregion

#region Expression Nodes

/// <summary>
/// Represents an expression node.
/// </summary>
public interface IExpressionNode : IQueryNode
{
    Type? ExpressionType { get; }
}

/// <summary>
/// Represents a literal value node.
/// </summary>
public interface ILiteralNode : IExpressionNode
{
    object? LiteralValue { get; }
    LiteralType LiteralType { get; }
}

/// <summary>
/// Represents an identifier node (column, table, alias).
/// </summary>
public interface IIdentifierNode : IExpressionNode
{
    string Name { get; }
    string? TableAlias { get; }
    string? SchemaName { get; }
}

/// <summary>
/// Represents a function call node.
/// </summary>
public interface IFunctionNode : IExpressionNode
{
    string FunctionName { get; }
    IReadOnlyList<IExpressionNode> Arguments { get; }
    bool IsAggregate { get; }
    bool IsWindow { get; }
    IWindowSpecificationNode? WindowSpecification { get; }
}

/// <summary>
/// Represents a binary operation node.
/// </summary>
public interface IBinaryOperationNode : IExpressionNode
{
    BinaryOperator Operator { get; }
    IExpressionNode Left { get; }
    IExpressionNode Right { get; }
}

/// <summary>
/// Represents a unary operation node.
/// </summary>
public interface IUnaryOperationNode : IExpressionNode
{
    UnaryOperator Operator { get; }
    IExpressionNode Operand { get; }
}

#endregion

#region Other Nodes

/// <summary>
/// Represents a SELECT item node.
/// </summary>
public interface ISelectItemNode : IQueryNode
{
    IExpressionNode Expression { get; }
    string? Alias { get; }
    bool IsWildcard { get; }
}

/// <summary>
/// Represents a table reference node.
/// </summary>
public interface ITableReferenceNode : IQueryNode
{
    string TableName { get; }
    string? Alias { get; }
    string? SchemaName { get; }
    ISelectQueryNode? SubQuery { get; }
}

/// <summary>
/// Represents an assignment node (for UPDATE queries).
/// </summary>
public interface IAssignmentNode : IQueryNode
{
    string ColumnName { get; }
    new IExpressionNode Value { get; }
}

/// <summary>
/// Represents an ORDER BY item node.
/// </summary>
public interface IOrderByItemNode : IQueryNode
{
    IExpressionNode Expression { get; }
    SortDirection Direction { get; }
    NullsOrdering NullsOrdering { get; }
}

/// <summary>
/// Represents a window specification node.
/// </summary>
public interface IWindowSpecificationNode : IQueryNode
{
    IReadOnlyList<IExpressionNode> PartitionBy { get; }
    IOrderByClauseNode? OrderBy { get; }
    IWindowFrameNode? Frame { get; }
}

/// <summary>
/// Represents a window frame node.
/// </summary>
public interface IWindowFrameNode : IQueryNode
{
    FrameType FrameType { get; }
    IFrameBoundNode StartBound { get; }
    IFrameBoundNode? EndBound { get; }
}

/// <summary>
/// Represents a frame bound node.
/// </summary>
public interface IFrameBoundNode : IQueryNode
{
    FrameBoundType BoundType { get; }
    IExpressionNode? Offset { get; }
}

#endregion

#region Enums

/// <summary>
/// Types of JOIN operations.
/// </summary>
public enum JoinType
{
    Inner,
    LeftOuter,
    RightOuter,
    FullOuter,
    Cross,
    Natural
}

/// <summary>
/// Types of literal values.
/// </summary>
public enum LiteralType
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Null,
    Binary
}

/// <summary>
/// Binary operators.
/// </summary>
public enum BinaryOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    
    // Comparison
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    
    // Logical
    And,
    Or,
    
    // String
    Like,
    NotLike,
    
    // Set
    In,
    NotIn,
    
    // Other
    Is,
    IsNot
}

/// <summary>
/// Unary operators.
/// </summary>
public enum UnaryOperator
{
    Plus,
    Minus,
    Not,
    IsNull,
    IsNotNull
}

/// <summary>
/// Sort directions.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// NULL ordering options.
/// </summary>
public enum NullsOrdering
{
    Default,
    First,
    Last
}

/// <summary>
/// Window frame types.
/// </summary>
public enum FrameType
{
    Rows,
    Range,
    Groups
}

/// <summary>
/// Frame bound types.
/// </summary>
public enum FrameBoundType
{
    UnboundedPreceding,
    Preceding,
    CurrentRow,
    Following,
    UnboundedFollowing
}

#endregion
