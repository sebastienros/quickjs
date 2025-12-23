# Step 4.7: Destructuring Parser

This step implements destructuring patterns in the parser, enabling ES6+ syntax for extracting values from arrays and objects into variables.

## Overview

Destructuring assignment provides a concise syntax for unpacking values from arrays or properties from objects into distinct variables. This step adds support for:

- **Array destructuring**: `const [a, b] = arr`
- **Object destructuring**: `const { x, y } = obj`
- **Default values**: `const [a = 1] = arr`
- **Rest patterns**: `const [first, ...rest] = arr`
- **Nested patterns**: `const [{ a }, [b]] = complex`
- **Function parameter destructuring**: `([a, b]) => a + b`

## QuickJS Reference

From `quickjs.c`, the destructuring is handled by `js_parse_destructuring_element()` (lines 25710-26150). Key aspects:

```c
static __exception int js_parse_destructuring_element(JSParseState *s, 
    int tok, int is_arg, int hasval, int has_ellipsis, BOOL allow_initializer)
{
    // Handles both array and object destructuring patterns
    // Uses iterator protocol for arrays
    // Uses property access for objects
}
```

## Implementation

### Modified Files

1. **Parser.cs** - Enhanced with destructuring support in multiple methods:
   - `ParseVariableDeclarationList()` - Detects and routes destructuring patterns
   - `ParseArrayDestructuringVar()` - New method for array destructuring in declarations
   - `ParseObjectDestructuringVar()` - New method for object destructuring in declarations
   - `ParseDestructuringPattern()` - Enhanced core destructuring logic
   - `ParseArrowFunctionWithParams()` - Added destructuring parameter support

### Key Methods

#### ParseVariableDeclarationList

Routes to appropriate handler based on first token:

```csharp
private void ParseVariableDeclarationList(JSVarKind kind, bool isLexical, bool isConst)
{
    do
    {
        if (Check(TokenType.LeftBracket))
        {
            // Array destructuring: const [a, b] = arr
            ParseArrayDestructuringVar(isLexical, isConst);
        }
        else if (Check(TokenType.LeftBrace))
        {
            // Object destructuring: const { x, y } = obj
            ParseObjectDestructuringVar(isLexical, isConst);
        }
        else if (Check(TokenType.Identifier))
        {
            // Simple variable: const x = value
            ParseSimpleVarDeclaration(isLexical, isConst);
        }
    }
    while (Match(TokenType.Comma));
}
```

#### ParseArrayDestructuringVar

Handles array destructuring in variable declarations:

```csharp
private void ParseArrayDestructuringVar(bool isLexical, bool isConst)
{
    NextToken(); // consume '['
    
    // Parse pattern, collect variable names
    var varNames = new List<JSAtom>();
    
    while (!Check(TokenType.RightBracket))
    {
        if (Match(TokenType.Comma))
        {
            // Elision - skip element
            continue;
        }
        
        if (Match(TokenType.Ellipsis))
        {
            // Rest element: ...rest
            var name = ParseBindingIdentifier();
            varNames.Add(name);
            break;
        }
        
        if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
        {
            // Nested pattern
            ParseNestedDestructuringPattern();
        }
        else
        {
            // Simple binding
            var name = ParseBindingIdentifier();
            varNames.Add(name);
            
            if (Match(TokenType.Assign))
            {
                // Default value
                ParseAssignExpression();
            }
        }
        
        if (!Check(TokenType.RightBracket))
            Expect(TokenType.Comma);
    }
    
    Expect(TokenType.RightBracket);
    Expect(TokenType.Assign);
    
    // Parse initializer
    ParseAssignExpression();
    
    // Emit iterator-based extraction
    EmitArrayDestructuringOps(varNames);
}
```

#### ParseObjectDestructuringVar

Handles object destructuring in variable declarations:

```csharp
private void ParseObjectDestructuringVar(bool isLexical, bool isConst)
{
    NextToken(); // consume '{'
    
    while (!Check(TokenType.RightBrace))
    {
        if (Match(TokenType.Ellipsis))
        {
            // Rest properties: ...rest
            var name = ParseBindingIdentifier();
            // Emit CopyDataProperties for rest
            break;
        }
        
        // Property: key or key: value
        var key = ParsePropertyName();
        
        JSAtom valueName;
        if (Match(TokenType.Colon))
        {
            // Renamed binding: { x: alias }
            valueName = ParseBindingIdentifier();
        }
        else
        {
            // Shorthand: { x } means { x: x }
            valueName = key;
        }
        
        if (Match(TokenType.Assign))
        {
            // Default value
            ParseAssignExpression();
        }
        
        if (!Check(TokenType.RightBrace))
            Expect(TokenType.Comma);
    }
    
    Expect(TokenType.RightBrace);
    Expect(TokenType.Assign);
    
    ParseAssignExpression();
    
    // Emit property access ops
    EmitObjectDestructuringOps();
}
```

#### Arrow Function Destructuring Parameters

Added support for destructuring in arrow function parameters:

```csharp
private void ParseArrowFunctionWithParams(JSFunctionKind kind)
{
    while (!Check(TokenType.RightParen))
    {
        bool isRest = Match(TokenType.Ellipsis);
        
        if (Check(TokenType.Identifier))
        {
            // Simple parameter
            ParseSimpleParameter(isRest);
        }
        else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
        {
            // Destructuring parameter
            hasSimpleParameterList = false;
            
            // Create synthetic arg to receive value
            var syntheticAtom = CreateSyntheticArg();
            int idx = _currentFunction.AddArg(syntheticAtom);
            
            EmitOp(OpCode.GetArg);
            EmitU16((ushort)idx);
            
            ParseDestructuringPattern(false, isRest);
        }
        
        if (!Match(TokenType.Comma))
            break;
    }
}
```

## Bytecode Generation

### Array Destructuring Opcodes

For array destructuring, we use iterator protocol:

| Opcode | Description |
|--------|-------------|
| `ForOfStart` | Initialize iterator from iterable |
| `ForOfNext` | Get next value from iterator |
| `IteratorClose` | Close iterator when done |
| `Undefined` | Default for elided elements |

### Object Destructuring Opcodes

For object destructuring, we use property access:

| Opcode | Description |
|--------|-------------|
| `ToObject` | Convert value to object |
| `GetField` | Extract property by name |
| `CopyDataProperties` | Copy remaining properties for rest |
| `Dup` | Duplicate value for multiple extractions |
| `Drop` | Clean up stack |

### Default Values

Default values use undefined check:

```
GetValue
Dup
Undefined
StrictEq
IfFalse skip_default
Drop
ParseDefault
skip_default:
```

## Supported Patterns

### Array Destructuring Examples

```javascript
// Basic
const [a, b] = [1, 2];

// With elision
const [a, , b] = [1, 2, 3];

// Rest element
const [first, ...rest] = [1, 2, 3, 4];

// Default values
const [a = 1, b = 2] = [undefined];

// Nested
const [[a, b], [c, d]] = [[1, 2], [3, 4]];
```

### Object Destructuring Examples

```javascript
// Basic
const { x, y } = { x: 1, y: 2 };

// Renamed
const { x: a, y: b } = { x: 1, y: 2 };

// Default values
const { x = 1, y = 2 } = {};

// Rest properties
const { x, ...rest } = { x: 1, y: 2, z: 3 };

// Nested
const { a: { b } } = { a: { b: 1 } };
```

### Function Parameter Destructuring

```javascript
// Arrow function with array destructuring
const sum = ([a, b]) => a + b;

// Arrow function with object destructuring
const greet = ({ name, age }) => `Hello ${name}, ${age}`;

// Regular function
function process({ data, options = {} }) {
    // ...
}
```

## Test Coverage

49 new tests were added in `DestructuringParserTests.cs`:

| Category | Tests | Description |
|----------|-------|-------------|
| Array Destructuring | 12 | Basic, elision, rest, defaults, nested |
| Object Destructuring | 10 | Basic, rename, defaults, rest, computed |
| Combined Patterns | 10 | Arrays in objects, objects in arrays |
| Function Parameters | 8 | Regular and arrow function destructuring |
| Error Cases | 6 | Invalid syntax detection |
| Real-World Patterns | 4 | Complex practical examples |
| Edge Cases | 4 | Unicode, empty patterns, deep nesting |

## ECMAScript Specification Reference

- [Array Destructuring](https://tc39.es/ecma262/#sec-destructuring-binding-patterns)
- [Object Destructuring](https://tc39.es/ecma262/#sec-destructuring-binding-patterns)
- [Default Values](https://tc39.es/ecma262/#sec-runtime-semantics-keyedbindinginitialization)
- [Rest Elements](https://tc39.es/ecma262/#sec-destructuring-binding-patterns)

## Commit

```bash
git add -A
git commit -m "step-4.7-destructuring-parser: Add ES6 destructuring support

- Array destructuring: const [a, b] = arr
- Object destructuring: const { x, y } = obj  
- Default values: const [a = 1] = arr
- Rest patterns: const [...rest] = arr, const {...rest} = obj
- Nested destructuring patterns
- Function parameter destructuring (arrow and regular)
- 49 new tests for comprehensive coverage
- Total tests: 1104 (2208 with multi-targeting)"
git tag step-4.7-destructuring-parser
```

## Next Steps

Step 4.8 implemented the **Template Literal Parser** for tagged templates and template strings:
- Basic template literals: `` `Hello ${name}` ``
- Tagged templates: `` tag`string ${expr}` ``
- Nested templates
- Raw string access
