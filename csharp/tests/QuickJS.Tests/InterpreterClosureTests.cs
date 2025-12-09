// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class InterpreterClosureTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterClosureTests()
    {
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    [Fact]
    public void Closure_CapturesOuterVariable()
    {
        // outer: let x = 41; return function inner() { return x + 1; }
        var outer = new JSFunctionDef();
        // var x (lexical)
        var xIdx = outer.AddVar(_context.Runtime.AtomTable.GetOrCreateAtom("x"), isLexical: true);
        outer.VarRefCount = 1;

        // inner function def
        var inner = new JSFunctionDef();
        inner.FuncType = JSParseFunctionType.Expression;
        inner.ClosureVars.Add(new JSClosureVar
        {
            Name = _context.Runtime.AtomTable.GetOrCreateAtom("x"),
            ClosureType = JSClosureType.Local,
            VarIndex = xIdx,
            IsLexical = true,
        });
        // bytecode: GetVarRef0 -> Push1 -> Add -> Return
        inner.ByteCode.EmitOp(OpCode.GetVarRef0);
        inner.ByteCode.EmitOp(OpCode.PushI32);
        inner.ByteCode.EmitI32(1);
        inner.ByteCode.EmitOp(OpCode.Add);
        inner.ByteCode.EmitOp(OpCode.Return);

        int innerConst = outer.Constants.Add(JSValue.FromObject(new JSFunction(inner)));

        // outer bytecode
        outer.ByteCode.EmitOp(OpCode.PushI32);
        outer.ByteCode.EmitI32(41);
        outer.ByteCode.EmitOp(OpCode.PutLoc0);
        // make var ref for x
        outer.ByteCode.EmitOp(OpCode.MakeVarRefRef);
        outer.ByteCode.EmitU32(_context.Runtime.AtomTable.GetOrCreateAtom("x").Value);
        outer.ByteCode.EmitU16((ushort)xIdx);
        // fclosure
        outer.ByteCode.EmitOp(OpCode.FClosure);
        outer.ByteCode.EmitU32((uint)innerConst);
        outer.ByteCode.EmitOp(OpCode.Return);

        var outerFunc = new JSFunction(outer);

        // caller: push outer; call0; push result func; call0; return
        var caller = new JSFunctionDef();
        int outerConst = caller.Constants.Add(JSValue.FromObject(outerFunc));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)outerConst);
        caller.ByteCode.EmitOp(OpCode.Call0);
        // top of stack: inner function
        caller.ByteCode.EmitOp(OpCode.Call0);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void Closure_DetachesAfterOuterReturn()
    {
        // outer returns inner; inner uses x after outer returns.
        var outer = new JSFunctionDef();
        var xIdx = outer.AddVar(_context.Runtime.AtomTable.GetOrCreateAtom("x"), isLexical: true);
        outer.VarRefCount = 1;

        var inner = new JSFunctionDef();
        inner.FuncType = JSParseFunctionType.Expression;
        inner.ClosureVars.Add(new JSClosureVar
        {
            Name = _context.Runtime.AtomTable.GetOrCreateAtom("x"),
            ClosureType = JSClosureType.Local,
            VarIndex = xIdx,
            IsLexical = true,
        });
        inner.ByteCode.EmitOp(OpCode.GetVarRef0);
        inner.ByteCode.EmitOp(OpCode.Return);

        int innerConst = outer.Constants.Add(JSValue.FromObject(new JSFunction(inner)));

        outer.ByteCode.EmitOp(OpCode.PushI32);
        outer.ByteCode.EmitI32(7);
        outer.ByteCode.EmitOp(OpCode.PutLoc0);
        outer.ByteCode.EmitOp(OpCode.MakeVarRefRef);
        outer.ByteCode.EmitU32(_context.Runtime.AtomTable.GetOrCreateAtom("x").Value);
        outer.ByteCode.EmitU16((ushort)xIdx);
        outer.ByteCode.EmitOp(OpCode.FClosure);
        outer.ByteCode.EmitU32((uint)innerConst);
        outer.ByteCode.EmitOp(OpCode.Return);

        var outerFunc = new JSFunction(outer);
        var caller = new JSFunctionDef();
        int outerConst = caller.Constants.Add(JSValue.FromObject(outerFunc));
        caller.ByteCode.EmitOp(OpCode.PushConst);
        caller.ByteCode.EmitU32((uint)outerConst);
        caller.ByteCode.EmitOp(OpCode.Call0);
        caller.ByteCode.EmitOp(OpCode.Call0);
        caller.ByteCode.EmitOp(OpCode.Return);

        var result = _interpreter.Execute(caller);
        Assert.False(_context.HasException);
        Assert.Equal(7, result.ToInt32());
    }
}
