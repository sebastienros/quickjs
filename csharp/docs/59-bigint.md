# BigInt Type Implementation (Step 9.4)

## Overview

This document describes the BigInt type implementation in the QuickJS.NET project. BigInt is a JavaScript primitive type that can represent integers of arbitrary precision, allowing for exact integer arithmetic beyond the safe integer range of Number.

## Current Status: Partial Implementation

The BigInt type infrastructure is in place but cannot be fully tested due to a separate issue with expression statement completion values in the eval mechanism.

## Architecture

### JSBigInt Class

The `JSBigInt` class (`src/QuickJS.Core/JSBigInt.cs`) wraps `System.Numerics.BigInteger` and provides:

```csharp
public sealed class JSBigInt : IComparable<JSBigInt>, IEquatable<JSBigInt>
{
    private readonly BigInteger _value;
    
    // Constructors
    public JSBigInt(long value)
    public JSBigInt(BigInteger value)
    
    // Arithmetic
    public static JSBigInt Add(JSBigInt left, JSBigInt right)
    public static JSBigInt Subtract(JSBigInt left, JSBigInt right)
    public static JSBigInt Multiply(JSBigInt left, JSBigInt right)
    public static JSBigInt Divide(JSBigInt left, JSBigInt right)
    public static JSBigInt Remainder(JSBigInt left, JSBigInt right)
    public static JSBigInt Power(JSBigInt baseVal, int exponent)
    public static JSBigInt Negate(JSBigInt value)
    public static JSBigInt Abs(JSBigInt value)
    
    // Bitwise
    public static JSBigInt BitwiseAnd(JSBigInt left, JSBigInt right)
    public static JSBigInt BitwiseOr(JSBigInt left, JSBigInt right)
    public static JSBigInt BitwiseXor(JSBigInt left, JSBigInt right)
    public static JSBigInt BitwiseNot(JSBigInt value)
    public static JSBigInt LeftShift(JSBigInt value, int shift)
    public static JSBigInt RightShift(JSBigInt value, int shift)
    
    // Comparison
    public int CompareTo(JSBigInt? other)
    public bool Equals(JSBigInt? other)
    
    // Conversion
    public bool TryToInt64(out long value)
    public BigInteger ToBigInteger()
    public override string ToString()
}
```

### JSValueType Extensions

Two BigInt types are defined:
- `ShortBigInt = 7` - for values that fit in a `long` (most common case)
- `BigInt = -9` - for values requiring full `BigInteger` storage

This optimization avoids heap allocation for small BigInt values.

### JSValue Methods

```csharp
// Creation
public static JSValue FromBigInt(long value)     // Creates ShortBigInt
public static JSValue FromBigInt(JSBigInt value) // Creates ShortBigInt or BigInt

// Type checking
public bool IsBigInt => _tag == JSValueType.BigInt || _tag == JSValueType.ShortBigInt;

// Conversion
public JSBigInt ToBigInt()
```

### Interpreter Operations

The interpreter handles BigInt in binary operations:

```csharp
// In AddOp(), SubOp(), MulOp(), etc.
if (a.IsBigInt || b.IsBigInt)
{
    var bigA = a.ToBigInt();
    var bigB = b.ToBigInt();
    // Perform BigInt operation
}
```

Mixed operations between BigInt and Number throw TypeError per the ECMAScript specification.

### ToBigInt Opcode

The `ToBigInt` opcode converts stack values to BigInt:

```csharp
case OpCode.ToBigInt:
    ToBigIntOp();
    return true;

public void ToBigIntOp()
{
    var op = _stack[_stackPointer - 1];
    if (op.IsBigInt) return;
    if (op.IsInt)
    {
        _stack[_stackPointer - 1] = JSValue.FromBigInt(op.ToInt32());
        return;
    }
    _context.ThrowTypeError("Cannot convert to BigInt");
}
```

### Lexer Support

BigInt literals end with the `n` suffix:

```javascript
123n        // BigInt literal
0x1ABCn     // Hexadecimal BigInt  
0o777n      // Octal BigInt
0b1010n     // Binary BigInt
```

The Lexer recognizes these patterns and stores the value as either `long` (for values fitting in 64 bits) or `BigInteger` (for larger values).

### Parser Support

The Parser emits appropriate bytecode for BigInt literals:

```csharp
private void EmitNumberLiteral()
{
    if (isBigInt)
    {
        if (value is long l64 && l64 >= int.MinValue && l64 <= int.MaxValue)
        {
            EmitOp(OpCode.PushI32);
            EmitI32((int)l64);
            EmitOp(OpCode.ToBigInt);  // Convert to BigInt
        }
        else if (value is BigInteger bigVal)
        {
            // Large BigInt - store in constant pool
            var bigInt = new JSBigInt(bigVal);
            int idx = _currentFunction.Constants.AddBigInt(bigInt);
            EmitOp(OpCode.PushConst);
            EmitU32((uint)idx);
        }
    }
}
```

## Known Issue: Expression Statement Completion Values

Testing revealed that the eval mechanism doesn't properly return expression values:

```javascript
eval("42")    // Should return 42, returns undefined
eval("123n")  // Should return BigInt(123), returns undefined
```

This is because `ParseExpressionStatement()` emits a `Drop` opcode after every expression statement, discarding the result. This is correct for statement context but wrong for eval completion values.

**Bytecode for `42`:**
```
PushI32 42
Drop        // <-- Discards the value
```

This is a fundamental issue with the parser/interpreter architecture that affects all expression evaluation, not just BigInt.

## What's Not Yet Implemented

1. **BigInt constructor** (`BigInt(value)`)
2. **BigInt prototype methods**:
   - `toString([radix])`
   - `valueOf()`
   - `toLocaleString()`
3. **BigInt static methods**:
   - `BigInt.asIntN(bits, bigint)`
   - `BigInt.asUintN(bits, bigint)`
4. **JSON.stringify** BigInt handling (should throw TypeError)
5. **Tests** (blocked by eval issue)

## Testing Strategy

Once the eval completion value issue is fixed, tests should cover:

1. **Literal parsing**: `123n`, `0x1ABCn`, `0o777n`, `0b1010n`
2. **Arithmetic**: `+`, `-`, `*`, `/`, `%`, `**`
3. **Bitwise**: `&`, `|`, `^`, `~`, `<<`, `>>`
4. **Comparison**: `<`, `>`, `<=`, `>=`, `==`, `===`, `!=`, `!==`
5. **Type mixing**: BigInt + Number should throw TypeError
6. **typeof**: `typeof 1n === "bigint"`
7. **Large numbers**: Values exceeding Number.MAX_SAFE_INTEGER

## References

- [ECMAScript BigInt Specification](https://tc39.es/ecma262/#sec-bigint-objects)
- [MDN BigInt Documentation](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/BigInt)
