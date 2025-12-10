# In Parameter Optimization for JSValue

This document describes the `in` parameter optimization applied to internal methods that accept `JSValue` parameters.

## Background

The `JSValue` struct is a 24-byte `readonly struct` with explicit memory layout:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 24)]
public readonly struct JSValue
{
    [FieldOffset(0)]  public readonly JSValueType Tag;      // 4 bytes + 4 padding
    [FieldOffset(8)]  public readonly long IntValue;        // 8 bytes (union with double)
    [FieldOffset(8)]  public readonly double FloatValue;    // 8 bytes (union with long)
    [FieldOffset(16)] public readonly object? ObjectValue;  // 8 bytes (reference)
}
```

When passing `JSValue` by value, the entire 24 bytes must be copied. For hot paths in the interpreter, this can add up to significant overhead.

## The `in` Modifier

C# 7.2 introduced the `in` parameter modifier, which passes arguments by readonly reference:

```csharp
// Before: 24-byte copy
private void Process(JSValue value) { ... }

// After: 8-byte reference, no copy
private void Process(in JSValue value) { ... }
```

### Benefits

1. **No Copy Overhead**: The caller passes a reference instead of copying 24 bytes
2. **No Defensive Copies**: Because `JSValue` is a `readonly struct`, the compiler doesn't need defensive copies
3. **Transparent Call Sites**: Unlike `ref`, the `in` modifier doesn't require explicit syntax at call sites

### When to Use

| Scenario | Recommendation |
|----------|----------------|
| Internal/private methods | ✅ Use `in` |
| Hot paths (interpreter loop) | ✅ Use `in` |
| Public API methods | ❌ Keep by-value for simplicity |
| Delegate signatures | ❌ Must match expected signature |
| Methods that store/return the value | ✅ Use `in` (still works) |

## Applied Changes

The `in` modifier was applied to internal/private methods in the following files:

### QuickJS.Core

| File | Methods |
|------|---------|
| `JSValueConversion.cs` | `ToBoolean`, `ToNumber`, `ToInteger`, `ToInt32`, `ToUInt32`, `ToUInt16`, `ToString`, `TypeOf`, `IsCallable`, `IsConstructor`, `SameValue`, `SameValueZero`, `StrictEquals` |
| `Interpreter.cs` | `Push`, `AddSlow`, `GetTypeOfString`, `AbstractRelationalComparison`, `AbstractEqualityComparison`, `StrictEqualityComparison`, `GetPropertyValue`, `SetPropertyValue`, `GetElementValue`, `SetElementValue`, `ToPrimitive` |
| `JSAsyncFunction.cs` | `Resume`, `SetupAwaitResume`, `CallResolve`, `CallReject` |
| `JSFunction.cs` | `SetClosureValue`, `Bind` |
| `JSGenerator.cs` | `Resume`, `ExecuteGenerator`, `ExecuteGeneratorBytecode`, `PushStack`, `AbstractEquality`, `CreateIteratorResult` |
| `JSArray.cs` | `SameValueZero` |
| `JSContext.cs` | `ToJsonCompatible` |
| `JSConsole.cs` | `FormatValue` |

### QuickJS.Cli

| File | Methods |
|------|---------|
| `Program.cs` | `FormatValue` |
| `Repl.cs` | `PrintResult`, `FormatValue` |

## What Was NOT Changed

1. **Public API methods**: Methods like `JSContext.Eval()`, `JSObject.Set()`, etc. remain by-value for caller convenience
2. **Delegate-bound methods**: Console callback methods (`ConsoleLog`, `ConsoleError`, etc.) must match the `JSNativeFunction` delegate signature
3. **Test code**: Test files were excluded as the optimization provides minimal benefit there

## Performance Impact

For a 24-byte struct like `JSValue`:
- **By value**: Copies 24 bytes per call
- **By `in` reference**: Passes 8-byte pointer, no copy

In tight loops like the interpreter's bytecode execution, this can reduce memory bandwidth and improve cache efficiency.

## References

- [C# `in` parameter modifier](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/in-parameter-modifier)
- [Readonly structs](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct)
- [Performance guidelines for value types](https://docs.microsoft.com/en-us/dotnet/csharp/write-safe-efficient-code)

## Static Members with `ref readonly` Returns

The commonly-used static `JSValue` members are exposed via `ref readonly` returns to avoid copying:

```csharp
// Backing fields
private static readonly JSValue s_undefined = new(JSValueType.Undefined);
private static readonly JSValue s_null = new(JSValueType.Null);
private static readonly JSValue s_true = new(JSValueType.Bool, 1);
private static readonly JSValue s_false = new(JSValueType.Bool, 0);
private static readonly JSValue s_exception = new(JSValueType.Exception);
private static readonly JSValue s_uninitialized = new(JSValueType.Uninitialized);

// ref readonly properties
public static ref readonly JSValue Undefined => ref s_undefined;
public static ref readonly JSValue Null => ref s_null;
public static ref readonly JSValue True => ref s_true;
public static ref readonly JSValue False => ref s_false;
public static ref readonly JSValue Exception => ref s_exception;
internal static ref readonly JSValue Uninitialized => ref s_uninitialized;
```

### Benefits

When code accesses `JSValue.Undefined`, it receives a reference to the static field rather than a 24-byte copy. This is particularly beneficial in tight loops where these sentinel values are frequently compared or returned.

### Usage

The `ref readonly` return is transparent to most callers:

```csharp
// These all work - implicit copy when needed
JSValue val = JSValue.Undefined;
Push(JSValue.Undefined);

// Can also capture by reference for zero-copy comparison
ref readonly JSValue undef = ref JSValue.Undefined;
if (ReferenceEquals(in value, in undef)) { ... }
```

## Small Integer and String Caching

To reduce allocations for frequently-used values, `JSValue` caches:

### Small Integer Cache (0-255)

```csharp
private const int SmallIntCacheMin = 0;
private const int SmallIntCacheMax = 255;
private static readonly JSValue[] s_smallIntCache = CreateSmallIntCache();

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static JSValue FromInt32(int value)
{
    // Return cached value for small integers (0-255)
    if ((uint)value <= SmallIntCacheMax)
    {
        return s_smallIntCache[value];
    }
    return new JSValue(JSValueType.Int, value);
}
```

The range 0-255 covers:
- Array indices in typical use cases
- Loop counters
- Small numeric constants
- Character codes (ASCII range)

### Empty String Cache

```csharp
private static readonly JSValue s_emptyString = new(JSValueType.String, string.Empty);

public static ref readonly JSValue EmptyString => ref s_emptyString;

public static JSValue FromString(string value)
{
    if (value.Length == 0)
    {
        return s_emptyString;
    }
    return new JSValue(JSValueType.String, value);
}
```

### Convenience Properties

For commonly used values, direct properties are provided:

```csharp
public static ref readonly JSValue Zero => ref s_smallIntCache[0];
public static ref readonly JSValue One => ref s_smallIntCache[1];
public static ref readonly JSValue EmptyString => ref s_emptyString;
```

### Performance Impact

- **Small integers**: No new `JSValue` struct allocation for values 0-255
- **Empty string**: Single cached instance reused across all empty string conversions
- **Memory**: ~6KB static memory for the integer cache (256 × 24 bytes)
