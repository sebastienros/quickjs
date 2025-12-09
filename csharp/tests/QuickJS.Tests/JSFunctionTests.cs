// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSFunction"/> - JavaScript function objects.
/// </summary>
public class JSFunctionTests
{
    #region Bytecode Function Tests

    [Fact]
    public void BytecodeFunction_Constructor_SetsProperties()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) }; // Use uint atom index
        funcDef.AddArg(new JSAtom(2));
        funcDef.AddArg(new JSAtom(3));

        var func = new JSFunction(funcDef);

        Assert.NotNull(func.Name);
        Assert.Equal(2, func.Length);
        Assert.True(func.IsBytecodeFunction);
        Assert.False(func.IsNativeFunction);
        Assert.False(func.IsBoundFunction);
        Assert.Equal(JSClassId.BytecodeFunction, func.ClassId);
    }

    [Fact]
    public void BytecodeFunction_FunctionDef_IsAccessible()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        funcDef.IsStrict = true;

        var func = new JSFunction(funcDef);

        Assert.Same(funcDef, func.FunctionDef);
        Assert.True(func.IsStrict);
    }

    [Fact]
    public void BytecodeFunction_AnonymousFunction_HasNullName()
    {
        var funcDef = new JSFunctionDef(); // No name

        var func = new JSFunction(funcDef);

        Assert.Null(func.Name);
    }

    [Fact]
    public void BytecodeFunction_ArrowFunction_HasCorrectProperties()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        funcDef.FuncType = JSParseFunctionType.Arrow;
        funcDef.HasThisBinding = false;
        funcDef.HasPrototype = false;

        var func = new JSFunction(funcDef);

        Assert.True(func.IsArrowFunction);
        Assert.False(func.HasThisBinding);
        Assert.False(func.HasPrototypeProperty);
    }

    [Fact]
    public void BytecodeFunction_Generator_HasCorrectKind()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        funcDef.FuncKind = JSFunctionKind.Generator;

        var func = new JSFunction(funcDef);

        Assert.True(func.IsGenerator);
        Assert.False(func.IsAsync);
    }

    [Fact]
    public void BytecodeFunction_AsyncFunction_HasCorrectKind()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        funcDef.FuncKind = JSFunctionKind.Async;

        var func = new JSFunction(funcDef);

        Assert.True(func.IsAsync);
        Assert.False(func.IsGenerator);
    }

    [Fact]
    public void BytecodeFunction_AsyncGenerator_HasBothFlags()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        funcDef.FuncKind = JSFunctionKind.AsyncGenerator;

        var func = new JSFunction(funcDef);

        Assert.True(func.IsAsync);
        Assert.True(func.IsGenerator);
    }

    [Fact]
    public void BytecodeFunction_WithVarRefs_StoresClosures()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
        var varRefs = new JSVarRef[]
        {
            new JSVarRef(JSValue.FromInt32(10)),
            new JSVarRef(JSValue.FromString("hello")),
        };

        var func = new JSFunction(funcDef, varRefs);

        Assert.NotNull(func.VarRefs);
        Assert.Equal(2, func.VarRefs!.Length);
        Assert.Equal(10, func.VarRefs[0].Value.ToInt32());
        Assert.Equal("hello", func.VarRefs[1].Value.ToString());
    }

    [Fact]
    public void BytecodeFunction_WithPrototype_SetsPrototypeChain()
    {
        var funcDef = new JSFunctionDef();
        var funcProto = new JSObject();

        var func = new JSFunction(funcDef, null, funcProto);

        Assert.Same(funcProto, func.Prototype);
    }

    #endregion

    #region Native Function Tests

    [Fact]
    public void NativeFunction_Constructor_SetsProperties()
    {
        JSCFunction impl = (thisArg, args) => JSValue.FromInt32(42);

        var func = new JSFunction(impl, "nativeFunc", 2);

        Assert.Equal("nativeFunc", func.Name);
        Assert.Equal(2, func.Length);
        Assert.True(func.IsNativeFunction);
        Assert.False(func.IsBytecodeFunction);
        Assert.False(func.IsBoundFunction);
        Assert.Equal(JSClassId.CFunction, func.ClassId);
    }

    [Fact]
    public void NativeFunction_CallNative_InvokesDelegate()
    {
        JSCFunction impl = (thisArg, args) =>
        {
            int sum = 0;
            foreach (var arg in args)
            {
                sum += arg.ToInt32();
            }
            return JSValue.FromInt32(sum);
        };

        var func = new JSFunction(impl, "sum", 2);
        var result = func.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromInt32(10),
            JSValue.FromInt32(20),
        });

        Assert.Equal(30, result.ToInt32());
    }

    [Fact]
    public void NativeFunction_CallNative_PassesThisArg()
    {
        JSValue? capturedThis = null;
        JSCFunction impl = (thisArg, args) =>
        {
            capturedThis = thisArg;
            return JSValue.Undefined;
        };

        var func = new JSFunction(impl);
        var thisObj = JSValue.FromInt32(123);
        func.CallNative(thisObj, System.Array.Empty<JSValue>());

        Assert.NotNull(capturedThis);
        Assert.Equal(123, capturedThis!.Value.ToInt32());
    }

    [Fact]
    public void NativeFunctionMagic_Constructor_SetsMagic()
    {
        JSCFunctionMagic impl = (thisArg, args, magic) => JSValue.FromInt32(magic);

        var func = new JSFunction(impl, 42, "magicFunc", 0);

        Assert.Equal(42, func.Magic);
        Assert.True(func.IsNativeFunction);
    }

    [Fact]
    public void NativeFunctionMagic_CallNative_PassesMagic()
    {
        JSCFunctionMagic impl = (thisArg, args, magic) => JSValue.FromInt32(magic * 2);

        var func = new JSFunction(impl, 21, "magicFunc", 0);
        var result = func.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());

        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void NativeFunction_AnonymousFunction_HasNullName()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;

        var func = new JSFunction(impl);

        Assert.Null(func.Name);
        Assert.Equal(0, func.Length);
    }

    #endregion

    #region Bound Function Tests

    [Fact]
    public void Bind_CreatesNewBoundFunction()
    {
        JSCFunction impl = (thisArg, args) => thisArg;

        var func = new JSFunction(impl, "original", 0);
        var boundThis = JSValue.FromInt32(42);
        var bound = func.Bind(boundThis);

        Assert.True(bound.IsBoundFunction);
        Assert.Equal(JSClassId.BoundFunction, bound.ClassId);
        Assert.Same(func, bound.BoundTarget);
        Assert.Equal(42, bound.BoundThis.ToInt32());
    }

    [Fact]
    public void Bind_SetsNameWithBoundPrefix()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;

        var func = new JSFunction(impl, "myFunc", 0);
        var bound = func.Bind(JSValue.Null);

        Assert.Equal("bound myFunc", bound.Name);
    }

    [Fact]
    public void Bind_WithBoundArgs_PrependsArguments()
    {
        JSCFunction impl = (thisArg, args) =>
        {
            string result = "";
            foreach (var arg in args)
            {
                result += arg.ToInt32() + ",";
            }
            return JSValue.FromString(result);
        };

        var func = new JSFunction(impl, "test", 3);
        var bound = func.Bind(JSValue.Null, JSValue.FromInt32(1), JSValue.FromInt32(2));

        var result = bound.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(3) });

        Assert.Equal("1,2,3,", result.ToString());
    }

    [Fact]
    public void Bind_AdjustsLengthProperty()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;

        var func = new JSFunction(impl, "test", 5);
        var bound = func.Bind(JSValue.Null, JSValue.FromInt32(1), JSValue.FromInt32(2));

        // Length should be original (5) minus bound args (2) = 3
        Assert.Equal(3, bound.Length);
    }

    [Fact]
    public void Bind_LengthDoesNotGoBelowZero()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;

        var func = new JSFunction(impl, "test", 2);
        var bound = func.Bind(JSValue.Null,
            JSValue.FromInt32(1),
            JSValue.FromInt32(2),
            JSValue.FromInt32(3));

        // Length cannot go below 0
        Assert.Equal(0, bound.Length);
    }

    [Fact]
    public void Bind_CallNative_UsesBoundThis()
    {
        JSValue? capturedThis = null;
        JSCFunction impl = (thisArg, args) =>
        {
            capturedThis = thisArg;
            return JSValue.Undefined;
        };

        var func = new JSFunction(impl);
        var boundThis = JSValue.FromInt32(999);
        var bound = func.Bind(boundThis);

        // Even if we pass a different this, the bound this should be used
        bound.CallNative(JSValue.FromInt32(111), System.Array.Empty<JSValue>());

        Assert.Equal(999, capturedThis!.Value.ToInt32());
    }

    [Fact]
    public void Bind_DoubleBinding_UsesPreviousBoundThis()
    {
        JSValue? capturedThis = null;
        JSCFunction impl = (thisArg, args) =>
        {
            capturedThis = thisArg;
            return JSValue.Undefined;
        };

        var func = new JSFunction(impl);
        var bound1 = func.Bind(JSValue.FromInt32(1));
        var bound2 = bound1.Bind(JSValue.FromInt32(2)); // This should be ignored

        bound2.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());

        // First bound this should win
        Assert.Equal(1, capturedThis!.Value.ToInt32());
    }

    [Fact]
    public void Bind_DoubleBinding_CombinesBoundArgs()
    {
        JSCFunction impl = (thisArg, args) =>
        {
            string result = "";
            foreach (var arg in args)
            {
                result += arg.ToInt32() + ",";
            }
            return JSValue.FromString(result);
        };

        var func = new JSFunction(impl, "test", 4);
        var bound1 = func.Bind(JSValue.Null, JSValue.FromInt32(1));
        var bound2 = bound1.Bind(JSValue.Null, JSValue.FromInt32(2));

        var result = bound2.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(3) });

        // Should be 1, 2, 3 (combined bound args plus call args)
        Assert.Equal("1,2,3,", result.ToString());
    }

    #endregion

    #region Closure Support Tests

    [Fact]
    public void GetVarRef_ReturnsCorrectVarRef()
    {
        var funcDef = new JSFunctionDef();
        var varRefs = new JSVarRef[]
        {
            new JSVarRef(JSValue.FromInt32(10)),
            new JSVarRef(JSValue.FromInt32(20)),
        };

        var func = new JSFunction(funcDef, varRefs);

        Assert.Same(varRefs[0], func.GetVarRef(0));
        Assert.Same(varRefs[1], func.GetVarRef(1));
    }

    [Fact]
    public void GetVarRef_ReturnsNullForInvalidIndex()
    {
        var funcDef = new JSFunctionDef();
        var varRefs = new JSVarRef[] { new JSVarRef(JSValue.FromInt32(10)) };

        var func = new JSFunction(funcDef, varRefs);

        Assert.Null(func.GetVarRef(-1));
        Assert.Null(func.GetVarRef(5));
    }

    [Fact]
    public void GetVarRef_ReturnsNullWhenNoVarRefs()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);

        Assert.Null(func.GetVarRef(0));
    }

    [Fact]
    public void GetClosureValue_ReturnsValue()
    {
        var funcDef = new JSFunctionDef();
        var varRefs = new JSVarRef[]
        {
            new JSVarRef(JSValue.FromInt32(42)),
        };

        var func = new JSFunction(funcDef, varRefs);

        Assert.Equal(42, func.GetClosureValue(0).ToInt32());
    }

    [Fact]
    public void GetClosureValue_ReturnsUndefinedForInvalidIndex()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);

        Assert.True(func.GetClosureValue(0).IsUndefined);
    }

    [Fact]
    public void SetClosureValue_SetsValue()
    {
        var funcDef = new JSFunctionDef();
        var varRefs = new JSVarRef[]
        {
            new JSVarRef(JSValue.FromInt32(10)),
        };

        var func = new JSFunction(funcDef, varRefs);

        bool result = func.SetClosureValue(0, JSValue.FromInt32(20));

        Assert.True(result);
        Assert.Equal(20, func.GetClosureValue(0).ToInt32());
    }

    [Fact]
    public void SetClosureValue_ReturnsFalseForConst()
    {
        var funcDef = new JSFunctionDef();
        var varRefs = new JSVarRef[]
        {
            new JSVarRef(JSValue.FromInt32(10), isConst: true),
        };

        var func = new JSFunction(funcDef, varRefs);

        bool result = func.SetClosureValue(0, JSValue.FromInt32(20));

        Assert.False(result);
        Assert.Equal(10, func.GetClosureValue(0).ToInt32());
    }

    #endregion

    #region HomeObject Tests

    [Fact]
    public void HomeObject_InitiallyNull()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);

        Assert.Null(func.HomeObject);
    }

    [Fact]
    public void HomeObject_CanBeSetAndRetrieved()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);
        var homeObj = new JSObject();

        func.HomeObject = homeObj;

        Assert.Same(homeObj, func.HomeObject);
    }

    #endregion

    #region Static Factory Methods Tests

    [Fact]
    public void CreateFromDef_CreatesFunction()
    {
        var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };

        var func = JSFunction.CreateFromDef(funcDef);

        Assert.Same(funcDef, func.FunctionDef);
    }

    [Fact]
    public void CreateNative_CreatesFunction()
    {
        JSCFunction impl = (thisArg, args) => JSValue.True;

        var func = JSFunction.CreateNative(impl, "native", 2);

        Assert.Equal("native", func.Name);
        Assert.Equal(2, func.Length);
        Assert.True(func.IsNativeFunction);
    }

    [Fact]
    public void CreateNativeWithMagic_CreatesFunction()
    {
        JSCFunctionMagic impl = (thisArg, args, magic) => JSValue.FromInt32(magic);

        var func = JSFunction.CreateNativeWithMagic(impl, 99, "magic", 1);

        Assert.Equal("magic", func.Name);
        Assert.Equal(99, func.Magic);
        Assert.True(func.IsNativeFunction);
    }

    #endregion

    #region ResolveForCall Tests

    [Fact]
    public void ResolveForCall_NonBoundFunction_ReturnsSelf()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var func = new JSFunction(impl);
        var thisArg = JSValue.FromInt32(1);
        var callArgs = new[] { JSValue.FromInt32(2) };

        var resolved = func.ResolveForCall(thisArg, callArgs, out var resolvedThis, out var resolvedArgs);

        Assert.Same(func, resolved);
        Assert.Equal(1, resolvedThis.ToInt32());
        Assert.Same(callArgs, resolvedArgs);
    }

    [Fact]
    public void ResolveForCall_BoundFunction_ReturnsTarget()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var func = new JSFunction(impl);
        var bound = func.Bind(JSValue.FromInt32(100));

        var resolved = bound.ResolveForCall(JSValue.Null, System.Array.Empty<JSValue>(), out var resolvedThis, out var resolvedArgs);

        Assert.Same(func, resolved);
        Assert.Equal(100, resolvedThis.ToInt32());
    }

    [Fact]
    public void ResolveForCall_BoundFunction_PrependsBoundArgs()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var func = new JSFunction(impl);
        var bound = func.Bind(JSValue.Null, JSValue.FromInt32(1), JSValue.FromInt32(2));
        var callArgs = new[] { JSValue.FromInt32(3) };

        var resolved = bound.ResolveForCall(JSValue.Null, callArgs, out var resolvedThis, out var resolvedArgs);

        Assert.Equal(3, resolvedArgs.Length);
        Assert.Equal(1, resolvedArgs[0].ToInt32());
        Assert.Equal(2, resolvedArgs[1].ToInt32());
        Assert.Equal(3, resolvedArgs[2].ToInt32());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsNativeCodeString()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var func = new JSFunction(impl, "myFunc");

        string result = func.ToString();

        Assert.Contains("function", result);
        Assert.Contains("myFunc", result);
        Assert.Contains("native code", result);
    }

    [Fact]
    public void ToString_AnonymousFunction()
    {
        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var func = new JSFunction(impl);

        string result = func.ToString();

        Assert.Contains("function", result);
    }

    #endregion

    #region JSObject Integration Tests

    [Fact]
    public void JSFunction_InheritsFromJSObject()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);

        // Can use JSObject properties
        Assert.True(func.IsExtensible);

        // Can add properties
        func.DefineProperty("customProp", new PropertyDescriptor(JSValue.FromInt32(42)));
        Assert.True(func.HasOwnProperty("customProp"));
        Assert.Equal(42, func.Get("customProp").ToInt32());
    }

    [Fact]
    public void JSFunction_HasCorrectClassId()
    {
        var bytecodeFunc = new JSFunction(new JSFunctionDef());
        Assert.Equal(JSClassId.BytecodeFunction, bytecodeFunc.ClassId);

        JSCFunction impl = (thisArg, args) => JSValue.Undefined;
        var nativeFunc = new JSFunction(impl);
        Assert.Equal(JSClassId.CFunction, nativeFunc.ClassId);

        var boundFunc = nativeFunc.Bind(JSValue.Null);
        Assert.Equal(JSClassId.BoundFunction, boundFunc.ClassId);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Constructor_NullFunctionDef_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new JSFunction((JSFunctionDef)null!));
    }

    [Fact]
    public void Constructor_NullNativeFunction_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new JSFunction((JSCFunction)null!));
    }

    [Fact]
    public void Constructor_NullNativeFunctionMagic_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new JSFunction((JSCFunctionMagic)null!, 0));
    }

    [Fact]
    public void CallNative_OnBytecodeFunction_Throws()
    {
        var funcDef = new JSFunctionDef();
        var func = new JSFunction(funcDef);

        Assert.Throws<System.InvalidOperationException>(() =>
            func.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()));
    }

    #endregion
}
