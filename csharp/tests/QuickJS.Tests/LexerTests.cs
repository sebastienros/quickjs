// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Unit tests for <see cref="Lexer"/>.
/// </summary>
public class LexerTests
{
    #region Basic Tokenization Tests

    [Fact]
    public void Lexer_EmptySource_ReturnsEOF()
    {
        var lexer = new Lexer("");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.EOF, token.Type);
    }

    [Fact]
    public void Lexer_WhitespaceOnly_ReturnsEOF()
    {
        var lexer = new Lexer("   \t\n\r\n   ");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.EOF, token.Type);
        Assert.True(token.HasLineTerminatorBefore);
    }

    [Fact]
    public void Lexer_TokenizeAll_IncludesEOF()
    {
        var lexer = new Lexer("a");
        var tokens = lexer.TokenizeAll();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.EOF, tokens[1].Type);
    }

    #endregion

    #region Identifier Tests

    [Fact]
    public void Lexer_SimpleIdentifier_ReturnsIdentifier()
    {
        var lexer = new Lexer("myVariable");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("myVariable", token.Text);
        Assert.Equal("myVariable", token.Value);
    }

    [Theory]
    [InlineData("_private")]
    [InlineData("$jquery")]
    [InlineData("_")]
    [InlineData("$")]
    [InlineData("abc123")]
    [InlineData("camelCase")]
    [InlineData("PascalCase")]
    [InlineData("SCREAMING_SNAKE")]
    public void Lexer_ValidIdentifiers_Recognized(string identifier)
    {
        var lexer = new Lexer(identifier);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal(identifier, token.Text);
    }

    [Fact]
    public void Lexer_UnicodeIdentifier_Recognized()
    {
        var lexer = new Lexer("日本語");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("日本語", token.Text);
    }

    [Fact]
    public void Lexer_MultipleIdentifiers_SeparatedByWhitespace()
    {
        var lexer = new Lexer("foo bar baz");
        var tokens = lexer.TokenizeAll();
        Assert.Equal(4, tokens.Count);
        Assert.Equal("foo", tokens[0].Text);
        Assert.Equal("bar", tokens[1].Text);
        Assert.Equal("baz", tokens[2].Text);
    }

    #endregion

    #region Keyword Tests

    [Theory]
    [InlineData("null", TokenType.Null)]
    [InlineData("true", TokenType.True)]
    [InlineData("false", TokenType.False)]
    [InlineData("if", TokenType.If)]
    [InlineData("else", TokenType.Else)]
    [InlineData("for", TokenType.For)]
    [InlineData("while", TokenType.While)]
    [InlineData("do", TokenType.Do)]
    [InlineData("switch", TokenType.Switch)]
    [InlineData("case", TokenType.Case)]
    [InlineData("default", TokenType.Default)]
    [InlineData("break", TokenType.Break)]
    [InlineData("continue", TokenType.Continue)]
    [InlineData("return", TokenType.Return)]
    [InlineData("throw", TokenType.Throw)]
    [InlineData("try", TokenType.Try)]
    [InlineData("catch", TokenType.Catch)]
    [InlineData("finally", TokenType.Finally)]
    [InlineData("function", TokenType.Function)]
    [InlineData("var", TokenType.Var)]
    [InlineData("let", TokenType.Let)]
    [InlineData("const", TokenType.Const)]
    [InlineData("class", TokenType.Class)]
    [InlineData("extends", TokenType.Extends)]
    [InlineData("super", TokenType.Super)]
    [InlineData("static", TokenType.Static)]
    [InlineData("this", TokenType.This)]
    [InlineData("new", TokenType.New)]
    [InlineData("delete", TokenType.Delete)]
    [InlineData("void", TokenType.Void)]
    [InlineData("typeof", TokenType.TypeOf)]
    [InlineData("in", TokenType.In)]
    [InlineData("of", TokenType.Of)]
    [InlineData("instanceof", TokenType.InstanceOf)]
    [InlineData("import", TokenType.Import)]
    [InlineData("export", TokenType.Export)]
    [InlineData("yield", TokenType.Yield)]
    [InlineData("await", TokenType.Await)]
    [InlineData("debugger", TokenType.Debugger)]
    [InlineData("with", TokenType.With)]
    public void Lexer_Keywords_Recognized(string keyword, TokenType expected)
    {
        var lexer = new Lexer(keyword);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
        Assert.Equal(keyword, token.Text);
    }

    [Theory]
    [InlineData("enum", TokenType.Enum)]
    [InlineData("implements", TokenType.Implements)]
    [InlineData("interface", TokenType.Interface)]
    [InlineData("package", TokenType.Package)]
    [InlineData("private", TokenType.Private)]
    [InlineData("protected", TokenType.Protected)]
    [InlineData("public", TokenType.Public)]
    public void Lexer_ReservedWords_Recognized(string keyword, TokenType expected)
    {
        var lexer = new Lexer(keyword);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Fact]
    public void Lexer_KeywordPrefix_IsIdentifier()
    {
        // "iffy" starts with "if" but is not the keyword
        var lexer = new Lexer("iffy");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("iffy", token.Text);
    }

    #endregion

    #region Number Tests

    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("42", 42.0)]
    [InlineData("123456789", 123456789.0)]
    public void Lexer_DecimalIntegers_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("0.5", 0.5)]
    [InlineData(".5", 0.5)]
    [InlineData("10.0", 10.0)]
    public void Lexer_FloatingPoint_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Theory]
    [InlineData("1e10", 1e10)]
    [InlineData("1E10", 1e10)]
    [InlineData("1e+10", 1e10)]
    [InlineData("1e-10", 1e-10)]
    [InlineData("2.5e3", 2500.0)]
    public void Lexer_ScientificNotation_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Theory]
    [InlineData("0x10", 16.0)]
    [InlineData("0xFF", 255.0)]
    [InlineData("0xCAFE", 51966.0)]
    [InlineData("0X1A", 26.0)]
    public void Lexer_HexadecimalNumbers_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Theory]
    [InlineData("0b1010", 10.0)]
    [InlineData("0B11111111", 255.0)]
    public void Lexer_BinaryNumbers_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Theory]
    [InlineData("0o10", 8.0)]
    [InlineData("0O777", 511.0)]
    public void Lexer_OctalNumbers_Parsed(string source, double expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(expected, token.GetNumberValue());
    }

    [Fact]
    public void Lexer_NumericSeparators_Parsed()
    {
        var lexer = new Lexer("1_000_000");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(1_000_000.0, token.GetNumberValue());
    }

    [Fact]
    public void Lexer_BigIntLiteral_Parsed()
    {
        var lexer = new Lexer("123n");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal("123n", token.Text);
        // BigInt is stored as long
        Assert.Equal(123L, token.Value);
    }

    [Fact]
    public void Lexer_BinaryBigIntLiteral_Parsed()
    {
        // Test via lexer - binary BigInt literal
        var lexer = new Lexer("0b1010n");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.True(token.Text == "0b1010n", $"Expected text '0b1010n' but got '{token.Text}'");
        var actualType = token.Value?.GetType()?.Name ?? "null";
        var actualValue = token.Value;
        Assert.True(token.Value is long, $"Expected long but got {actualType} with value {actualValue}, text='{token.Text}'");
        Assert.Equal(10L, (long)token.Value!);
    }

    [Fact]
    public void Lexer_HexBigIntLiteral_Parsed()
    {
        var lexer = new Lexer("0xFFn");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal("0xFFn", token.Text);
        Assert.Equal(255L, token.Value);
    }

    [Fact]
    public void Lexer_OctalBigIntLiteral_Parsed()
    {
        var lexer = new Lexer("0o777n");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal("0o777n", token.Text);
        Assert.Equal(511L, token.Value);
    }

    #endregion

    #region String Tests

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("'hello'", "hello")]
    [InlineData("\"\"", "")]
    [InlineData("''", "")]
    public void Lexer_SimpleStrings_Parsed(string source, string expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(expected, token.GetStringValue());
    }

    [Theory]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]
    [InlineData("\"tab\\there\"", "tab\there")]
    [InlineData("\"back\\\\slash\"", "back\\slash")]
    [InlineData("\"quote\\\"here\"", "quote\"here")]
    [InlineData("'quote\\'here'", "quote'here")]
    [InlineData("\"\\r\\n\"", "\r\n")]
    public void Lexer_EscapeSequences_Parsed(string source, string expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(expected, token.GetStringValue());
    }

    [Theory]
    [InlineData("\"\\x41\"", "A")]
    [InlineData("\"\\x61\\x62\\x63\"", "abc")]
    public void Lexer_HexEscapes_Parsed(string source, string expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(expected, token.GetStringValue());
    }

    [Theory]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("\"\\u{41}\"", "A")]
    [InlineData("\"\\u{1F600}\"", "😀")]
    public void Lexer_UnicodeEscapes_Parsed(string source, string expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(expected, token.GetStringValue());
    }

    #endregion

    #region Template Literal Tests

    [Fact]
    public void Lexer_SimpleTemplateLiteral_Parsed()
    {
        var lexer = new Lexer("`hello world`");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Template, token.Type);
        Assert.Equal("hello world", token.GetStringValue());
    }

    [Fact]
    public void Lexer_TemplateLiteral_WithNewline()
    {
        var lexer = new Lexer("`line1\nline2`");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.Template, token.Type);
        Assert.Equal("line1\nline2", token.GetStringValue());
    }

    #endregion

    #region Comment Tests

    [Fact]
    public void Lexer_SingleLineComment_Skipped()
    {
        var lexer = new Lexer("a // comment\nb");
        var tokens = lexer.TokenizeAll();
        Assert.Equal(3, tokens.Count);
        Assert.Equal("a", tokens[0].Text);
        Assert.Equal("b", tokens[1].Text);
        Assert.True(tokens[1].HasLineTerminatorBefore);
    }

    [Fact]
    public void Lexer_MultiLineComment_Skipped()
    {
        var lexer = new Lexer("a /* comment */ b");
        var tokens = lexer.TokenizeAll();
        Assert.Equal(3, tokens.Count);
        Assert.Equal("a", tokens[0].Text);
        Assert.Equal("b", tokens[1].Text);
    }

    [Fact]
    public void Lexer_MultiLineComment_WithNewline_SetsFlag()
    {
        var lexer = new Lexer("a /* line1\nline2 */ b");
        var tokens = lexer.TokenizeAll();
        Assert.True(tokens[1].HasLineTerminatorBefore);
    }

    #endregion

    #region Punctuator Tests

    [Theory]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    [InlineData("{", TokenType.LeftBrace)]
    [InlineData("}", TokenType.RightBrace)]
    [InlineData("[", TokenType.LeftBracket)]
    [InlineData("]", TokenType.RightBracket)]
    [InlineData(",", TokenType.Comma)]
    [InlineData(";", TokenType.Semicolon)]
    [InlineData(":", TokenType.Colon)]
    [InlineData(".", TokenType.Dot)]
    [InlineData("...", TokenType.Ellipsis)]
    public void Lexer_Punctuators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
        Assert.Equal(source, token.Text);
    }

    #endregion

    #region Operator Tests

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Asterisk)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("%", TokenType.Percent)]
    [InlineData("**", TokenType.Power)]
    [InlineData("++", TokenType.Increment)]
    [InlineData("--", TokenType.Decrement)]
    public void Lexer_ArithmeticOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Theory]
    [InlineData("<", TokenType.LessThan)]
    [InlineData("<=", TokenType.LessThanOrEqual)]
    [InlineData(">", TokenType.GreaterThan)]
    [InlineData(">=", TokenType.GreaterThanOrEqual)]
    [InlineData("==", TokenType.Equal)]
    [InlineData("!=", TokenType.NotEqual)]
    [InlineData("===", TokenType.StrictEqual)]
    [InlineData("!==", TokenType.StrictNotEqual)]
    public void Lexer_ComparisonOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Theory]
    [InlineData("&&", TokenType.LogicalAnd)]
    [InlineData("||", TokenType.LogicalOr)]
    [InlineData("!", TokenType.LogicalNot)]
    [InlineData("??", TokenType.NullishCoalescing)]
    public void Lexer_LogicalOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Theory]
    [InlineData("&", TokenType.Ampersand)]
    [InlineData("|", TokenType.Pipe)]
    [InlineData("^", TokenType.Caret)]
    [InlineData("~", TokenType.Tilde)]
    [InlineData("<<", TokenType.LeftShift)]
    [InlineData(">>", TokenType.RightShift)]
    [InlineData(">>>", TokenType.UnsignedRightShift)]
    public void Lexer_BitwiseOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Theory]
    [InlineData("=", TokenType.Assign)]
    [InlineData("+=", TokenType.PlusAssign)]
    [InlineData("-=", TokenType.MinusAssign)]
    [InlineData("*=", TokenType.AsteriskAssign)]
    [InlineData("/=", TokenType.SlashAssign)]
    [InlineData("%=", TokenType.PercentAssign)]
    [InlineData("**=", TokenType.PowerAssign)]
    [InlineData("&=", TokenType.AmpersandAssign)]
    [InlineData("|=", TokenType.PipeAssign)]
    [InlineData("^=", TokenType.CaretAssign)]
    [InlineData("<<=", TokenType.LeftShiftAssign)]
    [InlineData(">>=", TokenType.RightShiftAssign)]
    [InlineData(">>>=", TokenType.UnsignedRightShiftAssign)]
    [InlineData("&&=", TokenType.LogicalAndAssign)]
    [InlineData("||=", TokenType.LogicalOrAssign)]
    [InlineData("??=", TokenType.NullishCoalescingAssign)]
    public void Lexer_AssignmentOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    [Theory]
    [InlineData("?", TokenType.Question)]
    [InlineData("=>", TokenType.Arrow)]
    [InlineData("?.", TokenType.OptionalChaining)]
    public void Lexer_MiscOperators_Recognized(string source, TokenType expected)
    {
        var lexer = new Lexer(source);
        var token = lexer.NextToken();
        Assert.Equal(expected, token.Type);
    }

    #endregion

    #region Private Name Tests

    [Fact]
    public void Lexer_PrivateName_Recognized()
    {
        var lexer = new Lexer("#privateField");
        var token = lexer.NextToken();
        Assert.Equal(TokenType.PrivateName, token.Type);
        Assert.Equal("#privateField", token.Text);
    }

    #endregion

    #region Location Tracking Tests

    [Fact]
    public void Lexer_TracksLineNumbers()
    {
        var lexer = new Lexer("a\nb\nc");
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(1, tokens[0].Start.Line);
        Assert.Equal(2, tokens[1].Start.Line);
        Assert.Equal(3, tokens[2].Start.Line);
    }

    [Fact]
    public void Lexer_TracksColumnNumbers()
    {
        var lexer = new Lexer("ab cd ef");
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(1, tokens[0].Start.Column);
        Assert.Equal(4, tokens[1].Start.Column);
        Assert.Equal(7, tokens[2].Start.Column);
    }

    [Fact]
    public void Lexer_TracksFileName()
    {
        var lexer = new Lexer("x", "myfile.js");
        var token = lexer.NextToken();
        Assert.Equal("myfile.js", token.Start.FileName);
    }

    #endregion

    #region HasLineTerminatorBefore Tests

    [Fact]
    public void Lexer_NoLineTerminator_FlagIsFalse()
    {
        var lexer = new Lexer("a b");
        lexer.NextToken(); // a
        var token = lexer.NextToken(); // b
        Assert.False(token.HasLineTerminatorBefore);
    }

    [Fact]
    public void Lexer_WithLineTerminator_FlagIsTrue()
    {
        var lexer = new Lexer("a\nb");
        lexer.NextToken(); // a
        var token = lexer.NextToken(); // b
        Assert.True(token.HasLineTerminatorBefore);
    }

    [Fact]
    public void Lexer_WithCRLF_FlagIsTrue()
    {
        var lexer = new Lexer("a\r\nb");
        lexer.NextToken(); // a
        var token = lexer.NextToken(); // b
        Assert.True(token.HasLineTerminatorBefore);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Lexer_SimpleFunction()
    {
        var source = "function add(a, b) { return a + b; }";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        var types = tokens.Select(t => t.Type).ToList();
        Assert.Equal(new[]
        {
            TokenType.Function,
            TokenType.Identifier,      // add
            TokenType.LeftParen,
            TokenType.Identifier,      // a
            TokenType.Comma,
            TokenType.Identifier,      // b
            TokenType.RightParen,
            TokenType.LeftBrace,
            TokenType.Return,
            TokenType.Identifier,      // a
            TokenType.Plus,
            TokenType.Identifier,      // b
            TokenType.Semicolon,
            TokenType.RightBrace,
            TokenType.EOF,
        }, types);
    }

    [Fact]
    public void Lexer_ArrowFunction()
    {
        var source = "const fn = (x) => x * 2;";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(TokenType.Const, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal(TokenType.Assign, tokens[2].Type);
        Assert.Equal(TokenType.LeftParen, tokens[3].Type);
        Assert.Equal(TokenType.Identifier, tokens[4].Type);
        Assert.Equal(TokenType.RightParen, tokens[5].Type);
        Assert.Equal(TokenType.Arrow, tokens[6].Type);
    }

    [Fact]
    public void Lexer_ClassDefinition()
    {
        var source = "class Point extends Shape { #x; #y; }";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(TokenType.Class, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type); // Point
        Assert.Equal(TokenType.Extends, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type); // Shape
        Assert.Equal(TokenType.LeftBrace, tokens[4].Type);
        Assert.Equal(TokenType.PrivateName, tokens[5].Type); // #x
        Assert.Equal(TokenType.Semicolon, tokens[6].Type);
        Assert.Equal(TokenType.PrivateName, tokens[7].Type); // #y
    }

    [Fact]
    public void Lexer_NullishCoalescing()
    {
        var source = "x ?? y ??= z";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(TokenType.Identifier, tokens[0].Type); // x
        Assert.Equal(TokenType.NullishCoalescing, tokens[1].Type); // ??
        Assert.Equal(TokenType.Identifier, tokens[2].Type); // y
        Assert.Equal(TokenType.NullishCoalescingAssign, tokens[3].Type); // ??=
        Assert.Equal(TokenType.Identifier, tokens[4].Type); // z
    }

    [Fact]
    public void Lexer_OptionalChaining()
    {
        var source = "obj?.prop?.method?.()";
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll();
        
        Assert.Equal(TokenType.Identifier, tokens[0].Type);  // obj
        Assert.Equal(TokenType.OptionalChaining, tokens[1].Type); // ?.
        Assert.Equal(TokenType.Identifier, tokens[2].Type);  // prop
        Assert.Equal(TokenType.OptionalChaining, tokens[3].Type); // ?.
        Assert.Equal(TokenType.Identifier, tokens[4].Type);  // method
        Assert.Equal(TokenType.OptionalChaining, tokens[5].Type); // ?.
        Assert.Equal(TokenType.LeftParen, tokens[6].Type);
        Assert.Equal(TokenType.RightParen, tokens[7].Type);
    }

    #endregion
}
