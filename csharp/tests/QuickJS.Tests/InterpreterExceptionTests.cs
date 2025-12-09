// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class InterpreterExceptionTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterExceptionTests()
    {
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    [Fact]
    public void TryCatch_HandlesThrow()
    {
        var fd = new JSFunctionDef();
        var bc = new List<byte>();
        // PushI32 1
        bc.Add((byte)OpCode.PushI32);
        bc.AddRange(BitConverter.GetBytes(1));
        // Throw
        int throwPc = bc.Count;
        bc.Add((byte)OpCode.Throw);
        int catchPc = bc.Count;
        // Catch block: (exception on stack) push 1; add; return
        bc.Add((byte)OpCode.PushI32);
        bc.AddRange(BitConverter.GetBytes(1));
        bc.Add((byte)OpCode.Add);
        bc.Add((byte)OpCode.Return);

        var handler = new JSExceptionHandler
        {
            StartPc = 0,
            EndPc = catchPc, // include throw
            CatchPc = catchPc,
            StackDepth = 0,
        };
        fd.ExceptionHandlers.Add(handler);
        foreach (var b in bc) fd.ByteCode.EmitU8(b);

        var result = _interpreter.Execute(fd);
        Assert.False(_context.HasException);
        Assert.Equal(2, result.ToInt32());
    }

    [Fact]
    public void TryFinally_ReturnsFromFinally()
    {
        var fd = new JSFunctionDef();
        var bc = new List<byte>();
        // try: return 5
        bc.Add((byte)OpCode.PushI32);
        bc.AddRange(BitConverter.GetBytes(5));
        int returnPc = bc.Count;
        bc.Add((byte)OpCode.Return);
        int finallyPc = bc.Count;
        // finally: push 42; return
        bc.Add((byte)OpCode.PushI32);
        bc.AddRange(BitConverter.GetBytes(42));
        bc.Add((byte)OpCode.Return);

        var handler = new JSExceptionHandler
        {
            StartPc = 0,
            EndPc = returnPc + 1,
            FinallyPc = finallyPc,
            StackDepth = 0,
        };
        fd.ExceptionHandlers.Add(handler);
        foreach (var b in bc) fd.ByteCode.EmitU8(b);

        var result = _interpreter.Execute(fd);
        Assert.False(_context.HasException);
        Assert.Equal(42, result.ToInt32());
    }
}
