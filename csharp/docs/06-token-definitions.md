# Step 2.1: Token Definitions

## Overview

This step implements **lexical token definitions** - the fundamental types used to represent the output of the JavaScript lexer. Tokens are the basic building blocks that the parser consumes to build an Abstract Syntax Tree (AST).

## What is Lexical Analysis?

Lexical analysis (or "tokenization") is the first phase of parsing JavaScript source code:

```
Source Code → Lexer → Token Stream → Parser → AST
```

The lexer reads raw characters and groups them into meaningful units called **tokens**. For example:

```javascript
let x = 42 + y;
```

Becomes the token stream:

| Token | Type | Value |
|-------|------|-------|
| `let` | Keyword (Let) | - |
| `x` | Identifier | "x" |
| `=` | Assign | - |
| `42` | Number | 42 |
| `+` | Plus | - |
| `y` | Identifier | "y" |
| `;` | Semicolon | - |

## C# Implementation

### TokenType Enum

The `TokenType` enum defines all possible token types in JavaScript:

```csharp
public enum TokenType
{
    // Special
    EOF = 0,
    Error,

    // Literals
    Number,
    String,
    Template,
    RegExp,

    // Identifiers
    Identifier,
    PrivateName,  // #privateField

    // Keywords (40+ keywords)
    Null, False, True,
    If, Else, Do, While, For, // ...

    // Punctuation
    LeftParen, RightParen,    // ( )
    LeftBrace, RightBrace,    // { }
    LeftBracket, RightBracket, // [ ]
    Comma, Semicolon, Colon, Dot, Ellipsis,

    // Operators
    Plus, Minus, Asterisk, Slash, Percent, Power,
    Increment, Decrement,
    // ... comparison, logical, bitwise, assignment
}
```

### Token Class

The `Token` class represents a single token with its metadata:

```csharp
public sealed class Token
{
    public TokenType Type { get; }
    public string Text { get; }           // Raw source text
    public SourceLocation Start { get; }
    public SourceLocation End { get; }
    public object? Value { get; }         // Parsed value for literals
    public bool HasLineTerminatorBefore { get; }  // For ASI
}
```

#### HasLineTerminatorBefore

This property is critical for JavaScript's **Automatic Semicolon Insertion (ASI)**:

```javascript
return     // ASI inserts semicolon here!
  42

// Equivalent to: return; 42;
```

The lexer tracks whether a line terminator appeared before each token, allowing the parser to correctly implement ASI.

### Token Properties

```csharp
token.IsEOF          // End of input?
token.IsError        // Lexer error?
token.IsKeyword      // Is a keyword (if, for, function, etc.)
token.IsLiteral      // Is a literal (number, string, true, false, null)
token.IsAssignmentOperator    // =, +=, -=, etc.
token.IsComparisonOperator    // ==, ===, <, >, etc.
token.IsStrictModeReserved    // Reserved in strict mode (let, yield, etc.)
```

### TokenType Extensions

Extension methods provide conversions between token types and their string representations:

```csharp
// Get keyword string
TokenType.If.GetKeyword()       // "if"
TokenType.Function.GetKeyword() // "function"

// Get punctuator string
TokenType.Plus.GetPunctuator()        // "+"
TokenType.StrictEqual.GetPunctuator() // "==="
TokenType.Arrow.GetPunctuator()       // "=>"

// Operator classification
TokenType.Plus.IsBinaryOperator()   // true
TokenType.Plus.IsUnaryOperator()    // true (+x is valid)
TokenType.Asterisk.IsUnaryOperator() // false (*x is not valid)
```

## JavaScript Token Categories

### Keywords (Reserved Words)

JavaScript has several categories of keywords:

| Category | Examples |
|----------|----------|
| Control Flow | `if`, `else`, `for`, `while`, `do`, `switch`, `case` |
| Declarations | `var`, `let`, `const`, `function`, `class` |
| Operators | `typeof`, `instanceof`, `in`, `delete`, `void` |
| Values | `null`, `true`, `false`, `this`, `super` |
| Modules | `import`, `export` |
| Exception | `try`, `catch`, `finally`, `throw` |
| Strict Reserved | `implements`, `interface`, `private`, `public` |

### Operators

JavaScript has a rich set of operators:

| Category | Operators |
|----------|-----------|
| Arithmetic | `+`, `-`, `*`, `/`, `%`, `**` |
| Increment | `++`, `--` |
| Comparison | `<`, `<=`, `>`, `>=`, `==`, `!=`, `===`, `!==` |
| Logical | `&&`, `\|\|`, `!`, `??` |
| Bitwise | `&`, `\|`, `^`, `~`, `<<`, `>>`, `>>>` |
| Assignment | `=`, `+=`, `-=`, `*=`, `/=`, `&&=`, `\|\|=`, `??=` |
| Misc | `?`, `:`, `=>`, `?.`, `...` |

### Modern JavaScript (ES6+)

The lexer supports modern JavaScript features:

- **Arrow functions**: `=>`
- **Template literals**: `` ` ``
- **Optional chaining**: `?.`
- **Nullish coalescing**: `??`, `??=`
- **Exponentiation**: `**`, `**=`
- **Private fields**: `#name`
- **Logical assignment**: `&&=`, `||=`

## File Structure

```
src/QuickJS.Core/
├── TokenType.cs     # Token type enumeration
└── Token.cs         # Token class and extensions
```

## Usage Example

```csharp
// Creating tokens (typically done by the Lexer)
var letToken = new Token(
    TokenType.Let,
    "let",
    new SourceLocation("app.js", 1, 1),
    new SourceLocation("app.js", 1, 3)
);

var numToken = new Token(
    TokenType.Number,
    "42",
    new SourceLocation("app.js", 1, 9),
    new SourceLocation("app.js", 1, 10),
    value: 42.0
);

// Checking token properties
if (letToken.IsKeyword) { /* ... */ }
if (numToken.IsLiteral) { /* ... */ }

// Getting values
double? num = numToken.GetNumberValue(); // 42.0
string? str = stringToken.GetStringValue(); // "hello"
```

## Design Decisions

### Why a Class Instead of a Struct?

`Token` is a class (reference type) because:

1. **Variable size**: Tokens can hold different types of values
2. **Nullable value**: The `Value` property is often null
3. **Immutability**: Using `sealed class` with readonly properties
4. **Boxing avoidance**: Storing object? value in struct would box anyway

### Why Separate TokenType and Token?

- **TokenType**: Lightweight enum for switch statements and comparisons
- **Token**: Full token with source location and value

This allows efficient pattern matching on token types while preserving full information when needed.

## Comparison with QuickJS C

QuickJS C uses negative integers for tokens to distinguish from single-character tokens:

```c
enum {
    TOK_NUMBER = -128,
    TOK_STRING,
    TOK_IDENT,
    // ...
};
```

In C#, we use a proper enum starting at 0, which is cleaner and type-safe.

## Testing

The test suite covers:

1. **TokenType enum**: All keywords and punctuators have correct strings
2. **Token class**: Construction, properties, value extraction
3. **Extension methods**: IsBinaryOperator, IsUnaryOperator, etc.
4. **Edge cases**: Strict mode reserved words, operator classification

## Next Steps

With token definitions complete, we can implement:

1. **Step 2.2**: Basic Lexer - Identifiers and Keywords
2. **Step 2.3**: Lexer - Numbers
3. **Step 2.4**: Lexer - Strings and Templates
4. **Step 2.5**: Lexer - Operators and Punctuation
