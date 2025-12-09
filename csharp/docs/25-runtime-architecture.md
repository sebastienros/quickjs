# Step 6.1: Runtime Architecture

This document explains the runtime infrastructure for QuickJS.NET, including the `JSRuntime` and `JSContext` classes that form the foundation for JavaScript execution.

## Overview

QuickJS uses a two-level architecture for managing JavaScript execution:

```
┌─────────────────────────────────────────────────────────┐
│                      JSRuntime                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │
│  │ Atom Table  │  │Class Registry│  │ Memory Limits   │ │
│  └─────────────┘  └─────────────┘  └─────────────────┘ │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │
│  │ Job Queue   │  │ Interrupt   │  │ Stack Size      │ │
│  └─────────────┘  └─────────────┘  └─────────────────┘ │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │                   JSContext 1                     │  │
│  │  ┌─────────────┐  ┌──────────────┐              │  │
│  │  │Global Object│  │Class Protos  │              │  │
│  │  └─────────────┘  └──────────────┘              │  │
│  │  ┌─────────────┐  ┌──────────────┐              │  │
│  │  │   Modules   │  │  Exceptions  │              │  │
│  │  └─────────────┘  └──────────────┘              │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │                   JSContext 2                     │  │
│  │  ┌─────────────┐  ┌──────────────┐              │  │
│  │  │Global Object│  │Class Protos  │              │  │
│  │  └─────────────┘  └──────────────┘              │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## JSRuntime

`JSRuntime` is the top-level container for JavaScript execution. It manages resources that are shared across multiple execution contexts.

### Key Responsibilities

1. **Atom Table** - Interned strings for efficient property name lookup
2. **Class Registry** - Definitions for built-in and custom object types
3. **Memory Management** - Tracking allocations and enforcing limits
4. **Job Queue** - Promise microtask management
5. **Interrupt Handling** - Support for cancelling long-running scripts

### Creating a Runtime

```csharp
// Create a runtime with default settings
using var runtime = new JSRuntime();

// Configure memory limits
runtime.MemoryLimit = 64 * 1024 * 1024;  // 64 MB
runtime.GCThreshold = 1024 * 1024;        // Trigger GC at 1 MB

// Configure stack size (for recursion protection)
runtime.MaxStackSize = 512 * 1024;        // 512 KB
```

### Memory Management

```csharp
// Check memory usage
var stats = runtime.GetMemoryStats();
Console.WriteLine($"Memory used: {stats.MemoryUsed} bytes");
Console.WriteLine($"Allocations: {stats.AllocationCount}");

// Record allocations (for custom allocators)
if (runtime.RecordAllocation(1024))
{
    // Allocation allowed
}
else
{
    // Would exceed memory limit
}

// Force garbage collection
runtime.RunGC();
```

### Interrupt Handling

```csharp
// Request interrupt from another thread
runtime.RequestInterrupt();

// Check for interrupt in a loop (done by interpreter)
if (runtime.CheckInterrupt())
{
    // Stop execution
}

// Clear the interrupt request
runtime.ClearInterrupt();
```

### QuickJS C Comparison

The C# `JSRuntime` corresponds to QuickJS's `struct JSRuntime`:

```c
// QuickJS C
struct JSRuntime {
    JSMallocState malloc_state;       // → MemoryLimit, MemoryUsed
    uint32_t *atom_hash;              // → AtomTable
    JSClass *class_array;             // → Class registry
    struct list_head context_list;    // → List<JSContext>
    size_t stack_size;                // → MaxStackSize
    JSValue current_exception;        // → HasException, SetException
    struct list_head job_list;        // → Job queue
};
```

## JSContext

`JSContext` represents an isolated JavaScript execution environment within a runtime. Each context has its own global object and module registry, but shares the atom table and class definitions with other contexts in the same runtime.

### Key Responsibilities

1. **Global Object** - The `globalThis` / `window` object
2. **Class Prototypes** - Object.prototype, Array.prototype, etc.
3. **Module Registry** - Loaded ES modules
4. **Exception State** - Current thrown exception
5. **Random State** - For Math.random()

### Creating a Context

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Access the global object
var global = context.GlobalObject;

// Set global variables
context.SetGlobalProperty("myValue", JSValue.FromInt32(42));

// Register native functions
context.RegisterGlobalFunction("print", (thisArg, args) =>
{
    foreach (var arg in args)
        Console.Write(arg.ToString());
    Console.WriteLine();
    return JSValue.Undefined;
}, 1);
```

### Exception Handling

```csharp
// Throw a JavaScript error
context.ThrowTypeError("Expected a number");

// Check for exceptions
if (context.HasException)
{
    var error = context.GetAndClearException();
    var errorObj = error.AsObject();
    var name = errorObj.Get("name").ToString();    // "TypeError"
    var message = errorObj.Get("message").ToString(); // "Expected a number"
}

// Clear without retrieving
context.ClearException();
```

### Class Prototypes

```csharp
// Get built-in prototypes
var objectProto = context.GetClassPrototype(JSClassId.Object);
var arrayProto = context.GetClassPrototype(JSClassId.Array);

// Set custom prototype
var myProto = new JSObject();
context.SetClassPrototype(JSClassId.Number, myProto);
```

### Module Management

```csharp
// Register a module
var module = new JSModuleDef("./utils.js");
context.RegisterModule("./utils.js", module);

// Check if loaded
if (context.HasModule("./utils.js"))
{
    var loaded = context.GetModule("./utils.js");
}

// List all modules
foreach (var name in context.GetLoadedModuleNames())
{
    Console.WriteLine($"Loaded: {name}");
}
```

### QuickJS C Comparison

The C# `JSContext` corresponds to QuickJS's `struct JSContext`:

```c
// QuickJS C
struct JSContext {
    JSRuntime *rt;                    // → Runtime property
    JSValue global_obj;               // → GlobalObject
    JSValue *class_proto;             // → GetClassPrototype()
    struct list_head loaded_modules;  // → Module registry
    uint64_t random_state;            // → GetRandomValue()
};
```

## Multiple Contexts

Multiple contexts in the same runtime provide isolated JavaScript environments that efficiently share resources:

```csharp
using var runtime = new JSRuntime();

using var ctx1 = runtime.CreateContext();
using var ctx2 = runtime.CreateContext();

// Isolated global objects
ctx1.SetGlobalProperty("x", JSValue.FromInt32(1));
ctx2.SetGlobalProperty("x", JSValue.FromInt32(2));

// x == 1 in ctx1, x == 2 in ctx2

// But atoms are shared
var atom1 = ctx1.InternAtom("shared");
var atom2 = ctx2.InternAtom("shared");
// atom1.Equals(atom2) is true
```

### Use Cases for Multiple Contexts

1. **Sandboxing** - Run untrusted code in isolated contexts
2. **Web Workers** - Simulate worker threads with separate globals
3. **Module Testing** - Fresh global state for each test
4. **Multi-tenant** - Separate contexts for different users

## Call Frame Management

During execution, the interpreter tracks call frames for debugging and stack traces:

```csharp
// Create and push a call frame (done by interpreter)
var frame = new JSCallFrame(
    function: myFunction,
    functionName: "myFunction",
    fileName: "script.js",
    arguments: new JSValue[] { JSValue.FromInt32(1) }
);
frame.LineNumber = 10;
frame.ColumnNumber = 5;

context.PushCallFrame(frame);

// Get stack depth
int depth = context.GetStackDepth();  // 1

// Get stack trace
var trace = context.GetStackTrace();
foreach (var entry in trace.Frames)
{
    Console.WriteLine($"  at {entry.FunctionName} ({entry.Location})");
}

// Pop when function returns
context.PopCallFrame();
```

## Implementation Notes

### Thread Safety

- `JSRuntime` provides thread-safe memory tracking via locks
- `JSContext` is **not** thread-safe - use one context per thread
- Interrupt requests are thread-safe (volatile flag)

### Memory Tracking

The runtime tracks memory through `RecordAllocation` and `RecordDeallocation`:

```csharp
// Allocation tracking
runtime.RecordAllocation(objectSize);    // Returns false if over limit
runtime.RecordDeallocation(objectSize);  // Update when freed

// Check if GC should run
if (runtime.ShouldTriggerGC())
{
    runtime.RunGC();
}
```

### Disposal

Both `JSRuntime` and `JSContext` implement `IDisposable`:

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();
// Automatically cleaned up at end of scope

// Runtime disposal disposes all contexts
runtime.Dispose();  // Also disposes all child contexts
```

## Files Created

| File | Lines | Description |
|------|-------|-------------|
| `JSRuntime.cs` | ~810 | Runtime with memory limits, class registry, job queue |
| `JSContext.cs` | ~780 | Context with global object, prototypes, modules |
| `JSRuntimeTests.cs` | ~650 | 50 tests for runtime functionality |
| `JSContextTests.cs` | ~680 | 50 tests for context functionality |

## Next Steps

With the runtime infrastructure in place, the next steps are:

1. **Step 6.2: Bytecode Interpreter Loop** - Execute bytecode in a context
2. **Step 6.3: Stack Machine** - Implement the operand stack
3. **Step 6.4: Arithmetic Operators** - Basic math operations
4. **Step 6.5: Comparison Operators** - Equality and relational ops
5. **Step 6.6: Control Flow** - Jumps, conditionals, loops
6. **Step 6.7: Function Calls** - Native and bytecode function invocation
