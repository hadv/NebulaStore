using System;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Represents a token in the query language.
/// </summary>
public readonly struct QueryToken
{
    /// <summary>
    /// Gets the token type.
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Gets the token value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the position of the token in the source.
    /// </summary>
    public QueryPosition Position { get; }

    /// <summary>
    /// Initializes a new instance of the QueryToken struct.
    /// </summary>
    /// <param name="type">The token type</param>
    /// <param name="value">The token value</param>
    /// <param name="position">The token position</param>
    public QueryToken(TokenType type, string value, QueryPosition position)
    {
        Type = type;
        Value = value ?? string.Empty;
        Position = position;
    }

    /// <summary>
    /// Returns a string representation of the token.
    /// </summary>
    public override string ToString() => $"{Type}: '{Value}' at {Position}";

    /// <summary>
    /// Checks if the token is of the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the token is of the specified type</returns>
    public bool Is(TokenType type) => Type == type;

    /// <summary>
    /// Checks if the token is one of the specified types.
    /// </summary>
    /// <param name="types">The types to check</param>
    /// <returns>True if the token is one of the specified types</returns>
    public bool IsOneOf(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Type == type) return true;
        }
        return false;
    }
}

/// <summary>
/// Token types for the query language.
/// </summary>
public enum TokenType
{
    // Literals
    StringLiteral,
    IntegerLiteral,
    DecimalLiteral,
    
    // Keywords - Query Types
    Select,
    Insert,
    Update,
    Delete,
    
    // Keywords - Clauses
    From,
    Where,
    Join,
    Inner,
    Left,
    Right,
    Full,
    Outer,
    Cross,
    On,
    Using,
    Group,
    By,
    Having,
    Order,
    Asc,
    Desc,
    Limit,
    Offset,
    Distinct,
    
    // Keywords - DML
    Into,
    Values,
    Set,
    
    // Keywords - Logical
    And,
    Or,
    Not,
    In,
    Like,
    Is,
    Between,
    
    // Keywords - Literals
    Null,
    True,
    False,
    
    // Keywords - Other
    As,
    Case,
    When,
    Then,
    Else,
    End,
    
    // Keywords - Aggregate Functions
    Count,
    Sum,
    Avg,
    Min,
    Max,
    
    // Keywords - Window Functions
    Over,
    Partition,
    Window,
    Rows,
    Range,
    Unbounded,
    Preceding,
    Following,
    Current,
    Row,
    
    // Operators
    Plus,
    Minus,
    Multiply,
    Divide,
    Modulo,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    
    // Punctuation
    LeftParen,
    RightParen,
    Comma,
    Semicolon,
    Dot,
    Asterisk,
    
    // Identifiers
    Identifier,
    
    // Special
    EndOfFile,
    Unknown
}

/// <summary>
/// Extension methods for TokenType.
/// </summary>
public static class TokenTypeExtensions
{
    /// <summary>
    /// Checks if the token type is a keyword.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is a keyword</returns>
    public static bool IsKeyword(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Select or TokenType.Insert or TokenType.Update or TokenType.Delete or
            TokenType.From or TokenType.Where or TokenType.Join or TokenType.Inner or
            TokenType.Left or TokenType.Right or TokenType.Full or TokenType.Outer or
            TokenType.Cross or TokenType.On or TokenType.Using or TokenType.Group or
            TokenType.By or TokenType.Having or TokenType.Order or TokenType.Asc or
            TokenType.Desc or TokenType.Limit or TokenType.Offset or TokenType.Distinct or
            TokenType.Into or TokenType.Values or TokenType.Set or TokenType.And or
            TokenType.Or or TokenType.Not or TokenType.In or TokenType.Like or
            TokenType.Is or TokenType.Between or TokenType.Null or TokenType.True or
            TokenType.False or TokenType.As or TokenType.Case or TokenType.When or
            TokenType.Then or TokenType.Else or TokenType.End or TokenType.Count or
            TokenType.Sum or TokenType.Avg or TokenType.Min or TokenType.Max or
            TokenType.Over or TokenType.Partition or TokenType.Window or TokenType.Rows or
            TokenType.Range or TokenType.Unbounded or TokenType.Preceding or TokenType.Following or
            TokenType.Current or TokenType.Row => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is a literal.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is a literal</returns>
    public static bool IsLiteral(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.StringLiteral or TokenType.IntegerLiteral or TokenType.DecimalLiteral or
            TokenType.Null or TokenType.True or TokenType.False => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is an operator.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is an operator</returns>
    public static bool IsOperator(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Plus or TokenType.Minus or TokenType.Multiply or TokenType.Divide or
            TokenType.Modulo or TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or
            TokenType.LessThanOrEqual or TokenType.GreaterThan or TokenType.GreaterThanOrEqual => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is a comparison operator.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is a comparison operator</returns>
    public static bool IsComparisonOperator(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or
            TokenType.LessThanOrEqual or TokenType.GreaterThan or TokenType.GreaterThanOrEqual or
            TokenType.Like or TokenType.In or TokenType.Is or TokenType.Between => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is an arithmetic operator.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is an arithmetic operator</returns>
    public static bool IsArithmeticOperator(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Plus or TokenType.Minus or TokenType.Multiply or TokenType.Divide or
            TokenType.Modulo => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is a logical operator.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is a logical operator</returns>
    public static bool IsLogicalOperator(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.And or TokenType.Or or TokenType.Not => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is a join type keyword.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is a join type keyword</returns>
    public static bool IsJoinType(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Inner or TokenType.Left or TokenType.Right or TokenType.Full or
            TokenType.Cross => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if the token type is an aggregate function.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>True if the token type is an aggregate function</returns>
    public static bool IsAggregateFunction(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Count or TokenType.Sum or TokenType.Avg or TokenType.Min or TokenType.Max => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the precedence of an operator token type.
    /// Higher numbers indicate higher precedence.
    /// </summary>
    /// <param name="tokenType">The token type</param>
    /// <returns>The precedence level</returns>
    public static int GetOperatorPrecedence(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Or => 1,
            TokenType.And => 2,
            TokenType.Not => 3,
            TokenType.Equal or TokenType.NotEqual or TokenType.LessThan or
            TokenType.LessThanOrEqual or TokenType.GreaterThan or TokenType.GreaterThanOrEqual or
            TokenType.Like or TokenType.In or TokenType.Is or TokenType.Between => 4,
            TokenType.Plus or TokenType.Minus => 5,
            TokenType.Multiply or TokenType.Divide or TokenType.Modulo => 6,
            _ => 0
        };
    }
}
