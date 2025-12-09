// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for function parsing in the JavaScript parser.
/// 
/// This includes function declarations, function expressions, arrow functions,
/// async functions, generators, and related features.
/// </summary>
public class FunctionParserTests
{
    #region Function Declarations

    [Fact]
    public void ParseFunctionDeclaration_Simple_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo() { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_WithParameters_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function add(a, b) { return a + b; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_MultipleParameters_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(a, b, c, d) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_WithBody_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function greet(name) {
                var message = 'Hello, ' + name;
                return message;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_Nested_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function outer() {
                function inner() {
                    return 42;
                }
                return inner();
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_DeepNesting_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function a() {
                function b() {
                    function c() {
                        return 1;
                    }
                    return c();
                }
                return b();
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_WithoutName_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function () { }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    #endregion

    #region Generator Functions

    [Fact]
    public void ParseFunctionDeclaration_Generator_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function* gen() { yield 1; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_GeneratorWithYield_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function* range(start, end) {
                for (let i = start; i < end; i = i + 1) {
                    yield i;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_GeneratorEmpty_Succeeds()
    {
        // Generator without yield works
        var atoms = new AtomTable();
        var parser = new Parser("function* gen() { return 1; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Async Functions

    [Fact]
    public void ParseFunctionDeclaration_Async_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("async function fetchData() { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_AsyncWithAwait_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            async function loadData() {
                var data = await fetch();
                return data;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_AsyncGenerator_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("async function* asyncGen() { yield 1; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Function Expressions

    [Fact]
    public void ParseFunctionExpression_Anonymous_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = function() { };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionExpression_Named_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = function factorial(n) { return n * factorial(n - 1); };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionExpression_IIFE_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("(function() { return 42; })();", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionExpression_AsArgument_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("arr.map(function(x) { return x * 2; });", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Arrow Functions

    [Fact]
    public void ParseArrowFunction_SingleParam_NoParens_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = x => x * 2;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_NoParams_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = () => 42;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_MultipleParams_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var add = (a, b) => a + b;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_WithBlock_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = (x) => { return x * 2; };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_Nested_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = x => y => x + y;", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_AsyncArrow_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = async () => await fetch();", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseArrowFunction_AsyncWithParams_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = async (url) => await fetch(url);", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Default Parameters

    [Fact]
    public void ParseFunctionDeclaration_DefaultParameter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function greet(name = 'World') { return name; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_MultipleDefaults_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(a = 1, b = 2, c = 3) { return a + b + c; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_MixedDefaults_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(required, optional = 'default') { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_DefaultWithExpression_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(x = 1 + 2 * 3) { return x; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Rest Parameters

    [Fact]
    public void ParseFunctionDeclaration_RestParameter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function sum(...nums) { return nums; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_RestWithOthers_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(first, second, ...rest) { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_RestNotLast_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function test(...rest, last) { }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    #endregion

    #region Return Statements

    [Fact]
    public void ParseFunctionDeclaration_ReturnWithValue_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function getValue() { return 42; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_ReturnNoValue_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function exit() { return; }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseFunctionDeclaration_MultipleReturns_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function abs(x) {
                if (x < 0) {
                    return -x;
                }
                return x;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Complex Function Scenarios

    [Fact]
    public void ParseProgram_MultipleFunctions_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function add(a, b) { return a + b; }
            function sub(a, b) { return a - b; }
            function mul(a, b) { return a * b; }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_FunctionWithLocalVariables_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function compute(x) {
                var a = x + 1;
                var b = a * 2;
                var c = b - 3;
                return c;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_FunctionWithControlFlow_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function process(items) {
                var result = 0;
                for (var i = 0; i < items; i = i + 1) {
                    if (i % 2 === 0) {
                        result = result + i;
                    } else {
                        result = result - i;
                    }
                }
                return result;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_ClosurePattern_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function createCounter() {
                var count = 0;
                return function() {
                    count = count + 1;
                    return count;
                };
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_RecursiveFunction_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function factorial(n) {
                if (n <= 1) {
                    return 1;
                }
                return n * factorial(n - 1);
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_MutualRecursion_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function isEven(n) {
                if (n === 0) return true;
                return isOdd(n - 1);
            }
            function isOdd(n) {
                if (n === 0) return false;
                return isEven(n - 1);
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseProgram_HigherOrderFunction_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function map(arr, fn) {
                var result = [];
                for (var i = 0; i < arr; i = i + 1) {
                    result = fn(arr);
                }
                return result;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Use Strict Directive

    [Fact]
    public void ParseFunctionDeclaration_UseStrict_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            function strict() {
                'use strict';
                return this;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Function In Expressions

    [Fact]
    public void ParseExpression_FunctionInTernary_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var f = true ? function() { } : function() { };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExpression_FunctionInArray_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var arr = [function() { }, function() { }];", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExpression_FunctionInObject_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var obj = { method: function() { } };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion
}
