// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSProxy object implementation.
/// </summary>
public class ProxyTests : IDisposable
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;

    public ProxyTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _runtime.Dispose();
    }

    #region Basic Proxy Creation

    [Fact]
    public void Proxy_CreateWithTargetAndHandler()
    {
        var target = new JSObject();
        var handler = new JSObject();

        var proxy = new JSProxy(target, handler);

        Assert.NotNull(proxy);
        Assert.Equal(target, proxy.Target);
        Assert.Equal(handler, proxy.Handler);
        Assert.Equal(JSClassId.Proxy, proxy.ClassId);
    }

    [Fact]
    public void Proxy_IsNotRevokedByDefault()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        Assert.False(proxy.IsRevoked);
    }

    [Fact]
    public void Proxy_ThrowsOnNullTarget()
    {
        Assert.Throws<ArgumentNullException>(() => new JSProxy(null!, new JSObject()));
    }

    [Fact]
    public void Proxy_ThrowsOnNullHandler()
    {
        Assert.Throws<ArgumentNullException>(() => new JSProxy(new JSObject(), null!));
    }

    [Fact]
    public void Proxy_WithPrototype_IncludesPrototype()
    {
        var proto = new JSObject();
        var target = new JSObject();
        var handler = new JSObject();

        var proxy = new JSProxy(target, handler, proto);

        Assert.Equal(proto, proxy.Prototype);
    }

    #endregion

    #region Transparent Proxy (No Traps)

    [Fact]
    public void Proxy_GetPropertyWithoutTrap_ReturnsTargetValue()
    {
        var target = new JSObject();
        target.Set("value", JSValue.FromDouble(42));

        var proxy = new JSProxy(target, new JSObject());

        var result = proxy.ProxyGet("value");
        Assert.Equal(42.0, result.ToDouble());
    }

    [Fact]
    public void Proxy_SetPropertyWithoutTrap_SetsTargetValue()
    {
        var target = new JSObject();
        var proxy = new JSProxy(target, new JSObject());

        var success = proxy.ProxySet("value", JSValue.FromDouble(100));

        Assert.True(success);
        Assert.Equal(100.0, target.Get("value").ToDouble());
    }

    [Fact]
    public void Proxy_HasPropertyWithoutTrap_ReturnsTargetResult()
    {
        var target = new JSObject();
        target.Set("exists", JSValue.FromDouble(1));

        var proxy = new JSProxy(target, new JSObject());

        Assert.True(proxy.ProxyHas("exists"));
        Assert.False(proxy.ProxyHas("notExists"));
    }

    [Fact]
    public void Proxy_DeletePropertyWithoutTrap_DeletesFromTarget()
    {
        var target = new JSObject();
        target.Set("toDelete", JSValue.FromDouble(1));

        var proxy = new JSProxy(target, new JSObject());

        Assert.True(target.HasProperty("toDelete"));
        var deleted = proxy.ProxyDelete("toDelete");
        Assert.True(deleted);
        Assert.False(target.HasProperty("toDelete"));
    }

    [Fact]
    public void Proxy_OwnKeysWithoutTrap_ReturnsTargetKeys()
    {
        var target = new JSObject();
        target.Set("a", JSValue.FromDouble(1));
        target.Set("b", JSValue.FromDouble(2));

        var proxy = new JSProxy(target, new JSObject());
        var keys = new List<string>(proxy.ProxyOwnKeys());

        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    #endregion

    #region Proxy Get Trap

    [Fact]
    public void Proxy_GetTrap_InterceptsPropertyAccess()
    {
        var target = new JSObject();
        target.Set("value", JSValue.FromDouble(42));

        var handler = new JSObject();
        // Create a trap function that returns a fixed value
        JSCFunction getTrap = (thisArg, args) => JSValue.FromDouble(999);
        handler.Set("get", JSValue.FromObject(new JSFunction(getTrap, "get", 3)));

        var proxy = new JSProxy(target, handler);
        var result = proxy.ProxyGet("value");

        Assert.Equal(999.0, result.ToDouble());
    }

    [Fact]
    public void Proxy_GetTrap_ReceivesPropertyName()
    {
        var target = new JSObject();
        target.Set("test", JSValue.FromDouble(1));

        var handler = new JSObject();
        string? receivedProp = null;
        JSCFunction getTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[1].TryGetString(out var prop))
                receivedProp = prop;
            return JSValue.FromDouble(0);
        };
        handler.Set("get", JSValue.FromObject(new JSFunction(getTrap, "get", 3)));

        var proxy = new JSProxy(target, handler);
        proxy.ProxyGet("test");

        Assert.Equal("test", receivedProp);
    }

    #endregion

    #region Proxy Set Trap

    [Fact]
    public void Proxy_SetTrap_InterceptsPropertyAssignment()
    {
        var target = new JSObject();

        var handler = new JSObject();
        double? setValueReceived = null;
        JSCFunction setTrap = (thisArg, args) =>
        {
            if (args.Length > 2)
                setValueReceived = args[2].ToDouble();
            return JSValue.FromBoolean(true);
        };
        handler.Set("set", JSValue.FromObject(new JSFunction(setTrap, "set", 4)));

        var proxy = new JSProxy(target, handler);
        proxy.ProxySet("x", JSValue.FromDouble(100));

        Assert.Equal(100.0, setValueReceived);
    }

    [Fact]
    public void Proxy_SetTrap_ReturnsTrapResult()
    {
        var target = new JSObject();

        var handler = new JSObject();
        JSCFunction setTrap = (thisArg, args) => JSValue.FromBoolean(false);
        handler.Set("set", JSValue.FromObject(new JSFunction(setTrap, "set", 4)));

        var proxy = new JSProxy(target, handler);
        var result = proxy.ProxySet("x", JSValue.FromDouble(1));

        Assert.False(result);
    }

    #endregion

    #region Proxy Has Trap

    [Fact]
    public void Proxy_HasTrap_InterceptsPropertyCheck()
    {
        var target = new JSObject();
        target.Set("real", JSValue.FromDouble(1));

        var handler = new JSObject();
        JSCFunction hasTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[1].TryGetString(out var prop))
            {
                // Report "virtual" as existing even though it's not on target
                return JSValue.FromBoolean(prop == "virtual" || prop == "real");
            }
            return JSValue.FromBoolean(false);
        };
        handler.Set("has", JSValue.FromObject(new JSFunction(hasTrap, "has", 2)));

        var proxy = new JSProxy(target, handler);

        Assert.True(proxy.ProxyHas("real"));
        Assert.True(proxy.ProxyHas("virtual"));
        Assert.False(proxy.ProxyHas("nonexistent"));
    }

    #endregion

    #region Proxy Delete Trap

    [Fact]
    public void Proxy_DeleteTrap_InterceptsDeletion()
    {
        var target = new JSObject();
        target.Set("protected", JSValue.FromDouble(1));
        target.Set("normal", JSValue.FromDouble(2));

        var handler = new JSObject();
        JSCFunction deleteTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[1].TryGetString(out var prop))
            {
                // Prevent deletion of "protected"
                if (prop == "protected")
                    return JSValue.FromBoolean(false);
                // Allow deletion of others
                if (args[0].TryGetObject(out var targetObj))
                    targetObj.Delete(prop);
                return JSValue.FromBoolean(true);
            }
            return JSValue.FromBoolean(false);
        };
        handler.Set("deleteProperty", JSValue.FromObject(new JSFunction(deleteTrap, "deleteProperty", 2)));

        var proxy = new JSProxy(target, handler);

        var r1 = proxy.ProxyDelete("protected");
        var r2 = proxy.ProxyDelete("normal");

        Assert.False(r1); // protected was not deleted
        Assert.True(r2);  // normal was deleted
        Assert.True(target.HasProperty("protected"));
        Assert.False(target.HasProperty("normal"));
    }

    #endregion

    #region Proxy OwnKeys Trap

    [Fact]
    public void Proxy_OwnKeysTrap_InterceptsKeyEnumeration()
    {
        var target = new JSObject();
        target.Set("a", JSValue.FromDouble(1));
        target.Set("b", JSValue.FromDouble(2));
        target.Set("_private", JSValue.FromDouble(3));

        var handler = new JSObject();
        JSCFunction ownKeysTrap = (thisArg, args) =>
        {
            // Return only non-private keys
            var array = new JSArray();
            array.Push(JSValue.FromString("a"));
            array.Push(JSValue.FromString("b"));
            return JSValue.FromObject(array);
        };
        handler.Set("ownKeys", JSValue.FromObject(new JSFunction(ownKeysTrap, "ownKeys", 1)));

        var proxy = new JSProxy(target, handler);
        var keys = new List<string>(proxy.ProxyOwnKeys());

        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.DoesNotContain("_private", keys);
    }

    #endregion

    #region Proxy Apply Trap (Function Proxy)

    [Fact]
    public void Proxy_ApplyTrap_InterceptsFunctionCall()
    {
        // Create a simple function to proxy
        JSCFunction originalFn = (thisArg, args) =>
        {
            var a = args.Length > 0 ? args[0].ToDouble() : 0;
            var b = args.Length > 1 ? args[1].ToDouble() : 0;
            return JSValue.FromDouble(a + b);
        };
        var targetFunc = new JSFunction(originalFn, "sum", 2);

        var handler = new JSObject();
        JSCFunction applyTrap = (thisArg, args) =>
        {
            // args[0] = target, args[1] = thisArg, args[2] = arguments array
            if (args.Length > 2 && args[2].TryGetObject(out var argsObj) && argsObj is JSArray argsArray)
            {
                // Call original and double the result
                var a = argsArray.Length > 0 ? argsArray[0].ToDouble() : 0;
                var b = argsArray.Length > 1 ? argsArray[1].ToDouble() : 0;
                var originalResult = a + b;
                return JSValue.FromDouble(originalResult * 2);
            }
            return JSValue.Undefined;
        };
        handler.Set("apply", JSValue.FromObject(new JSFunction(applyTrap, "apply", 3)));

        var proxy = new JSProxy(targetFunc, handler);
        var result = proxy.ProxyApply(JSValue.Undefined,
            new[] { JSValue.FromDouble(3), JSValue.FromDouble(4) });

        // Original: 3 + 4 = 7, doubled = 14
        Assert.Equal(14.0, result.ToDouble());
    }

    #endregion

    #region Proxy Construct Trap

    [Fact]
    public void Proxy_ConstructTrap_InterceptsNew()
    {
        JSCFunction constructorFn = (thisArg, args) => JSValue.Undefined;
        var targetFunc = new JSFunction(constructorFn, "Constructor", 0);

        var handler = new JSObject();
        JSCFunction constructTrap = (thisArg, args) =>
        {
            // Create a custom object instead
            var obj = new JSObject();
            obj.Set("proxied", JSValue.FromBoolean(true));
            return JSValue.FromObject(obj);
        };
        handler.Set("construct", JSValue.FromObject(new JSFunction(constructTrap, "construct", 3)));

        var proxy = new JSProxy(targetFunc, handler);
        var result = proxy.ProxyConstruct(Array.Empty<JSValue>());

        Assert.True(result.IsObject);
        Assert.True(result.AsObject().Get("proxied").IsTrue);
    }

    #endregion

    #region Proxy Revocation

    [Fact]
    public void Proxy_Revoke_SetsIsRevokedTrue()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());

        Assert.False(proxy.IsRevoked);
        proxy.Revoke();
        Assert.True(proxy.IsRevoked);
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnTargetAccess()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => _ = proxy.Target);
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnHandlerAccess()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => _ = proxy.Handler);
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnProxyGet()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => proxy.ProxyGet("x"));
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnProxySet()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => proxy.ProxySet("x", JSValue.FromDouble(1)));
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnProxyHas()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => proxy.ProxyHas("x"));
    }

    [Fact]
    public void Proxy_Revoke_ThrowsOnProxyDelete()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());
        proxy.Revoke();

        Assert.Throws<JSTypeError>(() => proxy.ProxyDelete("x"));
    }

    [Fact]
    public void Proxy_Revoke_MultipleRevokesAreIdempotent()
    {
        var proxy = new JSProxy(new JSObject(), new JSObject());

        proxy.Revoke();
        proxy.Revoke();
        proxy.Revoke();

        Assert.True(proxy.IsRevoked);
    }

    #endregion

    #region Use Case: Default Values

    [Fact]
    public void Proxy_UseCase_DefaultValues()
    {
        var target = new JSObject();
        target.Set("volume", JSValue.FromDouble(50));

        var defaults = new JSObject();
        defaults.Set("volume", JSValue.FromDouble(100));
        defaults.Set("brightness", JSValue.FromDouble(80));

        var handler = new JSObject();
        JSCFunction getTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                if (targetObj.HasProperty(prop))
                    return targetObj.Get(prop);
                return defaults.Get(prop);
            }
            return JSValue.Undefined;
        };
        handler.Set("get", JSValue.FromObject(new JSFunction(getTrap, "get", 3)));

        var proxy = new JSProxy(target, handler);

        Assert.Equal(50.0, proxy.ProxyGet("volume").ToDouble()); // From target
        Assert.Equal(80.0, proxy.ProxyGet("brightness").ToDouble()); // From defaults
    }

    #endregion

    #region Use Case: Read-Only Object

    [Fact]
    public void Proxy_UseCase_ReadOnlyObject()
    {
        var target = new JSObject();
        target.Set("value", JSValue.FromDouble(42));

        var handler = new JSObject();
        JSCFunction setTrap = (thisArg, args) => JSValue.FromBoolean(false);
        JSCFunction deleteTrap = (thisArg, args) => JSValue.FromBoolean(false);

        handler.Set("set", JSValue.FromObject(new JSFunction(setTrap, "set", 4)));
        handler.Set("deleteProperty", JSValue.FromObject(new JSFunction(deleteTrap, "deleteProperty", 2)));

        var proxy = new JSProxy(target, handler);

        // Read works (no trap, falls through to target)
        Assert.Equal(42.0, proxy.ProxyGet("value").ToDouble());

        // Write is blocked
        var setResult = proxy.ProxySet("value", JSValue.FromDouble(100));
        Assert.False(setResult);
        Assert.Equal(42.0, target.Get("value").ToDouble()); // Still original

        // Delete is blocked
        var deleteResult = proxy.ProxyDelete("value");
        Assert.False(deleteResult);
        Assert.True(target.HasProperty("value")); // Still exists
    }

    #endregion

    #region Use Case: Observable Object

    [Fact]
    public void Proxy_UseCase_ObservableObject()
    {
        var target = new JSObject();
        target.Set("x", JSValue.FromDouble(1));

        var changes = new List<(string prop, double oldVal, double newVal)>();

        var handler = new JSObject();
        JSCFunction setTrap = (thisArg, args) =>
        {
            if (args.Length > 2 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                var newValue = args[2].ToDouble();
                var oldValue = targetObj.HasProperty(prop) ? targetObj.Get(prop).ToDouble() : 0;
                changes.Add((prop, oldValue, newValue));
                targetObj.Set(prop, args[2]);
                return JSValue.FromBoolean(true);
            }
            return JSValue.FromBoolean(false);
        };
        handler.Set("set", JSValue.FromObject(new JSFunction(setTrap, "set", 4)));

        var proxy = new JSProxy(target, handler);

        proxy.ProxySet("x", JSValue.FromDouble(2));
        proxy.ProxySet("y", JSValue.FromDouble(3));

        Assert.Equal(2, changes.Count);
        Assert.Equal(("x", 1.0, 2.0), changes[0]);
        Assert.Equal(("y", 0.0, 3.0), changes[1]);
    }

    #endregion

    #region Use Case: Private Properties

    [Fact]
    public void Proxy_UseCase_PrivateProperties()
    {
        var target = new JSObject();
        target.Set("public", JSValue.FromDouble(1));
        target.Set("_private", JSValue.FromDouble(2));

        var handler = new JSObject();

        // Hide _private from get
        JSCFunction getTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                if (prop.StartsWith("_"))
                    return JSValue.Undefined;
                return targetObj.Get(prop);
            }
            return JSValue.Undefined;
        };

        // Hide _private from has
        JSCFunction hasTrap = (thisArg, args) =>
        {
            if (args.Length > 1 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                if (prop.StartsWith("_"))
                    return JSValue.FromBoolean(false);
                return JSValue.FromBoolean(targetObj.HasProperty(prop));
            }
            return JSValue.FromBoolean(false);
        };

        // Hide _private from ownKeys
        JSCFunction ownKeysTrap = (thisArg, args) =>
        {
            if (args.Length > 0 && args[0].TryGetObject(out var targetObj))
            {
                var array = new JSArray();
                foreach (var key in targetObj.GetOwnPropertyNames())
                {
                    if (!key.StartsWith("_"))
                        array.Push(JSValue.FromString(key));
                }
                return JSValue.FromObject(array);
            }
            return JSValue.FromObject(new JSArray());
        };

        handler.Set("get", JSValue.FromObject(new JSFunction(getTrap, "get", 3)));
        handler.Set("has", JSValue.FromObject(new JSFunction(hasTrap, "has", 2)));
        handler.Set("ownKeys", JSValue.FromObject(new JSFunction(ownKeysTrap, "ownKeys", 1)));

        var proxy = new JSProxy(target, handler);

        Assert.Equal(1.0, proxy.ProxyGet("public").ToDouble());
        Assert.True(proxy.ProxyGet("_private").IsUndefined);
        Assert.True(proxy.ProxyHas("public"));
        Assert.False(proxy.ProxyHas("_private"));

        var keys = new List<string>(proxy.ProxyOwnKeys());
        Assert.Contains("public", keys);
        Assert.DoesNotContain("_private", keys);
    }

    #endregion

    #region Nested Proxy

    [Fact]
    public void Proxy_NestedProxy_BothInterceptorsAreCalled()
    {
        var callLog = new List<string>();

        var innerTarget = new JSObject();
        innerTarget.Set("value", JSValue.FromDouble(1));

        var innerHandler = new JSObject();
        JSCFunction innerGetTrap = (thisArg, args) =>
        {
            callLog.Add("inner");
            if (args.Length > 1 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                return targetObj.Get(prop);
            }
            return JSValue.Undefined;
        };
        innerHandler.Set("get", JSValue.FromObject(new JSFunction(innerGetTrap, "get", 3)));

        var innerProxy = new JSProxy(innerTarget, innerHandler);

        var outerHandler = new JSObject();
        JSCFunction outerGetTrap = (thisArg, args) =>
        {
            callLog.Add("outer");
            if (args.Length > 1 && args[0].TryGetObject(out var targetObj) && args[1].TryGetString(out var prop))
            {
                if (targetObj is JSProxy proxyObj)
                    return proxyObj.ProxyGet(prop);
                return targetObj.Get(prop);
            }
            return JSValue.Undefined;
        };
        outerHandler.Set("get", JSValue.FromObject(new JSFunction(outerGetTrap, "get", 3)));

        var outerProxy = new JSProxy(innerProxy, outerHandler);

        var result = outerProxy.ProxyGet("value");

        Assert.Equal(1.0, result.ToDouble());
        Assert.Equal(new[] { "outer", "inner" }, callLog.ToArray());
    }

    #endregion
}
