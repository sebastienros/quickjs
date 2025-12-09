// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsJsonTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsJsonTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void JsonParse_ParsesObject()
    {
        var json = _context.GetGlobalProperty("JSON").AsObject();
        var parse = (JSFunction)json.Get("parse").AsObject();
        var val = parse.CallNative(JSValue.Undefined, new[] { JSValue.FromString("{\"a\":1,\"b\":[2,3]}") });
        var obj = val.AsObject();
        Assert.Equal(1, obj.Get("a").ToInt32());
        var arr = obj.Get("b").AsObject();
        Assert.Equal(2, arr.Get(0).ToInt32());
        Assert.Equal(3, arr.Get(1).ToInt32());
    }

    [Fact]
    public void JsonStringify_SerializesPrimitivesAndObjects()
    {
        var json = _context.GetGlobalProperty("JSON").AsObject();
        var stringify = (JSFunction)json.Get("stringify").AsObject();

        var obj = new JSObject(null, JSClassId.Object);
        obj.Set("x", JSValue.FromInt32(5));
        obj.Set("y", JSValue.FromString("hi"));
        var arr = new JSObject(null, JSClassId.Array);
        arr.Set(0, JSValue.FromInt32(1));
        arr.Set(1, JSValue.FromInt32(2));
        obj.Set("z", JSValue.FromObject(arr));

        var jsonStr = stringify.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) }).ToString();
        Assert.Equal("{\"x\":5,\"y\":\"hi\",\"z\":[1,2]}", jsonStr);
    }
}
