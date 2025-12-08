# Overview of QuickJS

QuickJS is a small, embeddable JavaScript engine written in C by Fabrice Bellard.

## Source Code Structure

- **Core Engine**: `quickjs.c`, `quickjs.h` - The main JavaScript interpreter
- **Standard Library**: `quickjs-libc.c`, `quickjs-libc.h` - OS/file/process bindings
- **Regular Expressions**: `libregexp.c`, `libregexp.h` - Custom regex engine
- **Unicode Support**: `libunicode.c`, `libunicode.h` - Unicode handling
- **Utilities**: `cutils.c`, `dtoa.c` - Helper functions, number-to-string conversion
- **CLI Tools**: `qjs.c` (interpreter), `qjsc.c` (compiler)

## Recommended Organization in C#

```
QuickJS.NET/
├── src/
│   ├── QuickJS.Core/           # Core engine
│   │   ├── Runtime/
│   │   │   ├── JSRuntime.cs
│   │   │   ├── JSContext.cs
│   │   │   └── JSValue.cs
│   │   ├── Parser/
│   │   │   ├── Lexer.cs
│   │   │   ├── Parser.cs
│   │   │   └── AST/
│   │   ├── Compiler/
│   │   │   ├── BytecodeCompiler.cs
│   │   │   └── Opcodes.cs
│   │   ├── Interpreter/
│   │   │   └── BytecodeInterpreter.cs
│   │   ├── Objects/
│   │   │   ├── JSObject.cs
│   │   │   ├── JSFunction.cs
│   │   │   ├── JSArray.cs
│   │   │   └── JSString.cs
│   │   └── GC/
│   │       └── GarbageCollector.cs
│   ├── QuickJS.RegExp/         # Regex engine
│   ├── QuickJS.Unicode/        # Unicode support
│   └── QuickJS.StandardLib/    # Standard library bindings
└── tests/
```

## Features to IGNORE or Simplify

### 1. Manual Memory Management (High Priority to Change)

The C code has extensive manual memory allocation (`js_malloc`, `js_free`, reference counting). In C#:
- Use .NET's garbage collector
- Replace `JSRefCountHeader` with normal object references
- Remove all `JS_FreeValue`, `JS_DupValue` calls

### 2. Low-Level Pointer Arithmetic

- Replace pointer manipulation with array indexing or `Span<T>`
- Use `Memory<T>` for buffer handling

### 3. Union Types in JSValue

The C code uses unions for `JSValue`. In C#, consider:

```csharp
public readonly struct JSValue
{
    public readonly JSValueType Tag;
    private readonly long _value; // Or use a discriminated union pattern
}
```

### 4. Goto Statements

QuickJS uses `goto` extensively for error handling. Replace with:
- Try/catch/finally
- Early returns with proper cleanup

### 5. Custom dtoa (Double-to-ASCII)

- Use .NET's built-in `double.ToString()` with appropriate format specifiers
- Or use `Grisu3` algorithm implementations available in .NET

### 6. Custom Unicode Tables

- Replace `libunicode.c` with `System.Globalization` and `System.Text.Unicode`
- Use .NET's `Rune`, `StringInfo`, and `UnicodeCategory`

### 7. Platform-Specific Code

- Replace POSIX/Win32 APIs with .NET abstractions (`System.IO`, `System.Diagnostics`)

## Features to KEEP but Adapt

### 1. Bytecode Format

Keep the bytecode instruction set but adapt:

```csharp
public enum Opcode : byte
{
    Push,
    Pop,
    Call,
    // ... from quickjs-opcode.h
}
```

### 2. Parser/Lexer

Rewrite in idiomatic C# with:
- `ReadOnlySpan<char>` for tokenization
- Pattern matching for token types

### 3. Regular Expression Engine

Could either:
- Port `libregexp.c` to C# (for exact compatibility)
- Or use `System.Text.RegularExpressions` with compatibility flags

### 4. BigInt Support

- Use `System.Numerics.BigInteger` instead of custom implementation

## Key Challenges

| Challenge | C Approach | C# Solution |
|-----------|-----------|-------------|
| Value representation | Tagged union (64-bit) | Struct with tag + object/primitive |
| Property access | Hash tables with custom alloc | `Dictionary<string, JSValue>` or custom |
| Closures | Upvalue chains with manual management | Captured variables naturally via closures |
| Stack frames | Manual stack management | Use call stack or explicit `Stack<Frame>` |
| Atomized strings | Global atom table | `string.Intern()` or custom intern pool |

## Suggested Approach

1. **Start with JSValue and basic types** - Get the value representation right first
2. **Implement the parser** - Build AST from JavaScript source
3. **Build the bytecode compiler** - Convert AST to bytecode
4. **Create the interpreter loop** - Execute bytecode
5. **Add built-in objects** - Object, Array, Function, etc.
6. **Implement the standard library** - Console, JSON, Math, etc.
7. **Add advanced features** - Modules, async/await, generators

## Effort Estimate

- `quickjs.c` alone is **~55,000 lines** of dense C code
- A faithful C# port would likely be **30-40K lines** (cleaner abstractions, no manual memory)
- Expect **6-12 months** of focused work for a complete implementation

---
