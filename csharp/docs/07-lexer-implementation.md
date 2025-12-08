# Step 2.2: Lexer Implementation

## Overview

This step implements the **JavaScript Lexer (Tokenizer)** - the component that reads JavaScript source code and produces a stream of tokens. The lexer handles all JavaScript lexical elements including identifiers, keywords, numbers, strings, operators, and comments.

## What is a Lexer?

A lexer (also called tokenizer or scanner) is the first phase of a compiler/interpreter pipeline:

```
Source Code  →  Lexer  →  Token Stream  →  Parser  →  AST
     ↓                         ↓
"let x = 42;"          [let][x][=][42][;]
```

The lexer reads raw characters and groups them into meaningful **tokens**, abstracting away details like whitespace and comments.

## Implementation

### Lexer Class

```csharp
public sealed class Lexer
{
    private readonly string _source;
    private readonly string _fileName;
    private int _position;
    private int _line;
    private int _column;
    private bool _hasLineTerminatorBefore;

    public Lexer(string source, string fileName = "<anonymous>");

    public Token NextToken();          // Get next token
    public List<Token> TokenizeAll();  // Get all tokens
    
    public int Position { get; }
    public int Line { get; }
    public int Column { get; }
    public bool IsAtEnd { get; }
}
```

### Usage

```csharp
// Create lexer
var lexer = new Lexer("let x = 42;", "script.js");

// Get tokens one at a time
Token token;
while ((token = lexer.NextToken()).Type != TokenType.EOF)
{
    Console.WriteLine($"{token.Type}: {token.Text}");
}

// Or get all tokens at once
var allTokens = new Lexer("a + b").TokenizeAll();
```

## Features

### 1. Identifier and Keyword Recognition

Identifiers follow JavaScript rules:
- Start with letter, `_`, or `$`
- Continue with letters, digits, `_`, or `$`
- Support Unicode characters (ES6)

```csharp
"myVariable"  → Identifier
"_private"    → Identifier
"$jquery"     → Identifier
"日本語"      → Identifier (Unicode)
"if"          → Keyword (If)
"function"    → Keyword (Function)
```

### 2. Numeric Literals

All JavaScript number formats:

```javascript
42          // Decimal integer
3.14        // Floating point
1e10        // Scientific notation
0xFF        // Hexadecimal
0b1010      // Binary
0o777       // Octal
1_000_000   // Numeric separators
123n        // BigInt
```

### 3. String Literals

Single and double quoted strings with escape sequences:

```javascript
"hello"           // Double quoted
'world'           // Single quoted
"line1\nline2"    // Escape sequences
"\u0041"          // Unicode escape (\uXXXX)
"\u{1F600}"       // Unicode code point (\u{XXXX})
"\x41"            // Hex escape (\xXX)
```

### 4. Template Literals

Backtick strings with embedded expressions:

```javascript
`Hello, ${name}!`
`Multi
line`
```

### 5. Comments

Both comment styles:

```javascript
// Single-line comment

/* Multi-line
   comment */
```

### 6. Operators and Punctuation

All JavaScript operators including modern ES6+ additions:

| Category | Operators |
|----------|-----------|
| Arithmetic | `+` `-` `*` `/` `%` `**` `++` `--` |
| Comparison | `<` `<=` `>` `>=` `==` `!=` `===` `!==` |
| Logical | `&&` `\|\|` `!` `??` |
| Bitwise | `&` `\|` `^` `~` `<<` `>>` `>>>` |
| Assignment | `=` `+=` `-=` `*=` ... `??=` |
| Arrow | `=>` |
| Optional | `?.` |
| Spread | `...` |

### 7. Private Names

ES2022 private class fields:

```javascript
#privateField  → PrivateName token
```

## Line Terminator Tracking

The lexer tracks whether a line terminator (newline) appeared before each token. This is critical for JavaScript's **Automatic Semicolon Insertion (ASI)**:

```javascript
return    // ASI inserts semicolon here!
  42

// Parsed as: return; 42;
```

```csharp
token.HasLineTerminatorBefore  // true if preceded by newline
```

## Unicode Support

The lexer fully supports Unicode:

### Whitespace
- Standard: space, tab, form feed, vertical tab
- Unicode: non-breaking space, en/em spaces, ideographic space
- BOM (byte order mark)

### Line Terminators
- LF (`\n`), CR (`\r`), CRLF (`\r\n`)
- Unicode: Line Separator (U+2028), Paragraph Separator (U+2029)

### Identifiers
- Unicode letters (Lu, Ll, Lt, Lm, Lo, Nl categories)
- Zero-width joiner/non-joiner (for complex scripts)

## Error Handling

The lexer produces `TokenType.Error` for unrecognized characters rather than throwing exceptions, allowing the parser to provide better error messages with context.

## Performance Considerations

1. **Keyword lookup**: Uses `Dictionary<string, TokenType>` for O(1) keyword detection
2. **Character classification**: Uses `[MethodImpl(AggressiveInlining)]` for fast ASCII checks
3. **Single pass**: Processes source once without backtracking for most tokens
4. **Minimal allocations**: Reuses source string via `Substring`

## File Structure

```
src/QuickJS.Core/
├── Lexer.cs       # Main lexer implementation
├── Token.cs       # Token class
└── TokenType.cs   # Token type enum
```

## Testing

The test suite covers:

1. **Identifiers**: ASCII, Unicode, keywords as prefixes
2. **Keywords**: All 40+ JavaScript keywords
3. **Numbers**: Decimal, hex, binary, octal, scientific, BigInt
4. **Strings**: Quotes, escapes, Unicode escapes
5. **Templates**: Basic template literals
6. **Comments**: Single-line and multi-line
7. **Operators**: All operators including multi-character
8. **Location**: Line/column tracking
9. **ASI support**: HasLineTerminatorBefore flag

## Example Output

```javascript
const add = (a, b) => a + b;
```

Tokens:
```
const     → Const
add       → Identifier("add")
=         → Assign
(         → LeftParen
a         → Identifier("a")
,         → Comma
b         → Identifier("b")
)         → RightParen
=>        → Arrow
a         → Identifier("a")
+         → Plus
b         → Identifier("b")
;         → Semicolon
EOF       → EOF
```

## Comparison with QuickJS C

The C implementation uses:
- `uint8_t*` pointer arithmetic
- UTF-8 encoded source
- Macros for character classification

Our C# implementation uses:
- String with index-based access
- UTF-16 (native C# strings)
- Methods with aggressive inlining

The core algorithm remains the same: a switch-based dispatcher for the first character, with specialized handlers for each token category.

## Next Steps

With the lexer complete, we can now proceed to:

1. **Phase 3**: Parser - using tokens to build an Abstract Syntax Tree
2. Add regular expression literal support (requires parser context)
3. Add better error recovery and reporting
