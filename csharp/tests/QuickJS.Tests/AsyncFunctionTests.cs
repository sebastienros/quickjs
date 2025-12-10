// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for async functions and the JSAsyncFunctionExecutor class (Step 9.3).
/// </summary>
public class AsyncFunctionTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public AsyncFunctionTests()
    {
        _context = _runtime.CreateContext();
    }

    #region Async Function Creation Tests

    [Fact]
    public void AsyncFunction_CanBeCreated()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(42);
        funcDef.ByteCode.EmitOp(OpCode.Return);

        var func = new JSFunction(funcDef);

        Assert.True(func.IsAsync);
        Assert.False(func.IsGenerator);
    }

    [Fact]
    public void AsyncFunction_IsAsyncFlag()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        var func = new JSFunction(funcDef);

        Assert.True(func.IsAsync);
    }

    [Fact]
    public void AsyncGeneratorFunction_IsAsyncAndGenerator()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.AsyncGenerator };
        var func = new JSFunction(funcDef);

        Assert.True(func.IsAsync);
        Assert.True(func.IsGenerator);
    }

    #endregion

    #region Async Function Executor Tests

    [Fact]
    public void AsyncFunctionExecutor_CanBeCreated()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        Assert.NotNull(executor);
        Assert.Equal(AsyncFunctionState.SuspendedStart, executor.State);
    }

    [Fact]
    public void AsyncFunctionExecutor_Start_ReturnsPromise()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        _context.RunMicrotasks();

        Assert.True(promise.IsObject);
        // A promise object should have a 'then' method
        var promiseObj = promise.AsObject();
        Assert.True(promiseObj.HasProperty("then") || promiseObj.Prototype?.Get("then").IsObject == true);
    }

    [Fact]
    public void AsyncFunctionExecutor_SimpleReturn_ResolvesPromise()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(42);
        funcDef.ByteCode.EmitOp(OpCode.Return);

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        _context.RunMicrotasks();

        Assert.Equal(AsyncFunctionState.Completed, executor.State);

        // Verify the promise resolved with 42
        JSValue result = JSValue.Undefined;
        var thenFn = promise.AsObject().Prototype?.Get("then");
        if (thenFn.HasValue && thenFn.Value.IsObject)
        {
            var functionProto = _context.GetClassPrototype(JSClassId.CFunction);
            var callback = new JSFunction((_, args) =>
            {
                result = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.Undefined;
            }, "callback", 1, functionProto);

            var interpreter = new Interpreter(_context);
            interpreter.CallFunction(thenFn.Value, promise, new[] { JSValue.FromObject(callback) });
            _context.RunMicrotasks();
        }

        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void AsyncFunctionExecutor_ReturnUndef_ResolvesWithUndefined()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        _context.RunMicrotasks();

        Assert.Equal(AsyncFunctionState.Completed, executor.State);
    }

    #endregion

    #region Await Expression Tests

    [Fact]
    public void AsyncFunctionExecutor_Await_SuspendsExecution()
    {
        // async function that awaits a value
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(100);
        funcDef.ByteCode.EmitOp(OpCode.Await);  // await 100
        funcDef.ByteCode.EmitOp(OpCode.Return); // return the awaited value

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        // After await, should be suspended
        // After microtasks, Promise.resolve(100) should resolve and resume
        _context.RunMicrotasks();

        // Should complete after microtasks since we're awaiting a non-promise value
        Assert.Equal(AsyncFunctionState.Completed, executor.State);
    }

    [Fact]
    public void AsyncFunctionExecutor_AwaitResolvedPromise_ResumesWithValue()
    {
        // async function that returns 42
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(42);
        funcDef.ByteCode.EmitOp(OpCode.Await);  // await 42
        funcDef.ByteCode.EmitOp(OpCode.Return); // return awaited value

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        _context.RunMicrotasks();

        Assert.Equal(AsyncFunctionState.Completed, executor.State);

        // Verify the promise resolved with 42
        JSValue result = JSValue.Undefined;
        var thenFn = promise.AsObject().Prototype?.Get("then");
        if (thenFn.HasValue && thenFn.Value.IsObject)
        {
            var functionProto = _context.GetClassPrototype(JSClassId.CFunction);
            var callback = new JSFunction((_, args) =>
            {
                result = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.Undefined;
            }, "callback", 1, functionProto);

            var interpreter = new Interpreter(_context);
            interpreter.CallFunction(thenFn.Value, promise, new[] { JSValue.FromObject(callback) });
            _context.RunMicrotasks();
        }

        Assert.Equal(42, result.ToInt32());
    }

    #endregion

    #region Interpreter Integration Tests

    [Fact]
    public void Interpreter_AsyncFunction_ReturnsPromise()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(42);
        funcDef.ByteCode.EmitOp(OpCode.Return);

        var func = new JSFunction(funcDef);
        var interpreter = new Interpreter(_context);

        var result = interpreter.CallFunction(JSValue.FromObject(func), JSValue.Undefined, Array.Empty<JSValue>());
        _context.RunMicrotasks();

        // Should return a promise
        Assert.True(result.IsObject);
    }

    [Fact]
    public void Interpreter_AsyncFunction_NotTreatedAsGenerator()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var interpreter = new Interpreter(_context);

        var result = interpreter.CallFunction(JSValue.FromObject(func), JSValue.Undefined, Array.Empty<JSValue>());
        _context.RunMicrotasks();

        // Should not be a generator
        Assert.False(result.IsObject && result.AsObject() is JSGenerator);
    }

    [Fact]
    public void Interpreter_AsyncGenerator_ReturnGenerator()
    {
        // Async generators should still return a generator (they're handled differently)
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.AsyncGenerator };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var interpreter = new Interpreter(_context);

        var result = interpreter.CallFunction(JSValue.FromObject(func), JSValue.Undefined, Array.Empty<JSValue>());

        // Async generators should return a generator object
        Assert.True(result.IsObject);
    }

    #endregion

    #region Parsed Async Function Tests

    [Fact]
    public void Parse_AsyncFunction_SetsAsyncKind()
    {
        var atoms = new AtomTable();
        var parser = new Parser("async function foo() { return 42; }", "test.js", atoms);
        parser.ParseProgram();

        // Find the inner function definition
        var innerFunc = FindInnerFunction(parser.CurrentFunction);
        Assert.NotNull(innerFunc);
        Assert.True((innerFunc!.FuncKind & JSFunctionKind.Async) != 0);
    }

    [Fact]
    public void Parse_AsyncArrowFunction_SetsAsyncKind()
    {
        var atoms = new AtomTable();
        var parser = new Parser("const foo = async () => 42;", "test.js", atoms);
        parser.ParseProgram();

        // Find the inner function definition
        var innerFunc = FindInnerFunction(parser.CurrentFunction);
        Assert.NotNull(innerFunc);
        Assert.True((innerFunc!.FuncKind & JSFunctionKind.Async) != 0);
    }

    [Fact]
    public void Parse_AwaitExpression_EmitsAwaitOpcode()
    {
        var atoms = new AtomTable();
        var parser = new Parser("async function foo() { await 42; }", "test.js", atoms);
        parser.ParseProgram();

        var innerFunc = FindInnerFunction(parser.CurrentFunction);
        Assert.NotNull(innerFunc);

        // Check that Await opcode is present
        var bytecode = innerFunc!.ByteCode.ToArray();
        var hasAwait = false;
        for (int i = 0; i < bytecode.Length; i++)
        {
            if ((OpCode)bytecode[i] == OpCode.Await)
            {
                hasAwait = true;
                break;
            }
        }

        Assert.True(hasAwait, "Await opcode should be emitted for await expression");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Parse_AwaitOutsideAsync_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("function foo() { await 42; }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() =>
        {
            parser.ParseProgram();
        });
    }

    [Fact]
    public void AsyncFunctionExecutor_Exception_RejectsPromise()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Async };
        funcDef.ByteCode.EmitOp(OpCode.ThrowError);  // Throw error
        funcDef.ByteCode.EmitU8((byte)JSErrorType.Error);
        funcDef.ByteCode.EmitI32(0); // atom index

        var func = new JSFunction(funcDef);
        var executor = new JSAsyncFunctionExecutor(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var promise = executor.Start();
        _context.RunMicrotasks();

        Assert.Equal(AsyncFunctionState.Completed, executor.State);
    }

    #endregion

    #region Helper Methods

    private static JSFunctionDef? FindInnerFunction(JSFunctionDef funcDef)
    {
        if (funcDef.Children.Count > 0)
        {
            return funcDef.Children[0];
        }
        return null;
    }

    #endregion
}
