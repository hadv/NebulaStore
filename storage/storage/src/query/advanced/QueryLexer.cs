using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Lexical analyzer for the query language.
/// Tokenizes query strings into a sequence of tokens for parsing.
/// </summary>
public class QueryLexer
{
    private readonly string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly List<QueryToken> _tokens = new();

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "SELECT", TokenType.Select },
        { "FROM", TokenType.From },
        { "WHERE", TokenType.Where },
        { "JOIN", TokenType.Join },
        { "INNER", TokenType.Inner },
        { "LEFT", TokenType.Left },
        { "RIGHT", TokenType.Right },
        { "FULL", TokenType.Full },
        { "OUTER", TokenType.Outer },
        { "CROSS", TokenType.Cross },
        { "ON", TokenType.On },
        { "USING", TokenType.Using },
        { "GROUP", TokenType.Group },
        { "BY", TokenType.By },
        { "HAVING", TokenType.Having },
        { "ORDER", TokenType.Order },
        { "ASC", TokenType.Asc },
        { "DESC", TokenType.Desc },
        { "LIMIT", TokenType.Limit },
        { "OFFSET", TokenType.Offset },
        { "DISTINCT", TokenType.Distinct },
        { "INSERT", TokenType.Insert },
        { "INTO", TokenType.Into },
        { "VALUES", TokenType.Values },
        { "UPDATE", TokenType.Update },
        { "SET", TokenType.Set },
        { "DELETE", TokenType.Delete },
        { "AND", TokenType.And },
        { "OR", TokenType.Or },
        { "NOT", TokenType.Not },
        { "IN", TokenType.In },
        { "LIKE", TokenType.Like },
        { "IS", TokenType.Is },
        { "NULL", TokenType.Null },
        { "TRUE", TokenType.True },
        { "FALSE", TokenType.False },
        { "AS", TokenType.As },
        { "COUNT", TokenType.Count },
        { "SUM", TokenType.Sum },
        { "AVG", TokenType.Avg },
        { "MIN", TokenType.Min },
        { "MAX", TokenType.Max },
        { "CASE", TokenType.Case },
        { "WHEN", TokenType.When },
        { "THEN", TokenType.Then },
        { "ELSE", TokenType.Else },
        { "END", TokenType.End },
        { "OVER", TokenType.Over },
        { "PARTITION", TokenType.Partition },
        { "WINDOW", TokenType.Window },
        { "ROWS", TokenType.Rows },
        { "RANGE", TokenType.Range },
        { "BETWEEN", TokenType.Between },
        { "UNBOUNDED", TokenType.Unbounded },
        { "PRECEDING", TokenType.Preceding },
        { "FOLLOWING", TokenType.Following },
        { "CURRENT", TokenType.Current },
        { "ROW", TokenType.Row }
    };

    public QueryLexer(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <summary>
    /// Tokenizes the input string into a list of tokens.
    /// </summary>
    /// <returns>List of tokens</returns>
    public IReadOnlyList<QueryToken> Tokenize()
    {
        _tokens.Clear();
        _position = 0;
        _line = 1;
        _column = 1;

        while (_position < _input.Length)
        {
            SkipWhitespace();
            
            if (_position >= _input.Length)
                break;

            var token = ReadNextToken();
            if (token.HasValue)
            {
                _tokens.Add(token.Value);
            }
        }

        _tokens.Add(new QueryToken(TokenType.EndOfFile, string.Empty, GetCurrentPosition()));
        return _tokens;
    }

    private QueryToken? ReadNextToken()
    {
        var startPosition = GetCurrentPosition();
        var ch = _input[_position];

        return ch switch
        {
            '(' => CreateToken(TokenType.LeftParen, "(", startPosition),
            ')' => CreateToken(TokenType.RightParen, ")", startPosition),
            ',' => CreateToken(TokenType.Comma, ",", startPosition),
            ';' => CreateToken(TokenType.Semicolon, ";", startPosition),
            '.' => CreateToken(TokenType.Dot, ".", startPosition),
            '*' => CreateToken(TokenType.Asterisk, "*", startPosition),
            '+' => CreateToken(TokenType.Plus, "+", startPosition),
            '-' => ReadMinusOrComment(startPosition),
            '/' => CreateToken(TokenType.Divide, "/", startPosition),
            '%' => CreateToken(TokenType.Modulo, "%", startPosition),
            '=' => CreateToken(TokenType.Equal, "=", startPosition),
            '<' => ReadLessThanOrEqual(startPosition),
            '>' => ReadGreaterThanOrEqual(startPosition),
            '!' => ReadNotEqual(startPosition),
            '\'' => ReadStringLiteral(startPosition),
            '"' => ReadQuotedIdentifier(startPosition),
            '[' => ReadBracketedIdentifier(startPosition),
            _ when char.IsDigit(ch) => ReadNumericLiteral(startPosition),
            _ when char.IsLetter(ch) || ch == '_' => ReadIdentifierOrKeyword(startPosition),
            _ => throw new QuerySyntaxException($"Unexpected character '{ch}'", startPosition)
        };
    }

    private QueryToken CreateToken(TokenType type, string value, QueryPosition position)
    {
        _position++;
        _column++;
        return new QueryToken(type, value, position);
    }

    private QueryToken? ReadMinusOrComment(QueryPosition startPosition)
    {
        if (_position + 1 < _input.Length && _input[_position + 1] == '-')
        {
            // Single-line comment
            SkipSingleLineComment();
            return null;
        }
        
        return CreateToken(TokenType.Minus, "-", startPosition);
    }

    private QueryToken ReadLessThanOrEqual(QueryPosition startPosition)
    {
        if (_position + 1 < _input.Length && _input[_position + 1] == '=')
        {
            _position += 2;
            _column += 2;
            return new QueryToken(TokenType.LessThanOrEqual, "<=", startPosition);
        }
        
        return CreateToken(TokenType.LessThan, "<", startPosition);
    }

    private QueryToken ReadGreaterThanOrEqual(QueryPosition startPosition)
    {
        if (_position + 1 < _input.Length && _input[_position + 1] == '=')
        {
            _position += 2;
            _column += 2;
            return new QueryToken(TokenType.GreaterThanOrEqual, ">=", startPosition);
        }
        
        return CreateToken(TokenType.GreaterThan, ">", startPosition);
    }

    private QueryToken ReadNotEqual(QueryPosition startPosition)
    {
        if (_position + 1 < _input.Length && _input[_position + 1] == '=')
        {
            _position += 2;
            _column += 2;
            return new QueryToken(TokenType.NotEqual, "!=", startPosition);
        }
        
        throw new QuerySyntaxException("Expected '=' after '!'", startPosition);
    }

    private QueryToken ReadStringLiteral(QueryPosition startPosition)
    {
        var sb = new StringBuilder();
        _position++; // Skip opening quote
        _column++;

        while (_position < _input.Length)
        {
            var ch = _input[_position];
            
            if (ch == '\'')
            {
                if (_position + 1 < _input.Length && _input[_position + 1] == '\'')
                {
                    // Escaped quote
                    sb.Append('\'');
                    _position += 2;
                    _column += 2;
                }
                else
                {
                    // End of string
                    _position++;
                    _column++;
                    break;
                }
            }
            else if (ch == '\\' && _position + 1 < _input.Length)
            {
                // Escape sequence
                _position++;
                _column++;
                var escaped = _input[_position];
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    _ => escaped
                });
                _position++;
                _column++;
            }
            else
            {
                sb.Append(ch);
                _position++;
                if (ch == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
        }

        return new QueryToken(TokenType.StringLiteral, sb.ToString(), startPosition);
    }

    private QueryToken ReadQuotedIdentifier(QueryPosition startPosition)
    {
        var sb = new StringBuilder();
        _position++; // Skip opening quote
        _column++;

        while (_position < _input.Length && _input[_position] != '"')
        {
            var ch = _input[_position];
            sb.Append(ch);
            _position++;
            if (ch == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }

        if (_position < _input.Length)
        {
            _position++; // Skip closing quote
            _column++;
        }

        return new QueryToken(TokenType.Identifier, sb.ToString(), startPosition);
    }

    private QueryToken ReadBracketedIdentifier(QueryPosition startPosition)
    {
        var sb = new StringBuilder();
        _position++; // Skip opening bracket
        _column++;

        while (_position < _input.Length && _input[_position] != ']')
        {
            var ch = _input[_position];
            sb.Append(ch);
            _position++;
            if (ch == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }

        if (_position < _input.Length)
        {
            _position++; // Skip closing bracket
            _column++;
        }

        return new QueryToken(TokenType.Identifier, sb.ToString(), startPosition);
    }

    private QueryToken ReadNumericLiteral(QueryPosition startPosition)
    {
        var sb = new StringBuilder();
        var hasDecimalPoint = false;

        while (_position < _input.Length)
        {
            var ch = _input[_position];
            
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
                _position++;
                _column++;
            }
            else if (ch == '.' && !hasDecimalPoint)
            {
                hasDecimalPoint = true;
                sb.Append(ch);
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        var tokenType = hasDecimalPoint ? TokenType.DecimalLiteral : TokenType.IntegerLiteral;
        return new QueryToken(tokenType, sb.ToString(), startPosition);
    }

    private QueryToken ReadIdentifierOrKeyword(QueryPosition startPosition)
    {
        var sb = new StringBuilder();

        while (_position < _input.Length)
        {
            var ch = _input[_position];
            
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        var value = sb.ToString();
        var tokenType = Keywords.TryGetValue(value, out var keyword) ? keyword : TokenType.Identifier;
        
        return new QueryToken(tokenType, value, startPosition);
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
        {
            if (_input[_position] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private void SkipSingleLineComment()
    {
        while (_position < _input.Length && _input[_position] != '\n')
        {
            _position++;
            _column++;
        }
    }

    private QueryPosition GetCurrentPosition()
    {
        return new QueryPosition(_line, _column, _position);
    }
}

/// <summary>
/// Exception thrown when lexical analysis fails.
/// </summary>
public class QueryLexerException : QuerySyntaxException
{
    public QueryLexerException(string message, QueryPosition position)
        : base(message, position, QueryErrorType.SyntaxError)
    {
    }

    public QueryLexerException(string message, QueryPosition position, Exception innerException)
        : base(message, position, innerException, QueryErrorType.SyntaxError)
    {
    }
}
