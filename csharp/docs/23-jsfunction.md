# Step 5.3: JSFunction Runtime

This step implements JavaScript function objects, including bytecode functions, native (C#) functions, bound functions, and closure support through variable references.

## Overview

JavaScript functions are first-class objects - they can be assigned to variables, passed as arguments, and returned from other functions. In QuickJS, functions are implemented as special objects with additional internal slots for:

- Bytecode (for compiled JavaScript functions)
- Native function pointers (for built-in functions)
- Closure variable references (for captured variables)
- Bound function data (for `Function.prototype.bind()`)

## Key Types

### JSVarRef

Represents a reference to a captured variable in a closure:

```csharp
// Variable on the stack (not yet detached)
var stackVars = new JSValue[] { JSValue.FromInt32(10) };
var varRef = new JSVarRef(stackVars, 0, isLexical: true);
Console.WriteLine(varRef.Value.ToInt32()); // 10

// When the outer function returns, detach the value
varRef.Detach();

// The value is now stored in the var ref itself
stackVars[0] = JSValue.Undefined; // Stack is cleared
Console.WriteLine(varRef.Value.ToInt32()); // Still 10!
```

The closure lifecycle:
1. Initially, captured variables point to the stack frame
2. When the outer function returns, `Detach()` copies values off the stack
3. Inner functions continue to access values through the var ref

### JSFunction

The main function object class, supporting three types of functions:

#### Bytecode Functions

Functions compiled from JavaScript source code:

```csharp
var funcDef = new JSFunctionDef { FuncName = new JSAtom(1) };
funcDef.AddArg(new JSAtom(2));
funcDef.IsStrict = true;

var func = new JSFunction(funcDef);
Console.WriteLine(func.IsBytecodeFunction); // true
Console.WriteLine(func.Length);             // 1
Console.WriteLine(func.IsStrict);           // true
```

#### Native Functions

Functions implemented in C#:

```csharp
// Simple native function
JSCFunction add = (thisArg, args) =>
{
    int a = args.Length > 0 ? args[0].ToInt32() : 0;
    int b = args.Length > 1 ? args[1].ToInt32() : 0;
    return JSValue.FromInt32(a + b);
};

var addFunc = new JSFunction(add, "add", 2);
var result = addFunc.CallNative(JSValue.Undefined, new[]
{
    JSValue.FromInt32(3),
    JSValue.FromInt32(4)
});
Console.WriteLine(result.ToInt32()); // 7

// Native function with magic value (for overloading)
JSCFunctionMagic mathOp = (thisArg, args, magic) =>
{
    int a = args[0].ToInt32();
    int b = args[1].ToInt32();
    return magic switch
    {
        0 => JSValue.FromInt32(a + b), // ADD
        1 => JSValue.FromInt32(a - b), // SUB
        2 => JSValue.FromInt32(a * b), // MUL
        _ => JSValue.Undefined
    };
};

var mulFunc = new JSFunction(mathOp, 2, "multiply", 2); // magic = 2
```

#### Bound Functions

Functions created by `Function.prototype.bind()`:

```csharp
JSCFunction greet = (thisArg, args) =>
{
    var name = thisArg.ToString();
    var greeting = args.Length > 0 ? args[0].ToString() : "Hello";
    return JSValue.FromString($"{greeting}, {name}!");
};

var greetFunc = new JSFunction(greet, "greet", 1);

// Bind with a fixed this value
var boundGreet = greetFunc.Bind(JSValue.FromString("World"));
var result = boundGreet.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
// Result: "Hello, World!"

// Bind with this and prepended arguments
var hiGreet = greetFunc.Bind(JSValue.FromString("Alice"), JSValue.FromString("Hi"));
var result2 = hiGreet.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
// Result: "Hi, Alice!"
```

## Closures

Closures capture variables from outer scopes:

```javascript
function outer() {
    let x = 10;           // x is on the stack
    return function inner() {
        return x;          // x is captured via var ref
    };
}
let f = outer();          // outer returns, x is detached
f();                      // returns 10 via detached var ref
```

The C# implementation:

```csharp
// Create the closure
var funcDef = new JSFunctionDef();
var varRefs = new JSVarRef[]
{
    new JSVarRef(JSValue.FromInt32(10)), // Pre-detached for simplicity
};

var innerFunc = new JSFunction(funcDef, varRefs);

// Access captured variable
Console.WriteLine(innerFunc.GetClosureValue(0).ToInt32()); // 10

// Modify captured variable (if not const)
innerFunc.SetClosureValue(0, JSValue.FromInt32(20));
Console.WriteLine(innerFunc.GetClosureValue(0).ToInt32()); // 20
```

## QuickJS C Structure Reference

From `quickjs.c`:

```c
// Bytecode function data (in JSObject union)
struct { /* JS_CLASS_BYTECODE_FUNCTION */
    struct JSFunctionBytecode *function_bytecode;
    JSVarRef **var_refs;
    JSObject *home_object; /* for 'super' access */
} func;

// Native function data
struct { /* JS_CLASS_C_FUNCTION */
    JSContext *realm;
    JSCFunctionType c_function;
    uint8_t length;
    uint8_t cproto;
    int16_t magic;
} cfunc;

// Bound function data
typedef struct JSBoundFunction {
    JSValue func_obj;
    JSValue this_val;
    int argc;
    JSValue argv[0]; // Flexible array member
} JSBoundFunction;

// Variable reference for closures
typedef struct JSVarRef {
    JSGCObjectHeader header;
    JSValue *pvalue;
    union {
        JSValue value;         // When detached
        struct {
            uint16_t var_ref_idx;
            JSStackFrame *stack_frame;
        };                      // When on stack
    };
} JSVarRef;
```

## Class Hierarchy

```
JSObject (from Step 5.1)
    └── JSFunction
            - IsBytecodeFunction (JSFunctionDef)
            - IsNativeFunction (JSCFunction delegate)
            - IsBoundFunction (wraps another function)
```

## Property Summary

| Property | Description |
|----------|-------------|
| `FunctionDef` | The bytecode definition (null for native/bound) |
| `NativeFunction` | The native delegate (null for bytecode/bound) |
| `VarRefs` | Captured closure variables |
| `HomeObject` | Object for `super` references |
| `BoundTarget` | Target function (for bound functions) |
| `BoundThis` | Fixed `this` value (for bound functions) |
| `BoundArgs` | Prepended arguments (for bound functions) |
| `Name` | Function name |
| `Length` | Declared parameter count |

## Method Summary

| Method | Description |
|--------|-------------|
| `Bind(thisArg, args)` | Creates a bound function |
| `CallNative(thisArg, args)` | Invokes native function |
| `ResolveForCall(...)` | Resolves bound function chain |
| `GetVarRef(index)` | Gets a closure variable reference |
| `GetClosureValue(index)` | Gets a captured variable's value |
| `SetClosureValue(index, value)` | Sets a captured variable's value |

## Files Created/Modified

| File | Lines | Description |
|------|-------|-------------|
| `JSVarRef.cs` | ~250 | Closure variable references |
| `JSFunction.cs` | ~530 | Function object implementation |
| `JSVarRefTests.cs` | ~420 | 32 tests for var refs |
| `JSFunctionTests.cs` | ~675 | 70 tests for functions |

## Test Summary

- **JSVarRefTests**: 32 tests covering stack/detached values, const variables, cloning
- **JSFunctionTests**: 70 tests covering bytecode, native, bound functions, closures

Total new tests: 102 (Note: Actual may differ based on test counting)

## Next Steps

**Step 5.4: Array Object** will implement:
- `JSArray` class with optimized array storage
- Array methods (push, pop, slice, etc.)
- Length property with special semantics
- Sparse array support
