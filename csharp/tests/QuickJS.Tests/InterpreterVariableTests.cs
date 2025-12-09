// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for Interpreter variable and property access operations.
/// </summary>
public class InterpreterVariableTests
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterVariableTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    #region Local Variable Tests

    [Fact]
    public void GetLoc_WithFrame_ReturnsLocalValue()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(1, JSValue.FromInt32(42));
        _interpreter.CurrentFrame = frame;

        _interpreter.GetLoc(1);

        Assert.Equal(42, _interpreter.Peek().ToInt32());
    }

    [Fact]
    public void PutLoc_WithFrame_SetsLocalValue()
    {
        var frame = new CallFrame(localCount: 3);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromString("hello"));

        _interpreter.PutLoc(2);

        Assert.Equal("hello", frame.GetLocal(2).ToString());
        Assert.True(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void SetLoc_WithFrame_SetsLocalKeepsStack()
    {
        var frame = new CallFrame(localCount: 3);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromInt32(100));

        _interpreter.SetLoc(0);

        Assert.Equal(100, frame.GetLocal(0).ToInt32());
        Assert.Equal(100, _interpreter.Peek().ToInt32()); // Value still on stack
    }

    [Fact]
    public void GetLoc_WithoutFrame_ThrowsError()
    {
        _interpreter.CurrentFrame = null;

        _interpreter.GetLoc(0);

        Assert.True(_context.HasException);
    }

    #endregion

    #region Argument Tests

    [Fact]
    public void GetArg_WithFrame_ReturnsArgumentValue()
    {
        var frame = new CallFrame(argCount: 3);
        frame.SetArg(0, JSValue.FromDouble(3.14));
        _interpreter.CurrentFrame = frame;

        _interpreter.GetArg(0);

        Assert.Equal(3.14, _interpreter.Peek().ToDouble(), 5);
    }

    [Fact]
    public void PutArg_WithFrame_SetsArgumentValue()
    {
        var frame = new CallFrame(argCount: 3);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromBoolean(true));

        _interpreter.PutArg(1);

        Assert.True(frame.GetArg(1).IsTrue);
        Assert.True(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void SetArg_WithFrame_SetsArgKeepsStack()
    {
        var frame = new CallFrame(argCount: 3);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromString("test"));

        _interpreter.SetArg(2);

        Assert.Equal("test", frame.GetArg(2).ToString());
        Assert.Equal("test", _interpreter.Peek().ToString());
    }

    #endregion

    #region VarRef Tests

    [Fact]
    public void GetVarRef_WithFrame_ReturnsCapturedValue()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef(JSValue.FromInt32(999));
        frame.SetVarRef(0, varRef);
        _interpreter.CurrentFrame = frame;

        _interpreter.GetVarRef(0);

        Assert.Equal(999, _interpreter.Peek().ToInt32());
    }

    [Fact]
    public void PutVarRef_WithFrame_SetsCapturedValue()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef();
        frame.SetVarRef(1, varRef);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromString("captured"));

        _interpreter.PutVarRef(1);

        Assert.Equal("captured", varRef.Value.ToString());
    }

    [Fact]
    public void PutVarRef_ConstVariable_ThrowsError()
    {
        var frame = new CallFrame(varRefCount: 2);
        var constVarRef = new JSVarRef(JSValue.FromInt32(42), isConst: true);
        frame.SetVarRef(0, constVarRef);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromInt32(100));

        _interpreter.PutVarRef(0);

        Assert.True(_context.HasException);
    }

    [Fact]
    public void SetVarRef_WithFrame_SetsCapturedKeepsStack()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef();
        frame.SetVarRef(0, varRef);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromDouble(1.5));

        _interpreter.SetVarRef(0);

        Assert.Equal(1.5, varRef.Value.ToDouble(), 5);
        Assert.Equal(1.5, _interpreter.Peek().ToDouble(), 5);
    }

    #endregion

    #region TDZ Check Tests

    [Fact]
    public void GetLocCheck_UninitializedVariable_ThrowsError()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(0, JSValue.Uninitialized);
        _interpreter.CurrentFrame = frame;

        _interpreter.GetLocCheck(0);

        Assert.True(_context.HasException);
    }

    [Fact]
    public void GetLocCheck_InitializedVariable_ReturnsValue()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(0, JSValue.FromInt32(42));
        _interpreter.CurrentFrame = frame;

        _interpreter.GetLocCheck(0);

        Assert.False(_context.HasException);
        Assert.Equal(42, _interpreter.Peek().ToInt32());
    }

    [Fact]
    public void PutLocCheck_UninitializedVariable_ThrowsError()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(0, JSValue.Uninitialized);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromInt32(100));

        _interpreter.PutLocCheck(0);

        Assert.True(_context.HasException);
    }

    [Fact]
    public void PutLocCheckInit_InitializesVariable()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(0, JSValue.Uninitialized);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromInt32(42));

        _interpreter.PutLocCheckInit(0);

        Assert.False(_context.HasException);
        Assert.Equal(42, frame.GetLocal(0).ToInt32());
    }

    [Fact]
    public void SetLocUninitialized_MarksVariableUninitialized()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(0, JSValue.FromInt32(42));
        _interpreter.CurrentFrame = frame;

        _interpreter.SetLocUninitialized(0);

        Assert.True(frame.GetLocal(0).IsUninitialized);
    }

    [Fact]
    public void GetVarRefCheck_UninitializedVariable_ThrowsError()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef(JSValue.Uninitialized);
        frame.SetVarRef(0, varRef);
        _interpreter.CurrentFrame = frame;

        _interpreter.GetVarRefCheck(0);

        Assert.True(_context.HasException);
    }

    [Fact]
    public void PutVarRefCheck_ConstVariable_ThrowsError()
    {
        var frame = new CallFrame(varRefCount: 2);
        var constVarRef = new JSVarRef(JSValue.FromInt32(42), isConst: true);
        frame.SetVarRef(0, constVarRef);
        _interpreter.CurrentFrame = frame;
        _interpreter.Push(JSValue.FromInt32(100));

        _interpreter.PutVarRefCheck(0);

        Assert.True(_context.HasException);
    }

    #endregion
}

/// <summary>
/// Tests for Interpreter property access operations.
/// </summary>
public class InterpreterPropertyTests
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterPropertyTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    #region GetField Tests

    [Fact]
    public void GetField_Object_ReturnsPropertyValue()
    {
        var obj = new JSObject();
        obj.Set("name", JSValue.FromString("test"));
        _interpreter.Push(JSValue.FromObject(obj));

        _interpreter.GetField("name");

        Assert.Equal("test", _interpreter.Peek().ToString());
    }

    [Fact]
    public void GetField_Object_MissingProperty_ReturnsUndefined()
    {
        var obj = new JSObject();
        _interpreter.Push(JSValue.FromObject(obj));

        _interpreter.GetField("missing");

        Assert.True(_interpreter.Peek().IsUndefined);
    }

    [Fact]
    public void GetField_Null_ThrowsTypeError()
    {
        _interpreter.Push(JSValue.Null);

        _interpreter.GetField("prop");

        Assert.True(_context.HasException);
    }

    [Fact]
    public void GetField_Undefined_ThrowsTypeError()
    {
        _interpreter.Push(JSValue.Undefined);

        _interpreter.GetField("prop");

        Assert.True(_context.HasException);
    }

    [Fact]
    public void GetField_StringLength_ReturnsLength()
    {
        _interpreter.Push(JSValue.FromString("hello"));

        _interpreter.GetField("length");

        Assert.Equal(5, _interpreter.Peek().ToInt32());
    }

    [Fact]
    public void GetField_StringIndex_ReturnsCharacter()
    {
        _interpreter.Push(JSValue.FromString("abc"));

        _interpreter.GetField("1");

        Assert.Equal("b", _interpreter.Peek().ToString());
    }

    #endregion

    #region GetField2 Tests

    [Fact]
    public void GetField2_Object_KeepsObjectPushesValue()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromObject(obj));

        _interpreter.GetField2("x");

        Assert.Equal(2, _interpreter.StackPointer);
        Assert.Equal(10, _interpreter.Peek().ToInt32());
        _interpreter.Pop();
        Assert.True(_interpreter.Peek().IsObject);
    }

    #endregion

    #region PutField Tests

    [Fact]
    public void PutField_Object_SetsPropertyValue()
    {
        var obj = new JSObject();
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromInt32(42));

        _interpreter.PutField("count");

        Assert.Equal(42, obj.Get("count").ToInt32());
        Assert.True(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void PutField_Null_ThrowsTypeError()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.Push(JSValue.FromInt32(42));

        _interpreter.PutField("prop");

        Assert.True(_context.HasException);
    }

    #endregion

    #region DefineField Tests

    [Fact]
    public void DefineField_Object_SetsPropertyKeepsObject()
    {
        var obj = new JSObject();
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromString("value"));

        _interpreter.DefineField("key");

        Assert.Equal("value", obj.Get("key").ToString());
        Assert.Equal(1, _interpreter.StackPointer); // Object still on stack
    }

    #endregion

    #region GetArrayEl Tests

    [Fact]
    public void GetArrayEl_Object_IntIndex_ReturnsElement()
    {
        var obj = new JSObject();
        obj.Set(0u, JSValue.FromString("first"));
        obj.Set(1u, JSValue.FromString("second"));
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromInt32(1));

        _interpreter.GetArrayEl();

        Assert.Equal("second", _interpreter.Peek().ToString());
    }

    [Fact]
    public void GetArrayEl_String_IntIndex_ReturnsCharacter()
    {
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.Push(JSValue.FromInt32(2));

        _interpreter.GetArrayEl();

        Assert.Equal("l", _interpreter.Peek().ToString());
    }

    [Fact]
    public void GetArrayEl_Null_ThrowsTypeError()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.Push(JSValue.FromInt32(0));

        _interpreter.GetArrayEl();

        Assert.True(_context.HasException);
    }

    [Fact]
    public void GetArrayEl_StringIndex_ConvertsToProperty()
    {
        var obj = new JSObject();
        obj.Set("test", JSValue.FromInt32(99));
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromString("test"));

        _interpreter.GetArrayEl();

        Assert.Equal(99, _interpreter.Peek().ToInt32());
    }

    #endregion

    #region GetArrayEl2 Tests

    [Fact]
    public void GetArrayEl2_Object_KeepsObjectReplacesIndex()
    {
        var obj = new JSObject();
        obj.Set(0u, JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromInt32(0));

        _interpreter.GetArrayEl2();

        Assert.Equal(2, _interpreter.StackPointer);
        Assert.Equal(42, _interpreter.Peek().ToInt32());
    }

    #endregion

    #region PutArrayEl Tests

    [Fact]
    public void PutArrayEl_Object_IntIndex_SetsElement()
    {
        var obj = new JSObject();
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromString("fifth"));

        _interpreter.PutArrayEl();

        Assert.Equal("fifth", obj.Get(5u).ToString());
        Assert.True(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void PutArrayEl_Object_StringIndex_SetsProperty()
    {
        var obj = new JSObject();
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromString("key"));
        _interpreter.Push(JSValue.FromInt32(123));

        _interpreter.PutArrayEl();

        Assert.Equal(123, obj.Get("key").ToInt32());
    }

    [Fact]
    public void PutArrayEl_Null_ThrowsTypeError()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Push(JSValue.FromInt32(42));

        _interpreter.PutArrayEl();

        Assert.True(_context.HasException);
    }

    #endregion

    #region Prototype Chain Tests

    [Fact]
    public void GetField_FollowsPrototypeChain()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromString("from proto"));
        var obj = new JSObject();
        obj.SetPrototype(proto);
        _interpreter.Push(JSValue.FromObject(obj));

        _interpreter.GetField("inherited");

        Assert.Equal("from proto", _interpreter.Peek().ToString());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetArrayEl_OutOfBoundsString_ReturnsUndefined()
    {
        _interpreter.Push(JSValue.FromString("abc"));
        _interpreter.Push(JSValue.FromInt32(10));

        _interpreter.GetArrayEl();

        Assert.True(_interpreter.Peek().IsUndefined);
    }

    [Fact]
    public void GetArrayEl_NegativeIndex_UsesStringProperty()
    {
        var obj = new JSObject();
        obj.Set("-1", JSValue.FromString("negative"));
        _interpreter.Push(JSValue.FromObject(obj));
        _interpreter.Push(JSValue.FromInt32(-1));

        _interpreter.GetArrayEl();

        Assert.Equal("negative", _interpreter.Peek().ToString());
    }

    #endregion
}
