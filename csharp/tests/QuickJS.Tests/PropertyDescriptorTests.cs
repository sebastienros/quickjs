// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="PropertyDescriptor"/> and <see cref="PropertyFlags"/>.
/// </summary>
public class PropertyDescriptorTests
{
    #region PropertyFlags Tests

    [Fact]
    public void PropertyFlags_None_HasNoFlags()
    {
        var flags = PropertyFlags.None;
        Assert.Equal(0, (int)flags);
    }

    [Fact]
    public void PropertyFlags_Default_HasAllFlags()
    {
        var flags = PropertyFlags.Default;
        Assert.True((flags & PropertyFlags.Configurable) != 0);
        Assert.True((flags & PropertyFlags.Writable) != 0);
        Assert.True((flags & PropertyFlags.Enumerable) != 0);
    }

    [Fact]
    public void PropertyFlags_CanCombine()
    {
        var flags = PropertyFlags.Writable | PropertyFlags.Enumerable;
        Assert.True((flags & PropertyFlags.Writable) != 0);
        Assert.True((flags & PropertyFlags.Enumerable) != 0);
        Assert.False((flags & PropertyFlags.Configurable) != 0);
    }

    #endregion

    #region Data Descriptor Tests

    [Fact]
    public void DataDescriptor_WithValue_StoresValue()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);

        Assert.True(descriptor.IsDataDescriptor);
        Assert.False(descriptor.IsAccessorDescriptor);
        Assert.Equal(42, descriptor.Value.ToInt32());
    }

    [Fact]
    public void DataDescriptor_DefaultFlags_HasAllFlags()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);

        Assert.True(descriptor.IsConfigurable);
        Assert.True(descriptor.IsWritable);
        Assert.True(descriptor.IsEnumerable);
    }

    [Fact]
    public void DataDescriptor_NoFlags_IsReadOnlyNonEnumerableNonConfigurable()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.None);

        Assert.False(descriptor.IsConfigurable);
        Assert.False(descriptor.IsWritable);
        Assert.False(descriptor.IsEnumerable);
    }

    [Fact]
    public void DataDescriptor_CanUpdateValue()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);
        descriptor.Value = JSValue.FromInt32(100);

        Assert.Equal(100, descriptor.Value.ToInt32());
    }

    [Fact]
    public void DataDescriptor_Data_FactoryMethod_CreatesWithDefaults()
    {
        var descriptor = PropertyDescriptor.Data(JSValue.FromString("hello"));

        Assert.True(descriptor.IsDataDescriptor);
        Assert.True(descriptor.IsConfigurable);
        Assert.True(descriptor.IsWritable);
        Assert.True(descriptor.IsEnumerable);
        Assert.Equal("hello", descriptor.Value.ToString());
    }

    [Fact]
    public void DataDescriptor_Data_WithFlags_FactoryMethod()
    {
        var descriptor = PropertyDescriptor.Data(
            JSValue.FromInt32(42),
            writable: false,
            enumerable: true,
            configurable: false);

        Assert.False(descriptor.IsWritable);
        Assert.True(descriptor.IsEnumerable);
        Assert.False(descriptor.IsConfigurable);
    }

    [Fact]
    public void DataDescriptor_ReadOnly_FactoryMethod()
    {
        var descriptor = PropertyDescriptor.ReadOnly(JSValue.FromInt32(42));

        Assert.False(descriptor.IsWritable);
        Assert.True(descriptor.IsEnumerable);
        Assert.False(descriptor.IsConfigurable);
    }

    #endregion

    #region Accessor Descriptor Tests

    [Fact]
    public void AccessorDescriptor_WithGetterAndSetter_StoresValues()
    {
        var getter = JSValue.FromObject(new JSObject());
        var setter = JSValue.FromObject(new JSObject());

        var descriptor = new PropertyDescriptor(getter, setter, PropertyFlags.Default);

        Assert.True(descriptor.IsAccessorDescriptor);
        Assert.False(descriptor.IsDataDescriptor);
        Assert.Equal(getter, descriptor.Getter);
        Assert.Equal(setter, descriptor.Setter);
    }

    [Fact]
    public void AccessorDescriptor_IgnoresWritableFlag()
    {
        var descriptor = new PropertyDescriptor(
            JSValue.Undefined,
            JSValue.Undefined,
            PropertyFlags.Writable | PropertyFlags.Enumerable);

        // Writable is not applicable to accessor descriptors
        Assert.False(descriptor.IsWritable);
        Assert.True(descriptor.IsEnumerable);
    }

    [Fact]
    public void AccessorDescriptor_GetValue_Throws()
    {
        var descriptor = new PropertyDescriptor(JSValue.Undefined, JSValue.Undefined, PropertyFlags.None);

        Assert.Throws<InvalidOperationException>(() => descriptor.Value);
    }

    [Fact]
    public void AccessorDescriptor_SetValue_Throws()
    {
        var descriptor = new PropertyDescriptor(JSValue.Undefined, JSValue.Undefined, PropertyFlags.None);

        Assert.Throws<InvalidOperationException>(() => descriptor.Value = JSValue.FromInt32(42));
    }

    [Fact]
    public void AccessorDescriptor_Accessor_FactoryMethod()
    {
        var getter = JSValue.FromObject(new JSObject());
        var setter = JSValue.Undefined;

        var descriptor = PropertyDescriptor.Accessor(getter, setter, enumerable: true, configurable: true);

        Assert.True(descriptor.IsAccessorDescriptor);
        Assert.Equal(getter, descriptor.Getter);
        Assert.True(descriptor.Setter.IsUndefined);
        Assert.True(descriptor.IsEnumerable);
        Assert.True(descriptor.IsConfigurable);
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_DataDescriptor_CreatesIndependentCopy()
    {
        var original = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);
        var clone = original.Clone();

        clone.Value = JSValue.FromInt32(100);

        Assert.Equal(42, original.Value.ToInt32());
        Assert.Equal(100, clone.Value.ToInt32());
    }

    [Fact]
    public void Clone_AccessorDescriptor_CreatesIndependentCopy()
    {
        var getter = JSValue.FromObject(new JSObject());
        var original = new PropertyDescriptor(getter, JSValue.Undefined, PropertyFlags.Enumerable);
        var clone = original.Clone();

        Assert.True(clone.IsAccessorDescriptor);
        Assert.Equal(getter, clone.Getter);
        Assert.True(clone.IsEnumerable);
    }

    #endregion

    #region Internal Methods Tests

    [Fact]
    public void SetConfigurable_ChangesFlag()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);

        descriptor.SetConfigurable(false);
        Assert.False(descriptor.IsConfigurable);

        descriptor.SetConfigurable(true);
        Assert.True(descriptor.IsConfigurable);
    }

    [Fact]
    public void SetEnumerable_ChangesFlag()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);

        descriptor.SetEnumerable(false);
        Assert.False(descriptor.IsEnumerable);

        descriptor.SetEnumerable(true);
        Assert.True(descriptor.IsEnumerable);
    }

    [Fact]
    public void SetWritable_OnDataDescriptor_ChangesFlag()
    {
        var descriptor = new PropertyDescriptor(JSValue.FromInt32(42), PropertyFlags.Default);

        descriptor.SetWritable(false);
        Assert.False(descriptor.IsWritable);

        descriptor.SetWritable(true);
        Assert.True(descriptor.IsWritable);
    }

    [Fact]
    public void SetWritable_OnAccessorDescriptor_Throws()
    {
        var descriptor = new PropertyDescriptor(JSValue.Undefined, JSValue.Undefined, PropertyFlags.None);

        Assert.Throws<InvalidOperationException>(() => descriptor.SetWritable(true));
    }

    #endregion
}
