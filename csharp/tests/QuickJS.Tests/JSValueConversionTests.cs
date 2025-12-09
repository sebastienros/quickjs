// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for the JSValueConversion class.
/// </summary>
public class JSValueConversionTests
{
    #region ToBoolean Tests

    [Fact]
    public void ToBoolean_Undefined_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.Undefined));
    }

    [Fact]
    public void ToBoolean_Null_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.Null));
    }

    [Fact]
    public void ToBoolean_True_ReturnsTrue()
    {
        Assert.True(JSValueConversion.ToBoolean(JSValue.True));
    }

    [Fact]
    public void ToBoolean_False_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.False));
    }

    [Fact]
    public void ToBoolean_Zero_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.FromInt32(0)));
    }

    [Fact]
    public void ToBoolean_NaN_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.FromDouble(double.NaN)));
    }

    [Fact]
    public void ToBoolean_NonZeroNumber_ReturnsTrue()
    {
        Assert.True(JSValueConversion.ToBoolean(JSValue.FromInt32(42)));
        Assert.True(JSValueConversion.ToBoolean(JSValue.FromDouble(3.14)));
        Assert.True(JSValueConversion.ToBoolean(JSValue.FromInt32(-1)));
    }

    [Fact]
    public void ToBoolean_EmptyString_ReturnsFalse()
    {
        Assert.False(JSValueConversion.ToBoolean(JSValue.FromString("")));
    }

    [Fact]
    public void ToBoolean_NonEmptyString_ReturnsTrue()
    {
        Assert.True(JSValueConversion.ToBoolean(JSValue.FromString("hello")));
        Assert.True(JSValueConversion.ToBoolean(JSValue.FromString("false"))); // "false" is truthy
    }

    #endregion

    #region ToNumber Tests

    [Fact]
    public void ToNumber_Undefined_ReturnsNaN()
    {
        Assert.True(double.IsNaN(JSValueConversion.ToNumber(JSValue.Undefined)));
    }

    [Fact]
    public void ToNumber_Null_ReturnsZero()
    {
        Assert.Equal(0.0, JSValueConversion.ToNumber(JSValue.Null));
    }

    [Fact]
    public void ToNumber_True_ReturnsOne()
    {
        Assert.Equal(1.0, JSValueConversion.ToNumber(JSValue.True));
    }

    [Fact]
    public void ToNumber_False_ReturnsZero()
    {
        Assert.Equal(0.0, JSValueConversion.ToNumber(JSValue.False));
    }

    [Fact]
    public void ToNumber_Integer_ReturnsInteger()
    {
        Assert.Equal(42.0, JSValueConversion.ToNumber(JSValue.FromInt32(42)));
    }

    [Fact]
    public void ToNumber_Double_ReturnsDouble()
    {
        Assert.Equal(3.14, JSValueConversion.ToNumber(JSValue.FromDouble(3.14)));
    }

    [Fact]
    public void ToNumber_EmptyString_ReturnsZero()
    {
        Assert.Equal(0.0, JSValueConversion.ToNumber(JSValue.FromString("")));
    }

    [Fact]
    public void ToNumber_WhitespaceString_ReturnsZero()
    {
        Assert.Equal(0.0, JSValueConversion.ToNumber(JSValue.FromString("   ")));
    }

    [Fact]
    public void ToNumber_NumericString_ReturnsNumber()
    {
        Assert.Equal(42.0, JSValueConversion.ToNumber(JSValue.FromString("42")));
        Assert.Equal(3.14, JSValueConversion.ToNumber(JSValue.FromString("3.14")));
        Assert.Equal(-100.0, JSValueConversion.ToNumber(JSValue.FromString("-100")));
    }

    [Fact]
    public void ToNumber_InfinityString_ReturnsInfinity()
    {
        Assert.Equal(double.PositiveInfinity, JSValueConversion.ToNumber(JSValue.FromString("Infinity")));
        Assert.Equal(double.NegativeInfinity, JSValueConversion.ToNumber(JSValue.FromString("-Infinity")));
    }

    [Fact]
    public void ToNumber_HexString_ReturnsNumber()
    {
        Assert.Equal(255.0, JSValueConversion.ToNumber(JSValue.FromString("0xff")));
        Assert.Equal(255.0, JSValueConversion.ToNumber(JSValue.FromString("0xFF")));
    }

    [Fact]
    public void ToNumber_OctalString_ReturnsNumber()
    {
        Assert.Equal(8.0, JSValueConversion.ToNumber(JSValue.FromString("0o10")));
    }

    [Fact]
    public void ToNumber_BinaryString_ReturnsNumber()
    {
        Assert.Equal(5.0, JSValueConversion.ToNumber(JSValue.FromString("0b101")));
    }

    [Fact]
    public void ToNumber_NonNumericString_ReturnsNaN()
    {
        Assert.True(double.IsNaN(JSValueConversion.ToNumber(JSValue.FromString("hello"))));
        Assert.True(double.IsNaN(JSValueConversion.ToNumber(JSValue.FromString("42abc"))));
    }

    #endregion

    #region ToInteger Tests

    [Fact]
    public void ToInteger_NaN_ReturnsZero()
    {
        Assert.Equal(0.0, JSValueConversion.ToInteger(JSValue.FromDouble(double.NaN)));
    }

    [Fact]
    public void ToInteger_Infinity_ReturnsInfinity()
    {
        Assert.Equal(double.PositiveInfinity, JSValueConversion.ToInteger(JSValue.FromDouble(double.PositiveInfinity)));
        Assert.Equal(double.NegativeInfinity, JSValueConversion.ToInteger(JSValue.FromDouble(double.NegativeInfinity)));
    }

    [Fact]
    public void ToInteger_PositiveFloat_Truncates()
    {
        Assert.Equal(3.0, JSValueConversion.ToInteger(JSValue.FromDouble(3.7)));
        Assert.Equal(3.0, JSValueConversion.ToInteger(JSValue.FromDouble(3.2)));
    }

    [Fact]
    public void ToInteger_NegativeFloat_Truncates()
    {
        Assert.Equal(-3.0, JSValueConversion.ToInteger(JSValue.FromDouble(-3.7)));
        Assert.Equal(-3.0, JSValueConversion.ToInteger(JSValue.FromDouble(-3.2)));
    }

    #endregion

    #region ToInt32 Tests

    [Fact]
    public void ToInt32_Integer_ReturnsInteger()
    {
        Assert.Equal(42, JSValueConversion.ToInt32(JSValue.FromInt32(42)));
    }

    [Fact]
    public void ToInt32_NaN_ReturnsZero()
    {
        Assert.Equal(0, JSValueConversion.ToInt32(JSValue.FromDouble(double.NaN)));
    }

    [Fact]
    public void ToInt32_Infinity_ReturnsZero()
    {
        Assert.Equal(0, JSValueConversion.ToInt32(JSValue.FromDouble(double.PositiveInfinity)));
        Assert.Equal(0, JSValueConversion.ToInt32(JSValue.FromDouble(double.NegativeInfinity)));
    }

    [Fact]
    public void ToInt32_TruncatesDecimal()
    {
        Assert.Equal(3, JSValueConversion.ToInt32(JSValue.FromDouble(3.7)));
        Assert.Equal(-3, JSValueConversion.ToInt32(JSValue.FromDouble(-3.7)));
    }

    [Fact]
    public void ToInt32_LargeNumber_Wraps()
    {
        // 2^32 + 1 should wrap to 1
        Assert.Equal(1, JSValueConversion.ToInt32(JSValue.FromDouble(4294967297.0)));
    }

    #endregion

    #region ToUInt32 Tests

    [Fact]
    public void ToUInt32_Integer_ReturnsUnsigned()
    {
        Assert.Equal(42u, JSValueConversion.ToUInt32(JSValue.FromInt32(42)));
    }

    [Fact]
    public void ToUInt32_NegativeNumber_Wraps()
    {
        // -1 should become 0xFFFFFFFF
        Assert.Equal(4294967295u, JSValueConversion.ToUInt32(JSValue.FromInt32(-1)));
    }

    [Fact]
    public void ToUInt32_NaN_ReturnsZero()
    {
        Assert.Equal(0u, JSValueConversion.ToUInt32(JSValue.FromDouble(double.NaN)));
    }

    #endregion

    #region ToUInt16 Tests

    [Fact]
    public void ToUInt16_Integer_Returns16Bit()
    {
        Assert.Equal((ushort)42, JSValueConversion.ToUInt16(JSValue.FromInt32(42)));
    }

    [Fact]
    public void ToUInt16_LargeNumber_Wraps()
    {
        // 65537 should wrap to 1
        Assert.Equal((ushort)1, JSValueConversion.ToUInt16(JSValue.FromDouble(65537.0)));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", JSValueConversion.ToString(JSValue.Undefined));
    }

    [Fact]
    public void ToString_Null_ReturnsNull()
    {
        Assert.Equal("null", JSValueConversion.ToString(JSValue.Null));
    }

    [Fact]
    public void ToString_True_ReturnsTrue()
    {
        Assert.Equal("true", JSValueConversion.ToString(JSValue.True));
    }

    [Fact]
    public void ToString_False_ReturnsFalse()
    {
        Assert.Equal("false", JSValueConversion.ToString(JSValue.False));
    }

    [Fact]
    public void ToString_Integer_ReturnsNumber()
    {
        Assert.Equal("42", JSValueConversion.ToString(JSValue.FromInt32(42)));
    }

    [Fact]
    public void ToString_String_ReturnsSameString()
    {
        Assert.Equal("hello", JSValueConversion.ToString(JSValue.FromString("hello")));
    }

    #endregion

    #region NumberToString Tests

    [Fact]
    public void NumberToString_NaN_ReturnsNaN()
    {
        Assert.Equal("NaN", JSValueConversion.NumberToString(double.NaN));
    }

    [Fact]
    public void NumberToString_Infinity_ReturnsInfinity()
    {
        Assert.Equal("Infinity", JSValueConversion.NumberToString(double.PositiveInfinity));
        Assert.Equal("-Infinity", JSValueConversion.NumberToString(double.NegativeInfinity));
    }

    [Fact]
    public void NumberToString_Zero_ReturnsZero()
    {
        Assert.Equal("0", JSValueConversion.NumberToString(0.0));
        Assert.Equal("0", JSValueConversion.NumberToString(-0.0));
    }

    #endregion

    #region TypeOf Tests

    [Fact]
    public void TypeOf_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", JSValueConversion.TypeOf(JSValue.Undefined));
    }

    [Fact]
    public void TypeOf_Null_ReturnsObject()
    {
        // This is the famous typeof null === "object" bug in JavaScript
        Assert.Equal("object", JSValueConversion.TypeOf(JSValue.Null));
    }

    [Fact]
    public void TypeOf_Boolean_ReturnsBoolean()
    {
        Assert.Equal("boolean", JSValueConversion.TypeOf(JSValue.True));
        Assert.Equal("boolean", JSValueConversion.TypeOf(JSValue.False));
    }

    [Fact]
    public void TypeOf_Number_ReturnsNumber()
    {
        Assert.Equal("number", JSValueConversion.TypeOf(JSValue.FromInt32(42)));
        Assert.Equal("number", JSValueConversion.TypeOf(JSValue.FromDouble(3.14)));
    }

    [Fact]
    public void TypeOf_String_ReturnsString()
    {
        Assert.Equal("string", JSValueConversion.TypeOf(JSValue.FromString("hello")));
    }

    #endregion

    #region IsCallable Tests

    [Fact]
    public void IsCallable_NonObject_ReturnsFalse()
    {
        Assert.False(JSValueConversion.IsCallable(JSValue.Undefined));
        Assert.False(JSValueConversion.IsCallable(JSValue.Null));
        Assert.False(JSValueConversion.IsCallable(JSValue.FromInt32(42)));
        Assert.False(JSValueConversion.IsCallable(JSValue.FromString("hello")));
    }

    [Fact]
    public void IsCallable_PlainObject_ReturnsFalse()
    {
        var obj = new JSObject();
        Assert.False(JSValueConversion.IsCallable(JSValue.FromObject(obj)));
    }

    [Fact]
    public void IsCallable_Function_ReturnsTrue()
    {
        var func = new JSFunction((thisArg, args) => JSValue.Undefined, "test");
        Assert.True(JSValueConversion.IsCallable(JSValue.FromObject(func)));
    }

    #endregion

    #region SameValue Tests

    [Fact]
    public void SameValue_NaN_ReturnsTrue()
    {
        var nan1 = JSValue.FromDouble(double.NaN);
        var nan2 = JSValue.FromDouble(double.NaN);
        Assert.True(JSValueConversion.SameValue(nan1, nan2));
    }

    [Fact]
    public void SameValue_PositiveAndNegativeZero_ReturnsFalse()
    {
        var posZero = JSValue.FromDouble(0.0);
        var negZero = JSValue.FromDouble(-0.0);
        Assert.False(JSValueConversion.SameValue(posZero, negZero));
    }

    [Fact]
    public void SameValue_SameNumbers_ReturnsTrue()
    {
        Assert.True(JSValueConversion.SameValue(JSValue.FromInt32(42), JSValue.FromInt32(42)));
        Assert.True(JSValueConversion.SameValue(JSValue.FromDouble(3.14), JSValue.FromDouble(3.14)));
    }

    [Fact]
    public void SameValue_DifferentTypes_ReturnsFalse()
    {
        Assert.False(JSValueConversion.SameValue(JSValue.FromInt32(42), JSValue.FromString("42")));
    }

    [Fact]
    public void SameValue_SameStrings_ReturnsTrue()
    {
        Assert.True(JSValueConversion.SameValue(JSValue.FromString("hello"), JSValue.FromString("hello")));
    }

    [Fact]
    public void SameValue_Undefined_ReturnsTrue()
    {
        Assert.True(JSValueConversion.SameValue(JSValue.Undefined, JSValue.Undefined));
    }

    [Fact]
    public void SameValue_Null_ReturnsTrue()
    {
        Assert.True(JSValueConversion.SameValue(JSValue.Null, JSValue.Null));
    }

    [Fact]
    public void SameValue_DifferentBooleans_ReturnsFalse()
    {
        Assert.False(JSValueConversion.SameValue(JSValue.True, JSValue.False));
    }

    #endregion

    #region SameValueZero Tests

    [Fact]
    public void SameValueZero_NaN_ReturnsTrue()
    {
        var nan1 = JSValue.FromDouble(double.NaN);
        var nan2 = JSValue.FromDouble(double.NaN);
        Assert.True(JSValueConversion.SameValueZero(nan1, nan2));
    }

    [Fact]
    public void SameValueZero_PositiveAndNegativeZero_ReturnsTrue()
    {
        // Unlike SameValue, SameValueZero treats +0 and -0 as equal
        var posZero = JSValue.FromDouble(0.0);
        var negZero = JSValue.FromDouble(-0.0);
        Assert.True(JSValueConversion.SameValueZero(posZero, negZero));
    }

    #endregion
}
