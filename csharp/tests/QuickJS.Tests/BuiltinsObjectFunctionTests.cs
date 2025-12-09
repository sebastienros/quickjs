// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsObjectFunctionTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsObjectFunctionTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void ObjectConstructor_CreatesPlainObject()
    {
        var objCtorVal = _context.GetGlobalProperty("Object");
        Assert.True(objCtorVal.IsObject);
        var objCtor = (JSFunction)objCtorVal.AsObject();

        var result = objCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>());
        Assert.True(result.IsObject);
        var obj = result.AsObject();
        Assert.Same(_context.GetClassPrototype(JSClassId.Object), obj.Prototype);
    }

    [Fact]
    public void ObjectCreate_SetsPrototype()
    {
        var objCtor = (JSFunction)_context.GetGlobalProperty("Object").AsObject();
        var create = (JSFunction)objCtor.Get("create").AsObject();

        var proto = new JSObject(null, JSClassId.Object);
        var newObj = create.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(proto) });
        Assert.True(newObj.IsObject);
        Assert.Same(proto, newObj.AsObject().Prototype);
    }

    [Fact]
    public void ObjectPrototype_HasOwnProperty_Works()
    {
        var objCtor = (JSFunction)_context.GetGlobalProperty("Object").AsObject();
        var o = objCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();
        o.Set("x", JSValue.FromInt32(1));

        var hasOwn = (JSFunction)o.Prototype!.Get("hasOwnProperty").AsObject();
        var resultTrue = hasOwn.CallNative(JSValue.FromObject(o), new[] { JSValue.FromString("x") });
        var resultFalse = hasOwn.CallNative(JSValue.FromObject(o), new[] { JSValue.FromString("y") });
        Assert.True(resultTrue.IsBool && resultTrue.IsTrue);
        Assert.True(resultFalse.IsBool && resultFalse.IsFalse);
    }

    [Fact]
    public void FunctionConstructor_ThrowsNotSupported()
    {
        var funcCtor = (JSFunction)_context.GetGlobalProperty("Function").AsObject();
        var result = funcCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromString("return 1;") });
        Assert.True(result.IsException);
        Assert.True(_context.HasException);
    }
}
