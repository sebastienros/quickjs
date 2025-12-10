# Step 9.3: Async/Await

This document describes the implementation of async functions and the await operator in the QuickJS C# port.

## Overview

Async functions are a syntactic feature that makes working with Promises more convenient. An async function:
- Returns a Promise when called
- Can use the `await` keyword to pause execution until a Promise settles
- Resumes execution when the awaited Promise resolves or rejects

## Architecture

### Relationship to Generators

Async functions share significant infrastructure with generators because both use a coroutine-like execution model:

| Feature | Generators | Async Functions |
|---------|-----------|-----------------|
| Suspension | `yield` | `await` |
| Return type | Iterator object | Promise |
| Resumption | `.next()` call | Promise settlement |
| State machine | Yes | Yes |

In QuickJS (and our C# port), async functions reuse the generator bytecode execution infrastructure through the `JSGenerator` class.

### QuickJS C Reference

From the QuickJS C source, the key functions are:

```c
// Called when an async function is invoked
static JSValue js_async_function_call(JSContext *ctx, JSValue func_obj,
                                      JSValue this_obj, int argc, JSValue *argv)
{
    // 1. Create promise capability (promise + resolve + reject)
    // 2. Create async function state
    // 3. Start execution
    // 4. Return the promise
}

// Called to resume async function execution
static int js_async_function_resume(JSContext *ctx, JSAsyncFunctionState *s)
{
    // Execute bytecode until:
    // - await: wrap value in Promise, attach .then handlers
    // - return: resolve the promise
    // - throw: reject the promise
}
```

## Implementation

### JSAsyncFunctionExecutor Class

The `JSAsyncFunctionExecutor` class manages async function execution:

```csharp
public sealed class JSAsyncFunctionExecutor : JSObject
{
    private readonly JSContext _context;
    private AsyncFunctionState _state;
    private readonly JSFunction _function;
    private JSGenerator? _generator;
    private JSValue _resolveFunc;
    private JSValue _rejectFunc;

    public JSAsyncFunctionExecutor(JSContext context, JSFunction func, 
                                    JSValue thisVal, JSValue[] args)
    {
        // Create a generator to handle coroutine-like execution
        _generator = new JSGenerator(context, func, thisVal, args);
    }

    public JSValue Start()
    {
        // 1. Create a Promise using the Promise constructor
        // 2. Capture resolve/reject functions
        // 3. Start execution via Resume()
        // 4. Return the promise
    }

    internal void Resume(JSValue value, bool isThrow)
    {
        // Execute generator until yield/return/throw
        // Handle await by wrapping in Promise.resolve and attaching callbacks
    }
}
```

### AsyncFunctionState Enum

```csharp
public enum AsyncFunctionState
{
    SuspendedStart,   // Created but not yet started
    SuspendedAwait,   // Suspended at an await expression
    Executing,        // Currently running
    Completed         // Finished (resolved or rejected)
}
```

### Await Opcode

The parser emits `OpCode.Await` for await expressions:

```csharp
private void ParseAwaitExpression()
{
    if ((_currentFunction.FuncKind & JSFunctionKind.Async) == 0)
    {
        throw new JSSyntaxError(
            "await expression is only valid in async functions",
            _currentToken.Start);
    }

    NextToken(); // consume 'await'
    ParseUnaryExpression(ParseFlags.None);
    EmitOp(OpCode.Await);
}
```

The generator's bytecode executor handles `OpCode.Await`:

```csharp
case OpCode.Await:
{
    var awaitValue = PopStack();
    _funcState.ProgramCounter = pc;
    return (awaitValue, GeneratorReturnType.Yield);
}
```

### Interpreter Integration

The `Interpreter.CallFunction` method detects async functions:

```csharp
// Async functions return a promise and execute asynchronously
if (func.IsAsync && !func.IsGenerator && !isConstructor)
{
    var asyncExecutor = new JSAsyncFunctionExecutor(_context, func, thisVal, args);
    return asyncExecutor.Start();
}
```

## Execution Flow

### Example: Simple Async Function

```javascript
async function fetchData() {
    const result = await Promise.resolve(42);
    return result;
}

const promise = fetchData();
```

Execution flow:
1. `fetchData()` is called
2. `JSAsyncFunctionExecutor.Start()` creates a Promise and starts execution
3. Execution reaches `await Promise.resolve(42)`
4. The await value is wrapped in `Promise.resolve()`
5. `.then()` callbacks are attached to resume execution
6. The outer promise is returned to the caller
7. Microtasks run: `Promise.resolve(42)` settles
8. Resume callback fires with value `42`
9. Execution continues, `return result` executes
10. The outer promise is resolved with `42`

### Error Handling

When an async function throws:

```javascript
async function willThrow() {
    throw new Error("oops");
}

willThrow().catch(e => console.log(e.message)); // "oops"
```

Execution flow:
1. Function throws an exception
2. `JSAsyncFunctionExecutor` catches the error
3. The outer promise is rejected with the error
4. `.catch()` handler receives the error

When an awaited Promise rejects:

```javascript
async function awaitReject() {
    await Promise.reject("error");
}
```

Execution flow:
1. `await` suspends execution
2. Awaited promise rejects
3. Reject callback fires, resuming with `isThrow = true`
4. Exception propagates, rejecting the outer promise

## Await Resumption

The `SetupAwaitResume` method sets up Promise-based resumption:

```csharp
private void SetupAwaitResume(JSValue awaitValue)
{
    // 1. Wrap value in Promise.resolve()
    var resolvedPromise = Promise.resolve(awaitValue);
    
    // 2. Create callbacks that resume execution
    JSValue ResolveCallback(JSValue thisVal, JSValue[] args) {
        executor.Resume(args[0], false);  // Resume with value
    }
    
    JSValue RejectCallback(JSValue thisVal, JSValue[] args) {
        executor.Resume(args[0], true);   // Resume with throw
    }
    
    // 3. Attach callbacks
    resolvedPromise.then(ResolveCallback, RejectCallback);
}
```

## Tests

The `AsyncFunctionTests.cs` file includes tests for:

### Async Function Creation
- `AsyncFunction_CanBeCreated` - Basic async function creation
- `AsyncFunction_IsAsyncFlag` - IsAsync property works correctly
- `AsyncGeneratorFunction_IsAsyncAndGenerator` - Async generator detection

### Executor Tests
- `AsyncFunctionExecutor_CanBeCreated` - Executor creation
- `AsyncFunctionExecutor_Start_ReturnsPromise` - Returns a Promise
- `AsyncFunctionExecutor_SimpleReturn_ResolvesPromise` - Simple return resolves
- `AsyncFunctionExecutor_ReturnUndef_ResolvesWithUndefined` - Undefined return

### Await Tests
- `AsyncFunctionExecutor_Await_SuspendsExecution` - Await suspends execution
- `AsyncFunctionExecutor_AwaitResolvedPromise_ResumesWithValue` - Resume with value

### Interpreter Integration
- `Interpreter_AsyncFunction_ReturnsPromise` - Integration works
- `Interpreter_AsyncFunction_NotTreatedAsGenerator` - Not a generator object
- `Interpreter_AsyncGenerator_ReturnGenerator` - Async generator handling

### Parser Tests
- `Parse_AsyncFunction_SetsAsyncKind` - Async keyword sets function kind
- `Parse_AsyncArrowFunction_SetsAsyncKind` - Async arrow functions
- `Parse_AwaitExpression_EmitsAwaitOpcode` - Await emits opcode
- `Parse_AwaitOutsideAsync_ThrowsError` - Await validation

### Error Handling
- `AsyncFunctionExecutor_Exception_RejectsPromise` - Exceptions reject

## Files Changed

### New Files
- `src/QuickJS.Core/JSAsyncFunction.cs` - Async function executor
- `tests/QuickJS.Tests/AsyncFunctionTests.cs` - Async function tests
- `docs/58-async-await.md` - This documentation

### Modified Files
- `src/QuickJS.Core/JSGenerator.cs` - Added `OpCode.Await` handling
- `src/QuickJS.Core/Interpreter.cs` - Added async function detection
- `src/QuickJS.Core/JSContext.cs` - Added `CurrentException` property

## Future Work

### Async Iterators
Async iterators (`for await...of`) are not yet implemented. This would require:
- `Symbol.asyncIterator` support
- Async generator functions (`async function*`)
- `for await...of` parsing and execution

### Top-Level Await
Module-level await is not yet supported.

## References

- [ECMAScript Async Functions](https://tc39.es/ecma262/#sec-async-function-definitions)
- [ECMAScript Await](https://tc39.es/ecma262/#sec-await)
- QuickJS source: `quickjs.c` (`js_async_function_call`, `js_async_function_resume`)
