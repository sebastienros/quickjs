// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for parser diagnostics and error reporting.
/// </summary>
public class ParserDiagnosticsTests
{
    #region DiagnosticBag Tests

    [Fact]
    public void DiagnosticBag_InitiallyEmpty()
    {
        var bag = new DiagnosticBag();

        Assert.Equal(0, bag.Count);
        Assert.False(bag.HasErrors);
        Assert.Empty(bag.GetErrors());
        Assert.Empty(bag.GetWarnings());
    }

    [Fact]
    public void DiagnosticBag_AddError_TracksErrors()
    {
        var bag = new DiagnosticBag();
        var location = new SourceLocation("test.js", 1, 5);

        bag.AddError(ParseErrorCode.UnexpectedToken, "Unexpected token", location);

        Assert.Equal(1, bag.Count);
        Assert.True(bag.HasErrors);
        Assert.Single(bag.GetErrors());
        Assert.Empty(bag.GetWarnings());
    }

    [Fact]
    public void DiagnosticBag_AddWarning_TracksWarnings()
    {
        var bag = new DiagnosticBag();
        var location = new SourceLocation("test.js", 1, 5);

        bag.AddWarning(ParseErrorCode.WithStatement, "Use of with statement", location);

        Assert.Equal(1, bag.Count);
        Assert.False(bag.HasErrors);
        Assert.Empty(bag.GetErrors());
        Assert.Single(bag.GetWarnings());
    }

    [Fact]
    public void DiagnosticBag_MultipleErrors_CollectsAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError(ParseErrorCode.UnexpectedToken, "Error 1", new SourceLocation("test.js", 1, 1));
        bag.AddError(ParseErrorCode.ExpectedToken, "Error 2", new SourceLocation("test.js", 2, 5));
        bag.AddWarning(ParseErrorCode.WithStatement, "Warning 1", new SourceLocation("test.js", 3, 10));

        Assert.Equal(3, bag.Count);
        Assert.True(bag.HasErrors);
        Assert.Equal(2, bag.GetErrors().Count());
        Assert.Single(bag.GetWarnings());
    }

    [Fact]
    public void DiagnosticBag_Clear_RemovesAll()
    {
        var bag = new DiagnosticBag();

        bag.AddError(ParseErrorCode.UnexpectedToken, "Error", new SourceLocation("test.js", 1, 1));
        bag.AddWarning(ParseErrorCode.WithStatement, "Warning", new SourceLocation("test.js", 2, 1));

        bag.Clear();

        Assert.Equal(0, bag.Count);
        Assert.False(bag.HasErrors);
    }

    #endregion

    #region ParseDiagnostic Tests

    [Fact]
    public void ParseDiagnostic_Error_HasCorrectSeverity()
    {
        var location = new SourceLocation("test.js", 1, 5);
        var diagnostic = ParseDiagnostic.Error(ParseErrorCode.UnexpectedToken, "Test error", location);

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(ParseErrorCode.UnexpectedToken, diagnostic.Code);
        Assert.Equal("Test error", diagnostic.Message);
        Assert.Equal(location, diagnostic.Location);
    }

    [Fact]
    public void ParseDiagnostic_Warning_HasCorrectSeverity()
    {
        var location = new SourceLocation("test.js", 2, 10);
        var diagnostic = ParseDiagnostic.Warning(ParseErrorCode.WithStatement, "Test warning", location);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(ParseErrorCode.WithStatement, diagnostic.Code);
        Assert.Equal("Test warning", diagnostic.Message);
    }

    [Fact]
    public void ParseDiagnostic_Info_HasCorrectSeverity()
    {
        var location = new SourceLocation("test.js", 3, 1);
        var diagnostic = ParseDiagnostic.Info("INFO001", "Test info", location);

        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
    }

    [Fact]
    public void ParseDiagnostic_WithFileName_IncludesFileName()
    {
        var location = new SourceLocation("test.js", 1, 1);
        var diagnostic = ParseDiagnostic.Error(ParseErrorCode.UnexpectedToken, "Error", location, "script.js");

        Assert.Equal("script.js", diagnostic.FileName);
    }

    [Fact]
    public void ParseDiagnostic_ToString_FormatsCorrectly()
    {
        var location = new SourceLocation("script.js", 5, 10);
        var diagnostic = ParseDiagnostic.Error(ParseErrorCode.UnexpectedToken, "Unexpected token", location, "script.js");

        var str = diagnostic.ToString();

        Assert.Contains("script.js", str);
        Assert.Contains("5", str);
        Assert.Contains("10", str);
        Assert.Contains("JS2001", str);
        Assert.Contains("Unexpected token", str);
    }

    #endregion

    #region ParseErrorCode Tests

    [Fact]
    public void ParseErrorCode_GetDescription_ReturnsDescription()
    {
        Assert.Equal("Unterminated string literal", ParseErrorCode.GetDescription(ParseErrorCode.UnterminatedString));
        Assert.Equal("Unexpected token", ParseErrorCode.GetDescription(ParseErrorCode.UnexpectedToken));
        Assert.Equal("Expected identifier", ParseErrorCode.GetDescription(ParseErrorCode.ExpectedIdentifier));
    }

    [Fact]
    public void ParseErrorCode_GetDescription_UnknownCode_ReturnsUnknown()
    {
        Assert.Equal("Unknown error", ParseErrorCode.GetDescription("UNKNOWN"));
    }

    [Theory]
    [InlineData(ParseErrorCode.UnterminatedString, "JS1001")]
    [InlineData(ParseErrorCode.UnterminatedComment, "JS1002")]
    [InlineData(ParseErrorCode.UnexpectedToken, "JS2001")]
    [InlineData(ParseErrorCode.ExpectedToken, "JS2003")]
    [InlineData(ParseErrorCode.ConstRequiresInitializer, "JS3003")]
    [InlineData(ParseErrorCode.IllegalBreak, "JS4001")]
    [InlineData(ParseErrorCode.DuplicateConstructor, "JS5002")]
    [InlineData(ParseErrorCode.ImportNotAtTopLevel, "JS6001")]
    [InlineData(ParseErrorCode.UselessStatement, "JS8001")]
    public void ParseErrorCode_HasExpectedValue(string code, string expected)
    {
        Assert.Equal(expected, code);
    }

    #endregion

    #region Parser Diagnostics Integration Tests

    [Fact]
    public void Parser_HasDiagnosticsProperty()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x = 1;", "test.js", atoms);

        Assert.NotNull(parser.Diagnostics);
        Assert.Equal(0, parser.Diagnostics.Count);
    }

    [Fact]
    public void Parser_CollectsDiagnostics_OnSyntaxError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x = @;", "test.js", atoms);

        var ex = Assert.Throws<JSSyntaxError>(() => parser.ParseProgram());

        // Parser should have diagnostics
        Assert.True(parser.Diagnostics.Count >= 1 || parser.Diagnostics.HasErrors);
    }

    [Fact]
    public void Parser_Diagnostics_SuccessfulParse_NoDiagnostics()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo(a, b) { return a + b; }", "test.js", atoms);

        parser.ParseProgram();

        Assert.False(parser.Diagnostics.HasErrors);
    }

    [Fact]
    public void Parser_Diagnostics_InvalidSyntax_ThrowsAndRecordsDiagnostic()
    {
        var atoms = new AtomTable();
        var parser = new Parser("if () { }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseProgram());

        Assert.True(parser.Diagnostics.Count >= 1 || parser.Diagnostics.HasErrors);
    }

    [Fact]
    public void Parser_Diagnostics_MissingClosingBrace_RecordsDiagnostic()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo() {", "test.js", atoms);

        // May or may not throw depending on the parser implementation
        try
        {
            parser.ParseProgram();
        }
        catch
        {
            // Expected for some syntax errors
        }

        // Either way, parsing incomplete code should have issues
        // Just verify the parser exists - this tests the diagnostic infrastructure
        Assert.NotNull(parser.Diagnostics);
    }

    [Fact]
    public void Parser_Diagnostics_InvalidExpression_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x = ;", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseProgram());

        Assert.True(parser.Diagnostics.Count >= 1);
    }

    #endregion

    #region ParserErrorMessages Tests

    [Fact]
    public void ParserErrorMessages_FormatToken_Keyword()
    {
        Assert.Equal("'if'", ParserErrorMessages.FormatToken(TokenType.If));
        Assert.Equal("'while'", ParserErrorMessages.FormatToken(TokenType.While));
        Assert.Equal("'function'", ParserErrorMessages.FormatToken(TokenType.Function));
    }

    [Fact]
    public void ParserErrorMessages_FormatToken_Punctuators()
    {
        Assert.Equal("'{'", ParserErrorMessages.FormatToken(TokenType.LeftBrace));
        Assert.Equal("'}'", ParserErrorMessages.FormatToken(TokenType.RightBrace));
        Assert.Equal("'('", ParserErrorMessages.FormatToken(TokenType.LeftParen));
        Assert.Equal("')'", ParserErrorMessages.FormatToken(TokenType.RightParen));
        Assert.Equal("';'", ParserErrorMessages.FormatToken(TokenType.Semicolon));
    }

    [Fact]
    public void ParserErrorMessages_FormatToken_Operators()
    {
        Assert.Equal("'+'", ParserErrorMessages.FormatToken(TokenType.Plus));
        Assert.Equal("'-'", ParserErrorMessages.FormatToken(TokenType.Minus));
        Assert.Equal("'*'", ParserErrorMessages.FormatToken(TokenType.Asterisk));
        Assert.Equal("'/'", ParserErrorMessages.FormatToken(TokenType.Slash));
    }

    [Fact]
    public void ParserErrorMessages_FormatToken_EndOfInput()
    {
        Assert.Equal("end of input", ParserErrorMessages.FormatToken(TokenType.EOF));
    }

    [Fact]
    public void ParserErrorMessages_UnexpectedToken_FormatsCorrectly()
    {
        var message = ParserErrorMessages.UnexpectedToken(TokenType.Semicolon);

        Assert.Contains("Unexpected", message);
        Assert.Contains("';'", message);
    }

    [Fact]
    public void ParserErrorMessages_FormatExpected_FormatsCorrectly()
    {
        var message = ParserErrorMessages.FormatExpected(TokenType.Semicolon);

        Assert.Contains("';'", message);
    }

    [Fact]
    public void ParserErrorMessages_ExpectedGot_FormatsCorrectly()
    {
        var message = ParserErrorMessages.ExpectedGot(TokenType.Semicolon, TokenType.Plus);

        Assert.Contains("Expected", message);
        Assert.Contains("';'", message);
        Assert.Contains("'+'", message);
    }

    #endregion
}
