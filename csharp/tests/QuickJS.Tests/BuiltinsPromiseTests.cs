// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsPromiseTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsPromiseTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void PromiseResolve_Fulfills()
    {
        var promiseCtor = (JSFunction)_context.GetGlobalProperty("Promise").AsObject();
        var resolveCalled = false;

        JSValue Executor(JSValue thisVal, JSValue[] args)
        {
            var resolve = (JSFunction)args[0].AsObject();
            resolveCalled = true;
            resolve.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
            return JSValue.Undefined;
        }

        var pVal = promiseCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction(Executor, "", 2, _context.GetClassPrototype(JSClassId.CFunction))) });
        _context.RunMicrotasks();

        var thenFn = (JSFunction)pVal.AsObject().Prototype!.Get("then").AsObject();
        JSValue result = JSValue.Undefined;
        var thenPromise = thenFn.CallNative(pVal, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, _context.GetClassPrototype(JSClassId.CFunction))) });
        _context.RunMicrotasks();

        Assert.True(resolveCalled);
        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void PromiseReject_PropagatesToCatch()
    {
        var promiseCtor = (JSFunction)_context.GetGlobalProperty("Promise").AsObject();

        JSValue Executor(JSValue thisVal, JSValue[] args)
        {
            var reject = (JSFunction)args[1].AsObject();
            reject.CallNative(JSValue.Undefined, new[] { JSValue.FromString("err") });
            return JSValue.Undefined;
        }

        var pVal = promiseCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction(Executor, "", 2, _context.GetClassPrototype(JSClassId.CFunction))) });
        _context.RunMicrotasks();

        var catchFn = (JSFunction)pVal.AsObject().Prototype!.Get("catch").AsObject();
        JSValue result = JSValue.Undefined;
        var p2 = catchFn.CallNative(pVal, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, _context.GetClassPrototype(JSClassId.CFunction))) });
        _context.RunMicrotasks();

        Assert.Equal("err", result.ToString());
    }
}
