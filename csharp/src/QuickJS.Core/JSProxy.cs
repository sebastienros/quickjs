// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript Proxy object that wraps another object and intercepts operations.
/// </summary>
/// <remarks>
/// <para>
/// The Proxy object enables creation of a virtual object that wraps another object (the target)
/// and can intercept fundamental operations like property lookup, assignment, enumeration,
/// function invocation, etc.
/// </para>
/// <para>
/// This corresponds to the ES6 Proxy specification: https://tc39.es/ecma262/#sec-proxy-objects
/// </para>
/// </remarks>
public sealed class JSProxy : JSObject
{
    private JSObject? _target;
    private JSObject? _handler;
    private bool _revoked;

    /// <summary>
    /// Creates a new Proxy that wraps the target with the specified handler.
    /// </summary>
    /// <param name="target">The object to wrap.</param>
    /// <param name="handler">The handler object containing trap methods.</param>
    /// <param name="prototype">Optional prototype for the proxy object.</param>
    public JSProxy(JSObject target, JSObject handler, JSObject? prototype = null)
        : base(prototype, JSClassId.Proxy)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _revoked = false;
    }

    /// <summary>
    /// Gets the target object being wrapped.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the proxy has been revoked.</exception>
    public JSObject Target
    {
        get
        {
            ThrowIfRevoked();
            return _target!;
        }
    }

    /// <summary>
    /// Gets the handler object containing trap methods.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the proxy has been revoked.</exception>
    public JSObject Handler
    {
        get
        {
            ThrowIfRevoked();
            return _handler!;
        }
    }

    /// <summary>
    /// Gets whether this proxy has been revoked.
    /// </summary>
    public bool IsRevoked => _revoked;

    /// <summary>
    /// Revokes the proxy, making it unusable.
    /// </summary>
    public void Revoke()
    {
        _revoked = true;
        _target = null;
        _handler = null;
    }

    /// <summary>
    /// Throws if the proxy has been revoked.
    /// </summary>
    private void ThrowIfRevoked()
    {
        if (_revoked)
        {
            throw new JSTypeError("Cannot perform operation on a revoked proxy");
        }
    }

    #region Trap Methods

    /// <summary>
    /// Invokes the get trap if defined, otherwise returns the target property.
    /// </summary>
    public JSValue ProxyGet(string propertyName)
    {
        ThrowIfRevoked();

        var trap = GetTrap("get");
        if (trap != null)
        {
            return trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName),
                    JSValue.FromObject(this)
                });
        }

        return _target!.Get(propertyName);
    }

    /// <summary>
    /// Invokes the set trap if defined, otherwise sets the target property.
    /// </summary>
    public bool ProxySet(string propertyName, JSValue value)
    {
        ThrowIfRevoked();

        var trap = GetTrap("set");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName),
                    value,
                    JSValue.FromObject(this)
                });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.Set(propertyName, value);
    }

    /// <summary>
    /// Invokes the has trap if defined, otherwise checks the target.
    /// </summary>
    public bool ProxyHas(string propertyName)
    {
        ThrowIfRevoked();

        var trap = GetTrap("has");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName)
                });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.HasProperty(propertyName);
    }

    /// <summary>
    /// Invokes the deleteProperty trap if defined, otherwise deletes from target.
    /// </summary>
    public bool ProxyDelete(string propertyName)
    {
        ThrowIfRevoked();

        var trap = GetTrap("deleteProperty");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName)
                });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.Delete(propertyName);
    }

    /// <summary>
    /// Invokes the ownKeys trap if defined, otherwise returns target's own keys.
    /// </summary>
    public IEnumerable<string> ProxyOwnKeys()
    {
        ThrowIfRevoked();

        var trap = GetTrap("ownKeys");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] { JSValue.FromObject(_target!) });

            if (result.IsObject && result.AsObject() is JSArray array)
            {
                var keys = new List<string>();
                for (uint i = 0; i < array.Length; i++)
                {
                    // Use the indexer to get array elements, not Get(string)
                    keys.Add(JSValueConversion.ToString(array[i]));
                }
                return keys;
            }
            return Array.Empty<string>();
        }

        return _target!.GetOwnPropertyNames();
    }

    /// <summary>
    /// Invokes the getOwnPropertyDescriptor trap if defined.
    /// </summary>
    public bool ProxyGetOwnPropertyDescriptor(string propertyName, out PropertyDescriptor? descriptor)
    {
        ThrowIfRevoked();

        var trap = GetTrap("getOwnPropertyDescriptor");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName)
                });

            if (result.IsUndefined)
            {
                descriptor = null;
                return false;
            }

            if (result.IsObject)
            {
                var descObj = result.AsObject();
                var value = descObj.Get("value");
                var writable = JSValueConversion.ToBoolean(descObj.Get("writable"));
                var enumerable = JSValueConversion.ToBoolean(descObj.Get("enumerable"));
                var configurable = JSValueConversion.ToBoolean(descObj.Get("configurable"));

                var flags = PropertyFlags.None;
                if (writable) flags |= PropertyFlags.Writable;
                if (enumerable) flags |= PropertyFlags.Enumerable;
                if (configurable) flags |= PropertyFlags.Configurable;

                descriptor = new PropertyDescriptor(value, flags);
                return true;
            }

            descriptor = null;
            return false;
        }

        return _target!.TryGetOwnPropertyDescriptor(propertyName, out descriptor);
    }

    /// <summary>
    /// Invokes the defineProperty trap if defined.
    /// </summary>
    public bool ProxyDefineProperty(string propertyName, PropertyDescriptor descriptor)
    {
        ThrowIfRevoked();

        var trap = GetTrap("defineProperty");
        if (trap != null)
        {
            var descObj = new JSObject();
            if (descriptor.IsDataDescriptor)
            {
                descObj.Set("value", descriptor.Value);
                descObj.Set("writable", JSValue.FromBoolean(descriptor.IsWritable));
            }
            else
            {
                descObj.Set("get", descriptor.Getter);
                descObj.Set("set", descriptor.Setter);
            }
            descObj.Set("enumerable", JSValue.FromBoolean(descriptor.IsEnumerable));
            descObj.Set("configurable", JSValue.FromBoolean(descriptor.IsConfigurable));

            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromString(propertyName),
                    JSValue.FromObject(descObj)
                });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.DefineProperty(propertyName, descriptor);
    }

    /// <summary>
    /// Invokes the getPrototypeOf trap if defined.
    /// </summary>
    public JSObject? GetProxyPrototype()
    {
        ThrowIfRevoked();

        var trap = GetTrap("getPrototypeOf");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] { JSValue.FromObject(_target!) });

            if (result.IsNull)
                return null;
            if (result.IsObject)
                return result.AsObject();

            throw new JSTypeError("getPrototypeOf trap must return object or null");
        }

        return _target!.Prototype;
    }

    /// <summary>
    /// Invokes the setPrototypeOf trap if defined.
    /// </summary>
    public bool SetProxyPrototype(JSObject? prototype)
    {
        ThrowIfRevoked();

        var trap = GetTrap("setPrototypeOf");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    prototype != null ? JSValue.FromObject(prototype) : JSValue.Null
                });
            return JSValueConversion.ToBoolean(result);
        }

        _target!.Prototype = prototype;
        return true;
    }

    /// <summary>
    /// Invokes the isExtensible trap if defined.
    /// </summary>
    public bool IsProxyExtensible()
    {
        ThrowIfRevoked();

        var trap = GetTrap("isExtensible");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] { JSValue.FromObject(_target!) });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.IsExtensible;
    }

    /// <summary>
    /// Invokes the preventExtensions trap if defined.
    /// </summary>
    public bool PreventProxyExtensions()
    {
        ThrowIfRevoked();

        var trap = GetTrap("preventExtensions");
        if (trap != null)
        {
            var result = trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] { JSValue.FromObject(_target!) });
            return JSValueConversion.ToBoolean(result);
        }

        return _target!.PreventExtensions();
    }

    /// <summary>
    /// Invokes the apply trap if the target is callable.
    /// </summary>
    public JSValue ProxyApply(JSValue thisArg, JSValue[] args)
    {
        ThrowIfRevoked();

        var trap = GetTrap("apply");
        if (trap != null)
        {
            var argsArray = new JSArray();
            foreach (var arg in args)
            {
                argsArray.Push(arg);
            }

            return trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    thisArg,
                    JSValue.FromObject(argsArray)
                });
        }

        // If no apply trap and target is callable, call it
        if (_target is JSFunction fn)
        {
            return fn.CallNative(thisArg, args);
        }

        throw new JSTypeError("Target is not callable");
    }

    /// <summary>
    /// Invokes the construct trap if the target is a constructor.
    /// </summary>
    public JSValue ProxyConstruct(JSValue[] args, JSObject? newTarget = null)
    {
        ThrowIfRevoked();

        var trap = GetTrap("construct");
        if (trap != null)
        {
            var argsArray = new JSArray();
            foreach (var arg in args)
            {
                argsArray.Push(arg);
            }

            return trap.CallNative(
                JSValue.FromObject(_handler!),
                new[] {
                    JSValue.FromObject(_target!),
                    JSValue.FromObject(argsArray),
                    newTarget != null ? JSValue.FromObject(newTarget) : JSValue.FromObject(this)
                });
        }

        // If no construct trap and target is a constructor, create new object and call
        if (_target is JSFunction fn)
        {
            // Create a new object with the function's prototype
            var prototype = fn.Get("prototype");
            var newObj = new JSObject(prototype.IsObject ? prototype.AsObject() : null);
            
            // Call the function with the new object as 'this'
            var result = fn.CallNative(JSValue.FromObject(newObj), args);
            
            // If the function returns an object, use it; otherwise return newObj
            if (result.IsObject)
                return result;
            return JSValue.FromObject(newObj);
        }

        throw new JSTypeError("Target is not a constructor");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a trap function from the handler, or null if not defined.
    /// </summary>
    private JSFunction? GetTrap(string trapName)
    {
        var trap = _handler!.Get(trapName);
        if (trap.IsUndefined || trap.IsNull)
            return null;

        if (trap.IsObject && trap.AsObject() is JSFunction fn)
            return fn;

        throw new JSTypeError($"Proxy handler's {trapName} trap is not a function");
    }

    #endregion
}

/// <summary>
/// Represents a revocable proxy with its revoke function.
/// </summary>
public sealed class RevocableProxy
{
    /// <summary>
    /// Gets the proxy object.
    /// </summary>
    public JSProxy Proxy { get; }

    /// <summary>
    /// Gets the revoke function that will invalidate the proxy.
    /// </summary>
    public Action Revoke { get; }

    /// <summary>
    /// Creates a new revocable proxy result.
    /// </summary>
    public RevocableProxy(JSProxy proxy)
    {
        Proxy = proxy;
        Revoke = proxy.Revoke;
    }
}
