// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for BigInt type (Step 9.4).
/// </summary>
public class BigIntTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BigIntTests()
    {
        _context = _runtime.CreateContext();
    }

    #region JSBigInt Class Tests

    [Fact]
    public void JSBigInt_FromLong_CreatesValue()
    {
        var bigInt = new JSBigInt(42L);
        Assert.Equal(42L, bigInt.ToInt64());
    }

    [Fact]
    public void JSBigInt_Parse_DecimalString()
    {
        var bigInt = JSBigInt.Parse("12345678901234567890");
        Assert.Equal("12345678901234567890", bigInt.ToString(10));
    }

    [Fact]
    public void JSBigInt_Parse_HexString()
    {
        var bigInt = JSBigInt.Parse("0xFF", 16);
        Assert.Equal(255L, bigInt.ToInt64());
    }

    [Fact]
    public void JSBigInt_Parse_BinaryString()
    {
        var bigInt = JSBigInt.Parse("0b1010", 2);
        Assert.Equal(10L, bigInt.ToInt64());
    }

    [Fact]
    public void JSBigInt_Parse_OctalString()
    {
        var bigInt = JSBigInt.Parse("0o777", 8);
        Assert.Equal(511L, bigInt.ToInt64());
    }

    [Fact]
    public void JSBigInt_Add_Works()
    {
        var a = new JSBigInt(100L);
        var b = new JSBigInt(200L);
        var result = JSBigInt.Add(a, b);
        Assert.Equal(300L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Subtract_Works()
    {
        var a = new JSBigInt(100L);
        var b = new JSBigInt(30L);
        var result = JSBigInt.Subtract(a, b);
        Assert.Equal(70L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Multiply_Works()
    {
        var a = new JSBigInt(7L);
        var b = new JSBigInt(6L);
        var result = JSBigInt.Multiply(a, b);
        Assert.Equal(42L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Divide_Works()
    {
        var a = new JSBigInt(100L);
        var b = new JSBigInt(3L);
        var result = JSBigInt.Divide(a, b);
        Assert.Equal(33L, result.ToInt64()); // Truncated toward zero
    }

    [Fact]
    public void JSBigInt_Mod_Works()
    {
        var a = new JSBigInt(100L);
        var b = new JSBigInt(3L);
        var result = JSBigInt.Mod(a, b);
        Assert.Equal(1L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Pow_Works()
    {
        var baseVal = new JSBigInt(2L);
        var result = JSBigInt.Pow(baseVal, 10);
        Assert.Equal(1024L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Neg_Works()
    {
        var a = new JSBigInt(42L);
        var result = JSBigInt.Neg(a);
        Assert.Equal(-42L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_And_Works()
    {
        var a = new JSBigInt(0b1100L);
        var b = new JSBigInt(0b1010L);
        var result = JSBigInt.And(a, b);
        Assert.Equal(0b1000L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Or_Works()
    {
        var a = new JSBigInt(0b1100L);
        var b = new JSBigInt(0b1010L);
        var result = JSBigInt.Or(a, b);
        Assert.Equal(0b1110L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Xor_Works()
    {
        var a = new JSBigInt(0b1100L);
        var b = new JSBigInt(0b1010L);
        var result = JSBigInt.Xor(a, b);
        Assert.Equal(0b0110L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Not_Works()
    {
        var a = new JSBigInt(0L);
        var result = JSBigInt.Not(a);
        Assert.Equal(-1L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_LeftShift_Works()
    {
        var a = new JSBigInt(1L);
        var result = JSBigInt.LeftShift(a, 4);
        Assert.Equal(16L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_RightShift_Works()
    {
        var a = new JSBigInt(16L);
        var result = JSBigInt.RightShift(a, 2);
        Assert.Equal(4L, result.ToInt64());
    }

    [Fact]
    public void JSBigInt_Compare_Works()
    {
        var a = new JSBigInt(100L);
        var b = new JSBigInt(200L);
        var c = new JSBigInt(100L);
        Assert.True(JSBigInt.Compare(a, b) < 0);
        Assert.True(JSBigInt.Compare(b, a) > 0);
        Assert.Equal(0, JSBigInt.Compare(a, c));
    }

    [Fact]
    public void JSBigInt_Equals_Works()
    {
        var a = new JSBigInt(42L);
        var b = new JSBigInt(42L);
        var c = new JSBigInt(43L);
        Assert.True(JSBigInt.Equals(a, b));
        Assert.False(JSBigInt.Equals(a, c));
    }

    [Fact]
    public void JSBigInt_ToString_Radix10()
    {
        var a = new JSBigInt(255L);
        Assert.Equal("255", a.ToString(10));
    }

    [Fact]
    public void JSBigInt_ToString_Radix16()
    {
        var a = new JSBigInt(255L);
        Assert.Equal("ff", a.ToString(16));
    }

    [Fact]
    public void JSBigInt_ToString_Radix2()
    {
        var a = new JSBigInt(10L);
        Assert.Equal("1010", a.ToString(2));
    }

    [Fact]
    public void JSBigInt_AsIntN_Works()
    {
        // AsIntN wraps to signed range
        var large = new JSBigInt(128L); // 0x80
        var result = JSBigInt.AsIntN(8, large);
        Assert.Equal(-128L, result.ToInt64()); // Wraps to -128 in 8-bit signed
    }

    [Fact]
    public void JSBigInt_AsUintN_Works()
    {
        // AsUintN masks to unsigned range
        var neg = new JSBigInt(-1L);
        var result = JSBigInt.AsUintN(8, neg);
        Assert.Equal(255L, result.ToInt64()); // 0xFF
    }

    #endregion

    #region JSValue BigInt Integration

    [Fact]
    public void JSValue_FromBigInt_Long_CreatesShortBigInt()
    {
        var val = JSValue.FromBigInt(42L);
        Assert.True(val.IsBigInt);
        Assert.Equal(42L, val.AsBigInt().ToInt64());
    }

    [Fact]
    public void JSValue_FromBigInt_JSBigInt_Works()
    {
        var bigInt = new JSBigInt(123L);
        var val = JSValue.FromBigInt(bigInt);
        Assert.True(val.IsBigInt);
        Assert.Equal(123L, val.AsBigInt().ToInt64());
    }

    [Fact]
    public void JSValue_IsBigInt_TrueForBigInt()
    {
        var val = JSValue.FromBigInt(42L);
        Assert.True(val.IsBigInt);
        Assert.False(val.IsInt);
        Assert.False(val.IsNumber);
    }

    #endregion

    #region BigInt Global Function Tests

    [Fact]
    public void BigInt_GlobalFunction_Exists()
    {
        Assert.True(_context.HasGlobalProperty("BigInt"));
        var bigIntVal = _context.GetGlobalProperty("BigInt");
        Assert.True(bigIntVal.IsObject);
        Assert.IsType<JSFunction>(bigIntVal.AsObject());
    }

    [Fact]
    public void BigInt_FromInteger_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
        Assert.True(result.IsBigInt);
        Assert.Equal(42L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_FromString_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("123") });
        Assert.True(result.IsBigInt);
        Assert.Equal(123L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_FromHexString_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("0xff") });
        Assert.True(result.IsBigInt);
        Assert.Equal(255L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_FromBoolean_True_ReturnsOne()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.True });
        Assert.True(result.IsBigInt);
        Assert.Equal(1L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_FromBoolean_False_ReturnsZero()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.False });
        Assert.True(result.IsBigInt);
        Assert.Equal(0L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_NoArgs_ThrowsTypeError()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());
        Assert.True(result.IsException);
        Assert.True(_context.HasException);
    }

    [Fact]
    public void BigInt_FromNaN_ThrowsRangeError()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(double.NaN) });
        Assert.True(result.IsException);
        Assert.True(_context.HasException);
    }

    [Fact]
    public void BigInt_FromInfinity_ThrowsRangeError()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(double.PositiveInfinity) });
        Assert.True(result.IsException);
        Assert.True(_context.HasException);
    }

    [Fact]
    public void BigInt_FromNonInteger_ThrowsRangeError()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(3.14) });
        Assert.True(result.IsException);
        Assert.True(_context.HasException);
    }

    #endregion

    #region BigInt Static Methods

    [Fact]
    public void BigInt_AsIntN_StaticMethod_Exists()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var asIntNVal = bigIntFn.Get("asIntN");
        Assert.True(asIntNVal.IsObject);
        Assert.IsType<JSFunction>(asIntNVal.AsObject());
    }

    [Fact]
    public void BigInt_AsIntN_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var asIntN = (JSFunction)bigIntFn.Get("asIntN").AsObject();
        
        var bigInt = JSValue.FromBigInt(128L);
        var result = asIntN.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(8), bigInt });
        Assert.True(result.IsBigInt);
        Assert.Equal(-128L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void BigInt_AsUintN_StaticMethod_Exists()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var asUintNVal = bigIntFn.Get("asUintN");
        Assert.True(asUintNVal.IsObject);
        Assert.IsType<JSFunction>(asUintNVal.AsObject());
    }

    [Fact]
    public void BigInt_AsUintN_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var asUintN = (JSFunction)bigIntFn.Get("asUintN").AsObject();
        
        var bigInt = JSValue.FromBigInt(-1L);
        var result = asUintN.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(8), bigInt });
        Assert.True(result.IsBigInt);
        Assert.Equal(255L, result.AsBigInt().ToInt64());
    }

    #endregion

    #region BigInt Prototype Methods

    [Fact]
    public void BigInt_Prototype_ToString_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(255) });
        
        // Get prototype.toString
        var proto = _context.GetClassPrototype(JSClassId.BigInt);
        Assert.NotNull(proto);
        var toStringFn = (JSFunction)proto.Get("toString").AsObject();
        
        var str = toStringFn.CallNative(result, System.Array.Empty<JSValue>());
        Assert.True(str.IsString);
        Assert.Equal("255", str.ToString());
    }

    [Fact]
    public void BigInt_Prototype_ToString_WithRadix16()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var result = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(255) });
        
        var proto = _context.GetClassPrototype(JSClassId.BigInt);
        var toStringFn = (JSFunction)proto!.Get("toString").AsObject();
        
        var str = toStringFn.CallNative(result, new[] { JSValue.FromInt32(16) });
        Assert.True(str.IsString);
        Assert.Equal("ff", str.ToString());
    }

    [Fact]
    public void BigInt_Prototype_ValueOf_Works()
    {
        var bigIntFn = (JSFunction)_context.GetGlobalProperty("BigInt").AsObject();
        var bigInt = bigIntFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
        
        var proto = _context.GetClassPrototype(JSClassId.BigInt);
        var valueOfFn = (JSFunction)proto!.Get("valueOf").AsObject();
        
        var value = valueOfFn.CallNative(bigInt, System.Array.Empty<JSValue>());
        Assert.True(value.IsBigInt);
        Assert.Equal(42L, value.AsBigInt().ToInt64());
    }

    #endregion

    #region BigInt Evaluation Tests

    [Fact]
    public void Eval_BigIntLiteral_ReturnsBigInt()
    {
        // Use variable to capture BigInt value (expression statement completion values not yet supported)
        var evalResult = _context.Evaluate("var bigIntResult = 123n;");
        if (_context.HasException)
        {
            var exc = _context.CurrentException;
            var excObj = exc.IsObject ? exc.AsObject() : null;
            var msg = excObj?.Get("message").ToString() ?? exc.ToString();
            var stack = excObj?.Get("stack").ToString() ?? "";
            Assert.Fail($"Exception during eval: {msg}\nStack: {stack}");
        }
        Assert.False(evalResult.IsException, "Evaluate returned exception");
        
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt, $"Expected BigInt but got type tag {result.Tag}, value: {result}");
        Assert.Equal(123L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntHexLiteral_ReturnsBigInt()
    {
        _context.Evaluate("var bigIntResult = 0xFFn;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(255L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntBinaryLiteral_ReturnsBigInt()
    {
        _context.Evaluate("var bigIntResult = 0b1010n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(10L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntOctalLiteral_ReturnsBigInt()
    {
        _context.Evaluate("var bigIntResult = 0o777n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(511L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntAdd_Works()
    {
        _context.Evaluate("var bigIntResult = 10n + 5n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(15L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntSubtract_Works()
    {
        _context.Evaluate("var bigIntResult = 10n - 3n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(7L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntMultiply_Works()
    {
        _context.Evaluate("var bigIntResult = 7n * 6n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(42L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntDivide_Works()
    {
        _context.Evaluate("var bigIntResult = 100n / 3n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(33L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntMod_Works()
    {
        _context.Evaluate("var bigIntResult = 100n % 7n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(2L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntPow_Works()
    {
        _context.Evaluate("var bigIntResult = 2n ** 10n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(1024L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntNegate_Works()
    {
        _context.Evaluate("var bigIntResult = -42n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(-42L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntBitwiseAnd_Works()
    {
        _context.Evaluate("var bigIntResult = 12n & 10n;"); // 0b1100 & 0b1010 = 0b1000 = 8
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(8L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntBitwiseOr_Works()
    {
        _context.Evaluate("var bigIntResult = 12n | 10n;"); // 0b1100 | 0b1010 = 0b1110 = 14
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(14L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntBitwiseXor_Works()
    {
        _context.Evaluate("var bigIntResult = 12n ^ 10n;"); // 0b1100 ^ 0b1010 = 0b0110 = 6
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(6L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntBitwiseNot_Works()
    {
        _context.Evaluate("var bigIntResult = ~0n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(-1L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntLeftShift_Works()
    {
        _context.Evaluate("var bigIntResult = 1n << 4n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(16L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntRightShift_Works()
    {
        _context.Evaluate("var bigIntResult = 16n >> 2n;");
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(4L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntComparison_LessThan()
    {
        _context.Evaluate("var compareResult = 5n < 10n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntComparison_GreaterThan()
    {
        _context.Evaluate("var compareResult = 10n > 5n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntComparison_LessOrEqual()
    {
        _context.Evaluate("var compareResult = 5n <= 5n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntComparison_GreaterOrEqual()
    {
        _context.Evaluate("var compareResult = 5n >= 5n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntEquality_StrictEqual()
    {
        _context.Evaluate("var compareResult = 5n === 5n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntEquality_StrictNotEqual()
    {
        _context.Evaluate("var compareResult = 5n !== 10n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_BigIntEquality_LooseEqual_SameValue()
    {
        _context.Evaluate("var compareResult = 5n == 5n;");
        var result = _context.GetGlobalProperty("compareResult");
        Assert.True(result.IsBool);
        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Eval_TypeOf_BigInt_ReturnsBigint()
    {
        _context.Evaluate("var typeResult = typeof 123n;");
        var result = _context.GetGlobalProperty("typeResult");
        Assert.True(result.IsString);
        Assert.Equal("bigint", result.ToString());
    }

    [Fact]
    public void Eval_BigIntVariable_Works()
    {
        _context.Evaluate("var x = 42n;");
        var x = _context.GetGlobalProperty("x");
        Assert.True(x.IsBigInt);
        Assert.Equal(42L, x.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntExpression_InVariable()
    {
        _context.Evaluate("var y = 10n + 20n * 2n;");
        var y = _context.GetGlobalProperty("y");
        Assert.True(y.IsBigInt);
        Assert.Equal(50L, y.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntFunction_Works()
    {
        _context.Evaluate(@"
            function double(n) { return n * 2n; }
            var result = double(21n);
        ");
        var result = _context.GetGlobalProperty("result");
        Assert.True(result.IsBigInt);
        Assert.Equal(42L, result.AsBigInt().ToInt64());
    }

    [Fact]
    public void Eval_BigIntMixedWithNumber_ThrowsTypeError()
    {
        var result = _context.Evaluate("var x; try { x = 1n + 1; } catch(e) { x = 'error'; }");
        var x = _context.GetGlobalProperty("x");
        Assert.Equal("error", x.ToString());
    }

    #endregion

    #region Large BigInt Tests

    [Fact]
    public void JSBigInt_LargeValue_WorksCorrectly()
    {
        // Value larger than long.MaxValue
        var bigInt = JSBigInt.Parse("9999999999999999999999999999999999999999");
        Assert.Equal("9999999999999999999999999999999999999999", bigInt.ToString(10));
    }

    [Fact]
    public void Eval_LargeBigInt_Arithmetic()
    {
        _context.Evaluate("var bigIntResult = 9007199254740993n + 1n;"); // MAX_SAFE_INTEGER + 2
        var result = _context.GetGlobalProperty("bigIntResult");
        Assert.True(result.IsBigInt);
        Assert.Equal(9007199254740994L, result.AsBigInt().ToInt64());
    }

    #endregion
}
