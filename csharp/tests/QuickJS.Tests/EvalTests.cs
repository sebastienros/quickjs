// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for eval() and Function constructor (Step 9.1).
/// </summary>
public class EvalTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public EvalTests()
    {
        _context = _runtime.CreateContext();
    }

    #region Global eval() Function Tests

    [Fact]
    public void GlobalEval_Exists_OnGlobalObject()
    {
        Assert.True(_context.HasGlobalProperty("eval"));
        var evalVal = _context.GetGlobalProperty("eval");
        Assert.True(evalVal.IsObject);
        Assert.IsType<JSFunction>(evalVal.AsObject());
    }

    [Fact]
    public void GlobalEval_HasCorrectName()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        Assert.Equal("eval", evalFn.Name);
    }

    [Fact]
    public void GlobalEval_HasCorrectLength()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        Assert.Equal(1, evalFn.Length);
    }

    [Fact]
    public void GlobalEval_NoArguments_ReturnsUndefined()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        var result = evalFn.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void GlobalEval_NonStringArgument_ReturnsArgumentUnchanged()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        
        // Numbers
        var numResult = evalFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
        Assert.Equal(42, numResult.ToInt32());
        
        // Objects
        var obj = new JSObject();
        var objResult = evalFn.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) });
        Assert.Same(obj, objResult.AsObject());
        
        // Booleans
        var boolResult = evalFn.CallNative(JSValue.Undefined, new[] { JSValue.True });
        Assert.True(boolResult.IsTrue);
    }

    [Fact]
    public void GlobalEval_NullArgument_ReturnsNull()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        var result = evalFn.CallNative(JSValue.Undefined, new[] { JSValue.Null });
        Assert.True(result.IsNull);
    }

    [Fact]
    public void GlobalEval_UndefinedArgument_ReturnsUndefined()
    {
        var evalFn = (JSFunction)_context.GetGlobalProperty("eval").AsObject();
        var result = evalFn.CallNative(JSValue.Undefined, new[] { JSValue.Undefined });
        Assert.True(result.IsUndefined);
    }

    #endregion

    #region Function Constructor Tests

    [Fact]
    public void FunctionConstructor_Exists_OnGlobalObject()
    {
        Assert.True(_context.HasGlobalProperty("Function"));
        var funcVal = _context.GetGlobalProperty("Function");
        Assert.True(funcVal.IsObject);
        Assert.IsType<JSFunction>(funcVal.AsObject());
    }

    [Fact]
    public void FunctionConstructor_HasCorrectName()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        Assert.Equal("Function", FunctionCtor.Name);
    }

    [Fact]
    public void FunctionConstructor_HasPrototype()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        var proto = FunctionCtor.Get("prototype");
        Assert.True(proto.IsObject);
    }

    [Fact]
    public void FunctionConstructor_PrototypeHasConstructor()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        var proto = FunctionCtor.Get("prototype").AsObject();
        var constructor = proto.Get("constructor");
        Assert.True(constructor.IsObject);
        Assert.Same(FunctionCtor, constructor.AsObject());
    }

    [Fact]
    public void FunctionConstructor_InvalidParamName_ThrowsSyntaxError()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        FunctionCtor.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString("123invalid"),
            JSValue.FromString("return 0")
        });
        
        Assert.True(_context.HasException);
    }

    [Fact]
    public void FunctionConstructor_ReservedWordParam_ThrowsSyntaxError()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        FunctionCtor.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString("return"),
            JSValue.FromString("return 0")
        });
        
        Assert.True(_context.HasException);
    }

    [Fact]
    public void FunctionConstructor_EmptyParamString_IsAllowed()
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        // Empty string should be treated as no parameters
        var result = FunctionCtor.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString(""),
            JSValue.FromString("return 42")
        });
        
        Assert.False(_context.HasException);
        Assert.True(result.IsObject);
    }

    #endregion

    #region JSEval Static Methods Tests

    [Fact]
    public void JSEval_Compile_ReturnsFunction()
    {
        var fn = JSEval.Compile(_runtime, "1 + 2");
        Assert.NotNull(fn);
    }

    [Fact]
    public void JSEval_Compile_EmptySource_ReturnsFunction()
    {
        var fn = JSEval.Compile(_runtime, "");
        Assert.NotNull(fn);
    }

    [Fact]
    public void JSEval_Compile_NullSource_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => JSEval.Compile(_runtime, null!));
    }

    [Fact]
    public void JSEval_Compile_NullRuntime_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => JSEval.Compile((JSRuntime)null!, "1 + 2"));
    }

    [Fact]
    public void JSEval_Evaluate_NullSource_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => JSEval.Evaluate(_context, null!));
    }

    [Fact]
    public void JSEval_Evaluate_NullContext_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => JSEval.Evaluate(null!, "1 + 2"));
    }

    [Fact]
    public void JSEval_Evaluate_EmptySource_ReturnsUndefined()
    {
        var result = JSEval.Evaluate(_context, "");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void JSEval_Evaluate_WhitespaceOnly_ReturnsUndefined()
    {
        var result = JSEval.Evaluate(_context, "   \n   ");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void JSEval_CreateEvalFunction_Works()
    {
        var evalFn = JSEval.CreateEvalFunction(_context);
        Assert.NotNull(evalFn);
        Assert.Equal("eval", evalFn.Name);
        Assert.Equal(1, evalFn.Length);
    }

    [Fact]
    public void JSEval_CreateFunctionConstructor_Works()
    {
        var fnCtor = JSEval.CreateFunctionConstructor(_context);
        Assert.NotNull(fnCtor);
        Assert.Equal("Function", fnCtor.Name);
    }

    #endregion

    #region Context Methods Tests

    [Fact]
    public void Context_Evaluate_EmptyString_ReturnsUndefined()
    {
        var result = _context.Evaluate("");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void Context_Evaluate_Whitespace_ReturnsUndefined()
    {
        var result = _context.Evaluate("   \n   ");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void Context_EvaluateModule_EmptyString_ReturnsUndefined()
    {
        var result = _context.EvaluateModule("");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void Context_Compile_ReturnsFunction()
    {
        var result = _context.Compile("1 + 2");
        Assert.True(result.IsObject);
        Assert.IsType<JSFunction>(result.AsObject());
    }

    #endregion

    #region EvalFlags Tests

    [Fact]
    public void EvalFlags_Global_IsDefault()
    {
        Assert.Equal(JSEval.EvalFlags.Global, (JSEval.EvalFlags)0);
    }

    [Fact]
    public void EvalFlags_Module_IsNonZero()
    {
        Assert.NotEqual(0, (int)JSEval.EvalFlags.Module);
    }

    [Fact]
    public void EvalFlags_CompileOnly_CanBeCombined()
    {
        var flags = JSEval.EvalFlags.Global | JSEval.EvalFlags.CompileOnly;
        Assert.True((flags & JSEval.EvalFlags.CompileOnly) != 0);
    }

    #endregion

    #region IsValidIdentifier Tests (via reserved words)

    [Theory]
    [InlineData("break")]
    [InlineData("case")]
    [InlineData("catch")]
    [InlineData("continue")]
    [InlineData("debugger")]
    [InlineData("default")]
    [InlineData("delete")]
    [InlineData("do")]
    [InlineData("else")]
    [InlineData("finally")]
    [InlineData("for")]
    [InlineData("function")]
    [InlineData("if")]
    [InlineData("in")]
    [InlineData("instanceof")]
    [InlineData("new")]
    [InlineData("return")]
    [InlineData("switch")]
    [InlineData("this")]
    [InlineData("throw")]
    [InlineData("try")]
    [InlineData("typeof")]
    [InlineData("var")]
    [InlineData("void")]
    [InlineData("while")]
    [InlineData("with")]
    [InlineData("class")]
    [InlineData("const")]
    [InlineData("enum")]
    [InlineData("export")]
    [InlineData("extends")]
    [InlineData("import")]
    [InlineData("super")]
    [InlineData("implements")]
    [InlineData("interface")]
    [InlineData("let")]
    [InlineData("package")]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("public")]
    [InlineData("static")]
    [InlineData("yield")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    public void FunctionConstructor_ReservedWordAsParam_ThrowsSyntaxError(string reservedWord)
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        FunctionCtor.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString(reservedWord),
            JSValue.FromString("return 0")
        });
        
        Assert.True(_context.HasException);
        _context.ClearException();
    }

    [Theory]
    [InlineData("123abc")]
    [InlineData("-name")]
    [InlineData("na me")]
    [InlineData("na.me")]
    public void FunctionConstructor_InvalidIdentifier_ThrowsSyntaxError(string invalidName)
    {
        var FunctionCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        FunctionCtor.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString(invalidName),
            JSValue.FromString("return 0")
        });
        
        Assert.True(_context.HasException);
        _context.ClearException();
    }

    #endregion
}

