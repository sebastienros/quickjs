// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

public class BuiltinsNumberMathTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsNumberMathTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void NumberConstructor_ReturnsPrimitive()
    {
        var numberCtor = (JSFunction)_context.GetGlobalProperty("Number").AsObject();
        var result = numberCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromString("42") });
        Assert.True(result.IsNumber);
        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void NewNumber_ReturnsBoxedObject()
    {
        var numberCtor = (JSFunction)_context.GetGlobalProperty("Number").AsObject();
        // Simulate constructor call by providing an object as thisArg
        var boxed = numberCtor.CallNative(JSValue.FromObject(new JSObject(_context.GetClassPrototype(JSClassId.Number), JSClassId.Number)), new[] { JSValue.FromDouble(3.14) });
        Assert.True(boxed.IsObject);
        var obj = boxed.AsObject();
        Assert.Equal(JSClassId.Number, obj.ClassId);
        Assert.Equal(3.14, obj.InternalValue.ToDouble(), 3);
    }

    [Fact]
    public void MathFunctions_Work()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        double CallMath(string name, params JSValue[] args)
        {
            var fn = (JSFunction)math.Get(name).AsObject();
            var res = fn.CallNative(JSValue.Undefined, args);
            return res.ToDouble();
        }

        Assert.Equal(5, CallMath("abs", JSValue.FromInt32(-5)));
        Assert.Equal(2, CallMath("floor", JSValue.FromDouble(2.9)));
        Assert.Equal(3, CallMath("ceil", JSValue.FromDouble(2.1)));
        Assert.Equal(3, CallMath("round", JSValue.FromDouble(2.6)));
        Assert.Equal(7, CallMath("max", JSValue.FromInt32(3), JSValue.FromInt32(7)));
        Assert.Equal(3, CallMath("min", JSValue.FromInt32(3), JSValue.FromInt32(7)));
        Assert.Equal(9, CallMath("pow", JSValue.FromInt32(3), JSValue.FromInt32(2)));
    }

    [Fact]
    public void MathRandom_ReturnsInRange()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var randFn = (JSFunction)math.Get("random").AsObject();
        var v = randFn.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).ToDouble();
        Assert.InRange(v, 0.0, 1.0);
    }

    [Fact]
    public void MathSinCos_CalculatesTrigValues()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var sinFn = (JSFunction)math.Get("sin").AsObject();
        var cosFn = (JSFunction)math.Get("cos").AsObject();

        var sinVal = sinFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(Math.PI / 2) }).ToDouble();
        Assert.Equal(1.0, sinVal, 10);

        var cosVal = cosFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(1.0, cosVal, 10);
    }

    [Fact]
    public void MathTan_CalculatesTangent()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var tanFn = (JSFunction)math.Get("tan").AsObject();

        var tanVal = tanFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(0.0, tanVal, 10);
    }

    [Fact]
    public void MathAsin_CalculatesArcSin()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var asinFn = (JSFunction)math.Get("asin").AsObject();

        var asinVal = asinFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(1) }).ToDouble();
        Assert.Equal(Math.PI / 2, asinVal, 10);
    }

    [Fact]
    public void MathAcos_CalculatesArcCos()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var acosFn = (JSFunction)math.Get("acos").AsObject();

        var acosVal = acosFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(1) }).ToDouble();
        Assert.Equal(0.0, acosVal, 10);
    }

    [Fact]
    public void MathAtan_CalculatesArcTan()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var atanFn = (JSFunction)math.Get("atan").AsObject();

        var atanVal = atanFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(0.0, atanVal, 10);
    }

    [Fact]
    public void MathAtan2_CalculatesArcTan2()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var atan2Fn = (JSFunction)math.Get("atan2").AsObject();

        var atan2Val = atan2Fn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(1), JSValue.FromDouble(1) }).ToDouble();
        Assert.Equal(Math.PI / 4, atan2Val, 10);
    }

    [Fact]
    public void MathExp_CalculatesExponential()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var expFn = (JSFunction)math.Get("exp").AsObject();

        var expVal = expFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(1) }).ToDouble();
        Assert.Equal(Math.E, expVal, 10);
    }

    [Fact]
    public void MathLog_CalculatesNaturalLogarithm()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var logFn = (JSFunction)math.Get("log").AsObject();

        var logVal = logFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(Math.E) }).ToDouble();
        Assert.Equal(1.0, logVal, 10);
    }

    [Fact]
    public void MathLog10_CalculatesLog10()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var log10Fn = (JSFunction)math.Get("log10").AsObject();

        var log10Val = log10Fn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(100) }).ToDouble();
        Assert.Equal(2.0, log10Val, 10);
    }

    [Fact]
    public void MathLog2_CalculatesLog2()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var log2Fn = (JSFunction)math.Get("log2").AsObject();

        var log2Val = log2Fn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(8) }).ToDouble();
        Assert.Equal(3.0, log2Val, 10);
    }

    [Fact]
    public void MathSqrt_CalculatesSquareRoot()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var sqrtFn = (JSFunction)math.Get("sqrt").AsObject();

        var sqrtVal = sqrtFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(16) }).ToDouble();
        Assert.Equal(4.0, sqrtVal, 10);
    }

    [Fact]
    public void MathCbrt_CalculatesCubeRoot()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var cbrtFn = (JSFunction)math.Get("cbrt").AsObject();

        var cbrtVal = cbrtFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(27) }).ToDouble();
        Assert.Equal(3.0, cbrtVal, 10);
    }

    [Fact]
    public void MathHypot_CalculatesHypotenuse()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var hypotFn = (JSFunction)math.Get("hypot").AsObject();

        var hypotVal = hypotFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(3), JSValue.FromDouble(4) }).ToDouble();
        Assert.Equal(5.0, hypotVal, 10);
    }

    [Fact]
    public void MathTrunc_TruncatesTowardZero()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var truncFn = (JSFunction)math.Get("trunc").AsObject();

        Assert.Equal(4.0, truncFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(4.7) }).ToDouble(), 10);
        Assert.Equal(-4.0, truncFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(-4.7) }).ToDouble(), 10);
    }

    [Fact]
    public void MathSign_ReturnsSign()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var signFn = (JSFunction)math.Get("sign").AsObject();

        Assert.Equal(1.0, signFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(42) }).ToDouble(), 10);
        Assert.Equal(-1.0, signFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(-42) }).ToDouble(), 10);
        Assert.Equal(0.0, signFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble(), 10);
    }

    [Fact]
    public void MathClz32_CountsLeadingZeros()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var clz32Fn = (JSFunction)math.Get("clz32").AsObject();

        Assert.Equal(31, clz32Fn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(1) }).ToInt32());
        // clz32(0) should return 32 (all zeros)
        Assert.Equal(32, clz32Fn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(0) }).ToInt32());
    }

    [Fact]
    public void MathImul_MultipliesAs32BitIntegers()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var imulFn = (JSFunction)math.Get("imul").AsObject();

        Assert.Equal(6, imulFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(2), JSValue.FromInt32(3) }).ToInt32());
    }

    [Fact]
    public void MathConstants_AreCorrect()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();

        Assert.Equal(Math.E, math.Get("E").ToDouble(), 10);
        Assert.Equal(Math.PI, math.Get("PI").ToDouble(), 10);
        Assert.Equal(Math.Log(10), math.Get("LN10").ToDouble(), 10);
        Assert.Equal(Math.Log(2), math.Get("LN2").ToDouble(), 10);
        Assert.Equal(1.0 / Math.Log(2), math.Get("LOG2E").ToDouble(), 10);
        Assert.Equal(1.0 / Math.Log(10), math.Get("LOG10E").ToDouble(), 10);
        Assert.Equal(1.0 / Math.Sqrt(2), math.Get("SQRT1_2").ToDouble(), 10);
        Assert.Equal(Math.Sqrt(2), math.Get("SQRT2").ToDouble(), 10);
    }

    [Fact]
    public void MathSinh_CalculatesHyperbolicSine()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var sinhFn = (JSFunction)math.Get("sinh").AsObject();

        var sinhVal = sinhFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(0.0, sinhVal, 10);
    }

    [Fact]
    public void MathCosh_CalculatesHyperbolicCosine()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var coshFn = (JSFunction)math.Get("cosh").AsObject();

        var coshVal = coshFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(1.0, coshVal, 10);
    }

    [Fact]
    public void MathTanh_CalculatesHyperbolicTangent()
    {
        var math = _context.GetGlobalProperty("Math").AsObject();
        var tanhFn = (JSFunction)math.Get("tanh").AsObject();

        var tanhVal = tanhFn.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(0) }).ToDouble();
        Assert.Equal(0.0, tanhVal, 10);
    }
}
