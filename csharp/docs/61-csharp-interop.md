# C# Interop Guide

This guide covers how to interoperate between C# and JavaScript in QuickJS.NET.

## Table of Contents

- [Overview](#overview)
- [Exposing C# Functions to JavaScript](#exposing-c-functions-to-javascript)
- [Exposing C# Objects to JavaScript](#exposing-c-objects-to-javascript)
- [Calling JavaScript from C#](#calling-javascript-from-c)
- [Type Conversions](#type-conversions)
- [Working with Arrays and Collections](#working-with-arrays-and-collections)
- [Error Handling Across the Boundary](#error-handling-across-the-boundary)
- [Advanced Patterns](#advanced-patterns)
- [Complete Examples](#complete-examples)

## Overview

QuickJS.NET provides seamless interoperability between C# and JavaScript:

- **C# → JavaScript**: Expose C# functions and objects to JavaScript code
- **JavaScript → C#**: Call JavaScript functions and access objects from C#
- **Type Conversion**: Automatic conversion between C# and JavaScript types

## Exposing C# Functions to JavaScript

### Simple Function Registration

Use `RegisterGlobalFunction` to expose a C# method to JavaScript:

```csharp
using QuickJS;

var runtime = new JSRuntime();
var context = runtime.CreateContext();

// Register a simple function
context.RegisterGlobalFunction("greet", (thisVal, args) =>
{
    var name = args.Length > 0 ? args[0].ToString() : "World";
    return JSValue.FromString($"Hello, {name}!");
}, length: 1);

// Call from JavaScript
var result = context.Evaluate("greet('QuickJS')");
Console.WriteLine(result); // "Hello, QuickJS!"
```

### Function with Multiple Parameters

```csharp
context.RegisterGlobalFunction("add", (thisVal, args) =>
{
    var a = args.Length > 0 ? args[0].ToDouble() : 0;
    var b = args.Length > 1 ? args[1].ToDouble() : 0;
    return JSValue.FromDouble(a + b);
}, length: 2);

var sum = context.Evaluate("add(3, 4)");
Console.WriteLine(sum.ToDouble()); // 7
```

### Using Magic Values for Overloads

The `JSCFunctionMagic` delegate allows you to use a "magic" integer to implement multiple related functions with a single handler:

```csharp
// Single handler for min/max operations
JSValue MinMaxHandler(JSValue thisVal, JSValue[] args, int magic)
{
    var a = args[0].ToDouble();
    var b = args[1].ToDouble();
    
    return magic == 0 
        ? JSValue.FromDouble(Math.Min(a, b))  // magic=0 -> min
        : JSValue.FromDouble(Math.Max(a, b)); // magic=1 -> max
}

context.RegisterGlobalFunction("myMin", MinMaxHandler, 2, magic: 0);
context.RegisterGlobalFunction("myMax", MinMaxHandler, 2, magic: 1);
```

### Creating Functions Manually

For more control, create `JSFunction` objects directly:

```csharp
var functionProto = context.GetClassPrototype(JSClassId.CFunction);

var multiply = new JSFunction(
    (thisVal, args) =>
    {
        var x = args[0].ToDouble();
        var y = args[1].ToDouble();
        return JSValue.FromDouble(x * y);
    },
    name: "multiply",
    length: 2,
    prototype: functionProto
);

context.SetGlobalProperty("multiply", JSValue.FromObject(multiply));
```

## Exposing C# Objects to JavaScript

### Creating JavaScript Objects

Create custom JavaScript objects with C# methods:

```csharp
// Create a custom counter object
var objectProto = context.GetClassPrototype(JSClassId.Object);
var functionProto = context.GetClassPrototype(JSClassId.CFunction);

var counter = new JSObject(objectProto, JSClassId.Object);
int count = 0;

// Add properties
counter.Set("value", JSValue.FromInt32(count));

// Add methods
counter.Set("increment", JSValue.FromObject(new JSFunction(
    (thisVal, args) =>
    {
        count++;
        thisVal.AsObject().Set("value", JSValue.FromInt32(count));
        return JSValue.FromInt32(count);
    },
    "increment", 0, functionProto)));

counter.Set("reset", JSValue.FromObject(new JSFunction(
    (thisVal, args) =>
    {
        count = 0;
        thisVal.AsObject().Set("value", JSValue.FromInt32(count));
        return JSValue.Undefined;
    },
    "reset", 0, functionProto)));

// Expose to JavaScript
context.SetGlobalProperty("counter", JSValue.FromObject(counter));

// Use from JavaScript
context.Evaluate("counter.increment()");
context.Evaluate("counter.increment()");
var value = context.Evaluate("counter.value");
Console.WriteLine(value.ToInt32()); // 2
```

### Creating Constructor Functions

Implement JavaScript constructors in C#:

```csharp
var objectProto = context.GetClassPrototype(JSClassId.Object);
var functionProto = context.GetClassPrototype(JSClassId.CFunction);

// Create prototype for Point instances
var pointProto = new JSObject(objectProto, JSClassId.Object);

// Add prototype methods
pointProto.Set("distanceTo", JSValue.FromObject(new JSFunction(
    (thisVal, args) =>
    {
        var thisObj = thisVal.AsObject();
        var other = args[0].AsObject();
        
        var x1 = thisObj.Get("x").ToDouble();
        var y1 = thisObj.Get("y").ToDouble();
        var x2 = other.Get("x").ToDouble();
        var y2 = other.Get("y").ToDouble();
        
        return JSValue.FromDouble(Math.Sqrt(
            Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)));
    },
    "distanceTo", 1, functionProto)));

// Create constructor function
var pointCtor = new JSFunction(
    (thisVal, args) =>
    {
        var x = args.Length > 0 ? args[0].ToDouble() : 0;
        var y = args.Length > 1 ? args[1].ToDouble() : 0;
        
        var point = new JSObject(pointProto, JSClassId.Object);
        point.Set("x", JSValue.FromDouble(x));
        point.Set("y", JSValue.FromDouble(y));
        
        return JSValue.FromObject(point);
    },
    "Point", 2, functionProto);

// Set prototype property on constructor
pointCtor.Set("prototype", JSValue.FromObject(pointProto));

context.SetGlobalProperty("Point", JSValue.FromObject(pointCtor));

// Use from JavaScript
var result = context.Evaluate(@"
    var p1 = Point(0, 0);
    var p2 = Point(3, 4);
    p1.distanceTo(p2);
");
Console.WriteLine(result.ToDouble()); // 5
```

### Extending JSObject

For complex objects, extend `JSObject`:

```csharp
public class JSCalculator : JSObject
{
    private double _memory = 0;

    public JSCalculator(JSObject? prototype, JSObject? functionProto)
        : base(prototype, JSClassId.Object)
    {
        SetupMethods(functionProto);
    }

    private void SetupMethods(JSObject? functionProto)
    {
        Set("add", JSValue.FromObject(new JSFunction(Add, "add", 2, functionProto)));
        Set("subtract", JSValue.FromObject(new JSFunction(Subtract, "subtract", 2, functionProto)));
        Set("store", JSValue.FromObject(new JSFunction(Store, "store", 1, functionProto)));
        Set("recall", JSValue.FromObject(new JSFunction(Recall, "recall", 0, functionProto)));
    }

    private JSValue Add(JSValue thisVal, JSValue[] args)
    {
        var a = args[0].ToDouble();
        var b = args[1].ToDouble();
        return JSValue.FromDouble(a + b);
    }

    private JSValue Subtract(JSValue thisVal, JSValue[] args)
    {
        var a = args[0].ToDouble();
        var b = args[1].ToDouble();
        return JSValue.FromDouble(a - b);
    }

    private JSValue Store(JSValue thisVal, JSValue[] args)
    {
        _memory = args[0].ToDouble();
        return JSValue.Undefined;
    }

    private JSValue Recall(JSValue thisVal, JSValue[] args)
    {
        return JSValue.FromDouble(_memory);
    }
}

// Usage:
var calc = new JSCalculator(
    context.GetClassPrototype(JSClassId.Object),
    context.GetClassPrototype(JSClassId.CFunction));
context.SetGlobalProperty("calc", JSValue.FromObject(calc));
```

## Calling JavaScript from C#

### Evaluating Code

```csharp
// Simple evaluation
var result = context.Evaluate("2 + 2");
Console.WriteLine(result.ToInt32()); // 4

// Multi-line scripts
var script = @"
    function factorial(n) {
        return n <= 1 ? 1 : n * factorial(n - 1);
    }
    factorial(5);
";
var factResult = context.Evaluate(script);
Console.WriteLine(factResult.ToInt32()); // 120
```

### Calling JavaScript Functions

```csharp
// Define a function in JavaScript
context.Evaluate(@"
    function process(data) {
        return data.map(x => x * 2);
    }
");

// Get the function
var processFn = (JSFunction)context.GetGlobalProperty("process").AsObject();

// Create array argument
var arrayProto = context.GetClassPrototype(JSClassId.Array);
var arr = new JSObject(arrayProto, JSClassId.Array);
arr.Set(0u, JSValue.FromInt32(1));
arr.Set(1u, JSValue.FromInt32(2));
arr.Set(2u, JSValue.FromInt32(3));

// Call the function
var result = processFn.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(arr) });

// Read results
var resultArr = result.AsObject();
Console.WriteLine(resultArr.Get("0").ToInt32()); // 2
Console.WriteLine(resultArr.Get("1").ToInt32()); // 4
Console.WriteLine(resultArr.Get("2").ToInt32()); // 6
```

### Working with Promises

```csharp
// Get Promise constructor
var promiseCtor = (JSFunction)context.GetGlobalProperty("Promise").AsObject();

// Create an executor function
JSValue Executor(JSValue thisVal, JSValue[] args)
{
    var resolve = (JSFunction)args[0].AsObject();
    
    // Simulate async operation
    Task.Run(async () =>
    {
        await Task.Delay(100);
        resolve.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });
    });
    
    return JSValue.Undefined;
}

var functionProto = context.GetClassPrototype(JSClassId.CFunction);
var executorFn = new JSFunction(Executor, "executor", 2, functionProto);

// Create the promise
var promise = promiseCtor.CallNative(
    JSValue.Undefined, 
    new[] { JSValue.FromObject(executorFn) }
);

// Attach then handler
var thenFn = (JSFunction)promise.AsObject().Prototype!.Get("then").AsObject();
JSValue result = JSValue.Undefined;

var callback = new JSFunction(
    (_, a) => { result = a[0]; return JSValue.Undefined; },
    "callback", 1, functionProto);

thenFn.CallNative(promise, new[] { JSValue.FromObject(callback) });

// Run microtasks to process promise
context.RunMicrotasks();
```

## Type Conversions

### C# to JavaScript

| C# Type | JavaScript Type | Conversion Method |
|---------|----------------|-------------------|
| `bool` | Boolean | `JSValue.FromBoolean(value)` or implicit |
| `int` | Number (integer) | `JSValue.FromInt32(value)` or implicit |
| `double` | Number | `JSValue.FromDouble(value)` or implicit |
| `string` | String | `JSValue.FromString(value)` or implicit |
| `long` | BigInt | `JSValue.FromBigInt(value)` |
| `JSBigInt` | BigInt | `JSValue.FromBigInt(value)` |
| `JSObject` | Object | `JSValue.FromObject(obj)` |
| `JSSymbol` | Symbol | `JSValue.FromSymbol(symbol)` |
| `null` | null | `JSValue.Null` |
| - | undefined | `JSValue.Undefined` |

Implicit conversions are supported:

```csharp
JSValue v1 = 42;           // FromInt32
JSValue v2 = 3.14;         // FromDouble
JSValue v3 = "hello";      // FromString
JSValue v4 = true;         // FromBoolean
```

### JavaScript to C#

| JavaScript Type | C# Conversion | Method |
|----------------|---------------|--------|
| Boolean | `bool` | `value.ToBoolean()` |
| Number | `int` | `value.ToInt32()` |
| Number | `double` | `value.ToDouble()` |
| String | `string` | `value.ToString()` |
| BigInt | `long` | `value.ToInt64()` |
| BigInt | `JSBigInt` | `value.AsBigInt()` |
| Object | `JSObject` | `value.AsObject()` |
| Symbol | `JSSymbol` | `value.AsSymbol()` |

Type checking:

```csharp
if (value.IsNumber)
{
    double d = value.ToDouble();
}
else if (value.IsString)
{
    string s = value.ToString();
}
else if (value.IsObject)
{
    var obj = value.AsObject();
}
```

## Working with Arrays and Collections

### Creating JavaScript Arrays

```csharp
var arrayProto = context.GetClassPrototype(JSClassId.Array);
var arr = new JSObject(arrayProto, JSClassId.Array);

// Add elements using uint indices
arr.Set(0u, JSValue.FromInt32(10));
arr.Set(1u, JSValue.FromInt32(20));
arr.Set(2u, JSValue.FromInt32(30));

context.SetGlobalProperty("myArray", JSValue.FromObject(arr));
var length = context.Evaluate("myArray.length");
Console.WriteLine(length.ToInt32()); // 3
```

### Converting C# Collections

```csharp
// Helper to create JS array from C# enumerable
JSObject CreateArray(JSContext context, IEnumerable<int> items)
{
    var arrayProto = context.GetClassPrototype(JSClassId.Array);
    var arr = new JSObject(arrayProto, JSClassId.Array);
    uint i = 0;
    foreach (var item in items)
    {
        arr.Set(i++, JSValue.FromInt32(item));
    }
    return arr;
}

var numbers = new[] { 1, 2, 3, 4, 5 };
var jsArray = CreateArray(context, numbers);
context.SetGlobalProperty("numbers", JSValue.FromObject(jsArray));

var sum = context.Evaluate("numbers.reduce((a, b) => a + b, 0)");
Console.WriteLine(sum.ToInt32()); // 15
```

### Reading JavaScript Arrays

```csharp
context.Evaluate("var items = [10, 20, 30]");
var jsArray = context.GetGlobalProperty("items").AsObject();

var length = (int)jsArray.Get("length").ToDouble();
var csharpList = new List<int>(length);

for (uint i = 0; i < length; i++)
{
    csharpList.Add(jsArray.Get(i).ToInt32());
}
```

## Error Handling Across the Boundary

### Throwing JavaScript Errors from C#

```csharp
context.RegisterGlobalFunction("validateAge", (thisVal, args) =>
{
    var age = args[0].ToInt32();
    if (age < 0)
    {
        // Throw JavaScript RangeError
        return context.ThrowRangeError("Age cannot be negative");
    }
    if (age > 150)
    {
        return context.ThrowRangeError("Age seems unrealistic");
    }
    return JSValue.True;
}, length: 1);
```

### Catching JavaScript Exceptions in C#

```csharp
try
{
    context.Evaluate("throw new Error('Something went wrong')");
}
catch (JSException ex)
{
    Console.WriteLine($"JavaScript Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.JSStackTrace}");
}
```

### Returning Error Values

Check for exception values when calling functions:

```csharp
var result = context.Evaluate("someRiskyOperation()");
if (result.IsException)
{
    var error = context.GetCurrentException();
    Console.WriteLine($"Error occurred: {error}");
}
```

## Advanced Patterns

### Property Descriptors

Define properties with getters and setters:

```csharp
var obj = new JSObject(context.GetClassPrototype(JSClassId.Object));

// Define a property with getter/setter
int _value = 0;

var getter = new JSFunction(
    (thisVal, args) => JSValue.FromInt32(_value * 2), // Returns doubled value
    "get value", 0, context.GetClassPrototype(JSClassId.CFunction));

var setter = new JSFunction(
    (thisVal, args) => { _value = args[0].ToInt32(); return JSValue.Undefined; },
    "set value", 1, context.GetClassPrototype(JSClassId.CFunction));

obj.DefineProperty("value", new PropertyDescriptor(getter, setter, PropertyFlags.Default));

context.SetGlobalProperty("obj", JSValue.FromObject(obj));

context.Evaluate("obj.value = 5");
var result = context.Evaluate("obj.value");
Console.WriteLine(result.ToInt32()); // 10 (5 * 2)
```

### Symbol Properties

```csharp
// Create a unique symbol
var mySymbol = JSSymbol.Create("mySecret");

var obj = new JSObject(context.GetClassPrototype(JSClassId.Object));
obj.DefineProperty(
    mySymbol.Description!,
    new PropertyDescriptor(JSValue.FromString("hidden"), PropertyFlags.Default));

context.SetGlobalProperty("myObj", JSValue.FromObject(obj));
```

### Console Output Handling

Capture JavaScript console output in C#:

```csharp
var context = runtime.CreateContext();

context.ConsoleOutput += (sender, e) =>
{
    Console.WriteLine($"[{e.Level}] {e.Message}");
};

context.Evaluate("console.log('Hello from JS!')");
// Output: [Log] Hello from JS!

context.Evaluate("console.error('Oops!')");
// Output: [Error] Oops!
```

## Complete Examples

### Example 1: Database API

```csharp
public class DatabaseApi
{
    private readonly Dictionary<string, JSValue> _store = new();
    private readonly JSContext _context;

    public DatabaseApi(JSContext context)
    {
        _context = context;
        SetupGlobals();
    }

    private void SetupGlobals()
    {
        var objectProto = _context.GetClassPrototype(JSClassId.Object);
        var functionProto = _context.GetClassPrototype(JSClassId.CFunction);

        var db = new JSObject(objectProto, JSClassId.Object);
        
        db.Set("get", JSValue.FromObject(new JSFunction(
            (_, args) =>
            {
                var key = args[0].ToString();
                return _store.TryGetValue(key, out var val) ? val : JSValue.Undefined;
            },
            "get", 1, functionProto)));

        db.Set("set", JSValue.FromObject(new JSFunction(
            (_, args) =>
            {
                var key = args[0].ToString();
                var value = args[1];
                _store[key] = value;
                return JSValue.True;
            },
            "set", 2, functionProto)));

        db.Set("delete", JSValue.FromObject(new JSFunction(
            (_, args) =>
            {
                var key = args[0].ToString();
                return JSValue.FromBoolean(_store.Remove(key));
            },
            "delete", 1, functionProto)));

        db.Set("keys", JSValue.FromObject(new JSFunction(
            (_, args) =>
            {
                var arrayProto = _context.GetClassPrototype(JSClassId.Array);
                var arr = new JSObject(arrayProto, JSClassId.Array);
                uint i = 0;
                foreach (var key in _store.Keys)
                {
                    arr.Set(i++, JSValue.FromString(key));
                }
                return JSValue.FromObject(arr);
            },
            "keys", 0, functionProto)));

        _context.SetGlobalProperty("db", JSValue.FromObject(db));
    }
}

// Usage:
var dbApi = new DatabaseApi(context);
context.Evaluate(@"
    db.set('user:1', { name: 'Alice', age: 30 });
    db.set('user:2', { name: 'Bob', age: 25 });
    console.log(db.keys()); // ['user:1', 'user:2']
    console.log(db.get('user:1').name); // 'Alice'
");
```

### Example 2: Event Emitter

```csharp
public class JSEventEmitter : JSObject
{
    private readonly Dictionary<string, List<JSFunction>> _handlers = new();

    public JSEventEmitter(JSObject? prototype, JSObject? functionProto)
        : base(prototype, JSClassId.Object)
    {
        Set("on", JSValue.FromObject(new JSFunction(On, "on", 2, functionProto)));
        Set("emit", JSValue.FromObject(new JSFunction(Emit, "emit", 1, functionProto)));
        Set("off", JSValue.FromObject(new JSFunction(Off, "off", 2, functionProto)));
    }

    private JSValue On(JSValue thisVal, JSValue[] args)
    {
        var eventName = args[0].ToString();
        var handler = (JSFunction)args[1].AsObject();

        if (!_handlers.ContainsKey(eventName))
            _handlers[eventName] = new List<JSFunction>();

        _handlers[eventName].Add(handler);
        return thisVal; // Allow chaining
    }

    private JSValue Emit(JSValue thisVal, JSValue[] args)
    {
        var eventName = args[0].ToString();
        
        if (_handlers.TryGetValue(eventName, out var handlers))
        {
            var eventArgs = new JSValue[args.Length - 1];
            Array.Copy(args, 1, eventArgs, 0, eventArgs.Length);

            foreach (var handler in handlers)
            {
                handler.CallNative(thisVal, eventArgs);
            }
        }
        return thisVal;
    }

    private JSValue Off(JSValue thisVal, JSValue[] args)
    {
        var eventName = args[0].ToString();
        
        if (args.Length == 1)
        {
            _handlers.Remove(eventName);
        }
        else
        {
            var handler = (JSFunction)args[1].AsObject();
            _handlers[eventName]?.Remove(handler);
        }
        return thisVal;
    }
}

// Usage:
var emitter = new JSEventEmitter(
    context.GetClassPrototype(JSClassId.Object),
    context.GetClassPrototype(JSClassId.CFunction));
context.SetGlobalProperty("events", JSValue.FromObject(emitter));

context.Evaluate(@"
    events.on('click', function(x, y) {
        console.log('Clicked at', x, y);
    });
    
    events.emit('click', 100, 200); // Clicked at 100 200
");
```

## Best Practices

1. **Validate Arguments**: Always check argument count and types in native functions
2. **Handle Exceptions**: Wrap native code in try-catch and return appropriate JS errors
3. **Use Appropriate Types**: Match C# types to JavaScript expectations
4. **Memory Management**: Be careful with object references across the boundary
5. **Thread Safety**: Don't share JSContext across threads
6. **Run Microtasks**: Call `context.RunMicrotasks()` when working with Promises

## See Also

- [Public API Reference](60-public-api.md)
- [Runtime Architecture](25-runtime-architecture.md)
- [Exception Handling](05-exception-hierarchy.md)
