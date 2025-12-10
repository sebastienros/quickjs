# Step 10.1: Public API Design

## Overview

QuickJS.NET provides a clean, idiomatic C# API for embedding JavaScript in .NET applications. The API is designed around three core types:

1. **JSRuntime** - The JavaScript runtime environment
2. **JSContext** - An isolated JavaScript execution context
3. **JSValue** - A JavaScript value (primitives and objects)

## Quick Start

```csharp
using QuickJS;

// Create runtime and context
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Evaluate JavaScript code
var result = context.Evaluate("1 + 2");
Console.WriteLine(result.ToInt32()); // Output: 3

// Work with variables
context.SetGlobalProperty("x", JSValue.FromInt32(42));
var x = context.GetGlobalProperty("x");
Console.WriteLine(x.ToInt32()); // Output: 42
```

## Core Types

### JSRuntime

The `JSRuntime` is the top-level container that manages shared resources across multiple contexts:

- Atom table (interned strings)
- Class registry
- Memory limits
- Stack size limits

```csharp
// Create with defaults
using var runtime = new JSRuntime();

// Configure memory limits
runtime.MemoryLimit = 100 * 1024 * 1024; // 100 MB

// Configure stack size
runtime.MaxStackSize = 512 * 1024; // 512 KB

// Create multiple contexts
using var ctx1 = runtime.CreateContext();
using var ctx2 = runtime.CreateContext();
```

### JSContext

The `JSContext` is an isolated JavaScript execution environment with its own global object and built-in objects:

```csharp
using var context = runtime.CreateContext();

// Evaluate code
var result = context.Evaluate("Math.sqrt(16)");

// Access global object
var global = context.GlobalObject;

// Set/get global properties
context.SetGlobalProperty("myValue", JSValue.FromString("Hello"));
var value = context.GetGlobalProperty("myValue");

// Register native functions
context.RegisterGlobalFunction("greet", (thisVal, args) => {
    var name = args.Length > 0 ? args[0].ToString() : "World";
    return JSValue.FromString($"Hello, {name}!");
}, 1);

// Handle exceptions
if (context.HasException)
{
    var exception = context.GetAndClearException();
    Console.WriteLine($"Error: {exception}");
}

// Run microtasks (for Promises)
context.RunMicrotasks();
```

### JSValue

`JSValue` is an immutable struct representing any JavaScript value:

```csharp
// Primitive values
var undefined = JSValue.Undefined;
var null_ = JSValue.Null;
var bool_ = JSValue.True;
var int_ = JSValue.FromInt32(42);
var double_ = JSValue.FromDouble(3.14);
var string_ = JSValue.FromString("hello");
var bigint = JSValue.FromBigInt(123456789012345L);

// Type checking
if (value.IsNumber) { /* ... */ }
if (value.IsString) { /* ... */ }
if (value.IsObject) { /* ... */ }
if (value.IsBigInt) { /* ... */ }

// Type conversion
int i = value.ToInt32();
double d = value.ToDouble();
string s = value.ToString();
bool b = value.ToBoolean();

// Object values
var obj = value.AsObject();
var arr = value.AsArray();
var func = value.AsFunction();
var bigInt = value.AsBigInt();
```

## Working with Objects

### Creating Objects

```csharp
// Empty object
var obj = new JSObject();

// Object with prototype
var proto = context.GetClassPrototype(JSClassId.Object);
var obj = new JSObject(proto);

// Set properties
obj.Set("name", JSValue.FromString("John"));
obj.Set("age", JSValue.FromInt32(30));

// Get properties
var name = obj.Get("name");
var age = obj.Get("age");

// Check/delete properties
if (obj.HasProperty("name")) { /* ... */ }
obj.DeleteProperty("age");
```

### Working with Arrays

```csharp
// Create array
var arr = new JSArray();
arr.Push(JSValue.FromInt32(1));
arr.Push(JSValue.FromInt32(2));
arr.Push(JSValue.FromInt32(3));

// Access by index
var first = arr.Get(0);
arr.Set(0, JSValue.FromInt32(10));

// Length
var length = arr.Length;

// Iterate
foreach (var item in arr)
{
    Console.WriteLine(item);
}
```

### Working with Functions

```csharp
// Get a function from global
var parseIntFunc = context.GetGlobalProperty("parseInt");
var func = (JSFunction)parseIntFunc.AsObject();

// Call the function
var result = func.Call(JSValue.Undefined, JSValue.FromString("42"));

// Create native functions
var myFunc = new JSFunction((thisVal, args) => {
    return JSValue.FromInt32(args[0].ToInt32() * 2);
}, "double", 1);

context.SetGlobalProperty("double", JSValue.FromObject(myFunc));
```

## Error Handling

### Exceptions from JavaScript

```csharp
var result = context.Evaluate("throw new Error('Something went wrong')");

if (result.IsException || context.HasException)
{
    var exception = context.GetAndClearException();
    
    if (exception.IsObject)
    {
        var errorObj = exception.AsObject();
        var message = errorObj.Get("message").ToString();
        var stack = errorObj.Get("stack").ToString();
        Console.WriteLine($"Error: {message}\nStack: {stack}");
    }
}
```

### Throwing Errors

```csharp
// From native function
JSValue MyNativeFunc(JSValue thisVal, JSValue[] args)
{
    if (args.Length == 0)
    {
        return context.ThrowTypeError("Expected at least one argument");
    }
    // ...
}
```

## Type Hierarchy

### Core Types

| Type | Description |
|------|-------------|
| `JSRuntime` | Runtime environment (sealed class, disposable) |
| `JSContext` | Execution context (sealed class, disposable) |
| `JSValue` | JavaScript value (readonly struct) |
| `JSValueType` | Value type tag (enum) |

### Object Types

| Type | Description |
|------|-------------|
| `JSObject` | Base object type |
| `JSArray` | Array object |
| `JSFunction` | Function object |
| `JSBigInt` | BigInt value |
| `JSSymbol` | Symbol value |
| `JSGenerator` | Generator object |
| `JSPromise` | Promise object |
| `JSDate` | Date object |
| `JSRegExp` | Regular expression object |
| `JSMap` | Map collection |
| `JSSet` | Set collection |

### Exception Types

| Type | Description |
|------|-------------|
| `JSException` | Base exception |
| `JSSyntaxError` | Syntax error |
| `JSTypeError` | Type error |
| `JSReferenceError` | Reference error |
| `JSRangeError` | Range error |
| `JSURIError` | URI error |
| `JSEvalError` | Eval error |

## Advanced Features

### Modules

```csharp
// Load and evaluate module
var result = context.EvaluateModule(@"
    export function greet(name) {
        return `Hello, ${name}!`;
    }
", "greeting.js");
```

### Promises

```csharp
// Create a promise that resolves
context.Evaluate(@"
    var p = new Promise((resolve, reject) => {
        resolve(42);
    });
    p.then(x => console.log(x));
");

// Run microtasks to process promise callbacks
context.RunMicrotasks();
```

### Console Output

```csharp
// Subscribe to console output
context.ConsoleOutput += (sender, e) => {
    Console.WriteLine($"[{e.Level}] {e.Message}");
};

context.Evaluate("console.log('Hello from JavaScript!')");
```

### Timers

```csharp
// Timers require the event loop
context.Evaluate("setTimeout(() => console.log('Delayed'), 1000)");

// Process pending timers
context.EventLoop.Run();
```

## Thread Safety

- `JSRuntime` is **not thread-safe**. Access from multiple threads requires synchronization.
- `JSContext` is **not thread-safe**. Each context should be used from a single thread.
- `JSValue` is **thread-safe** (immutable struct) but values containing object references should only be used with their originating context.

## Memory Management

- `JSRuntime` and `JSContext` implement `IDisposable` and should be disposed when no longer needed.
- `JSValue` does not require disposal (it's a struct).
- Object references in `JSValue` are managed by the .NET garbage collector.

```csharp
// Recommended pattern
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Use context...

// Automatic cleanup when 'using' scope ends
```

## Best Practices

1. **Dispose properly**: Always dispose `JSRuntime` and `JSContext` using `using` statements.

2. **Check for exceptions**: Always check `context.HasException` after operations that can fail.

3. **Type check before conversion**: Use `IsNumber`, `IsString`, etc. before calling `ToInt32()`, `ToString()`, etc.

4. **Run microtasks**: Call `context.RunMicrotasks()` after operations that may queue microtasks (Promises).

5. **Limit memory**: Set `runtime.MemoryLimit` for untrusted code.

6. **Handle interrupts**: For long-running scripts, implement interrupt handling.

## API Reference

See the XML documentation on each type for detailed API reference.
