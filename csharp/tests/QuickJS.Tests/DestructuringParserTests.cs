// Copyright (c) 2025 The QuickJS.NET Authors
// Licensed under the MIT License

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for destructuring pattern parsing in variable declarations, 
/// function parameters, and assignment expressions.
/// </summary>
public class DestructuringParserTests
{
    #region Array Destructuring in Variable Declarations

    [Fact]
    public void ArrayDestructuring_BasicTwoElements()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_ThreeElements()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let [x, y, z] = [1, 2, 3];", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_WithElision()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, , b] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_WithElisionAtStart()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [, , a] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_WithRest()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [first, ...rest] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_OnlyRest()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [...all] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_WithDefaultValue()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a = 1, b = 2] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_MixedDefaultAndNoDefault()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b = 5, c] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_Nested()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, [b, c]] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_NestedMultipleLevels()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, [b, [c, d]]] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrayDestructuring_TrailingComma()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b,] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Object Destructuring in Variable Declarations

    [Fact]
    public void ObjectDestructuring_BasicTwoProperties()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_ThreeProperties()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let { x, y, z } = { x: 1, y: 2, z: 3 };", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_WithRenaming()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { prop: newName } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_MultipleRenamings()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a: x, b: y, c: z } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_MixedShorthandAndRenaming()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b: newB, c } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_WithDefaultValue()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a = 1, b = 2 } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_RenamingWithDefault()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { prop: newName = 42 } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_WithRest()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b, ...rest } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_OnlyRest()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { ...all } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_Nested()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b: { c, d } } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_DeeplyNested()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a: { b: { c } } } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ObjectDestructuring_TrailingComma()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b, } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Mixed Array and Object Destructuring

    [Fact]
    public void MixedDestructuring_ObjectInsideArray()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, { b, c }] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void MixedDestructuring_ArrayInsideObject()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b: [c, d] } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void MixedDestructuring_Complex()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b: [c, { d, e }] } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Destructuring in Function Parameters

    [Fact]
    public void FunctionParameter_ArrayDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo([a, b]) { return a + b; }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void FunctionParameter_ObjectDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo({ a, b }) { return a + b; }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void FunctionParameter_DestructuringWithDefault()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo({ a = 1, b = 2 }) { return a + b; }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void FunctionParameter_ArrayWithRest()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo([first, ...rest]) { return first; }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void FunctionParameter_MixedWithNormalParams()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo(x, { a, b }, [c, d]) { return x + a + c; }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrowFunction_ArrayDestructuringParameter()
    {
        var atoms = new AtomTable();
        var parser = new Parser("([a, b]) => a + b", "test.js", atoms);
        parser.ParseExpression();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ArrowFunction_ObjectDestructuringParameter()
    {
        var atoms = new AtomTable();
        var parser = new Parser("({ a, b }) => a + b", "test.js", atoms);
        parser.ParseExpression();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Destructuring with var/let/const

    [Fact]
    public void VarDeclaration_ArrayDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var [a, b] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void LetDeclaration_ArrayDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let [a, b] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ConstDeclaration_ArrayDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b] = arr;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void VarDeclaration_ObjectDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var { a, b } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void LetDeclaration_ObjectDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("let { a, b } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ConstDeclaration_ObjectDestructuring()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { a, b } = obj;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Error_DestructuringWithoutInitializer()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b];", "test.js", atoms);
        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void Error_RestNotLast_Array()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [...rest, a] = arr;", "test.js", atoms);
        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void Error_RestNotLast_Object()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { ...rest, a } = obj;", "test.js", atoms);
        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void NestedRestInArray_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, [...rest]] = arr;", "test.js", atoms);
        // This should parse successfully - nested rest is allowed
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Complex Real-World Patterns

    [Fact]
    public void RealWorld_ReactHookPattern()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [state, setState] = useState(0);", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void RealWorld_FunctionOptions()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { timeout = 5000, retries = 3, debug = false } = options;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void RealWorld_NestedObjectConfig()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const { server: { host, port = 8080 }, db: { url } } = config;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void RealWorld_MultipleDeclarations()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [a, b] = arr1, { c, d } = obj1;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void RealWorld_AsyncFunction()
    {
        var atoms = new AtomTable();
        var parser = new Parser("async function fetch({ url, method = 'GET' }) { }", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void RealWorld_PromiseAll()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const [result1, result2] = results;", "test.js", atoms);
        parser.ParseStatement();
        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion
}
