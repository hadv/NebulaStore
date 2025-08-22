using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Concrete implementations of query AST nodes.
/// </summary>

#region Base Classes

/// <summary>
/// Base implementation of query AST.
/// </summary>
public class QueryAst : IQueryAst
{
    public IQueryNode Root { get; }
    public QueryType QueryType { get; }
    public IReadOnlyList<string> ReferencedTables { get; }
    public IReadOnlyList<string> ReferencedColumns { get; }

    public QueryAst(IQueryNode root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        QueryType = DetermineQueryType(root);
        
        var analyzer = new QueryAnalyzer();
        var analysis = analyzer.Analyze(this);
        ReferencedTables = analysis.ReferencedTables;
        ReferencedColumns = analysis.ReferencedColumns;
    }

    public T Accept<T>(IQueryAstVisitor<T> visitor)
    {
        return Root.Accept(new QueryAstVisitorAdapter<T>(visitor));
    }

    private static QueryType DetermineQueryType(IQueryNode root)
    {
        return root.NodeType switch
        {
            QueryNodeType.SelectQuery => QueryType.Select,
            QueryNodeType.InsertQuery => QueryType.Insert,
            QueryNodeType.UpdateQuery => QueryType.Update,
            QueryNodeType.DeleteQuery => QueryType.Delete,
            _ => QueryType.Unknown
        };
    }
}

/// <summary>
/// Base implementation of query node.
/// </summary>
public abstract class QueryNode : IQueryNode
{
    public QueryNodeType NodeType { get; }
    public IReadOnlyList<IQueryNode> Children { get; }
    public object? Value { get; }
    public QueryPosition Position { get; }

    protected QueryNode(QueryNodeType nodeType, IReadOnlyList<IQueryNode>? children, object? value, QueryPosition position)
    {
        NodeType = nodeType;
        Children = children ?? Array.Empty<IQueryNode>();
        Value = value;
        Position = position;
    }

    public abstract T Accept<T>(IQueryNodeVisitor<T> visitor);
}

/// <summary>
/// Base implementation of expression node.
/// </summary>
public abstract class ExpressionNode : QueryNode, IExpressionNode
{
    public Type? ExpressionType { get; }

    protected ExpressionNode(QueryNodeType nodeType, IReadOnlyList<IQueryNode>? children, object? value, QueryPosition position, Type? expressionType = null)
        : base(nodeType, children, value, position)
    {
        ExpressionType = expressionType;
    }
}

#endregion

#region Query Nodes

/// <summary>
/// Implementation of SELECT query node.
/// </summary>
public class SelectQueryNode : QueryNode, ISelectQueryNode
{
    public IReadOnlyList<ISelectItemNode> SelectItems { get; }
    public IFromClauseNode? FromClause { get; }
    public IWhereClauseNode? WhereClause { get; }
    public IReadOnlyList<IJoinClauseNode> JoinClauses { get; }
    public IGroupByClauseNode? GroupByClause { get; }
    public IHavingClauseNode? HavingClause { get; }
    public IOrderByClauseNode? OrderByClause { get; }
    public ILimitClauseNode? LimitClause { get; }
    public bool IsDistinct { get; }

    public SelectQueryNode(
        IReadOnlyList<ISelectItemNode> selectItems,
        IFromClauseNode? fromClause,
        IWhereClauseNode? whereClause,
        IReadOnlyList<IJoinClauseNode> joinClauses,
        IGroupByClauseNode? groupByClause,
        IHavingClauseNode? havingClause,
        IOrderByClauseNode? orderByClause,
        ILimitClauseNode? limitClause,
        bool isDistinct,
        QueryPosition position)
        : base(QueryNodeType.SelectQuery, BuildChildren(selectItems, fromClause, whereClause, joinClauses, groupByClause, havingClause, orderByClause, limitClause), null, position)
    {
        SelectItems = selectItems ?? throw new ArgumentNullException(nameof(selectItems));
        FromClause = fromClause;
        WhereClause = whereClause;
        JoinClauses = joinClauses ?? Array.Empty<IJoinClauseNode>();
        GroupByClause = groupByClause;
        HavingClause = havingClause;
        OrderByClause = orderByClause;
        LimitClause = limitClause;
        IsDistinct = isDistinct;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(
        IReadOnlyList<ISelectItemNode> selectItems,
        IFromClauseNode? fromClause,
        IWhereClauseNode? whereClause,
        IReadOnlyList<IJoinClauseNode> joinClauses,
        IGroupByClauseNode? groupByClause,
        IHavingClauseNode? havingClause,
        IOrderByClauseNode? orderByClause,
        ILimitClauseNode? limitClause)
    {
        var children = new List<IQueryNode>();
        children.AddRange(selectItems);
        if (fromClause != null) children.Add(fromClause);
        if (whereClause != null) children.Add(whereClause);
        children.AddRange(joinClauses);
        if (groupByClause != null) children.Add(groupByClause);
        if (havingClause != null) children.Add(havingClause);
        if (orderByClause != null) children.Add(orderByClause);
        if (limitClause != null) children.Add(limitClause);
        return children;
    }
}

/// <summary>
/// Implementation of INSERT query node.
/// </summary>
public class InsertQueryNode : QueryNode, IInsertQueryNode
{
    public string TableName { get; }
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<IReadOnlyList<IExpressionNode>> Values { get; }
    public ISelectQueryNode? SelectQuery { get; }

    public InsertQueryNode(
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<IExpressionNode>> values,
        ISelectQueryNode? selectQuery,
        QueryPosition position)
        : base(QueryNodeType.InsertQuery, BuildChildren(values, selectQuery), tableName, position)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        Columns = columns ?? Array.Empty<string>();
        Values = values ?? Array.Empty<IReadOnlyList<IExpressionNode>>();
        SelectQuery = selectQuery;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(
        IReadOnlyList<IReadOnlyList<IExpressionNode>> values,
        ISelectQueryNode? selectQuery)
    {
        var children = new List<IQueryNode>();
        foreach (var valueList in values)
        {
            children.AddRange(valueList);
        }
        if (selectQuery != null) children.Add(selectQuery);
        return children;
    }
}

/// <summary>
/// Implementation of UPDATE query node.
/// </summary>
public class UpdateQueryNode : QueryNode, IUpdateQueryNode
{
    public string TableName { get; }
    public IReadOnlyList<IAssignmentNode> Assignments { get; }
    public IWhereClauseNode? WhereClause { get; }

    public UpdateQueryNode(
        string tableName,
        IReadOnlyList<IAssignmentNode> assignments,
        IWhereClauseNode? whereClause,
        QueryPosition position)
        : base(QueryNodeType.UpdateQuery, BuildChildren(assignments, whereClause), tableName, position)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        Assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
        WhereClause = whereClause;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(
        IReadOnlyList<IAssignmentNode> assignments,
        IWhereClauseNode? whereClause)
    {
        var children = new List<IQueryNode>(assignments);
        if (whereClause != null) children.Add(whereClause);
        return children;
    }
}

/// <summary>
/// Implementation of DELETE query node.
/// </summary>
public class DeleteQueryNode : QueryNode, IDeleteQueryNode
{
    public string TableName { get; }
    public IWhereClauseNode? WhereClause { get; }

    public DeleteQueryNode(string tableName, IWhereClauseNode? whereClause, QueryPosition position)
        : base(QueryNodeType.DeleteQuery, whereClause != null ? new[] { whereClause } : Array.Empty<IQueryNode>(), tableName, position)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        WhereClause = whereClause;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

#endregion

#region Clause Nodes

/// <summary>
/// Implementation of FROM clause node.
/// </summary>
public class FromClauseNode : QueryNode, IFromClauseNode
{
    public IReadOnlyList<ITableReferenceNode> TableReferences { get; }

    public FromClauseNode(IReadOnlyList<ITableReferenceNode> tableReferences, QueryPosition position)
        : base(QueryNodeType.FromClause, tableReferences.Cast<IQueryNode>().ToList(), null, position)
    {
        TableReferences = tableReferences ?? throw new ArgumentNullException(nameof(tableReferences));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of WHERE clause node.
/// </summary>
public class WhereClauseNode : QueryNode, IWhereClauseNode
{
    public IExpressionNode Condition { get; }

    public WhereClauseNode(IExpressionNode condition, QueryPosition position)
        : base(QueryNodeType.WhereClause, new[] { condition }, null, position)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of JOIN clause node.
/// </summary>
public class JoinClauseNode : QueryNode, IJoinClauseNode
{
    public JoinType JoinType { get; }
    public ITableReferenceNode Table { get; }
    public IExpressionNode? OnCondition { get; }
    public IReadOnlyList<string> UsingColumns { get; }

    public JoinClauseNode(
        JoinType joinType,
        ITableReferenceNode table,
        IExpressionNode? onCondition,
        IReadOnlyList<string> usingColumns,
        QueryPosition position)
        : base(QueryNodeType.JoinClause, BuildChildren(table, onCondition), joinType, position)
    {
        JoinType = joinType;
        Table = table ?? throw new ArgumentNullException(nameof(table));
        OnCondition = onCondition;
        UsingColumns = usingColumns ?? Array.Empty<string>();
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(ITableReferenceNode table, IExpressionNode? onCondition)
    {
        var children = new List<IQueryNode> { table };
        if (onCondition != null) children.Add(onCondition);
        return children;
    }
}

/// <summary>
/// Implementation of ORDER BY clause node.
/// </summary>
public class OrderByClauseNode : QueryNode, IOrderByClauseNode
{
    public IReadOnlyList<IOrderByItemNode> OrderByItems { get; }

    public OrderByClauseNode(IReadOnlyList<IOrderByItemNode> orderByItems, QueryPosition position)
        : base(QueryNodeType.OrderByClause, orderByItems.Cast<IQueryNode>().ToList(), null, position)
    {
        OrderByItems = orderByItems ?? throw new ArgumentNullException(nameof(orderByItems));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of GROUP BY clause node.
/// </summary>
public class GroupByClauseNode : QueryNode, IGroupByClauseNode
{
    public IReadOnlyList<IExpressionNode> GroupByExpressions { get; }

    public GroupByClauseNode(IReadOnlyList<IExpressionNode> groupByExpressions, QueryPosition position)
        : base(QueryNodeType.GroupByClause, groupByExpressions.Cast<IQueryNode>().ToList(), null, position)
    {
        GroupByExpressions = groupByExpressions ?? throw new ArgumentNullException(nameof(groupByExpressions));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of HAVING clause node.
/// </summary>
public class HavingClauseNode : QueryNode, IHavingClauseNode
{
    public IExpressionNode Condition { get; }

    public HavingClauseNode(IExpressionNode condition, QueryPosition position)
        : base(QueryNodeType.HavingClause, new[] { condition }, null, position)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of LIMIT clause node.
/// </summary>
public class LimitClauseNode : QueryNode, ILimitClauseNode
{
    public int? Limit { get; }
    public int? Offset { get; }

    public LimitClauseNode(int? limit, int? offset, QueryPosition position)
        : base(QueryNodeType.Unknown, Array.Empty<IQueryNode>(), null, position)
    {
        Limit = limit;
        Offset = offset;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

#endregion

#region Expression Nodes

/// <summary>
/// Implementation of literal node.
/// </summary>
public class LiteralNode : ExpressionNode, ILiteralNode
{
    public object? LiteralValue { get; }
    public LiteralType LiteralType { get; }

    public LiteralNode(object? value, LiteralType literalType, QueryPosition position)
        : base(QueryNodeType.Literal, Array.Empty<IQueryNode>(), value, position, value?.GetType())
    {
        LiteralValue = value;
        LiteralType = literalType;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitLiteral(this);
}

/// <summary>
/// Implementation of identifier node.
/// </summary>
public class IdentifierNode : ExpressionNode, IIdentifierNode
{
    public string Name { get; }
    public string? TableAlias { get; }
    public string? SchemaName { get; }

    public IdentifierNode(string name, string? tableAlias, string? schemaName, QueryPosition position)
        : base(QueryNodeType.Identifier, Array.Empty<IQueryNode>(), name, position)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TableAlias = tableAlias;
        SchemaName = schemaName;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitIdentifier(this);
}

/// <summary>
/// Implementation of function node.
/// </summary>
public class FunctionNode : ExpressionNode, IFunctionNode
{
    public string FunctionName { get; }
    public IReadOnlyList<IExpressionNode> Arguments { get; }
    public bool IsAggregate { get; }
    public bool IsWindow { get; }
    public IWindowSpecificationNode? WindowSpecification { get; }

    public FunctionNode(
        string functionName,
        IReadOnlyList<IExpressionNode> arguments,
        bool isAggregate,
        bool isWindow,
        IWindowSpecificationNode? windowSpecification,
        QueryPosition position)
        : base(QueryNodeType.Function, arguments.Cast<IQueryNode>().ToList(), functionName, position)
    {
        FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
        Arguments = arguments ?? Array.Empty<IExpressionNode>();
        IsAggregate = isAggregate;
        IsWindow = isWindow;
        WindowSpecification = windowSpecification;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitFunction(this);
}

/// <summary>
/// Implementation of binary operation node.
/// </summary>
public class BinaryOperationNode : ExpressionNode, IBinaryOperationNode
{
    public BinaryOperator Operator { get; }
    public IExpressionNode Left { get; }
    public IExpressionNode Right { get; }

    public BinaryOperationNode(BinaryOperator op, IExpressionNode left, IExpressionNode right, QueryPosition position)
        : base(QueryNodeType.BinaryOperation, new IQueryNode[] { left, right }, op, position)
    {
        Operator = op;
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitBinaryOperation(this);
}

/// <summary>
/// Implementation of unary operation node.
/// </summary>
public class UnaryOperationNode : ExpressionNode, IUnaryOperationNode
{
    public UnaryOperator Operator { get; }
    public IExpressionNode Operand { get; }

    public UnaryOperationNode(UnaryOperator op, IExpressionNode operand, QueryPosition position)
        : base(QueryNodeType.UnaryOperation, new[] { operand }, op, position)
    {
        Operator = op;
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitUnaryOperation(this);
}

#endregion

#region Other Nodes

/// <summary>
/// Implementation of SELECT item node.
/// </summary>
public class SelectItemNode : QueryNode, ISelectItemNode
{
    public IExpressionNode Expression { get; }
    public string? Alias { get; }
    public bool IsWildcard { get; }

    public SelectItemNode(IExpressionNode expression, string? alias, bool isWildcard, QueryPosition position)
        : base(QueryNodeType.Expression, new[] { expression }, null, position)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Alias = alias;
        IsWildcard = isWildcard;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of table reference node.
/// </summary>
public class TableReferenceNode : QueryNode, ITableReferenceNode
{
    public string TableName { get; }
    public string? Alias { get; }
    public string? SchemaName { get; }
    public ISelectQueryNode? SubQuery { get; }

    public TableReferenceNode(
        string tableName,
        string? alias,
        string? schemaName,
        ISelectQueryNode? subQuery,
        QueryPosition position)
        : base(QueryNodeType.Unknown, subQuery != null ? new[] { subQuery } : Array.Empty<IQueryNode>(), tableName, position)
    {
        TableName = tableName ?? string.Empty;
        Alias = alias;
        SchemaName = schemaName;
        SubQuery = subQuery;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of assignment node.
/// </summary>
public class AssignmentNode : QueryNode, IAssignmentNode
{
    public string ColumnName { get; }
    public new IExpressionNode Value { get; }

    public AssignmentNode(string columnName, IExpressionNode value, QueryPosition position)
        : base(QueryNodeType.Unknown, new[] { value }, columnName, position)
    {
        ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of ORDER BY item node.
/// </summary>
public class OrderByItemNode : QueryNode, IOrderByItemNode
{
    public IExpressionNode Expression { get; }
    public SortDirection Direction { get; }
    public NullsOrdering NullsOrdering { get; }

    public OrderByItemNode(
        IExpressionNode expression,
        SortDirection direction,
        NullsOrdering nullsOrdering,
        QueryPosition position)
        : base(QueryNodeType.Unknown, new[] { expression }, direction, position)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Direction = direction;
        NullsOrdering = nullsOrdering;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

/// <summary>
/// Implementation of window specification node.
/// </summary>
public class WindowSpecificationNode : QueryNode, IWindowSpecificationNode
{
    public IReadOnlyList<IExpressionNode> PartitionBy { get; }
    public IOrderByClauseNode? OrderBy { get; }
    public IWindowFrameNode? Frame { get; }

    public WindowSpecificationNode(
        IReadOnlyList<IExpressionNode> partitionBy,
        IOrderByClauseNode? orderBy,
        IWindowFrameNode? frame,
        QueryPosition position)
        : base(QueryNodeType.Unknown, BuildChildren(partitionBy, orderBy, frame), null, position)
    {
        PartitionBy = partitionBy ?? Array.Empty<IExpressionNode>();
        OrderBy = orderBy;
        Frame = frame;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(
        IReadOnlyList<IExpressionNode> partitionBy,
        IOrderByClauseNode? orderBy,
        IWindowFrameNode? frame)
    {
        var children = new List<IQueryNode>(partitionBy);
        if (orderBy != null) children.Add(orderBy);
        if (frame != null) children.Add(frame);
        return children;
    }
}

/// <summary>
/// Implementation of window frame node.
/// </summary>
public class WindowFrameNode : QueryNode, IWindowFrameNode
{
    public FrameType FrameType { get; }
    public IFrameBoundNode StartBound { get; }
    public IFrameBoundNode? EndBound { get; }

    public WindowFrameNode(
        FrameType frameType,
        IFrameBoundNode startBound,
        IFrameBoundNode? endBound,
        QueryPosition position)
        : base(QueryNodeType.Unknown, BuildChildren(startBound, endBound), frameType, position)
    {
        FrameType = frameType;
        StartBound = startBound ?? throw new ArgumentNullException(nameof(startBound));
        EndBound = endBound;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);

    private static IReadOnlyList<IQueryNode> BuildChildren(IFrameBoundNode startBound, IFrameBoundNode? endBound)
    {
        var children = new List<IQueryNode> { startBound };
        if (endBound != null) children.Add(endBound);
        return children;
    }
}

/// <summary>
/// Implementation of frame bound node.
/// </summary>
public class FrameBoundNode : QueryNode, IFrameBoundNode
{
    public FrameBoundType BoundType { get; }
    public IExpressionNode? Offset { get; }

    public FrameBoundNode(FrameBoundType boundType, IExpressionNode? offset, QueryPosition position)
        : base(QueryNodeType.Unknown, offset != null ? new[] { offset } : Array.Empty<IQueryNode>(), boundType, position)
    {
        BoundType = boundType;
        Offset = offset;
    }

    public override T Accept<T>(IQueryNodeVisitor<T> visitor) => visitor.VisitNode(this);
}

#endregion

#region Helper Classes

/// <summary>
/// Adapter to convert IQueryNodeVisitor to IQueryAstVisitor.
/// </summary>
internal class QueryAstVisitorAdapter<T> : IQueryNodeVisitor<T>
{
    private readonly IQueryAstVisitor<T> _astVisitor;

    public QueryAstVisitorAdapter(IQueryAstVisitor<T> astVisitor)
    {
        _astVisitor = astVisitor ?? throw new ArgumentNullException(nameof(astVisitor));
    }

    public T VisitNode(IQueryNode node)
    {
        return node switch
        {
            ISelectQueryNode selectNode => _astVisitor.VisitSelectQuery(selectNode),
            IInsertQueryNode insertNode => _astVisitor.VisitInsertQuery(insertNode),
            IUpdateQueryNode updateNode => _astVisitor.VisitUpdateQuery(updateNode),
            IDeleteQueryNode deleteNode => _astVisitor.VisitDeleteQuery(deleteNode),
            IFromClauseNode fromNode => _astVisitor.VisitFromClause(fromNode),
            IWhereClauseNode whereNode => _astVisitor.VisitWhereClause(whereNode),
            IJoinClauseNode joinNode => _astVisitor.VisitJoinClause(joinNode),
            IOrderByClauseNode orderByNode => _astVisitor.VisitOrderByClause(orderByNode),
            IGroupByClauseNode groupByNode => _astVisitor.VisitGroupByClause(groupByNode),
            IHavingClauseNode havingNode => _astVisitor.VisitHavingClause(havingNode),
            _ => throw new NotSupportedException($"Node type {node.GetType().Name} is not supported by AST visitor")
        };
    }

    public T VisitExpression(IExpressionNode node) => VisitNode(node);
    public T VisitLiteral(ILiteralNode node) => VisitNode(node);
    public T VisitIdentifier(IIdentifierNode node) => VisitNode(node);
    public T VisitFunction(IFunctionNode node) => VisitNode(node);
    public T VisitBinaryOperation(IBinaryOperationNode node) => VisitNode(node);
    public T VisitUnaryOperation(IUnaryOperationNode node) => VisitNode(node);
}

/// <summary>
/// Query analyzer for extracting metadata from AST.
/// </summary>
internal class QueryAnalyzer
{
    public QueryAnalysisResult Analyze(IQueryAst ast)
    {
        var tables = new HashSet<string>();
        var columns = new HashSet<string>();

        AnalyzeNode(ast.Root, tables, columns);

        return new QueryAnalysisResult(tables.ToList(), columns.ToList());
    }

    private void AnalyzeNode(IQueryNode node, HashSet<string> tables, HashSet<string> columns)
    {
        switch (node)
        {
            case ITableReferenceNode tableRef:
                if (!string.IsNullOrEmpty(tableRef.TableName))
                    tables.Add(tableRef.TableName);
                break;

            case IIdentifierNode identifier:
                columns.Add(identifier.Name);
                if (!string.IsNullOrEmpty(identifier.TableAlias))
                    tables.Add(identifier.TableAlias);
                break;
        }

        foreach (var child in node.Children)
        {
            AnalyzeNode(child, tables, columns);
        }
    }
}

/// <summary>
/// Result of query analysis.
/// </summary>
internal class QueryAnalysisResult
{
    public IReadOnlyList<string> ReferencedTables { get; }
    public IReadOnlyList<string> ReferencedColumns { get; }

    public QueryAnalysisResult(IReadOnlyList<string> tables, IReadOnlyList<string> columns)
    {
        ReferencedTables = tables ?? Array.Empty<string>();
        ReferencedColumns = columns ?? Array.Empty<string>();
    }
}

#endregion
