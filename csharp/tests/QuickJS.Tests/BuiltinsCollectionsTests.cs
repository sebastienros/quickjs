// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsCollectionsTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsCollectionsTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void Map_BasicOperations()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var mapVal = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());
        var map = mapVal.AsObject();
        var proto = map.Prototype!;
        var setFn = (JSFunction)proto.Get("set").AsObject();
        var getFn = (JSFunction)proto.Get("get").AsObject();
        var hasFn = (JSFunction)proto.Get("has").AsObject();
        var sizeFn = (JSFunction)proto.Get("size").AsObject();

        setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("k"), JSValue.FromInt32(1) });
        Assert.True(hasFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("k") }).IsTrue);
        Assert.Equal(1, getFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("k") }).ToInt32());
        Assert.Equal(1, sizeFn.CallNative(JSValue.FromObject(map), System.Array.Empty<JSValue>()).ToInt32());
    }

    [Fact]
    public void Set_BasicOperations()
    {
        var setCtor = (JSFunction)_context.GetGlobalProperty("Set").AsObject();
        var setVal = setCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());
        var set = setVal.AsObject();
        var proto = set.Prototype!;
        var addFn = (JSFunction)proto.Get("add").AsObject();
        var hasFn = (JSFunction)proto.Get("has").AsObject();
        var sizeFn = (JSFunction)proto.Get("size").AsObject();

        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(5) });
        Assert.True(hasFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(5) }).IsTrue);
        Assert.Equal(1, sizeFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>()).ToInt32());
    }
}
