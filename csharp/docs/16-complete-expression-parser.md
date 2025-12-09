# Step 4.4: Complete Expression Parser

## Overview

This step extends the expression parser to handle advanced JavaScript expression types including function expressions, arrow functions, yield expressions, await expressions, and super expressions. These features are essential for supporting modern JavaScript patterns like callbacks, async/await, and generator functions.

## Changes Made

### Parser.cs Extensions

#### 1. Parenthesized Expression and Arrow Function Disambiguation

The challenge with arrow functions is distinguishing between:
- Parenthesized expression: `(a + b) * c`
- Arrow function with params: `(a, b) => a + b`
- Arrow function empty params: `() => 42`

```csharp
private void ParseParenthesizedExpressionOrArrow()
{
    NextToken(); // consume '('

    // Check for empty parens: () => ...
    if (Check(TokenType.RightParen))
    {
        NextToken(); // consume ')'
        if (!Check(TokenType.Arrow))
        {
            throw new JSSyntaxError("Unexpected token ')'", _currentToken.Start);
        }
        ParseArrowFunctionDirect(JSFunctionKind.Normal, new List<JSAtom>());
        return;
    }

    // Check for rest parameter: (...) => ...
    if (Check(TokenType.Ellipsis))
    {
        ParseArrowFunctionWithParams(JSFunctionKind.Normal);
        return;
    }

    // Parse as expression, tracking potential parameters
    var potentialParams = new List<JSAtom>();
    bool couldBeArrowParams = true;
    int startPos = _currentFunction.ByteCode.Size;

    // Track identifiers during parsing
    if (Check(TokenType.Identifier))
    {
        potentialParams.Add(_atoms.GetOrCreateAtom((string)_currentToken.Value!));
    }
    else
    {
        couldBeArrowParams = false;
    }
    ParseAssignExpression();

    // Continue with comma-separated items...
    // If we see => after ), it's an arrow function
    if (Check(TokenType.Arrow))
    {
        _currentFunction.ByteCode.Truncate(startPos);
        ParseArrowFunctionDirect(JSFunctionKind.Normal, potentialParams);
        return;
    }
}
```

#### 2. Arrow Function Direct Parsing

When we've already identified parameters, we can build the arrow function directly:

```csharp
private void ParseArrowFunctionDirect(JSFunctionKind kind, List<JSAtom> parameters)
{
    var parentFunction = _currentFunction;
    var newFunction = new JSFunctionDef(JSAtom.Empty);
    newFunction.FuncKind = kind;
    newFunction.FuncType = JSParseFunctionType.Arrow;
    
    ConfigureFunctionByType(newFunction, JSParseFunctionType.Arrow, kind);
    _currentFunction = newFunction;

    // Add all parameters
    foreach (var param in parameters)
    {
        _currentFunction.AddArg(param);
    }
    _currentFunction.DefinedArgCount = parameters.Count;
    _currentFunction.HasSimpleParameterList = true;

    Expect(TokenType.Arrow);

    // Parse body (expression or block)
    _currentFunction.PushScope();
    if (Check(TokenType.LeftBrace))
    {
        ParseFunctionBody();
    }
    else
    {
        ParseAssignExpression();
        EmitOp((kind & JSFunctionKind.Async) != 0 ? OpCode.ReturnAsync : OpCode.Return);
    }
    _currentFunction.PopScope();

    // Emit closure
    int funcIdx = parentFunction.AddChildFunction(newFunction);
    _currentFunction = parentFunction;
    EmitOp(OpCode.FClosure);
    EmitU16((ushort)funcIdx);
}
```

#### 3. Yield Expression Support

Generator functions use yield to produce values:

```csharp
private void ParseYieldExpression()
{
    if ((_currentFunction.FuncKind & JSFunctionKind.Generator) == 0)
    {
        throw new JSSyntaxError(
            "yield expression is only valid in generator functions",
            _currentToken.Start);
    }

    NextToken(); // consume 'yield'

    bool isStar = Match(TokenType.Asterisk);  // yield*

    // Check for expression
    if (!IsEndOfExpression())
    {
        ParseAssignExpression();
    }
    else
    {
        EmitOp(OpCode.Undefined);
    }

    EmitOp(isStar ? OpCode.YieldStar : OpCode.Yield);
}
```

#### 4. Await Expression Support

Async functions use await for asynchronous operations:

```csharp
private void ParseAwaitExpression()
{
    if ((_currentFunction.FuncKind & JSFunctionKind.Async) == 0)
    {
        throw new JSSyntaxError(
            "await expression is only valid in async functions",
            _currentToken.Start);
    }

    NextToken(); // consume 'await'
    ParseUnaryExpression(ParseFlags.None);
    EmitOp(OpCode.Await);
}
```

#### 5. Super Expression Support

Class methods can use `super` to access parent class:

```csharp
private void ParseSuperExpression()
{
    NextToken(); // consume 'super'

    if (Check(TokenType.LeftParen))
    {
        // super() - constructor call
        EmitOp(OpCode.GetSuper);
        ParseCallArguments();
        EmitOp(OpCode.CallConstructor);
        EmitU16((ushort)argCount);
    }
    else if (Match(TokenType.Dot))
    {
        // super.property
        EmitOp(OpCode.GetSuperField);
        EmitAtom(propertyAtom);
    }
    else if (Match(TokenType.LeftBracket))
    {
        // super[expr]
        EmitOp(OpCode.GetSuper);
        ParseExpression();
        Expect(TokenType.RightBracket);
        EmitOp(OpCode.GetPropertyValue);
    }
}
```

#### 6. Async Expression Parsing

The `async` keyword can appear in expressions:

```csharp
private void ParseAsyncExpression()
{
    NextToken(); // consume 'async'

    // Check line terminator - async [no LineTerminator] function/arrow
    if (_currentToken.HasLineTerminatorBefore)
    {
        // Treat 'async' as identifier
        EmitOp(OpCode.ScopeGetVar);
        EmitAtom(_atoms.GetOrCreateAtom("async"));
        return;
    }

    if (Check(TokenType.Function))
    {
        // async function expression
        ParseFunction(JSParseFunctionType.Expression, JSFunctionKind.Async, JSAtom.Empty);
    }
    else if (Check(TokenType.LeftParen))
    {
        // async arrow function: async (params) => ...
        ParseArrowFunctionWithParams(JSFunctionKind.Async);
    }
    else if (Check(TokenType.Identifier))
    {
        // async arrow with single param: async x => ...
        // Parse parameter and arrow body
    }
}
```

### ByteCodeBuffer.cs Update

Added `Truncate` method for bytecode backtracking when we need to discard tentatively emitted code:

```csharp
public void Truncate(int position)
{
    if (position < 0 || position > _size)
        throw new ArgumentOutOfRangeException(nameof(position));
    _size = position;
}
```

## Bytecode Generated

### Arrow Functions
```
// () => 42
FClosure <funcIdx>    // Creates closure for arrow function
                      // Arrow function body: Push 42, Return
```

### Yield Expressions
```
// yield value
<value>               // Push value to yield
Yield                 // Yield and suspend generator

// yield* iterable
<iterable>            // Push iterable
YieldStar             // Delegate to iterable
```

### Await Expressions
```
// await promise
<promise>             // Push promise/value
Await                 // Await result
```

### Super Expressions
```
// super()
GetSuper              // Get super constructor
<args>                // Push arguments
CallConstructor N     // Call with N args

// super.method
GetSuperField <atom>  // Get super property

// super[expr]
GetSuper              // Get super object
<expr>                // Push key
GetPropertyValue      // Get property by key
```

## ECMAScript Compliance Notes

### Line Terminator Restrictions

The `async` keyword has special line terminator rules:
- `async [no LineTerminator] function` - async function
- `async [no LineTerminator] ArrowFunction` - async arrow
- `async \n function` - async is identifier, function is separate

We check `_currentToken.HasLineTerminatorBefore` AFTER consuming `async` to determine if the next token is separated by a newline.

### Arrow Parameter Restrictions

Arrow function parameters have restrictions:
- Simple parameters: `(a, b, c)`
- Default parameters: `(a = 1)`
- Rest parameters: `(...args)` - must be last
- Destructuring: `({ x, y })` - not yet implemented

### Yield/Await Context

- `yield` is only valid inside generator functions
- `await` is only valid inside async functions
- Both throw syntax errors if used in wrong context

## Test Coverage

This step enables the following test categories:
- Arrow function tests (empty params, multiple params, block body)
- Async function tests (declarations and expressions)
- Generator function tests (yield expressions)
- Function expression tests

### Tests Still Skipped

Two advanced cases require additional lookahead:
1. `x => x * 2` - Single param without parentheses
2. `a => b => a + b` - Nested arrow functions

These require the parser to look ahead past an identifier to see if `=>` follows, which requires more complex state management.

## Usage Example

```javascript
// Arrow functions
var f = () => 42;
var add = (a, b) => a + b;
var square = (x) => { return x * x; };

// Async/await
async function loadData() {
    var response = await fetch('/api/data');
    return response.json();
}

// Generators
function* range(start, end) {
    for (var i = start; i < end; i++) {
        yield i;
    }
}

// Super calls
class Child extends Parent {
    constructor() {
        super();
    }
    method() {
        return super.method();
    }
}
```

## Next Steps

Step 4.5 will implement the class parser for full ES6 class syntax including:
- Class declarations and expressions
- Constructor methods
- Instance and static methods
- Getter/setter accessors
- Field declarations
- Private members (#private)
- Computed property names
