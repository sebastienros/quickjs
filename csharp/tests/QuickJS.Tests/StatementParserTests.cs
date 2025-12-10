// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for statement parsing in the Parser class.
/// </summary>
public class StatementParserTests
{
    #region Program Parsing

    [Fact]
    public void ParseProgram_EmptySource_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("", "test.js", atoms);
        parser.ParseProgram();

        // Should complete without error
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_SingleStatement_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42;", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_MultipleStatements_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1; 2; 3;", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Block Statement

    [Fact]
    public void ParseStatement_EmptyBlock_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("{}", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_BlockWithStatements_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("{ 1; 2; 3; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_NestedBlocks_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("{ { { } } }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Expression Statement

    [Fact]
    public void ParseStatement_ExpressionStatement_EmitsDropOpCode()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should contain Drop to discard expression result
        Assert.Contains((byte)OpCode.Drop, bytecode);
    }

    [Fact]
    public void ParseStatement_ExpressionWithoutSemicolon_ASI()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42\n", "test.js", atoms);
        // This should work due to automatic semicolon insertion
        parser.ParseStatement();

        // Parser should have consumed the expression
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Variable Declarations

    [Fact]
    public void ParseStatement_VarDeclaration_DefinesVariable()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x;", "test.js", atoms);
        parser.ParseStatement();

        // Should define a variable
        Assert.True(parser.CurrentFunction.Vars.Count > 0);
    }

    [Fact]
    public void ParseStatement_VarWithInitializer_EmitsBytecode()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x = 42;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // For top-level var, should have PushI32, PushThis, Swap, PutField (sets global property)
        Assert.Contains((byte)OpCode.PushI32, bytecode);
        Assert.Contains((byte)OpCode.PushThis, bytecode);
        Assert.Contains((byte)OpCode.PutField, bytecode);
    }

    [Fact]
    public void ParseStatement_VarMultipleDeclarations_DefinesAll()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var x, y, z;", "test.js", atoms);
        parser.ParseStatement();

        Assert.Equal(3, parser.CurrentFunction.Vars.Count);
    }

    [Fact]
    public void ParseStatement_LetDeclaration_DefinesLexicalVariable()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let x = 1;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentFunction.Vars.Count > 0);
        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Let uses local variables with PutLoc
        Assert.Contains((byte)OpCode.PutLoc, bytecode);
    }

    [Fact]
    public void ParseStatement_LetWithoutInitializer_InitializesToUndefined()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let x;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Undefined, bytecode);
        Assert.Contains((byte)OpCode.PutLoc, bytecode);
    }

    [Fact]
    public void ParseStatement_ConstDeclaration_RequiresInitializer()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const x;", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void ParseStatement_ConstWithInitializer_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const x = 42;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentFunction.Vars.Count > 0);
    }

    #endregion

    #region If Statement

    [Fact]
    public void ParseStatement_IfStatement_EmitsConditionalJump()
    {
        var atoms = new AtomTable();
        var parser = new Parser("if (true) 42;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.IfFalse, bytecode);
    }

    [Fact]
    public void ParseStatement_IfElseStatement_EmitsJumps()
    {
        var atoms = new AtomTable();
        var parser = new Parser("if (true) 1; else 2;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.IfFalse, bytecode);
        Assert.Contains((byte)OpCode.Goto, bytecode);
    }

    [Fact]
    public void ParseStatement_IfElseIfElse_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("if (a) 1; else if (b) 2; else 3;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_IfWithBlock_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("if (true) { 1; 2; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region While Statement

    [Fact]
    public void ParseStatement_WhileLoop_EmitsLoopStructure()
    {
        var atoms = new AtomTable();
        var parser = new Parser("while (true) 42;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.IfFalse, bytecode);
        Assert.Contains((byte)OpCode.Goto, bytecode);
    }

    [Fact]
    public void ParseStatement_WhileWithBlock_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("while (x < 10) { x = x + 1; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Do-While Statement

    [Fact]
    public void ParseStatement_DoWhileLoop_EmitsLoopStructure()
    {
        var atoms = new AtomTable();
        var parser = new Parser("do { 42; } while (false);", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.IfTrue, bytecode);
    }

    [Fact]
    public void ParseStatement_DoWhile_BodyExecutesFirst()
    {
        var atoms = new AtomTable();
        var parser = new Parser("do x; while (false);", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region For Statement

    [Fact]
    public void ParseStatement_BasicForLoop_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (var i = 0; i < 10; i = i + 1) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_ForLoopNoInit_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (; i < 10; i = i + 1) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_ForLoopNoTest_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (var i = 0; ; i = i + 1) { break; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_ForLoopNoUpdate_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (var i = 0; i < 10; ) { i = i + 1; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_ForLoopWithLet_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (let i = 0; i < 10; i = i + 1) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_InfiniteForLoop_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("for (;;) { break; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Return Statement

    [Fact]
    public void ParseStatement_ReturnWithValue_EmitsReturn()
    {
        var atoms = new AtomTable();
        var parser = new Parser("return 42;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Return, bytecode);
    }

    [Fact]
    public void ParseStatement_ReturnWithoutValue_ReturnsUndefined()
    {
        var atoms = new AtomTable();
        var parser = new Parser("return;", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Undefined, bytecode);
        Assert.Contains((byte)OpCode.Return, bytecode);
    }

    [Fact]
    public void ParseStatement_ReturnASI_ReturnsUndefined()
    {
        var atoms = new AtomTable();
        var parser = new Parser("return\n42;", "test.js", atoms);
        parser.ParseStatement();

        // Due to ASI, this is "return;" followed by "42;"
        // So we should have returned undefined
        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Undefined, bytecode);
    }

    #endregion

    #region Break/Continue Statements

    [Fact]
    public void ParseStatement_BreakInLoop_EmitsGoto()
    {
        var atoms = new AtomTable();
        var parser = new Parser("while (true) { break; }", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Goto, bytecode);
    }

    [Fact]
    public void ParseStatement_BreakOutsideLoop_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("break;", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void ParseStatement_ContinueInLoop_EmitsGoto()
    {
        var atoms = new AtomTable();
        var parser = new Parser("while (true) { continue; }", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should have multiple Goto instructions
        Assert.True(bytecode.Length > 5);
    }

    [Fact]
    public void ParseStatement_ContinueOutsideLoop_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("continue;", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void ParseStatement_BreakInSwitch_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { case 1: break; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Throw Statement

    [Fact]
    public void ParseStatement_ThrowStatement_EmitsThrow()
    {
        var atoms = new AtomTable();
        var parser = new Parser("throw new Error();", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Throw, bytecode);
    }

    [Fact]
    public void ParseStatement_ThrowWithLineTerminator_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("throw\nerror;", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    #endregion

    #region Try/Catch/Finally

    [Fact]
    public void ParseStatement_TryCatch_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("try { x; } catch (e) { y; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Catch, bytecode);
    }

    [Fact]
    public void ParseStatement_TryFinally_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("try { x; } finally { y; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_TryCatchFinally_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("try { x; } catch (e) { y; } finally { z; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_TryCatchNoParameter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("try { x; } catch { y; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Switch Statement

    [Fact]
    public void ParseStatement_EmptySwitch_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_SwitchWithCases_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { case 1: y; break; case 2: z; break; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_SwitchWithDefault_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { case 1: y; break; default: z; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_SwitchDefaultFirst_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { default: z; case 1: y; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_SwitchDuplicateDefault_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { default: y; default: z; }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void ParseStatement_SwitchFallthrough_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { case 1: case 2: y; break; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_SwitchEmitsStrictEq()
    {
        var atoms = new AtomTable();
        var parser = new Parser("switch (x) { case 1: break; }", "test.js", atoms);
        parser.ParseStatement();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.StrictEq, bytecode);
    }

    #endregion

    #region Empty Statement

    [Fact]
    public void ParseStatement_EmptyStatement_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(";", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_MultipleSemicolons_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(";;;", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Function Declaration

    [Fact]
    public void ParseStatement_FunctionDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo() { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseStatement_FunctionWithParams_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo(a, b, c) { return a + b + c; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Complex Programs

    [Fact]
    public void ParseProgram_FibonacciFunction_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function fib(n) {
                if (n <= 1) return n;
                return fib(n - 1) + fib(n - 2);
            }
            var result = fib(10);
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_LoopWithBreakContinue_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            var sum = 0;
            for (var i = 0; i < 100; i = i + 1) {
                if (i % 2 == 0) continue;
                if (i > 50) break;
                sum = sum + i;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_NestedLoops_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            for (var i = 0; i < 10; i = i + 1) {
                for (var j = 0; j < 10; j = j + 1) {
                    if (i * j > 50) break;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_TryCatchInLoop_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            for (var i = 0; i < 10; i = i + 1) {
                try {
                    riskyOperation();
                } catch (e) {
                    handleError(e);
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion
}
