// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for prototype chain operations in JSObject.
/// </summary>
public class PrototypeChainTests
{
    #region SetPrototype Tests

    [Fact]
    public void SetPrototype_NewPrototype_Succeeds()
    {
        var proto = new JSObject();
        var obj = new JSObject();

        var result = obj.SetPrototype(proto);

        Assert.True(result);
        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void SetPrototype_SamePrototype_Succeeds()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);

        var result = obj.SetPrototype(proto);

        Assert.True(result);
        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void SetPrototype_NullPrototype_Succeeds()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);

        var result = obj.SetPrototype(null);

        Assert.True(result);
        Assert.Null(obj.Prototype);
    }

    [Fact]
    public void SetPrototype_ImmutablePrototype_Fails()
    {
        var obj = new JSObject();
        obj.SetImmutablePrototype();

        var newProto = new JSObject();
        var result = obj.SetPrototype(newProto);

        Assert.False(result);
        Assert.Null(obj.Prototype);
    }

    [Fact]
    public void SetPrototype_ImmutablePrototype_SameValue_Succeeds()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);
        obj.SetImmutablePrototype();

        // Setting to the same prototype should succeed
        var result = obj.SetPrototype(proto);

        Assert.True(result);
        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void SetPrototype_NonExtensible_Fails()
    {
        var obj = new JSObject();
        obj.PreventExtensions();

        var newProto = new JSObject();
        var result = obj.SetPrototype(newProto);

        Assert.False(result);
        Assert.Null(obj.Prototype);
    }

    [Fact]
    public void SetPrototype_NonExtensible_SameValue_Succeeds()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);
        obj.PreventExtensions();

        // Setting to the same prototype should succeed
        var result = obj.SetPrototype(proto);

        Assert.True(result);
        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void SetPrototype_CircularChain_Self_Fails()
    {
        var obj = new JSObject();

        // Setting prototype to self would create a cycle
        var result = obj.SetPrototype(obj);

        Assert.False(result);
        Assert.Null(obj.Prototype);
    }

    [Fact]
    public void SetPrototype_CircularChain_Indirect_Fails()
    {
        var obj1 = new JSObject();
        var obj2 = new JSObject(obj1);
        var obj3 = new JSObject(obj2);

        // Setting obj1's prototype to obj3 would create: obj1 -> obj3 -> obj2 -> obj1 (cycle)
        var result = obj1.SetPrototype(obj3);

        Assert.False(result);
        Assert.Null(obj1.Prototype);
    }

    [Fact]
    public void SetPrototype_ValidChain_Succeeds()
    {
        var obj1 = new JSObject();
        var obj2 = new JSObject();
        var obj3 = new JSObject();

        Assert.True(obj3.SetPrototype(obj2));
        Assert.True(obj2.SetPrototype(obj1));

        Assert.Same(obj2, obj3.Prototype);
        Assert.Same(obj1, obj2.Prototype);
    }

    #endregion

    #region SetPrototypeOrThrow Tests

    [Fact]
    public void SetPrototypeOrThrow_Success()
    {
        var proto = new JSObject();
        var obj = new JSObject();

        obj.SetPrototypeOrThrow(proto);

        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void SetPrototypeOrThrow_ImmutablePrototype_Throws()
    {
        var obj = new JSObject();
        obj.SetImmutablePrototype();

        var ex = Assert.Throws<System.InvalidOperationException>(
            () => obj.SetPrototypeOrThrow(new JSObject()));

        Assert.Contains("immutable", ex.Message.ToLower());
    }

    [Fact]
    public void SetPrototypeOrThrow_NonExtensible_Throws()
    {
        var obj = new JSObject();
        obj.PreventExtensions();

        var ex = Assert.Throws<System.InvalidOperationException>(
            () => obj.SetPrototypeOrThrow(new JSObject()));

        Assert.Contains("extensible", ex.Message.ToLower());
    }

    [Fact]
    public void SetPrototypeOrThrow_CircularChain_Throws()
    {
        var obj = new JSObject();

        var ex = Assert.Throws<System.InvalidOperationException>(
            () => obj.SetPrototypeOrThrow(obj));

        Assert.Contains("circular", ex.Message.ToLower());
    }

    #endregion

    #region HasImmutablePrototype Tests

    [Fact]
    public void HasImmutablePrototype_DefaultFalse()
    {
        var obj = new JSObject();
        Assert.False(obj.HasImmutablePrototype);
    }

    [Fact]
    public void SetImmutablePrototype_SetsFlag()
    {
        var obj = new JSObject();
        obj.SetImmutablePrototype();
        Assert.True(obj.HasImmutablePrototype);
    }

    [Fact]
    public void ImmutablePrototype_PreservesCurrentPrototype()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);

        obj.SetImmutablePrototype();

        Assert.Same(proto, obj.Prototype);
    }

    #endregion

    #region GetPrototypeValue Tests

    [Fact]
    public void GetPrototypeValue_WithPrototype_ReturnsObjectValue()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);

        var result = obj.GetPrototypeValue();

        Assert.True(result.IsObject);
        Assert.Same(proto, result.AsObject());
    }

    [Fact]
    public void GetPrototypeValue_NoPrototype_ReturnsNull()
    {
        var obj = new JSObject();

        var result = obj.GetPrototypeValue();

        Assert.True(result.IsNull);
    }

    #endregion

    #region Object.create Tests

    [Fact]
    public void Create_WithPrototype_SetsPrototype()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(42));

        var obj = JSObject.Create(proto);

        Assert.Same(proto, obj.Prototype);
        Assert.Equal(42, obj.Get("inherited").ToInt32());
    }

    [Fact]
    public void Create_NullPrototype_HasNoPrototype()
    {
        var obj = JSObject.Create(null);

        Assert.Null(obj.Prototype);
    }

    [Fact]
    public void Create_WithProperties_DefinesProperties()
    {
        var proto = new JSObject();
        var properties = new Dictionary<string, PropertyDescriptor>
        {
            ["x"] = PropertyDescriptor.Data(JSValue.FromInt32(10)),
            ["y"] = PropertyDescriptor.Data(JSValue.FromInt32(20), writable: false, enumerable: true, configurable: false),
        };

        var obj = JSObject.Create(proto, properties);

        Assert.Equal(10, obj.Get("x").ToInt32());
        Assert.Equal(20, obj.Get("y").ToInt32());

        var yDesc = obj.GetOwnPropertyDescriptor("y");
        Assert.NotNull(yDesc);
        Assert.False(yDesc!.IsWritable);
        Assert.False(yDesc.IsConfigurable);
    }

    [Fact]
    public void Create_EmptyProperties_CreatesEmptyObject()
    {
        var properties = new Dictionary<string, PropertyDescriptor>();

        var obj = JSObject.Create(null, properties);

        Assert.Equal(0, obj.OwnPropertyCount);
    }

    #endregion

    #region Object.assign Tests

    [Fact]
    public void Assign_SingleSource_CopiesProperties()
    {
        var target = new JSObject();
        var source = new JSObject();
        source.Set("a", JSValue.FromInt32(1));
        source.Set("b", JSValue.FromString("hello"));

        var result = JSObject.Assign(target, source);

        Assert.Same(target, result);
        Assert.Equal(1, target.Get("a").ToInt32());
        Assert.Equal("hello", target.Get("b").ToString());
    }

    [Fact]
    public void Assign_MultipleSources_CopiesAll()
    {
        var target = new JSObject();
        var source1 = new JSObject();
        source1.Set("a", JSValue.FromInt32(1));

        var source2 = new JSObject();
        source2.Set("b", JSValue.FromInt32(2));

        JSObject.Assign(target, source1, source2);

        Assert.Equal(1, target.Get("a").ToInt32());
        Assert.Equal(2, target.Get("b").ToInt32());
    }

    [Fact]
    public void Assign_OverwritesExisting()
    {
        var target = new JSObject();
        target.Set("x", JSValue.FromInt32(1));

        var source = new JSObject();
        source.Set("x", JSValue.FromInt32(99));

        JSObject.Assign(target, source);

        Assert.Equal(99, target.Get("x").ToInt32());
    }

    [Fact]
    public void Assign_OnlyCopiesEnumerable()
    {
        var target = new JSObject();
        var source = new JSObject();
        source.Set("enumerable", JSValue.FromInt32(1));
        source.DefineProperty("hidden", PropertyDescriptor.Data(
            JSValue.FromInt32(2),
            writable: true,
            enumerable: false,
            configurable: true));

        JSObject.Assign(target, source);

        Assert.True(target.HasProperty("enumerable"));
        Assert.False(target.HasProperty("hidden"));
    }

    [Fact]
    public void Assign_SkipsNullSources()
    {
        var target = new JSObject();
        target.Set("original", JSValue.FromInt32(1));

        var result = JSObject.Assign(target, null!);

        Assert.Same(target, result);
        Assert.Equal(1, target.Get("original").ToInt32());
    }

    [Fact]
    public void Assign_NullTarget_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => JSObject.Assign(null!, new JSObject()));
    }

    #endregion

    #region Object.keys/values/entries Tests

    [Fact]
    public void Keys_ReturnsEnumerableKeys()
    {
        var obj = new JSObject();
        obj.Set("a", JSValue.FromInt32(1));
        obj.Set("b", JSValue.FromInt32(2));
        obj.DefineProperty("hidden", PropertyDescriptor.Data(
            JSValue.FromInt32(3),
            writable: true,
            enumerable: false,
            configurable: true));

        var keys = obj.Keys();

        Assert.Equal(2, keys.Length);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.DoesNotContain("hidden", keys);
    }

    [Fact]
    public void Values_ReturnsEnumerableValues()
    {
        var obj = new JSObject();
        obj.Set("a", JSValue.FromInt32(10));
        obj.Set("b", JSValue.FromInt32(20));

        var values = obj.Values();

        Assert.Equal(2, values.Length);
        Assert.Contains(JSValue.FromInt32(10), values);
        Assert.Contains(JSValue.FromInt32(20), values);
    }

    [Fact]
    public void Entries_ReturnsKeyValuePairs()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(100));
        obj.Set("y", JSValue.FromString("test"));

        var entries = obj.Entries();

        Assert.Equal(2, entries.Length);
        Assert.Contains(("x", JSValue.FromInt32(100)), entries);
        Assert.Contains(("y", JSValue.FromString("test")), entries);
    }

    #endregion

    #region Object.fromEntries Tests

    [Fact]
    public void FromEntries_CreatesObjectFromPairs()
    {
        var entries = new List<(string Key, JSValue Value)>
        {
            ("a", JSValue.FromInt32(1)),
            ("b", JSValue.FromString("hello")),
        };

        var obj = JSObject.FromEntries(entries);

        Assert.Equal(1, obj.Get("a").ToInt32());
        Assert.Equal("hello", obj.Get("b").ToString());
    }

    [Fact]
    public void FromEntries_WithPrototype()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(42));

        var entries = new List<(string Key, JSValue Value)>
        {
            ("own", JSValue.FromInt32(1)),
        };

        var obj = JSObject.FromEntries(entries, proto);

        Assert.Same(proto, obj.Prototype);
        Assert.Equal(1, obj.Get("own").ToInt32());
        Assert.Equal(42, obj.Get("inherited").ToInt32());
    }

    [Fact]
    public void FromEntries_DuplicateKeys_LastWins()
    {
        var entries = new List<(string Key, JSValue Value)>
        {
            ("x", JSValue.FromInt32(1)),
            ("x", JSValue.FromInt32(2)),
            ("x", JSValue.FromInt32(3)),
        };

        var obj = JSObject.FromEntries(entries);

        Assert.Equal(3, obj.Get("x").ToInt32());
    }

    #endregion

    #region Prototype Property Setter Tests

    [Fact]
    public void PrototypeSetter_UsesSetPrototype()
    {
        var proto = new JSObject();
        var obj = new JSObject();

        obj.Prototype = proto;

        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void PrototypeSetter_ImmutablePrototype_NoChange()
    {
        var originalProto = new JSObject();
        var obj = new JSObject(originalProto);
        obj.SetImmutablePrototype();

        obj.Prototype = new JSObject(); // Should fail silently

        Assert.Same(originalProto, obj.Prototype);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void DeepPrototypeChain_InheritanceWorks()
    {
        var grandProto = new JSObject();
        grandProto.Set("level", JSValue.FromString("grandparent"));
        grandProto.Set("grandOnly", JSValue.FromInt32(1));

        var proto = new JSObject(grandProto);
        proto.Set("level", JSValue.FromString("parent"));
        proto.Set("parentOnly", JSValue.FromInt32(2));

        var obj = new JSObject(proto);
        obj.Set("level", JSValue.FromString("child"));
        obj.Set("childOnly", JSValue.FromInt32(3));

        Assert.Equal("child", obj.Get("level").ToString());
        Assert.Equal(3, obj.Get("childOnly").ToInt32());
        Assert.Equal(2, obj.Get("parentOnly").ToInt32());
        Assert.Equal(1, obj.Get("grandOnly").ToInt32());
    }

    [Fact]
    public void PrototypeChainBreaking_StopsInheritance()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(42));

        var obj = new JSObject(proto);
        Assert.Equal(42, obj.Get("inherited").ToInt32());

        obj.Prototype = null;
        Assert.True(obj.Get("inherited").IsUndefined);
    }

    [Fact]
    public void PrototypeChange_UpdatesInheritance()
    {
        var proto1 = new JSObject();
        proto1.Set("value", JSValue.FromInt32(1));

        var proto2 = new JSObject();
        proto2.Set("value", JSValue.FromInt32(2));

        var obj = new JSObject(proto1);
        Assert.Equal(1, obj.Get("value").ToInt32());

        obj.Prototype = proto2;
        Assert.Equal(2, obj.Get("value").ToInt32());
    }

    #endregion
}
