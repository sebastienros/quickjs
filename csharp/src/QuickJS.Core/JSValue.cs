// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript value using a tagged union representation.
/// This is the core type that mirrors QuickJS's JSValue, holding either
/// a primitive value (int32, bool, null, undefined) or a reference to
/// a heap-allocated object (string, object, symbol, bigint).
/// </summary>
/// <remarks>
/// <para>
/// QuickJS uses a tagged union representation where each value consists of:
/// - A tag (JSValueType) identifying the type
/// - A payload (union of int32, double, or pointer)
/// </para>
/// <para>
/// We use explicit struct layout to create a true union for numeric values:
/// - Bytes 0-3: Tag (JSValueType enum)
/// - Bytes 4-7: Padding for alignment
/// - Bytes 8-15: Union of int64 and double (overlapped)
/// - Bytes 16-23: Object reference for heap-allocated values
/// </para>
/// <para>
/// This struct is immutable after construction.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct JSValue : IEquatable<JSValue>
{
    // Explicit layout for true int64/double union:
    // - Offset 0: Tag (4 bytes enum)
    // - Offset 8: Numeric union (int64 overlapped with double)
    // - Offset 16: Object reference

    [FieldOffset(0)]
    private readonly JSValueType _tag;

    [FieldOffset(8)]
    private readonly long _int64Value;

    [FieldOffset(8)]  // Overlaps with _int64Value - true union!
    private readonly double _float64Value;

    [FieldOffset(16)]
    private readonly object? _objectValue;

    #region Constructors (private - use factory methods)

    private JSValue(JSValueType tag)
    {
        _int64Value = 0;
        _float64Value = 0;
        _objectValue = null;
        _tag = tag;
    }

    private JSValue(JSValueType tag, long value)
    {
        _float64Value = 0;
        _objectValue = null;
        _int64Value = value;
        _tag = tag;
    }

    private JSValue(double value)
    {
        _int64Value = 0;
        _objectValue = null;
        _float64Value = value;
        _tag = JSValueType.Float64;
    }

    private JSValue(JSValueType tag, object? obj)
    {
        _int64Value = 0;
        _float64Value = 0;
        _objectValue = obj;
        _tag = tag;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// The JavaScript <c>undefined</c> value.
    /// </summary>
    public static JSValue Undefined { get; } = new(JSValueType.Undefined);

    /// <summary>
    /// The JavaScript <c>null</c> value.
    /// </summary>
    public static JSValue Null { get; } = new(JSValueType.Null);

    /// <summary>
    /// The JavaScript <c>true</c> value.
    /// </summary>
    public static JSValue True { get; } = new(JSValueType.Bool, 1);

    /// <summary>
    /// The JavaScript <c>false</c> value.
    /// </summary>
    public static JSValue False { get; } = new(JSValueType.Bool, 0);

    /// <summary>
    /// A special value indicating an exception was thrown.
    /// </summary>
    public static JSValue Exception { get; } = new(JSValueType.Exception);

    /// <summary>
    /// A special value indicating an uninitialized variable.
    /// </summary>
    internal static JSValue Uninitialized { get; } = new(JSValueType.Uninitialized);

    /// <summary>
    /// Creates a JavaScript boolean value.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>A JSValue representing the boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromBoolean(bool value) => value ? True : False;

    /// <summary>
    /// Creates a JavaScript number from a 32-bit integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A JSValue representing the integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromInt32(int value) => new(JSValueType.Int, value);

    /// <summary>
    /// Creates a JavaScript number from a 64-bit floating-point value.
    /// </summary>
    /// <param name="value">The floating-point value.</param>
    /// <returns>A JSValue representing the number.</returns>
    /// <remarks>
    /// If the value can be exactly represented as a 32-bit integer (and fits in Int32 range),
    /// it will be stored as an integer for efficiency. Note: -0 is preserved as a double.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromDouble(double value)
    {
        // Optimization: if the double is actually an integer that fits in int32, store as int
        // This matches QuickJS behavior in JS_NewFloat64
        // Exception: -0 must be preserved as a double
        if (value >= int.MinValue && value <= int.MaxValue)
        {
            int intVal = (int)value;
            if ((double)intVal == value && !IsNegativeZero(value))
            {
                return new JSValue(JSValueType.Int, intVal);
            }
        }

        return new JSValue(value);
    }

    /// <summary>
    /// Checks if a double value is negative zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNegativeZero(double value)
    {
        // -0 has bit pattern 0x8000000000000000 (only sign bit set)
        // We check if value equals zero but has negative infinity when we divide 1 by it
        return value == 0.0 && 1.0 / value < 0;
    }

    /// <summary>
    /// Creates a JavaScript string value.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A JSValue representing the string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromString(string value)
    {
        if (value is null)
        {
            ThrowArgumentNull(nameof(value));
        }

        return new JSValue(JSValueType.String, value);
    }

    /// <summary>
    /// Creates a JavaScript object value.
    /// </summary>
    /// <param name="obj">The JSObject instance.</param>
    /// <returns>A JSValue representing the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="obj"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromObject(JSObject obj)
    {
        if (obj is null)
        {
            ThrowArgumentNull(nameof(obj));
        }

        return new JSValue(JSValueType.Object, obj);
    }

    /// <summary>
    /// Creates a JavaScript symbol value.
    /// </summary>
    /// <param name="symbol">The JSSymbol instance.</param>
    /// <returns>A JSValue representing the symbol.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="symbol"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromSymbol(JSSymbol symbol)
    {
        if (symbol is null)
        {
            ThrowArgumentNull(nameof(symbol));
        }

        return new JSValue(JSValueType.Symbol, symbol);
    }

    /// <summary>
    /// Creates a JavaScript BigInt value from a long.
    /// </summary>
    /// <param name="value">The long value.</param>
    /// <returns>A JSValue representing the BigInt.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromBigInt(long value)
    {
        return new JSValue(JSValueType.ShortBigInt, value);
    }

    /// <summary>
    /// Creates a JavaScript BigInt value from a JSBigInt.
    /// </summary>
    /// <param name="value">The JSBigInt value.</param>
    /// <returns>A JSValue representing the BigInt.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue FromBigInt(JSBigInt value)
    {
        if (value is null)
        {
            ThrowArgumentNull(nameof(value));
        }

        // Use ShortBigInt for values that fit in a long
        if (value.TryToInt64(out long longVal))
        {
            return new JSValue(JSValueType.ShortBigInt, longVal);
        }

        return new JSValue(JSValueType.BigInt, value);
    }

    #endregion

    #region Type Properties

    /// <summary>
    /// Gets the type tag of this value.
    /// </summary>
    public JSValueType Tag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is <c>undefined</c>.
    /// </summary>
    public bool IsUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Undefined;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is <c>null</c>.
    /// </summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Null;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is <c>null</c> or <c>undefined</c>.
    /// </summary>
    public bool IsNullOrUndefined
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Null || _tag == JSValueType.Undefined;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is a boolean.
    /// </summary>
    public bool IsBool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Bool;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is the boolean <c>true</c>.
    /// </summary>
    public bool IsTrue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Bool && _int64Value != 0;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is the boolean <c>false</c>.
    /// </summary>
    public bool IsFalse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Bool && _int64Value == 0;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is a number (int32 or float64).
    /// </summary>
    public bool IsNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Int || _tag == JSValueType.Float64;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is an integer.
    /// </summary>
    public bool IsInt
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Int;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is a string.
    /// </summary>
    public bool IsString
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.String || _tag == JSValueType.StringRope;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is a symbol.
    /// </summary>
    public bool IsSymbol
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Symbol;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is a BigInt.
    /// </summary>
    public bool IsBigInt
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.BigInt || _tag == JSValueType.ShortBigInt;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is an object.
    /// </summary>
    public bool IsObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Object;
    }

    /// <summary>
    /// Returns <c>true</c> if this value represents an exception.
    /// </summary>
    public bool IsException
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Exception;
    }

    /// <summary>
    /// Returns <c>true</c> if this value is uninitialized.
    /// </summary>
    internal bool IsUninitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tag == JSValueType.Uninitialized;
    }

    #endregion

    #region Conversion Methods

    /// <summary>
    /// Converts this value to a boolean following JavaScript coercion rules.
    /// </summary>
    /// <returns>The boolean representation of this value.</returns>
    /// <remarks>
    /// JavaScript falsy values: undefined, null, false, 0, -0, NaN, ""
    /// Everything else is truthy.
    /// </remarks>
    public bool ToBoolean()
    {
        return _tag switch
        {
            JSValueType.Undefined => false,
            JSValueType.Null => false,
            JSValueType.Bool => _int64Value != 0,
            JSValueType.Int => _int64Value != 0,
            JSValueType.Float64 => _float64Value != 0 && !double.IsNaN(_float64Value),
            JSValueType.String => _objectValue is string s && s.Length > 0,
            // All objects are truthy in JavaScript
            JSValueType.Object => true,
            JSValueType.Symbol => true,
            JSValueType.BigInt => true, // BigInt(0) is falsy, but we'd need the actual value
            JSValueType.ShortBigInt => _int64Value != 0,
            _ => false,
        };
    }

    /// <summary>
    /// Attempts to convert this value to a 32-bit integer.
    /// </summary>
    /// <param name="result">The integer value if conversion succeeds.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryGetInt32(out int result)
    {
        if (_tag == JSValueType.Int)
        {
            result = (int)_int64Value;
            return true;
        }

        if (_tag == JSValueType.Float64)
        {
            if (_float64Value >= int.MinValue && _float64Value <= int.MaxValue)
            {
                int intVal = (int)_float64Value;
                if ((double)intVal == _float64Value)
                {
                    result = intVal;
                    return true;
                }
            }
        }

        result = 0;
        return false;
    }

    /// <summary>
    /// Converts this value to a 32-bit integer.
    /// </summary>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the value cannot be converted to an integer.</exception>
    public int ToInt32()
    {
        if (TryGetInt32(out int result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot convert {_tag} to Int32.");
    }

    /// <summary>
    /// Attempts to convert this value to a 64-bit floating-point number.
    /// </summary>
    /// <param name="result">The floating-point value if conversion succeeds.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryGetDouble(out double result)
    {
        switch (_tag)
        {
            case JSValueType.Int:
                result = _int64Value;
                return true;
            case JSValueType.Float64:
                result = _float64Value;
                return true;
            case JSValueType.Bool:
                result = _int64Value != 0 ? 1.0 : 0.0;
                return true;
            case JSValueType.Null:
                result = 0.0;
                return true;
            case JSValueType.Undefined:
                result = double.NaN;
                return true;
            default:
                result = double.NaN;
                return false;
        }
    }

    /// <summary>
    /// Converts this value to a 64-bit floating-point number.
    /// </summary>
    /// <returns>The floating-point value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the value cannot be converted to a number.</exception>
    public double ToDouble()
    {
        if (TryGetDouble(out double result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot convert {_tag} to Double.");
    }

    /// <summary>
    /// Attempts to get the string value.
    /// </summary>
    /// <param name="result">The string value if this is a string.</param>
    /// <returns><c>true</c> if this value is a string; otherwise, <c>false</c>.</returns>
    public bool TryGetString([NotNullWhen(true)] out string? result)
    {
        if (_tag == JSValueType.String && _objectValue is string s)
        {
            result = s;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Attempts to get this value as a JSObject.
    /// </summary>
    /// <param name="result">The JSObject if this is an object.</param>
    /// <returns><c>true</c> if this value is an object; otherwise, <c>false</c>.</returns>
    public bool TryGetObject([NotNullWhen(true)] out JSObject? result)
    {
        if (_tag == JSValueType.Object && _objectValue is JSObject obj)
        {
            result = obj;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Gets this value as a JSObject.
    /// </summary>
    /// <returns>The JSObject.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this value is not an object.</exception>
    public JSObject AsObject()
    {
        if (TryGetObject(out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot convert {_tag} to Object.");
    }

    /// <summary>
    /// Attempts to get this value as a JSSymbol.
    /// </summary>
    /// <param name="result">The symbol if successful.</param>
    /// <returns>True if this value is a symbol.</returns>
    public bool TryGetSymbol([NotNullWhen(true)] out JSSymbol? result)
    {
        if (_tag == JSValueType.Symbol && _objectValue is JSSymbol symbol)
        {
            result = symbol;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Gets this value as a JSSymbol.
    /// </summary>
    /// <returns>The JSSymbol.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this value is not a symbol.</exception>
    public JSSymbol AsSymbol()
    {
        if (TryGetSymbol(out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot convert {_tag} to Symbol.");
    }

    /// <summary>
    /// Attempts to get this value as a JSBigInt.
    /// </summary>
    /// <param name="result">The BigInt if successful.</param>
    /// <returns>True if this value is a BigInt.</returns>
    public bool TryGetBigInt([NotNullWhen(true)] out JSBigInt? result)
    {
        if (_tag == JSValueType.ShortBigInt)
        {
            result = new JSBigInt(_int64Value);
            return true;
        }

        if (_tag == JSValueType.BigInt && _objectValue is JSBigInt bigInt)
        {
            result = bigInt;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Gets this value as a JSBigInt.
    /// </summary>
    /// <returns>The JSBigInt.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this value is not a BigInt.</exception>
    public JSBigInt AsBigInt()
    {
        if (TryGetBigInt(out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot convert {_tag} to BigInt.");
    }

    /// <summary>
    /// Converts this value to its string representation.
    /// </summary>
    /// <returns>The string representation of this value.</returns>
    /// <remarks>
    /// This follows JavaScript's ToString conversion rules:
    /// - undefined → "undefined"
    /// - null → "null"
    /// - true → "true", false → "false"
    /// - Numbers → their string representation
    /// - Strings → the string itself
    /// - Objects → "[object Object]" (simplified)
    /// </remarks>
    public override string ToString()
    {
        return _tag switch
        {
            JSValueType.Undefined => "undefined",
            JSValueType.Null => "null",
            JSValueType.Bool => _int64Value != 0 ? "true" : "false",
            JSValueType.Int => _int64Value.ToString(CultureInfo.InvariantCulture),
            JSValueType.Float64 => FormatDouble(_float64Value),
            JSValueType.String => _objectValue as string ?? "",
            JSValueType.Symbol => _objectValue is JSSymbol sym ? sym.ToString() : "Symbol()",
            JSValueType.Object => "[object Object]",
            JSValueType.BigInt => _objectValue?.ToString() ?? "0n",
            JSValueType.ShortBigInt => $"{_int64Value}n",
            JSValueType.Exception => "[exception]",
            JSValueType.Uninitialized => "[uninitialized]",
            _ => $"[{_tag}]",
        };
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
            return "NaN";
        if (double.IsPositiveInfinity(value))
            return "Infinity";
        if (double.IsNegativeInfinity(value))
            return "-Infinity";

        // Use G17 for round-trip precision
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Equality

    /// <summary>
    /// Determines whether this value is equal to another JSValue.
    /// This performs a strict equality check (like JavaScript ===).
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><c>true</c> if the values are strictly equal; otherwise, <c>false</c>.</returns>
    public bool Equals(JSValue other)
    {
        if (_tag != other._tag)
            return false;

        return _tag switch
        {
            JSValueType.Undefined => true,
            JSValueType.Null => true,
            JSValueType.Bool => _int64Value == other._int64Value,
            JSValueType.Int => _int64Value == other._int64Value,
            JSValueType.Float64 => _float64Value == other._float64Value,
            JSValueType.String => string.Equals(_objectValue as string, other._objectValue as string, StringComparison.Ordinal),
            JSValueType.ShortBigInt => _int64Value == other._int64Value,
            // For reference types, compare by reference
            _ => ReferenceEquals(_objectValue, other._objectValue),
        };
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JSValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _tag switch
        {
            JSValueType.Undefined => HashCode.Combine(_tag),
            JSValueType.Null => HashCode.Combine(_tag),
            JSValueType.Bool => HashCode.Combine(_tag, _int64Value),
            JSValueType.Int => HashCode.Combine(_tag, _int64Value),
            JSValueType.Float64 => HashCode.Combine(_tag, _float64Value),
            JSValueType.String => HashCode.Combine(_tag, _objectValue?.GetHashCode() ?? 0),
            JSValueType.ShortBigInt => HashCode.Combine(_tag, _int64Value),
            _ => HashCode.Combine(_tag, _objectValue?.GetHashCode() ?? 0),
        };
    }

    /// <summary>
    /// Determines whether two JSValue instances are equal.
    /// </summary>
    public static bool operator ==(JSValue left, JSValue right) => left.Equals(right);

    /// <summary>
    /// Determines whether two JSValue instances are not equal.
    /// </summary>
    public static bool operator !=(JSValue left, JSValue right) => !left.Equals(right);

    #endregion

    #region Implicit Conversions

    /// <summary>
    /// Implicitly converts a boolean to a JSValue.
    /// </summary>
    public static implicit operator JSValue(bool value) => FromBoolean(value);

    /// <summary>
    /// Implicitly converts an int32 to a JSValue.
    /// </summary>
    public static implicit operator JSValue(int value) => FromInt32(value);

    /// <summary>
    /// Implicitly converts a double to a JSValue.
    /// </summary>
    public static implicit operator JSValue(double value) => FromDouble(value);

    /// <summary>
    /// Implicitly converts a string to a JSValue.
    /// </summary>
    public static implicit operator JSValue(string value) => FromString(value);

    #endregion

    #region Helpers

    [DoesNotReturn]
    private static void ThrowArgumentNull(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    private string DebuggerDisplay => $"{_tag}: {ToString()}";

    #endregion
}
