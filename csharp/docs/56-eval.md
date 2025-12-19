# Step 9.1: Eval and Function Constructor

This document describes the implementation of JavaScript's `eval()` function and the `Function` constructor, which allow runtime compilation and execution of JavaScript code from strings.

## Overview

JavaScript provides two primary mechanisms for dynamic code execution:

1. **`eval()`** - Evaluates a string as JavaScript code
2. **`Function` constructor** - Creates a new function from strings

These features are powerful but should be used carefully due to security implications when evaluating untrusted input.

## Implementation

### JSEval Class

The `JSEval` static class provides the core functionality:

```csharp
public static class JSEval
{
    // Compiles JavaScript source code
    public static JSFunctionDef? Compile(JSRuntime runtime, string source, 
        string fileName = "<eval>", bool isModule = false);
    
    // Compiles and executes JavaScript code
    public static JSValue Evaluate(JSContext context, string source,
        string fileName = "<eval>", EvalFlags flags = EvalFlags.Global);
    
    // Creates the global eval function
    public static JSFunction CreateEvalFunction(JSContext context);
    
    // Creates a function from string arguments
    public static JSValue CreateFunctionFromStrings(JSContext context, JSValue[] args);
}
```

### EvalFlags

Flags control how code is evaluated:

```csharp
[Flags]
public enum EvalFlags
{
    Global = 0,          // Global code (script mode)
    Module = 1 << 0,     // Module code (ES module mode)
    Direct = 1 << 1,     // Direct eval (has access to local scope)
    Indirect = 1 << 2,   // Indirect eval (global scope only)
    Strict = 1 << 3,     // Strict mode evaluation
    Strip = 1 << 4,      // Strip debug information
    CompileOnly = 1 << 5, // Compile only (don't execute)
    ForceStrict = 1 << 6, // Force strict mode
}
```

### JSContext Methods

Convenience methods on `JSContext`:

```csharp
// Evaluate JavaScript code
public JSValue Evaluate(string source, string fileName = "<eval>");

// Evaluate as module
public JSValue EvaluateModule(string source, string fileName = "<module>");

// Compile without executing
public JSValue Compile(string source, string fileName = "<script>");

// Execute previously compiled bytecode
public JSValue Execute(JSFunctionDef functionDef);
```

## Reusing Compiled Scripts (Performance)

`JSContext.Evaluate()` compiles the source string every time you call it (lexing + parsing + bytecode generation). For hot paths (benchmarks, tight loops, per-request code), compile once and execute the compiled result repeatedly:

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Compile once
var fn = JSEval.Compile(runtime, "1 + 2 * 3", "<benchmark>")
    ?? throw new InvalidOperationException("Compile failed");

// Execute many times (no per-call parse/compile)
for (int i = 0; i < 1_000_000; i++)
{
    var value = context.Execute(fn);
    if (value.IsException)
        throw new Exception(context.GetAndClearException().ToString());
}
```

### Reuse Rules

- Safe to reuse a compiled `JSFunctionDef` across multiple `JSContext` instances created from the same `JSRuntime`.
- Do not reuse a compiled `JSFunctionDef` across different `JSRuntime` instances: the compiled form depends on the runtime's atom table (interned strings), so atom IDs will not match.

## The eval() Function

### Behavior

Per the ECMAScript specification:

1. If called with no arguments, returns `undefined`
2. If the argument is not a string, returns the argument unchanged
3. If the argument is a string, parses and executes it as JavaScript code

```csharp
JSValue EvalImpl(JSValue thisArg, JSValue[] args)
{
    if (args.Length == 0)
        return JSValue.Undefined;

    var arg = args[0];
    
    // Non-string arguments are returned unchanged
    if (!arg.IsString)
        return arg;

    var source = arg.ToString();
    return Evaluate(context, source, "<eval>", EvalFlags.Indirect);
}
```

### Direct vs Indirect Eval

- **Direct eval**: Called directly as `eval(...)`, has access to the local scope
- **Indirect eval**: Called indirectly (e.g., `(0, eval)(...)` or `window.eval(...)`), runs in global scope

The current implementation uses indirect eval semantics.

## The Function Constructor

### Usage Patterns

```javascript
// No arguments - creates empty function
new Function();
// Creates: function anonymous() { }

// Body only - no parameters
new Function('return 42');
// Creates: function anonymous() { return 42; }

// With parameters
new Function('a', 'b', 'return a + b');
// Creates: function anonymous(a, b) { return a + b; }

// Comma-separated parameters in single string
new Function('a, b, c', 'return a + b + c');
// Creates: function anonymous(a, b, c) { return a + b + c; }
```

### Parameter Validation

The implementation validates parameter names:

1. Must be valid JavaScript identifiers
2. First character must be letter, underscore, or dollar sign
3. Subsequent characters can include digits
4. Cannot be reserved words

```csharp
private static bool IsValidIdentifier(string name)
{
    if (string.IsNullOrEmpty(name))
        return false;

    var first = name[0];
    if (!char.IsLetter(first) && first != '_' && first != '$')
        return false;

    for (int i = 1; i < name.Length; i++)
    {
        var c = name[i];
        if (!char.IsLetterOrDigit(c) && c != '_' && c != '$')
            return false;
    }

    return !IsReservedWord(name);
}
```

### Reserved Words

The following words cannot be used as parameter names:

- **Keywords**: `break`, `case`, `catch`, `continue`, `debugger`, `default`, `delete`, `do`, `else`, `finally`, `for`, `function`, `if`, `in`, `instanceof`, `new`, `return`, `switch`, `this`, `throw`, `try`, `typeof`, `var`, `void`, `while`, `with`
- **Future reserved**: `class`, `const`, `enum`, `export`, `extends`, `import`, `super`
- **Strict mode reserved**: `implements`, `interface`, `let`, `package`, `private`, `protected`, `public`, `static`, `yield`
- **Literals**: `null`, `true`, `false`

## Compilation Pipeline

The compilation process:

1. **Lexing**: Source code is tokenized by the `Lexer`
2. **Parsing**: The `Parser` converts tokens to bytecode in `JSFunctionDef`
3. **Execution**: The `Interpreter` executes the bytecode

```csharp
// Compile against the runtime (shared atom table)
public static JSFunctionDef? Compile(JSRuntime runtime, string source,
    string fileName = "<eval>", bool isModule = false, out DiagnosticBag diagnostics)
{
    var parser = new Parser(source, fileName, runtime.AtomTable, isModule);
    parser.ParseProgram();

    diagnostics = parser.Diagnostics;
    return diagnostics.HasErrors ? null : parser.CurrentFunction;
}
```

## Security Considerations

Using `eval()` and `Function` constructor with untrusted input poses security risks:

- **Code injection**: Malicious code can be executed
- **Scope access**: Direct eval can access local variables
- **Performance**: Dynamic code cannot be optimized ahead of time

### Alternatives

Consider these safer alternatives:

1. **JSON.parse()** for parsing JSON data
2. **Template literals** for string interpolation
3. **Function references** instead of eval'd function names
4. **Sandboxed execution** for untrusted code

## QuickJS Correspondence

This implementation corresponds to QuickJS functions:

| C# | QuickJS C |
|----|-----------|
| `JSEval.Evaluate()` | `JS_Eval()` |
| `JSEval.Compile()` | `JS_EvalThis()` with compile flag |
| `JSContext.Evaluate()` | `JS_Eval()` |
| `Function constructor` | `js_function_constructor()` |

## Tests

The `EvalTests` class provides comprehensive tests:

- Global `eval` function existence and properties
- Function constructor existence and properties  
- Parameter validation for reserved words
- Parameter validation for invalid identifiers
- JSEval static method behavior
- Empty/whitespace source handling
- Null argument handling

## Example Usage

```csharp
// Create runtime and context
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Evaluate simple expression
var result = context.Evaluate("1 + 2"); // Returns 3

// Evaluate with variables
context.SetGlobalProperty("x", JSValue.FromInt32(10));
var sum = context.Evaluate("x + 5"); // Returns 15

// Compile without executing
var compiled = context.Compile("return 42");
// compiled is a JSFunction that can be called later

// Use Function constructor
var fn = context.GetGlobalProperty("Function").AsObject() as JSFunction;
var add = fn.CallNative(JSValue.Undefined, new[]
{
    JSValue.FromString("a"),
    JSValue.FromString("b"),
    JSValue.FromString("return a + b")
});
```

## Implementation Notes

1. **Error Handling**: Syntax errors are converted to `SyntaxError` exceptions
2. **Empty Source**: Empty or whitespace-only source returns `undefined`
3. **Module Mode**: Use `EvaluateModule()` for ES module syntax support
4. **Compile-Only**: Use `Compile()` to get a callable function without execution

## Future Enhancements

Potential improvements:

1. **Direct eval scope access**: Support for local variable access in direct eval
2. **Source maps**: Debug information preservation
3. **Caching**: Compiled code caching for repeated evaluation
4. **Strict mode enforcement**: Automatic strict mode in modules
