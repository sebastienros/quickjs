// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for the CallFrame class.
/// </summary>
public class CallFrameTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithFunctionDef_InitializesLocals()
    {
        var functionDef = new JSFunctionDef();
        functionDef.Vars.Add(new JSVarDef());
        functionDef.Vars.Add(new JSVarDef());
        
        var frame = new CallFrame(functionDef, JSValue.Undefined);
        
        Assert.Equal(2, frame.LocalCount);
        Assert.True(frame.GetLocal(0).IsUndefined);
        Assert.True(frame.GetLocal(1).IsUndefined);
    }

    [Fact]
    public void Constructor_WithArgs_StoresArguments()
    {
        var args = new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) };
        var frame = new CallFrame(null, JSValue.Undefined, args);
        
        Assert.Equal(2, frame.ArgCount);
        Assert.Equal(1, frame.GetArg(0).ToInt32());
        Assert.Equal(2, frame.GetArg(1).ToInt32());
    }

    [Fact]
    public void Constructor_DefaultCounts_CreatesEmptyFrame()
    {
        var frame = new CallFrame(localCount: 3, argCount: 2, varRefCount: 1);
        
        Assert.Equal(3, frame.LocalCount);
        Assert.Equal(2, frame.ArgCount);
        Assert.Equal(1, frame.VarRefCount);
    }

    [Fact]
    public void Constructor_PadsArguments_WhenNotEnoughPassed()
    {
        var functionDef = new JSFunctionDef();
        functionDef.Args.Add(new JSVarDef());
        functionDef.Args.Add(new JSVarDef());
        functionDef.Args.Add(new JSVarDef());
        
        var args = new[] { JSValue.FromInt32(1) };
        var frame = new CallFrame(functionDef, JSValue.Undefined, args);
        
        Assert.Equal(3, frame.ArgCount);
        Assert.Equal(1, frame.GetArg(0).ToInt32());
        Assert.True(frame.GetArg(1).IsUndefined);
        Assert.True(frame.GetArg(2).IsUndefined);
    }

    #endregion

    #region Local Variable Tests

    [Fact]
    public void GetLocal_ValidIndex_ReturnsValue()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(1, JSValue.FromInt32(42));
        
        Assert.Equal(42, frame.GetLocal(1).ToInt32());
    }

    [Fact]
    public void GetLocal_InvalidIndex_ReturnsUndefined()
    {
        var frame = new CallFrame(localCount: 3);
        
        Assert.True(frame.GetLocal(5).IsUndefined);
        Assert.True(frame.GetLocal(-1).IsUndefined);
    }

    [Fact]
    public void SetLocal_ValidIndex_SetsValue()
    {
        var frame = new CallFrame(localCount: 3);
        
        bool result = frame.SetLocal(1, JSValue.FromString("hello"));
        
        Assert.True(result);
        Assert.Equal("hello", frame.GetLocal(1).ToString());
    }

    [Fact]
    public void SetLocal_InvalidIndex_ReturnsFalse()
    {
        var frame = new CallFrame(localCount: 3);
        
        bool result = frame.SetLocal(5, JSValue.FromInt32(42));
        
        Assert.False(result);
    }

    #endregion

    #region Argument Tests

    [Fact]
    public void GetArg_ValidIndex_ReturnsValue()
    {
        var frame = new CallFrame(argCount: 3);
        frame.SetArg(1, JSValue.FromInt32(42));
        
        Assert.Equal(42, frame.GetArg(1).ToInt32());
    }

    [Fact]
    public void GetArg_InvalidIndex_ReturnsUndefined()
    {
        var frame = new CallFrame(argCount: 3);
        
        Assert.True(frame.GetArg(5).IsUndefined);
        Assert.True(frame.GetArg(-1).IsUndefined);
    }

    [Fact]
    public void SetArg_ValidIndex_SetsValue()
    {
        var frame = new CallFrame(argCount: 3);
        
        bool result = frame.SetArg(2, JSValue.FromBoolean(true));
        
        Assert.True(result);
        Assert.True(frame.GetArg(2).IsTrue);
    }

    [Fact]
    public void SetArg_InvalidIndex_ReturnsFalse()
    {
        var frame = new CallFrame(argCount: 3);
        
        bool result = frame.SetArg(5, JSValue.FromInt32(42));
        
        Assert.False(result);
    }

    #endregion

    #region VarRef Tests

    [Fact]
    public void GetVarRef_ValidIndex_ReturnsVarRef()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef(JSValue.FromInt32(42));
        frame.SetVarRef(0, varRef);
        
        var result = frame.GetVarRef(0);
        
        Assert.Same(varRef, result);
    }

    [Fact]
    public void GetVarRef_InvalidIndex_ReturnsNull()
    {
        var frame = new CallFrame(varRefCount: 2);
        
        Assert.Null(frame.GetVarRef(5));
        Assert.Null(frame.GetVarRef(-1));
    }

    [Fact]
    public void GetVarRefValue_ValidIndex_ReturnsValue()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef(JSValue.FromString("test"));
        frame.SetVarRef(1, varRef);
        
        var value = frame.GetVarRefValue(1);
        
        Assert.Equal("test", value.ToString());
    }

    [Fact]
    public void SetVarRefValue_ValidIndex_SetsValue()
    {
        var frame = new CallFrame(varRefCount: 2);
        var varRef = new JSVarRef(JSValue.Undefined);
        frame.SetVarRef(0, varRef);
        
        frame.SetVarRefValue(0, JSValue.FromInt32(100));
        
        Assert.Equal(100, frame.GetVarRefValue(0).ToInt32());
    }

    #endregion

    #region ThisValue Tests

    [Fact]
    public void ThisValue_GetSet_Works()
    {
        var frame = new CallFrame(null, JSValue.FromString("this"));
        
        Assert.Equal("this", frame.ThisValue.ToString());
        
        frame.ThisValue = JSValue.FromInt32(42);
        Assert.Equal(42, frame.ThisValue.ToInt32());
    }

    #endregion

    #region SavedPC and SavedSP Tests

    [Fact]
    public void SavedPC_GetSet_Works()
    {
        var frame = new CallFrame();
        
        frame.SavedPC = 100;
        
        Assert.Equal(100, frame.SavedPC);
    }

    [Fact]
    public void SavedSP_GetSet_Works()
    {
        var frame = new CallFrame();
        
        frame.SavedSP = 50;
        
        Assert.Equal(50, frame.SavedSP);
    }

    #endregion

    #region VarRef Creation Tests

    [Fact]
    public void CreateVarRefForLocal_CreatesAttachedVarRef()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(1, JSValue.FromInt32(42));
        
        var varRef = frame.CreateVarRefForLocal(1);
        
        Assert.False(varRef.IsDetached);
        Assert.Equal(42, varRef.Value.ToInt32());
    }

    [Fact]
    public void CreateVarRefForLocal_SharesValueWithLocal()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(1, JSValue.FromInt32(42));
        var varRef = frame.CreateVarRefForLocal(1);
        
        // Change through frame
        frame.SetLocal(1, JSValue.FromInt32(100));
        
        // VarRef should see the change
        Assert.Equal(100, varRef.Value.ToInt32());
    }

    [Fact]
    public void CreateVarRefForLocal_WithConst_CreatesConstVarRef()
    {
        var frame = new CallFrame(localCount: 3);
        frame.SetLocal(1, JSValue.FromInt32(42));
        
        var varRef = frame.CreateVarRefForLocal(1, isLexical: true, isConst: true);
        
        Assert.True(varRef.IsConst);
        Assert.True(varRef.IsLexical);
    }

    #endregion

    #region Parent Frame Tests

    [Fact]
    public void Parent_TracksPreviousFrame()
    {
        var parentFrame = new CallFrame();
        var childFrame = new CallFrame(null, JSValue.Undefined, null, null, parentFrame);
        
        Assert.Same(parentFrame, childFrame.Parent);
    }

    #endregion
}
