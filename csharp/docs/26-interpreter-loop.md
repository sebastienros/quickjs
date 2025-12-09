# Step 6.2: Interpreter Loop - Basics

## Overview

This step implements the basic bytecode interpreter for the QuickJS C# port, including
the core execution loop, stack operations, arithmetic operations, comparison operations,
and JavaScript type conversion utilities.

## QuickJS Reference

The interpreter is based on the `JS_CallInternal` function in QuickJS `quickjs.c` 
(lines 17356-19850). This function is the heart of the JavaScript engine, executing
bytecode through a main dispatch loop.

### Architecture

QuickJS uses a stack-based virtual machine with:
- **Value Stack**: Holds operands and intermediate results
- **Program Counter (PC)**: Points to current bytecode instruction
- **Bytecode Buffer**: Contains compiled JavaScript as opcodes and operands
- **Call Stack**: Manages function call frames (implemented separately)

## Files Created

### `src/QuickJS.Core/Interpreter.cs`

The main interpreter class with ~1300 lines implementing:

```csharp
public sealed class Interpreter
{
    private readonly JSContext _context;
    private JSValue[] _stack;
    private int _stackTop;
    private byte[] _bytecode;
    private int _pc;

    public JSValue Execute(JSFunctionDef function);
}
```

#### Stack Operations

| Opcode | Description |
|--------|-------------|
| `OP_push_i32` | Push 32-bit integer |
| `OP_push_const` | Push constant from pool |
| `OP_push_minus1` to `OP_push_7` | Push small integers (-1 to 7) |
| `OP_dup` | Duplicate top of stack |
| `OP_drop` | Discard top of stack |
| `OP_nip` | Remove second item (swap + drop) |
| `OP_swap` | Swap top two values |
| `OP_rot3l` | Rotate 3 left |
| `OP_rot3r` | Rotate 3 right |

#### Arithmetic Operations

| Opcode | Operation | Description |
|--------|-----------|-------------|
| `OP_add` | a + b | Addition (with string concat) |
| `OP_sub` | a - b | Subtraction |
| `OP_mul` | a * b | Multiplication |
| `OP_div` | a / b | Division |
| `OP_mod` | a % b | Modulo |
| `OP_pow` | a ** b | Exponentiation |
| `OP_neg` | -a | Negation |
| `OP_plus` | +a | Unary plus (ToNumber) |
| `OP_inc` | a + 1 | Increment |
| `OP_dec` | a - 1 | Decrement |

#### Bitwise Operations

| Opcode | Operation | Description |
|--------|-----------|-------------|
| `OP_and` | a & b | Bitwise AND |
| `OP_or` | a \| b | Bitwise OR |
| `OP_xor` | a ^ b | Bitwise XOR |
| `OP_not` | ~a | Bitwise NOT |
| `OP_shl` | a << b | Left shift |
| `OP_sar` | a >> b | Arithmetic right shift |
| `OP_shr` | a >>> b | Logical right shift |

#### Comparison Operations

| Opcode | Operation | Description |
|--------|-----------|-------------|
| `OP_lt` | a < b | Less than |
| `OP_lte` | a <= b | Less than or equal |
| `OP_gt` | a > b | Greater than |
| `OP_gte` | a >= b | Greater than or equal |
| `OP_eq` | a == b | Abstract equality |
| `OP_neq` | a != b | Abstract inequality |
| `OP_strict_eq` | a === b | Strict equality |
| `OP_strict_neq` | a !== b | Strict inequality |

### `src/QuickJS.Core/JSValueConversion.cs`

JavaScript type conversion utilities (~517 lines):

```csharp
public static class JSValueConversion
{
    public static double ToNumber(JSValue value);
    public static string ToStringValue(JSValue value);
    public static JSValue ToPrimitive(JSValue value, ToPrimitiveHint? hint = null);
    public static bool ToBoolean(JSValue value);
    public static int ToInt32(JSValue value);
    public static uint ToUInt32(JSValue value);
    public static bool StrictEquals(JSValue x, JSValue y);
    public static bool AbstractEquals(JSValue x, JSValue y);
    public static bool SameValue(JSValue x, JSValue y);
    public static bool SameValueZero(JSValue x, JSValue y);
}
```

#### ToNumber Conversion

Follows ECMAScript specification for type coercion to numbers:

| Type | Result |
|------|--------|
| Undefined | NaN |
| Null | 0 |
| Boolean | true → 1, false → 0 |
| Number | unchanged |
| String | parsed as number or NaN |
| Object | ToPrimitive(hint:number) then ToNumber |

#### ToString Conversion

| Type | Result |
|------|--------|
| Undefined | "undefined" |
| Null | "null" |
| Boolean | "true" or "false" |
| Number | number formatted as string |
| String | unchanged |
| Object | ToPrimitive(hint:string) then ToString |

#### Equality Semantics

**Strict Equality (===)**
- Types must match
- NaN !== NaN
- +0 === -0

**Abstract Equality (==)**  
- Coerces types before comparing
- null == undefined
- String/Number coercion
- Boolean → Number coercion

**SameValue**
- Used for Object.is()
- NaN is SameValue to NaN
- +0 is NOT SameValue to -0

**SameValueZero**
- Used for Map/Set comparison
- NaN is SameValueZero to NaN
- +0 IS SameValueZero to -0

## Modified Files

### `src/QuickJS.Core/JSValue.cs`

Added helper properties for boolean conversion:

```csharp
/// <summary>
/// Returns true if this value is truthy (not false, 0, "", null, undefined, NaN).
/// </summary>
public bool IsTrue => JSValueConversion.ToBoolean(this);

/// <summary>
/// Returns true if this value is falsy (false, 0, "", null, undefined, NaN).
/// </summary>
public bool IsFalse => !IsTrue;
```

Fixed negative zero handling in `FromDouble`:

```csharp
// -0 must be preserved as a double (not optimized to int 0)
if (value >= int.MinValue && value <= int.MaxValue)
{
    int intVal = (int)value;
    if ((double)intVal == value && !IsNegativeZero(value))
    {
        return new JSValue(JSValueType.Int, intVal);
    }
}

private static bool IsNegativeZero(double value)
{
    return value == 0.0 && 1.0 / value < 0;
}
```

## Test Files

### `tests/QuickJS.Tests/InterpreterTests.cs`

~179 tests covering:
- Stack operations (Push, Pop, Peek, Dup, Drop, etc.)
- Arithmetic operations (Add, Sub, Mul, Div, Mod, Pow, Neg)
- Bitwise operations (And, Or, Xor, Not, Shl, Sar, Shr)
- Comparison operations (Lt, Lte, Gt, Gte, Eq, Neq)
- Edge cases (NaN, Infinity, -0, type coercion)

### `tests/QuickJS.Tests/JSValueConversionTests.cs`

~179 tests covering:
- ToNumber for all types
- ToString for all types
- ToPrimitive with hints
- ToBoolean truthiness rules
- ToInt32 and ToUInt32 bit truncation
- All four equality semantics
- Edge cases (empty strings, whitespace, NaN)

## Key Design Decisions

### 1. Stack-Based VM

We follow QuickJS's stack-based architecture where:
- Operands are pushed onto the stack
- Operations pop operands and push results
- This simplifies bytecode and matches JavaScript semantics

### 2. Integer Optimization

When possible, numeric values are stored as Int32 rather than Float64:
- Faster integer arithmetic
- Better memory usage
- Matches QuickJS behavior

Exception: Negative zero must remain as Float64 to preserve JavaScript semantics.

### 3. Type Coercion in Operators

JavaScript operators perform implicit type coercion:
- `+` with strings performs concatenation
- Other operators convert to numbers via ToNumber
- Comparisons follow ECMAScript comparison rules

### 4. Separation of Concerns

- `Interpreter.cs` handles opcode dispatch and execution
- `JSValueConversion.cs` handles type conversions
- This separation makes the code more testable and maintainable

## JavaScript Semantics Preserved

### Negative Zero (-0)

JavaScript distinguishes between +0 and -0:

```javascript
1 / 0    // Infinity
1 / -0   // -Infinity
Object.is(0, -0)  // false
0 === -0  // true
```

Our implementation preserves -0 correctly.

### NaN Semantics

```javascript
NaN === NaN  // false
Object.is(NaN, NaN)  // true
isNaN(NaN)  // true
```

### Loose vs Strict Equality

```javascript
1 == "1"   // true (type coercion)
1 === "1"  // false (no coercion)
null == undefined  // true
null === undefined  // false
```

## Test Results

```
total: 3510
failed: 0
succeeded: 3510
```

~358 new tests added in this step.

## Next Steps

Step 6.3 will add:
- Control flow opcodes (jumps, conditionals)
- Function call opcodes
- Variable access opcodes
- Object and array opcodes

## References

- QuickJS `quickjs.c` lines 17356-19850 (`JS_CallInternal`)
- ECMAScript Specification § 7.1 (Type Conversion)
- ECMAScript Specification § 7.2 (Testing and Comparison)
