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
        obj.Set("getThis", JSValue.FromObject(methodFunc)); // Store method on object

        // Get atom for "getThis"
        var atom = _runtime.InternAtom("getThis");

        var caller = new JSFunctionDef();
        int objConst = caller.Constants.Add(JSValue.FromObject(obj));
        // Push object (this value)
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)objConst);
        // CallMethod with atom and argc
        caller.ByteCode.EmitOp(OpCode.CallMethod);
        caller.ByteCode.EmitU32(atom.Value); // Atom for method name
        caller.ByteCode.EmitU16(0); // argc
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.True(result.IsObject);
        Assert.Same(obj, result.AsObject());
    }

    [Fact]
    public void MappedArguments_AliasFunctionArgs()
    {
        // function f(a) { const args = arguments; args[0] = 20; return a; }
        var f = new JSFunctionDef();
        // One formal parameter
        f.Args.Add(new JSVarDef());

        f.ByteCode.EmitOp(OpCode.SpecialObject);
        f.ByteCode.EmitU8((byte)SpecialObjectType.MappedArguments);
        f.ByteCode.EmitOp(OpCode.Dup);
        f.ByteCode.EmitOp(OpCode.PushI32);
        f.ByteCode.EmitI32(0);
        f.ByteCode.EmitOp(OpCode.PushI32);
        f.ByteCode.EmitI32(20);
        f.ByteCode.EmitOp(OpCode.PutArrayEl);
        f.ByteCode.EmitOp(OpCode.GetArg0);
        f.ByteCode.EmitOp(OpCode.Return);

        var fn = new JSFunction(f);

        // Caller: push fn const; push arg 10; call1; return
        var caller = new JSFunctionDef();
        int fnIdx = caller.Constants.Add(JSValue.FromObject(fn));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)fnIdx);
        caller.ByteCode.EmitOp(OpCode.PushI32);
        caller.ByteCode.EmitI32(10);
        caller.ByteCode.EmitOp(OpCode.Call1);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.Equal(20, result.ToInt32());
    }

    [Fact]
    public void StrictArguments_NoAliasing_CalleeUndefined()
    {
        var f = new JSFunctionDef { IsStrict = true };
        f.Args.Add(new JSVarDef());

        // atoms
        var calleeAtom = _context.Runtime.AtomTable.GetOrCreateAtom("callee");

        f.ByteCode.EmitOp(OpCode.SpecialObject);
        f.ByteCode.EmitU8((byte)SpecialObjectType.Arguments);
        // Read callee: args.callee
        f.ByteCode.EmitOp(OpCode.GetField);
        f.ByteCode.EmitU32(calleeAtom.Value);
        // Drop callee
        f.ByteCode.EmitOp(OpCode.Drop);
        // args[0] = 30
        f.ByteCode.EmitOp(OpCode.SpecialObject);
        f.ByteCode.EmitU8((byte)SpecialObjectType.Arguments);
        f.ByteCode.EmitOp(OpCode.PushI32);
        f.ByteCode.EmitI32(0);
        f.ByteCode.EmitOp(OpCode.PushI32);
        f.ByteCode.EmitI32(30);
        f.ByteCode.EmitOp(OpCode.PutArrayEl);
        f.ByteCode.EmitOp(OpCode.GetArg0);
        f.ByteCode.EmitOp(OpCode.Return);

        var fn = new JSFunction(f);

        var caller = new JSFunctionDef();
        int fnIdx = caller.Constants.Add(JSValue.FromObject(fn));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)fnIdx);
        caller.ByteCode.EmitOp(OpCode.PushI32);
        caller.ByteCode.EmitI32(10);
        caller.ByteCode.EmitOp(OpCode.Call1);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        // No aliasing: a stays 10
        Assert.Equal(10, result.ToInt32());
    }
}
