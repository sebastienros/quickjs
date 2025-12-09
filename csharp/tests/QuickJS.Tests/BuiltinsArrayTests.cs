// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsArrayTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsArrayTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void ArrayConstructor_WithElements_SetsLengthAndValues()
    {
        var arrayCtor = (JSFunction)_context.GetGlobalProperty("Array").AsObject();
        var arrVal = arrayCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        Assert.True(arrVal.IsObject);
        var arr = arrVal.AsObject();
        Assert.Equal(JSClassId.Array, arr.ClassId);
        Assert.Equal(3, arr.Get("length").ToInt32());
        Assert.Equal(2, arr.Get(1).ToInt32());
    }

    [Fact]
    public void ArrayConstructor_WithLength_CreatesSparseArray()
    {
        var arrayCtor = (JSFunction)_context.GetGlobalProperty("Array").AsObject();
        var arrVal = arrayCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(5) });
        var arr = arrVal.AsObject();
        Assert.Equal(5, arr.Get("length").ToInt32());
        Assert.True(arr.Get(0).IsUndefined);
    }

    [Fact]
    public void ArrayPushPop_Works()
    {
        var arrayCtor = (JSFunction)_context.GetGlobalProperty("Array").AsObject();
        var arr = arrayCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();
        var pushFn = (JSFunction)arr.Prototype!.Get("push").AsObject();
        var popFn = (JSFunction)arr.Prototype!.Get("pop").AsObject();

        var newLen = pushFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(10), JSValue.FromInt32(20) });
        Assert.Equal(2, newLen.ToInt32());
        Assert.Equal(2, arr.Get("length").ToInt32());
        Assert.Equal(20, arr.Get(1).ToInt32());

        var popped = popFn.CallNative(JSValue.FromObject(arr), System.Array.Empty<JSValue>());
        Assert.Equal(20, popped.ToInt32());
        Assert.Equal(1, arr.Get("length").ToInt32());
    }
}
