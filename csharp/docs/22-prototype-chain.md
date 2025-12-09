# Step 5.2: Prototype Chain Operations

This document explains the prototype chain implementation and Object static methods.

## Overview

JavaScript uses prototypal inheritance - objects can have a prototype object from which they inherit properties. This step enhances our JSObject with:

1. **Validated prototype setting** with cycle detection
2. **Immutable prototype flag** for built-in objects
3. **Object static methods** (create, assign, keys, values, entries, fromEntries)

## Key Concepts

### Prototype Chain

Every JavaScript object can have a prototype - another object from which it inherits properties:

```javascript
const proto = { inherited: 42 };
const obj = Object.create(proto);
console.log(obj.inherited); // 42 (inherited from proto)
```

When accessing a property:
1. Check the object's own properties
2. If not found, check the prototype
3. Continue up the chain until `null` is reached

### Setting Prototypes Safely

Setting a prototype requires validation:

1. **Same value check**: If the new prototype is the same as the current one, succeed immediately
2. **Immutable check**: Some objects (like `Object.prototype`) have immutable prototypes
3. **Extensibility check**: Non-extensible objects cannot have their prototype changed
4. **Cycle detection**: Prevent circular prototype chains

```csharp
public bool SetPrototype(JSObject? prototype)
{
    // Same prototype - no change needed
    if (ReferenceEquals(_prototype, prototype))
        return true;

    // Check for immutable prototype
    if (_hasImmutablePrototype)
        return false;

    // Check extensibility
    if (!_extensible)
        return false;

    // Check for circular prototype chain
    if (prototype != null && WouldCreatePrototypeCycle(prototype))
        return false;

    _prototype = prototype;
    return true;
}
```

### Cycle Detection

A circular prototype chain would cause infinite loops:

```
obj1 -> obj2 -> obj3 -> obj1 (CYCLE!)
```

We detect this by walking up from the proposed prototype:

```csharp
private bool WouldCreatePrototypeCycle(JSObject newPrototype)
{
    var current = newPrototype;
    while (current != null)
    {
        if (ReferenceEquals(current, this))
            return true; // Would create a cycle!
        current = current._prototype;
    }
    return false;
}
```

## Immutable Prototypes

Some built-in objects should never have their prototype changed:

- `Object.prototype` - The root of most prototype chains
- `Function.prototype` - The prototype of all functions
- `Array.prototype` - The prototype of all arrays

```csharp
// Mark Object.prototype as having an immutable prototype
objectPrototype.SetImmutablePrototype();

// Now this fails:
objectPrototype.SetPrototype(null); // Returns false
```

## Object Static Methods

### Object.create

Creates a new object with the specified prototype:

```csharp
// Object.create(proto)
var obj = JSObject.Create(proto);

// Object.create(proto, { x: { value: 42 } })
var obj = JSObject.Create(proto, new Dictionary<string, PropertyDescriptor>
{
    ["x"] = PropertyDescriptor.Data(JSValue.FromInt32(42))
});

// Object.create(null) - object with no prototype
var bare = JSObject.Create(null);
```

### Object.assign

Copies all enumerable own properties from source objects to a target:

```csharp
var target = new JSObject();
var source1 = new JSObject();
source1.Set("a", JSValue.FromInt32(1));

var source2 = new JSObject();
source2.Set("b", JSValue.FromInt32(2));

JSObject.Assign(target, source1, source2);
// target now has { a: 1, b: 2 }
```

Key behaviors:
- Only copies **enumerable** properties
- Later sources overwrite earlier ones
- Returns the target object
- Skips null/undefined sources

### Object.keys / values / entries

```csharp
var obj = new JSObject();
obj.Set("x", JSValue.FromInt32(10));
obj.Set("y", JSValue.FromInt32(20));

string[] keys = obj.Keys();         // ["x", "y"]
JSValue[] values = obj.Values();    // [10, 20]
var entries = obj.Entries();        // [("x", 10), ("y", 20)]
```

These methods only return **enumerable** properties.

### Object.fromEntries

Creates an object from key-value pairs:

```csharp
var entries = new List<(string, JSValue)>
{
    ("a", JSValue.FromInt32(1)),
    ("b", JSValue.FromString("hello")),
};

var obj = JSObject.FromEntries(entries);
// obj has { a: 1, b: "hello" }
```

## Comparison with QuickJS C

### QuickJS Implementation

QuickJS's `JS_SetPrototypeInternal` function:

```c
static int JS_SetPrototypeInternal(JSContext *ctx, JSValueConst obj,
                                   JSValueConst proto_val, BOOL throw_flag)
{
    // ... validation ...
    
    if (unlikely(p->has_immutable_prototype)) {
        if (throw_flag) {
            JS_ThrowTypeError(ctx, "prototype is immutable");
            return -1;
        }
        return FALSE;
    }
    
    if (unlikely(!p->extensible)) {
        // ... throw or return false ...
    }
    
    // Check for cycle
    if (proto) {
        p1 = proto;
        do {
            if (p1 == p) {
                // ... throw or return false (circular) ...
            }
            p1 = p1->shape->proto;
        } while (p1 != NULL);
    }
    
    // Update prototype
    sh = p->shape;
    sh->proto = proto;
    return TRUE;
}
```

### Our C# Implementation

We follow the same logic but in idiomatic C#:

- `SetPrototype(proto)` - Returns bool, never throws
- `SetPrototypeOrThrow(proto)` - Throws with descriptive error messages
- `HasImmutablePrototype` property for inspection
- `SetImmutablePrototype()` internal method for built-ins

## Usage Examples

### Basic Prototype Chain

```csharp
var animal = new JSObject();
animal.Set("speak", JSValue.FromString("..."));

var dog = new JSObject(animal);
dog.Set("speak", JSValue.FromString("Woof!"));
dog.Set("bark", JSValue.FromString("Bark!"));

var myDog = new JSObject(dog);

Console.WriteLine(myDog.Get("speak"));  // "Woof!" (from dog)
Console.WriteLine(myDog.Get("bark"));   // "Bark!" (from dog)
```

### Preventing Prototype Modification

```csharp
var proto = new JSObject();
var obj = new JSObject(proto);

// Method 1: Make object non-extensible
obj.PreventExtensions();
obj.SetPrototype(new JSObject()); // Returns false

// Method 2: Make prototype immutable
var immutableObj = new JSObject(proto);
immutableObj.SetImmutablePrototype();
immutableObj.SetPrototype(new JSObject()); // Returns false
```

### Creating Objects Without Prototype

```csharp
// Similar to Object.create(null) in JavaScript
var bare = JSObject.Create(null);

// This object has no inherited properties
bare.HasProperty("toString");  // false
bare.HasProperty("valueOf");   // false
```

## API Summary

### JSObject Instance Methods

| Method | Description |
|--------|-------------|
| `SetPrototype(proto)` | Sets prototype, returns success |
| `SetPrototypeOrThrow(proto)` | Sets prototype, throws on failure |
| `GetPrototypeValue()` | Returns prototype as JSValue |
| `Keys()` | Returns enumerable property names |
| `Values()` | Returns enumerable property values |
| `Entries()` | Returns key-value pairs |

### JSObject Instance Properties

| Property | Description |
|----------|-------------|
| `Prototype` | Gets or sets the prototype |
| `HasImmutablePrototype` | Whether prototype is immutable |

### JSObject Static Methods

| Method | Description |
|--------|-------------|
| `Create(proto)` | Creates object with prototype |
| `Create(proto, props)` | Creates with prototype and properties |
| `Assign(target, sources)` | Copies enumerable properties |
| `FromEntries(entries)` | Creates object from key-value pairs |

### Internal Methods

| Method | Description |
|--------|-------------|
| `SetImmutablePrototype()` | Marks prototype as immutable |

## Test Coverage

The implementation includes tests for:

- SetPrototype success and failure cases
- Circular prototype chain detection
- Immutable prototype behavior
- Non-extensible object prototype handling
- Object.create with and without properties
- Object.assign with single and multiple sources
- Keys/Values/Entries enumeration
- FromEntries object creation
- Prototype property getter/setter behavior

## Files Changed

| File | Description |
|------|-------------|
| `JSObject.cs` | Added SetPrototype, immutable flag, static methods |
| `PrototypeChainTests.cs` | 40 comprehensive tests |

## Next Steps

With robust prototype handling in place, the next steps will implement:
- **Step 5.3**: JSFunction for callable objects
- **Step 5.4**: Array with optimized indexed storage
