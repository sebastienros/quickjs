// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Unit tests for <see cref="TokenType"/>, <see cref="Token"/>, and related extensions.
/// </summary>
public class TokenTests
{
    #region TokenType Tests

    [Fact]
    public void TokenType_EOF_IsZero()
    {
        Assert.Equal(0, (int)TokenType.EOF);
    }

    [Fact]
    public void TokenType_AllKeywords_HaveKeywordStrings()
    {
        // All keyword token types should return a non-null keyword
        var keywords = new[]
        {
            TokenType.Null, TokenType.False, TokenType.True,
            TokenType.If, TokenType.Else, TokenType.Do, TokenType.While, TokenType.For,
            TokenType.Break, TokenType.Continue, TokenType.Switch, TokenType.Case, TokenType.Default,
            TokenType.Return, TokenType.Function, TokenType.Yield, TokenType.Await,
            TokenType.Throw, TokenType.Try, TokenType.Catch, TokenType.Finally,
            TokenType.Var, TokenType.Let, TokenType.Const,
            TokenType.Class, TokenType.Extends, TokenType.Super, TokenType.Static,
            TokenType.This, TokenType.New, TokenType.Delete, TokenType.Void, TokenType.TypeOf,
            TokenType.In, TokenType.Of, TokenType.InstanceOf,
            TokenType.Import, TokenType.Export, TokenType.Debugger, TokenType.With,
            TokenType.Enum, TokenType.Implements, TokenType.Interface, TokenType.Package,
            TokenType.Private, TokenType.Protected, TokenType.Public,
        };

        foreach (var keyword in keywords)
        {
            var keywordString = keyword.GetKeyword();
            Assert.NotNull(keywordString);
            Assert.NotEmpty(keywordString);
        }
    }

    [Fact]
    public void TokenType_NonKeywords_ReturnNullKeyword()
    {
        Assert.Null(TokenType.EOF.GetKeyword());
        Assert.Null(TokenType.Number.GetKeyword());
        Assert.Null(TokenType.Identifier.GetKeyword());
        Assert.Null(TokenType.Plus.GetKeyword());
    }

    [Theory]
    [InlineData(TokenType.LeftParen, "(")]
    [InlineData(TokenType.RightParen, ")")]
    [InlineData(TokenType.LeftBrace, "{")]
    [InlineData(TokenType.RightBrace, "}")]
    [InlineData(TokenType.LeftBracket, "[")]
    [InlineData(TokenType.RightBracket, "]")]
    [InlineData(TokenType.Comma, ",")]
    [InlineData(TokenType.Semicolon, ";")]
    [InlineData(TokenType.Colon, ":")]
    [InlineData(TokenType.Dot, ".")]
    [InlineData(TokenType.Ellipsis, "...")]
    public void TokenType_Punctuators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    [Theory]
    [InlineData(TokenType.Plus, "+")]
    [InlineData(TokenType.Minus, "-")]
    [InlineData(TokenType.Asterisk, "*")]
    [InlineData(TokenType.Slash, "/")]
    [InlineData(TokenType.Percent, "%")]
    [InlineData(TokenType.Power, "**")]
    [InlineData(TokenType.Increment, "++")]
    [InlineData(TokenType.Decrement, "--")]
    public void TokenType_ArithmeticOperators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    [Theory]
    [InlineData(TokenType.Equal, "==")]
    [InlineData(TokenType.NotEqual, "!=")]
    [InlineData(TokenType.StrictEqual, "===")]
    [InlineData(TokenType.StrictNotEqual, "!==")]
    [InlineData(TokenType.LessThan, "<")]
    [InlineData(TokenType.LessThanOrEqual, "<=")]
    [InlineData(TokenType.GreaterThan, ">")]
    [InlineData(TokenType.GreaterThanOrEqual, ">=")]
    public void TokenType_ComparisonOperators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    [Theory]
    [InlineData(TokenType.Assign, "=")]
    [InlineData(TokenType.PlusAssign, "+=")]
    [InlineData(TokenType.MinusAssign, "-=")]
    [InlineData(TokenType.AsteriskAssign, "*=")]
    [InlineData(TokenType.SlashAssign, "/=")]
    [InlineData(TokenType.LogicalAndAssign, "&&=")]
    [InlineData(TokenType.LogicalOrAssign, "||=")]
    [InlineData(TokenType.NullishCoalescingAssign, "??=")]
    public void TokenType_AssignmentOperators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    [Theory]
    [InlineData(TokenType.LeftShift, "<<")]
    [InlineData(TokenType.RightShift, ">>")]
    [InlineData(TokenType.UnsignedRightShift, ">>>")]
    [InlineData(TokenType.LeftShiftAssign, "<<=")]
    [InlineData(TokenType.RightShiftAssign, ">>=")]
    [InlineData(TokenType.UnsignedRightShiftAssign, ">>>=")]
    public void TokenType_ShiftOperators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    [Theory]
    [InlineData(TokenType.Arrow, "=>")]
    [InlineData(TokenType.OptionalChaining, "?.")]
    [InlineData(TokenType.NullishCoalescing, "??")]
    public void TokenType_ES6Operators_ReturnCorrectString(TokenType type, string expected)
    {
        Assert.Equal(expected, type.GetPunctuator());
    }

    #endregion

    #region Token Tests

    [Fact]
    public void Token_Constructor_SetsAllProperties()
    {
        var start = new SourceLocation("test.js", 1, 1);
        var end = new SourceLocation("test.js", 1, 5);
        var token = new Token(TokenType.Identifier, new SourceSlice("test"), start, end, "test", true);

        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("test", token.Text.ToString());
        Assert.Equal(start, token.Start);
        Assert.Equal(end, token.End);
        Assert.Equal("test", token.Value);
        Assert.True(token.HasLineTerminatorBefore);
    }

    [Fact]
    public void Token_SimpleConstructor_SameStartAndEnd()
    {
        var location = new SourceLocation("test.js", 1, 1);
        var token = new Token(TokenType.Plus, new SourceSlice("+"), location);

        Assert.Equal(location, token.Start);
        Assert.Equal(location, token.End);
    }

    [Fact]
    public void Token_IsEOF_TrueForEOFToken()
    {
        var token = new Token(TokenType.EOF, new SourceSlice(""), SourceLocation.Empty);
        Assert.True(token.IsEOF);
    }

    [Fact]
    public void Token_IsError_TrueForErrorToken()
    {
        var token = new Token(TokenType.Error, new SourceSlice("?"), SourceLocation.Empty);
        Assert.True(token.IsError);
    }

    [Fact]
    public void Token_IsKeyword_TrueForKeywords()
    {
        Assert.True(new Token(TokenType.If, new SourceSlice("if"), SourceLocation.Empty).IsKeyword);
        Assert.True(new Token(TokenType.Function, new SourceSlice("function"), SourceLocation.Empty).IsKeyword);
        Assert.True(new Token(TokenType.Class, new SourceSlice("class"), SourceLocation.Empty).IsKeyword);
        Assert.True(new Token(TokenType.Null, new SourceSlice("null"), SourceLocation.Empty).IsKeyword);
    }

    [Fact]
    public void Token_IsKeyword_FalseForNonKeywords()
    {
        Assert.False(new Token(TokenType.Identifier, new SourceSlice("myVar"), SourceLocation.Empty).IsKeyword);
        Assert.False(new Token(TokenType.Number, new SourceSlice("42"), SourceLocation.Empty).IsKeyword);
        Assert.False(new Token(TokenType.Plus, new SourceSlice("+"), SourceLocation.Empty).IsKeyword);
    }

    [Fact]
    public void Token_IsLiteral_TrueForLiterals()
    {
        Assert.True(new Token(TokenType.Number, new SourceSlice("42"), SourceLocation.Empty, 42.0).IsLiteral);
        Assert.True(new Token(TokenType.String, new SourceSlice("\"hello\""), SourceLocation.Empty, "hello").IsLiteral);
        Assert.True(new Token(TokenType.True, new SourceSlice("true"), SourceLocation.Empty).IsLiteral);
        Assert.True(new Token(TokenType.False, new SourceSlice("false"), SourceLocation.Empty).IsLiteral);
        Assert.True(new Token(TokenType.Null, new SourceSlice("null"), SourceLocation.Empty).IsLiteral);
    }

    [Fact]
    public void Token_IsAssignmentOperator_CorrectRange()
    {
        Assert.True(new Token(TokenType.Assign, new SourceSlice("="), SourceLocation.Empty).IsAssignmentOperator);
        Assert.True(new Token(TokenType.PlusAssign, new SourceSlice("+="), SourceLocation.Empty).IsAssignmentOperator);
        Assert.True(new Token(TokenType.NullishCoalescingAssign, new SourceSlice("??="), SourceLocation.Empty).IsAssignmentOperator);
        
        Assert.False(new Token(TokenType.Plus, new SourceSlice("+"), SourceLocation.Empty).IsAssignmentOperator);
        Assert.False(new Token(TokenType.Equal, new SourceSlice("=="), SourceLocation.Empty).IsAssignmentOperator);
    }

    [Fact]
    public void Token_IsComparisonOperator_CorrectRange()
    {
        Assert.True(new Token(TokenType.LessThan, new SourceSlice("<"), SourceLocation.Empty).IsComparisonOperator);
        Assert.True(new Token(TokenType.StrictEqual, new SourceSlice("==="), SourceLocation.Empty).IsComparisonOperator);
        
        Assert.False(new Token(TokenType.Assign, new SourceSlice("="), SourceLocation.Empty).IsComparisonOperator);
        Assert.False(new Token(TokenType.LogicalAnd, new SourceSlice("&&"), SourceLocation.Empty).IsComparisonOperator);
    }

    [Fact]
    public void Token_GetStringValue_ReturnsValueForStrings()
    {
        var token = new Token(TokenType.String, new SourceSlice("\"hello\""), SourceLocation.Empty, "hello");
        Assert.Equal("hello", token.GetStringValue());
    }

    [Fact]
    public void Token_GetStringValue_ReturnsNullForNonStrings()
    {
        var token = new Token(TokenType.Number, new SourceSlice("42"), SourceLocation.Empty, 42.0);
        Assert.Null(token.GetStringValue());
    }

    [Fact]
    public void Token_GetNumberValue_ReturnsValueForNumbers()
    {
        var token = new Token(TokenType.Number, new SourceSlice("42"), SourceLocation.Empty, 42.0);
        Assert.Equal(42.0, token.GetNumberValue());
    }

    [Fact]
    public void Token_GetNumberValue_ReturnsNullForNonNumbers()
    {
        var token = new Token(TokenType.String, new SourceSlice("\"42\""), SourceLocation.Empty, "42");
        Assert.Null(token.GetNumberValue());
    }

    [Fact]
    public void Token_GetNumberValue_HandlesIntegerValues()
    {
        var token = new Token(TokenType.Number, new SourceSlice("42"), SourceLocation.Empty, 42L);
        Assert.Equal(42.0, token.GetNumberValue());
    }

    [Fact]
    public void Token_ToString_IncludesTypeTextAndLocation()
    {
        var token = new Token(TokenType.Identifier, new SourceSlice("myVar"), new SourceLocation("test.js", 1, 5));
        var str = token.ToString();
        
        Assert.Contains("Identifier", str);
        Assert.Contains("myVar", str);
    }

    #endregion

    #region TokenType Extension Tests

    [Theory]
    [InlineData(TokenType.Plus, true)]
    [InlineData(TokenType.Minus, true)]
    [InlineData(TokenType.Asterisk, true)]
    [InlineData(TokenType.Slash, true)]
    [InlineData(TokenType.LessThan, true)]
    [InlineData(TokenType.LogicalAnd, true)]
    [InlineData(TokenType.In, true)]
    [InlineData(TokenType.InstanceOf, true)]
    [InlineData(TokenType.Assign, false)]
    [InlineData(TokenType.Identifier, false)]
    public void TokenType_IsBinaryOperator_ReturnsCorrectly(TokenType type, bool expected)
    {
        Assert.Equal(expected, type.IsBinaryOperator());
    }

    [Theory]
    [InlineData(TokenType.Plus, true)]
    [InlineData(TokenType.Minus, true)]
    [InlineData(TokenType.LogicalNot, true)]
    [InlineData(TokenType.Tilde, true)]
    [InlineData(TokenType.Increment, true)]
    [InlineData(TokenType.TypeOf, true)]
    [InlineData(TokenType.Void, true)]
    [InlineData(TokenType.Delete, true)]
    [InlineData(TokenType.Asterisk, false)]
    [InlineData(TokenType.Assign, false)]
    public void TokenType_IsUnaryOperator_ReturnsCorrectly(TokenType type, bool expected)
    {
        Assert.Equal(expected, type.IsUnaryOperator());
    }

    #endregion

    #region Keyword Mapping Tests

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
    public void Keyword_GetKeyword_ReturnsOriginalString(string keyword, TokenType type)
    {
        Assert.Equal(keyword, type.GetKeyword());
    }

    #endregion

    #region Strict Mode Reserved Words Tests

    [Fact]
    public void Token_IsStrictModeReserved_TrueForReservedWords()
    {
        Assert.True(new Token(TokenType.Implements, new SourceSlice("implements"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Interface, new SourceSlice("interface"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Package, new SourceSlice("package"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Private, new SourceSlice("private"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Protected, new SourceSlice("protected"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Public, new SourceSlice("public"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Let, new SourceSlice("let"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Yield, new SourceSlice("yield"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.True(new Token(TokenType.Static, new SourceSlice("static"), SourceLocation.Empty).IsStrictModeReserved);
    }

    [Fact]
    public void Token_IsStrictModeReserved_FalseForNonReserved()
    {
        Assert.False(new Token(TokenType.If, new SourceSlice("if"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.False(new Token(TokenType.Function, new SourceSlice("function"), SourceLocation.Empty).IsStrictModeReserved);
        Assert.False(new Token(TokenType.Identifier, new SourceSlice("myVar"), SourceLocation.Empty).IsStrictModeReserved);
    }

    #endregion
}
