# Performance Optimization Guide

This document describes the performance characteristics of QuickJS.NET and optimization strategies employed in the implementation.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Hot Paths](#hot-paths)
- [Current Optimizations](#current-optimizations)
- [Memory Layout](#memory-layout)
- [Profiling Guidelines](#profiling-guidelines)
- [Future Optimizations](#future-optimizations)
- [Benchmarking](#benchmarking)

## Architecture Overview

QuickJS.NET is a stack-based bytecode interpreter that closely mirrors the original QuickJS design:

```
┌─────────────────────────────────────────────────────────────┐
│                        JSRuntime                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                    JSContext                          │   │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐    │   │
│  │  │   Parser   │  │  Compiler  │  │Interpreter │    │   │
│  │  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘    │   │
│  │        │               │               │            │   │
│  │        v               v               v            │   │
│  │   ┌─────────────────────────────────────────┐      │   │
│  │   │           Operand Stack                  │      │   │
│  │   │  [ JSValue | JSValue | JSValue | ... ]  │      │   │
│  │   └─────────────────────────────────────────┘      │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Execution Pipeline

1. **Parsing**: JavaScript source → AST (Token stream)
2. **Compilation**: AST → Bytecode (JSFunctionDef)
3. **Execution**: Bytecode interpretation (Interpreter)

## Hot Paths

The following code paths are most frequently executed and are critical for performance:

### 1. Interpreter Loop (`Interpreter.Execute`)

The main execution loop processes bytecode instructions:

```csharp
while (pc < bytecode.Length && !HasException && !didReturn)
{
    var opcode = (OpCode)bytecode[pc++];
    switch (opcode)
    {
        case OpCode.Add: Add(); break;
        case OpCode.GetLocal: GetLocal(index); break;
        // ... ~200 opcodes
    }
}
```

**Optimization opportunities**:
- Direct threading (computed gotos - not available in C#)
- Opcode fusion (combining common sequences)
- Specialized fast paths for common patterns

### 2. Stack Operations (`Push`, `Pop`, `Peek`)

Stack operations are the most frequently called methods:

```csharp
public void Push(JSValue value)
{
    if (_stackPointer >= _maxStackSize)
    {
        ThrowStackOverflow();
        return;
    }
    _stack[_stackPointer++] = value;
}

public JSValue Pop()
{
    if (_stackPointer <= 0)
    {
        ThrowStackUnderflow();
        return JSValue.Undefined;
    }
    return _stack[--_stackPointer];
}
```

### 3. Property Access (`JSObject.Get`, `JSObject.Set`)

Property operations involve dictionary lookups and prototype chain traversal:

```csharp
public JSValue Get(string propertyName)
{
    // Own property lookup
    if (_properties.TryGetValue(propertyName, out var descriptor))
    {
        return GetValueFromDescriptor(descriptor);
    }
    
    // Prototype chain traversal
    if (_prototype != null)
    {
        return _prototype.Get(propertyName);
    }
    
    return JSValue.Undefined;
}
```

### 4. Arithmetic Operations (`Add`, `Sub`, `Mul`, `Div`)

Arithmetic operations include type checking and conversion:

```csharp
public void Add()
{
    var op2 = _stack[_stackPointer - 1];
    var op1 = _stack[_stackPointer - 2];
    _stackPointer--;

    // Fast path: both integers
    if (op1.IsInt && op2.IsInt)
    {
        long result = (long)op1.ToInt32() + op2.ToInt32();
        if (result >= int.MinValue && result <= int.MaxValue)
        {
            _stack[_stackPointer - 1] = JSValue.FromInt32((int)result);
            return;
        }
    }
    
    // Slow path: type coercion, string concatenation
    AddSlow(op1, op2);
}
```

### 5. Function Calls (`CallFunction`)

Function invocation is a complex operation:

```csharp
public JSValue CallFunction(JSValue callee, JSValue thisVal, JSValue[] args)
{
    // 1. Type check
    // 2. Extract JSFunction
    // 3. Create call frame
    // 4. Execute bytecode
    // 5. Clean up frame
}
```

## Current Optimizations

### JSValue Memory Layout

The `JSValue` struct uses explicit layout for optimal memory usage:

```csharp
[StructLayout(LayoutKind.Explicit)]
public readonly struct JSValue
{
    [FieldOffset(0)]  private readonly JSValueType _tag;    // 4 bytes
    [FieldOffset(8)]  private readonly long _int64Value;    // 8 bytes (union)
    [FieldOffset(8)]  private readonly double _float64Value; // overlapped!
    [FieldOffset(16)] private readonly object? _objectValue; // 8 bytes
}
// Total: 24 bytes per value
```

**Benefits**:
- True union for numeric values (int64/double share memory)
- No boxing for primitive types
- Stack allocation for value types

### Aggressive Inlining

Critical methods are marked for aggressive inlining:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static JSValue FromInt32(int value) => new(JSValueType.Int, value);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool IsInt => _tag == JSValueType.Int;
```

### Fast Paths for Common Operations

Arithmetic operations have specialized fast paths:

```csharp
// Fast path: integer arithmetic avoids heap allocation
if (op1.IsInt && op2.IsInt)
{
    long result = (long)op1.ToInt32() + op2.ToInt32();
    // Check overflow, stay in int if possible
}

// Fast path: string operations
if (op1.IsString || op2.IsString)
{
    // Direct concatenation, no numeric conversion
}
```

### Stack-Based Execution

The interpreter uses a pre-allocated stack array:

```csharp
private readonly JSValue[] _stack;  // Pre-allocated
private int _stackPointer;          // Simple index tracking
```

### Short Opcode Forms

Common operations have short forms that reduce bytecode size:

```csharp
// Short forms (1 byte total)
case OpCode.Push0: PushI32(0); break;
case OpCode.Push1: PushI32(1); break;
// ...
case OpCode.GetLocal0: GetLocal(0); break;
case OpCode.GetLocal1: GetLocal(1); break;

// Long forms (1 byte opcode + operand bytes)
case OpCode.PushI32: PushI32(ReadInt32()); break;
case OpCode.GetLocal: GetLocal(ReadU16()); break;
```

### Property Descriptor Caching

Property descriptors cache flags and values:

```csharp
public readonly struct PropertyDescriptor
{
    public readonly JSValue Value;
    public readonly PropertyFlags Flags;
    public readonly JSFunction? Getter;
    public readonly JSFunction? Setter;
}
```

## Memory Layout

### Object Property Storage

Properties are stored in a dictionary with indexed property optimization:

```csharp
public class JSObject
{
    // Named properties
    private readonly Dictionary<string, PropertyDescriptor> _properties;
    
    // Indexed properties (arrays) - separate storage for performance
    private Dictionary<uint, PropertyDescriptor>? _indexedProperties;
    
    // Array length tracking
    private uint _arrayLength;
}
```

### Constant Pool

Literal values are deduplicated in the constant pool:

```csharp
public class ConstantPool
{
    private readonly List<JSValue> _constants;
    private readonly Dictionary<string, int> _stringIndex;
    private readonly Dictionary<double, int> _numberIndex;
}
```

## Profiling Guidelines

### Using BenchmarkDotNet

Create benchmarks for hot paths:

```csharp
[MemoryDiagnoser]
public class InterpreterBenchmarks
{
    private JSRuntime _runtime;
    private JSContext _context;
    private JSFunctionDef _compiledArithmetic;

    [GlobalSetup]
    public void Setup()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();

        // Compile once so the benchmark measures execution, not parsing.
        _compiledArithmetic = JSEval.Compile(_runtime, "1 + 2 + 3 + 4 + 5", "<benchmark>")
            ?? throw new InvalidOperationException("Compile failed");
    }

    [Benchmark]
    public JSValue SimpleArithmetic_ExecuteOnly()
    {
        return _context.Execute(_compiledArithmetic);
    }

    [Benchmark]
    public JSFunctionDef SimpleArithmetic_CompileOnly()
    {
        return JSEval.Compile(_runtime, "1 + 2 + 3 + 4 + 5", "<benchmark>")
            ?? throw new InvalidOperationException("Compile failed");
    }

    [Benchmark]
    public JSValue PropertyAccess()
    {
        return _context.Evaluate("var obj = {a: 1, b: 2}; obj.a + obj.b");
    }

    [Benchmark]
    public JSValue FunctionCall()
    {
        return _context.Evaluate("function f(x) { return x * 2; } f(21)");
    }
}
```

### Using dotTrace / PerfView

For detailed analysis:
1. Profile CPU time in interpreter loop
2. Identify GC pressure points
3. Track allocations per operation

### Key Metrics to Monitor

| Metric | Target | Notes |
|--------|--------|-------|
| Ops/sec | High | Operations per second for simple expressions |
| Allocations | Low | Bytes allocated per operation |
| GC Pressure | Low | Gen0/Gen1/Gen2 collection frequency |
| Cache Misses | Low | L1/L2 cache miss rate |

## Future Optimizations

### 1. Inline Caching (IC)

Cache property lookup results for repeated access patterns:

```csharp
// Conceptual inline cache
public struct InlineCache
{
    public Shape ExpectedShape;      // Object shape/hidden class
    public int PropertySlot;         // Cached slot index
    public PropertyDescriptor Cached;
}

// Fast path with IC
if (obj.Shape == ic.ExpectedShape)
{
    return ic.Cached.Value;  // Direct access
}
// Slow path: update cache
```

### 2. Hidden Classes / Shapes

Track object structure for faster property access:

```csharp
public class Shape
{
    public Shape Parent;
    public string LastProperty;
    public int SlotIndex;
    public Dictionary<string, int> PropertySlots;
}
```

### 3. JIT Compilation

Generate native code for hot functions:

```csharp
// Tiered execution
if (function.CallCount > JIT_THRESHOLD)
{
    var nativeCode = JITCompile(function);
    return nativeCode.Execute(args);
}
```

### 4. Escape Analysis

Avoid heap allocation for short-lived objects:

```csharp
// Stack-allocate object that doesn't escape
var point = stackalloc Point[1];
point[0] = new Point(x, y);
```

### 5. String Interning

Deduplicate string values:

```csharp
private static readonly Dictionary<string, string> _internedStrings = new();

public static string Intern(string s)
{
    if (_internedStrings.TryGetValue(s, out var interned))
        return interned;
    _internedStrings[s] = s;
    return s;
}
```

### 6. Bytecode Optimization

Post-compilation bytecode optimization:

- **Constant folding**: `1 + 2` → `3`
- **Dead code elimination**: Remove unreachable code
- **Instruction fusion**: Combine common sequences
- **Register allocation**: Use locals instead of stack

## Benchmarking

### Running Benchmarks

```bash
cd tests/QuickJS.Benchmarks
dotnet run -c Release
```

### Example Results

```
| Method           | Mean       | Error    | Gen0   | Allocated |
|----------------- |-----------:|---------:|-------:|----------:|
| SimpleArithmetic |   2.345 µs | 0.012 µs | 0.0153 |      64 B |
| PropertyAccess   |   4.567 µs | 0.023 µs | 0.0305 |     128 B |
| FunctionCall     |   8.901 µs | 0.045 µs | 0.0610 |     256 B |
| LoopIteration    |  12.345 µs | 0.062 µs | 0.0305 |     128 B |
```

### Comparison with QuickJS (C)

Performance target: Within 5-10x of native QuickJS for interpreted code.

| Operation | QuickJS (C) | QuickJS.NET | Ratio |
|-----------|-------------|-------------|-------|
| Add int   | ~10 ns      | ~30 ns      | 3x    |
| Property  | ~50 ns      | ~200 ns     | 4x    |
| Call      | ~100 ns     | ~500 ns     | 5x    |

### Memory Comparison

| Type | QuickJS (C) | QuickJS.NET | Notes |
|------|-------------|-------------|-------|
| JSValue | 16 bytes | 24 bytes | Extra tag alignment |
| Object overhead | ~32 bytes | ~56 bytes | CLR object header |
| String | Length-prefixed | .NET String | Interned strings help |

## Best Practices

### For Library Users

1. **Reuse Contexts**: Create contexts once, reuse for multiple evaluations
2. **Batch Operations**: Combine multiple operations in single `Evaluate` calls
3. **Use Native Functions**: For performance-critical code, implement in C#
4. **Minimize Interop**: Reduce boundary crossings between C# and JS
5. **Avoid eval()**: Prefer pre-compiled functions

### For Contributors

1. **Profile Before Optimizing**: Use real workloads to identify bottlenecks
2. **Benchmark Changes**: Always measure impact of optimizations
3. **Consider Memory**: GC pressure often matters more than raw CPU time
4. **Test Edge Cases**: Optimizations must handle all cases correctly
5. **Document Trade-offs**: Explain why optimization choices were made

## See Also

- [Interpreter Loop](26-interpreter-loop.md)
- [Runtime Architecture](25-runtime-architecture.md)
- [Value Types](02-value-types.md)
