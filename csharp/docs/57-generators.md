# Step 9.2: Generators

This document describes the implementation of JavaScript generator functions in the QuickJS C# port.

## Overview

Generator functions are special functions that can be paused and resumed, allowing them to produce a sequence of values lazily. They use the `function*` syntax and the `yield` keyword to produce values.

```javascript
function* counter() {
    yield 1;
    yield 2;
    yield 3;
}

const gen = counter();
console.log(gen.next()); // { value: 1, done: false }
console.log(gen.next()); // { value: 2, done: false }
console.log(gen.next()); // { value: 3, done: false }
console.log(gen.next()); // { value: undefined, done: true }
```

## Implementation Architecture

The generator implementation follows the QuickJS C architecture with the following components:

### GeneratorState Enum

```csharp
public enum GeneratorState
{
    SuspendedStart = 0,   // Created but next() not yet called
    SuspendedYield = 1,   // Paused at a yield expression
    SuspendedYieldStar = 2, // Paused at a yield* expression
    Executing = 3,        // Currently running
    Completed = 4         // Finished execution
}
```

### GeneratorFunctionState Class

Stores the execution state of a generator between calls:

```csharp
public class GeneratorFunctionState
{
    public JSFunctionDef FunctionDef { get; }
    public JSValue ThisValue { get; set; }
    public JSValue[] Args { get; }
    public JSValue[] Locals { get; }
    public int PC { get; set; }
    public Stack<JSValue> Stack { get; }
    public JSVarRef[]? VarRefs { get; set; }
    public bool ThrowFlag { get; set; }
}
```

Key fields:
- **PC**: Program counter - position in bytecode where execution will resume
- **Stack**: Operand stack state when yield occurred
- **Locals**: Local variable values
- **Args**: Function arguments
- **ThisValue**: The `this` binding
- **VarRefs**: Closure variable references
- **ThrowFlag**: Set when resuming with throw()

### JSGenerator Class

The main generator object that implements the iterator protocol:

```csharp
public class JSGenerator : JSObject
{
    public GeneratorState State { get; private set; }
    public GeneratorFunctionState FunctionState { get; }
    
    public JSValue Next(JSValue value);   // Resume with value
    public JSValue Return(JSValue value); // Complete with value
    public JSValue Throw(JSValue value);  // Throw into generator
}
```

## State Machine

```
                    ┌─────────────────────┐
                    │   SuspendedStart    │
                    │   (after creation)  │
                    └─────────┬───────────┘
                              │
                    next()    │   return() / throw()
                              ▼
              ┌───────────────────────────────┐
              │         Executing             │
              │   (running bytecode)          │
              └───────────┬───────────────────┘
                          │
           ┌──────────────┴──────────────┐
           │                             │
     yield │                       return│ / end
           ▼                             ▼
┌────────────────────┐         ┌─────────────────┐
│   SuspendedYield   │         │    Completed    │
│  (paused at yield) │         │   (finished)    │
└────────────────────┘         └─────────────────┘
           │                             ▲
     next()│                             │
           └─────────────────────────────┘
```

## Iterator Protocol

Generators implement the ES6 iterator protocol by providing:

### next(value)

Resumes execution. If `value` is provided, it becomes the result of the `yield` expression:

```javascript
function* echo() {
    const received = yield 'first';
    yield received;
}

const gen = echo();
gen.next();           // { value: 'first', done: false }
gen.next('hello');    // { value: 'hello', done: false }
```

### return(value)

Completes the generator immediately with the given value:

```javascript
function* gen() {
    yield 1;
    yield 2;
}

const g = gen();
g.next();           // { value: 1, done: false }
g.return('early');  // { value: 'early', done: true }
g.next();           // { value: undefined, done: true }
```

### throw(error)

Throws an error at the current yield point:

```javascript
function* gen() {
    try {
        yield 1;
    } catch (e) {
        yield 'caught: ' + e;
    }
}

const g = gen();
g.next();              // { value: 1, done: false }
g.throw('oops');       // { value: 'caught: oops', done: false }
```

## Bytecode Integration

### InitialYield Opcode

Generator functions emit an `InitialYield` at the start of their body. This immediately suspends the generator after creation, before any user code runs.

### Yield Opcode

The `Yield` opcode:
1. Creates an iterator result `{ value: <top of stack>, done: false }`
2. Saves the current PC, stack, and locals to `GeneratorFunctionState`
3. Returns the iterator result

When `next()` is called:
1. The passed value replaces the yielded value on the stack
2. Execution resumes from the saved PC

### YieldStar Opcode

The `YieldStar` opcode implements `yield* iterable`:
1. Gets an iterator from the operand
2. Delegates to the iterator's `next()`, `return()`, and `throw()` methods
3. Yields each value from the inner iterator
4. Returns the inner iterator's return value

## Generator Function Detection

In the interpreter, when calling a function:

```csharp
// In Interpreter.CallFunction
if (func.IsGenerator && !isConstructor)
{
    return JSValue.FromObject(new JSGenerator(context, func, thisVal, args));
}
```

The `IsGenerator` property checks `FunctionDef.FuncKind == JSFunctionKind.Generator`.

## Generator Prototype

The generator prototype is initialized with native functions:

```csharp
public static void InitializeGeneratorPrototype(JSContext context, JSObject proto)
{
    proto.DefineNativeFunction(context, "next", 1, NextImpl);
    proto.DefineNativeFunction(context, "return", 1, ReturnImpl);
    proto.DefineNativeFunction(context, "throw", 1, ThrowImpl);
    
    // Symbol.toStringTag
    proto.DefineProperty(context.GetSymbol("toStringTag"),
        JSValue.FromString("Generator"),
        JSPropertyFlags.Configurable);
    
    // Symbol.iterator - returns this
    proto.DefineNativeFunction(context, context.GetSymbol("iterator"), 0,
        (ctx, thisVal, args) => thisVal);
}
```

## Execution Model

The `ExecuteGenerator` method is a simplified bytecode interpreter that handles:

1. **Stack operations**: Push, pop, dup, drop
2. **Constants**: Numbers, strings, objects
3. **Local variables**: Get/set locals and args
4. **Control flow**: Jumps, conditionals
5. **Function calls**: Via the main interpreter
6. **Yield/Return**: State saving and result creation

When a yield is encountered:
1. Save current state (PC, stack, locals)
2. Return `{ value, done: false }`
3. Set state to `SuspendedYield`

When resumed:
1. Restore state
2. Push the value passed to `next()` onto the stack
3. Continue execution from saved PC

## QuickJS C Reference

The implementation follows patterns from QuickJS:

```c
// From quickjs.c
typedef struct JSAsyncFunctionState {
    JSValue this_val;
    int argc;
    BOOL throw_flag;
    BOOL is_completed;
    JSAsyncFunctionFrame frame;
} JSAsyncFunctionState;

typedef struct JSGeneratorData {
    JSGeneratorStateEnum state;
    JSAsyncFunctionState func_state;
} JSGeneratorData;
```

The C implementation uses `JS_FUNC_RET_YIELD` to signal yield from the interpreter loop.

## Testing

The implementation includes comprehensive tests in `GeneratorTests.cs`:

- Generator creation and state initialization
- Generator prototype methods (next, return, throw)
- Generator state transitions
- Multiple yields
- Iterator result format
- Completed generator behavior

## Files

- `src/QuickJS.Core/JSGenerator.cs` - Generator implementation
- `src/QuickJS.Core/JSContext.cs` - Generator prototype initialization
- `src/QuickJS.Core/Interpreter.cs` - Generator function detection
- `tests/QuickJS.Tests/GeneratorTests.cs` - Unit tests

## References

- [ECMAScript 2024 - Generator Objects](https://tc39.es/ecma262/#sec-generator-objects)
- [MDN - Iterators and Generators](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Iterators_and_Generators)
- QuickJS source: `quickjs.c` (JSGeneratorData, js_generator_next, async_func_*)
