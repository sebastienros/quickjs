# Step 1.5: Exception Hierarchy

## Overview

This step implements the **JavaScript exception hierarchy** - a set of C# exception classes that correspond to JavaScript's native error types. These exceptions are thrown when JavaScript code encounters parsing or runtime errors.

## JavaScript Error Types

JavaScript defines several built-in error types, all inheriting from `Error`:

| Type | Purpose | Example |
|------|---------|---------|
| `Error` | Generic error | `throw new Error("Something went wrong")` |
| `SyntaxError` | Parsing error | `eval("function(")` |
| `TypeError` | Type mismatch | `null.toString()` |
| `ReferenceError` | Invalid reference | `undeclaredVar` |
| `RangeError` | Value out of range | `new Array(-1)` |
| `URIError` | URI encoding error | `decodeURI("%")` |
| `EvalError` | eval() error (legacy) | Rarely used |
| `InternalError` | Engine internal error | Too much recursion |
| `AggregateError` | Multiple errors (ES2021) | `Promise.any([])` |

## C# Implementation

### Class Hierarchy

```
System.Exception
└── JSException (base for all JS errors)
    ├── JSSyntaxError
    ├── JSTypeError
    ├── JSReferenceError
    ├── JSRangeError
    ├── JSURIError
    ├── JSEvalError
    ├── JSInternalError
    └── JSAggregateError
```

### Supporting Types

#### JSErrorType Enum

```csharp
public enum JSErrorType
{
    Error = 0,
    EvalError = 1,
    RangeError = 2,
    ReferenceError = 3,
    SyntaxError = 4,
    TypeError = 5,
    URIError = 6,
    InternalError = 7,
    AggregateError = 8,
}
```

#### SourceLocation Struct

Tracks where in the source code an error occurred:

```csharp
public readonly struct SourceLocation : IEquatable<SourceLocation>
{
    public string FileName { get; }   // e.g., "app.js"
    public int Line { get; }          // 1-based line number
    public int Column { get; }        // 1-based column number
    
    public bool IsEmpty => ...;
    public static SourceLocation Empty => default;
}

// Usage:
var location = new SourceLocation("script.js", 42, 15);
Console.WriteLine(location); // "script.js:42:15"
```

#### JSStackFrame and JSStackTrace

Represent JavaScript stack traces for debugging:

```csharp
public sealed class JSStackFrame
{
    public string? FunctionName { get; }
    public SourceLocation Location { get; }
    public bool IsEval { get; }
    public bool IsNative { get; }
}

public sealed class JSStackTrace
{
    public IReadOnlyList<JSStackFrame> Frames { get; }
    public int Count { get; }
}

// Output format:
//     at myFunction (test.js:10:5)
//     at <anonymous> (test.js:20:1)
//     at Array.prototype.map [native code]
```

### JSException Base Class

```csharp
public class JSException : Exception
{
    public JSErrorType ErrorType { get; }
    public SourceLocation Location { get; }
    public JSStackTrace? JSStackTrace { get; }
    
    public string ErrorName { get; }        // "TypeError", "SyntaxError", etc.
    public string FormattedMessage { get; } // "TypeError: message"
    
    // Multiple constructor overloads for different use cases
}
```

### Derived Exception Classes

Each JavaScript error type has a corresponding C# class:

```csharp
// Syntax errors (parsing)
throw new JSSyntaxError("Unexpected token ')'", 
    new SourceLocation("app.js", 10, 5));

// Type errors (runtime)
throw new JSTypeError("undefined is not a function");

// Reference errors (variable lookup)
throw new JSReferenceError("x is not defined");

// Range errors (numeric bounds)
throw new JSRangeError("Invalid array length");

// URI errors
throw new JSURIError("malformed URI sequence");

// Aggregate errors (multiple failures)
throw new JSAggregateError("All promises rejected", error1, error2);
```

## Design Decisions

### Why C# Exceptions?

We use C# exceptions rather than return values because:

1. **Natural error propagation**: Exceptions automatically unwind the call stack
2. **Standard .NET pattern**: Integrates with try/catch/finally
3. **Rich debugging**: Stack traces, inner exceptions, etc.
4. **Interoperability**: .NET code can catch JavaScript errors naturally

### Why a Single Base Class?

Using `JSException` as a base class allows:

```csharp
// Catch any JavaScript error
try { engine.Evaluate(code); }
catch (JSException ex) {
    Console.WriteLine($"{ex.ErrorName}: {ex.Message}");
}

// Catch specific types
try { engine.Evaluate(code); }
catch (JSSyntaxError ex) {
    Console.WriteLine($"Syntax error at {ex.Location}");
}
catch (JSTypeError ex) {
    Console.WriteLine($"Type error: {ex.Message}");
}
```

### ErrorType vs Derived Classes

We provide both:
- **Derived classes** for type-safe catching (`catch (JSTypeError)`)
- **ErrorType property** for runtime type checking and switch statements

```csharp
switch (ex.ErrorType)
{
    case JSErrorType.TypeError:
    case JSErrorType.ReferenceError:
        // Handle as recoverable
        break;
    case JSErrorType.SyntaxError:
        // Handle as compile-time error
        break;
}
```

## File Structure

```
src/QuickJS.Core/
├── JSErrorType.cs     # Error type enum
├── SourceLocation.cs  # Source file location
├── JSStackTrace.cs    # Stack frame and trace
└── JSException.cs     # Exception hierarchy
```

## Usage Examples

### Parser Error

```csharp
// In the lexer/parser
if (currentChar == '\0' && insideString)
{
    throw new JSSyntaxError(
        "Unterminated string literal",
        new SourceLocation(fileName, line, column)
    );
}
```

### Runtime Error

```csharp
// In the interpreter
if (callee.Type != JSValueType.Object || !IsCallable(callee))
{
    throw new JSTypeError($"{callee} is not a function");
}
```

### With Stack Trace

```csharp
// Building a full error with stack
var trace = new JSStackTrace(stackFrames);
throw new JSException(
    "Cannot read property 'x' of undefined",
    JSErrorType.TypeError,
    currentLocation,
    trace
);
```

### Catching in Host Code

```csharp
try
{
    var result = jsContext.Evaluate("badCode(");
}
catch (JSSyntaxError ex)
{
    logger.Error($"Syntax error in script: {ex.FormattedMessage}");
    logger.Error($"  at {ex.Location}");
}
catch (JSException ex)
{
    logger.Error($"JavaScript error: {ex.FormattedMessage}");
    if (ex.JSStackTrace != null)
    {
        logger.Error(ex.JSStackTrace.ToString());
    }
}
```

## Comparison with QuickJS C

In C, QuickJS uses return values and a pending exception model:

```c
// C style - return special value, check for pending exception
JSValue result = JS_Call(ctx, func, this_val, argc, argv);
if (JS_IsException(result)) {
    JSValue exception = JS_GetException(ctx);
    // Handle exception...
    JS_FreeValue(ctx, exception);
}
```

In C#, we use exceptions directly:

```csharp
// C# style - exceptions propagate naturally
try
{
    JSValue result = context.Call(func, thisVal, args);
    // Use result...
}
catch (JSException ex)
{
    // Handle exception...
}
```

## Testing

The test suite covers:

1. **SourceLocation**: Construction, formatting, equality
2. **JSStackFrame/JSStackTrace**: Formatting, empty cases
3. **JSException base**: All constructors, properties, formatting
4. **Derived exceptions**: Correct error types, inheritance
5. **Realistic scenarios**: Parser errors, runtime errors, Promise.any rejections

## Next Steps

With the exception hierarchy in place, we can:

1. **Phase 2**: Implement the lexer/tokenizer (throws JSSyntaxError)
2. **Phase 3**: Implement the parser (throws JSSyntaxError)
3. **Phase 4**: Implement the interpreter (throws various runtime errors)
