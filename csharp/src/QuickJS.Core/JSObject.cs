// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript object with named and indexed properties, a prototype chain,
/// and extensibility control.
/// </summary>
/// <remarks>
/// <para>
/// This class mirrors QuickJS's <c>struct JSObject</c> with the following key components:
/// </para>
/// <list type="bullet">
/// <item><description>A class ID (<see cref="JSClassId"/>) identifying the object type</description></item>
/// <item><description>A prototype reference for prototype chain lookups</description></item>
/// <item><description>Named property storage via a dictionary of <see cref="PropertyDescriptor"/></description></item>
/// <item><description>Indexed property storage for array-like objects</description></item>
/// <item><description>Extensibility flag controlling whether new properties can be added</description></item>
/// </list>
/// <para>
/// In QuickJS, objects use a "shape" system (hidden classes) for optimized property access.
/// This C# implementation uses a simpler dictionary-based approach for clarity, while
/// maintaining the same semantics. A shape-based optimization could be added later.
/// </para>
/// <para>
/// The property access methods follow the ECMAScript specification algorithms:
/// [[Get]], [[Set]], [[Delete]], [[HasProperty]], [[DefineOwnProperty]], etc.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class JSObject
{
    #region Fields

    // Named properties storage. Key is property name (string or atom).
    private readonly Dictionary<string, PropertyDescriptor> _properties;

    // Indexed (numeric) properties for array-like objects.
    private Dictionary<uint, PropertyDescriptor>? _indexedProperties;

    // Prototype chain reference. Null means no prototype (like Object.create(null)).
    private JSObject? _prototype;

    // The class ID determines special behavior (Array, Function, etc.)
    private readonly JSClassId _classId;

    // Extensibility flag - if false, no new properties can be added.
    private bool _extensible;

    // Immutable prototype flag - if true, the prototype cannot be changed.
    // Used for Object.prototype and similar built-in objects.
    private bool _hasImmutablePrototype;

    // Optional internal data for primitive wrappers and special objects
    private JSValue _internalValue;

    // Optional host data for built-ins (e.g., RegExp, Promise, Map backing)
    internal object? HostData;

    // Array length tracking
    private uint _arrayLength;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new JavaScript object with the specified prototype and class ID.
    /// </summary>
    /// <param name="prototype">The prototype object, or null for no prototype.</param>
    /// <param name="classId">The class ID for this object. Defaults to <see cref="JSClassId.Object"/>.</param>
    public JSObject(JSObject? prototype = null, JSClassId classId = JSClassId.Object)
    {
        _prototype = prototype;
        _classId = classId;
        _extensible = true;
        _hasImmutablePrototype = false;
        _properties = new Dictionary<string, PropertyDescriptor>();
        _internalValue = JSValue.Undefined;
        _arrayLength = 0;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the class ID for this object.
    /// </summary>
    public JSClassId ClassId => _classId;

    /// <summary>
    /// Gets or sets the prototype of this object.
    /// </summary>
    /// <remarks>
    /// Setting the prototype uses <see cref="SetPrototype"/> internally.
    /// The setter will not throw on failure; use <see cref="SetPrototype"/> directly
    /// if you need to check the return value.
    /// </remarks>
    public JSObject? Prototype
    {
        get => _prototype;
        set => SetPrototype(value);
    }

    /// <summary>
    /// Gets a value indicating whether this object has an immutable prototype.
    /// Objects with immutable prototypes cannot have their prototype changed.
    /// </summary>
    public bool HasImmutablePrototype => _hasImmutablePrototype;

    /// <summary>
    /// Gets a value indicating whether this object is extensible
    /// (can have new properties added).
    /// </summary>
    public bool IsExtensible => _extensible;

    /// <summary>
    /// Gets the array length (for Array objects).
    /// </summary>
    public uint ArrayLength => _arrayLength;

    /// <summary>
    /// Gets the number of own properties (not including prototype properties).
    /// </summary>
    public int OwnPropertyCount => _properties.Count + (_indexedProperties?.Count ?? 0);

    /// <summary>
    /// Gets or sets the internal value for primitive wrapper objects.
    /// </summary>
    /// <remarks>
    /// This is used for Number, String, Boolean, Symbol, and BigInt wrapper objects
    /// to store their primitive value.
    /// </remarks>
    internal JSValue InternalValue
    {
        get => _internalValue;
        set => _internalValue = value;
    }

    #endregion

    #region Property Access - Get

    /// <summary>
    /// Gets the value of a named property, following the prototype chain.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value, or <see cref="JSValue.Undefined"/> if not found.</returns>
    public JSValue Get(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        // String wrapper special cases
        if (_classId == JSClassId.String)
        {
            if (propertyName == "length")
            {
                var s = _internalValue.ToString() ?? string.Empty;
                return JSValue.FromInt32(s.Length);
            }
        }
        else if (_classId == JSClassId.Array && propertyName == "length")
        {
            return JSValue.FromInt32((int)_arrayLength);
        }

        // Look up in own properties first
        if (_properties.TryGetValue(propertyName, out var descriptor))
        {
            return GetValueFromDescriptor(descriptor);
        }

        // Walk the prototype chain
        var proto = _prototype;
        while (proto != null)
        {
            if (proto._properties.TryGetValue(propertyName, out descriptor))
            {
                return proto.GetValueFromDescriptor(descriptor);
            }
            proto = proto._prototype;
        }

        return JSValue.Undefined;
    }

    /// <summary>
    /// Enumerates own enumerable properties with descriptors.
    /// </summary>
    public IReadOnlyDictionary<string, PropertyDescriptor> GetOwnProperties() => _properties;

    /// <summary>
    /// Gets the value of an indexed property.
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <returns>The property value, or <see cref="JSValue.Undefined"/> if not found.</returns>
    public JSValue Get(uint index)
    {
        // String wrapper indexed access
        if (_classId == JSClassId.String)
        {
            var s = _internalValue.ToString() ?? string.Empty;
            if (index < (uint)s.Length)
            {
                return JSValue.FromString(s[(int)index].ToString());
            }
            return JSValue.Undefined;
        }

        // Look up in own indexed properties first
        if (_indexedProperties != null && _indexedProperties.TryGetValue(index, out var descriptor))
        {
            return GetValueFromDescriptor(descriptor);
        }

        // Walk the prototype chain
        var proto = _prototype;
        while (proto != null)
        {
            if (proto._indexedProperties != null && proto._indexedProperties.TryGetValue(index, out descriptor))
            {
                return proto.GetValueFromDescriptor(descriptor);
            }
            proto = proto._prototype;
        }

        return JSValue.Undefined;
    }

    /// <summary>
    /// Gets the value from a property descriptor.
    /// </summary>
    private JSValue GetValueFromDescriptor(PropertyDescriptor descriptor)
    {
        if (descriptor.IsDataDescriptor)
        {
            return descriptor.Value;
        }

        // Accessor descriptor - need to call getter
        // For now, return undefined if no getter or not callable
        if (descriptor.Getter.IsUndefined)
        {
            return JSValue.Undefined;
        }

        // TODO: Actually call the getter function when we have a runtime context
        // For now, just return undefined
        return JSValue.Undefined;
    }

    #endregion

    #region Property Access - Set

    /// <summary>
    /// Sets the value of a named property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if the property was set successfully, false otherwise.</returns>
    public bool Set(string propertyName, JSValue value)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        // Check if we have an own property
        if (_properties.TryGetValue(propertyName, out var descriptor))
        {
            return SetWithDescriptor(descriptor, value);
        }

        // Check prototype chain for accessor or non-writable data property
        var proto = _prototype;
        while (proto != null)
        {
            if (proto._properties.TryGetValue(propertyName, out var protoDesc))
            {
                if (protoDesc.IsAccessorDescriptor)
                {
                    // Accessor found in prototype - call setter on this object
                    if (protoDesc.Setter.IsUndefined)
                    {
                        return false; // No setter
                    }
                    // TODO: Call setter
                    return false;
                }

                if (!protoDesc.IsWritable)
                {
                    return false; // Non-writable data property in prototype
                }

                break; // Writable data property - create own property
            }
            proto = proto._prototype;
        }

        // Create new own property if extensible
        if (!_extensible)
        {
            return false;
        }

        _properties[propertyName] = PropertyDescriptor.Data(value);
        return true;
    }

    /// <summary>
    /// Sets the value of an indexed property.
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if the property was set successfully, false otherwise.</returns>
    public bool Set(uint index, JSValue value)
    {
        // Check if we have an own indexed property
        if (_indexedProperties != null && _indexedProperties.TryGetValue(index, out var descriptor))
        {
            return SetWithDescriptor(descriptor, value);
        }

        // Create new own indexed property if extensible
        if (!_extensible)
        {
            return false;
        }

        _indexedProperties ??= new Dictionary<uint, PropertyDescriptor>();
        _indexedProperties[index] = PropertyDescriptor.Data(value);

        if (_classId == JSClassId.Array)
        {
            var newLen = index + 1;
            if (newLen > _arrayLength)
                _arrayLength = newLen;
        }
        return true;
    }

    /// <summary>
    /// Removes the last element for array objects and returns its value, or undefined if empty.
    /// </summary>
    public JSValue ArrayPop()
    {
        if (_classId != JSClassId.Array || _arrayLength == 0)
            return JSValue.Undefined;

        uint lastIndex = _arrayLength - 1;
        JSValue result = JSValue.Undefined;
        if (_indexedProperties != null && _indexedProperties.TryGetValue(lastIndex, out var desc))
        {
            result = GetValueFromDescriptor(desc);
            _indexedProperties.Remove(lastIndex);
        }
        _arrayLength = lastIndex;
        return result;
    }

    /// <summary>
    /// Sets the array length (truncating elements when shrinking).
    /// </summary>
    public void SetArrayLength(uint newLength)
    {
        if (_classId != JSClassId.Array)
            return;

        if (newLength < _arrayLength && _indexedProperties != null)
        {
            var keysToRemove = _indexedProperties.Keys.Where(k => k >= newLength).ToList();
            foreach (var k in keysToRemove)
                _indexedProperties.Remove(k);
        }
        _arrayLength = newLength;
    }

    /// <summary>
    /// Sets value through an existing descriptor.
    /// </summary>
    private bool SetWithDescriptor(PropertyDescriptor descriptor, JSValue value)
    {
        if (descriptor.IsDataDescriptor)
        {
            if (!descriptor.IsWritable)
            {
                return false;
            }
            descriptor.Value = value;
            return true;
        }

        // Accessor descriptor
        if (descriptor.Setter.IsUndefined)
        {
            return false;
        }

        // TODO: Call setter
        return false;
    }

    #endregion

    #region Property Access - Has

    /// <summary>
    /// Checks if the object has a named property (own or inherited).
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>True if the property exists, false otherwise.</returns>
    public bool HasProperty(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        if (_properties.ContainsKey(propertyName))
        {
            return true;
        }

        var proto = _prototype;
        while (proto != null)
        {
            if (proto._properties.ContainsKey(propertyName))
            {
                return true;
            }
            proto = proto._prototype;
        }

        return false;
    }

    /// <summary>
    /// Checks if the object has an indexed property (own or inherited).
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <returns>True if the property exists, false otherwise.</returns>
    public bool HasProperty(uint index)
    {
        if (_indexedProperties != null && _indexedProperties.ContainsKey(index))
        {
            return true;
        }

        var proto = _prototype;
        while (proto != null)
        {
            if (proto._indexedProperties != null && proto._indexedProperties.ContainsKey(index))
            {
                return true;
            }
            proto = proto._prototype;
        }

        return false;
    }

    /// <summary>
    /// Checks if the object has an own named property (not inherited).
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>True if the object has an own property with this name, false otherwise.</returns>
    public bool HasOwnProperty(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        return _properties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Checks if the object has an own indexed property (not inherited).
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <returns>True if the object has an own property at this index, false otherwise.</returns>
    public bool HasOwnProperty(uint index)
    {
        return _indexedProperties != null && _indexedProperties.ContainsKey(index);
    }

    #endregion

    #region Property Access - Delete

    /// <summary>
    /// Deletes a named property from the object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>True if the property was deleted or didn't exist, false if non-configurable.</returns>
    public bool Delete(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        if (!_properties.TryGetValue(propertyName, out var descriptor))
        {
            return true; // Property doesn't exist
        }

        if (!descriptor.IsConfigurable)
        {
            return false; // Cannot delete non-configurable property
        }

        return _properties.Remove(propertyName);
    }

    /// <summary>
    /// Deletes an indexed property from the object.
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <returns>True if the property was deleted or didn't exist, false if non-configurable.</returns>
    public bool Delete(uint index)
    {
        if (_indexedProperties == null || !_indexedProperties.TryGetValue(index, out var descriptor))
        {
            return true; // Property doesn't exist
        }

        if (!descriptor.IsConfigurable)
        {
            return false; // Cannot delete non-configurable property
        }

        return _indexedProperties.Remove(index);
    }

    #endregion

    #region Property Definition

    /// <summary>
    /// Defines a named property with a descriptor.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="descriptor">The property descriptor.</param>
    /// <returns>True if the property was defined successfully, false otherwise.</returns>
    /// <remarks>
    /// This implements the [[DefineOwnProperty]] internal method from the ECMAScript spec.
    /// </remarks>
    public bool DefineProperty(string propertyName, PropertyDescriptor descriptor)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        // Check if property already exists
        if (_properties.TryGetValue(propertyName, out var existing))
        {
            return RedefineProperty(existing, descriptor, propertyName, isIndexed: false);
        }

        // New property - check extensibility
        if (!_extensible)
        {
            return false;
        }

        _properties[propertyName] = descriptor.Clone();
        return true;
    }

    /// <summary>
    /// Defines an indexed property with a descriptor.
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <param name="descriptor">The property descriptor.</param>
    /// <returns>True if the property was defined successfully, false otherwise.</returns>
    public bool DefineProperty(uint index, PropertyDescriptor descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        // Check if property already exists
        if (_indexedProperties != null && _indexedProperties.TryGetValue(index, out var existing))
        {
            return RedefineProperty(existing, descriptor, index.ToString(), isIndexed: true, index);
        }

        // New property - check extensibility
        if (!_extensible)
        {
            return false;
        }

        _indexedProperties ??= new Dictionary<uint, PropertyDescriptor>();
        _indexedProperties[index] = descriptor.Clone();
        return true;
    }

    /// <summary>
    /// Attempts to redefine an existing property according to ECMAScript rules.
    /// </summary>
    private bool RedefineProperty(PropertyDescriptor existing, PropertyDescriptor newDesc,
        string propertyName, bool isIndexed, uint index = 0)
    {
        // If the existing property is not configurable, there are restrictions
        if (!existing.IsConfigurable)
        {
            // Cannot change to accessor if data (or vice versa)
            if (existing.IsDataDescriptor != newDesc.IsDataDescriptor)
            {
                return false;
            }

            // Cannot change enumerable if not configurable
            if (existing.IsEnumerable != newDesc.IsEnumerable)
            {
                return false;
            }

            if (existing.IsDataDescriptor)
            {
                // Cannot change writable from false to true
                if (!existing.IsWritable && newDesc.IsWritable)
                {
                    return false;
                }

                // Cannot change value if not writable
                if (!existing.IsWritable && !existing.Value.Equals(newDesc.Value))
                {
                    return false;
                }
            }
            else
            {
                // Cannot change getter or setter if not configurable
                // (simplified check - full spec is more complex)
                return false;
            }
        }

        // Apply the new descriptor
        var cloned = newDesc.Clone();
        if (isIndexed)
        {
            _indexedProperties![index] = cloned;
        }
        else
        {
            _properties[propertyName] = cloned;
        }

        return true;
    }

    #endregion

    #region Property Descriptor Access

    /// <summary>
    /// Gets the own property descriptor for a named property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property descriptor, or null if the property doesn't exist.</returns>
    public PropertyDescriptor? GetOwnPropertyDescriptor(string propertyName)
    {
        if (propertyName == null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        if (_properties.TryGetValue(propertyName, out var descriptor))
        {
            return descriptor.Clone();
        }

        return null;
    }

    /// <summary>
    /// Gets the own property descriptor for an indexed property.
    /// </summary>
    /// <param name="index">The property index.</param>
    /// <returns>The property descriptor, or null if the property doesn't exist.</returns>
    public PropertyDescriptor? GetOwnPropertyDescriptor(uint index)
    {
        if (_indexedProperties != null && _indexedProperties.TryGetValue(index, out var descriptor))
        {
            return descriptor.Clone();
        }

        return null;
    }

    #endregion

    #region Property Enumeration

    /// <summary>
    /// Gets the names of all own enumerable properties.
    /// </summary>
    /// <returns>An enumerable of property names.</returns>
    public IEnumerable<string> GetOwnEnumerablePropertyNames()
    {
        foreach (var kvp in _properties)
        {
            if (kvp.Value.IsEnumerable)
            {
                yield return kvp.Key;
            }
        }
    }

    /// <summary>
    /// Gets the names of all own properties (enumerable and non-enumerable).
    /// </summary>
    /// <returns>An enumerable of property names.</returns>
    public IEnumerable<string> GetOwnPropertyNames()
    {
        foreach (var kvp in _properties)
        {
            yield return kvp.Key;
        }
    }

    /// <summary>
    /// Tries to get the property descriptor for an own property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="descriptor">When this method returns, contains the descriptor if found.</param>
    /// <returns>True if the property exists on this object (not inherited).</returns>
    public bool TryGetOwnPropertyDescriptor(string propertyName, out PropertyDescriptor descriptor)
    {
        return _properties.TryGetValue(propertyName, out descriptor!);
    }

    /// <summary>
    /// Gets the indices of all own indexed properties.
    /// </summary>
    /// <returns>An enumerable of property indices.</returns>
    public IEnumerable<uint> GetOwnPropertyIndices()
    {
        if (_indexedProperties != null)
        {
            foreach (var index in _indexedProperties.Keys)
            {
                yield return index;
            }
        }
    }

    #endregion

    #region Extensibility Control

    /// <summary>
    /// Prevents any new properties from being added to the object.
    /// Existing properties can still be modified or deleted (if configurable).
    /// </summary>
    /// <returns>True if the operation succeeded.</returns>
    public bool PreventExtensions()
    {
        _extensible = false;
        return true;
    }

    /// <summary>
    /// Seals the object: prevents new properties and makes all existing properties non-configurable.
    /// Existing writable properties can still be modified.
    /// </summary>
    /// <returns>True if the operation succeeded.</returns>
    public bool Seal()
    {
        _extensible = false;

        foreach (var kvp in _properties)
        {
            kvp.Value.SetConfigurable(false);
        }

        if (_indexedProperties != null)
        {
            foreach (var kvp in _indexedProperties)
            {
                kvp.Value.SetConfigurable(false);
            }
        }

        return true;
    }

    /// <summary>
    /// Freezes the object: prevents new properties, makes all existing properties
    /// non-configurable, and makes all data properties non-writable.
    /// </summary>
    /// <returns>True if the operation succeeded.</returns>
    public bool Freeze()
    {
        _extensible = false;

        foreach (var kvp in _properties)
        {
            kvp.Value.SetConfigurable(false);
            if (kvp.Value.IsDataDescriptor)
            {
                kvp.Value.SetWritable(false);
            }
        }

        if (_indexedProperties != null)
        {
            foreach (var kvp in _indexedProperties)
            {
                kvp.Value.SetConfigurable(false);
                if (kvp.Value.IsDataDescriptor)
                {
                    kvp.Value.SetWritable(false);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the object is sealed (not extensible and all properties non-configurable).
    /// </summary>
    public bool IsSealed
    {
        get
        {
            if (_extensible) return false;

            foreach (var kvp in _properties)
            {
                if (kvp.Value.IsConfigurable) return false;
            }

            if (_indexedProperties != null)
            {
                foreach (var kvp in _indexedProperties)
                {
                    if (kvp.Value.IsConfigurable) return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Checks if the object is frozen (sealed and all data properties non-writable).
    /// </summary>
    public bool IsFrozen
    {
        get
        {
            if (_extensible) return false;

            foreach (var kvp in _properties)
            {
                if (kvp.Value.IsConfigurable) return false;
                if (kvp.Value.IsDataDescriptor && kvp.Value.IsWritable) return false;
            }

            if (_indexedProperties != null)
            {
                foreach (var kvp in _indexedProperties)
                {
                    if (kvp.Value.IsConfigurable) return false;
                    if (kvp.Value.IsDataDescriptor && kvp.Value.IsWritable) return false;
                }
            }

            return true;
        }
    }

    #endregion

    #region Prototype Operations

    /// <summary>
    /// Sets the prototype of this object.
    /// </summary>
    /// <param name="prototype">The new prototype, or null for no prototype.</param>
    /// <returns>
    /// True if the prototype was set successfully.
    /// False if the object has an immutable prototype, is not extensible,
    /// or if setting the prototype would create a cycle.
    /// </returns>
    /// <remarks>
    /// This method implements the [[SetPrototypeOf]] internal method from the ECMAScript spec.
    /// It performs the following checks:
    /// <list type="number">
    /// <item><description>If the prototype is the same as the current one, return true.</description></item>
    /// <item><description>If the object has an immutable prototype, return false.</description></item>
    /// <item><description>If the object is not extensible, return false.</description></item>
    /// <item><description>If setting the prototype would create a cycle, return false.</description></item>
    /// </list>
    /// </remarks>
    public bool SetPrototype(JSObject? prototype)
    {
        // Same prototype - no change needed
        if (ReferenceEquals(_prototype, prototype))
        {
            return true;
        }

        // Check for immutable prototype
        if (_hasImmutablePrototype)
        {
            return false;
        }

        // Check extensibility
        if (!_extensible)
        {
            return false;
        }

        // Check for circular prototype chain
        if (prototype != null && WouldCreatePrototypeCycle(prototype))
        {
            return false;
        }

        _prototype = prototype;
        return true;
    }

    /// <summary>
    /// Sets the prototype of this object, throwing an exception on failure.
    /// </summary>
    /// <param name="prototype">The new prototype, or null for no prototype.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the prototype cannot be set due to immutability, non-extensibility,
    /// or if it would create a circular prototype chain.
    /// </exception>
    public void SetPrototypeOrThrow(JSObject? prototype)
    {
        // Same prototype - no change needed
        if (ReferenceEquals(_prototype, prototype))
        {
            return;
        }

        // Check for immutable prototype
        if (_hasImmutablePrototype)
        {
            throw new InvalidOperationException("Cannot set prototype: object has an immutable prototype.");
        }

        // Check extensibility
        if (!_extensible)
        {
            throw new InvalidOperationException("Cannot set prototype: object is not extensible.");
        }

        // Check for circular prototype chain
        if (prototype != null && WouldCreatePrototypeCycle(prototype))
        {
            throw new InvalidOperationException("Cannot set prototype: would create a circular prototype chain.");
        }

        _prototype = prototype;
    }

    /// <summary>
    /// Checks if setting the specified object as this object's prototype would create a cycle.
    /// </summary>
    private bool WouldCreatePrototypeCycle(JSObject newPrototype)
    {
        var current = newPrototype;
        while (current != null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
            current = current._prototype;
        }
        return false;
    }

    /// <summary>
    /// Makes this object's prototype immutable.
    /// Once set, the prototype cannot be changed.
    /// </summary>
    /// <remarks>
    /// This is used for built-in objects like Object.prototype which
    /// should not have their prototype modified.
    /// </remarks>
    internal void SetImmutablePrototype()
    {
        _hasImmutablePrototype = true;
    }

    /// <summary>
    /// Gets the prototype of this object as a JSValue.
    /// Returns JSValue.Null if this object has no prototype.
    /// </summary>
    /// <returns>A JSValue containing the prototype object, or JSValue.Null.</returns>
    public JSValue GetPrototypeValue()
    {
        return _prototype != null ? JSValue.FromObject(_prototype) : JSValue.Null;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new object with the specified prototype.
    /// Equivalent to Object.create(proto).
    /// </summary>
    /// <param name="prototype">The prototype for the new object, or null for no prototype.</param>
    /// <returns>A new JSObject with the specified prototype.</returns>
    public static JSObject Create(JSObject? prototype)
    {
        return new JSObject(prototype);
    }

    /// <summary>
    /// Creates a new object with the specified prototype and properties.
    /// Equivalent to Object.create(proto, propertiesObject).
    /// </summary>
    /// <param name="prototype">The prototype for the new object, or null for no prototype.</param>
    /// <param name="properties">
    /// A dictionary of property names to descriptors to define on the new object.
    /// </param>
    /// <returns>A new JSObject with the specified prototype and properties.</returns>
    public static JSObject Create(JSObject? prototype, IDictionary<string, PropertyDescriptor> properties)
    {
        var obj = new JSObject(prototype);

        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                obj.DefineProperty(kvp.Key, kvp.Value);
            }
        }

        return obj;
    }

    /// <summary>
    /// Assigns all enumerable own properties from one or more source objects to a target object.
    /// Equivalent to Object.assign(target, ...sources).
    /// </summary>
    /// <param name="target">The target object to copy properties to.</param>
    /// <param name="sources">One or more source objects to copy properties from.</param>
    /// <returns>The target object.</returns>
    public static JSObject Assign(JSObject target, params JSObject[] sources)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (sources == null)
        {
            return target;
        }

        foreach (var source in sources)
        {
            if (source == null)
            {
                continue;
            }

            foreach (var name in source.GetOwnEnumerablePropertyNames())
            {
                var value = source.Get(name);
                target.Set(name, value);
            }

            foreach (var index in source.GetOwnPropertyIndices())
            {
                var descriptor = source.GetOwnPropertyDescriptor(index);
                if (descriptor != null && descriptor.IsEnumerable)
                {
                    target.Set(index, source.Get(index));
                }
            }
        }

        return target;
    }

    /// <summary>
    /// Gets the names of all own enumerable string-keyed properties.
    /// Equivalent to Object.keys(obj).
    /// </summary>
    /// <returns>An array of property names.</returns>
    public string[] Keys()
    {
        var keys = new List<string>();
        foreach (var kvp in _properties)
        {
            if (kvp.Value.IsEnumerable)
            {
                keys.Add(kvp.Key);
            }
        }
        return keys.ToArray();
    }

    /// <summary>
    /// Gets the values of all own enumerable string-keyed properties.
    /// Equivalent to Object.values(obj).
    /// </summary>
    /// <returns>An array of property values.</returns>
    public JSValue[] Values()
    {
        var values = new List<JSValue>();
        foreach (var kvp in _properties)
        {
            if (kvp.Value.IsEnumerable)
            {
                values.Add(kvp.Value.IsDataDescriptor ? kvp.Value.Value : JSValue.Undefined);
            }
        }
        return values.ToArray();
    }

    /// <summary>
    /// Gets key-value pairs for all own enumerable string-keyed properties.
    /// Equivalent to Object.entries(obj).
    /// </summary>
    /// <returns>An array of [key, value] tuples.</returns>
    public (string Key, JSValue Value)[] Entries()
    {
        var entries = new List<(string, JSValue)>();
        foreach (var kvp in _properties)
        {
            if (kvp.Value.IsEnumerable)
            {
                var value = kvp.Value.IsDataDescriptor ? kvp.Value.Value : JSValue.Undefined;
                entries.Add((kvp.Key, value));
            }
        }
        return entries.ToArray();
    }

    /// <summary>
    /// Creates a new object from key-value pairs.
    /// Equivalent to Object.fromEntries(iterable).
    /// </summary>
    /// <param name="entries">An enumerable of key-value pairs.</param>
    /// <param name="prototype">Optional prototype for the new object.</param>
    /// <returns>A new JSObject with properties from the entries.</returns>
    public static JSObject FromEntries(IEnumerable<(string Key, JSValue Value)> entries, JSObject? prototype = null)
    {
        var obj = new JSObject(prototype);

        foreach (var (key, value) in entries)
        {
            obj.Set(key, value);
        }

        return obj;
    }

    #endregion

    #region Debug Support

    private string DebuggerDisplay
    {
        get
        {
            var typeName = _classId.ToString();
            return $"[{typeName}] {OwnPropertyCount} properties";
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[object {_classId}]";
    }

    #endregion
}
