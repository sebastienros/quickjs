# Step 4.3: Function Parser

## Overview

This step implements function parsing in our JavaScript parser. Functions are one of the most fundamental concepts in JavaScript - they provide code reuse, encapsulation, and form the basis for objects and modules. QuickJS compiles functions into separate bytecode units that can be instantiated as closures at runtime.

## JavaScript Functions: A Deeper Look

### Why Functions Are Special

In JavaScript, functions are "first-class citizens," meaning they:
- Can be assigned to variables
- Can be passed as arguments
- Can be returned from other functions
- Can have properties (they're objects too!)

This first-class nature requires sophisticated parser support.

### The Many Forms of JavaScript Functions

JavaScript has evolved to support multiple function syntaxes:

```javascript
// Function declaration (hoisted)
function add(a, b) { return a + b; }

// Generator function
function* range(start, end) {
    for (let i = start; i < end; i++) yield i;
}

// Async function
async function fetchData() {
    return await fetch('/api/data');
}

// Async generator
async function* asyncRange() {
    yield await getValue();
}

// Function expression (not hoisted)
const multiply = function(a, b) { return a * b; };

// Arrow function (lexical this)
const double = x => x * 2;
```

## QuickJS Function Parsing Architecture

### JSFunctionDef: The Function Container

QuickJS represents each function during compilation with a `JSFunctionDef` structure:

```c
// From quickjs.c
typedef struct JSFunctionDef {
    JSContext *ctx;
    struct JSFunctionDef *parent;  // Enclosing function
    int parent_cpool_idx;           // Index in parent's constant pool
    
    JSAtom func_name;
    int func_kind : 8;              // Generator, async, etc.
    int has_prototype : 1;
    int has_simple_parameter_list : 1;
    int is_derived_class_constructor : 1;
    
    // Parameters and variables
    int arg_count;
    int defined_arg_count;          // Args before rest/defaults
    JSVarDef *args;
    JSVarDef *vars;
    
    // Bytecode generation
    DynBuf byte_code;
    // ... more fields
} JSFunctionDef;
```

### Function Types and Kinds

QuickJS distinguishes between:

**Parse Function Type** (where the function appears):
- `JS_PARSE_FUNC_STATEMENT` - `function foo() {}`
- `JS_PARSE_FUNC_EXPR` - `var x = function() {}`
- `JS_PARSE_FUNC_ARROW` - `x => x + 1`
- `JS_PARSE_FUNC_GETTER/SETTER` - Property accessors
- `JS_PARSE_FUNC_CLASS_CONSTRUCTOR` - Class constructors
- `JS_PARSE_FUNC_METHOD` - Object/class methods

**Function Kind** (special behavior):
- `JS_FUNC_NORMAL` - Regular function
- `JS_FUNC_GENERATOR` - Uses `yield`
- `JS_FUNC_ASYNC` - Uses `await`
- `JS_FUNC_ASYNC_GENERATOR` - Both `yield` and `await`

## C# Implementation

### ParseFunctionDeclaration

The entry point for parsing `function` statements:

```csharp
public void ParseFunctionDeclaration()
{
    // Determine function kind from current token
    var isAsync = CurrentToken.Type == TokenType.Async;
    if (isAsync) Advance();
    
    Expect(TokenType.Function);
    
    // Check for generator
    var isGenerator = Match(TokenType.Star);
    
    // Determine function kind
    var funcKind = isAsync && isGenerator ? JSFunctionKind.AsyncGenerator :
                   isAsync ? JSFunctionKind.Async :
                   isGenerator ? JSFunctionKind.Generator :
                   JSFunctionKind.Normal;
    
    // Function name is required for declarations
    if (CurrentToken.Type != TokenType.Identifier)
        throw new JSSyntaxError("Function declaration requires a name", filename, line);
    
    var funcName = atoms.Intern(CurrentToken.StringValue);
    Advance();
    
    // Parse the function body (creates nested JSFunctionDef)
    ParseFunctionBody(JSParseFunctionType.Statement, funcKind, funcName);
}
```

### ParseFunctionBody

Creates a new function scope and compiles the function:

```csharp
public void ParseFunctionBody(JSParseFunctionType funcType, JSFunctionKind funcKind, JSAtom funcName)
{
    // Save current function context
    var parentFunc = currentFunction;
    
    // Create new function definition
    var childFunc = new JSFunctionDef(funcName);
    childFunc.FuncKind = funcKind;
    ConfigureFunctionByType(childFunc, funcType, funcKind);
    
    // Link to parent for closure support
    childFunc.Parent = parentFunc;
    
    // Switch context to child function
    currentFunction = childFunc;
    
    // Parse parameter list
    Expect(TokenType.LeftParen);
    bool hasSimpleParameterList = true;
    if (CurrentToken.Type != TokenType.RightParen)
        ParseParameterList(ref hasSimpleParameterList);
    Expect(TokenType.RightParen);
    
    childFunc.HasSimpleParameterList = hasSimpleParameterList;
    
    // For generators, emit initial yield
    if (funcKind == JSFunctionKind.Generator || 
        funcKind == JSFunctionKind.AsyncGenerator)
    {
        EmitOp(OpCode.InitialYield);
    }
    
    // Parse function body
    Expect(TokenType.LeftBrace);
    while (CurrentToken.Type != TokenType.RightBrace && !CurrentToken.IsEOF)
    {
        ParseStatement();
    }
    Expect(TokenType.RightBrace);
    
    // Add implicit return undefined if needed
    if (!EndsWithReturn())
    {
        EmitOp(OpCode.Undefined);
        EmitOp(OpCode.Return);
    }
    
    // Restore parent context
    currentFunction = parentFunc;
    
    // Add child function to parent's pool and emit closure creation
    int funcIndex = parentFunc.AddChildFunction(childFunc);
    parentFunc.ConstPool.AddFunction(funcIndex);
    EmitOp(OpCode.FClosure);
    EmitU32((uint)funcIndex);
    
    // For declarations, store in variable
    if (funcType == JSParseFunctionType.Statement)
    {
        EmitOp(OpCode.PutVar);
        EmitAtom(funcName);
    }
}
```

### ParseParameterList

Handles JavaScript's rich parameter syntax:

```csharp
private void ParseParameterList(ref bool hasSimpleParameterList)
{
    bool hasRest = false;
    
    do
    {
        // Rest parameter: ...args
        if (Match(TokenType.Ellipsis))
        {
            hasSimpleParameterList = false;
            hasRest = true;
            
            if (CurrentToken.Type != TokenType.Identifier)
                throw new JSSyntaxError("Rest parameter must be an identifier");
            
            var restName = atoms.Intern(CurrentToken.StringValue);
            currentFunction.AddArg(restName, isRest: true);
            Advance();
            
            // Rest must be last
            if (CurrentToken.Type == TokenType.Comma)
                throw new JSSyntaxError("Rest parameter must be last");
            break;
        }
        
        // Destructuring parameter: {a, b} or [x, y]
        if (CurrentToken.Type == TokenType.LeftBrace || 
            CurrentToken.Type == TokenType.LeftBracket)
        {
            hasSimpleParameterList = false;
            ParseDestructuringParameter();
        }
        // Simple identifier
        else if (CurrentToken.Type == TokenType.Identifier)
        {
            var paramName = atoms.Intern(CurrentToken.StringValue);
            Advance();
            
            // Default value: a = 10
            if (Match(TokenType.Assign))
            {
                hasSimpleParameterList = false;
                currentFunction.AddArg(paramName, hasDefault: true);
                ParseAssignmentExpression();  // Parse default value
            }
            else
            {
                currentFunction.AddArg(paramName);
                currentFunction.DefinedArgCount++;
            }
        }
        else
        {
            throw new JSSyntaxError($"Unexpected token in parameter list: {CurrentToken.Type}");
        }
    }
    while (Match(TokenType.Comma) && !hasRest);
}
```

### ConfigureFunctionByType

Sets up function properties based on its syntactic context:

```csharp
private void ConfigureFunctionByType(JSFunctionDef func, JSParseFunctionType funcType, JSFunctionKind funcKind)
{
    // Regular functions and methods have a prototype property
    func.HasPrototype = funcType == JSParseFunctionType.Statement ||
                        funcType == JSParseFunctionType.Expression;
    
    // Arrow functions don't have their own this/arguments
    func.HasThisBinding = funcType != JSParseFunctionType.Arrow;
    func.HasArgumentsBinding = funcType != JSParseFunctionType.Arrow;
    
    // new.target is available in functions (not arrows)
    func.NewTargetAllowed = funcType != JSParseFunctionType.Arrow;
    
    // super.x is allowed in methods
    func.SuperAllowed = funcType == JSParseFunctionType.Method ||
                        funcType == JSParseFunctionType.DerivedClassConstructor;
    
    // super() is only allowed in derived class constructors
    func.SuperCallAllowed = funcType == JSParseFunctionType.DerivedClassConstructor;
    
    // Mark as being inside function body
    func.InFunctionBody = true;
    func.ArgumentsAllowed = funcType != JSParseFunctionType.Arrow;
}
```

## How Functions Relate to JavaScript Semantics

### Hoisting

Function declarations are hoisted to the top of their scope:

```javascript
foo();  // Works! Function is hoisted

function foo() {
    console.log("Hello");
}
```

This is why we use `JSParseFunctionType.Statement` and handle function storage specially - declarations need to be available before their textual position.

### The Arguments Object

Every non-arrow function has an implicit `arguments` object:

```javascript
function sum() {
    let total = 0;
    for (let i = 0; i < arguments.length; i++) {
        total += arguments[i];
    }
    return total;
}

sum(1, 2, 3, 4);  // 10
```

The `HasArgumentsBinding` flag controls whether this binding is created.

### this Binding

Functions have different `this` behavior:

```javascript
const obj = {
    name: "Object",
    regular: function() {
        return this.name;      // "Object" - this refers to obj
    },
    arrow: () => {
        return this.name;      // undefined - arrow inherits outer this
    }
};
```

The `HasThisBinding` flag distinguishes these cases.

### Generator Functions

Generators use `yield` to pause and resume:

```javascript
function* countdown(n) {
    while (n > 0) {
        yield n;
        n--;
    }
}

const iter = countdown(3);
iter.next();  // { value: 3, done: false }
iter.next();  // { value: 2, done: false }
iter.next();  // { value: 1, done: false }
iter.next();  // { value: undefined, done: true }
```

The `InitialYield` opcode sets up the generator's initial suspended state.

### Simple vs Complex Parameter Lists

ES6 introduced complex parameters that affect "use strict" handling:

```javascript
// Simple parameter list - can have "use strict" directive
function simple(a, b) {
    "use strict";  // OK
}

// Complex parameter list - cannot use "use strict"
function complex(a = 1, b) {
    "use strict";  // SyntaxError!
}
```

The `HasSimpleParameterList` flag tracks this for validation.

## Bytecode Generation

### Function Creation Flow

1. **Compile**: Parse function body, generating bytecode in child `JSFunctionDef`
2. **Store**: Add child function to parent's constant pool
3. **Create**: Emit `FClosure` opcode at runtime to instantiate

```
; Creating a closure at runtime
FClosure <func_index>  ; Creates function object from compiled definition
PutVar "myFunc"        ; Store in variable (for declarations)
```

### Nested Functions and Closures

Each function maintains a parent link for closure resolution:

```javascript
function outer(x) {
    function inner(y) {
        return x + y;  // Accesses x from outer scope
    }
    return inner;
}
```

The parser creates two `JSFunctionDef` structures:
- `outer`: Parent is null (or global)
- `inner`: Parent is `outer`, references `x` from enclosing scope

## Current Limitations

This step implements function **declarations**. The following are deferred:

- **Function Expressions**: `var f = function() {}` - requires primary expression parsing
- **Arrow Functions**: `x => x + 1` - requires parenthesized expression lookahead
- **Yield Expressions**: `yield value` - requires expression-level `yield` parsing
- **Await Expressions**: `await promise` - requires expression-level `await` parsing
- **Async Statement Handling**: `async function` at statement level

These features will be added in subsequent steps as we complete the expression parser.

## Files Modified

- `src/QuickJS.Core/Parser.cs` - Added function parsing methods (~500 lines)
- `src/QuickJS.Core/JSFunctionDef.cs` - Added constructor and new properties
- `src/QuickJS.Core/ConstantPool.cs` - Added `AddFunction()` method
- `src/QuickJS.Core/TokenType.cs` - Added `Async` token
- `src/QuickJS.Core/Lexer.cs` - Added `async` keyword recognition

## Tests Added

45 new tests in `FunctionParserTests.cs`:
- 7 function declaration tests
- 3 generator function tests (1 active, 2 skipped pending yield expressions)
- 3 async function tests (skipped pending async integration)
- 4 function expression tests (skipped pending expression parsing)
- 7 arrow function tests (skipped pending arrow parsing)
- 4 default parameter tests
- 3 rest parameter tests
- 3 return statement tests
- 8 complex scenario tests (7 active, 1 skipped)
- 1 "use strict" test
- 3 function-in-expression tests (skipped)

**Active tests: 25 | Skipped: 20 (for future phases)**

## Running the Tests

```bash
cd csharp
dotnet test --filter "FunctionParserTests"
```

## Summary

This step establishes the foundation for JavaScript function parsing:

1. **Function declarations** with parameters and body
2. **Generator syntax** recognition (`function*`)
3. **Parameter variations**: rest, defaults, destructuring
4. **Nested functions** with parent-child linking
5. **Bytecode emission** via `FClosure` opcode

The implementation follows QuickJS's approach of creating separate `JSFunctionDef` structures for each function, enabling proper scope handling and closure support when we build the runtime later.
