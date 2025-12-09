// Licensed under the MIT License.

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
}
