// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsRegExpTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsRegExpTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void RegExpExec_MatchesAndReturnsArray()
    {
        var reCtor = (JSFunction)_context.GetGlobalProperty("RegExp").AsObject();
        var reVal = reCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromString("(a)(b)"), JSValue.FromString("") });
        var re = reVal.AsObject();

        var execFn = (JSFunction)re.Get("exec").AsObject();
        var res = execFn.CallNative(JSValue.FromObject(re), new[] { JSValue.FromString("xabcy") }).AsObject();
        Assert.Equal("ab", res.Get(0).ToString());
        Assert.Equal("a", res.Get(1).ToString());
        Assert.Equal("b", res.Get(2).ToString());
        Assert.Equal(1, res.Get("index").ToInt32());
    }

    [Fact]
    public void RegExpTest_GlobalUpdatesLastIndex()
    {
        var reCtor = (JSFunction)_context.GetGlobalProperty("RegExp").AsObject();
        var reVal = reCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromString("a"), JSValue.FromString("g") });
        var re = reVal.AsObject();

        var testFn = (JSFunction)re.Get("test").AsObject();
        var input = JSValue.FromString("aa");
        var r1 = testFn.CallNative(JSValue.FromObject(re), new[] { input });
        Assert.True(r1.IsBool && r1.IsTrue);
        Assert.Equal(1, re.Get("lastIndex").ToInt32());
        var r2 = testFn.CallNative(JSValue.FromObject(re), new[] { input });
        Assert.True(r2.IsBool && r2.IsTrue);
        Assert.Equal(2, re.Get("lastIndex").ToInt32());
    }
}
