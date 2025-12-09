// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for interpreter call/return opcodes (Step 6.5).
/// </summary>
public class InterpreterCallTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterCallTests()
    {
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    private JSFunctionDef BuildFunction(byte[] bytes, int argCount = 0)
    {
        var fd = new JSFunctionDef();
        for (int i = 0; i < argCount; i++) fd.Args.Add(new JSVarDef());
        foreach (var b in bytes)
            fd.ByteCode.EmitU8(b);
        return fd;
    }

    [Fact]
    public void CallBytecodeFunction_ReturnsValue()
    {
        // Callee: return 42;
        var callee = new JSFunctionDef();
        callee.ByteCode.EmitOp(OpCode.PushI32);
        callee.ByteCode.EmitI32(42);
        callee.ByteCode.EmitOp(OpCode.Return);
        var calleeFunc = new JSFunction(callee);

        // Caller: push callee const; call0; return
        var caller = new JSFunctionDef();
        int constIdx = caller.Constants.Add(JSValue.FromObject(calleeFunc));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)constIdx);
        caller.ByteCode.EmitOp(OpCode.Call0);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void CallBytecodeFunction_WithArgs_AddsNumbers()
    {
        // Callee: return arg0 + arg1;
        var callee = new JSFunctionDef();
        callee.Args.Add(new JSVarDef());
        callee.Args.Add(new JSVarDef());
        callee.ByteCode.EmitOp(OpCode.GetArg0);
        callee.ByteCode.EmitOp(OpCode.GetArg1);
        callee.ByteCode.EmitOp(OpCode.Add);
        callee.ByteCode.EmitOp(OpCode.Return);
        var calleeFunc = new JSFunction(callee);

        var caller = new JSFunctionDef();
        int constIdx = caller.Constants.Add(JSValue.FromObject(calleeFunc));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)constIdx);
        // push args 3 and 4
        caller.ByteCode.EmitOp(OpCode.PushI32);
        caller.ByteCode.EmitI32(3);
        caller.ByteCode.EmitOp(OpCode.PushI32);
        caller.ByteCode.EmitI32(4);
        caller.ByteCode.EmitOp(OpCode.Call);
        caller.ByteCode.EmitU16(2); // argc
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.Equal(7, result.ToInt32());
    }

    [Fact]
    public void CallMethod_UsesThisValue()
    {
        // Method: return this;
        var methodDef = new JSFunctionDef();
        methodDef.ByteCode.EmitOp(OpCode.PushThis);
        methodDef.ByteCode.EmitOp(OpCode.Return);
        var methodFunc = new JSFunction(methodDef);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(99)); // sanity

        var caller = new JSFunctionDef();
        int calleeConst = caller.Constants.Add(JSValue.FromObject(methodFunc));
        int objConst = caller.Constants.Add(JSValue.FromObject(obj));
        // push this then callee
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)objConst);
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)calleeConst);
        caller.ByteCode.EmitOp(OpCode.CallMethod);
        caller.ByteCode.EmitU16(0);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.True(result.IsObject);
        Assert.Same(obj, result.AsObject());
    }
}
