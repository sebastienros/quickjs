// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSValue object-related methods.
/// </summary>
public class JSValueObjectTests
{
    #region FromObject Tests

    [Fact]
    public void FromObject_CreatesObjectValue()
    {
        var jsObject = new JSObject();
        var value = JSValue.FromObject(jsObject);

        Assert.True(value.IsObject);
        Assert.Equal(JSValueType.Object, value.Tag);
    }

    [Fact]
    public void FromObject_Null_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => JSValue.FromObject(null!));
    }

    [Fact]
    public void FromObject_PreservesObjectReference()
    {
        var jsObject = new JSObject();
        jsObject.Set("test", JSValue.FromInt32(42));

        var value = JSValue.FromObject(jsObject);
        var retrieved = value.AsObject();

        Assert.Same(jsObject, retrieved);
        Assert.Equal(42, retrieved.Get("test").ToInt32());
    }

    #endregion

    #region TryGetObject Tests

    [Fact]
    public void TryGetObject_ObjectValue_ReturnsTrue()
    {
        var jsObject = new JSObject();
        var value = JSValue.FromObject(jsObject);

        Assert.True(value.TryGetObject(out var result));
        Assert.Same(jsObject, result);
    }

    [Fact]
    public void TryGetObject_NonObjectValue_ReturnsFalse()
    {
        var values = new[]
        {
            JSValue.Undefined,
            JSValue.Null,
            JSValue.True,
            JSValue.False,
            JSValue.FromInt32(42),
            JSValue.FromDouble(3.14),
            JSValue.FromString("hello"),
        };

        foreach (var value in values)
        {
            Assert.False(value.TryGetObject(out var result));
            Assert.Null(result);
        }
    }

    #endregion

    #region AsObject Tests

    [Fact]
    public void AsObject_ObjectValue_ReturnsObject()
    {
        var jsObject = new JSObject();
        var value = JSValue.FromObject(jsObject);

        var result = value.AsObject();

        Assert.Same(jsObject, result);
    }

    [Fact]
    public void AsObject_NonObjectValue_Throws()
    {
        var value = JSValue.FromInt32(42);

        Assert.Throws<System.InvalidOperationException>(() => value.AsObject());
    }

    [Fact]
    public void AsObject_Undefined_Throws()
    {
        Assert.Throws<System.InvalidOperationException>(() => JSValue.Undefined.AsObject());
    }

    [Fact]
    public void AsObject_Null_Throws()
    {
        Assert.Throws<System.InvalidOperationException>(() => JSValue.Null.AsObject());
    }

    #endregion

    #region IsObject Tests

    [Fact]
    public void IsObject_ObjectValue_ReturnsTrue()
    {
        var value = JSValue.FromObject(new JSObject());
        Assert.True(value.IsObject);
    }

    [Fact]
    public void IsObject_PrimitiveValues_ReturnsFalse()
    {
        Assert.False(JSValue.Undefined.IsObject);
        Assert.False(JSValue.Null.IsObject);
        Assert.False(JSValue.True.IsObject);
        Assert.False(JSValue.False.IsObject);
        Assert.False(JSValue.FromInt32(42).IsObject);
        Assert.False(JSValue.FromDouble(3.14).IsObject);
        Assert.False(JSValue.FromString("hello").IsObject);
    }

    #endregion

    #region ToBoolean Tests (Object-related)

    [Fact]
    public void ToBoolean_Object_ReturnsTrue()
    {
        // All objects are truthy in JavaScript
        var value = JSValue.FromObject(new JSObject());
        Assert.True(value.ToBoolean());
    }

    [Fact]
    public void ToBoolean_EmptyObject_ReturnsTrue()
    {
        // Even empty objects are truthy
        var value = JSValue.FromObject(new JSObject());
        Assert.True(value.ToBoolean());
    }

    #endregion

    #region ToString Tests (Object-related)

    [Fact]
    public void ToString_Object_ReturnsObjectString()
    {
        var value = JSValue.FromObject(new JSObject());
        Assert.Equal("[object Object]", value.ToString());
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameObject_ReturnsTrue()
    {
        var jsObject = new JSObject();
        var value1 = JSValue.FromObject(jsObject);
        var value2 = JSValue.FromObject(jsObject);

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Equals_DifferentObjects_ReturnsFalse()
    {
        var obj1 = new JSObject();
        var obj2 = new JSObject();
        var value1 = JSValue.FromObject(obj1);
        var value2 = JSValue.FromObject(obj2);

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Equals_ObjectAndNull_ReturnsFalse()
    {
        var value = JSValue.FromObject(new JSObject());
        Assert.NotEqual(value, JSValue.Null);
    }

    [Fact]
    public void Equals_ObjectAndUndefined_ReturnsFalse()
    {
        var value = JSValue.FromObject(new JSObject());
        Assert.NotEqual(value, JSValue.Undefined);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ObjectWithProperties_RoundTrip()
    {
        var jsObject = new JSObject();
        jsObject.Set("name", JSValue.FromString("test"));
        jsObject.Set("value", JSValue.FromInt32(123));

        var value = JSValue.FromObject(jsObject);
        var retrieved = value.AsObject();

        Assert.Equal("test", retrieved.Get("name").ToString());
        Assert.Equal(123, retrieved.Get("value").ToInt32());
    }

    [Fact]
    public void NestedObjects_RoundTrip()
    {
        var inner = new JSObject();
        inner.Set("x", JSValue.FromInt32(10));

        var outer = new JSObject();
        outer.Set("inner", JSValue.FromObject(inner));

        var outerValue = JSValue.FromObject(outer);
        var retrievedOuter = outerValue.AsObject();
        var retrievedInner = retrievedOuter.Get("inner").AsObject();

        Assert.Equal(10, retrievedInner.Get("x").ToInt32());
    }

    [Fact]
    public void ObjectWithDifferentClassIds()
    {
        var array = new JSObject(null, JSClassId.Array);
        var date = new JSObject(null, JSClassId.Date);
        var func = new JSObject(null, JSClassId.BytecodeFunction);

        var arrayValue = JSValue.FromObject(array);
        var dateValue = JSValue.FromObject(date);
        var funcValue = JSValue.FromObject(func);

        Assert.Equal(JSClassId.Array, arrayValue.AsObject().ClassId);
        Assert.Equal(JSClassId.Date, dateValue.AsObject().ClassId);
        Assert.Equal(JSClassId.BytecodeFunction, funcValue.AsObject().ClassId);
    }

    #endregion
}
