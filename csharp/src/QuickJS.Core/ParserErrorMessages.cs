// Licensed under the MIT License.

using System;
using System.Text;

namespace QuickJS;

/// <summary>
/// Utility class for formatting parser error messages.
/// </summary>
internal static class ParserErrorMessages
{
    /// <summary>
    /// Formats a token for display in error messages.
    /// </summary>
    public static string FormatToken(TokenType type, object? value = null)
    {
        return type switch
        {
            TokenType.EOF => "end of input",
            TokenType.LineTerminator => "line terminator",
            TokenType.Identifier => value != null ? $"identifier '{value}'" : "identifier",
            TokenType.Number => value != null ? $"number '{value}'" : "number",
            TokenType.String => value != null ? $"string \"{TruncateString(value.ToString()!, 20)}\"" : "string",
            TokenType.Template => "template literal",
            TokenType.RegExp => "regular expression",

            // Punctuators
            TokenType.LeftParen => "'('",
            TokenType.RightParen => "')'",
            TokenType.LeftBracket => "'['",
            TokenType.RightBracket => "']'",
            TokenType.LeftBrace => "'{'",
            TokenType.RightBrace => "'}'",
            TokenType.Semicolon => "';'",
            TokenType.Comma => "','",
            TokenType.Colon => "':'",
            TokenType.Dot => "'.'",
            TokenType.OptionalChaining => "'?.'",
            TokenType.Question => "'?'",
            TokenType.Arrow => "'=>'",
            TokenType.Ellipsis => "'...'",
            TokenType.Assign => "'='",
            TokenType.Plus => "'+'",
            TokenType.Minus => "'-'",
            TokenType.Asterisk => "'*'",
            TokenType.Slash => "'/'",
            TokenType.Percent => "'%'",
            TokenType.Power => "'**'",
            TokenType.Increment => "'++'",
            TokenType.Decrement => "'--'",
            TokenType.LogicalAnd => "'&&'",
            TokenType.LogicalOr => "'||'",
            TokenType.NullishCoalescing => "'??'",
            TokenType.Ampersand => "'&'",
            TokenType.Pipe => "'|'",
            TokenType.Caret => "'^'",
            TokenType.Tilde => "'~'",
            TokenType.LogicalNot => "'!'",
            TokenType.Equal => "'=='",
            TokenType.NotEqual => "'!='",
            TokenType.StrictEqual => "'==='",
            TokenType.StrictNotEqual => "'!=='",
            TokenType.LessThan => "'<'",
            TokenType.GreaterThan => "'>'",
            TokenType.LessThanOrEqual => "'<='",
            TokenType.GreaterThanOrEqual => "'>='",
            TokenType.LeftShift => "'<<'",
            TokenType.RightShift => "'>>'",
            TokenType.UnsignedRightShift => "'>>>'",

            // Keywords
            TokenType.Var => "'var'",
            TokenType.Let => "'let'",
            TokenType.Const => "'const'",
            TokenType.Function => "'function'",
            TokenType.Class => "'class'",
            TokenType.Extends => "'extends'",
            TokenType.If => "'if'",
            TokenType.Else => "'else'",
            TokenType.For => "'for'",
            TokenType.While => "'while'",
            TokenType.Do => "'do'",
            TokenType.Switch => "'switch'",
            TokenType.Case => "'case'",
            TokenType.Default => "'default'",
            TokenType.Break => "'break'",
            TokenType.Continue => "'continue'",
            TokenType.Return => "'return'",
            TokenType.Throw => "'throw'",
            TokenType.Try => "'try'",
            TokenType.Catch => "'catch'",
            TokenType.Finally => "'finally'",
            TokenType.New => "'new'",
            TokenType.Delete => "'delete'",
            TokenType.TypeOf => "'typeof'",
            TokenType.Void => "'void'",
            TokenType.In => "'in'",
            TokenType.InstanceOf => "'instanceof'",
            TokenType.This => "'this'",
            TokenType.Super => "'super'",
            TokenType.Null => "'null'",
            TokenType.True => "'true'",
            TokenType.False => "'false'",
            TokenType.Import => "'import'",
            TokenType.Export => "'export'",
            TokenType.Async => "'async'",
            TokenType.Await => "'await'",
            TokenType.Yield => "'yield'",
            TokenType.Static => "'static'",
            TokenType.Of => "'of'",
            TokenType.With => "'with'",
            TokenType.Debugger => "'debugger'",

            _ => $"'{type}'"
        };
    }

    /// <summary>
    /// Formats expected token type for error messages.
    /// </summary>
    public static string FormatExpected(TokenType expected)
    {
        return expected switch
        {
            TokenType.Identifier => "an identifier",
            TokenType.Number => "a number",
            TokenType.String => "a string",
            TokenType.Semicolon => "';'",
            TokenType.RightParen => "')'",
            TokenType.RightBracket => "']'",
            TokenType.RightBrace => "'}'",
            TokenType.Colon => "':'",
            TokenType.Arrow => "'=>'",
            _ => FormatToken(expected)
        };
    }

    /// <summary>
    /// Creates an "unexpected token" error message.
    /// </summary>
    public static string UnexpectedToken(TokenType type, object? value = null)
    {
        return $"Unexpected {FormatToken(type, value)}";
    }

    /// <summary>
    /// Creates an "expected X, got Y" error message.
    /// </summary>
    public static string ExpectedGot(TokenType expected, TokenType got, object? gotValue = null)
    {
        return $"Expected {FormatExpected(expected)}, but got {FormatToken(got, gotValue)}";
    }

    /// <summary>
    /// Creates an "expected identifier" error message.
    /// </summary>
    public static string ExpectedIdentifier(TokenType got)
    {
        return $"Expected an identifier, but got {FormatToken(got)}";
    }

    /// <summary>
    /// Creates an "expected expression" error message.
    /// </summary>
    public static string ExpectedExpression(TokenType got)
    {
        return $"Expected an expression, but got {FormatToken(got)}";
    }

    /// <summary>
    /// Extracts a context snippet from source code around a position.
    /// </summary>
    /// <param name="source">The full source code.</param>
    /// <param name="position">The character position of the error.</param>
    /// <param name="contextChars">Number of characters to include before/after.</param>
    /// <returns>A snippet showing context around the error position.</returns>
    public static string GetSourceContext(string source, int position, int contextChars = 40)
    {
        if (string.IsNullOrEmpty(source) || position < 0)
            return string.Empty;

        // Clamp position to valid range so error reporting never throws.
        if (position > source.Length)
            position = source.Length;

        // Find line start and end
        int lineStart = position;
        while (lineStart > 0 && source[lineStart - 1] != '\n')
            lineStart--;

        int lineEnd = position;
        while (lineEnd < source.Length && source[lineEnd] != '\n' && source[lineEnd] != '\r')
            lineEnd++;

        // Extract the line
        var line = source.Substring(lineStart, lineEnd - lineStart);

        // Truncate if too long
        if (line.Length > 80)
        {
            int errorOffset = position - lineStart;
            int start = Math.Max(0, errorOffset - 30);
            int end = Math.Min(line.Length, errorOffset + 30);

            var truncated = line.Substring(start, end - start);
            if (start > 0) truncated = "..." + truncated;
            if (end < line.Length) truncated = truncated + "...";
            return truncated;
        }

        return line;
    }

    /// <summary>
    /// Creates a caret line pointing to the error column.
    /// </summary>
    public static string GetCaretLine(int column)
    {
        if (column <= 0) return "^";
        return new string(' ', column - 1) + "^";
    }

    /// <summary>
    /// Truncates a string for display.
    /// </summary>
    private static string TruncateString(string s, int maxLength)
    {
        if (s.Length <= maxLength)
            return s;
        return s.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Gets a suggestion for common errors.
    /// </summary>
    public static string? GetSuggestion(string errorCode, string? context = null)
    {
        return errorCode switch
        {
            ParseErrorCode.ExpectedToken when context == ")" =>
                "Did you forget a closing parenthesis?",

            ParseErrorCode.ExpectedToken when context == "}" =>
                "Did you forget a closing brace?",

            ParseErrorCode.ExpectedToken when context == ";" =>
                "Did you forget a semicolon?",

            ParseErrorCode.ConstRequiresInitializer =>
                "'const' declarations must be initialized when declared.",

            ParseErrorCode.RestParameterNotLast =>
                "Move the rest parameter (...) to the end of the parameter list.",

            ParseErrorCode.DuplicateConstructor =>
                "A class can only have one constructor method.",

            ParseErrorCode.IllegalReturn =>
                "'return' statements can only appear inside functions.",

            ParseErrorCode.IllegalBreak =>
                "'break' statements can only appear inside loops or switch statements.",

            ParseErrorCode.IllegalContinue =>
                "'continue' statements can only appear inside loops.",

            ParseErrorCode.YieldInNonGenerator =>
                "Make the function a generator by adding '*' after 'function'.",

            ParseErrorCode.AwaitInNonAsync =>
                "Make the function async by adding 'async' before 'function'.",

            ParseErrorCode.ImportInScript =>
                "Use module mode when parsing files with import/export statements.",

            _ => null
        };
    }
}
