# Step 4.8: Parser Error Recovery

## Overview

This step adds a diagnostic collection infrastructure to the parser, providing better error messages and improved developer experience when parsing JavaScript source code.

## Learning Objectives

1. Understanding error recovery in parsers
2. Creating diagnostic collection systems
3. Designing user-friendly error messages
4. Implementing error codes and categories

## Key Concepts

### Diagnostic Severity

The parser distinguishes between different severity levels:

```csharp
public enum DiagnosticSeverity
{
    Error,   // Prevents successful parsing
    Warning, // Allows parsing but indicates potential issues
    Info     // Informational messages
}
```

### Error Codes

Error codes are organized into categories:

| Range | Category | Description |
|-------|----------|-------------|
| JS1xxx | Lexer | Tokenization errors (unterminated strings, invalid characters) |
| JS2xxx | Syntax | General syntax errors (unexpected tokens, missing expressions) |
| JS3xxx | Declaration | Declaration errors (const without initializer, duplicate declarations) |
| JS4xxx | Control Flow | Control flow errors (illegal break/continue, undefined labels) |
| JS5xxx | Class/Function | Class and function errors (duplicate constructor, yield outside generator) |
| JS6xxx | Module | Module errors (import/export not at top level) |
| JS8xxx | Warning | Warning codes (useless statements, with statement) |

### ParseDiagnostic

A diagnostic captures all relevant information about a parse error:

```csharp
public sealed class ParseDiagnostic
{
    public DiagnosticSeverity Severity { get; }
    public string Code { get; }         // e.g., "JS2001"
    public string Message { get; }      // Human-readable message
    public SourceLocation Location { get; }
    public string? FileName { get; }
    public string? SourceContext { get; }  // Code snippet showing the error
}
```

### DiagnosticBag

The `DiagnosticBag` class collects diagnostics during parsing:

```csharp
var bag = new DiagnosticBag();

// Add errors and warnings
bag.AddError(ParseErrorCode.UnexpectedToken, "Unexpected token", location);
bag.AddWarning(ParseErrorCode.WithStatement, "Use of with statement", location);

// Query diagnostics
if (bag.HasErrors) { /* ... */ }
foreach (var error in bag.GetErrors()) { /* ... */ }
foreach (var warning in bag.GetWarnings()) { /* ... */ }
```

### Error Message Formatting

The `ParserErrorMessages` class provides consistent formatting:

```csharp
// Format token types for display
ParserErrorMessages.FormatToken(TokenType.Semicolon) // Returns "';'"
ParserErrorMessages.FormatToken(TokenType.EOF)       // Returns "end of input"

// Create error messages
ParserErrorMessages.UnexpectedToken(TokenType.Plus)  // "Unexpected '+'"
ParserErrorMessages.ExpectedGot(TokenType.Semicolon, TokenType.Plus)
    // "Expected ';', but got '+'"
```

## Implementation Details

### Parser Integration

The parser has a `Diagnostics` property that collects errors during parsing:

```csharp
var atoms = new AtomTable();
var parser = new Parser("var x = @;", "script.js", atoms);

try
{
    parser.ParseProgram();
}
catch (JSSyntaxError ex)
{
    // Access collected diagnostics
    foreach (var diagnostic in parser.Diagnostics.GetErrors())
    {
        Console.WriteLine(diagnostic.ToString());
        // Output: script.js(1,9): error JS2001: Unexpected token
    }
}
```

### Detailed Error Output

Diagnostics can provide detailed output with source context:

```csharp
var diagnostic = parser.Diagnostics.GetErrors().First();
Console.WriteLine(diagnostic.ToDetailedString());

// Output:
// script.js(1,9): error JS2001: Unexpected token
//    1 | var x = @;
//      |         ^
```

### Error Code Descriptions

Get human-readable descriptions for error codes:

```csharp
ParseErrorCode.GetDescription("JS2001")  // "Unexpected token"
ParseErrorCode.GetDescription("JS3003")  // "Const requires initializer"
```

## Files Changed

### New Files

- `src/QuickJS.Core/ParseDiagnostic.cs` - Diagnostic infrastructure
  - `DiagnosticSeverity` enum
  - `ParseDiagnostic` class
  - `ParseErrorCode` constants
  - `DiagnosticBag` collection

- `src/QuickJS.Core/ParserErrorMessages.cs` - Error message formatting utilities

### Modified Files

- `src/QuickJS.Core/Parser.cs` - Added diagnostics integration

### Test Files

- `tests/QuickJS.Tests/ParserDiagnosticsTests.cs` - 26 new tests covering:
  - DiagnosticBag operations
  - ParseDiagnostic creation and formatting
  - Error code validation
  - Parser diagnostics integration
  - Error message formatting

## Benefits

1. **Better Error Messages**: Contextual error messages with source location and code snippets
2. **Multiple Errors**: Collect multiple errors in a single parse pass (foundation for future enhancement)
3. **Error Categories**: Organized error codes help identify error types quickly
4. **IDE Integration**: Structured diagnostics enable IDE features like error squiggles
5. **Consistent Formatting**: Standardized token and message formatting

## Connection to QuickJS C Implementation

QuickJS uses `js_parse_error` for error reporting:

```c
static __attribute__((format(printf, 2, 3))) int js_parse_error(JSParseState *s, const char *fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    JS_ThrowSyntaxErrorV(s->ctx, s->filename, s->line_num, fmt, ap);
    va_end(ap);
    return -1;
}
```

Our C# implementation provides similar functionality with enhanced diagnostics:

```csharp
private JSSyntaxError ReportError(string code, string message, SourceLocation location)
{
    var diagnostic = new ParseDiagnostic(
        DiagnosticSeverity.Error,
        code,
        message,
        location,
        _fileName);
    _diagnostics.Add(diagnostic);
    return new JSSyntaxError(message, location);
}
```

## Testing

Run the new diagnostic tests:

```bash
cd csharp
dotnet test --filter "FullyQualifiedName~ParserDiagnosticsTests"
```

## Summary

This step establishes the foundation for error recovery in the parser. While currently the parser still throws on the first error, the diagnostic infrastructure allows for:

1. Collecting multiple errors in a single pass (future enhancement)
2. Better error formatting and context
3. Categorized error codes
4. IDE-friendly structured diagnostics

The diagnostic system follows patterns used in modern compilers and IDEs like Roslyn and TypeScript.
