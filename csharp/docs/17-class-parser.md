# Step 4.5: Class Parser

This step implements class declaration and expression parsing for the QuickJS C# port.

## Overview

JavaScript classes are syntactic sugar over prototypal inheritance. The parser handles:

- **Class declarations**: `class Foo { }`
- **Class expressions**: `var Foo = class { }`  
- **Inheritance**: `class Dog extends Animal { }`
- **Methods**: Regular methods, getters, setters
- **Static members**: `static method() { }`
- **Class fields**: `x = 1;`
- **Computed property names**: `[expr]() { }`
- **Async/generator methods**: `async fetch() { }`, `*generator() { }`
- **Static initialization blocks**: `static { }`

## Key Implementation Details

### Class Parsing Entry Points

Two methods handle the different contexts:

```csharp
// Called from ParseStatement for: class Foo { }
private void ParseClassDeclaration()
{
    ParseClass(isExpression: false);
}

// Called from ParsePrimaryExpression for: var x = class { }
private void ParseClassExpression()
{
    ParseClass(isExpression: true);
}
```

### Core Class Parsing

The `ParseClass` method handles both cases:

1. **Parse class name** (required for declarations, optional for expressions)
2. **Define variable** for the class name (declarations only)
3. **Parse extends clause** if present
4. **Emit DefineClass opcode** to create the class
5. **Parse class body**
6. **Store class** in variable (declarations only)

```csharp
private void ParseClass(bool isExpression)
{
    NextToken(); // consume 'class'
    
    JSAtom className = JSAtom.Empty;
    if (Check(TokenType.Identifier))
    {
        className = GetAtom(_currentToken.Value);
        NextToken();
    }
    else if (!isExpression)
    {
        throw new JSSyntaxError("Class declaration requires a name");
    }
    
    // Parse 'extends Parent' if present
    bool hasHeritage = Match(TokenType.Extends);
    if (hasHeritage)
        ParseMemberExpression();
    else
        EmitOp(OpCode.Undefined);
    
    // Define the class
    EmitOp(OpCode.DefineClass);
    EmitAtom(className);
    EmitU8((byte)(hasHeritage ? 1 : 0));
    
    // Parse body
    Expect(TokenType.LeftBrace);
    ParseClassBody();
    Expect(TokenType.RightBrace);
    
    // Store if declaration
    if (!isExpression && className != JSAtom.Empty)
    {
        EmitOp(OpCode.ScopePutVar);
        EmitAtom(className);
    }
}
```

### Class Element Parsing

Class elements include methods, fields, getters/setters, and static members. The parser uses lookahead to distinguish:

- **Modifiers**: `static`, `async`, `*` (generator)
- **Accessor keywords**: `get`, `set`
- **Property names**: identifiers, strings, numbers, computed `[expr]`
- **Methods vs fields**: Methods have `()`, fields don't

The key challenge is that keywords like `static`, `get`, `set`, `async` can also be method names:

```javascript
class Foo {
    static()    { }  // Method named "static"
    get()       { }  // Method named "get", NOT a getter
    async()     { }  // Method named "async", NOT async method
    get value() { }  // This IS a getter
}
```

### Lookahead Pattern

The parser saves position, consumes the potential modifier, checks what follows, then either:
- Keeps the modifier flag set (modifier case)
- Restores position to re-parse as method name

```csharp
if (Check(TokenType.Static))
{
    var savedToken = _currentToken;
    var savedPos = _lexer.SavePosition();
    NextToken(); // consume 'static'
    
    if (Check(TokenType.LeftParen) || Check(TokenType.Semicolon))
    {
        // 'static' is the method name
        _lexer.RestorePosition(savedPos);
        _currentToken = savedToken;
    }
    else
    {
        // 'static' is a modifier
        isStatic = true;
    }
}
```

### Keywords as Property Names

In JavaScript, reserved words are valid as property names:

```javascript
class Foo {
    if()       { }  // Valid
    return()   { }  // Valid  
    delete()   { }  // Valid
}
```

The `IsPropertyNameToken()` helper checks if the current token can be a property name:

```csharp
private bool IsPropertyNameToken()
{
    return _currentToken.Type == TokenType.Identifier ||
           _currentToken.Type == TokenType.String ||
           _currentToken.Type == TokenType.Number ||
           IsKeyword(_currentToken.Type);
}
```

## Opcodes Used

| Opcode | Description |
|--------|-------------|
| `DefineClass` | Creates class constructor and prototype |
| `DefineMethod` | Adds method to class prototype |
| `DefineMethodComputed` | Adds method with computed name |
| `DefineField` | Defines class field |

Method flags byte:
- Bit 0: static
- Bit 1: getter
- Bit 2: setter
- Bit 3: private

## Test Categories

The test file `ClassParserTests.cs` covers:

1. **Class Declarations** - Basic class syntax
2. **Class Expressions** - Anonymous and named
3. **Inheritance** - `extends` clause
4. **Getters and Setters** - Property accessors
5. **Static Members** - Static methods and fields
6. **Computed Property Names** - `[expr]()` syntax
7. **Class Fields** - Instance and static fields
8. **Async/Generator Methods** - Async and generator syntax
9. **Special Method Names** - Keywords as method names (`get()`, `set()`, `static()`, `async()`)

## QuickJS Reference

The implementation follows QuickJS's `js_parse_class()` function from `quickjs.c`:

- Uses lookahead to distinguish modifiers from method names
- Handles all ES2015+ class features
- Emits appropriate opcodes for runtime class creation

## Files Modified

- `src/QuickJS.Core/Parser.cs`
  - Added `ParseClassDeclaration()`, `ParseClassExpression()`
  - Added `ParseClass()`, `ParseClassBody()`, `ParseClassElement()`
  - Added `ParseClassField()`, `ParseClassMethod()`, `ParseStaticBlock()`
  - Added `IsPropertyNameToken()`, `IsKeyword()`, `GetPropertyName()` helpers
  - Updated `ParseStatement()` and `ParsePrimaryExpression()` for class case

- `tests/QuickJS.Tests/ClassParserTests.cs` - 44 test methods

## Test Results

```
Total:     2042
Passed:    2042
Failed:    0
Skipped:   0
```

New tests: 88 (44 methods × 2 frameworks)
