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

    // Keyword lookup table
    private static readonly Dictionary<string, TokenType> Keywords = CreateKeywordTable();

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
            return new Token(TokenType.EOF, "", start, start, null, _hasLineTerminatorBefore);
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
            return ScanTemplateLiteral(start);
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

        string text = _source.Substring(startPos, _position - startPos);
        var end = CreateLocation();

        // Check if it's a keyword
        if (Keywords.TryGetValue(text, out TokenType keywordType))
        {
            return new Token(keywordType, text, start, end, null, _hasLineTerminatorBefore);
        }

        // It's an identifier
        return new Token(TokenType.Identifier, text, start, end, text, _hasLineTerminatorBefore);
    }

    private Token ScanPrivateName(SourceLocation start)
    {
        int startPos = _position;
        Advance(); // Skip #

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            Advance();
        }

        string text = _source.Substring(startPos, _position - startPos);
        var end = CreateLocation();

        return new Token(TokenType.PrivateName, text, start, end, text, _hasLineTerminatorBefore);
    }

    private static Dictionary<string, TokenType> CreateKeywordTable()
    {
        return new Dictionary<string, TokenType>
        {
            // Literal keywords
            ["null"] = TokenType.Null,
            ["true"] = TokenType.True,
            ["false"] = TokenType.False,

            // Control flow
            ["if"] = TokenType.If,
            ["else"] = TokenType.Else,
            ["do"] = TokenType.Do,
            ["while"] = TokenType.While,
            ["for"] = TokenType.For,
            ["break"] = TokenType.Break,
            ["continue"] = TokenType.Continue,
            ["switch"] = TokenType.Switch,
            ["case"] = TokenType.Case,
            ["default"] = TokenType.Default,

            // Functions
            ["return"] = TokenType.Return,
            ["function"] = TokenType.Function,
            ["async"] = TokenType.Async,
            ["yield"] = TokenType.Yield,
            ["await"] = TokenType.Await,

            // Exceptions
            ["throw"] = TokenType.Throw,
            ["try"] = TokenType.Try,
            ["catch"] = TokenType.Catch,
            ["finally"] = TokenType.Finally,

            // Declarations
            ["var"] = TokenType.Var,
            ["let"] = TokenType.Let,
            ["const"] = TokenType.Const,

            // Classes
            ["class"] = TokenType.Class,
            ["extends"] = TokenType.Extends,
            ["super"] = TokenType.Super,
            ["static"] = TokenType.Static,

            // Operators
            ["this"] = TokenType.This,
            ["new"] = TokenType.New,
            ["delete"] = TokenType.Delete,
            ["void"] = TokenType.Void,
            ["typeof"] = TokenType.TypeOf,
            ["in"] = TokenType.In,
            ["of"] = TokenType.Of,
            ["instanceof"] = TokenType.InstanceOf,

            // Modules
            ["import"] = TokenType.Import,
            ["export"] = TokenType.Export,

            // Other
            ["debugger"] = TokenType.Debugger,
            ["with"] = TokenType.With,

            // Future reserved
            ["enum"] = TokenType.Enum,
            ["implements"] = TokenType.Implements,
            ["interface"] = TokenType.Interface,
            ["package"] = TokenType.Package,
            ["private"] = TokenType.Private,
            ["protected"] = TokenType.Protected,
            ["public"] = TokenType.Public,
        };
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

        string text = _source.Substring(startPos, _position - startPos);
        var end = CreateLocation();

        // Parse the numeric value
        object? value = ParseNumericLiteral(text, isBigInt);

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

    private static object? ParseNumericLiteral(string text, bool isBigInt)
    {
        // Remove underscores and BigInt suffix
        string cleanText = text.Replace("_", "");
        if (isBigInt && cleanText.EndsWith("n", StringComparison.Ordinal))
            cleanText = cleanText.Substring(0, cleanText.Length - 1);

        try
        {
            if (cleanText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Hexadecimal
                long value = Convert.ToInt64(cleanText.Substring(2), 16);
                return isBigInt ? value : (double)value;
            }
            else if (cleanText.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                // Binary
                long value = Convert.ToInt64(cleanText.Substring(2), 2);
                return isBigInt ? value : (double)value;
            }
            else if (cleanText.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                // Octal
                long value = Convert.ToInt64(cleanText.Substring(2), 8);
                return isBigInt ? value : (double)value;
            }
            else
            {
                // Decimal (possibly floating point)
                if (isBigInt)
                {
                    // Use BigInteger for large numbers
                    if (System.Numerics.BigInteger.TryParse(cleanText, out var bigVal))
                    {
                        // If it fits in a long, return as long for efficiency
                        if (bigVal >= long.MinValue && bigVal <= long.MaxValue)
                            return (long)bigVal;
                        // Return as BigInteger for very large numbers
                        return bigVal;
                    }
                    return null;
                }
                else
                {
                    return double.Parse(cleanText, CultureInfo.InvariantCulture);
                }
            }
        }
        catch
        {
            // Return the text for error reporting
            return null;
        }
    }

    #endregion

    #region Strings

    private Token ScanString(SourceLocation start, char quote)
    {
        int startPos = _position;
        var sb = new StringBuilder();
        
        Advance(); // Opening quote

        while (!IsAtEnd)
        {
            char c = Current;

            if (c == quote)
            {
                Advance(); // Closing quote
                break;
            }

            if (c == '\r' || c == '\n')
            {
                // Unterminated string
                break;
            }

            if (c == '\\')
            {
                Advance(); // backslash
                if (!IsAtEnd)
                {
                    sb.Append(ScanEscapeSequence());
                }
            }
            else
            {
                sb.Append(c);
                Advance();
            }
        }

        string text = _source.Substring(startPos, _position - startPos);
        var end = CreateLocation();

        return new Token(TokenType.String, text, start, end, sb.ToString(), _hasLineTerminatorBefore);
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

    private Token ScanTemplateLiteral(SourceLocation start)
    {
        int startPos = _position;
        var sb = new StringBuilder();
        
        Advance(); // Opening backtick

        while (!IsAtEnd)
        {
            char c = Current;

            if (c == '`')
            {
                Advance();
                break;
            }

            if (c == '$' && Peek(1) == '{')
            {
                // Template expression - for now just consume it
                // TODO: Handle template expressions properly
                sb.Append(c);
                Advance();
            }
            else if (c == '\\')
            {
                Advance();
                if (!IsAtEnd)
                {
                    sb.Append(ScanEscapeSequence());
                }
            }
            else
            {
                if (c == '\r')
                {
                    sb.Append('\n');
                    Advance();
                    Match('\n');
                }
                else
                {
                    sb.Append(c);
                    Advance();
                }
            }
        }

        string text = _source.Substring(startPos, _position - startPos);
        var end = CreateLocation();

        return new Token(TokenType.Template, text, start, end, sb.ToString(), _hasLineTerminatorBefore);
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

        string text = _source.Substring(startPos, _position - startPos);
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
                        string ident = _source.Substring(start, pos - start);

                        // Check for specific keywords we care about
                        return ident switch
                        {
                            "function" => TokenType.Function,
                            "in" => TokenType.In,
                            "of" => TokenType.Of,
                            "import" => TokenType.Import,
                            "export" => TokenType.Export,
                            _ => TokenType.Identifier
                        };
                    }

                    // Return the character as-is for other cases
                    return TokenType.Error;
            }
        }

        return TokenType.EOF;
    }

    #endregion
}
