// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace QuickJS;

/// <summary>
/// A lexer (tokenizer) for JavaScript source code.
/// </summary>
/// <remarks>
/// <para>
/// The lexer reads JavaScript source code and produces a stream of tokens.
/// It handles:
/// - Identifiers and keywords
/// - Numeric literals (decimal, hex, binary, octal, scientific, BigInt)
/// - String literals (single/double quoted, with escape sequences)
/// - Template literals
/// - Regular expression literals
/// - Operators and punctuation
/// - Comments (single-line and multi-line)
/// - Whitespace and line terminators (for ASI)
/// </para>
/// <para>
/// The lexer maintains state about the current position in the source
/// and whether a line terminator was encountered since the last token.
/// </para>
/// </remarks>
public sealed class Lexer
{
    private readonly string _source;
    private readonly string _fileName;
    private int _position;
    private int _line;
    private int _column;
    private bool _hasLineTerminatorBefore;

    /// <summary>
    /// Creates a new lexer for the specified source code.
    /// </summary>
    /// <param name="source">The JavaScript source code to tokenize.</param>
    /// <param name="fileName">The file name for error reporting (optional).</param>
    public Lexer(string source, string fileName = "<anonymous>")
    {
        _source = source ?? string.Empty;
        _fileName = fileName ?? "<anonymous>";
        _position = 0;
        _line = 1;
        _column = 1;
        _hasLineTerminatorBefore = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SourceSlice GetSlice(int startPos, int length)
    {
        return new SourceSlice(_source, startPos, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetKeyword(ReadOnlySpan<char> span, out TokenType type)
    {
        // Allocation-free keyword detection.
        // Use a switch expression and span patterns.
        type = span.Length switch
        {
            2 when span is "do" => TokenType.Do,
            2 when span is "if" => TokenType.If,
            2 when span is "in" => TokenType.In,
            2 when span is "of" => TokenType.Of,

            3 when span is "for" => TokenType.For,
            3 when span is "let" => TokenType.Let,
            3 when span is "new" => TokenType.New,
            3 when span is "try" => TokenType.Try,
            3 when span is "var" => TokenType.Var,

            4 when span is "case" => TokenType.Case,
            4 when span is "else" => TokenType.Else,
            4 when span is "enum" => TokenType.Enum,
            4 when span is "null" => TokenType.Null,
            4 when span is "this" => TokenType.This,
            4 when span is "true" => TokenType.True,
            4 when span is "void" => TokenType.Void,
            4 when span is "with" => TokenType.With,

            5 when span is "async" => TokenType.Async,
            5 when span is "await" => TokenType.Await,
            5 when span is "break" => TokenType.Break,
            5 when span is "catch" => TokenType.Catch,
            5 when span is "class" => TokenType.Class,
            5 when span is "const" => TokenType.Const,
            5 when span is "false" => TokenType.False,
            5 when span is "super" => TokenType.Super,
            5 when span is "throw" => TokenType.Throw,
            5 when span is "while" => TokenType.While,
            5 when span is "yield" => TokenType.Yield,

            6 when span is "delete" => TokenType.Delete,
            6 when span is "export" => TokenType.Export,
            6 when span is "import" => TokenType.Import,
            6 when span is "public" => TokenType.Public,
            6 when span is "return" => TokenType.Return,
            6 when span is "static" => TokenType.Static,
            6 when span is "switch" => TokenType.Switch,
            6 when span is "typeof" => TokenType.TypeOf,

            7 when span is "default" => TokenType.Default,
            7 when span is "extends" => TokenType.Extends,
            7 when span is "finally" => TokenType.Finally,
            7 when span is "package" => TokenType.Package,
            7 when span is "private" => TokenType.Private,

            8 when span is "continue" => TokenType.Continue,
            8 when span is "debugger" => TokenType.Debugger,
            8 when span is "function" => TokenType.Function,

            9 when span is "interface" => TokenType.Interface,
            9 when span is "protected" => TokenType.Protected,

            10 when span is "implements" => TokenType.Implements,
            10 when span is "instanceof" => TokenType.InstanceOf,

            _ => default
        };

        return type != default;
    }

    /// <summary>
    /// Gets the current position in the source code.
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Gets the current line number (1-based).
    /// </summary>
    public int Line => _line;

    /// <summary>
    /// Gets the current column number (1-based).
    /// </summary>
    public int Column => _column;

    /// <summary>
    /// Gets a value indicating whether the end of the source has been reached.
    /// </summary>
    public bool IsAtEnd => _position >= _source.Length;

    /// <summary>
    /// Reads the next token from the source.
    /// </summary>
    /// <returns>The next token.</returns>
    public Token NextToken()
    {
        _hasLineTerminatorBefore = false;

        // Skip whitespace and comments
        SkipWhitespaceAndComments();

        var start = CreateLocation();

        if (IsAtEnd)
        {
            return new Token(TokenType.EOF, SourceSlice.Empty, start, start, null, _hasLineTerminatorBefore);
        }

        char c = Current;

        // Identifiers and keywords
        if (IsIdentifierStart(c))
        {
            return ScanIdentifierOrKeyword(start);
        }

        // Numbers
        if (char.IsDigit(c) || (c == '.' && !IsAtEnd && char.IsDigit(Peek(1))))
        {
            return ScanNumber(start);
        }

        // Strings
        if (c == '"' || c == '\'')
        {
            return ScanString(start, c);
        }

        // Template literals
        if (c == '`')
        {
            return ScanTemplateLiteral(start, isContinuation: false);
        }

        // Private name
        if (c == '#' && !IsAtEnd && IsIdentifierStart(Peek(1)))
        {
            return ScanPrivateName(start);
        }

        // Punctuation and operators
        return ScanPunctuator(start);
    }

    /// <summary>
    /// Tokenizes the entire source and returns all tokens.
    /// </summary>
    /// <returns>A list of all tokens, including EOF.</returns>
    public List<Token> TokenizeAll()
    {
        var tokens = new List<Token>();
        Token token;
        do
        {
            token = NextToken();
            tokens.Add(token);
        } while (token.Type != TokenType.EOF);

        return tokens;
    }

    #region Character Helpers

    private char Current => _position < _source.Length ? _source[_position] : '\0';

    private char Peek(int offset = 0)
    {
        int pos = _position + offset;
        return pos < _source.Length ? _source[pos] : '\0';
    }

    private char Advance()
    {
        char c = Current;
        _position++;
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private bool Match(char expected)
    {
        if (Current == expected)
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool Match(string expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (Peek(i) != expected[i])
                return false;
        }
        for (int i = 0; i < expected.Length; i++)
            Advance();
        return true;
    }

    private SourceLocation CreateLocation() => new SourceLocation(_fileName, _line, _column);

    #endregion

    #region Whitespace and Comments

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            char c = Current;

            switch (c)
            {
                case ' ':
                case '\t':
                case '\f':
                case '\v':
                    Advance();
                    break;

                case '\r':
                    Advance();
                    Match('\n'); // Handle CRLF
                    _hasLineTerminatorBefore = true;
                    break;

                case '\n':
                    Advance();
                    _hasLineTerminatorBefore = true;
                    break;

                case '/':
                    if (Peek(1) == '/')
                    {
                        SkipSingleLineComment();
                    }
                    else if (Peek(1) == '*')
                    {
                        SkipMultiLineComment();
                    }
                    else
                    {
                        return;
                    }
                    break;

                // Unicode whitespace
                case '\u00A0': // Non-breaking space
                case '\u1680': // Ogham space mark
                case '\u2000': // En quad
                case '\u2001': // Em quad
                case '\u2002': // En space
                case '\u2003': // Em space
                case '\u2004': // Three-per-em space
                case '\u2005': // Four-per-em space
                case '\u2006': // Six-per-em space
                case '\u2007': // Figure space
                case '\u2008': // Punctuation space
                case '\u2009': // Thin space
                case '\u200A': // Hair space
                case '\u202F': // Narrow no-break space
                case '\u205F': // Medium mathematical space
                case '\u3000': // Ideographic space
                case '\uFEFF': // BOM / Zero width no-break space
                    Advance();
                    break;

                // Unicode line terminators
                case '\u2028': // Line separator
                case '\u2029': // Paragraph separator
                    Advance();
                    _hasLineTerminatorBefore = true;
                    break;

                default:
                    return;
            }
        }
    }

    private void SkipSingleLineComment()
    {
        // Skip //
        Advance();
        Advance();

        while (!IsAtEnd && Current != '\n' && Current != '\r' 
               && Current != '\u2028' && Current != '\u2029')
        {
            Advance();
        }
    }

    private void SkipMultiLineComment()
    {
        // Skip /*
        Advance();
        Advance();

        while (!IsAtEnd)
        {
            char c = Current;

            if (c == '*' && Peek(1) == '/')
            {
                Advance(); // *
                Advance(); // /
                return;
            }

            if (c == '\n' || c == '\r' || c == '\u2028' || c == '\u2029')
            {
                _hasLineTerminatorBefore = true;
            }

            Advance();
        }

        // Unterminated comment - let the parser handle the error
    }

    #endregion

    #region Identifiers and Keywords

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentifierStart(char c)
    {
        // Fast path for ASCII
        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '$')
            return true;

        // Unicode identifier start
        if (c > 127)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.UppercaseLetter
                || category == UnicodeCategory.LowercaseLetter
                || category == UnicodeCategory.TitlecaseLetter
                || category == UnicodeCategory.ModifierLetter
                || category == UnicodeCategory.OtherLetter
                || category == UnicodeCategory.LetterNumber;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentifierPart(char c)
    {
        // Fast path for ASCII
        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') 
            || (c >= '0' && c <= '9') || c == '_' || c == '$')
            return true;

        // Unicode identifier continue
        if (c > 127)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.UppercaseLetter
                || category == UnicodeCategory.LowercaseLetter
                || category == UnicodeCategory.TitlecaseLetter
                || category == UnicodeCategory.ModifierLetter
                || category == UnicodeCategory.OtherLetter
                || category == UnicodeCategory.LetterNumber
                || category == UnicodeCategory.NonSpacingMark
                || category == UnicodeCategory.SpacingCombiningMark
                || category == UnicodeCategory.DecimalDigitNumber
                || category == UnicodeCategory.ConnectorPunctuation
                || c == '\u200C'  // Zero-width non-joiner
                || c == '\u200D'; // Zero-width joiner
        }

        return false;
    }

    private Token ScanIdentifierOrKeyword(SourceLocation start)
    {
        int startPos = _position;

        // First character already validated
        Advance();

        // Continue with identifier characters
        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            // Handle escape sequences in identifiers
            if (Current == '\\' && Peek(1) == 'u')
            {
                // Unicode escape in identifier - complex case
                // For now, just consume the escape
                Advance(); // backslash
                Advance(); // u
                
                if (Current == '{')
                {
                    // \u{XXXX} form
                    Advance();
                    while (!IsAtEnd && IsHexDigit(Current))
                        Advance();
                    if (Current == '}')
                        Advance();
                }
                else
                {
                    // \uXXXX form
                    for (int i = 0; i < 4 && !IsAtEnd && IsHexDigit(Current); i++)
                        Advance();
                }
            }
            else
            {
                Advance();
            }
        }

        int length = _position - startPos;
        var text = GetSlice(startPos, length);
        var end = CreateLocation();

        // Check if it's a keyword
        if (TryGetKeyword(text.Span, out TokenType keywordType))
        {
            return new Token(keywordType, text, start, end, null, _hasLineTerminatorBefore);
        }

        // It's an identifier
        return new Token(TokenType.Identifier, text, start, end, null, _hasLineTerminatorBefore);
    }

    private Token ScanPrivateName(SourceLocation start)
    {
        int startPos = _position;
        Advance(); // Skip #

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            Advance();
        }

        var text = GetSlice(startPos, _position - startPos);
        var end = CreateLocation();

        return new Token(TokenType.PrivateName, text, start, end, null, _hasLineTerminatorBefore);
    }

    #endregion

    #region Numbers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHexDigit(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBinaryDigit(char c)
        => c == '0' || c == '1';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOctalDigit(char c)
        => c >= '0' && c <= '7';

    private Token ScanNumber(SourceLocation start)
    {
        int startPos = _position;
        bool isBigInt = false;
        bool isFloat = false;

        if (Current == '0')
        {
            Advance();
            char next = char.ToLowerInvariant(Current);

            if (next == 'x')
            {
                // Hexadecimal
                Advance();
                while (!IsAtEnd && (IsHexDigit(Current) || Current == '_'))
                    Advance();
            }
            else if (next == 'b')
            {
                // Binary
                Advance();
                while (!IsAtEnd && (IsBinaryDigit(Current) || Current == '_'))
                    Advance();
            }
            else if (next == 'o')
            {
                // Octal
                Advance();
                while (!IsAtEnd && (IsOctalDigit(Current) || Current == '_'))
                    Advance();
            }
            else
            {
                // Decimal or legacy octal
                ScanDecimalPart(ref isFloat);
            }
        }
        else if (Current == '.')
        {
            // Starts with .
            isFloat = true;
            Advance();
            while (!IsAtEnd && (char.IsDigit(Current) || Current == '_'))
                Advance();
            ScanExponent(ref isFloat);
        }
        else
        {
            // Regular decimal
            ScanDecimalPart(ref isFloat);
        }

        // Check for BigInt suffix
        if (!isFloat && Current == 'n')
        {
            isBigInt = true;
            Advance();
        }

        var text = GetSlice(startPos, _position - startPos);
        var end = CreateLocation();

        // Parse the numeric value using span to avoid allocations
        ReadOnlySpan<char> numSpan = _source.AsSpan(startPos, _position - startPos);
        object? value = ParseNumericLiteral(numSpan, isBigInt);

        return new Token(TokenType.Number, text, start, end, value, _hasLineTerminatorBefore);
    }

    private void ScanDecimalPart(ref bool isFloat)
    {
        // Integer part
        while (!IsAtEnd && (char.IsDigit(Current) || Current == '_'))
            Advance();

        // Fractional part
        if (Current == '.' && char.IsDigit(Peek(1)))
        {
            isFloat = true;
            Advance(); // .
            while (!IsAtEnd && (char.IsDigit(Current) || Current == '_'))
                Advance();
        }

        ScanExponent(ref isFloat);
    }

    private void ScanExponent(ref bool isFloat)
    {
        if (Current == 'e' || Current == 'E')
        {
            isFloat = true;
            Advance();
            if (Current == '+' || Current == '-')
                Advance();
            while (!IsAtEnd && (char.IsDigit(Current) || Current == '_'))
                Advance();
        }
    }

    private static object? ParseNumericLiteral(ReadOnlySpan<char> text, bool isBigInt)
    {
        if (text.IsEmpty)
            return null;

        // Remove BigInt suffix from consideration
        if (isBigInt && text[text.Length - 1] == 'n')
            text = text.Slice(0, text.Length - 1);

        try
        {
            // Check for hex/binary/octal prefixes
            if (text.Length >= 2 && text[0] == '0')
            {
                char prefix = char.ToLowerInvariant(text[1]);
                if (prefix == 'x')
                {
                    // Hexadecimal
                    long value = ParseIntegerSpan(text.Slice(2), 16);
                    if (isBigInt)
                        return value;
                    return (double)value;
                }
                else if (prefix == 'b')
                {
                    // Binary
                    long value = ParseIntegerSpan(text.Slice(2), 2);
                    if (isBigInt)
                        return value;
                    return (double)value;
                }
                else if (prefix == 'o')
                {
                    // Octal
                    long value = ParseIntegerSpan(text.Slice(2), 8);
                    if (isBigInt)
                        return value;
                    return (double)value;
                }
            }

            // Decimal (possibly floating point)
            if (isBigInt)
            {
                // Parse as integer, skipping underscores
                return ParseBigIntegerSpan(text);
            }
            else
            {
                return ParseDoubleSpan(text);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses an integer from a span, skipping underscores.
    /// </summary>
    private static long ParseIntegerSpan(ReadOnlySpan<char> span, int radix)
    {
        long result = 0;
        foreach (char c in span)
        {
            if (c == '_')
                continue;

            int digit = radix switch
            {
                16 => HexValue(c),
                2 => c - '0',
                8 => c - '0',
                10 => c - '0',
                _ => c - '0'
            };

            result = result * radix + digit;
        }
        return result;
    }

    /// <summary>
    /// Parses a BigInteger from a span, skipping underscores.
    /// </summary>
    private static object ParseBigIntegerSpan(ReadOnlySpan<char> span)
    {
        // Count digits to determine if we need BigInteger
        int digitCount = 0;
        foreach (char c in span)
        {
            if (c != '_')
                digitCount++;
        }

        // If small enough, parse as long
        if (digitCount <= 18)
        {
            long result = 0;
            foreach (char c in span)
            {
                if (c == '_')
                    continue;
                result = result * 10 + (c - '0');
            }
            return result;
        }

        // Large number - need BigInteger
        // Build clean string for BigInteger.Parse
        Span<char> clean = digitCount <= 128 ? stackalloc char[digitCount] : new char[digitCount];
        int idx = 0;
        foreach (char c in span)
        {
            if (c != '_')
                clean[idx++] = c;
        }

#if NETSTANDARD2_0
        return System.Numerics.BigInteger.Parse(clean.ToString());
#else
        return System.Numerics.BigInteger.Parse(clean);
#endif
    }

    /// <summary>
    /// Parses a double from a span, handling underscores and exponents.
    /// </summary>
    private static double ParseDoubleSpan(ReadOnlySpan<char> span)
    {
        // Check if we have underscores - if not, try direct parse
        bool hasUnderscores = false;
        foreach (char c in span)
        {
            if (c == '_')
            {
                hasUnderscores = true;
                break;
            }
        }

        if (!hasUnderscores)
        {
#if NETSTANDARD2_0
            return double.Parse(span.ToString(), CultureInfo.InvariantCulture);
#else
            return double.Parse(span, CultureInfo.InvariantCulture);
#endif
        }

        // Has underscores - need to build clean string
        int cleanLen = 0;
        foreach (char c in span)
        {
            if (c != '_')
                cleanLen++;
        }

        Span<char> clean = cleanLen <= 64 ? stackalloc char[cleanLen] : new char[cleanLen];
        int idx = 0;
        foreach (char c in span)
        {
            if (c != '_')
                clean[idx++] = c;
        }

#if NETSTANDARD2_0
        return double.Parse(clean.ToString(), CultureInfo.InvariantCulture);
#else
        return double.Parse(clean, CultureInfo.InvariantCulture);
#endif
    }

    #endregion

    #region Strings

    private Token ScanString(SourceLocation start, char quote)
    {
        int startPos = _position;
        StringBuilder? sb = null;
        
        Advance(); // Opening quote

        int valueStartPos = _position;
        bool isClosed = false;

        while (!IsAtEnd)
        {
            char c = Current;

            if (c == quote)
            {
                Advance(); // Closing quote
                isClosed = true;
                break;
            }

            if (c == '\r' || c == '\n')
            {
                // Unterminated string
                break;
            }

            if (c == '\\')
            {
                if (sb is null)
                {
                    // Lazily allocate only when we encounter the first escape sequence.
                    // Initialize with the already-scanned characters.
                    int prefixLen = _position - valueStartPos;
                    sb = new StringBuilder(capacity: Math.Max(16, prefixLen + 8));
                    if (prefixLen > 0)
                    {
                        sb.Append(_source, valueStartPos, prefixLen);
                    }
                }

                Advance(); // backslash
                if (!IsAtEnd)
                {
                    sb.Append(ScanEscapeSequence());
                }
            }
            else
            {
                if (sb is not null)
                {
                    sb.Append(c);
                }
                Advance();
            }
        }

        var text = GetSlice(startPos, _position - startPos);
        var end = CreateLocation();

        int valueEndExclusive = isClosed ? _position - 1 : _position;
        string value = sb is null
            ? _source.Substring(valueStartPos, valueEndExclusive - valueStartPos)
            : sb.ToString();

        return new Token(TokenType.String, text, start, end, value, _hasLineTerminatorBefore);
    }

    private string ScanEscapeSequence()
    {
        char c = Current;
        Advance();

        switch (c)
        {
            case 'n': return "\n";
            case 'r': return "\r";
            case 't': return "\t";
            case 'b': return "\b";
            case 'f': return "\f";
            case 'v': return "\v";
            case '0': return "\0";
            case '\\': return "\\";
            case '\'': return "'";
            case '"': return "\"";
            case '\r':
                Match('\n'); // CRLF line continuation
                return "";
            case '\n':
                return "";  // Line continuation
            case 'x':
                return ScanHexEscape(2);
            case 'u':
                if (Current == '{')
                {
                    Advance();
                    return ScanUnicodeCodePointEscape();
                }
                return ScanHexEscape(4);
            default:
                return c.ToString();
        }
    }

    private string ScanHexEscape(int length)
    {
        int value = 0;
        for (int i = 0; i < length && !IsAtEnd && IsHexDigit(Current); i++)
        {
            value = value * 16 + HexValue(Current);
            Advance();
        }
        return char.ConvertFromUtf32(value);
    }

    private string ScanUnicodeCodePointEscape()
    {
        int value = 0;
        while (!IsAtEnd && Current != '}' && IsHexDigit(Current))
        {
            value = value * 16 + HexValue(Current);
            Advance();
        }
        if (Current == '}')
            Advance();

        if (value > 0x10FFFF)
            return "\uFFFD"; // Replacement character for invalid code points

        return char.ConvertFromUtf32(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return 0;
    }

    #endregion

    #region Template Literals

    private Token ScanTemplateLiteral(SourceLocation start, bool isContinuation)
    {
        int startPos = _position;
        var cooked = new StringBuilder();
        var raw = new StringBuilder();
        bool isTail = false;

        if (!isContinuation)
        {
            Advance(); // Opening backtick
        }

        while (!IsAtEnd)
        {
            char c = Current;

            if (c == '`')
            {
                isTail = true;
                Advance();
                break;
            }

            if (c == '$' && Peek(1) == '{')
            {
                Advance();
                Advance();
                break;
            }

            if (c == '\\')
            {
                int escapeStart = _position;
                Advance(); // backslash
                if (IsAtEnd)
                    break;
                cooked.Append(ScanEscapeSequence());
                raw.Append(_source, escapeStart, _position - escapeStart);
            }
            else if (c == '\r')
            {
                cooked.Append('\n');
                raw.Append('\n');
                Advance();
                Match('\n');
            }
            else if (c == '\n')
            {
                cooked.Append('\n');
                raw.Append('\n');
                Advance();
            }
            else
            {
                cooked.Append(c);
                raw.Append(c);
                Advance();
            }
        }

        var text = GetSlice(startPos, _position - startPos);
        var end = CreateLocation();

        var rawText = raw.ToString()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
        var value = new TemplateLiteralToken(cooked.ToString(), rawText, isTail);
        return new Token(TokenType.Template, text, start, end, value, _hasLineTerminatorBefore);
    }

    internal Token ScanTemplateToken()
    {
        var start = CreateLocation();
        return ScanTemplateLiteral(start, isContinuation: true);
    }

    #endregion

    #region Punctuators and Operators

    private Token ScanPunctuator(SourceLocation start)
    {
        int startPos = _position;
        char c = Current;
        Advance();

        TokenType type;
        
        switch (c)
        {
            case '(':
                type = TokenType.LeftParen;
                break;
            case ')':
                type = TokenType.RightParen;
                break;
            case '{':
                type = TokenType.LeftBrace;
                break;
            case '}':
                type = TokenType.RightBrace;
                break;
            case '[':
                type = TokenType.LeftBracket;
                break;
            case ']':
                type = TokenType.RightBracket;
                break;
            case ',':
                type = TokenType.Comma;
                break;
            case ';':
                type = TokenType.Semicolon;
                break;
            case ':':
                type = TokenType.Colon;
                break;
            case '~':
                type = TokenType.Tilde;
                break;

            case '.':
                if (Match(".."))
                    type = TokenType.Ellipsis;
                else
                    type = TokenType.Dot;
                break;

            case '+':
                if (Match('+'))
                    type = TokenType.Increment;
                else if (Match('='))
                    type = TokenType.PlusAssign;
                else
                    type = TokenType.Plus;
                break;

            case '-':
                if (Match('-'))
                    type = TokenType.Decrement;
                else if (Match('='))
                    type = TokenType.MinusAssign;
                else
                    type = TokenType.Minus;
                break;

            case '*':
                if (Match('*'))
                {
                    if (Match('='))
                        type = TokenType.PowerAssign;
                    else
                        type = TokenType.Power;
                }
                else if (Match('='))
                    type = TokenType.AsteriskAssign;
                else
                    type = TokenType.Asterisk;
                break;

            case '/':
                if (Match('='))
                    type = TokenType.SlashAssign;
                else
                    type = TokenType.Slash;
                break;

            case '%':
                if (Match('='))
                    type = TokenType.PercentAssign;
                else
                    type = TokenType.Percent;
                break;

            case '&':
                if (Match('&'))
                {
                    if (Match('='))
                        type = TokenType.LogicalAndAssign;
                    else
                        type = TokenType.LogicalAnd;
                }
                else if (Match('='))
                    type = TokenType.AmpersandAssign;
                else
                    type = TokenType.Ampersand;
                break;

            case '|':
                if (Match('|'))
                {
                    if (Match('='))
                        type = TokenType.LogicalOrAssign;
                    else
                        type = TokenType.LogicalOr;
                }
                else if (Match('='))
                    type = TokenType.PipeAssign;
                else
                    type = TokenType.Pipe;
                break;

            case '^':
                if (Match('='))
                    type = TokenType.CaretAssign;
                else
                    type = TokenType.Caret;
                break;

            case '<':
                if (Match('<'))
                {
                    if (Match('='))
                        type = TokenType.LeftShiftAssign;
                    else
                        type = TokenType.LeftShift;
                }
                else if (Match('='))
                    type = TokenType.LessThanOrEqual;
                else
                    type = TokenType.LessThan;
                break;

            case '>':
                if (Match('>'))
                {
                    if (Match('>'))
                    {
                        if (Match('='))
                            type = TokenType.UnsignedRightShiftAssign;
                        else
                            type = TokenType.UnsignedRightShift;
                    }
                    else if (Match('='))
                        type = TokenType.RightShiftAssign;
                    else
                        type = TokenType.RightShift;
                }
                else if (Match('='))
                    type = TokenType.GreaterThanOrEqual;
                else
                    type = TokenType.GreaterThan;
                break;

            case '=':
                if (Match('='))
                {
                    if (Match('='))
                        type = TokenType.StrictEqual;
                    else
                        type = TokenType.Equal;
                }
                else if (Match('>'))
                    type = TokenType.Arrow;
                else
                    type = TokenType.Assign;
                break;

            case '!':
                if (Match('='))
                {
                    if (Match('='))
                        type = TokenType.StrictNotEqual;
                    else
                        type = TokenType.NotEqual;
                }
                else
                    type = TokenType.LogicalNot;
                break;

            case '?':
                if (Match('?'))
                {
                    if (Match('='))
                        type = TokenType.NullishCoalescingAssign;
                    else
                        type = TokenType.NullishCoalescing;
                }
                else if (Match('.') && !char.IsDigit(Current))
                    type = TokenType.OptionalChaining;
                else
                    type = TokenType.Question;
                break;

            default:
                type = TokenType.Error;
                break;
        }

        var text = GetSlice(startPos, _position - startPos);
        var end = CreateLocation();

        return new Token(type, text, start, end, null, _hasLineTerminatorBefore);
    }

    #endregion

    #region Position Save/Restore (for lookahead)

    /// <summary>
    /// Represents a saved lexer position that can be restored later.
    /// </summary>
    public readonly struct LexerPosition
    {
        /// <summary>The character position in the source.</summary>
        public readonly int Position;
        /// <summary>The line number (1-based).</summary>
        public readonly int Line;
        /// <summary>The column number (1-based).</summary>
        public readonly int Column;
        /// <summary>Whether a line terminator was encountered before this position.</summary>
        public readonly bool HasLineTerminatorBefore;

        /// <summary>
        /// Creates a new lexer position.
        /// </summary>
        public LexerPosition(int position, int line, int column, bool hasLineTerminatorBefore)
        {
            Position = position;
            Line = line;
            Column = column;
            HasLineTerminatorBefore = hasLineTerminatorBefore;
        }
    }

    /// <summary>
    /// Saves the current lexer position for later restoration.
    /// </summary>
    public LexerPosition SavePosition()
    {
        return new LexerPosition(_position, _line, _column, _hasLineTerminatorBefore);
    }

    /// <summary>
    /// Restores the lexer to a previously saved position.
    /// </summary>
    public void RestorePosition(LexerPosition pos)
    {
        _position = pos.Position;
        _line = pos.Line;
        _column = pos.Column;
        _hasLineTerminatorBefore = pos.HasLineTerminatorBefore;
    }

    /// <summary>
    /// Performs a lightweight peek to see the next token type without full tokenization.
    /// This is used for arrow function detection (looking for =>).
    /// </summary>
    /// <param name="noLineTerminator">If true, returns '\n' token type if a line terminator is encountered.</param>
    /// <returns>The type of the next token.</returns>
    public TokenType SimplePeekToken(bool noLineTerminator)
    {
        int pos = _position;

        // Skip whitespace and comments
        while (pos < _source.Length)
        {
            char c = _source[pos];

            switch (c)
            {
                case ' ':
                case '\t':
                case '\f':
                case '\v':
                    pos++;
                    continue;

                case '\r':
                case '\n':
                    if (noLineTerminator)
                        return TokenType.LineTerminator;
                    pos++;
                    continue;

                case '/':
                    if (pos + 1 < _source.Length)
                    {
                        if (_source[pos + 1] == '/')
                        {
                            // Single-line comment
                            if (noLineTerminator)
                                return TokenType.LineTerminator;
                            pos += 2;
                            while (pos < _source.Length && _source[pos] != '\r' && _source[pos] != '\n')
                                pos++;
                            continue;
                        }
                        if (_source[pos + 1] == '*')
                        {
                            // Multi-line comment
                            pos += 2;
                            while (pos + 1 < _source.Length)
                            {
                                if (noLineTerminator && (_source[pos] == '\r' || _source[pos] == '\n'))
                                    return TokenType.LineTerminator;
                                if (_source[pos] == '*' && _source[pos + 1] == '/')
                                {
                                    pos += 2;
                                    break;
                                }
                                pos++;
                            }
                            continue;
                        }
                    }
                    // It's just a '/' - could be division or regex
                    return TokenType.Slash;

                case '=':
                    if (pos + 1 < _source.Length && _source[pos + 1] == '>')
                        return TokenType.Arrow;
                    return TokenType.Assign;

                case '(':
                    return TokenType.LeftParen;
                case ')':
                    return TokenType.RightParen;
                case '{':
                    return TokenType.LeftBrace;
                case '}':
                    return TokenType.RightBrace;
                case '[':
                    return TokenType.LeftBracket;
                case ']':
                    return TokenType.RightBracket;
                case ',':
                    return TokenType.Comma;
                case ';':
                    return TokenType.Semicolon;
                case ':':
                    return TokenType.Colon;
                case '.':
                    return TokenType.Dot;

                default:
                    // Check for identifiers
                    if (IsIdentifierStart(c))
                    {
                        // Read the full identifier to check for keywords
                        int start = pos;
                        pos++;
                        while (pos < _source.Length && IsIdentifierPart(_source[pos]))
                            pos++;
                        ReadOnlySpan<char> ident = _source.AsSpan(start, pos - start);

                        // Check for specific keywords we care about
                        if (ident.SequenceEqual("function")) return TokenType.Function;
                        if (ident.SequenceEqual("in")) return TokenType.In;
                        if (ident.SequenceEqual("of")) return TokenType.Of;
                        if (ident.SequenceEqual("import")) return TokenType.Import;
                        if (ident.SequenceEqual("export")) return TokenType.Export;
                        return TokenType.Identifier;
                    }

                    // Return the character as-is for other cases
                    return TokenType.Error;
            }
        }

        return TokenType.EOF;
    }

    #endregion
}
