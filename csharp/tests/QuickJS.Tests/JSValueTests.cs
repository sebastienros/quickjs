// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Unit tests for the <see cref="JSValue"/> struct.
/// </summary>
public class JSValueTests
{
    #region Factory Methods Tests

    [Fact]
    public void Undefined_HasCorrectTag()
    {
        var value = JSValue.Undefined;
        Assert.Equal(JSValueType.Undefined, value.Tag);
        Assert.True(value.IsUndefined);
        Assert.False(value.IsNull);
    }

    [Fact]
    public void Null_HasCorrectTag()
    {
        var value = JSValue.Null;
        Assert.Equal(JSValueType.Null, value.Tag);
        Assert.True(value.IsNull);
        Assert.False(value.IsUndefined);
    }

    [Fact]
    public void True_HasCorrectTagAndValue()
    {
        var value = JSValue.True;
        Assert.Equal(JSValueType.Bool, value.Tag);
        Assert.True(value.IsBool);
        Assert.True(value.ToBoolean());
    }

    [Fact]
    public void False_HasCorrectTagAndValue()
    {
        var value = JSValue.False;
        Assert.Equal(JSValueType.Bool, value.Tag);
        Assert.True(value.IsBool);
        Assert.False(value.ToBoolean());
    }

    [Fact]
    public void FromBoolean_True_ReturnsTrue()
    {
        var value = JSValue.FromBoolean(true);
        Assert.True(value.IsBool);
        Assert.True(value.ToBoolean());
    }

    [Fact]
    public void FromBoolean_False_ReturnsFalse()
    {
        var value = JSValue.FromBoolean(false);
        Assert.True(value.IsBool);
        Assert.False(value.ToBoolean());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void FromInt32_CreatesIntValue(int input)
    {
        var value = JSValue.FromInt32(input);
        Assert.Equal(JSValueType.Int, value.Tag);
        Assert.True(value.IsInt);
        Assert.True(value.IsNumber);
        Assert.True(value.TryGetInt32(out int result));
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-1.5)]
    [InlineData(3.14159)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.Epsilon)]
    public void FromDouble_NonInteger_CreatesFloat64Value(double input)
    {
        var value = JSValue.FromDouble(input);
        Assert.Equal(JSValueType.Float64, value.Tag);
        Assert.True(value.IsNumber);
        Assert.True(value.TryGetDouble(out double result));
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(42.0)]
    [InlineData(100.0)]
    public void FromDouble_WholeNumber_OptimizesToInt(double input)
    {
        var value = JSValue.FromDouble(input);
        // Whole numbers within int range should be stored as Int
        Assert.Equal(JSValueType.Int, value.Tag);
        Assert.True(value.IsInt);
        Assert.True(value.TryGetInt32(out int result));
        Assert.Equal((int)input, result);
    }

    [Fact]
    public void FromDouble_NaN_CreatesFloat64()
    {
        var value = JSValue.FromDouble(double.NaN);
        Assert.Equal(JSValueType.Float64, value.Tag);
        Assert.True(value.TryGetDouble(out double result));
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void FromDouble_Infinity_CreatesFloat64()
    {
        var pos = JSValue.FromDouble(double.PositiveInfinity);
        var neg = JSValue.FromDouble(double.NegativeInfinity);

        Assert.Equal(JSValueType.Float64, pos.Tag);
        Assert.Equal(JSValueType.Float64, neg.Tag);

        Assert.True(pos.TryGetDouble(out double posResult));
        Assert.True(neg.TryGetDouble(out double negResult));

        Assert.True(double.IsPositiveInfinity(posResult));
        Assert.True(double.IsNegativeInfinity(negResult));
    }

    [Fact]
    public void FromString_ValidString_CreatesStringValue()
    {
        var value = JSValue.FromString("hello");
        Assert.Equal(JSValueType.String, value.Tag);
        Assert.True(value.IsString);
        Assert.True(value.TryGetString(out string? result));
        Assert.Equal("hello", result);
    }

    [Fact]
    public void FromString_EmptyString_CreatesStringValue()
    {
        var value = JSValue.FromString("");
        Assert.True(value.IsString);
        Assert.True(value.TryGetString(out string? result));
        Assert.Equal("", result);
    }

    [Fact]
    public void FromString_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JSValue.FromString(null!));
    }

    #endregion

    #region Type Checking Properties Tests

    [Fact]
    public void IsNullOrUndefined_Null_ReturnsTrue()
    {
        Assert.True(JSValue.Null.IsNullOrUndefined);
    }

    [Fact]
    public void IsNullOrUndefined_Undefined_ReturnsTrue()
    {
        Assert.True(JSValue.Undefined.IsNullOrUndefined);
    }

    [Fact]
    public void IsNullOrUndefined_Number_ReturnsFalse()
    {
        Assert.False(JSValue.FromInt32(42).IsNullOrUndefined);
    }

    [Fact]
    public void IsException_Exception_ReturnsTrue()
    {
        Assert.True(JSValue.Exception.IsException);
    }

    [Fact]
    public void IsException_Undefined_ReturnsFalse()
    {
        Assert.False(JSValue.Undefined.IsException);
    }

    #endregion

    #region ToBoolean Tests (JavaScript Coercion)

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToBoolean_Bool_ReturnsCorrectValue(bool input)
    {
        var value = JSValue.FromBoolean(input);
        Assert.Equal(input, value.ToBoolean());
    }

    [Fact]
    public void ToBoolean_Undefined_ReturnsFalse()
    {
        Assert.False(JSValue.Undefined.ToBoolean());
    }

    [Fact]
    public void ToBoolean_Null_ReturnsFalse()
    {
        Assert.False(JSValue.Null.ToBoolean());
    }

    [Fact]
    public void ToBoolean_Zero_ReturnsFalse()
    {
        Assert.False(JSValue.FromInt32(0).ToBoolean());
        Assert.False(JSValue.FromDouble(0.0).ToBoolean());
    }

    [Fact]
    public void ToBoolean_NonZeroNumber_ReturnsTrue()
    {
        Assert.True(JSValue.FromInt32(1).ToBoolean());
        Assert.True(JSValue.FromInt32(-1).ToBoolean());
        Assert.True(JSValue.FromDouble(0.1).ToBoolean());
    }

    [Fact]
    public void ToBoolean_NaN_ReturnsFalse()
    {
        Assert.False(JSValue.FromDouble(double.NaN).ToBoolean());
    }

    [Fact]
    public void ToBoolean_EmptyString_ReturnsFalse()
    {
        Assert.False(JSValue.FromString("").ToBoolean());
    }

    [Fact]
    public void ToBoolean_NonEmptyString_ReturnsTrue()
    {
        Assert.True(JSValue.FromString("hello").ToBoolean());
        Assert.True(JSValue.FromString(" ").ToBoolean()); // whitespace is truthy
    }

    #endregion

    #region TryGetInt32 / ToInt32 Tests

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TryGetInt32_IntValue_ReturnsTrue(int input)
    {
        var value = JSValue.FromInt32(input);
        Assert.True(value.TryGetInt32(out int result));
        Assert.Equal(input, result);
    }

    [Fact]
    public void TryGetInt32_WholeFloat_ReturnsTrue()
    {
        var value = JSValue.FromDouble(42.0);
        // Note: FromDouble(42.0) optimizes to Int, so this tests the Int path
        Assert.True(value.TryGetInt32(out int result));
        Assert.Equal(42, result);
    }

    [Fact]
    public void TryGetInt32_FractionalFloat_ReturnsFalse()
    {
        // Create a Float64 that can't be represented as int
        var value = JSValue.FromDouble(42.5);
        Assert.False(value.TryGetInt32(out _));
    }

    [Fact]
    public void TryGetInt32_String_ReturnsFalse()
    {
        var value = JSValue.FromString("42");
        Assert.False(value.TryGetInt32(out _));
    }

    [Fact]
    public void ToInt32_ValidInt_ReturnsValue()
    {
        var value = JSValue.FromInt32(42);
        Assert.Equal(42, value.ToInt32());
    }

    [Fact]
    public void ToInt32_InvalidType_ThrowsInvalidOperationException()
    {
        var value = JSValue.FromString("42");
        Assert.Throws<InvalidOperationException>(() => value.ToInt32());
    }

    #endregion

    #region TryGetDouble / ToDouble Tests

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    public void TryGetDouble_IntValue_ReturnsTrue(int input)
    {
        var value = JSValue.FromInt32(input);
        Assert.True(value.TryGetDouble(out double result));
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(3.14)]
    [InlineData(-3.14)]
    [InlineData(double.MaxValue)]
    public void TryGetDouble_FloatValue_ReturnsTrue(double input)
    {
        var value = JSValue.FromDouble(input);
        Assert.True(value.TryGetDouble(out double result));
        Assert.Equal(input, result);
    }

    [Fact]
    public void TryGetDouble_Bool_ReturnsTrue()
    {
        Assert.True(JSValue.True.TryGetDouble(out double trueResult));
        Assert.Equal(1.0, trueResult);

        Assert.True(JSValue.False.TryGetDouble(out double falseResult));
        Assert.Equal(0.0, falseResult);
    }

    [Fact]
    public void TryGetDouble_Null_ReturnsZero()
    {
        Assert.True(JSValue.Null.TryGetDouble(out double result));
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void TryGetDouble_Undefined_ReturnsNaN()
    {
        Assert.True(JSValue.Undefined.TryGetDouble(out double result));
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void TryGetDouble_String_ReturnsFalse()
    {
        var value = JSValue.FromString("3.14");
        Assert.False(value.TryGetDouble(out _));
    }

    [Fact]
    public void ToDouble_ValidNumber_ReturnsValue()
    {
        Assert.Equal(42.0, JSValue.FromInt32(42).ToDouble());
        Assert.Equal(3.14, JSValue.FromDouble(3.14).ToDouble());
    }

    [Fact]
    public void ToDouble_InvalidType_ThrowsInvalidOperationException()
    {
        var value = JSValue.FromString("3.14");
        Assert.Throws<InvalidOperationException>(() => value.ToDouble());
    }

    #endregion

    #region TryGetString Tests

    [Fact]
    public void TryGetString_StringValue_ReturnsTrue()
    {
        var value = JSValue.FromString("hello");
        Assert.True(value.TryGetString(out string? result));
        Assert.Equal("hello", result);
    }

    [Fact]
    public void TryGetString_NonString_ReturnsFalse()
    {
        Assert.False(JSValue.FromInt32(42).TryGetString(out _));
        Assert.False(JSValue.Undefined.TryGetString(out _));
        Assert.False(JSValue.Null.TryGetString(out _));
    }

    #endregion

    #region ToString Tests (JavaScript Conversion)

    [Fact]
    public void ToString_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", JSValue.Undefined.ToString());
    }

    [Fact]
    public void ToString_Null_ReturnsNull()
    {
        Assert.Equal("null", JSValue.Null.ToString());
    }

    [Fact]
    public void ToString_True_ReturnsTrue()
    {
        Assert.Equal("true", JSValue.True.ToString());
    }

    [Fact]
    public void ToString_False_ReturnsFalse()
    {
        Assert.Equal("false", JSValue.False.ToString());
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(-42, "-42")]
    public void ToString_Int_ReturnsNumberString(int input, string expected)
    {
        Assert.Equal(expected, JSValue.FromInt32(input).ToString());
    }

    [Fact]
    public void ToString_NaN_ReturnsNaN()
    {
        Assert.Equal("NaN", JSValue.FromDouble(double.NaN).ToString());
    }

    [Fact]
    public void ToString_Infinity_ReturnsInfinity()
    {
        Assert.Equal("Infinity", JSValue.FromDouble(double.PositiveInfinity).ToString());
        Assert.Equal("-Infinity", JSValue.FromDouble(double.NegativeInfinity).ToString());
    }

    [Fact]
    public void ToString_String_ReturnsString()
    {
        Assert.Equal("hello", JSValue.FromString("hello").ToString());
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameUndefined_ReturnsTrue()
    {
        Assert.True(JSValue.Undefined.Equals(JSValue.Undefined));
        Assert.True(JSValue.Undefined == JSValue.Undefined);
    }

    [Fact]
    public void Equals_SameNull_ReturnsTrue()
    {
        Assert.True(JSValue.Null.Equals(JSValue.Null));
        Assert.True(JSValue.Null == JSValue.Null);
    }

    [Fact]
    public void Equals_NullAndUndefined_ReturnsFalse()
    {
        // Strict equality: null !== undefined
        Assert.False(JSValue.Null.Equals(JSValue.Undefined));
        Assert.False(JSValue.Null == JSValue.Undefined);
    }

    [Fact]
    public void Equals_SameInt_ReturnsTrue()
    {
        var a = JSValue.FromInt32(42);
        var b = JSValue.FromInt32(42);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentInt_ReturnsFalse()
    {
        var a = JSValue.FromInt32(42);
        var b = JSValue.FromInt32(43);
        Assert.False(a.Equals(b));
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_SameDouble_ReturnsTrue()
    {
        var a = JSValue.FromDouble(3.14);
        var b = JSValue.FromDouble(3.14);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_SameString_ReturnsTrue()
    {
        var a = JSValue.FromString("hello");
        var b = JSValue.FromString("hello");
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentString_ReturnsFalse()
    {
        var a = JSValue.FromString("hello");
        var b = JSValue.FromString("world");
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_IntAndDouble_ReturnsFalse()
    {
        // Strict equality: tags must match
        var intVal = JSValue.FromInt32(42);
        var floatVal = JSValue.FromDouble(42.5); // 42.5 can't be int, so stays Float64

        Assert.False(intVal.Equals(floatVal));
    }

    [Fact]
    public void GetHashCode_EqualValues_ReturnsSameHash()
    {
        var a = JSValue.FromInt32(42);
        var b = JSValue.FromInt32(42);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var s1 = JSValue.FromString("test");
        var s2 = JSValue.FromString("test");
        Assert.Equal(s1.GetHashCode(), s2.GetHashCode());
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_Bool_Works()
    {
        JSValue value = true;
        Assert.True(value.IsBool);
        Assert.True(value.ToBoolean());

        value = false;
        Assert.True(value.IsBool);
        Assert.False(value.ToBoolean());
    }

    [Fact]
    public void ImplicitConversion_Int_Works()
    {
        JSValue value = 42;
        Assert.True(value.IsInt);
        Assert.Equal(42, value.ToInt32());
    }

    [Fact]
    public void ImplicitConversion_Double_Works()
    {
        JSValue value = 3.14;
        Assert.True(value.IsNumber);
        Assert.True(value.TryGetDouble(out double result));
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void ImplicitConversion_String_Works()
    {
        JSValue value = "hello";
        Assert.True(value.IsString);
        Assert.True(value.TryGetString(out string? result));
        Assert.Equal("hello", result);
    }

    #endregion
}
