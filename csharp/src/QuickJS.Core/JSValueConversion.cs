// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace QuickJS;

/// <summary>
/// Provides JavaScript-compliant type conversion functions.
/// </summary>
/// <remarks>
/// <para>
/// These methods implement the ECMAScript type conversion abstract operations:
/// ToBoolean, ToNumber, ToInteger, ToInt32, ToUInt32, ToString.
/// </para>
/// <para>
/// Unlike the methods on <see cref="JSValue"/> which may throw exceptions,
/// these methods follow JavaScript semantics and always return a value
/// (e.g., NaN for invalid number conversions).
/// </para>
/// </remarks>
public static class JSValueConversion
{
    #region ToBoolean

    /// <summary>
    /// Converts a value to boolean following JavaScript ToBoolean rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The boolean result.</returns>
    /// <remarks>
    /// JavaScript falsy values: undefined, null, false, 0, -0, NaN, ""
    /// Everything else is truthy.
    /// </remarks>
    public static bool ToBoolean(JSValue value)
    {
        return value.ToBoolean();
    }

    #endregion

    #region ToNumber

    /// <summary>
    /// Converts a value to a number following JavaScript ToNumber rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The numeric result (may be NaN or Infinity).</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>undefined → NaN</item>
    /// <item>null → +0</item>
    /// <item>true → 1, false → 0</item>
    /// <item>Number → unchanged</item>
    /// <item>String → parsed as number, NaN if invalid</item>
    /// <item>Object → ToPrimitive then ToNumber</item>
    /// </list>
    /// </remarks>
    public static double ToNumber(JSValue value)
    {
        switch (value.Tag)
        {
            case JSValueType.Undefined:
                return double.NaN;

            case JSValueType.Null:
                return 0.0;

            case JSValueType.Bool:
                return value.IsTrue ? 1.0 : 0.0;

            case JSValueType.Int:
                if (value.TryGetInt32(out int intVal))
                    return intVal;
                // Fall through to float handling
                goto case JSValueType.Float64;

            case JSValueType.Float64:
                if (value.TryGetDouble(out double dVal))
                    return dVal;
                return double.NaN;

            case JSValueType.String:
                return StringToNumber(value.ToString() ?? "");

            case JSValueType.ShortBigInt:
                // BigInt cannot be implicitly converted to number in JS
                // but for convenience we return the value
                if (value.TryGetInt32(out int bigIntVal))
                    return bigIntVal;
                return double.NaN;

            case JSValueType.Object:
                if (value.TryGetObject(out var obj))
                {
                    switch (obj.ClassId)
                    {
                        case JSClassId.Number:
                        case JSClassId.String:
                        case JSClassId.Boolean:
                            return ToNumber(obj.InternalValue);
                    }
                }
                // TODO: Proper ToPrimitive implementation
                return double.NaN;

            case JSValueType.Symbol:
                // TypeError in JS, but we return NaN
                return double.NaN;

            default:
                return double.NaN;
        }
    }

    /// <summary>
    /// Converts a string to a number following JavaScript rules.
    /// </summary>
    private static double StringToNumber(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0.0;

        s = s.Trim();
        if (s.Length == 0)
            return 0.0;

        // Handle special values
        if (s.Equals("Infinity", StringComparison.Ordinal) ||
            s.Equals("+Infinity", StringComparison.Ordinal))
            return double.PositiveInfinity;
        if (s.Equals("-Infinity", StringComparison.Ordinal))
            return double.NegativeInfinity;

        // Handle hex, octal, binary prefixes (0x, 0o, 0b)
        if (s.Length >= 2 && s[0] == '0')
        {
#if NET8_0_OR_GREATER
            ReadOnlySpan<char> digits = s.AsSpan(2);
            switch (s[1])
            {
                case 'x' or 'X':
                    return TryParseHex(digits, out long hexVal) ? hexVal : double.NaN;
                case 'o' or 'O':
                    return TryParseOctal(digits, out long octVal) ? octVal : double.NaN;
                case 'b' or 'B':
                    return TryParseBinary(digits, out long binVal) ? binVal : double.NaN;
            }
#else
            string digits = s.Substring(2);
            switch (s[1])
            {
                case 'x':
                case 'X':
                    return TryParseHex(digits, out long hexVal) ? hexVal : double.NaN;
                case 'o':
                case 'O':
                    return TryParseOctal(digits, out long octVal) ? octVal : double.NaN;
                case 'b':
                case 'B':
                    return TryParseBinary(digits, out long binVal) ? binVal : double.NaN;
            }
#endif
        }

        // Parse as decimal
#if NET8_0_OR_GREATER
        if (double.TryParse(s.AsSpan(), NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out double result))
#else
        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out double result))
#endif
        {
            return result;
        }

        return double.NaN;
    }

#if NET8_0_OR_GREATER
    private static bool TryParseHex(ReadOnlySpan<char> s, out long result)
    {
        result = 0;
        if (s.IsEmpty)
            return false;

        foreach (char c in s)
        {
            int digit;
            if (c >= '0' && c <= '9')
                digit = c - '0';
            else if (c >= 'a' && c <= 'f')
                digit = c - 'a' + 10;
            else if (c >= 'A' && c <= 'F')
                digit = c - 'A' + 10;
            else
                return false;

            result = result * 16 + digit;
        }
        return true;
    }

    private static bool TryParseOctal(ReadOnlySpan<char> s, out long result)
    {
        result = 0;
        if (s.IsEmpty)
            return false;

        foreach (char c in s)
        {
            if (c < '0' || c > '7')
                return false;
            result = result * 8 + (c - '0');
        }
        return true;
    }

    private static bool TryParseBinary(ReadOnlySpan<char> s, out long result)
    {
        result = 0;
        if (s.IsEmpty)
            return false;

        foreach (char c in s)
        {
            if (c != '0' && c != '1')
                return false;
            result = result * 2 + (c - '0');
        }
        return true;
    }
#else
    private static bool TryParseHex(string s, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(s))
            return false;

        foreach (char c in s)
        {
            int digit;
            if (c >= '0' && c <= '9')
                digit = c - '0';
            else if (c >= 'a' && c <= 'f')
                digit = c - 'a' + 10;
            else if (c >= 'A' && c <= 'F')
                digit = c - 'A' + 10;
            else
                return false;

            result = result * 16 + digit;
        }
        return true;
    }

    private static bool TryParseOctal(string s, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(s))
            return false;

        foreach (char c in s)
        {
            if (c < '0' || c > '7')
                return false;
            result = result * 8 + (c - '0');
        }
        return true;
    }

    private static bool TryParseBinary(string s, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(s))
            return false;

        foreach (char c in s)
        {
            if (c != '0' && c != '1')
                return false;
            result = result * 2 + (c - '0');
        }
        return true;
    }
#endif

    #endregion

    #region ToInteger

    /// <summary>
    /// Converts a value to an integer following JavaScript ToInteger rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The integer result (as double, may be large).</returns>
    public static double ToInteger(JSValue value)
    {
        double number = ToNumber(value);

        if (double.IsNaN(number))
            return 0.0;

        if (number == 0.0 || double.IsInfinity(number))
            return number;

        return Math.Sign(number) * Math.Floor(Math.Abs(number));
    }

    #endregion

    #region ToInt32

    /// <summary>
    /// Converts a value to a 32-bit signed integer following JavaScript ToInt32 rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The 32-bit signed integer result.</returns>
    /// <remarks>
    /// This performs modulo 2^32 conversion as specified by ECMAScript.
    /// </remarks>
    public static int ToInt32(JSValue value)
    {
        double number = ToNumber(value);

        if (double.IsNaN(number) || double.IsInfinity(number) || number == 0.0)
            return 0;

        // Truncate toward zero
        double integer = Math.Sign(number) * Math.Floor(Math.Abs(number));

        // Modulo 2^32
        double int32Bit = integer % 4294967296.0; // 2^32

        // Convert to signed
        if (int32Bit >= 2147483648.0) // 2^31
            int32Bit -= 4294967296.0;
        else if (int32Bit < -2147483648.0)
            int32Bit += 4294967296.0;

        return (int)int32Bit;
    }

    #endregion

    #region ToUInt32

    /// <summary>
    /// Converts a value to a 32-bit unsigned integer following JavaScript ToUInt32 rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The 32-bit unsigned integer result.</returns>
    public static uint ToUInt32(JSValue value)
    {
        double number = ToNumber(value);

        if (double.IsNaN(number) || double.IsInfinity(number) || number == 0.0)
            return 0;

        double integer = Math.Sign(number) * Math.Floor(Math.Abs(number));
        double uint32Bit = integer % 4294967296.0;

        if (uint32Bit < 0)
            uint32Bit += 4294967296.0;

        return (uint)uint32Bit;
    }

    #endregion

    #region ToUInt16

    /// <summary>
    /// Converts a value to a 16-bit unsigned integer following JavaScript ToUInt16 rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The 16-bit unsigned integer result.</returns>
    public static ushort ToUInt16(JSValue value)
    {
        double number = ToNumber(value);

        if (double.IsNaN(number) || double.IsInfinity(number) || number == 0.0)
            return 0;

        double integer = Math.Sign(number) * Math.Floor(Math.Abs(number));
        double uint16Bit = integer % 65536.0;

        if (uint16Bit < 0)
            uint16Bit += 65536.0;

        return (ushort)uint16Bit;
    }

    #endregion

    #region ToString

    /// <summary>
    /// Converts a value to a string following JavaScript ToString rules.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The string result.</returns>
    public static string ToString(JSValue value)
    {
        if (value.Tag == JSValueType.Object && value.TryGetObject(out var obj))
        {
            switch (obj.ClassId)
            {
                case JSClassId.String:
                case JSClassId.Number:
                case JSClassId.Boolean:
                    return ToString(obj.InternalValue);
            }
        }

        return value.ToString() ?? "";
    }

    /// <summary>
    /// Converts a number to its string representation following JavaScript rules.
    /// </summary>
    /// <param name="value">The number to convert.</param>
    /// <returns>The string representation.</returns>
    public static string NumberToString(double value)
    {
        if (double.IsNaN(value))
            return "NaN";
        if (double.IsPositiveInfinity(value))
            return "Infinity";
        if (double.IsNegativeInfinity(value))
            return "-Infinity";

        // Handle -0
        if (value == 0.0)
            return "0";

        // Use JavaScript-like formatting
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Type Checking

    /// <summary>
    /// Returns the typeof result for a value as a string.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>The typeof string.</returns>
    public static string TypeOf(JSValue value)
    {
        return value.Tag switch
        {
            JSValueType.Undefined => "undefined",
            JSValueType.Null => "object", // typeof null === "object" (historical bug)
            JSValueType.Bool => "boolean",
            JSValueType.Int or JSValueType.Float64 => "number",
            JSValueType.String => "string",
            JSValueType.Symbol => "symbol",
            JSValueType.BigInt or JSValueType.ShortBigInt => "bigint",
            JSValueType.Object => value.AsObject() is JSFunction ? "function" : "object",
            _ => "undefined"
        };
    }

    /// <summary>
    /// Checks if a value is callable (a function).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is callable.</returns>
    public static bool IsCallable(JSValue value)
    {
        if (!value.IsObject)
            return false;

        return value.AsObject() is JSFunction;
    }

    /// <summary>
    /// Checks if a value is a constructor.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is a constructor.</returns>
    public static bool IsConstructor(JSValue value)
    {
        if (!value.IsObject)
            return false;

        // For now, all functions are considered constructors
        // TODO: Arrow functions and methods are not constructors
        return value.AsObject() is JSFunction;
    }

    #endregion

    #region SameValue

    /// <summary>
    /// Implements the SameValue abstract operation.
    /// Used by Object.is().
    /// </summary>
    /// <param name="x">First value.</param>
    /// <param name="y">Second value.</param>
    /// <returns>True if the values are the same.</returns>
    /// <remarks>
    /// Differs from === in that:
    /// - SameValue(NaN, NaN) is true
    /// - SameValue(+0, -0) is false
    /// </remarks>
    public static bool SameValue(JSValue x, JSValue y)
    {
        // Different types
        if (x.Tag != y.Tag)
        {
            // Handle number type differences
            if (x.IsNumber && y.IsNumber)
            {
                double dx = ToNumber(x);
                double dy = ToNumber(y);
                return SameValueNumber(dx, dy);
            }
            return false;
        }

        // Same type
        switch (x.Tag)
        {
            case JSValueType.Undefined:
            case JSValueType.Null:
                return true;

            case JSValueType.Int:
            case JSValueType.Float64:
                return SameValueNumber(ToNumber(x), ToNumber(y));

            case JSValueType.String:
                return x.ToString() == y.ToString();

            case JSValueType.Bool:
                return x.IsTrue == y.IsTrue;

            case JSValueType.Object:
                return ReferenceEquals(x.AsObject(), y.AsObject());

            case JSValueType.Symbol:
                // Symbols are compared by identity - for value types we compare Tag/value
                // In a full implementation, Symbol values would hold a reference to a Symbol object
                return x.Tag == y.Tag; // Placeholder - proper symbol comparison needs Symbol identity

            default:
                return false;
        }
    }

    private static bool SameValueNumber(double x, double y)
    {
        // NaN is same as NaN
        if (double.IsNaN(x) && double.IsNaN(y))
            return true;

        // +0 is not same as -0
        if (x == 0 && y == 0)
        {
            // Check for -0 using 1/x
            return (1.0 / x) == (1.0 / y);
        }

        return x == y;
    }

    /// <summary>
    /// Implements the SameValueZero abstract operation.
    /// Used by Array.prototype.includes, Map, Set.
    /// </summary>
    /// <param name="x">First value.</param>
    /// <param name="y">Second value.</param>
    /// <returns>True if the values are the same (treating +0 and -0 as equal).</returns>
    public static bool SameValueZero(JSValue x, JSValue y)
    {
        if (x.Tag != y.Tag)
        {
            if (x.IsNumber && y.IsNumber)
            {
                double dx = ToNumber(x);
                double dy = ToNumber(y);

                // NaN is same as NaN
                if (double.IsNaN(dx) && double.IsNaN(dy))
                    return true;

                return dx == dy; // +0 === -0 is true
            }
            return false;
        }

        return SameValue(x, y);
    }

    #endregion

    #region StrictEquals

    /// <summary>
    /// Implements the Strict Equality Comparison (===) operation.
    /// </summary>
    /// <param name="x">First value.</param>
    /// <param name="y">Second value.</param>
    /// <returns>True if the values are strictly equal.</returns>
    public static bool StrictEquals(JSValue x, JSValue y)
    {
        // Different types are never strictly equal (except Int and Float64 which are both numbers)
        if (x.Tag != y.Tag)
        {
            if (x.IsNumber && y.IsNumber)
            {
                double dx = ToNumber(x);
                double dy = ToNumber(y);
                return dx == dy; // NaN !== NaN, +0 === -0
            }
            return false;
        }

        switch (x.Tag)
        {
            case JSValueType.Undefined:
            case JSValueType.Null:
                return true;
            case JSValueType.Bool:
                return x.IsTrue == y.IsTrue;
            case JSValueType.Int:
                return x.ToInt32() == y.ToInt32();
            case JSValueType.Float64:
                double dx = x.ToDouble();
                double dy = y.ToDouble();
                return dx == dy; // NaN !== NaN (returns false)
            case JSValueType.String:
                return x.ToString() == y.ToString();
            case JSValueType.Object:
                return ReferenceEquals(x.AsObject(), y.AsObject());
            default:
                return false;
        }
    }

    #endregion
}
