// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Numerics;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript BigInt value.
/// </summary>
/// <remarks>
/// <para>
/// BigInt is a built-in object that provides a way to represent whole numbers 
/// larger than 2^53 - 1 (Number.MAX_SAFE_INTEGER). BigInt can be used for 
/// arbitrarily large integers.
/// </para>
/// <para>
/// In JavaScript, BigInt is created by appending 'n' to an integer literal
/// or by calling the BigInt() function. BigInt values are not strictly equal
/// to Number values even if they represent the same mathematical value.
/// </para>
/// <para>
/// This implementation uses System.Numerics.BigInteger as the underlying
/// storage, which provides arbitrary precision integer arithmetic.
/// </para>
/// </remarks>
public sealed class JSBigInt : IEquatable<JSBigInt>, IComparable<JSBigInt>
{
    #region Fields

    private readonly BigInteger _value;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new JSBigInt from a BigInteger value.
    /// </summary>
    /// <param name="value">The BigInteger value.</param>
    public JSBigInt(BigInteger value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new JSBigInt from a long value.
    /// </summary>
    /// <param name="value">The long value.</param>
    public JSBigInt(long value)
    {
        _value = new BigInteger(value);
    }

    /// <summary>
    /// Creates a new JSBigInt from a ulong value.
    /// </summary>
    /// <param name="value">The ulong value.</param>
    public JSBigInt(ulong value)
    {
        _value = new BigInteger(value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying BigInteger value.
    /// </summary>
    public BigInteger Value => _value;

    /// <summary>
    /// Gets whether this BigInt is zero.
    /// </summary>
    public bool IsZero => _value.IsZero;

    /// <summary>
    /// Gets whether this BigInt is one.
    /// </summary>
    public bool IsOne => _value.IsOne;

    /// <summary>
    /// Gets whether this BigInt is negative.
    /// </summary>
    public bool IsNegative => _value.Sign < 0;

    /// <summary>
    /// Gets whether this BigInt is positive (not zero and not negative).
    /// </summary>
    public bool IsPositive => _value.Sign > 0;

    /// <summary>
    /// Gets the sign of this BigInt (-1, 0, or 1).
    /// </summary>
    public int Sign => _value.Sign;

    /// <summary>
    /// Gets a BigInt representing zero.
    /// </summary>
    public static JSBigInt Zero { get; } = new JSBigInt(BigInteger.Zero);

    /// <summary>
    /// Gets a BigInt representing one.
    /// </summary>
    public static JSBigInt One { get; } = new JSBigInt(BigInteger.One);

    /// <summary>
    /// Gets a BigInt representing negative one.
    /// </summary>
    public static JSBigInt MinusOne { get; } = new JSBigInt(BigInteger.MinusOne);

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a JSBigInt from a string in the specified radix.
    /// </summary>
    /// <param name="value">The string representation.</param>
    /// <param name="radix">The radix (2-36). Default is 10.</param>
    /// <returns>The JSBigInt value.</returns>
    /// <exception cref="FormatException">Thrown if the string is not a valid number.</exception>
    public static JSBigInt Parse(string value, int radix = 10)
    {
        if (string.IsNullOrEmpty(value))
            throw new FormatException("Cannot parse empty string as BigInt");

        // Remove 'n' suffix if present
        if (value.EndsWith("n", StringComparison.Ordinal))
            value = value.Substring(0, value.Length - 1);

        // Remove underscores (numeric separators)
        value = value.Replace("_", "");

        bool negative = false;
        int startIndex = 0;

        // Handle sign
        if (value.Length > 0 && value[0] == '-')
        {
            negative = true;
            startIndex = 1;
        }
        else if (value.Length > 0 && value[0] == '+')
        {
            startIndex = 1;
        }

        // Handle prefix for radix detection
        if (radix == 0 || radix == 16)
        {
            if (value.Length > startIndex + 1 &&
                value[startIndex] == '0' &&
                (value[startIndex + 1] == 'x' || value[startIndex + 1] == 'X'))
            {
                radix = 16;
                startIndex += 2;
            }
        }

        if (radix == 0 || radix == 2)
        {
            if (value.Length > startIndex + 1 &&
                value[startIndex] == '0' &&
                (value[startIndex + 1] == 'b' || value[startIndex + 1] == 'B'))
            {
                radix = 2;
                startIndex += 2;
            }
        }

        if (radix == 0 || radix == 8)
        {
            if (value.Length > startIndex + 1 &&
                value[startIndex] == '0' &&
                (value[startIndex + 1] == 'o' || value[startIndex + 1] == 'O'))
            {
                radix = 8;
                startIndex += 2;
            }
        }

        if (radix == 0)
            radix = 10;

        string digits = startIndex > 0 ? value.Substring(startIndex) : value;

        if (string.IsNullOrEmpty(digits))
            throw new FormatException("Cannot parse empty string as BigInt");

        BigInteger result;

        if (radix == 10)
        {
            result = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        }
        else if (radix == 16)
        {
            // BigInteger.Parse with hex requires leading 0 for positive numbers
            // to avoid sign extension
            if (digits.Length > 0 && "89ABCDEFabcdef".Contains(digits[0]))
            {
                digits = "0" + digits;
            }
            result = BigInteger.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        else
        {
            // For other radixes, convert manually
            result = BigInteger.Zero;
            foreach (char c in digits)
            {
                int digit = GetDigitValue(c, radix);
                if (digit < 0)
                    throw new FormatException($"Invalid digit '{c}' for radix {radix}");
                result = result * radix + digit;
            }
        }

        if (negative)
            result = -result;

        return new JSBigInt(result);
    }

    /// <summary>
    /// Tries to parse a string as a JSBigInt.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed result.</param>
    /// <param name="radix">The radix (2-36). Default is 10.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string value, out JSBigInt result, int radix = 10)
    {
        try
        {
            result = Parse(value, radix);
            return true;
        }
        catch
        {
            result = Zero;
            return false;
        }
    }

    /// <summary>
    /// Tries to create a JSBigInt from a string.
    /// This is an alias for TryParse with default radix.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed result.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryFromString(string value, out JSBigInt result)
    {
        return TryParse(value, out result);
    }

    /// <summary>
    /// Creates a JSBigInt from a double value (truncated toward zero).
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>The JSBigInt value.</returns>
    /// <exception cref="ArgumentException">Thrown if the value is NaN or Infinity.</exception>
    public static JSBigInt FromDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("Cannot convert NaN or Infinity to BigInt", nameof(value));
        
        // Truncate toward zero
        value = Math.Truncate(value);
        
        // For values within long range, use direct conversion
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            return new JSBigInt((long)value);
        }
        
        // For larger values, use BigInteger conversion via string
        return new JSBigInt(new BigInteger(value));
    }

    private static int GetDigitValue(char c, int radix)
    {
        int value;
        if (c >= '0' && c <= '9')
            value = c - '0';
        else if (c >= 'a' && c <= 'z')
            value = c - 'a' + 10;
        else if (c >= 'A' && c <= 'Z')
            value = c - 'A' + 10;
        else
            return -1;

        return value < radix ? value : -1;
    }

    #endregion

    #region Arithmetic Operations

    /// <summary>
    /// Adds two BigInts.
    /// </summary>
    public static JSBigInt operator +(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value + right._value);

    /// <summary>
    /// Subtracts two BigInts.
    /// </summary>
    public static JSBigInt operator -(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value - right._value);

    /// <summary>
    /// Multiplies two BigInts.
    /// </summary>
    public static JSBigInt operator *(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value * right._value);

    /// <summary>
    /// Divides two BigInts (truncating toward zero).
    /// </summary>
    public static JSBigInt operator /(JSBigInt left, JSBigInt right)
    {
        if (right._value.IsZero)
            throw new DivideByZeroException("BigInt division by zero");
        return new JSBigInt(left._value / right._value);
    }

    /// <summary>
    /// Gets the remainder of dividing two BigInts.
    /// </summary>
    public static JSBigInt operator %(JSBigInt left, JSBigInt right)
    {
        if (right._value.IsZero)
            throw new DivideByZeroException("BigInt division by zero");
        return new JSBigInt(left._value % right._value);
    }

    /// <summary>
    /// Negates a BigInt.
    /// </summary>
    public static JSBigInt operator -(JSBigInt value)
        => new JSBigInt(-value._value);

    /// <summary>
    /// Returns the absolute value.
    /// </summary>
    public JSBigInt Abs() => new JSBigInt(BigInteger.Abs(_value));

    /// <summary>
    /// Raises this BigInt to a power.
    /// </summary>
    /// <param name="exponent">The exponent (must be non-negative).</param>
    /// <returns>The result.</returns>
    public JSBigInt Pow(int exponent)
    {
        if (exponent < 0)
            throw new ArgumentOutOfRangeException(nameof(exponent), "BigInt exponent must be non-negative");
        return new JSBigInt(BigInteger.Pow(_value, exponent));
    }

    #endregion

    #region Bitwise Operations

    /// <summary>
    /// Bitwise AND.
    /// </summary>
    public static JSBigInt operator &(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value & right._value);

    /// <summary>
    /// Bitwise OR.
    /// </summary>
    public static JSBigInt operator |(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value | right._value);

    /// <summary>
    /// Bitwise XOR.
    /// </summary>
    public static JSBigInt operator ^(JSBigInt left, JSBigInt right)
        => new JSBigInt(left._value ^ right._value);

    /// <summary>
    /// Bitwise NOT (two's complement).
    /// </summary>
    public static JSBigInt operator ~(JSBigInt value)
        => new JSBigInt(~value._value);

    /// <summary>
    /// Left shift.
    /// </summary>
    public static JSBigInt operator <<(JSBigInt value, int shift)
    {
        if (shift < 0)
            return value >> (-shift);
        return new JSBigInt(value._value << shift);
    }

    /// <summary>
    /// Right shift (sign-preserving).
    /// </summary>
    public static JSBigInt operator >>(JSBigInt value, int shift)
    {
        if (shift < 0)
            return value << (-shift);
        return new JSBigInt(value._value >> shift);
    }

    #endregion

    #region Comparison Operations

    /// <summary>
    /// Compares two BigInts.
    /// </summary>
    public int CompareTo(JSBigInt? other)
    {
        if (other is null) return 1;
        return _value.CompareTo(other._value);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(JSBigInt? left, JSBigInt? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(JSBigInt? left, JSBigInt? right)
        => !(left == right);

    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(JSBigInt left, JSBigInt right)
        => left._value < right._value;

    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(JSBigInt left, JSBigInt right)
        => left._value > right._value;

    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(JSBigInt left, JSBigInt right)
        => left._value <= right._value;

    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(JSBigInt left, JSBigInt right)
        => left._value >= right._value;

    #endregion

    #region Static Methods

    /// <summary>
    /// Adds two BigInts.
    /// </summary>
    public static JSBigInt Add(JSBigInt left, JSBigInt right)
        => left + right;

    /// <summary>
    /// Subtracts two BigInts.
    /// </summary>
    public static JSBigInt Subtract(JSBigInt left, JSBigInt right)
        => left - right;

    /// <summary>
    /// Multiplies two BigInts.
    /// </summary>
    public static JSBigInt Multiply(JSBigInt left, JSBigInt right)
        => left * right;

    /// <summary>
    /// Divides two BigInts.
    /// </summary>
    public static JSBigInt Divide(JSBigInt left, JSBigInt right)
        => left / right;

    /// <summary>
    /// Gets the remainder of two BigInts.
    /// </summary>
    public static JSBigInt Mod(JSBigInt left, JSBigInt right)
        => left % right;

    /// <summary>
    /// Negates a BigInt.
    /// </summary>
    public static JSBigInt Neg(JSBigInt value)
        => -value;

    /// <summary>
    /// Raises a BigInt to a power.
    /// </summary>
    public static JSBigInt Pow(JSBigInt baseValue, int exponent)
        => baseValue.Pow(exponent);

    /// <summary>
    /// Bitwise AND of two BigInts.
    /// </summary>
    public static JSBigInt And(JSBigInt left, JSBigInt right)
        => left & right;

    /// <summary>
    /// Bitwise OR of two BigInts.
    /// </summary>
    public static JSBigInt Or(JSBigInt left, JSBigInt right)
        => left | right;

    /// <summary>
    /// Bitwise XOR of two BigInts.
    /// </summary>
    public static JSBigInt Xor(JSBigInt left, JSBigInt right)
        => left ^ right;

    /// <summary>
    /// Bitwise NOT of a BigInt.
    /// </summary>
    public static JSBigInt Not(JSBigInt value)
        => ~value;

    /// <summary>
    /// Left shifts a BigInt.
    /// </summary>
    public static JSBigInt LeftShift(JSBigInt value, int shift)
        => value << shift;

    /// <summary>
    /// Right shifts a BigInt.
    /// </summary>
    public static JSBigInt RightShift(JSBigInt value, int shift)
        => value >> shift;

    /// <summary>
    /// Compares two BigInts.
    /// </summary>
    public static int Compare(JSBigInt left, JSBigInt right)
        => left.CompareTo(right);

    /// <summary>
    /// Checks if two BigInts are equal.
    /// </summary>
    public static bool Equals(JSBigInt? left, JSBigInt? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    #endregion

    #region Conversion Methods

    /// <summary>
    /// Converts to a long, throwing if out of range.
    /// </summary>
    public long ToInt64()
    {
        if (_value < long.MinValue || _value > long.MaxValue)
            throw new OverflowException("BigInt value cannot be represented as Int64");
        return (long)_value;
    }

    /// <summary>
    /// Converts to a ulong, throwing if out of range.
    /// </summary>
    public ulong ToUInt64()
    {
        if (_value < 0 || _value > ulong.MaxValue)
            throw new OverflowException("BigInt value cannot be represented as UInt64");
        return (ulong)_value;
    }

    /// <summary>
    /// Converts to an int, throwing if out of range.
    /// </summary>
    public int ToInt32()
    {
        if (_value < int.MinValue || _value > int.MaxValue)
            throw new OverflowException("BigInt value cannot be represented as Int32");
        return (int)_value;
    }

    /// <summary>
    /// Converts to a double, potentially losing precision.
    /// </summary>
    public double ToDouble()
    {
        return (double)_value;
    }

    /// <summary>
    /// Tries to convert to an int.
    /// </summary>
    public bool TryToInt32(out int result)
    {
        if (_value >= int.MinValue && _value <= int.MaxValue)
        {
            result = (int)_value;
            return true;
        }
        result = 0;
        return false;
    }

    /// <summary>
    /// Tries to convert to a long.
    /// </summary>
    public bool TryToInt64(out long result)
    {
        if (_value >= long.MinValue && _value <= long.MaxValue)
        {
            result = (long)_value;
            return true;
        }
        result = 0;
        return false;
    }

    /// <summary>
    /// Converts to a string in the specified radix.
    /// </summary>
    /// <param name="radix">The radix (2-36). Default is 10.</param>
    /// <returns>The string representation.</returns>
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36");

        if (_value.IsZero)
            return "0";

        if (radix == 10)
            return _value.ToString(CultureInfo.InvariantCulture);

        bool negative = _value.Sign < 0;
        BigInteger absValue = BigInteger.Abs(_value);
        
        var chars = new System.Collections.Generic.List<char>();
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        while (absValue > 0)
        {
            int remainder = (int)(absValue % radix);
            chars.Add(digits[remainder]);
            absValue /= radix;
        }

        chars.Reverse();
        string result = new string(chars.ToArray());
        
        return negative ? "-" + result : result;
    }

    /// <summary>
    /// Returns the string representation with 'n' suffix.
    /// </summary>
    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture) + "n";
    }

    /// <summary>
    /// Returns the string representation without 'n' suffix.
    /// </summary>
    public string ToStringWithoutSuffix()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region Static Methods (BigInt API)

    /// <summary>
    /// Returns a BigInt representing the value with only the specified bits.
    /// </summary>
    /// <param name="bits">The number of bits.</param>
    /// <param name="bigint">The BigInt value.</param>
    /// <returns>The result as unsigned.</returns>
    public static JSBigInt AsUintN(int bits, JSBigInt bigint)
    {
        if (bits == 0)
            return Zero;

        // Mask off to get only the low 'bits' bits (unsigned)
        BigInteger mask = (BigInteger.One << bits) - 1;
        BigInteger result = bigint._value & mask;
        return new JSBigInt(result);
    }

    /// <summary>
    /// Returns a BigInt representing the value with only the specified bits (signed).
    /// </summary>
    /// <param name="bits">The number of bits.</param>
    /// <param name="bigint">The BigInt value.</param>
    /// <returns>The result as signed.</returns>
    public static JSBigInt AsIntN(int bits, JSBigInt bigint)
    {
        if (bits == 0)
            return Zero;

        // Get unsigned value first
        BigInteger mask = (BigInteger.One << bits) - 1;
        BigInteger result = bigint._value & mask;

        // Check if sign bit is set
        BigInteger signBit = BigInteger.One << (bits - 1);
        if ((result & signBit) != 0)
        {
            // Sign extend (subtract 2^bits)
            result -= BigInteger.One << bits;
        }

        return new JSBigInt(result);
    }

    #endregion

    #region Equality

    /// <summary>
    /// Checks equality with another JSBigInt.
    /// </summary>
    public bool Equals(JSBigInt? other)
    {
        if (other is null) return false;
        return _value.Equals(other._value);
    }

    /// <summary>
    /// Checks equality with another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is JSBigInt other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    #endregion

    #region Implicit Conversions

    /// <summary>
    /// Implicit conversion from long.
    /// </summary>
    public static implicit operator JSBigInt(long value) => new JSBigInt(value);

    /// <summary>
    /// Implicit conversion from int.
    /// </summary>
    public static implicit operator JSBigInt(int value) => new JSBigInt(value);

    /// <summary>
    /// Implicit conversion from BigInteger.
    /// </summary>
    public static implicit operator JSBigInt(BigInteger value) => new JSBigInt(value);

    #endregion
}
