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

    private JSFunction GetPromiseCtor() => (JSFunction)_context.GetGlobalProperty("Promise").AsObject();
    
    private JSObject GetFunctionProto() => _context.GetClassPrototype(JSClassId.CFunction)!;

    private JSObject CreateResolvedPromise(JSValue value)
    {
        var promiseCtor = GetPromiseCtor();
        var resolveFn = (JSFunction)promiseCtor.Get("resolve").AsObject();
        return resolveFn.CallNative(JSValue.FromObject(promiseCtor), new[] { value }).AsObject();
    }

    private JSObject CreateRejectedPromise(JSValue reason)
    {
        var promiseCtor = GetPromiseCtor();
        var rejectFn = (JSFunction)promiseCtor.Get("reject").AsObject();
        return rejectFn.CallNative(JSValue.FromObject(promiseCtor), new[] { reason }).AsObject();
    }

    private JSObject CreateArray(params JSValue[] values)
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var arr = new JSObject(arrayProto, JSClassId.Array);
        for (var i = 0; i < values.Length; i++)
        {
            // Use uint index version which updates ArrayLength
            arr.Set((uint)i, values[i]);
        }
        return arr;
    }

    [Fact]
    public void PromiseResolve_Fulfills()
    {
        var promiseCtor = GetPromiseCtor();
        var resolveCalled = false;

        JSValue Executor(JSValue thisVal, JSValue[] args)
        {
            var resolve = (JSFunction)args[0].AsObject();
            resolveCalled = true;
            resolve.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
            return JSValue.Undefined;
        }

        var pVal = promiseCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction(Executor, "", 2, GetFunctionProto())) });
        _context.RunMicrotasks();

        var thenFn = (JSFunction)pVal.AsObject().Prototype!.Get("then").AsObject();
        JSValue result = JSValue.Undefined;
        var thenPromise = thenFn.CallNative(pVal, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.True(resolveCalled);
        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void PromiseReject_PropagatesToCatch()
    {
        var promiseCtor = GetPromiseCtor();

        JSValue Executor(JSValue thisVal, JSValue[] args)
        {
            var reject = (JSFunction)args[1].AsObject();
            reject.CallNative(JSValue.Undefined, new[] { JSValue.FromString("err") });
            return JSValue.Undefined;
        }

        var pVal = promiseCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction(Executor, "", 2, GetFunctionProto())) });
        _context.RunMicrotasks();

        var catchFn = (JSFunction)pVal.AsObject().Prototype!.Get("catch").AsObject();
        JSValue result = JSValue.Undefined;
        var p2 = catchFn.CallNative(pVal, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.Equal("err", result.ToString());
    }

    // ==================== Promise.all Tests ====================

    [Fact]
    public void PromiseAll_SingleNonPromise_Resolves()
    {
        var promiseCtor = GetPromiseCtor();
        var allFn = (JSFunction)promiseCtor.Get("all").AsObject();
        
        // Single non-promise value
        var arr = CreateArray(JSValue.FromInt32(42));
        
        var resultP = allFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(1, resultArr.Get("length").ToInt32());
        Assert.Equal(42, resultArr.Get(0u).ToInt32());
    }

    [Fact]
    public void PromiseAll_EmptyArray_ResolvesImmediately()
    {
        var promiseCtor = GetPromiseCtor();
        var allFn = (JSFunction)promiseCtor.Get("all").AsObject();
        
        var emptyArray = CreateArray();
        var resultP = allFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(emptyArray) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(0, resultArr.Get("length").ToInt32());
    }

    [Fact]
    public void PromiseAll_AllResolve_ResolvesWithArray()
    {
        var promiseCtor = GetPromiseCtor();
        var allFn = (JSFunction)promiseCtor.Get("all").AsObject();
        
        var p1 = CreateResolvedPromise(JSValue.FromInt32(1));
        var p2 = CreateResolvedPromise(JSValue.FromInt32(2));
        var p3 = CreateResolvedPromise(JSValue.FromInt32(3));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2), JSValue.FromObject(p3));
        
        var resultP = allFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(3, resultArr.Get("length").ToInt32());
        Assert.Equal(1, resultArr.Get(0u).ToInt32());
        Assert.Equal(2, resultArr.Get(1u).ToInt32());
        Assert.Equal(3, resultArr.Get(2u).ToInt32());
    }

    [Fact]
    public void PromiseAll_OneRejects_RejectsWithReason()
    {
        var promiseCtor = GetPromiseCtor();
        var allFn = (JSFunction)promiseCtor.Get("all").AsObject();
        
        var p1 = CreateResolvedPromise(JSValue.FromInt32(1));
        var p2 = CreateRejectedPromise(JSValue.FromString("error"));
        var p3 = CreateResolvedPromise(JSValue.FromInt32(3));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2), JSValue.FromObject(p3));
        
        var resultP = allFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue error = JSValue.Undefined;
        var catchFn = (JSFunction)resultP.AsObject().Prototype!.Get("catch").AsObject();
        catchFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { error = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.Equal("error", error.ToString());
    }

    [Fact]
    public void PromiseAll_NonPromiseValues_WrapsAsResolved()
    {
        var promiseCtor = GetPromiseCtor();
        var allFn = (JSFunction)promiseCtor.Get("all").AsObject();
        
        // Mix of promises and plain values
        var p1 = CreateResolvedPromise(JSValue.FromInt32(1));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromInt32(42), JSValue.FromString("hello"));
        
        var resultP = allFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Run again in case chained

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Run again in case chained

        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(3, resultArr.Get("length").ToInt32());
        Assert.Equal(1, resultArr.Get(0u).ToInt32());
        Assert.Equal(42, resultArr.Get(1u).ToInt32());
        Assert.Equal("hello", resultArr.Get(2u).ToString());
    }

    // ==================== Promise.race Tests ====================

    [Fact]
    public void PromiseRace_FirstResolved_Wins()
    {
        var promiseCtor = GetPromiseCtor();
        var raceFn = (JSFunction)promiseCtor.Get("race").AsObject();
        
        var p1 = CreateResolvedPromise(JSValue.FromInt32(1));
        var p2 = CreateResolvedPromise(JSValue.FromInt32(2));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2));
        
        var resultP = raceFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        // First resolved promise wins
        Assert.Equal(1, result.ToInt32());
    }

    [Fact]
    public void PromiseRace_FirstRejected_RejectsResult()
    {
        var promiseCtor = GetPromiseCtor();
        var raceFn = (JSFunction)promiseCtor.Get("race").AsObject();
        
        var p1 = CreateRejectedPromise(JSValue.FromString("fast error"));
        var p2 = CreateResolvedPromise(JSValue.FromInt32(2));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2));
        
        var resultP = raceFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue error = JSValue.Undefined;
        var catchFn = (JSFunction)resultP.AsObject().Prototype!.Get("catch").AsObject();
        catchFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { error = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.Equal("fast error", error.ToString());
    }

    [Fact]
    public void PromiseRace_EmptyArray_NeverSettles()
    {
        var promiseCtor = GetPromiseCtor();
        var raceFn = (JSFunction)promiseCtor.Get("race").AsObject();
        
        var emptyArray = CreateArray();
        var resultP = raceFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(emptyArray) });
        _context.RunMicrotasks();

        // Promise should still be pending
        var stateVal = resultP.AsObject().Get("[[PromiseState]]");
        Assert.Equal("pending", stateVal.ToString());
    }

    // ==================== Promise.allSettled Tests ====================

    [Fact]
    public void PromiseAllSettled_EmptyArray_ResolvesEmpty()
    {
        var promiseCtor = GetPromiseCtor();
        var allSettledFn = (JSFunction)promiseCtor.Get("allSettled").AsObject();
        
        var emptyArray = CreateArray();
        var resultP = allSettledFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(emptyArray) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.True(result.IsObject);
        Assert.Equal(0, result.AsObject().Get("length").ToInt32());
    }

    [Fact]
    public void PromiseAllSettled_MixedResults_ReturnsAllStatuses()
    {
        var promiseCtor = GetPromiseCtor();
        var allSettledFn = (JSFunction)promiseCtor.Get("allSettled").AsObject();
        
        var p1 = CreateResolvedPromise(JSValue.FromInt32(42));
        var p2 = CreateRejectedPromise(JSValue.FromString("error"));
        var p3 = CreateResolvedPromise(JSValue.FromString("success"));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2), JSValue.FromObject(p3));
        
        var resultP = allSettledFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(3, resultArr.Get("length").ToInt32());

        // First: fulfilled
        var item0 = resultArr.Get(0u).AsObject();
        Assert.Equal("fulfilled", item0.Get("status").ToString());
        Assert.Equal(42, item0.Get("value").ToInt32());

        // Second: rejected
        var item1 = resultArr.Get(1u).AsObject();
        Assert.Equal("rejected", item1.Get("status").ToString());
        Assert.Equal("error", item1.Get("reason").ToString());

        // Third: fulfilled
        var item2 = resultArr.Get(2u).AsObject();
        Assert.Equal("fulfilled", item2.Get("status").ToString());
        Assert.Equal("success", item2.Get("value").ToString());
    }

    [Fact]
    public void PromiseAllSettled_AllRejected_StillResolves()
    {
        var promiseCtor = GetPromiseCtor();
        var allSettledFn = (JSFunction)promiseCtor.Get("allSettled").AsObject();
        
        var p1 = CreateRejectedPromise(JSValue.FromString("error1"));
        var p2 = CreateRejectedPromise(JSValue.FromString("error2"));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2));
        
        var resultP = allSettledFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        JSValue result = JSValue.Undefined;
        bool resolved = false;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; resolved = true; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        Assert.True(resolved);
        Assert.True(result.IsObject);
        var resultArr = result.AsObject();
        Assert.Equal(2, resultArr.Get("length").ToInt32());
        
        var item0 = resultArr.Get(0u).AsObject();
        Assert.Equal("rejected", item0.Get("status").ToString());
        Assert.Equal("error1", item0.Get("reason").ToString());
        
        var item1 = resultArr.Get(1u).AsObject();
        Assert.Equal("rejected", item1.Get("status").ToString());
        Assert.Equal("error2", item1.Get("reason").ToString());
    }

    // ==================== Promise.any Tests ====================

    [Fact]
    public void PromiseAny_OneResolves_ResolvesWithValue()
    {
        var promiseCtor = GetPromiseCtor();
        var anyFn = (JSFunction)promiseCtor.Get("any").AsObject();
        
        var p1 = CreateRejectedPromise(JSValue.FromString("error1"));
        var p2 = CreateResolvedPromise(JSValue.FromInt32(42));
        var p3 = CreateRejectedPromise(JSValue.FromString("error3"));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2), JSValue.FromObject(p3));
        
        var resultP = anyFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        Assert.Equal(42, result.ToInt32());
    }

    [Fact]
    public void PromiseAny_AllReject_RejectsWithAggregateError()
    {
        var promiseCtor = GetPromiseCtor();
        var anyFn = (JSFunction)promiseCtor.Get("any").AsObject();
        
        var p1 = CreateRejectedPromise(JSValue.FromString("error1"));
        var p2 = CreateRejectedPromise(JSValue.FromString("error2"));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2));
        
        var resultP = anyFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        JSValue error = JSValue.Undefined;
        var catchFn = (JSFunction)resultP.AsObject().Prototype!.Get("catch").AsObject();
        catchFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { error = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();
        _context.RunMicrotasks(); // Extra run for chained promises

        Assert.True(error.IsObject);
        var errorObj = error.AsObject();
        Assert.Equal("AggregateError", errorObj.Get("name").ToString());
        Assert.Equal("All promises were rejected", errorObj.Get("message").ToString());
        
        var errors = errorObj.Get("errors").AsObject();
        Assert.Equal(2, errors.Get("length").ToInt32());
        Assert.Equal("error1", errors.Get(0u).ToString());
        Assert.Equal("error2", errors.Get(1u).ToString());
    }

    [Fact]
    public void PromiseAny_EmptyArray_Rejects()
    {
        var promiseCtor = GetPromiseCtor();
        var anyFn = (JSFunction)promiseCtor.Get("any").AsObject();
        
        var emptyArray = CreateArray();
        var resultP = anyFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(emptyArray) });
        _context.RunMicrotasks();

        // Should reject (empty array means no promises can fulfill)
        var stateVal = resultP.AsObject().Get("[[PromiseState]]");
        Assert.Equal("rejected", stateVal.ToString());
    }

    [Fact]
    public void PromiseAny_FirstFulfilled_Wins()
    {
        var promiseCtor = GetPromiseCtor();
        var anyFn = (JSFunction)promiseCtor.Get("any").AsObject();
        
        var p1 = CreateResolvedPromise(JSValue.FromInt32(1));
        var p2 = CreateResolvedPromise(JSValue.FromInt32(2));
        var arr = CreateArray(JSValue.FromObject(p1), JSValue.FromObject(p2));
        
        var resultP = anyFn.CallNative(JSValue.FromObject(promiseCtor), new[] { JSValue.FromObject(arr) });
        _context.RunMicrotasks();

        JSValue result = JSValue.Undefined;
        var thenFn = (JSFunction)resultP.AsObject().Prototype!.Get("then").AsObject();
        thenFn.CallNative(resultP, new[] { JSValue.FromObject(new JSFunction((_, a) => { result = a[0]; return JSValue.Undefined; }, "", 1, GetFunctionProto())) });
        _context.RunMicrotasks();

        // First resolved promise wins
        Assert.Equal(1, result.ToInt32());
    }
}
