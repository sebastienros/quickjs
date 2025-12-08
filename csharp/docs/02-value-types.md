# Step 1.2 & 1.3: JavaScript Value Types

This document explains how JavaScript values are represented in QuickJS and how we implement the type system in C#.

## JavaScript's Dynamic Type System

JavaScript is a dynamically typed language, meaning variables don't have fixed types - the values they hold do. A single variable can hold a number, then a string, then an object during its lifetime.

```javascript
let x = 42;        // x holds a number
x = "hello";       // now x holds a string
x = { a: 1 };      // now x holds an object
```

This flexibility requires the engine to track the type of each value at runtime.

## How QuickJS Represents Values

In the C implementation, QuickJS uses a **tagged union** approach. Each `JSValue` contains:

1. A **tag** indicating the type (stored as an integer)
2. A **payload** containing the actual value data

From `quickjs.h`:

```c
enum {
    /* all tags with a reference count are negative */
    JS_TAG_FIRST       = -9, /* first negative tag */
    JS_TAG_BIG_INT     = -9,
    JS_TAG_SYMBOL      = -8,
    JS_TAG_STRING      = -7,
    JS_TAG_STRING_ROPE = -6,
    JS_TAG_MODULE      = -3, /* used internally */
    JS_TAG_FUNCTION_BYTECODE = -2, /* used internally */
    JS_TAG_OBJECT      = -1,

    JS_TAG_INT         = 0,
    JS_TAG_BOOL        = 1,
    JS_TAG_NULL        = 2,
    JS_TAG_UNDEFINED   = 3,
    JS_TAG_UNINITIALIZED = 4,
    JS_TAG_CATCH_OFFSET = 5,
    JS_TAG_EXCEPTION   = 6,
    JS_TAG_SHORT_BIG_INT = 7,
    JS_TAG_FLOAT64     = 8,
};
```

### Design Insight: Negative vs. Positive Tags

Notice that QuickJS divides tags into two groups:

- **Negative tags** (-9 to -1): Reference-counted heap objects
- **Non-negative tags** (0 to 8): Immediate values

This is a deliberate optimization:
- **Immediate values** can be stored directly in the `JSValue` without heap allocation
- **Heap objects** require memory allocation and reference counting

In C#, we don't need manual reference counting (the GC handles it), but we preserve this structure for compatibility and understanding.

## C# Implementation: JSValueType Enum

We implement the tags as a C# enum:

```csharp
public enum JSValueType
{
    // Heap-allocated types (negative values)
    BigInt = -9,
    Symbol = -8,
    String = -7,
    StringRope = -6,
    Module = -3,
    FunctionBytecode = -2,
    Object = -1,

    // Immediate types (non-negative values)
    Int = 0,
    Bool = 1,
    Null = 2,
    Undefined = 3,
    Uninitialized = 4,
    CatchOffset = 5,
    Exception = 6,
    ShortBigInt = 7,
    Float64 = 8,
}
```

### Extension Methods

We provide extension methods for common type checks:

```csharp
public static bool IsHeapAllocated(this JSValueType type)
    => (int)type < 0;

public static bool IsImmediate(this JSValueType type)
    => (int)type >= 0;

public static bool IsNumber(this JSValueType type)
    => type is JSValueType.Int or JSValueType.Float64 
            or JSValueType.BigInt or JSValueType.ShortBigInt;
```

## Type Categories Explained

### Primitive Types

| Type | Description | JavaScript Example |
|------|-------------|-------------------|
| `Undefined` | No value assigned | `undefined` |
| `Null` | Intentional absence of value | `null` |
| `Bool` | Boolean true/false | `true`, `false` |
| `Int` | 32-bit signed integer | `42`, `-17` |
| `Float64` | 64-bit floating point | `3.14`, `Infinity`, `NaN` |
| `String` | UTF-16 text | `"hello"` |
| `Symbol` | Unique identifier | `Symbol("id")` |
| `BigInt` | Arbitrary precision integer | `9007199254740993n` |

### Object Types

The `Object` tag covers all JavaScript objects:
- Plain objects: `{ a: 1, b: 2 }`
- Arrays: `[1, 2, 3]`
- Functions: `function() {}`
- Built-in objects: `Date`, `RegExp`, `Map`, `Set`, etc.

### Internal Types

Some types are used internally by the engine:

| Type | Purpose |
|------|---------|
| `Module` | ES module metadata |
| `FunctionBytecode` | Compiled function code |
| `Uninitialized` | Temporal dead zone marker |
| `CatchOffset` | Exception handler offset |
| `Exception` | Exception pending marker |
| `StringRope` | Efficient string concatenation |
| `ShortBigInt` | Optimized small BigInt |

## Why Not Use Inheritance?

You might wonder why we use an enum + struct instead of an object hierarchy like:

```csharp
abstract class JSValue { }
class JSNumber : JSValue { }
class JSString : JSValue { }
// etc.
```

Reasons for the tagged approach:

1. **Performance**: Structs avoid heap allocation for primitive values
2. **Memory efficiency**: A small struct (16 bytes) vs. object overhead (24+ bytes)
3. **Compatibility**: Matches the original C implementation's design
4. **Value semantics**: JavaScript primitives are values, not references

---

## Part 2: JSValue Struct (Step 1.3)

The `JSValue` struct is the core type that holds any JavaScript value. It combines:
- A type tag (`JSValueType`)
- A numeric payload (for int32, double, bool)
- An object reference (for strings, objects, etc.)

### Struct Layout

```csharp
public readonly struct JSValue : IEquatable<JSValue>
{
    private readonly JSValueType _tag;
    private readonly long _numericValue;   // Holds int32, bool, or double (via BitConverter)
    private readonly object? _objectValue; // Holds strings, objects, etc.
    
    // ... implementation
}
```

### Factory Methods

JSValue uses static factory methods instead of constructors for clarity:

```csharp
// Singleton-like constants
JSValue.Undefined     // The undefined value
JSValue.Null          // The null value
JSValue.True          // Boolean true
JSValue.False         // Boolean false
JSValue.Exception     // Indicates an exception was thrown

// Factory methods
JSValue.FromBoolean(true)      // Create from bool
JSValue.FromInt32(42)          // Create from int
JSValue.FromDouble(3.14)       // Create from double
JSValue.FromString("hello")    // Create from string
```

### Type Optimization

Following QuickJS's design, `FromDouble` optimizes whole numbers:

```csharp
public static JSValue FromDouble(double value)
{
    // If the double is actually an integer that fits in int32, store as int
    if (value >= int.MinValue && value <= int.MaxValue)
    {
        int intVal = (int)value;
        if ((double)intVal == value)
            return new JSValue(JSValueType.Int, intVal);
    }
    return new JSValue(value);
}
```

This means `FromDouble(42.0)` returns an `Int`-tagged value, saving memory and enabling faster integer operations.

### Type Checking Properties

```csharp
value.IsUndefined       // true if undefined
value.IsNull            // true if null
value.IsNullOrUndefined // true if null or undefined
value.IsBool            // true if boolean
value.IsNumber          // true if int or float64
value.IsInt             // true if int32
value.IsString          // true if string
value.IsObject          // true if object
value.IsException       // true if exception marker
```

### Conversion Methods

JSValue provides both throwing and try-pattern conversions:

```csharp
// Try-pattern (safe)
if (value.TryGetInt32(out int i)) { /* use i */ }
if (value.TryGetDouble(out double d)) { /* use d */ }
if (value.TryGetString(out string? s)) { /* use s */ }

// Throwing (when you know the type)
int i = value.ToInt32();      // throws if not convertible
double d = value.ToDouble();  // throws if not convertible
```

### JavaScript Coercion: ToBoolean

The `ToBoolean()` method follows JavaScript's falsy value rules:

```csharp
// These are falsy in JavaScript:
JSValue.Undefined.ToBoolean()           // false
JSValue.Null.ToBoolean()                // false
JSValue.False.ToBoolean()               // false
JSValue.FromInt32(0).ToBoolean()        // false
JSValue.FromDouble(0.0).ToBoolean()     // false
JSValue.FromDouble(double.NaN).ToBoolean() // false
JSValue.FromString("").ToBoolean()      // false

// Everything else is truthy:
JSValue.True.ToBoolean()                // true
JSValue.FromInt32(42).ToBoolean()       // true
JSValue.FromString("hello").ToBoolean() // true
// Objects are always truthy (even empty objects)
```

### Implicit Conversions

For convenience, implicit conversions from C# types are supported:

```csharp
JSValue v1 = true;        // FromBoolean
JSValue v2 = 42;          // FromInt32
JSValue v3 = 3.14;        // FromDouble
JSValue v4 = "hello";     // FromString
```

### Equality

JSValue implements strict equality (like JavaScript's `===`):

```csharp
JSValue.FromInt32(42) == JSValue.FromInt32(42)    // true
JSValue.FromString("a") == JSValue.FromString("a") // true
JSValue.Null == JSValue.Undefined                   // false (different types)
```

## References

- [QuickJS source: quickjs.h](https://github.com/nicohund/quickjs/blob/master/quickjs.h)
- [ECMAScript Language Types](https://tc39.es/ecma262/#sec-ecmascript-language-types)
- [JavaScript data types](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Data_structures)
