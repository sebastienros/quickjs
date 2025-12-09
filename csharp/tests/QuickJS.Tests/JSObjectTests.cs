// Licensed under the MIT License.

using System.Linq;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSObject"/> class.
/// </summary>
public class JSObjectTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Default_CreatesExtensibleObject()
    {
        var obj = new JSObject();

        Assert.True(obj.IsExtensible);
        Assert.Equal(JSClassId.Object, obj.ClassId);
        Assert.Null(obj.Prototype);
        Assert.Equal(0, obj.OwnPropertyCount);
    }

    [Fact]
    public void Constructor_WithPrototype_SetsPrototype()
    {
        var proto = new JSObject();
        var obj = new JSObject(proto);

        Assert.Same(proto, obj.Prototype);
    }

    [Fact]
    public void Constructor_WithClassId_SetsClassId()
    {
        var obj = new JSObject(null, JSClassId.Array);

        Assert.Equal(JSClassId.Array, obj.ClassId);
    }

    #endregion

    #region Named Property - Get/Set Tests

    [Fact]
    public void Set_NewProperty_AddsProperty()
    {
        var obj = new JSObject();

        var result = obj.Set("x", JSValue.FromInt32(42));

        Assert.True(result);
        Assert.Equal(1, obj.OwnPropertyCount);
    }

    [Fact]
    public void Get_ExistingProperty_ReturnsValue()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var value = obj.Get("x");

        Assert.Equal(42, value.ToInt32());
    }

    [Fact]
    public void Get_NonExistentProperty_ReturnsUndefined()
    {
        var obj = new JSObject();

        var value = obj.Get("nonexistent");

        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void Set_ExistingProperty_UpdatesValue()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        obj.Set("x", JSValue.FromInt32(100));

        Assert.Equal(100, obj.Get("x").ToInt32());
        Assert.Equal(1, obj.OwnPropertyCount);
    }

    [Fact]
    public void Set_NullPropertyName_Throws()
    {
        var obj = new JSObject();

        Assert.Throws<System.ArgumentNullException>(() => obj.Set(null!, JSValue.FromInt32(42)));
    }

    [Fact]
    public void Get_NullPropertyName_Throws()
    {
        var obj = new JSObject();

        Assert.Throws<System.ArgumentNullException>(() => obj.Get(null!));
    }

    #endregion

    #region Indexed Property Tests

    [Fact]
    public void Set_IndexedProperty_AddsProperty()
    {
        var obj = new JSObject();

        var result = obj.Set(0, JSValue.FromString("first"));

        Assert.True(result);
        Assert.Equal(1, obj.OwnPropertyCount);
    }

    [Fact]
    public void Get_IndexedProperty_ReturnsValue()
    {
        var obj = new JSObject();
        obj.Set(0, JSValue.FromString("first"));

        var value = obj.Get(0);

        Assert.Equal("first", value.ToString());
    }

    [Fact]
    public void Get_NonExistentIndexedProperty_ReturnsUndefined()
    {
        var obj = new JSObject();

        var value = obj.Get(999);

        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void Set_MultipleIndexedProperties()
    {
        var obj = new JSObject();
        obj.Set(0, JSValue.FromInt32(10));
        obj.Set(1, JSValue.FromInt32(20));
        obj.Set(2, JSValue.FromInt32(30));

        Assert.Equal(3, obj.OwnPropertyCount);
        Assert.Equal(10, obj.Get(0).ToInt32());
        Assert.Equal(20, obj.Get(1).ToInt32());
        Assert.Equal(30, obj.Get(2).ToInt32());
    }

    #endregion

    #region Prototype Chain Tests

    [Fact]
    public void Get_InheritsFromPrototype()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromString("from proto"));

        var obj = new JSObject(proto);

        Assert.Equal("from proto", obj.Get("inherited").ToString());
    }

    [Fact]
    public void Get_OwnPropertyShadowsPrototype()
    {
        var proto = new JSObject();
        proto.Set("prop", JSValue.FromString("proto value"));

        var obj = new JSObject(proto);
        obj.Set("prop", JSValue.FromString("own value"));

        Assert.Equal("own value", obj.Get("prop").ToString());
    }

    [Fact]
    public void Get_MultiLevelPrototypeChain()
    {
        var grandProto = new JSObject();
        grandProto.Set("deep", JSValue.FromInt32(123));

        var proto = new JSObject(grandProto);
        var obj = new JSObject(proto);

        Assert.Equal(123, obj.Get("deep").ToInt32());
    }

    [Fact]
    public void Get_IndexedProperty_InheritsFromPrototype()
    {
        var proto = new JSObject();
        proto.Set(5, JSValue.FromString("from proto"));

        var obj = new JSObject(proto);

        Assert.Equal("from proto", obj.Get(5).ToString());
    }

    [Fact]
    public void Prototype_CanBeChanged()
    {
        var proto1 = new JSObject();
        proto1.Set("source", JSValue.FromString("proto1"));

        var proto2 = new JSObject();
        proto2.Set("source", JSValue.FromString("proto2"));

        var obj = new JSObject(proto1);
        Assert.Equal("proto1", obj.Get("source").ToString());

        obj.Prototype = proto2;
        Assert.Equal("proto2", obj.Get("source").ToString());

        obj.Prototype = null;
        Assert.True(obj.Get("source").IsUndefined);
    }

    #endregion

    #region HasProperty Tests

    [Fact]
    public void HasProperty_OwnProperty_ReturnsTrue()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        Assert.True(obj.HasProperty("x"));
    }

    [Fact]
    public void HasProperty_InheritedProperty_ReturnsTrue()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(1));

        var obj = new JSObject(proto);

        Assert.True(obj.HasProperty("inherited"));
    }

    [Fact]
    public void HasProperty_NonExistent_ReturnsFalse()
    {
        var obj = new JSObject();

        Assert.False(obj.HasProperty("nonexistent"));
    }

    [Fact]
    public void HasOwnProperty_OwnProperty_ReturnsTrue()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        Assert.True(obj.HasOwnProperty("x"));
    }

    [Fact]
    public void HasOwnProperty_InheritedProperty_ReturnsFalse()
    {
        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(1));

        var obj = new JSObject(proto);

        Assert.False(obj.HasOwnProperty("inherited"));
    }

    [Fact]
    public void HasProperty_IndexedProperty()
    {
        var obj = new JSObject();
        obj.Set(0, JSValue.FromInt32(42));

        Assert.True(obj.HasProperty(0));
        Assert.False(obj.HasProperty(1));
    }

    [Fact]
    public void HasOwnProperty_IndexedProperty()
    {
        var proto = new JSObject();
        proto.Set(0, JSValue.FromInt32(42));

        var obj = new JSObject(proto);
        obj.Set(1, JSValue.FromInt32(100));

        Assert.False(obj.HasOwnProperty(0));
        Assert.True(obj.HasOwnProperty(1));
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_ExistingProperty_RemovesIt()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var result = obj.Delete("x");

        Assert.True(result);
        Assert.False(obj.HasProperty("x"));
        Assert.Equal(0, obj.OwnPropertyCount);
    }

    [Fact]
    public void Delete_NonExistentProperty_ReturnsTrue()
    {
        var obj = new JSObject();

        var result = obj.Delete("nonexistent");

        Assert.True(result);
    }

    [Fact]
    public void Delete_NonConfigurableProperty_ReturnsFalse()
    {
        var obj = new JSObject();
        obj.DefineProperty("constant", PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: false,
            enumerable: true,
            configurable: false));

        var result = obj.Delete("constant");

        Assert.False(result);
        Assert.True(obj.HasProperty("constant"));
    }

    [Fact]
    public void Delete_IndexedProperty()
    {
        var obj = new JSObject();
        obj.Set(0, JSValue.FromInt32(42));

        var result = obj.Delete(0);

        Assert.True(result);
        Assert.False(obj.HasProperty(0));
    }

    #endregion

    #region DefineProperty Tests

    [Fact]
    public void DefineProperty_NewProperty_AddsIt()
    {
        var obj = new JSObject();

        var result = obj.DefineProperty("x", PropertyDescriptor.Data(JSValue.FromInt32(42)));

        Assert.True(result);
        Assert.Equal(42, obj.Get("x").ToInt32());
    }

    [Fact]
    public void DefineProperty_WithCustomFlags()
    {
        var obj = new JSObject();

        obj.DefineProperty("readonly", PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: false,
            enumerable: true,
            configurable: true));

        var descriptor = obj.GetOwnPropertyDescriptor("readonly");
        Assert.NotNull(descriptor);
        Assert.False(descriptor!.IsWritable);
        Assert.True(descriptor.IsEnumerable);
        Assert.True(descriptor.IsConfigurable);
    }

    [Fact]
    public void DefineProperty_NonExtensible_Fails()
    {
        var obj = new JSObject();
        obj.PreventExtensions();

        var result = obj.DefineProperty("x", PropertyDescriptor.Data(JSValue.FromInt32(42)));

        Assert.False(result);
        Assert.False(obj.HasProperty("x"));
    }

    [Fact]
    public void DefineProperty_CannotChangeNonConfigurable_TypeChange()
    {
        var obj = new JSObject();
        obj.DefineProperty("prop", PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: true,
            enumerable: true,
            configurable: false));

        // Try to change from data to accessor - should fail
        var result = obj.DefineProperty("prop", PropertyDescriptor.Accessor(
            JSValue.Undefined, JSValue.Undefined, enumerable: true, configurable: false));

        Assert.False(result);
    }

    [Fact]
    public void DefineProperty_IndexedProperty()
    {
        var obj = new JSObject();

        var result = obj.DefineProperty(0, PropertyDescriptor.Data(JSValue.FromString("zero")));

        Assert.True(result);
        Assert.Equal("zero", obj.Get(0).ToString());
    }

    #endregion

    #region GetOwnPropertyDescriptor Tests

    [Fact]
    public void GetOwnPropertyDescriptor_ExistingProperty_ReturnsDescriptor()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var descriptor = obj.GetOwnPropertyDescriptor("x");

        Assert.NotNull(descriptor);
        Assert.True(descriptor!.IsDataDescriptor);
        Assert.Equal(42, descriptor.Value.ToInt32());
    }

    [Fact]
    public void GetOwnPropertyDescriptor_NonExistentProperty_ReturnsNull()
    {
        var obj = new JSObject();

        var descriptor = obj.GetOwnPropertyDescriptor("nonexistent");

        Assert.Null(descriptor);
    }

    [Fact]
    public void GetOwnPropertyDescriptor_ReturnsClone()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var descriptor = obj.GetOwnPropertyDescriptor("x");
        descriptor!.Value = JSValue.FromInt32(100);

        // Original should be unchanged
        Assert.Equal(42, obj.Get("x").ToInt32());
    }

    [Fact]
    public void GetOwnPropertyDescriptor_IndexedProperty()
    {
        var obj = new JSObject();
        obj.Set(5, JSValue.FromString("five"));

        var descriptor = obj.GetOwnPropertyDescriptor(5);

        Assert.NotNull(descriptor);
        Assert.Equal("five", descriptor!.Value.ToString());
    }

    #endregion

    #region Property Enumeration Tests

    [Fact]
    public void GetOwnEnumerablePropertyNames_ReturnsEnumerableOnly()
    {
        var obj = new JSObject();
        obj.Set("enumerable", JSValue.FromInt32(1));
        obj.DefineProperty("hidden", PropertyDescriptor.Data(
            JSValue.FromInt32(2),
            writable: true,
            enumerable: false,
            configurable: true));

        var names = obj.GetOwnEnumerablePropertyNames().ToList();

        Assert.Single(names);
        Assert.Contains("enumerable", names);
        Assert.DoesNotContain("hidden", names);
    }

    [Fact]
    public void GetOwnPropertyNames_ReturnsAll()
    {
        var obj = new JSObject();
        obj.Set("enumerable", JSValue.FromInt32(1));
        obj.DefineProperty("hidden", PropertyDescriptor.Data(
            JSValue.FromInt32(2),
            writable: true,
            enumerable: false,
            configurable: true));

        var names = obj.GetOwnPropertyNames().ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("enumerable", names);
        Assert.Contains("hidden", names);
    }

    [Fact]
    public void GetOwnPropertyIndices_ReturnsAllIndices()
    {
        var obj = new JSObject();
        obj.Set(0, JSValue.FromInt32(10));
        obj.Set(5, JSValue.FromInt32(50));
        obj.Set(10, JSValue.FromInt32(100));

        var indices = obj.GetOwnPropertyIndices().ToList();

        Assert.Equal(3, indices.Count);
        Assert.Contains((uint)0, indices);
        Assert.Contains((uint)5, indices);
        Assert.Contains((uint)10, indices);
    }

    #endregion

    #region Extensibility Tests

    [Fact]
    public void PreventExtensions_BlocksNewProperties()
    {
        var obj = new JSObject();
        obj.Set("existing", JSValue.FromInt32(1));

        obj.PreventExtensions();

        Assert.False(obj.IsExtensible);
        Assert.False(obj.Set("new", JSValue.FromInt32(2)));
        Assert.True(obj.HasProperty("existing"));
        Assert.False(obj.HasProperty("new"));
    }

    [Fact]
    public void PreventExtensions_AllowsModifyingExisting()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        obj.PreventExtensions();

        Assert.True(obj.Set("x", JSValue.FromInt32(2)));
        Assert.Equal(2, obj.Get("x").ToInt32());
    }

    [Fact]
    public void Seal_MakesNonConfigurable()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        obj.Seal();

        Assert.True(obj.IsSealed);
        Assert.False(obj.IsExtensible);

        var descriptor = obj.GetOwnPropertyDescriptor("x");
        Assert.False(descriptor!.IsConfigurable);
        Assert.True(descriptor.IsWritable); // Still writable

        // Can still modify values
        Assert.True(obj.Set("x", JSValue.FromInt32(2)));
        Assert.Equal(2, obj.Get("x").ToInt32());

        // Cannot delete
        Assert.False(obj.Delete("x"));

        // Cannot add new
        Assert.False(obj.Set("y", JSValue.FromInt32(3)));
    }

    [Fact]
    public void Freeze_MakesNonWritable()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        obj.Freeze();

        Assert.True(obj.IsFrozen);
        Assert.True(obj.IsSealed);
        Assert.False(obj.IsExtensible);

        var descriptor = obj.GetOwnPropertyDescriptor("x");
        Assert.False(descriptor!.IsConfigurable);
        Assert.False(descriptor.IsWritable);

        // Cannot modify values
        Assert.False(obj.Set("x", JSValue.FromInt32(2)));
        Assert.Equal(1, obj.Get("x").ToInt32());
    }

    [Fact]
    public void IsSealed_EmptyObject_AfterPreventExtensions()
    {
        var obj = new JSObject();
        obj.PreventExtensions();

        // Empty non-extensible object is considered sealed
        Assert.True(obj.IsSealed);
    }

    [Fact]
    public void IsFrozen_EmptyObject_AfterPreventExtensions()
    {
        var obj = new JSObject();
        obj.PreventExtensions();

        // Empty non-extensible object is considered frozen
        Assert.True(obj.IsFrozen);
    }

    [Fact]
    public void IsSealed_WithConfigurableProperty_ReturnsFalse()
    {
        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));
        obj.PreventExtensions();

        Assert.False(obj.IsSealed); // Property is still configurable
    }

    #endregion

    #region Non-Writable Property Tests

    [Fact]
    public void Set_NonWritableProperty_Fails()
    {
        var obj = new JSObject();
        obj.DefineProperty("readonly", PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: false,
            enumerable: true,
            configurable: true));

        var result = obj.Set("readonly", JSValue.FromInt32(100));

        Assert.False(result);
        Assert.Equal(42, obj.Get("readonly").ToInt32());
    }

    [Fact]
    public void Set_NonWritablePropertyInPrototype_FailsToCreate()
    {
        var proto = new JSObject();
        proto.DefineProperty("prop", PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: false,
            enumerable: true,
            configurable: true));

        var obj = new JSObject(proto);

        // Cannot create own property shadowing non-writable prototype property
        var result = obj.Set("prop", JSValue.FromInt32(100));

        Assert.False(result);
        Assert.Equal(42, obj.Get("prop").ToInt32());
    }

    #endregion

    #region InternalValue Tests

    [Fact]
    public void InternalValue_DefaultIsUndefined()
    {
        var obj = new JSObject();

        Assert.True(obj.InternalValue.IsUndefined);
    }

    [Fact]
    public void InternalValue_CanBeSet()
    {
        var obj = new JSObject(null, JSClassId.Number);
        obj.InternalValue = JSValue.FromInt32(42);

        Assert.Equal(42, obj.InternalValue.ToInt32());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsObjectClassId()
    {
        var obj = new JSObject();
        Assert.Equal("[object Object]", obj.ToString());

        var arr = new JSObject(null, JSClassId.Array);
        Assert.Equal("[object Array]", arr.ToString());

        var date = new JSObject(null, JSClassId.Date);
        Assert.Equal("[object Date]", date.ToString());
    }

    #endregion
}
