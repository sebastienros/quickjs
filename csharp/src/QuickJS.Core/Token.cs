// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace QuickJS;

/// <summary>
/// Represents a lexical token produced by the JavaScript lexer.
/// </summary>
/// <remarks>
/// <para>
/// A token is the smallest meaningful unit of JavaScript syntax. The lexer
/// reads source code and produces a stream of tokens for the parser to consume.
/// </para>
/// <para>
/// Each token contains:
/// - The type of token (keyword, identifier, operator, etc.)
/// - The source text that produced the token
/// - The location in the source file
/// - Optional associated value (for literals)
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Token
{
    /// <summary>
    /// Gets the type of this token.
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Gets the raw source text that produced this token.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the start position of this token in the source.
    /// </summary>
    public SourceLocation Start { get; }

    /// <summary>
    /// Gets the end position of this token in the source.
    /// </summary>
    public SourceLocation End { get; }

    /// <summary>
    /// Gets the associated value for literal tokens.
    /// </summary>
    /// <remarks>
    /// For Number tokens, this is the parsed double or long value.
    /// For String tokens, this is the unescaped string value.
    /// For Identifier tokens, this is the identifier name.
    /// For other tokens, this may be null.
    /// </remarks>
    public object? Value { get; }

    /// <summary>
    /// Gets a value indicating whether this token had preceding whitespace
    /// including at least one line terminator.
    /// </summary>
    /// <remarks>
    /// This is important for automatic semicolon insertion (ASI).
    /// </remarks>
    public bool HasLineTerminatorBefore { get; }

    /// <summary>
    /// Creates a new token.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <param name="text">The raw source text.</param>
    /// <param name="start">The start position.</param>
    /// <param name="end">The end position.</param>
    /// <param name="value">The associated value (optional).</param>
    /// <param name="hasLineTerminatorBefore">Whether preceded by a line terminator.</param>
    public Token(
        TokenType type,
        string text,
        SourceLocation start,
        SourceLocation end,
        object? value = null,
        bool hasLineTerminatorBefore = false)
    {
        Type = type;
        Text = text ?? string.Empty;
        Start = start;
        End = end;
        Value = value;
        HasLineTerminatorBefore = hasLineTerminatorBefore;
    }

    /// <summary>
    /// Creates a token with start/end at the same location.
    /// </summary>
    public Token(TokenType type, string text, SourceLocation location, object? value = null)
        : this(type, text, location, location, value)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this is an end-of-file token.
    /// </summary>
    public bool IsEOF => Type == TokenType.EOF;

    /// <summary>
    /// Gets a value indicating whether this token represents an error.
    /// </summary>
    public bool IsError => Type == TokenType.Error;

    /// <summary>
    /// Gets a value indicating whether this token is a keyword.
    /// </summary>
    public bool IsKeyword => Type >= TokenType.Null && Type <= TokenType.Public;

    /// <summary>
    /// Gets a value indicating whether this token is a literal value.
    /// </summary>
    public bool IsLiteral => Type is TokenType.Number or TokenType.String 
                                   or TokenType.Template or TokenType.RegExp
                                   or TokenType.True or TokenType.False or TokenType.Null;

    /// <summary>
    /// Gets a value indicating whether this token is an assignment operator.
    /// </summary>
    public bool IsAssignmentOperator => Type >= TokenType.Assign && Type <= TokenType.NullishCoalescingAssign;

    /// <summary>
    /// Gets a value indicating whether this token is a comparison operator.
    /// </summary>
    public bool IsComparisonOperator => Type >= TokenType.LessThan && Type <= TokenType.StrictNotEqual;

    /// <summary>
    /// Gets a value indicating whether this is a reserved word in strict mode.
    /// </summary>
    public bool IsStrictModeReserved => Type >= TokenType.Implements && Type <= TokenType.Public
                                      || Type == TokenType.Let || Type == TokenType.Yield
                                      || Type == TokenType.Static;

    /// <summary>
    /// Gets the string value of this token (for String and Identifier tokens).
    /// </summary>
    /// <returns>The string value, or null if not applicable.</returns>
    public string? GetStringValue() => Value as string;

    /// <summary>
    /// Gets the numeric value of this token (for Number tokens).
    /// </summary>
    /// <returns>The numeric value, or null if not applicable.</returns>
    public double? GetNumberValue() => Value switch
    {
        double d => d,
        long l => l,
        int i => i,
        _ => null,
    };

    private string DebuggerDisplay => Type switch
    {
        TokenType.EOF => "EOF",
        TokenType.Identifier => $"Identifier: {Value}",
        TokenType.Number => $"Number: {Value}",
        TokenType.String => $"String: \"{Value}\"",
        _ when IsKeyword => $"Keyword: {Text}",
        _ => $"{Type}: {Text}",
    };

    /// <inheritdoc />
    public override string ToString() => $"Token({Type}, \"{Text}\", {Start})";
}

/// <summary>
/// Provides extension methods for <see cref="TokenType"/>.
/// </summary>
public static class TokenTypeExtensions
{
    /// <summary>
    /// Gets the keyword string for a keyword token type.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <returns>The keyword string, or null if not a keyword.</returns>
    public static string? GetKeyword(this TokenType type) => type switch
    {
        TokenType.Null => "null",
        TokenType.False => "false",
        TokenType.True => "true",
        TokenType.If => "if",
        TokenType.Else => "else",
        TokenType.Do => "do",
        TokenType.While => "while",
        TokenType.For => "for",
        TokenType.Break => "break",
        TokenType.Continue => "continue",
        TokenType.Switch => "switch",
        TokenType.Case => "case",
        TokenType.Default => "default",
        TokenType.Return => "return",
        TokenType.Function => "function",
        TokenType.Yield => "yield",
        TokenType.Await => "await",
        TokenType.Throw => "throw",
        TokenType.Try => "try",
        TokenType.Catch => "catch",
        TokenType.Finally => "finally",
        TokenType.Var => "var",
        TokenType.Let => "let",
        TokenType.Const => "const",
        TokenType.Class => "class",
        TokenType.Extends => "extends",
        TokenType.Super => "super",
        TokenType.Static => "static",
        TokenType.This => "this",
        TokenType.New => "new",
        TokenType.Delete => "delete",
        TokenType.Void => "void",
        TokenType.TypeOf => "typeof",
        TokenType.In => "in",
        TokenType.Of => "of",
        TokenType.InstanceOf => "instanceof",
        TokenType.Import => "import",
        TokenType.Export => "export",
        TokenType.Debugger => "debugger",
        TokenType.With => "with",
        TokenType.Enum => "enum",
        TokenType.Implements => "implements",
        TokenType.Interface => "interface",
        TokenType.Package => "package",
        TokenType.Private => "private",
        TokenType.Protected => "protected",
        TokenType.Public => "public",
        _ => null,
    };

    /// <summary>
    /// Gets the punctuator/operator string for a token type.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <returns>The punctuator string, or null if not a punctuator.</returns>
    public static string? GetPunctuator(this TokenType type) => type switch
    {
        TokenType.LeftParen => "(",
        TokenType.RightParen => ")",
        TokenType.LeftBrace => "{",
        TokenType.RightBrace => "}",
        TokenType.LeftBracket => "[",
        TokenType.RightBracket => "]",
        TokenType.Comma => ",",
        TokenType.Semicolon => ";",
        TokenType.Colon => ":",
        TokenType.Dot => ".",
        TokenType.Ellipsis => "...",
        TokenType.Plus => "+",
        TokenType.Minus => "-",
        TokenType.Asterisk => "*",
        TokenType.Slash => "/",
        TokenType.Percent => "%",
        TokenType.Power => "**",
        TokenType.Increment => "++",
        TokenType.Decrement => "--",
        TokenType.Ampersand => "&",
        TokenType.Pipe => "|",
        TokenType.Caret => "^",
        TokenType.Tilde => "~",
        TokenType.LeftShift => "<<",
        TokenType.RightShift => ">>",
        TokenType.UnsignedRightShift => ">>>",
        TokenType.LessThan => "<",
        TokenType.LessThanOrEqual => "<=",
        TokenType.GreaterThan => ">",
        TokenType.GreaterThanOrEqual => ">=",
        TokenType.Equal => "==",
        TokenType.NotEqual => "!=",
        TokenType.StrictEqual => "===",
        TokenType.StrictNotEqual => "!==",
        TokenType.LogicalAnd => "&&",
        TokenType.LogicalOr => "||",
        TokenType.LogicalNot => "!",
        TokenType.NullishCoalescing => "??",
        TokenType.Assign => "=",
        TokenType.PlusAssign => "+=",
        TokenType.MinusAssign => "-=",
        TokenType.AsteriskAssign => "*=",
        TokenType.SlashAssign => "/=",
        TokenType.PercentAssign => "%=",
        TokenType.PowerAssign => "**=",
        TokenType.AmpersandAssign => "&=",
        TokenType.PipeAssign => "|=",
        TokenType.CaretAssign => "^=",
        TokenType.LeftShiftAssign => "<<=",
        TokenType.RightShiftAssign => ">>=",
        TokenType.UnsignedRightShiftAssign => ">>>=",
        TokenType.LogicalAndAssign => "&&=",
        TokenType.LogicalOrAssign => "||=",
        TokenType.NullishCoalescingAssign => "??=",
        TokenType.Question => "?",
        TokenType.Arrow => "=>",
        TokenType.OptionalChaining => "?.",
        _ => null,
    };

    /// <summary>
    /// Gets a value indicating whether the token type is a binary operator.
    /// </summary>
    public static bool IsBinaryOperator(this TokenType type) => type switch
    {
        TokenType.Plus or TokenType.Minus or TokenType.Asterisk or TokenType.Slash or TokenType.Percent or TokenType.Power => true,
        TokenType.Ampersand or TokenType.Pipe or TokenType.Caret => true,
        TokenType.LeftShift or TokenType.RightShift or TokenType.UnsignedRightShift => true,
        TokenType.LessThan or TokenType.LessThanOrEqual or TokenType.GreaterThan or TokenType.GreaterThanOrEqual => true,
        TokenType.Equal or TokenType.NotEqual or TokenType.StrictEqual or TokenType.StrictNotEqual => true,
        TokenType.LogicalAnd or TokenType.LogicalOr or TokenType.NullishCoalescing => true,
        TokenType.In or TokenType.InstanceOf => true,
        _ => false,
    };

    /// <summary>
    /// Gets a value indicating whether the token type is a unary operator.
    /// </summary>
    public static bool IsUnaryOperator(this TokenType type) => type switch
    {
        TokenType.Plus or TokenType.Minus => true,
        TokenType.LogicalNot or TokenType.Tilde => true,
        TokenType.Increment or TokenType.Decrement => true,
        TokenType.TypeOf or TokenType.Void or TokenType.Delete => true,
        _ => false,
    };
}
