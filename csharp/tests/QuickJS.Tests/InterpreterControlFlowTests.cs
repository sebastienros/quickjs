// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for interpreter control flow opcodes (Step 6.4).
/// </summary>
public class InterpreterControlFlowTests
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterControlFlowTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    private JSFunctionDef BuildFunction(byte[] bytes, int argCount = 0)
    {
        var fd = new JSFunctionDef();
        for (int i = 0; i < argCount; i++) fd.Args.Add(new JSVarDef());
        foreach (var b in bytes)
        {
            fd.ByteCode.EmitU8(b);
        }
        return fd;
    }

    [Fact]
    public void IfTrue_TakesThenBranch()
    {
        var bytes = new List<byte>();
        bytes.Add((byte)OpCode.PushTrue);
        bytes.Add((byte)OpCode.IfFalse);
        // placeholder offset
        bytes.AddRange(new byte[4]);
        int thenStart = bytes.Count;
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(1));
        bytes.Add((byte)OpCode.Return);
        int elseStart = bytes.Count;
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.Add((byte)OpCode.Return);
        // patch IfFalse offset to elseStart
        int rel = elseStart - 6; // pc after IfFalse operand (index 6)
        var bufArr = bytes.ToArray();
        BitConverter.GetBytes(rel).CopyTo(bufArr, 2);

        var fd = BuildFunction(bufArr);
        var result = _interpreter.Execute(fd);
        Assert.False(_context.HasException);
        Assert.Equal(1, result.ToInt32());
    }

    [Fact]
    public void IfFalse_TakesElseBranch()
    {
        var bytes = new List<byte>();
        bytes.Add((byte)OpCode.PushFalse);
        bytes.Add((byte)OpCode.IfFalse);
        bytes.AddRange(new byte[4]);
        int thenStart = bytes.Count;
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(1));
        bytes.Add((byte)OpCode.Return);
        int elseStart = bytes.Count;
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.Add((byte)OpCode.Return);
        int rel = elseStart - 6;
        var bufArr = bytes.ToArray();
        BitConverter.GetBytes(rel).CopyTo(bufArr, 2);

        var fd = BuildFunction(bufArr);
        var result = _interpreter.Execute(fd);
        Assert.False(_context.HasException);
        Assert.Equal(2, result.ToInt32());
    }

    [Fact]
    public void WhileLoop_IncrementsCounter()
    {
        var bytes = new List<byte>();
        // counter = 0
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(0));
        bytes.Add((byte)OpCode.PutLoc0);
        int loopTestPos = bytes.Count;
        bytes.Add((byte)OpCode.GetLoc0);
        bytes.Add((byte)OpCode.PushI32);
        bytes.AddRange(BitConverter.GetBytes(3));
        bytes.Add((byte)OpCode.Lt);
        bytes.Add((byte)OpCode.IfFalse);
        bytes.AddRange(new byte[4]);
        int bodyStart = bytes.Count;
        bytes.Add((byte)OpCode.GetLoc0);
        bytes.Add((byte)OpCode.Inc);
        bytes.Add((byte)OpCode.PutLoc0);
        bytes.Add((byte)OpCode.Goto);
        bytes.AddRange(new byte[4]);
        int exitPos = bytes.Count;
        bytes.Add((byte)OpCode.GetLoc0);
        bytes.Add((byte)OpCode.Return);

        var bufArr = bytes.ToArray();
        int ifFalseOffset = exitPos - 18; // pc after IfFalse operand
        BitConverter.GetBytes(ifFalseOffset).CopyTo(bufArr, 14);
        int gotoBackOffset = loopTestPos - 26; // pc after goto operand
        BitConverter.GetBytes(gotoBackOffset).CopyTo(bufArr, 22);

        var fd = BuildFunction(bufArr, argCount:0);
        _interpreter.CurrentFrame = new CallFrame(localCount:1);
        var result = _interpreter.Execute(fd);
        Assert.False(_context.HasException);
        Assert.Equal(3, result.ToInt32());
    }
}
