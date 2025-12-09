# Step 5.1: JSObject Implementation

This document explains the implementation of JavaScript objects in our QuickJS C# port.

## Overview

In JavaScript, objects are the fundamental building blocks for data structures. An object is a collection of properties, where each property has a name (key) and a value. Objects also participate in a prototype chain for inheritance.

Our implementation follows the ECMAScript specification and mirrors the QuickJS C implementation while adapting it to idiomatic C#.

## Key Components

### 1. PropertyFlags

```csharp
[Flags]
public enum PropertyFlags
{
    None = 0,
    Configurable = 1 << 0,  // Can delete/redefine
    Writable = 1 << 1,      // Can change value (data props only)
    Enumerable = 1 << 2,    // Appears in for-in loops
    Default = Configurable | Writable | Enumerable,
}
```

These flags correspond to:
- **Configurable**: Whether the property can be deleted or have its attributes changed
- **Writable**: Whether the property's value can be changed (data properties only)
- **Enumerable**: Whether the property appears in `for-in` loops and `Object.keys()`

### 2. PropertyDescriptor

A property descriptor defines the attributes of a property. There are two kinds:

**Data Descriptors** have:
- `Value`: The property value
- `Writable`: Whether the value can be changed
- `Enumerable`: Whether the property is enumerable
- `Configurable`: Whether the property is configurable

**Accessor Descriptors** have:
- `Getter`: A function that returns the property value
- `Setter`: A function that sets the property value
- `Enumerable`: Whether the property is enumerable
- `Configurable`: Whether the property is configurable

```csharp
// Data descriptor
var dataProp = PropertyDescriptor.Data(JSValue.FromInt32(42));

// Read-only data descriptor
var readOnly = PropertyDescriptor.ReadOnly(JSValue.FromString("constant"));

// Accessor descriptor
var accessor = PropertyDescriptor.Accessor(getterFunc, setterFunc);
```

### 3. JSClassId

Every object has a class ID that identifies its type. This is used for:
- Dispatch of type-specific behavior (e.g., Array length)
- Prototype selection during object creation
- Instanceof checks
- Optimized fast paths for known types

```csharp
public enum JSClassId
{
    None = 0,
    Object = 1,      // Plain object
    Array = 2,       // Array with special length behavior
    Error = 3,       // Error objects
    Number = 4,      // Boxed Number
    String = 5,      // Boxed String
    Boolean = 6,     // Boxed Boolean
    // ... many more built-in types
    FirstUserClass = 100,  // User-defined classes start here
}
```

### 4. JSObject

The main object class with these key features:

```csharp
public class JSObject
{
    // Class ID for type identification
    public JSClassId ClassId { get; }
    
    // Prototype for inheritance chain
    public JSObject? Prototype { get; set; }
    
    // Whether new properties can be added
    public bool IsExtensible { get; }
    
    // Property access
    public JSValue Get(string name);
    public bool Set(string name, JSValue value);
    public bool Delete(string name);
    
    // Property definition
    public bool DefineProperty(string name, PropertyDescriptor descriptor);
    public PropertyDescriptor? GetOwnPropertyDescriptor(string name);
    
    // Extensibility control
    public bool PreventExtensions();
    public bool Seal();
    public bool Freeze();
}
```

## Property Storage

### Named Properties

Named properties are stored in a `Dictionary<string, PropertyDescriptor>`. This provides O(1) average lookup time.

```csharp
private readonly Dictionary<string, PropertyDescriptor> _properties;
```

### Indexed Properties

Array-like objects need indexed property storage. These are stored separately:

```csharp
private Dictionary<uint, PropertyDescriptor>? _indexedProperties;
```

The indexed properties dictionary is created lazily - only when the first indexed property is set.

## Prototype Chain

JavaScript uses prototypal inheritance. When looking up a property:

1. Check if the object has the property as an own property
2. If not found, check the prototype
3. Continue up the prototype chain until `null` is reached

```csharp
public JSValue Get(string propertyName)
{
    // Check own properties first
    if (_properties.TryGetValue(propertyName, out var descriptor))
        return GetValueFromDescriptor(descriptor);

    // Walk the prototype chain
    var proto = _prototype;
    while (proto != null)
    {
        if (proto._properties.TryGetValue(propertyName, out descriptor))
            return proto.GetValueFromDescriptor(descriptor);
        proto = proto._prototype;
    }

    return JSValue.Undefined;
}
```

## Extensibility Control

JavaScript provides three levels of object locking:

### PreventExtensions

Prevents adding new properties, but existing properties can still be modified or deleted.

```csharp
var obj = new JSObject();
obj.Set("x", JSValue.FromInt32(1));
obj.PreventExtensions();

obj.Set("y", JSValue.FromInt32(2));  // Returns false - blocked
obj.Set("x", JSValue.FromInt32(3));  // Works - modifying existing
obj.Delete("x");                      // Works - deleting existing
```

### Seal

Prevents adding new properties AND makes all existing properties non-configurable. Values can still be changed.

```csharp
obj.Seal();
// obj.Delete("x") would now fail
// obj.Set("x", value) still works if x was writable
```

### Freeze

Prevents adding new properties, makes all properties non-configurable AND non-writable. Object is completely immutable.

```csharp
obj.Freeze();
// Object is now completely immutable
```

## Comparison with QuickJS C

### QuickJS C Implementation

QuickJS uses a "shape" system (hidden classes) for optimized property access:

```c
struct JSObject {
    uint8_t extensible : 1;
    uint16_t class_id;
    JSShape *shape;      // Metadata: prototype + property names/flags
    JSProperty *prop;    // Array of property values
};

struct JSShape {
    JSObject *proto;
    JSShapeProperty prop[0];  // Property metadata array
};
```

The shape contains:
- Prototype reference
- Property names and flags
- Hash table for fast lookup

Multiple objects with the same structure share the same shape.

### Our C# Implementation

For clarity, we use a simpler dictionary-based approach:

```csharp
public class JSObject
{
    private readonly Dictionary<string, PropertyDescriptor> _properties;
    private JSObject? _prototype;
    private readonly JSClassId _classId;
    private bool _extensible;
}
```

This trades some performance for simplicity. A shape-based optimization could be added later if needed.

## Integration with JSValue

Objects are stored in JSValue using the Object tag:

```csharp
// Creating an object value
var obj = new JSObject();
var value = JSValue.FromObject(obj);

// Retrieving an object
if (value.TryGetObject(out var retrieved))
{
    // Use retrieved...
}

// Or throw if not an object
var obj2 = value.AsObject();
```

## Usage Examples

### Creating Objects

```csharp
// Plain object
var obj = new JSObject();

// Object with prototype
var proto = new JSObject();
proto.Set("inherited", JSValue.FromString("value"));
var child = new JSObject(proto);

// Object with class ID
var arr = new JSObject(null, JSClassId.Array);
```

### Property Operations

```csharp
// Simple properties
obj.Set("name", JSValue.FromString("Alice"));
obj.Set("age", JSValue.FromInt32(30));

// Get values
var name = obj.Get("name");  // "Alice"
var missing = obj.Get("x");  // undefined

// Check existence
obj.HasProperty("name");      // true
obj.HasOwnProperty("name");   // true

// Delete
obj.Delete("age");            // true
```

### Defining Properties with Specific Attributes

```csharp
// Non-writable property
obj.DefineProperty("PI", PropertyDescriptor.Data(
    JSValue.FromDouble(3.14159),
    writable: false,
    enumerable: true,
    configurable: false));

// Non-enumerable property
obj.DefineProperty("_internal", PropertyDescriptor.Data(
    JSValue.FromInt32(42),
    writable: true,
    enumerable: false,
    configurable: true));
```

## Test Coverage

The implementation includes comprehensive tests for:
- Basic property get/set/delete operations
- Indexed property operations
- Prototype chain lookup and shadowing
- HasProperty and HasOwnProperty
- DefineProperty with various attribute combinations
- Property enumeration
- Extensibility control (PreventExtensions, Seal, Freeze)
- Non-writable property protection
- JSValue integration

## Files Changed

| File | Description |
|------|-------------|
| `PropertyDescriptor.cs` | PropertyFlags enum and PropertyDescriptor class |
| `JSClassId.cs` | JSClassId enum with built-in class IDs |
| `JSObject.cs` | Main JSObject implementation |
| `JSValue.cs` | Added FromObject, TryGetObject, AsObject |
| `PropertyDescriptorTests.cs` | Tests for property descriptors |
| `JSClassIdTests.cs` | Tests for class ID extensions |
| `JSObjectTests.cs` | Comprehensive JSObject tests |
| `JSValueObjectTests.cs` | Tests for JSValue object methods |

## Next Steps

With the basic object model in place, the next steps will implement:
- **Step 5.2**: JSArray with optimized indexed property storage
- **Step 5.3**: JSFunction for callable objects
- **Step 5.4**: JSSymbol for unique property keys
- **Step 5.5**: Built-in object constructors (Object, Array, etc.)
