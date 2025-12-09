// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSContext class.
/// </summary>
public class JSContextTests
{
    #region Constructor Tests

    [Fact]
    public void Context_CreatedWithRuntime()
    {
        using var runtime = new JSRuntime();

        using var context = runtime.CreateContext();

        Assert.NotNull(context);
        Assert.Same(runtime, context.Runtime);
        Assert.False(context.IsDisposed);
    }

    [Fact]
    public void Context_HasGlobalObject()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.NotNull(context.GlobalObject);
        Assert.True(context.GlobalObject.IsExtensible);
    }

    [Fact]
    public void Context_HasGlobalVarObject()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.NotNull(context.GlobalVarObject);
    }

    #endregion

    #region Global Object Tests

    [Fact]
    public void SetGlobalProperty_SetsProperty()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var result = context.SetGlobalProperty("testVar", JSValue.FromInt32(42));

        Assert.True(result);
        Assert.Equal(42, context.GetGlobalProperty("testVar").ToInt32());
    }

    [Fact]
    public void GetGlobalProperty_ReturnsUndefinedForMissing()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var value = context.GetGlobalProperty("nonExistent");

        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void HasGlobalProperty_ReturnsTrueWhenExists()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.SetGlobalProperty("exists", JSValue.FromInt32(1));

        Assert.True(context.HasGlobalProperty("exists"));
        Assert.False(context.HasGlobalProperty("notExists"));
    }

    [Fact]
    public void DeleteGlobalProperty_RemovesProperty()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.SetGlobalProperty("toDelete", JSValue.FromInt32(1));

        var result = context.DeleteGlobalProperty("toDelete");

        Assert.True(result);
        Assert.False(context.HasGlobalProperty("toDelete"));
    }

    [Fact]
    public void DefineGlobalProperty_DefinesWithDescriptor()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var descriptor = PropertyDescriptor.Data(
            JSValue.FromString("constant"),
            writable: false,
            enumerable: true,
            configurable: false
        );

        var result = context.DefineGlobalProperty("CONST", descriptor);

        Assert.True(result);
        Assert.Equal("constant", context.GetGlobalProperty("CONST").ToString());
    }

    #endregion

    #region Global Function Registration Tests

    [Fact]
    public void RegisterGlobalFunction_RegistersFunction()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.RegisterGlobalFunction("myFunc", (thisArg, args) => JSValue.FromInt32(123), 0);

        var value = context.GetGlobalProperty("myFunc");
        Assert.True(value.IsObject);
        Assert.IsType<JSFunction>(value.AsObject());
    }

    [Fact]
    public void RegisterGlobalFunction_FunctionCanBeCalled()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.RegisterGlobalFunction("add", (thisArg, args) =>
        {
            int a = args.Length > 0 ? args[0].ToInt32() : 0;
            int b = args.Length > 1 ? args[1].ToInt32() : 0;
            return JSValue.FromInt32(a + b);
        }, 2);

        var func = (JSFunction)context.GetGlobalProperty("add").AsObject();
        var result = func.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(3), JSValue.FromInt32(4) });

        Assert.Equal(7, result.ToInt32());
    }

    [Fact]
    public void RegisterGlobalFunction_WithMagic()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.RegisterGlobalFunction("magicFunc", (thisArg, args, magic) =>
            JSValue.FromInt32(magic * 10), 0, 42);

        var func = (JSFunction)context.GetGlobalProperty("magicFunc").AsObject();
        var result = func.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Equal(420, result.ToInt32());
    }

    #endregion

    #region Class Prototype Tests

    [Fact]
    public void GetClassPrototype_ReturnsSetPrototype()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        // Object prototype should be set during initialization
        var objectProto = context.GetClassPrototype(JSClassId.Object);

        Assert.NotNull(objectProto);
    }

    [Fact]
    public void SetClassPrototype_SetsPrototype()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var customProto = new JSObject();

        context.SetClassPrototype(JSClassId.Number, customProto);

        Assert.Same(customProto, context.GetClassPrototype(JSClassId.Number));
    }

    [Fact]
    public void ClassPrototypes_ArrayInheritsFromObject()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var arrayProto = context.GetClassPrototype(JSClassId.Array);
        var objectProto = context.GetClassPrototype(JSClassId.Object);

        Assert.NotNull(arrayProto);
        Assert.NotNull(objectProto);
        Assert.Same(objectProto, arrayProto.Prototype);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void HasException_InitiallyFalse()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.False(context.HasException);
    }

    [Fact]
    public void SetException_SetsHasException()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.SetException(JSValue.FromString("error"));

        Assert.True(context.HasException);
    }

    [Fact]
    public void GetAndClearException_ReturnsAndClears()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.SetException(JSValue.FromString("test error"));

        var exception = context.GetAndClearException();

        Assert.Equal("test error", exception.ToString());
        Assert.False(context.HasException);
    }

    [Fact]
    public void ClearException_ClearsException()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.SetException(JSValue.FromString("error"));

        context.ClearException();

        Assert.False(context.HasException);
    }

    [Fact]
    public void ThrowError_SetsExceptionAndReturnsExceptionValue()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var result = context.ThrowError(JSErrorType.TypeError, "Invalid type");

        Assert.True(result.IsException);
        Assert.True(context.HasException);

        var exception = context.GetAndClearException();
        Assert.True(exception.IsObject);
        var errorObj = exception.AsObject();
        Assert.Equal("TypeError", errorObj.Get("name").ToString());
        Assert.Equal("Invalid type", errorObj.Get("message").ToString());
    }

    [Fact]
    public void ThrowTypeError_ThrowsTypeError()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ThrowTypeError("type error message");

        var exception = context.GetAndClearException();
        var errorObj = exception.AsObject();
        Assert.Equal("TypeError", errorObj.Get("name").ToString());
    }

    [Fact]
    public void ThrowReferenceError_ThrowsReferenceError()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ThrowReferenceError("x is not defined");

        var exception = context.GetAndClearException();
        var errorObj = exception.AsObject();
        Assert.Equal("ReferenceError", errorObj.Get("name").ToString());
    }

    [Fact]
    public void ThrowSyntaxError_ThrowsSyntaxError()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ThrowSyntaxError("Unexpected token");

        var exception = context.GetAndClearException();
        var errorObj = exception.AsObject();
        Assert.Equal("SyntaxError", errorObj.Get("name").ToString());
    }

    [Fact]
    public void ThrowRangeError_ThrowsRangeError()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ThrowRangeError("Invalid array length");

        var exception = context.GetAndClearException();
        var errorObj = exception.AsObject();
        Assert.Equal("RangeError", errorObj.Get("name").ToString());
    }

    #endregion

    #region Random Tests

    [Fact]
    public void GetRandomValue_ReturnsValueBetween0And1()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        for (int i = 0; i < 100; i++)
        {
            var value = context.GetRandomValue();
            Assert.True(value >= 0.0);
            Assert.True(value < 1.0);
        }
    }

    [Fact]
    public void GetRandomValue_ProducesVaryingValues()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var values = new HashSet<double>();
        for (int i = 0; i < 10; i++)
        {
            values.Add(context.GetRandomValue());
        }

        // Should produce multiple unique values
        Assert.True(values.Count > 5);
    }

    [Fact]
    public void SetRandomSeed_ProducesReproducibleSequence()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.SetRandomSeed(12345);
        var seq1 = new double[5];
        for (int i = 0; i < 5; i++)
            seq1[i] = context.GetRandomValue();

        context.SetRandomSeed(12345);
        var seq2 = new double[5];
        for (int i = 0; i < 5; i++)
            seq2[i] = context.GetRandomValue();

        Assert.Equal(seq1, seq2);
    }

    #endregion

    #region Module Management Tests

    [Fact]
    public void LoadedModuleCount_InitiallyZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Equal(0, context.LoadedModuleCount);
    }

    [Fact]
    public void RegisterModule_AddsModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var module = new JSModuleDef("./myModule.js");

        context.RegisterModule("./myModule.js", module);

        Assert.Equal(1, context.LoadedModuleCount);
        Assert.True(context.HasModule("./myModule.js"));
    }

    [Fact]
    public void GetModule_ReturnsRegisteredModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var module = new JSModuleDef("test-module");
        context.RegisterModule("test-module", module);

        var retrieved = context.GetModule("test-module");

        Assert.Same(module, retrieved);
    }

    [Fact]
    public void GetModule_ReturnsNullForUnknown()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var module = context.GetModule("unknown-module");

        Assert.Null(module);
    }

    [Fact]
    public void GetLoadedModuleNames_ReturnsAllNames()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.RegisterModule("module1", new JSModuleDef("module1"));
        context.RegisterModule("module2", new JSModuleDef("module2"));

        var names = context.GetLoadedModuleNames().ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("module1", names);
        Assert.Contains("module2", names);
    }

    #endregion

    #region Stack Frame Tests

    [Fact]
    public void GetStackDepth_InitiallyZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Equal(0, context.GetStackDepth());
    }

    [Fact]
    public void PushAndPopCallFrame_ManagesStack()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var frame1 = new JSCallFrame(null, "func1", "test.js", Array.Empty<JSValue>());
        context.PushCallFrame(frame1);
        Assert.Equal(1, context.GetStackDepth());

        var frame2 = new JSCallFrame(null, "func2", "test.js", Array.Empty<JSValue>());
        context.PushCallFrame(frame2);
        Assert.Equal(2, context.GetStackDepth());

        var popped = context.PopCallFrame();
        Assert.Same(frame2, popped);
        Assert.Equal(1, context.GetStackDepth());

        context.PopCallFrame();
        Assert.Equal(0, context.GetStackDepth());
    }

    [Fact]
    public void GetStackTrace_ReturnsStackTrace()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var frame1 = new JSCallFrame(null, "outer", "main.js", Array.Empty<JSValue>())
        {
            LineNumber = 10,
            ColumnNumber = 5
        };
        context.PushCallFrame(frame1);

        var frame2 = new JSCallFrame(null, "inner", "helper.js", Array.Empty<JSValue>())
        {
            LineNumber = 20,
            ColumnNumber = 15
        };
        context.PushCallFrame(frame2);

        var stackTrace = context.GetStackTrace();

        Assert.Equal(2, stackTrace.Count);
        Assert.Equal("inner", stackTrace[0].FunctionName);
        Assert.Equal("outer", stackTrace[1].FunctionName);
    }

    #endregion

    #region Atom Convenience Tests

    [Fact]
    public void InternAtom_ReturnsAtom()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var atom = context.InternAtom("test");

        Assert.False(atom.IsEmpty);
    }

    [Fact]
    public void GetAtomString_ReturnsString()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var atom = context.InternAtom("myString");

        var str = context.GetAtomString(atom);

        Assert.Equal("myString", str);
    }

    #endregion

    #region Strict Mode Tests

    [Fact]
    public void StrictMode_DefaultIsFalse()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.False(context.StrictMode);
    }

    [Fact]
    public void StrictMode_CanBeSet()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.StrictMode = true;

        Assert.True(context.StrictMode);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_SetsIsDisposed()
    {
        var runtime = new JSRuntime();
        var context = runtime.CreateContext();

        context.Dispose();

        Assert.True(context.IsDisposed);
    }

    [Fact]
    public void Dispose_RemovesFromRuntime()
    {
        using var runtime = new JSRuntime();
        var context = runtime.CreateContext();
        Assert.Equal(1, runtime.ContextCount);

        context.Dispose();

        Assert.Equal(0, runtime.ContextCount);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        using var runtime = new JSRuntime();
        var context = runtime.CreateContext();

        context.Dispose();
        context.Dispose(); // Should not throw

        Assert.True(context.IsDisposed);
    }

    [Fact]
    public void Methods_ThrowWhenDisposed()
    {
        using var runtime = new JSRuntime();
        var context = runtime.CreateContext();
        context.Dispose();

        Assert.Throws<ObjectDisposedException>(() => context.SetGlobalProperty("x", JSValue.FromInt32(1)));
        Assert.Throws<ObjectDisposedException>(() => context.GetGlobalProperty("x"));
        Assert.Throws<ObjectDisposedException>(() => context.RegisterGlobalFunction("f", (_, _) => JSValue.Undefined));
    }

    #endregion

    #region UserOpaque Tests

    [Fact]
    public void UserOpaque_InitiallyNull()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Null(context.UserOpaque);
    }

    [Fact]
    public void UserOpaque_CanBeSetAndRetrieved()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        var data = new object();

        context.UserOpaque = data;

        Assert.Same(data, context.UserOpaque);
    }

    #endregion

    #region Multiple Context Isolation Tests

    [Fact]
    public void MultipleContexts_HaveIsolatedGlobals()
    {
        using var runtime = new JSRuntime();
        using var ctx1 = runtime.CreateContext();
        using var ctx2 = runtime.CreateContext();

        ctx1.SetGlobalProperty("x", JSValue.FromInt32(1));
        ctx2.SetGlobalProperty("x", JSValue.FromInt32(2));

        Assert.Equal(1, ctx1.GetGlobalProperty("x").ToInt32());
        Assert.Equal(2, ctx2.GetGlobalProperty("x").ToInt32());
    }

    [Fact]
    public void MultipleContexts_ShareRuntime()
    {
        using var runtime = new JSRuntime();
        using var ctx1 = runtime.CreateContext();
        using var ctx2 = runtime.CreateContext();

        Assert.Same(runtime, ctx1.Runtime);
        Assert.Same(runtime, ctx2.Runtime);
    }

    [Fact]
    public void MultipleContexts_ShareAtoms()
    {
        using var runtime = new JSRuntime();
        using var ctx1 = runtime.CreateContext();
        using var ctx2 = runtime.CreateContext();

        var atom1 = ctx1.InternAtom("shared");
        var atom2 = ctx2.InternAtom("shared");

        Assert.Equal(atom1, atom2);
    }

    #endregion
}
