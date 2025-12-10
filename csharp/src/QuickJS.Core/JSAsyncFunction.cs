// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Async function state enumeration.
/// </summary>
public enum AsyncFunctionState
{
    /// <summary>Async function created but not yet started.</summary>
    SuspendedStart,

    /// <summary>Async function suspended at an await expression.</summary>
    SuspendedAwait,

    /// <summary>Async function is currently executing.</summary>
    Executing,

    /// <summary>Async function has completed (resolved or rejected).</summary>
    Completed
}

/// <summary>
/// Represents an async function executor that manages async function execution.
/// </summary>
/// <remarks>
/// <para>
/// Async functions are implemented using a similar model to generators.
/// When an async function is called:
/// 1. A Promise is created and returned immediately
/// 2. The function starts executing
/// 3. When an await is encountered, execution suspends
/// 4. When the awaited promise settles, execution resumes
/// 5. When the function completes, the Promise is resolved/rejected
/// </para>
/// <para>
/// Based on js_async_function_call and js_async_function_resume from QuickJS.
/// </para>
/// </remarks>
public sealed class JSAsyncFunctionExecutor : JSObject
{
    #region Fields

    private readonly JSContext _context;
    private AsyncFunctionState _state;
    private readonly JSFunction _function;
    private readonly JSValue _thisValue;
    private readonly JSValue[] _args;
    private JSGenerator? _generator;
    private JSValue _resolveFunc;
    private JSValue _rejectFunc;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new async function executor.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="func">The async function.</param>
    /// <param name="thisVal">The 'this' value.</param>
    /// <param name="args">The function arguments.</param>
    public JSAsyncFunctionExecutor(JSContext context, JSFunction func, JSValue thisVal, JSValue[] args)
        : base(null, JSClassId.AsyncFunction)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (func?.FunctionDef == null)
            throw new ArgumentNullException(nameof(func));

        _function = func;
        _thisValue = thisVal;
        _args = args ?? Array.Empty<JSValue>();
        _state = AsyncFunctionState.SuspendedStart;
        _resolveFunc = JSValue.Undefined;
        _rejectFunc = JSValue.Undefined;
        
        // Create a generator to handle the coroutine-like execution
        _generator = new JSGenerator(context, func, thisVal, _args);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current async function state.
    /// </summary>
    public AsyncFunctionState State => _state;

    #endregion

    #region Execution

    /// <summary>
    /// Starts async function execution and returns a promise.
    /// </summary>
    /// <returns>A promise that resolves/rejects when the async function completes.</returns>
    public JSValue Start()
    {
        // Create a new promise using the Promise constructor
        var promiseCtor = _context.GlobalObject.Get("Promise");
        if (!promiseCtor.IsObject)
        {
            _context.ThrowTypeError("Promise constructor not found");
            return JSValue.Exception;
        }

        var functionProto = _context.GetClassPrototype(JSClassId.CFunction);
        JSValue capturedResolve = JSValue.Undefined;
        JSValue capturedReject = JSValue.Undefined;

        // Executor that captures the resolve/reject functions
        JSValue ExecutorFn(JSValue thisVal, JSValue[] executorArgs)
        {
            capturedResolve = executorArgs.Length > 0 ? executorArgs[0] : JSValue.Undefined;
            capturedReject = executorArgs.Length > 1 ? executorArgs[1] : JSValue.Undefined;
            return JSValue.Undefined;
        }

        var executorFunc = new JSFunction(ExecutorFn, "executor", 2, functionProto);
        
        // Call Promise constructor with the executor
        var interpreter = new Interpreter(_context);
        var promise = interpreter.CallFunction(promiseCtor, JSValue.Undefined, new[] { JSValue.FromObject(executorFunc) });

        if (promise.IsException)
            return JSValue.Exception;

        _resolveFunc = capturedResolve;
        _rejectFunc = capturedReject;

        // Start execution
        Resume(JSValue.Undefined, false);

        return promise;
    }

    /// <summary>
    /// Resumes async function execution.
    /// </summary>
    /// <param name="value">The value to resume with (await result or thrown value).</param>
    /// <param name="isThrow">Whether to throw the value.</param>
    internal void Resume(JSValue value, bool isThrow)
    {
        if (_generator == null || _state == AsyncFunctionState.Completed)
            return;

        _state = AsyncFunctionState.Executing;

        try
        {
            JSValue result;
            
            if (isThrow)
            {
                // Throw into the generator
                result = _generator.Throw(value);
            }
            else
            {
                // Send value into generator (replaces await result)
                result = _generator.Next(value);
            }
            
            // Check if it's an exception
            if (result.IsException)
            {
                _state = AsyncFunctionState.Completed;
                var error = _context.HasException ? _context.CurrentException : CreateError("Async function error");
                _context.ClearException();
                CallReject(error);
                FreeState();
                return;
            }
            
            // Check the done status
            if (!result.IsObject)
            {
                _state = AsyncFunctionState.Completed;
                CallResolve(JSValue.Undefined);
                FreeState();
                return;
            }

            var resultObj = result.AsObject();
            var done = resultObj.Get("done");
            var resultValue = resultObj.Get("value");

            if (done.IsTrue)
            {
                // Async function completed
                _state = AsyncFunctionState.Completed;
                CallResolve(resultValue);
                FreeState();
            }
            else
            {
                // Async function suspended at await
                _state = AsyncFunctionState.SuspendedAwait;
                SetupAwaitResume(resultValue);
            }
        }
        catch (Exception ex)
        {
            _state = AsyncFunctionState.Completed;
            CallReject(CreateError(ex.Message));
            FreeState();
        }
    }

    /// <summary>
    /// Sets up resumption when the awaited promise settles.
    /// </summary>
    private void SetupAwaitResume(JSValue awaitValue)
    {
        var functionProto = _context.GetClassPrototype(JSClassId.CFunction);
        var interpreter = new Interpreter(_context);

        // Wrap the value in Promise.resolve
        var promiseCtor = _context.GlobalObject.Get("Promise");
        if (!promiseCtor.IsObject)
        {
            CallReject(CreateError("Promise constructor not found"));
            return;
        }

        var promiseResolve = promiseCtor.AsObject().Get("resolve");
        if (!promiseResolve.IsObject)
        {
            CallReject(CreateError("Promise.resolve not found"));
            return;
        }

        var resolvedPromise = interpreter.CallFunction(promiseResolve, promiseCtor, new[] { awaitValue });
        if (resolvedPromise.IsException)
        {
            var error = _context.HasException ? _context.CurrentException : CreateError("Failed to resolve promise");
            _context.ClearException();
            CallReject(error);
            return;
        }

        // Create resolve/reject callbacks that resume execution
        var executor = this;

        JSValue ResolveCallback(JSValue thisVal, JSValue[] args)
        {
            var val = args.Length > 0 ? args[0] : JSValue.Undefined;
            executor.Resume(val, false);
            return JSValue.Undefined;
        }

        JSValue RejectCallback(JSValue thisVal, JSValue[] args)
        {
            var val = args.Length > 0 ? args[0] : JSValue.Undefined;
            executor.Resume(val, true);
            return JSValue.Undefined;
        }

        var resolveCallback = JSValue.FromObject(new JSFunction(ResolveCallback, "resolve", 1, functionProto));
        var rejectCallback = JSValue.FromObject(new JSFunction(RejectCallback, "reject", 1, functionProto));

        // Call then() on the promise
        if (resolvedPromise.IsObject)
        {
            var thenMethod = resolvedPromise.AsObject().Get("then");
            if (thenMethod.IsObject)
            {
                interpreter.CallFunction(thenMethod, resolvedPromise, new[] { resolveCallback, rejectCallback });
            }
        }
    }

    private JSValue CreateError(string message)
    {
        var errorObj = new JSObject(_context.GetClassPrototype(JSClassId.Error), JSClassId.Error);
        errorObj.Set("message", JSValue.FromString(message));
        errorObj.Set("name", JSValue.FromString("Error"));
        return JSValue.FromObject(errorObj);
    }

    /// <summary>
    /// Calls the resolve function.
    /// </summary>
    private void CallResolve(JSValue value)
    {
        if (!_resolveFunc.IsObject)
            return;

        var interpreter = new Interpreter(_context);
        interpreter.CallFunction(_resolveFunc, JSValue.Undefined, new[] { value });
    }

    /// <summary>
    /// Calls the reject function.
    /// </summary>
    private void CallReject(JSValue value)
    {
        if (!_rejectFunc.IsObject)
            return;

        var interpreter = new Interpreter(_context);
        interpreter.CallFunction(_rejectFunc, JSValue.Undefined, new[] { value });
    }

    /// <summary>
    /// Frees the execution state.
    /// </summary>
    private void FreeState()
    {
        _generator = null;
    }

    #endregion
}

/// <summary>
/// Return type for async function execution.
/// </summary>
internal enum AsyncReturnType
{
    /// <summary>Function suspended at await.</summary>
    Await,

    /// <summary>Function returned normally.</summary>
    Return,

    /// <summary>Function threw an exception.</summary>
    Exception
}
