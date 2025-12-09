# Step 7.12: Symbol and Reflect

This step implements the JavaScript Symbol primitive type and the Reflect object, completing another piece of ES6+ functionality.

## Overview

**Symbol** is a primitive data type introduced in ES6, providing a way to create unique, immutable identifiers. Symbols are often used as property keys to avoid name collisions.

**Reflect** is a built-in object that provides methods for interceptable JavaScript operations. It mirrors many of the internal operations and is closely related to Proxy handlers.

## Symbol Implementation

### JSSymbol Class

The `JSSymbol` class represents JavaScript Symbol primitives:

```csharp
public sealed class JSSymbol : IEquatable<JSSymbol>
{
    // Unique identifier for this symbol instance
    private readonly Guid _id;
    
    // Optional description for debugging
    public string? Description { get; }
    
    // Symbols are unique - two symbols with same description are different
    public JSSymbol(string? description = null)
    {
        _id = Guid.NewGuid();
        Description = description;
    }
}
```

### Key Properties of Symbols

1. **Uniqueness**: Every symbol is unique, even with the same description
   ```javascript
   Symbol('foo') !== Symbol('foo')  // true
   ```

2. **Immutability**: Symbols cannot be modified after creation

3. **Non-enumerable**: Symbol-keyed properties don't appear in `for...in`

### Well-Known Symbols

ES6 defines several well-known symbols used by the language:

| Symbol | Description |
|--------|-------------|
| `Symbol.iterator` | Method returning the default iterator |
| `Symbol.asyncIterator` | Method returning the default async iterator |
| `Symbol.hasInstance` | Customize `instanceof` behavior |
| `Symbol.isConcatSpreadable` | Indicates if object should be flattened in `concat` |
| `Symbol.match` | Customize `String.prototype.match` |
| `Symbol.matchAll` | Customize `String.prototype.matchAll` |
| `Symbol.replace` | Customize `String.prototype.replace` |
| `Symbol.search` | Customize `String.prototype.search` |
| `Symbol.split` | Customize `String.prototype.split` |
| `Symbol.species` | Constructor function for derived objects |
| `Symbol.toPrimitive` | Customize type conversion |
| `Symbol.toStringTag` | Customize `Object.prototype.toString` |
| `Symbol.unscopables` | Properties excluded from `with` environment |

### Global Symbol Registry

The global symbol registry allows sharing symbols across realms:

```csharp
// Global registry maps keys to symbols
private static readonly Dictionary<string, JSSymbol> GlobalRegistry = new();

// Symbol.for(key) - returns existing or creates new
public static JSSymbol For(string key)
{
    if (!GlobalRegistry.TryGetValue(key, out var symbol))
    {
        symbol = new JSSymbol(key);
        GlobalRegistry[key] = symbol;
    }
    return symbol;
}

// Symbol.keyFor(symbol) - returns key if registered, undefined otherwise
public static string? KeyFor(JSSymbol symbol)
{
    foreach (var kvp in GlobalRegistry)
    {
        if (ReferenceEquals(kvp.Value, symbol))
            return kvp.Key;
    }
    return null;
}
```

## JSValue Symbol Support

Extended JSValue to support Symbol:

```csharp
// Create symbol value
public static JSValue FromSymbol(JSSymbol symbol)
{
    return new JSValue(JSValueType.Symbol, symbol);
}

// Check if symbol
public bool IsSymbol => _type == JSValueType.Symbol;

// Get symbol value
public JSSymbol? AsSymbol()
{
    if (_type == JSValueType.Symbol)
        return _objectValue as JSSymbol;
    return null;
}
```

## Reflect Object

The Reflect object provides methods that mirror JavaScript's internal operations:

### Reflect Methods

| Method | Description |
|--------|-------------|
| `Reflect.apply(fn, thisArg, args)` | Call a function with given this and args |
| `Reflect.construct(target, args)` | Like `new target(...args)` |
| `Reflect.defineProperty(target, key, desc)` | Like `Object.defineProperty` but returns boolean |
| `Reflect.deleteProperty(target, key)` | Like `delete target[key]` but returns boolean |
| `Reflect.get(target, key)` | Like `target[key]` |
| `Reflect.getOwnPropertyDescriptor(target, key)` | Like `Object.getOwnPropertyDescriptor` |
| `Reflect.getPrototypeOf(target)` | Like `Object.getPrototypeOf` |
| `Reflect.has(target, key)` | Like `key in target` |
| `Reflect.isExtensible(target)` | Like `Object.isExtensible` |
| `Reflect.ownKeys(target)` | Returns all property keys |
| `Reflect.preventExtensions(target)` | Like `Object.preventExtensions` |
| `Reflect.set(target, key, value)` | Like `target[key] = value` but returns boolean |
| `Reflect.setPrototypeOf(target, proto)` | Like `Object.setPrototypeOf` but returns boolean |

### Reflect vs Object Methods

Reflect methods differ from Object methods in important ways:

1. **Return values**: Reflect methods return boolean success/failure instead of throwing
2. **Consistent API**: All take target as first argument
3. **Proxy trap parity**: Each Reflect method corresponds to a Proxy trap

```csharp
// Object.defineProperty throws on failure
try { Object.defineProperty(frozen, 'x', {...}); }
catch { /* handle */ }

// Reflect.defineProperty returns false
if (!Reflect.defineProperty(frozen, 'x', {...})) {
    // handle gracefully
}
```

## Context Integration

### Symbol Constructor

```csharp
private void InitializeSymbol()
{
    // Symbol is callable but not constructable
    // Symbol('desc') creates symbol, new Symbol() throws
    
    // Static methods
    symbolCtor.Set("for", SymbolFor);      // Symbol.for(key)
    symbolCtor.Set("keyFor", SymbolKeyFor); // Symbol.keyFor(sym)
    
    // Well-known symbols as static properties
    symbolCtor.Set("iterator", JSValue.FromSymbol(JSSymbol.Iterator));
    symbolCtor.Set("toStringTag", JSValue.FromSymbol(JSSymbol.ToStringTag));
    // ... etc
}
```

### Reflect Object

```csharp
private void InitializeReflect()
{
    var reflect = new JSObject();
    
    reflect.Set("apply", ReflectApply);
    reflect.Set("construct", ReflectConstruct);
    reflect.Set("defineProperty", ReflectDefineProperty);
    // ... etc
    
    GlobalObject.Set("Reflect", JSValue.FromObject(reflect));
}
```

## Usage Examples

### Symbol Usage

```javascript
// Unique property keys
const ID = Symbol('id');
const obj = {
    [ID]: 123,
    name: 'test'
};

// Symbol.for for shared symbols
const s1 = Symbol.for('shared');
const s2 = Symbol.for('shared');
console.log(s1 === s2);  // true

// Well-known symbols
const iterable = {
    [Symbol.iterator]() {
        let i = 0;
        return {
            next() {
                return i < 3 
                    ? { value: i++, done: false }
                    : { done: true };
            }
        };
    }
};
```

### Reflect Usage

```javascript
// Safe property operations
const obj = Object.freeze({});
Reflect.defineProperty(obj, 'x', { value: 1 }); // false, no throw

// Getting prototype
const proto = Reflect.getPrototypeOf(obj);

// Property existence
Reflect.has(obj, 'toString'); // true (inherited)

// All own keys
Reflect.ownKeys({ a: 1, [Symbol.for('b')]: 2 }); // ['a', Symbol(b)]
```

## Test Coverage

76 new tests covering:

### Symbol Tests (39 tests)
- Symbol creation (with/without description)
- Symbol uniqueness
- Symbol.toString()
- Well-known symbols (13 tests)
- Global registry (Symbol.for, Symbol.keyFor)
- JSValue symbol support
- Context Symbol constructor

### Reflect Tests (37 tests)
- All 13 Reflect methods
- Method existence verification
- Correct behavior for each method
- Error handling for non-object arguments

## Files Changed

- `JSSymbol.cs` - New Symbol primitive class
- `JSValue.cs` - Added Symbol support
- `JSContext.cs` - Added InitializeSymbol() and InitializeReflect()
- `SymbolReflectTests.cs` - 76 comprehensive tests

## Key Design Decisions

1. **Symbol as Class**: While Symbol is a primitive in JS, we use a class with reference equality for uniqueness

2. **Global Registry**: Using static dictionary for Symbol.for/keyFor registry

3. **Well-Known as Singletons**: Each well-known symbol is a static singleton

4. **Reflect Return Values**: Consistently return boolean for success/failure operations

## Next Step

Step 7.13 will implement Proxy, the final built-in object in Phase 7, which uses many of the Reflect operations as its trap handlers.
