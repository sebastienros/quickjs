# Step 7.13: Proxy Object Implementation

## Overview

The Proxy object is one of the most powerful metaprogramming features in JavaScript, introduced in ES6 (ECMAScript 2015). It allows you to create a wrapper around another object that can intercept and redefine fundamental operations on that object.

## What is a Proxy?

A Proxy wraps another object (the **target**) and intercepts operations on it through customizable functions called **traps** defined in a **handler** object. This enables powerful patterns like:

- **Data validation** - Validate values before assignment
- **Default values** - Provide defaults for missing properties
- **Observable objects** - Track property access and changes
- **Read-only views** - Prevent modifications
- **Virtual properties** - Generate property values on-the-fly
- **Access control** - Hide private properties

## Proxy Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Proxy Object                             │
│  ┌─────────────┐   ┌──────────────┐   ┌────────────────────┐   │
│  │   Target    │   │   Handler    │   │    IsRevoked       │   │
│  │  (JSObject) │   │  (JSObject)  │   │    (boolean)       │   │
│  └─────────────┘   └──────────────┘   └────────────────────┘   │
│                           │                                      │
│               Contains trap methods                              │
│          ┌────────────────┼────────────────┐                    │
│          ▼                ▼                ▼                    │
│    ┌─────────┐      ┌─────────┐      ┌─────────┐              │
│    │   get   │      │   set   │      │   has   │  ...         │
│    └─────────┘      └─────────┘      └─────────┘              │
└─────────────────────────────────────────────────────────────────┘
```

## The 13 Proxy Traps

JavaScript defines 13 internal operations that can be intercepted:

| Trap | Intercepted Operation | Triggered By |
|------|----------------------|--------------|
| `get` | Property read | `proxy.prop`, `proxy[key]` |
| `set` | Property write | `proxy.prop = value` |
| `has` | Property existence check | `key in proxy` |
| `deleteProperty` | Property deletion | `delete proxy.prop` |
| `ownKeys` | Property enumeration | `Object.keys()`, `for...in` |
| `getOwnPropertyDescriptor` | Get descriptor | `Object.getOwnPropertyDescriptor()` |
| `defineProperty` | Define property | `Object.defineProperty()` |
| `getPrototypeOf` | Get prototype | `Object.getPrototypeOf()` |
| `setPrototypeOf` | Set prototype | `Object.setPrototypeOf()` |
| `isExtensible` | Check extensibility | `Object.isExtensible()` |
| `preventExtensions` | Prevent extensions | `Object.preventExtensions()` |
| `apply` | Function call | `proxy()`, `proxy.call()` |
| `construct` | Constructor call | `new proxy()` |

## Implementation Details

### JSProxy Class

```csharp
public sealed class JSProxy : JSObject
{
    private JSObject? _target;
    private JSObject? _handler;
    private bool _revoked;

    public JSProxy(JSObject target, JSObject handler, JSObject? prototype = null)
        : base(prototype, JSClassId.Proxy)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _revoked = false;
    }

    public JSObject Target => _revoked ? throw new JSTypeError(...) : _target!;
    public JSObject Handler => _revoked ? throw new JSTypeError(...) : _handler!;
    public bool IsRevoked => _revoked;

    public void Revoke()
    {
        _revoked = true;
        _target = null;
        _handler = null;
    }
}
```

### Trap Invocation Pattern

Each trap follows the same pattern:

1. Check if proxy is revoked
2. Look for trap method on handler
3. If trap exists, invoke it with appropriate arguments
4. If trap doesn't exist, fall through to target operation

```csharp
public JSValue ProxyGet(string propertyName)
{
    ThrowIfRevoked();

    var trap = GetTrap("get");
    if (trap != null)
    {
        return trap.CallNative(
            JSValue.FromObject(_handler!),
            new[] {
                JSValue.FromObject(_target!),   // target
                JSValue.FromString(propertyName), // property
                JSValue.FromObject(this)          // receiver
            });
    }

    return _target!.Get(propertyName);
}
```

### Revocable Proxies

`Proxy.revocable()` creates a proxy along with a revoke function:

```javascript
const { proxy, revoke } = Proxy.revocable(target, handler);
// Use proxy...
revoke(); // Proxy is now unusable
proxy.foo; // Throws TypeError
```

Implementation:
```csharp
// In JSContext.InitializeProxy():
JSCFunction proxyRevocable = (thisArg, args) =>
{
    var target = args[0].AsObject();
    var handler = args[1].AsObject();
    var proxy = new JSProxy(target, handler);
    
    var result = new JSObject();
    result.Set("proxy", JSValue.FromObject(proxy));
    
    JSCFunction revoke = (_, __) => 
    {
        proxy.Revoke();
        return JSValue.Undefined;
    };
    result.Set("revoke", JSValue.FromObject(new JSFunction(revoke)));
    
    return JSValue.FromObject(result);
};
```

## Use Cases

### 1. Default Values

```javascript
function withDefaults(target, defaults) {
    return new Proxy(target, {
        get(target, prop) {
            return prop in target ? target[prop] : defaults[prop];
        }
    });
}

const settings = withDefaults({ volume: 50 }, { volume: 100, brightness: 80 });
settings.volume;     // 50 (from target)
settings.brightness; // 80 (from defaults)
```

### 2. Observable Objects

```javascript
function observable(target, callback) {
    return new Proxy(target, {
        set(target, prop, value) {
            callback(prop, target[prop], value);
            target[prop] = value;
            return true;
        }
    });
}

const obj = observable({ x: 1 }, (prop, oldVal, newVal) => {
    console.log(`${prop}: ${oldVal} → ${newVal}`);
});
obj.x = 2; // Logs: "x: 1 → 2"
```

### 3. Read-Only View

```javascript
function readOnly(target) {
    return new Proxy(target, {
        set() { return false; },
        deleteProperty() { return false; },
        defineProperty() { return false; }
    });
}

const frozen = readOnly({ a: 1 });
frozen.a = 2; // Silently fails (or throws in strict mode)
```

### 4. Private Properties

```javascript
function hidePrivate(target) {
    return new Proxy(target, {
        get(target, prop) {
            if (prop.startsWith('_')) return undefined;
            return target[prop];
        },
        has(target, prop) {
            if (prop.startsWith('_')) return false;
            return prop in target;
        },
        ownKeys(target) {
            return Object.keys(target).filter(k => !k.startsWith('_'));
        }
    });
}

const obj = hidePrivate({ public: 1, _private: 2 });
obj.public;     // 1
obj._private;   // undefined
'_private' in obj; // false
Object.keys(obj);  // ['public']
```

### 5. Negative Array Indices

```javascript
const negativeArray = new Proxy([1, 2, 3, 4, 5], {
    get(target, prop) {
        const index = Number(prop);
        if (index < 0) {
            return target[target.length + index];
        }
        return target[prop];
    }
});

negativeArray[-1]; // 5
negativeArray[-2]; // 4
```

## Proxy vs Reflect

Proxy and Reflect are designed to work together. Reflect provides methods that correspond exactly to the proxy traps:

```javascript
const handler = {
    get(target, prop, receiver) {
        console.log(`Getting ${prop}`);
        return Reflect.get(target, prop, receiver);
    },
    set(target, prop, value, receiver) {
        console.log(`Setting ${prop} = ${value}`);
        return Reflect.set(target, prop, value, receiver);
    }
};
```

Using Reflect ensures proper handling of prototype chains and receivers.

## Performance Considerations

Proxies add overhead to every intercepted operation:

1. **Trap lookup** - Check if handler has the trap
2. **Function call** - Invoke the trap function
3. **Argument marshaling** - Create arguments array

For hot code paths, consider:
- Using direct objects when metaprogramming isn't needed
- Caching trap functions
- Minimizing the number of intercepted operations

## Invariants

The Proxy specification enforces certain invariants that traps must respect:

1. **getPrototypeOf** - Must return object/null; must return target's prototype if non-extensible
2. **setPrototypeOf** - Must return false if target is non-extensible and proto differs
3. **isExtensible** - Must return same value as target's extensibility
4. **preventExtensions** - Can only return true if target is non-extensible
5. **getOwnPropertyDescriptor** - Can't report non-existent as existent if non-extensible
6. **defineProperty** - Can't add property to non-extensible target
7. **has** - Can't hide non-configurable own property
8. **get** - Must return same value for non-writable, non-configurable data property
9. **set** - Can't change non-writable, non-configurable property
10. **deleteProperty** - Can't delete non-configurable property
11. **ownKeys** - Must include all non-configurable own properties

## Files Modified/Created

### New Files
- `src/QuickJS.Core/JSProxy.cs` - Proxy object implementation with all 13 traps
- `tests/QuickJS.Tests/ProxyTests.cs` - 32 tests covering proxy functionality
- `docs/52-proxy.md` - This documentation file

### Modified Files
- `src/QuickJS.Core/JSContext.cs` - Added `InitializeProxy()` method

## Test Coverage

The proxy tests cover:
- Basic proxy creation and properties
- Transparent proxies (no traps)
- Each trap type (get, set, has, delete, ownKeys, etc.)
- Apply and construct traps for function proxies
- Proxy revocation
- Real-world use cases (default values, read-only, observable, private properties)
- Nested proxies

## References

- [MDN: Proxy](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy)
- [MDN: Reflect](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Reflect)
- [ECMAScript Specification: Proxy Objects](https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots)
- [JavaScript Metaprogramming with Proxies](https://exploringjs.com/deep-js/ch_proxies.html)
