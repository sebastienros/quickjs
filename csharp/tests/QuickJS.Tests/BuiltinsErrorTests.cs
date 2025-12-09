// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsErrorTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsErrorTests()
    {
        _context = _runtime.CreateContext();
    }

    [Theory]
    [InlineData(JSErrorType.Error, "Error")]
    [InlineData(JSErrorType.TypeError, "TypeError")]
    [InlineData(JSErrorType.RangeError, "RangeError")]
    [InlineData(JSErrorType.ReferenceError, "ReferenceError")]
    [InlineData(JSErrorType.SyntaxError, "SyntaxError")]
    public void ErrorConstructors_CreateObjectsWithNameAndMessage(JSErrorType type, string name)
    {
        var ctorVal = _context.GetGlobalProperty(name);
        Assert.True(ctorVal.IsObject);
        var ctor = (JSFunction)ctorVal.AsObject();
        var msg = "boom";
        var errVal = ctor.CallNative(JSValue.Undefined, new[] { JSValue.FromString(msg) });
        Assert.True(errVal.IsObject);
        var err = errVal.AsObject();

        Assert.Equal(name, err.Get("name").ToString());
        Assert.Equal(msg, err.Get("message").ToString());
    }

    [Fact]
    public void ThrowTypeError_UsesTypeErrorPrototype()
    {
        var ex = _context.ThrowTypeError("bad");
        Assert.True(ex.IsException);
        var errVal = _context.GetAndClearException();
        Assert.True(errVal.IsObject);
        var err = errVal.AsObject();
        Assert.Equal("TypeError", err.Get("name").ToString());
        Assert.Equal("bad", err.Get("message").ToString());
    }
}
