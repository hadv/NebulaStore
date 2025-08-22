using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Recursive descent parser for the query language.
/// Builds an abstract syntax tree from a sequence of tokens.
/// </summary>
public class QueryParser
{
    private readonly IReadOnlyList<QueryToken> _tokens;
    private int _position;
    private QueryToken _currentToken;

    public QueryParser(IReadOnlyList<QueryToken> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _position = 0;
        _currentToken = _tokens.Count > 0 ? _tokens[0] : new QueryToken(TokenType.EndOfFile, string.Empty, new QueryPosition());
    }

    /// <summary>
    /// Parses the tokens into a query AST.
    /// </summary>
    /// <returns>The parsed query AST</returns>
    public IQueryAst Parse()
    {
        var root = ParseQuery();
        ExpectToken(TokenType.EndOfFile);
        
        return new QueryAst(root);
    }

    private IQueryNode ParseQuery()
    {
        return _currentToken.Type switch
        {
            TokenType.Select => ParseSelectQuery(),
            TokenType.Insert => ParseInsertQuery(),
            TokenType.Update => ParseUpdateQuery(),
            TokenType.Delete => ParseDeleteQuery(),
            _ => throw new QueryParserException($"Expected query statement, found '{_currentToken.Value}'", _currentToken.Position)
        };
    }

    private ISelectQueryNode ParseSelectQuery()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Select);

        var isDistinct = false;
        if (_currentToken.Is(TokenType.Distinct))
        {
            isDistinct = true;
            NextToken();
        }

        var selectItems = ParseSelectItems();
        
        IFromClauseNode? fromClause = null;
        if (_currentToken.Is(TokenType.From))
        {
            fromClause = ParseFromClause();
        }

        var joinClauses = new List<IJoinClauseNode>();
        while (_currentToken.IsOneOf(TokenType.Join, TokenType.Inner, TokenType.Left, TokenType.Right, TokenType.Full, TokenType.Cross))
        {
            joinClauses.Add(ParseJoinClause());
        }

        IWhereClauseNode? whereClause = null;
        if (_currentToken.Is(TokenType.Where))
        {
            whereClause = ParseWhereClause();
        }

        IGroupByClauseNode? groupByClause = null;
        if (_currentToken.Is(TokenType.Group))
        {
            groupByClause = ParseGroupByClause();
        }

        IHavingClauseNode? havingClause = null;
        if (_currentToken.Is(TokenType.Having))
        {
            havingClause = ParseHavingClause();
        }

        IOrderByClauseNode? orderByClause = null;
        if (_currentToken.Is(TokenType.Order))
        {
            orderByClause = ParseOrderByClause();
        }

        ILimitClauseNode? limitClause = null;
        if (_currentToken.Is(TokenType.Limit))
        {
            limitClause = ParseLimitClause();
        }

        return new SelectQueryNode(
            selectItems,
            fromClause,
            whereClause,
            joinClauses,
            groupByClause,
            havingClause,
            orderByClause,
            limitClause,
            isDistinct,
            startPosition);
    }

    private IInsertQueryNode ParseInsertQuery()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Insert);
        ExpectToken(TokenType.Into);

        var tableName = ExpectIdentifier();
        
        var columns = new List<string>();
        if (_currentToken.Is(TokenType.LeftParen))
        {
            NextToken();
            columns.Add(ExpectIdentifier());
            
            while (_currentToken.Is(TokenType.Comma))
            {
                NextToken();
                columns.Add(ExpectIdentifier());
            }
            
            ExpectToken(TokenType.RightParen);
        }

        ISelectQueryNode? selectQuery = null;
        var values = new List<IReadOnlyList<IExpressionNode>>();

        if (_currentToken.Is(TokenType.Select))
        {
            selectQuery = ParseSelectQuery();
        }
        else
        {
            ExpectToken(TokenType.Values);
            values.Add(ParseValuesList());
            
            while (_currentToken.Is(TokenType.Comma))
            {
                NextToken();
                values.Add(ParseValuesList());
            }
        }

        return new InsertQueryNode(tableName, columns, values, selectQuery, startPosition);
    }

    private IUpdateQueryNode ParseUpdateQuery()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Update);

        var tableName = ExpectIdentifier();
        ExpectToken(TokenType.Set);

        var assignments = new List<IAssignmentNode>();
        assignments.Add(ParseAssignment());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            assignments.Add(ParseAssignment());
        }

        IWhereClauseNode? whereClause = null;
        if (_currentToken.Is(TokenType.Where))
        {
            whereClause = ParseWhereClause();
        }

        return new UpdateQueryNode(tableName, assignments, whereClause, startPosition);
    }

    private IDeleteQueryNode ParseDeleteQuery()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Delete);
        ExpectToken(TokenType.From);

        var tableName = ExpectIdentifier();

        IWhereClauseNode? whereClause = null;
        if (_currentToken.Is(TokenType.Where))
        {
            whereClause = ParseWhereClause();
        }

        return new DeleteQueryNode(tableName, whereClause, startPosition);
    }

    private IReadOnlyList<ISelectItemNode> ParseSelectItems()
    {
        var items = new List<ISelectItemNode>();
        items.Add(ParseSelectItem());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            items.Add(ParseSelectItem());
        }
        
        return items;
    }

    private ISelectItemNode ParseSelectItem()
    {
        var startPosition = _currentToken.Position;
        
        if (_currentToken.Is(TokenType.Asterisk))
        {
            NextToken();
            return new SelectItemNode(new LiteralNode("*", LiteralType.String, startPosition), null, true, startPosition);
        }

        var expression = ParseExpression();
        
        string? alias = null;
        if (_currentToken.Is(TokenType.As))
        {
            NextToken();
            alias = ExpectIdentifier();
        }
        else if (_currentToken.Is(TokenType.Identifier))
        {
            alias = _currentToken.Value;
            NextToken();
        }

        return new SelectItemNode(expression, alias, false, startPosition);
    }

    private IFromClauseNode ParseFromClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.From);

        var tableReferences = new List<ITableReferenceNode>();
        tableReferences.Add(ParseTableReference());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            tableReferences.Add(ParseTableReference());
        }

        return new FromClauseNode(tableReferences, startPosition);
    }

    private ITableReferenceNode ParseTableReference()
    {
        var startPosition = _currentToken.Position;
        
        if (_currentToken.Is(TokenType.LeftParen))
        {
            NextToken();
            var subQuery = ParseSelectQuery();
            ExpectToken(TokenType.RightParen);
            
            string? alias = null;
            if (_currentToken.Is(TokenType.As))
            {
                NextToken();
                alias = ExpectIdentifier();
            }
            else if (_currentToken.Is(TokenType.Identifier))
            {
                alias = _currentToken.Value;
                NextToken();
            }
            
            return new TableReferenceNode(string.Empty, alias, null, subQuery, startPosition);
        }

        var tableName = ExpectIdentifier();
        string? tableAlias = null;
        
        if (_currentToken.Is(TokenType.As))
        {
            NextToken();
            tableAlias = ExpectIdentifier();
        }
        else if (_currentToken.Is(TokenType.Identifier))
        {
            tableAlias = _currentToken.Value;
            NextToken();
        }

        return new TableReferenceNode(tableName, tableAlias, null, null, startPosition);
    }

    private IJoinClauseNode ParseJoinClause()
    {
        var startPosition = _currentToken.Position;
        var joinType = JoinType.Inner;

        if (_currentToken.Is(TokenType.Inner))
        {
            NextToken();
            ExpectToken(TokenType.Join);
        }
        else if (_currentToken.Is(TokenType.Left))
        {
            joinType = JoinType.LeftOuter;
            NextToken();
            if (_currentToken.Is(TokenType.Outer))
                NextToken();
            ExpectToken(TokenType.Join);
        }
        else if (_currentToken.Is(TokenType.Right))
        {
            joinType = JoinType.RightOuter;
            NextToken();
            if (_currentToken.Is(TokenType.Outer))
                NextToken();
            ExpectToken(TokenType.Join);
        }
        else if (_currentToken.Is(TokenType.Full))
        {
            joinType = JoinType.FullOuter;
            NextToken();
            if (_currentToken.Is(TokenType.Outer))
                NextToken();
            ExpectToken(TokenType.Join);
        }
        else if (_currentToken.Is(TokenType.Cross))
        {
            joinType = JoinType.Cross;
            NextToken();
            ExpectToken(TokenType.Join);
        }
        else
        {
            ExpectToken(TokenType.Join);
        }

        var table = ParseTableReference();
        
        IExpressionNode? onCondition = null;
        var usingColumns = new List<string>();
        
        if (_currentToken.Is(TokenType.On))
        {
            NextToken();
            onCondition = ParseExpression();
        }
        else if (_currentToken.Is(TokenType.Using))
        {
            NextToken();
            ExpectToken(TokenType.LeftParen);
            usingColumns.Add(ExpectIdentifier());
            
            while (_currentToken.Is(TokenType.Comma))
            {
                NextToken();
                usingColumns.Add(ExpectIdentifier());
            }
            
            ExpectToken(TokenType.RightParen);
        }

        return new JoinClauseNode(joinType, table, onCondition, usingColumns, startPosition);
    }

    private IWhereClauseNode ParseWhereClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Where);
        var condition = ParseExpression();
        return new WhereClauseNode(condition, startPosition);
    }

    private IGroupByClauseNode ParseGroupByClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Group);
        ExpectToken(TokenType.By);

        var expressions = new List<IExpressionNode>();
        expressions.Add(ParseExpression());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            expressions.Add(ParseExpression());
        }

        return new GroupByClauseNode(expressions, startPosition);
    }

    private IHavingClauseNode ParseHavingClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Having);
        var condition = ParseExpression();
        return new HavingClauseNode(condition, startPosition);
    }

    private IOrderByClauseNode ParseOrderByClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Order);
        ExpectToken(TokenType.By);

        var items = new List<IOrderByItemNode>();
        items.Add(ParseOrderByItem());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            items.Add(ParseOrderByItem());
        }

        return new OrderByClauseNode(items, startPosition);
    }

    private IOrderByItemNode ParseOrderByItem()
    {
        var startPosition = _currentToken.Position;
        var expression = ParseExpression();
        
        var direction = SortDirection.Ascending;
        if (_currentToken.Is(TokenType.Asc))
        {
            NextToken();
        }
        else if (_currentToken.Is(TokenType.Desc))
        {
            direction = SortDirection.Descending;
            NextToken();
        }

        return new OrderByItemNode(expression, direction, NullsOrdering.Default, startPosition);
    }

    private ILimitClauseNode ParseLimitClause()
    {
        var startPosition = _currentToken.Position;
        ExpectToken(TokenType.Limit);
        
        var limitToken = ExpectToken(TokenType.IntegerLiteral);
        var limit = int.Parse(limitToken.Value);
        
        int? offset = null;
        if (_currentToken.Is(TokenType.Offset))
        {
            NextToken();
            var offsetToken = ExpectToken(TokenType.IntegerLiteral);
            offset = int.Parse(offsetToken.Value);
        }

        return new LimitClauseNode(limit, offset, startPosition);
    }

    private IAssignmentNode ParseAssignment()
    {
        var startPosition = _currentToken.Position;
        var columnName = ExpectIdentifier();
        ExpectToken(TokenType.Equal);
        var value = ParseExpression();
        return new AssignmentNode(columnName, value, startPosition);
    }

    private IReadOnlyList<IExpressionNode> ParseValuesList()
    {
        ExpectToken(TokenType.LeftParen);
        
        var values = new List<IExpressionNode>();
        values.Add(ParseExpression());
        
        while (_currentToken.Is(TokenType.Comma))
        {
            NextToken();
            values.Add(ParseExpression());
        }
        
        ExpectToken(TokenType.RightParen);
        return values;
    }

    private IExpressionNode ParseExpression()
    {
        return ParseOrExpression();
    }

    private IExpressionNode ParseOrExpression()
    {
        var left = ParseAndExpression();
        
        while (_currentToken.Is(TokenType.Or))
        {
            var operatorPosition = _currentToken.Position;
            NextToken();
            var right = ParseAndExpression();
            left = new BinaryOperationNode(BinaryOperator.Or, left, right, operatorPosition);
        }
        
        return left;
    }

    private IExpressionNode ParseAndExpression()
    {
        var left = ParseNotExpression();
        
        while (_currentToken.Is(TokenType.And))
        {
            var operatorPosition = _currentToken.Position;
            NextToken();
            var right = ParseNotExpression();
            left = new BinaryOperationNode(BinaryOperator.And, left, right, operatorPosition);
        }
        
        return left;
    }

    private IExpressionNode ParseNotExpression()
    {
        if (_currentToken.Is(TokenType.Not))
        {
            var operatorPosition = _currentToken.Position;
            NextToken();
            var operand = ParseNotExpression();
            return new UnaryOperationNode(UnaryOperator.Not, operand, operatorPosition);
        }
        
        return ParseComparisonExpression();
    }

    private IExpressionNode ParseComparisonExpression()
    {
        var left = ParseArithmeticExpression();
        
        if (_currentToken.Type.IsComparisonOperator())
        {
            var operatorPosition = _currentToken.Position;
            var op = _currentToken.Type switch
            {
                TokenType.Equal => BinaryOperator.Equal,
                TokenType.NotEqual => BinaryOperator.NotEqual,
                TokenType.LessThan => BinaryOperator.LessThan,
                TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                TokenType.GreaterThan => BinaryOperator.GreaterThan,
                TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                TokenType.Like => BinaryOperator.Like,
                TokenType.In => BinaryOperator.In,
                _ => throw new QueryParserException($"Unsupported comparison operator: {_currentToken.Type}", _currentToken.Position)
            };
            
            NextToken();
            var right = ParseArithmeticExpression();
            return new BinaryOperationNode(op, left, right, operatorPosition);
        }
        
        return left;
    }

    private IExpressionNode ParseArithmeticExpression()
    {
        var left = ParseTermExpression();
        
        while (_currentToken.IsOneOf(TokenType.Plus, TokenType.Minus))
        {
            var operatorPosition = _currentToken.Position;
            var op = _currentToken.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            NextToken();
            var right = ParseTermExpression();
            left = new BinaryOperationNode(op, left, right, operatorPosition);
        }
        
        return left;
    }

    private IExpressionNode ParseTermExpression()
    {
        var left = ParseFactorExpression();
        
        while (_currentToken.IsOneOf(TokenType.Multiply, TokenType.Divide, TokenType.Modulo))
        {
            var operatorPosition = _currentToken.Position;
            var op = _currentToken.Type switch
            {
                TokenType.Multiply => BinaryOperator.Multiply,
                TokenType.Divide => BinaryOperator.Divide,
                TokenType.Modulo => BinaryOperator.Modulo,
                _ => throw new QueryParserException($"Unexpected operator: {_currentToken.Type}", _currentToken.Position)
            };
            NextToken();
            var right = ParseFactorExpression();
            left = new BinaryOperationNode(op, left, right, operatorPosition);
        }
        
        return left;
    }

    private IExpressionNode ParseFactorExpression()
    {
        if (_currentToken.IsOneOf(TokenType.Plus, TokenType.Minus))
        {
            var operatorPosition = _currentToken.Position;
            var op = _currentToken.Type == TokenType.Plus ? UnaryOperator.Plus : UnaryOperator.Minus;
            NextToken();
            var operand = ParseFactorExpression();
            return new UnaryOperationNode(op, operand, operatorPosition);
        }
        
        return ParsePrimaryExpression();
    }

    private IExpressionNode ParsePrimaryExpression()
    {
        var startPosition = _currentToken.Position;
        
        return _currentToken.Type switch
        {
            TokenType.StringLiteral => ParseStringLiteral(),
            TokenType.IntegerLiteral => ParseIntegerLiteral(),
            TokenType.DecimalLiteral => ParseDecimalLiteral(),
            TokenType.True or TokenType.False => ParseBooleanLiteral(),
            TokenType.Null => ParseNullLiteral(),
            TokenType.Identifier => ParseIdentifierOrFunction(),
            TokenType.LeftParen => ParseParenthesizedExpression(),
            // Handle aggregate function keywords as identifiers
            TokenType.Count or TokenType.Sum or TokenType.Avg or TokenType.Min or TokenType.Max => ParseIdentifierOrFunction(),
            _ => throw new QueryParserException($"Unexpected token in expression: {_currentToken.Type}", _currentToken.Position)
        };
    }

    private IExpressionNode ParseStringLiteral()
    {
        var value = _currentToken.Value;
        var position = _currentToken.Position;
        NextToken();
        return new LiteralNode(value, LiteralType.String, position);
    }

    private IExpressionNode ParseIntegerLiteral()
    {
        var value = int.Parse(_currentToken.Value);
        var position = _currentToken.Position;
        NextToken();
        return new LiteralNode(value, LiteralType.Integer, position);
    }

    private IExpressionNode ParseDecimalLiteral()
    {
        var value = decimal.Parse(_currentToken.Value);
        var position = _currentToken.Position;
        NextToken();
        return new LiteralNode(value, LiteralType.Decimal, position);
    }

    private IExpressionNode ParseBooleanLiteral()
    {
        var value = _currentToken.Type == TokenType.True;
        var position = _currentToken.Position;
        NextToken();
        return new LiteralNode(value, LiteralType.Boolean, position);
    }

    private IExpressionNode ParseNullLiteral()
    {
        var position = _currentToken.Position;
        NextToken();
        return new LiteralNode(null, LiteralType.Null, position);
    }

    private IExpressionNode ParseIdentifierOrFunction()
    {
        var name = _currentToken.Value;
        var position = _currentToken.Position;
        NextToken();
        
        if (_currentToken.Is(TokenType.LeftParen))
        {
            // Function call
            NextToken();
            var arguments = new List<IExpressionNode>();
            
            if (!_currentToken.Is(TokenType.RightParen))
            {
                // Handle special case of COUNT(*) and similar aggregate functions
                if (_currentToken.Is(TokenType.Asterisk))
                {
                    var asteriskPosition = _currentToken.Position;
                    NextToken();
                    arguments.Add(new LiteralNode("*", LiteralType.String, asteriskPosition));
                }
                else
                {
                    arguments.Add(ParseExpression());
                }

                while (_currentToken.Is(TokenType.Comma))
                {
                    NextToken();
                    if (_currentToken.Is(TokenType.Asterisk))
                    {
                        var asteriskPosition = _currentToken.Position;
                        NextToken();
                        arguments.Add(new LiteralNode("*", LiteralType.String, asteriskPosition));
                    }
                    else
                    {
                        arguments.Add(ParseExpression());
                    }
                }
            }
            
            ExpectToken(TokenType.RightParen);
            
            var isAggregate = name.ToUpperInvariant() switch
            {
                "COUNT" or "SUM" or "AVG" or "MIN" or "MAX" => true,
                _ => false
            };
            
            return new FunctionNode(name, arguments, isAggregate, false, null, position);
        }
        else if (_currentToken.Is(TokenType.Dot))
        {
            // Qualified identifier
            NextToken();
            var columnName = ExpectIdentifier();
            return new IdentifierNode(columnName, name, null, position);
        }
        else
        {
            // Simple identifier
            return new IdentifierNode(name, null, null, position);
        }
    }

    private IExpressionNode ParseParenthesizedExpression()
    {
        ExpectToken(TokenType.LeftParen);
        var expression = ParseExpression();
        ExpectToken(TokenType.RightParen);
        return expression;
    }

    private void NextToken()
    {
        if (_position < _tokens.Count - 1)
        {
            _position++;
            _currentToken = _tokens[_position];
        }
    }

    private QueryToken ExpectToken(TokenType expectedType)
    {
        if (!_currentToken.Is(expectedType))
        {
            throw new QueryParserException($"Expected {expectedType}, found {_currentToken.Type}", _currentToken.Position);
        }
        
        var token = _currentToken;
        NextToken();
        return token;
    }

    private string ExpectIdentifier()
    {
        var token = ExpectToken(TokenType.Identifier);
        return token.Value;
    }
}

/// <summary>
/// Exception thrown when parsing fails.
/// </summary>
public class QueryParserException : QuerySyntaxException
{
    public QueryParserException(string message, QueryPosition position)
        : base(message, position, QueryErrorType.SyntaxError)
    {
    }

    public QueryParserException(string message, QueryPosition position, Exception innerException)
        : base(message, position, innerException, QueryErrorType.SyntaxError)
    {
    }
}
