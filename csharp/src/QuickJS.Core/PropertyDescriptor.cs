// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Flags controlling property attributes as defined by the ECMAScript specification.
/// </summary>
/// <remarks>
/// <para>
/// These flags correspond to QuickJS's JS_PROP_* constants and the ECMAScript
/// property attribute flags [[Writable]], [[Enumerable]], and [[Configurable]].
/// </para>
/// <para>
/// By default, properties created with <c>Object.defineProperty()</c> have all
/// flags set to false, while properties created via assignment have all flags
/// set to true.
/// </para>
/// </remarks>
[Flags]
public enum PropertyFlags
{
    /// <summary>
    /// No flags set. The property is read-only, non-enumerable, and non-configurable.
    /// </summary>
    None = 0,

    /// <summary>
    /// If set, the property can be deleted and its attributes can be changed.
    /// Corresponds to [[Configurable]] = true.
    /// </summary>
    Configurable = 1 << 0,

    /// <summary>
    /// If set, the property value can be changed (for data properties).
    /// Corresponds to [[Writable]] = true.
    /// </summary>
    Writable = 1 << 1,

    /// <summary>
    /// If set, the property will be included in for-in loops and Object.keys().
    /// Corresponds to [[Enumerable]] = true.
    /// </summary>
    Enumerable = 1 << 2,

    /// <summary>
    /// Convenience constant for all standard attributes set (Configurable | Writable | Enumerable).
    /// This is the default for properties created via assignment.
    /// </summary>
    Default = Configurable | Writable | Enumerable,
}

/// <summary>
/// Internal flags indicating which property attributes have been specified.
/// Used when calling DefineProperty to know which attributes to update.
/// </summary>
[Flags]
internal enum PropertyDefinitionFlags
{
    /// <summary>No attributes specified.</summary>
    None = 0,

    /// <summary>[[Configurable]] attribute is specified.</summary>
    HasConfigurable = 1 << 0,

    /// <summary>[[Writable]] attribute is specified.</summary>
    HasWritable = 1 << 1,

    /// <summary>[[Enumerable]] attribute is specified.</summary>
    HasEnumerable = 1 << 2,

    /// <summary>[[Get]] accessor is specified.</summary>
    HasGet = 1 << 3,

    /// <summary>[[Set]] accessor is specified.</summary>
    HasSet = 1 << 4,

    /// <summary>[[Value]] is specified.</summary>
    HasValue = 1 << 5,
}

/// <summary>
/// Represents a property descriptor as defined by the ECMAScript specification.
/// </summary>
/// <remarks>
/// <para>
/// A property descriptor is either a data descriptor (has value and writable)
/// or an accessor descriptor (has getter and/or setter). It cannot be both.
/// </para>
/// <para>
/// This class mirrors the property storage in QuickJS's JSProperty union, which can hold:
/// - A normal value (JS_PROP_NORMAL)
/// - A getter/setter pair (JS_PROP_GETSET)
/// </para>
/// </remarks>
public sealed class PropertyDescriptor
{
    private JSValue _value;
    private JSValue _getter;
    private JSValue _setter;
    private PropertyFlags _flags;
    private readonly bool _isAccessor;

    #region Constructors

    /// <summary>
    /// Creates a data property descriptor with the specified value and flags.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="flags">The property flags (Configurable, Writable, Enumerable).</param>
    public PropertyDescriptor(JSValue value, PropertyFlags flags = PropertyFlags.Default)
    {
        _value = value;
        _getter = JSValue.Undefined;
        _setter = JSValue.Undefined;
        _flags = flags;
        _isAccessor = false;
    }

    /// <summary>
    /// Creates an accessor property descriptor with the specified getter and setter.
    /// </summary>
    /// <param name="getter">The getter function (or undefined if no getter).</param>
    /// <param name="setter">The setter function (or undefined if no setter).</param>
    /// <param name="flags">The property flags (Configurable, Enumerable). Writable is ignored for accessors.</param>
    public PropertyDescriptor(JSValue getter, JSValue setter, PropertyFlags flags)
    {
        _value = JSValue.Undefined;
        _getter = getter;
        _setter = setter;
        // Writable is not applicable to accessor descriptors
        _flags = flags & ~PropertyFlags.Writable;
        _isAccessor = true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this is an accessor descriptor (has getter/setter).
    /// </summary>
    public bool IsAccessorDescriptor => _isAccessor;

    /// <summary>
    /// Gets a value indicating whether this is a data descriptor (has value/writable).
    /// </summary>
    public bool IsDataDescriptor => !_isAccessor;

    /// <summary>
    /// Gets or sets the property value (for data descriptors).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is an accessor descriptor.</exception>
    public JSValue Value
    {
        get
        {
            if (_isAccessor)
            {
                throw new InvalidOperationException("Cannot get value of an accessor descriptor.");
            }
            return _value;
        }
        set
        {
            if (_isAccessor)
            {
                throw new InvalidOperationException("Cannot set value of an accessor descriptor.");
            }
            _value = value;
        }
    }

    /// <summary>
    /// Gets the getter function (for accessor descriptors).
    /// Returns undefined if no getter is defined.
    /// </summary>
    public JSValue Getter => _getter;

    /// <summary>
    /// Gets the setter function (for accessor descriptors).
    /// Returns undefined if no setter is defined.
    /// </summary>
    public JSValue Setter => _setter;

    /// <summary>
    /// Gets the property flags.
    /// </summary>
    public PropertyFlags Flags => _flags;

    /// <summary>
    /// Gets a value indicating whether the property is configurable.
    /// </summary>
    public bool IsConfigurable => (_flags & PropertyFlags.Configurable) != 0;

    /// <summary>
    /// Gets a value indicating whether the property is writable (data descriptors only).
    /// </summary>
    public bool IsWritable => !_isAccessor && (_flags & PropertyFlags.Writable) != 0;

    /// <summary>
    /// Gets a value indicating whether the property is enumerable.
    /// </summary>
    public bool IsEnumerable => (_flags & PropertyFlags.Enumerable) != 0;

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a data descriptor with default flags (configurable, writable, enumerable).
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <returns>A new data property descriptor.</returns>
    public static PropertyDescriptor Data(JSValue value)
    {
        return new PropertyDescriptor(value, PropertyFlags.Default);
    }

    /// <summary>
    /// Creates a data descriptor with specified PropertyFlags.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="flags">The property flags.</param>
    /// <returns>A new data property descriptor.</returns>
    public static PropertyDescriptor Data(JSValue value, PropertyFlags flags)
    {
        return new PropertyDescriptor(value, flags);
    }

    /// <summary>
    /// Creates a data descriptor with specified flags.
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="writable">Whether the property is writable.</param>
    /// <param name="enumerable">Whether the property is enumerable.</param>
    /// <param name="configurable">Whether the property is configurable.</param>
    /// <returns>A new data property descriptor.</returns>
    public static PropertyDescriptor Data(JSValue value, bool writable, bool enumerable, bool configurable)
    {
        var flags = PropertyFlags.None;
        if (writable) flags |= PropertyFlags.Writable;
        if (enumerable) flags |= PropertyFlags.Enumerable;
        if (configurable) flags |= PropertyFlags.Configurable;
        return new PropertyDescriptor(value, flags);
    }

    /// <summary>
    /// Creates an accessor descriptor.
    /// </summary>
    /// <param name="getter">The getter function (or undefined if no getter).</param>
    /// <param name="setter">The setter function (or undefined if no setter).</param>
    /// <param name="enumerable">Whether the property is enumerable.</param>
    /// <param name="configurable">Whether the property is configurable.</param>
    /// <returns>A new accessor property descriptor.</returns>
    public static PropertyDescriptor Accessor(JSValue getter, JSValue setter, bool enumerable = false, bool configurable = false)
    {
        var flags = PropertyFlags.None;
        if (enumerable) flags |= PropertyFlags.Enumerable;
        if (configurable) flags |= PropertyFlags.Configurable;
        return new PropertyDescriptor(getter, setter, flags);
    }

    /// <summary>
    /// Creates a read-only data descriptor (writable = false).
    /// </summary>
    /// <param name="value">The property value.</param>
    /// <param name="enumerable">Whether the property is enumerable.</param>
    /// <param name="configurable">Whether the property is configurable.</param>
    /// <returns>A new read-only data property descriptor.</returns>
    public static PropertyDescriptor ReadOnly(JSValue value, bool enumerable = true, bool configurable = false)
    {
        var flags = PropertyFlags.None;
        if (enumerable) flags |= PropertyFlags.Enumerable;
        if (configurable) flags |= PropertyFlags.Configurable;
        return new PropertyDescriptor(value, flags);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sets the configurable flag.
    /// </summary>
    /// <param name="configurable">The new value for the configurable flag.</param>
    internal void SetConfigurable(bool configurable)
    {
        if (configurable)
            _flags |= PropertyFlags.Configurable;
        else
            _flags &= ~PropertyFlags.Configurable;
    }

    /// <summary>
    /// Sets the enumerable flag.
    /// </summary>
    /// <param name="enumerable">The new value for the enumerable flag.</param>
    internal void SetEnumerable(bool enumerable)
    {
        if (enumerable)
            _flags |= PropertyFlags.Enumerable;
        else
            _flags &= ~PropertyFlags.Enumerable;
    }

    /// <summary>
    /// Sets the writable flag (only valid for data descriptors).
    /// </summary>
    /// <param name="writable">The new value for the writable flag.</param>
    internal void SetWritable(bool writable)
    {
        if (_isAccessor)
        {
            throw new InvalidOperationException("Cannot set writable flag on accessor descriptor.");
        }

        if (writable)
            _flags |= PropertyFlags.Writable;
        else
            _flags &= ~PropertyFlags.Writable;
    }

    /// <summary>
    /// Creates a copy of this descriptor.
    /// </summary>
    /// <returns>A new PropertyDescriptor with the same values and flags.</returns>
    public PropertyDescriptor Clone()
    {
        if (_isAccessor)
        {
            return new PropertyDescriptor(_getter, _setter, _flags);
        }
        return new PropertyDescriptor(_value, _flags);
    }

    #endregion
}
