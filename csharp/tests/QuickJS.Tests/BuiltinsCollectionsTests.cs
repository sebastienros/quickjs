// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JavaScript collection builtins: Map, Set, WeakMap, WeakSet.
/// </summary>
public class BuiltinsCollectionsTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsCollectionsTests()
    {
        _context = _runtime.CreateContext();
    }

    // ==================== Map Tests ====================

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
    public void Map_Constructor_CreatesEmptyMap()
    {
        var mapCtor = _context.GetGlobalProperty("Map").AsObject() as JSFunction;
        Assert.NotNull(mapCtor);

        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();
        Assert.NotNull(map);
        Assert.Equal(JSClassId.Map, map.ClassId);

        var sizeFn = (JSFunction)map.Prototype!.Get("size").AsObject();
        var size = sizeFn.CallNative(JSValue.FromObject(map), System.Array.Empty<JSValue>());
        Assert.Equal(0, size.ToInt32());
    }

    [Fact]
    public void Map_SetAndGet_WorksWithObjects()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)map.Prototype!.Get("set").AsObject();
        var getFn = (JSFunction)map.Prototype!.Get("get").AsObject();

        // Create an object as key
        var objKey = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        
        setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromObject(objKey), JSValue.FromString("value") });

        var val = getFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromObject(objKey) });
        Assert.Equal("value", val.ToString());
    }

    [Fact]
    public void Map_Delete_RemovesEntry()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)map.Prototype!.Get("set").AsObject();
        var deleteFn = (JSFunction)map.Prototype!.Get("delete").AsObject();
        var hasFn = (JSFunction)map.Prototype!.Get("has").AsObject();

        setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("toDelete"), JSValue.FromInt32(1) });
        Assert.True(hasFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("toDelete") }).IsTrue);

        var deleted = deleteFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("toDelete") });
        Assert.True(deleted.IsTrue);

        Assert.False(hasFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("toDelete") }).IsTrue);
    }

    [Fact]
    public void Map_Clear_RemovesAllEntries()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)map.Prototype!.Get("set").AsObject();
        var clearFn = (JSFunction)map.Prototype!.Get("clear").AsObject();
        var sizeFn = (JSFunction)map.Prototype!.Get("size").AsObject();

        setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("key1"), JSValue.FromInt32(1) });
        setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("key2"), JSValue.FromInt32(2) });
        Assert.Equal(2, sizeFn.CallNative(JSValue.FromObject(map), System.Array.Empty<JSValue>()).ToInt32());

        clearFn.CallNative(JSValue.FromObject(map), System.Array.Empty<JSValue>());
        Assert.Equal(0, sizeFn.CallNative(JSValue.FromObject(map), System.Array.Empty<JSValue>()).ToInt32());
    }

    [Fact]
    public void Map_Set_ReturnsSameMap()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)map.Prototype!.Get("set").AsObject();
        var result = setFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("key"), JSValue.FromInt32(1) });

        Assert.Same(map, result.AsObject());
    }

    [Fact]
    public void Map_Get_ReturnsUndefinedForMissingKey()
    {
        var mapCtor = (JSFunction)_context.GetGlobalProperty("Map").AsObject();
        var map = mapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var getFn = (JSFunction)map.Prototype!.Get("get").AsObject();

        var val = getFn.CallNative(JSValue.FromObject(map), new[] { JSValue.FromString("nonexistent") });
        Assert.Equal(JSValueType.Undefined, val.Tag);
    }

    // ==================== Set Tests ====================

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

    [Fact]
    public void Set_Constructor_CreatesEmptySet()
    {
        var setCtor = (JSFunction)_context.GetGlobalProperty("Set").AsObject();
        var set = setCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        Assert.NotNull(set);
        Assert.Equal(JSClassId.Set, set.ClassId);

        var sizeFn = (JSFunction)set.Prototype!.Get("size").AsObject();
        Assert.Equal(0, sizeFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>()).ToInt32());
    }

    [Fact]
    public void Set_Add_DuplicatesIgnored()
    {
        var setCtor = (JSFunction)_context.GetGlobalProperty("Set").AsObject();
        var set = setCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)set.Prototype!.Get("add").AsObject();
        var sizeFn = (JSFunction)set.Prototype!.Get("size").AsObject();

        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) });
        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) });
        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) });

        Assert.Equal(1, sizeFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>()).ToInt32());
    }

    [Fact]
    public void Set_Delete_RemovesValue()
    {
        var setCtor = (JSFunction)_context.GetGlobalProperty("Set").AsObject();
        var set = setCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)set.Prototype!.Get("add").AsObject();
        var deleteFn = (JSFunction)set.Prototype!.Get("delete").AsObject();
        var hasFn = (JSFunction)set.Prototype!.Get("has").AsObject();

        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) });
        Assert.True(hasFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) }).IsTrue);

        var deleted = deleteFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) });
        Assert.True(deleted.IsTrue);

        Assert.False(hasFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(42) }).IsTrue);
    }

    [Fact]
    public void Set_Clear_RemovesAllValues()
    {
        var setCtor = (JSFunction)_context.GetGlobalProperty("Set").AsObject();
        var set = setCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)set.Prototype!.Get("add").AsObject();
        var clearFn = (JSFunction)set.Prototype!.Get("clear").AsObject();
        var sizeFn = (JSFunction)set.Prototype!.Get("size").AsObject();

        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(1) });
        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(2) });
        addFn.CallNative(JSValue.FromObject(set), new[] { JSValue.FromInt32(3) });
        Assert.Equal(3, sizeFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>()).ToInt32());

        clearFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>());
        Assert.Equal(0, sizeFn.CallNative(JSValue.FromObject(set), System.Array.Empty<JSValue>()).ToInt32());
    }

    // ==================== WeakMap Tests ====================

    [Fact]
    public void WeakMap_Constructor_CreatesWeakMap()
    {
        var weakMapCtor = (JSFunction)_context.GetGlobalProperty("WeakMap").AsObject();
        var weakMap = weakMapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        Assert.NotNull(weakMap);
        Assert.Equal(JSClassId.WeakMap, weakMap.ClassId);
    }

    [Fact]
    public void WeakMap_SetAndGet_WorksWithObjects()
    {
        var weakMapCtor = (JSFunction)_context.GetGlobalProperty("WeakMap").AsObject();
        var weakMap = weakMapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)weakMap.Prototype!.Get("set").AsObject();
        var getFn = (JSFunction)weakMap.Prototype!.Get("get").AsObject();

        var objKey = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        setFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey), JSValue.FromString("value") });

        var val = getFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey) });
        Assert.Equal("value", val.ToString());
    }

    [Fact]
    public void WeakMap_Set_RequiresObjectKey()
    {
        var weakMapCtor = (JSFunction)_context.GetGlobalProperty("WeakMap").AsObject();
        var weakMap = weakMapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)weakMap.Prototype!.Get("set").AsObject();

        // Should throw for non-object keys
        var result = setFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromString("stringKey"), JSValue.FromInt32(1) });
        Assert.Equal(JSValueType.Exception, result.Tag);
    }

    [Fact]
    public void WeakMap_Delete_RemovesEntry()
    {
        var weakMapCtor = (JSFunction)_context.GetGlobalProperty("WeakMap").AsObject();
        var weakMap = weakMapCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var setFn = (JSFunction)weakMap.Prototype!.Get("set").AsObject();
        var deleteFn = (JSFunction)weakMap.Prototype!.Get("delete").AsObject();
        var hasFn = (JSFunction)weakMap.Prototype!.Get("has").AsObject();

        var objKey = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        setFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey), JSValue.FromInt32(1) });

        Assert.True(hasFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey) }).IsTrue);

        var deleted = deleteFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey) });
        Assert.True(deleted.IsTrue);

        Assert.False(hasFn.CallNative(JSValue.FromObject(weakMap), new[] { JSValue.FromObject(objKey) }).IsTrue);
    }

    // ==================== WeakSet Tests ====================

    [Fact]
    public void WeakSet_Constructor_CreatesWeakSet()
    {
        var weakSetCtor = (JSFunction)_context.GetGlobalProperty("WeakSet").AsObject();
        var weakSet = weakSetCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        Assert.NotNull(weakSet);
        Assert.Equal(JSClassId.WeakSet, weakSet.ClassId);
    }

    [Fact]
    public void WeakSet_Add_WorksWithObjects()
    {
        var weakSetCtor = (JSFunction)_context.GetGlobalProperty("WeakSet").AsObject();
        var weakSet = weakSetCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)weakSet.Prototype!.Get("add").AsObject();
        var hasFn = (JSFunction)weakSet.Prototype!.Get("has").AsObject();

        var obj = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        addFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) });

        Assert.True(hasFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) }).IsTrue);
    }

    [Fact]
    public void WeakSet_Add_RequiresObject()
    {
        var weakSetCtor = (JSFunction)_context.GetGlobalProperty("WeakSet").AsObject();
        var weakSet = weakSetCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)weakSet.Prototype!.Get("add").AsObject();

        // Should throw for non-object values
        var result = addFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromString("string") });
        Assert.Equal(JSValueType.Exception, result.Tag);
    }

    [Fact]
    public void WeakSet_Delete_RemovesValue()
    {
        var weakSetCtor = (JSFunction)_context.GetGlobalProperty("WeakSet").AsObject();
        var weakSet = weakSetCtor.CallNative(JSValue.Undefined, System.Array.Empty<JSValue>()).AsObject();

        var addFn = (JSFunction)weakSet.Prototype!.Get("add").AsObject();
        var deleteFn = (JSFunction)weakSet.Prototype!.Get("delete").AsObject();
        var hasFn = (JSFunction)weakSet.Prototype!.Get("has").AsObject();

        var obj = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        addFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) });

        Assert.True(hasFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) }).IsTrue);

        var deleted = deleteFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) });
        Assert.True(deleted.IsTrue);

        Assert.False(hasFn.CallNative(JSValue.FromObject(weakSet), new[] { JSValue.FromObject(obj) }).IsTrue);
    }
}
