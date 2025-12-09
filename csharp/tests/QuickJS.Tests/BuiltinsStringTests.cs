// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsStringTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsStringTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void StringConstructor_ReturnsPrimitive()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var result = stringCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(123) });
        Assert.True(result.IsString);
        Assert.Equal("123", result.ToString());
    }

    [Fact]
    public void NewString_ReturnsBoxedObjectWithLength()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var boxed = stringCtor.CallNative(JSValue.FromObject(new JSObject(_context.GetClassPrototype(JSClassId.String), JSClassId.String)), new[] { JSValue.FromString("abc") });
        Assert.True(boxed.IsObject);
        var obj = boxed.AsObject();
        Assert.Equal(JSClassId.String, obj.ClassId);
        Assert.Equal(3, obj.Get("length").ToInt32());
        Assert.Equal("b", obj.Get(1).ToString());
    }

    [Fact]
    public void StringPrototypeToString_WorksForWrapper()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var boxed = stringCtor.CallNative(JSValue.FromObject(new JSObject(_context.GetClassPrototype(JSClassId.String), JSClassId.String)), new[] { JSValue.FromString("xyz") });
        var obj = boxed.AsObject();
        var toStringFn = (JSFunction)obj.Prototype!.Get("toString").AsObject();
        var res = toStringFn.CallNative(JSValue.FromObject(obj), System.Array.Empty<JSValue>());
        Assert.Equal("xyz", res.ToString());
    }
}
