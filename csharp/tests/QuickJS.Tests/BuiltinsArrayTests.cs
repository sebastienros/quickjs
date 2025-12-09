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

    [Fact]
    public void ArrayShift_RemovesFirstElement()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var shiftFn = (JSFunction)arrayProto.Get("shift").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        var result = shiftFn.CallNative(JSValue.FromObject(arr), System.Array.Empty<JSValue>());
        Assert.Equal(1, result.ToInt32());
        Assert.Equal(2, arr.Get("length").ToInt32());
        Assert.Equal(2, arr.Get(0).ToInt32());
    }

    [Fact]
    public void ArrayUnshift_AddsElementsToFront()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var unshiftFn = (JSFunction)arrayProto.Get("unshift").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(1));

        var newLen = unshiftFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        Assert.Equal(3, newLen.ToInt32());
        Assert.Equal(1, arr.Get(0).ToInt32());
        Assert.Equal(2, arr.Get(1).ToInt32());
        Assert.Equal(3, arr.Get(2).ToInt32());
    }

    [Fact]
    public void ArraySlice_ReturnsSubarray()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var sliceFn = (JSFunction)arrayProto.Get("slice").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        for (int i = 0; i < 5; i++)
            arr.Set((uint)i, JSValue.FromInt32(i));
        arr.Set("length", JSValue.FromInt32(5));

        var result = sliceFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(1), JSValue.FromInt32(4) }).AsObject();
        Assert.Equal(3, result.Get("length").ToInt32());
        Assert.Equal(1, result.Get(0).ToInt32());
        Assert.Equal(2, result.Get(1).ToInt32());
        Assert.Equal(3, result.Get(2).ToInt32());
    }

    [Fact]
    public void ArrayConcat_MergesArrays()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var concatFn = (JSFunction)arrayProto.Get("concat").AsObject();

        var arr1 = new JSObject(arrayProto, JSClassId.Array);
        arr1.Set(0u, JSValue.FromInt32(1));
        arr1.Set(1u, JSValue.FromInt32(2));
        arr1.Set("length", JSValue.FromInt32(2));

        var arr2 = new JSObject(arrayProto, JSClassId.Array);
        arr2.Set(0u, JSValue.FromInt32(3));
        arr2.Set(1u, JSValue.FromInt32(4));
        arr2.Set("length", JSValue.FromInt32(2));

        var result = concatFn.CallNative(JSValue.FromObject(arr1), new[] { JSValue.FromObject(arr2) }).AsObject();
        Assert.Equal(4, result.Get("length").ToInt32());
    }

    [Fact]
    public void ArrayJoin_ConcatenatesElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var joinFn = (JSFunction)arrayProto.Get("join").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        var result = joinFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromString("-") });
        Assert.Equal("1-2-3", result.ToString());
    }

    [Fact]
    public void ArrayReverse_ReversesInPlace()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var reverseFn = (JSFunction)arrayProto.Get("reverse").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        reverseFn.CallNative(JSValue.FromObject(arr), System.Array.Empty<JSValue>());
        Assert.Equal(3, arr.Get(0).ToInt32());
        Assert.Equal(2, arr.Get(1).ToInt32());
        Assert.Equal(1, arr.Get(2).ToInt32());
    }

    [Fact]
    public void ArrayIndexOf_FindsElement()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var indexOfFn = (JSFunction)arrayProto.Get("indexOf").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(10));
        arr.Set(1u, JSValue.FromInt32(20));
        arr.Set(2u, JSValue.FromInt32(30));
        arr.Set("length", JSValue.FromInt32(3));

        Assert.Equal(1, indexOfFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(20) }).ToInt32());
        Assert.Equal(-1, indexOfFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(99) }).ToInt32());
    }

    [Fact]
    public void ArrayIncludes_ReturnsTrueIfContains()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var includesFn = (JSFunction)arrayProto.Get("includes").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        Assert.True(includesFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(2) }).IsTrue);
        Assert.False(includesFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(99) }).IsTrue);
    }

    [Fact]
    public void ArrayMap_TransformsElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var mapFn = (JSFunction)arrayProto.Get("map").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        // Create a simple callback that doubles values using correct constructor
        JSValue DoubleCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromInt32(args[0].ToInt32() * 2);

        var callback = new JSFunction(DoubleCallback, "double", 1);

        var result = mapFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(callback) }).AsObject();
        Assert.Equal(3, result.Get("length").ToInt32());
        Assert.Equal(2, result.Get(0).ToInt32());
        Assert.Equal(4, result.Get(1).ToInt32());
        Assert.Equal(6, result.Get(2).ToInt32());
    }

    [Fact]
    public void ArrayFilter_SelectsMatchingElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var filterFn = (JSFunction)arrayProto.Get("filter").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set(3u, JSValue.FromInt32(4));
        arr.Set("length", JSValue.FromInt32(4));

        // Create a callback that selects even numbers
        JSValue IsEvenCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromBoolean(args[0].ToInt32() % 2 == 0);

        var callback = new JSFunction(IsEvenCallback, "isEven", 1);

        var result = filterFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(callback) }).AsObject();
        Assert.Equal(2, result.Get("length").ToInt32());
        Assert.Equal(2, result.Get(0).ToInt32());
        Assert.Equal(4, result.Get(1).ToInt32());
    }

    [Fact]
    public void ArrayReduce_AggregatesElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var reduceFn = (JSFunction)arrayProto.Get("reduce").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set(3u, JSValue.FromInt32(4));
        arr.Set("length", JSValue.FromInt32(4));

        // Create a sum callback (accumulator, currentValue) => accumulator + currentValue
        JSValue SumCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromInt32(args[0].ToInt32() + args[1].ToInt32());

        var callback = new JSFunction(SumCallback, "sum", 2);

        var result = reduceFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(callback), JSValue.FromInt32(0) });
        Assert.Equal(10, result.ToInt32());
    }

    [Fact]
    public void ArrayEvery_ChecksAllElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var everyFn = (JSFunction)arrayProto.Get("every").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(2));
        arr.Set(1u, JSValue.FromInt32(4));
        arr.Set(2u, JSValue.FromInt32(6));
        arr.Set("length", JSValue.FromInt32(3));

        // Create a callback that checks if number is even
        JSValue IsEvenCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromBoolean(args[0].ToInt32() % 2 == 0);

        var isEvenCallback = new JSFunction(IsEvenCallback, "isEven", 1);

        Assert.True(everyFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).IsTrue);

        arr.Set(2u, JSValue.FromInt32(5));
        Assert.False(everyFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).IsTrue);
    }

    [Fact]
    public void ArraySome_ChecksAnyElement()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var someFn = (JSFunction)arrayProto.Get("some").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(3));
        arr.Set(2u, JSValue.FromInt32(4));
        arr.Set("length", JSValue.FromInt32(3));

        JSValue IsEvenCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromBoolean(args[0].ToInt32() % 2 == 0);

        var isEvenCallback = new JSFunction(IsEvenCallback, "isEven", 1);

        Assert.True(someFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).IsTrue);

        arr.Set(2u, JSValue.FromInt32(5));
        Assert.False(someFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).IsTrue);
    }

    [Fact]
    public void ArrayFind_ReturnsFirstMatchingElement()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var findFn = (JSFunction)arrayProto.Get("find").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        JSValue IsEvenCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromBoolean(args[0].ToInt32() % 2 == 0);

        var isEvenCallback = new JSFunction(IsEvenCallback, "isEven", 1);

        Assert.Equal(2, findFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).ToInt32());
    }

    [Fact]
    public void ArrayFindIndex_ReturnsIndexOfFirstMatchingElement()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var findIndexFn = (JSFunction)arrayProto.Get("findIndex").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        JSValue IsEvenCallback(JSValue thisVal, JSValue[] args) =>
            JSValue.FromBoolean(args[0].ToInt32() % 2 == 0);

        var isEvenCallback = new JSFunction(IsEvenCallback, "isEven", 1);

        Assert.Equal(1, findIndexFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromObject(isEvenCallback) }).ToInt32());
    }

    [Fact]
    public void ArrayFill_FillsWithValue()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var fillFn = (JSFunction)arrayProto.Get("fill").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        fillFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(0) });
        Assert.Equal(0, arr.Get(0).ToInt32());
        Assert.Equal(0, arr.Get(1).ToInt32());
        Assert.Equal(0, arr.Get(2).ToInt32());
    }

    [Fact]
    public void ArrayAt_ReturnsElementAtIndex()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var atFn = (JSFunction)arrayProto.Get("at").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set("length", JSValue.FromInt32(3));

        Assert.Equal(3, atFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(-1) }).ToInt32());
        Assert.Equal(1, atFn.CallNative(JSValue.FromObject(arr), new[] { JSValue.FromInt32(0) }).ToInt32());
    }

    [Fact]
    public void ArrayIsArray_IdentifiesArrays()
    {
        var arrayCtor = (JSFunction)_context.GetGlobalProperty("Array").AsObject();
        var isArrayFn = (JSFunction)arrayCtor.Get("isArray").AsObject();
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set("length", JSValue.FromInt32(0));

        var obj = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);

        Assert.True(isArrayFn.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(arr) }).IsTrue);
        Assert.False(isArrayFn.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) }).IsTrue);
    }

    [Fact]
    public void ArrayOf_CreatesArrayFromArguments()
    {
        var arrayCtor = (JSFunction)_context.GetGlobalProperty("Array").AsObject();
        var ofFn = (JSFunction)arrayCtor.Get("of").AsObject();

        var result = ofFn.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) }).AsObject();
        Assert.Equal(3, result.Get("length").ToInt32());
        Assert.Equal(1, result.Get(0).ToInt32());
        Assert.Equal(2, result.Get(1).ToInt32());
        Assert.Equal(3, result.Get(2).ToInt32());
    }

    [Fact]
    public void ArraySplice_RemovesAndInsertsElements()
    {
        var arrayProto = _context.GetClassPrototype(JSClassId.Array)!;
        var spliceFn = (JSFunction)arrayProto.Get("splice").AsObject();

        var arr = new JSObject(arrayProto, JSClassId.Array);
        arr.Set(0u, JSValue.FromInt32(1));
        arr.Set(1u, JSValue.FromInt32(2));
        arr.Set(2u, JSValue.FromInt32(3));
        arr.Set(3u, JSValue.FromInt32(4));
        arr.Set("length", JSValue.FromInt32(4));

        // Remove 2 elements at index 1, insert 10 and 20
        var removed = spliceFn.CallNative(JSValue.FromObject(arr), new[] {
            JSValue.FromInt32(1),
            JSValue.FromInt32(2),
            JSValue.FromInt32(10),
            JSValue.FromInt32(20)
        }).AsObject();

        Assert.Equal(2, removed.Get("length").ToInt32());
        Assert.Equal(2, removed.Get(0).ToInt32());
        Assert.Equal(3, removed.Get(1).ToInt32());

        Assert.Equal(4, arr.Get("length").ToInt32());
        Assert.Equal(1, arr.Get(0).ToInt32());
        Assert.Equal(10, arr.Get(1).ToInt32());
        Assert.Equal(20, arr.Get(2).ToInt32());
        Assert.Equal(4, arr.Get(3).ToInt32());
    }
}
