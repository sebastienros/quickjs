# Step 4.2: Statement Parser

## Overview

This step extends the parser from Step 4.1 to handle JavaScript statements. While expressions compute values, statements perform actions and control program flow. The statement parser transforms JavaScript control flow constructs into bytecode jump instructions, implementing the same one-pass compilation approach used by the original QuickJS.

## JavaScript Statement Categories

JavaScript statements fall into several categories, each requiring different bytecode patterns:

### Declaration Statements

```javascript
var x = 1;     // Variable declaration (function-scoped, hoisted)
let y = 2;     // Block-scoped variable (ES6+)
const z = 3;   // Block-scoped constant (ES6+)
```

### Control Flow Statements

```javascript
if (condition) { ... } else { ... }  // Conditional execution
switch (value) { case 1: ... }       // Multi-way branch
```

### Loop Statements

```javascript
while (condition) { ... }           // Pre-test loop
do { ... } while (condition);       // Post-test loop
for (init; test; update) { ... }    // C-style loop
for (let x of iterable) { ... }     // Iterator loop (ES6+)
for (let key in object) { ... }     // Property enumeration
```

### Jump Statements

```javascript
break;           // Exit loop/switch
continue;        // Skip to next iteration
return value;    // Exit function with value
throw error;     // Raise exception
```

### Exception Handling

```javascript
try { ... } catch (e) { ... } finally { ... }
```

## Implementation Details

### Label Management for Control Flow

JavaScript's control flow requires forward and backward jumps. The parser manages this with labels:

```csharp
// Allocate a label slot
int labelEnd = NewLabel();

// Parse code that jumps to the label
EmitGoto(OpCode.IfFalse, labelEnd);

// Later, mark where the label points
EmitLabel(labelEnd);
```

Labels are resolved during bytecode finalization when actual offsets are known.

### Break/Continue Context Stack

Nested loops require tracking which loop a `break` or `continue` targets:

```csharp
// Stack of (breakLabel, continueLabel) pairs
private readonly Stack<(int breakLabel, int continueLabel)> _breakStack = new();

private void PushBreakContext(int breakLabel, int continueLabel)
{
    _breakStack.Push((breakLabel, continueLabel));
}

private void PopBreakContext()
{
    _breakStack.Pop();
}
```

When parsing `break` or `continue`:
- **Unlabeled**: Use the innermost context
- **Labeled**: Search the context stack for matching label

### Variable Declaration Compilation

JavaScript's variable declarations have complex semantics:

| Keyword | Scope | Hoisting | TDZ | Reassignable |
|---------|-------|----------|-----|--------------|
| `var`   | Function | Declaration | No | Yes |
| `let`   | Block | No | Yes | Yes |
| `const` | Block | No | Yes | No |

**TDZ (Temporal Dead Zone)**: Let/const variables exist but are unusable before initialization.

```csharp
private void ParseVariableDeclarationList(JSVarKind kind, bool isLexical, bool isConst)
{
    do
    {
        var name = (string)_currentToken.Value!;
        var atom = _atoms.GetOrCreateAtom(name);
        NextToken();

        // Add variable to current scope
        int varIdx = _currentFunction.AddVar(atom, kind, isConst, isLexical);

        if (Match(TokenType.Assign))
        {
            ParseAssignExpression();
            // ScopePutVarInit marks the variable as initialized (exits TDZ)
            EmitOp(isLexical ? OpCode.ScopePutVarInit : OpCode.ScopePutVar);
            EmitAtom(atom);
            EmitU16((ushort)_currentFunction.ScopeLevel);
        }
        else if (isConst)
        {
            throw new JSSyntaxError("Missing initializer for const variable");
        }
    }
    while (Match(TokenType.Comma));
}
```

### If Statement Compilation

```javascript
if (condition) {
    thenBranch;
} else {
    elseBranch;
}
```

Compiles to:
```
[condition expression]
IfFalse -> labelElse
[thenBranch]
Goto -> labelEnd
labelElse:
[elseBranch]
labelEnd:
```

```csharp
public void ParseIfStatement()
{
    Expect(TokenType.If);
    Expect(TokenType.LeftParen);
    ParseExpression();            // Condition
    Expect(TokenType.RightParen);

    int labelElse = NewLabel();
    EmitGoto(OpCode.IfFalse, labelElse);

    ParseStatement();             // Then branch

    if (Match(TokenType.Else))
    {
        int labelEnd = NewLabel();
        EmitGoto(OpCode.Goto, labelEnd);
        EmitLabel(labelElse);
        ParseStatement();         // Else branch
        EmitLabel(labelEnd);
    }
    else
    {
        EmitLabel(labelElse);
    }
}
```

### While Loop Compilation

```javascript
while (condition) {
    body;
}
```

Compiles to:
```
labelContinue:
[condition]
IfFalse -> labelBreak
[body]
Goto -> labelContinue
labelBreak:
```

```csharp
public void ParseWhileStatement()
{
    Expect(TokenType.While);

    int labelContinue = NewLabel();
    int labelBreak = NewLabel();

    PushBreakContext(labelBreak, labelContinue);

    EmitLabel(labelContinue);
    Expect(TokenType.LeftParen);
    ParseExpression();
    Expect(TokenType.RightParen);
    EmitGoto(OpCode.IfFalse, labelBreak);

    ParseStatement();
    EmitGoto(OpCode.Goto, labelContinue);
    EmitLabel(labelBreak);

    PopBreakContext();
}
```

### For Loop Compilation

The C-style for loop has three optional parts:

```javascript
for (init; test; update) {
    body;
}
```

The compilation order differs from source order for efficiency:

```
[init]
labelTest:
[test]
IfFalse -> labelBreak
Goto -> labelBody
labelContinue:
[update]
Goto -> labelTest
labelBody:
[body]
Goto -> labelContinue
labelBreak:
```

This arrangement means `continue` correctly executes the update expression.

### For-In/For-Of Loops

These loops use JavaScript's iterator protocol:

```javascript
for (let x of iterable) { ... }
```

Compiles to iterator operations:
```
[iterable]
GetIterator
labelContinue:
IteratorNext
IfTrue -> labelBreak   ; done?
[store loop variable]
[body]
Goto -> labelContinue
labelBreak:
IteratorClose
```

### Switch Statement Compilation

Switch statements compile to a jump table pattern:

```javascript
switch (value) {
    case 1: a(); break;
    case 2: b(); break;
    default: c();
}
```

Compiles to:
```
[value]
Dup; [case 1 value]; StrictEq; IfTrue -> label1
Dup; [case 2 value]; StrictEq; IfTrue -> label2
Drop; Goto -> labelDefault
label1: [case 1 body]; Goto -> labelBreak
label2: [case 2 body]; Goto -> labelBreak
labelDefault: [default body]
labelBreak:
```

### Try-Catch-Finally

Exception handling uses special opcodes:

```javascript
try {
    mayThrow();
} catch (e) {
    handle(e);
} finally {
    cleanup();
}
```

```
Gosub -> labelFinally      ; Set up finally handler
Catch -> labelCatch        ; Set up catch handler
[try body]
Drop                       ; Remove catch handler
Goto -> labelAfterCatch

labelCatch:
[store exception in 'e']
[catch body]

labelAfterCatch:
Ret                        ; Return from gosub to finally

labelFinally:
[finally body]
Ret                        ; Continue after try statement
```

### Assignment Expression Enhancement

Step 4.2 also completes assignment expression parsing that was stubbed in Step 4.1:

```csharp
public void ParseAssignExpression(ParseFlags flags = ParseFlags.None)
{
    ParseConditionalExpression(flags);

    // Check for assignment operators
    switch (_currentToken.Type)
    {
        case TokenType.Assign:           // =
        case TokenType.PlusAssign:       // +=
        case TokenType.MinusAssign:      // -=
        // ... other compound assignments
    }

    if (isAssignment)
    {
        NextToken();
        
        if (isCompound)
        {
            EmitOp(OpCode.Dup);
            ParseAssignExpression(flags);  // Right-associative
            EmitOp(compoundOp);
        }
        else
        {
            ParseAssignExpression(flags);
        }
        
        EmitOp(OpCode.PutRefValue);
    }
}
```

### New Expression Parsing

The `new` operator creates object instances:

```javascript
new Constructor(arg1, arg2)
```

```csharp
private void ParseNewExpression()
{
    Expect(TokenType.New);

    // Handle 'new.target' meta-property
    if (Check(TokenType.Dot))
    {
        // ... handle new.target
    }

    ParseMemberExpression();  // Get constructor

    int argc = 0;
    if (Check(TokenType.LeftParen))
    {
        // Parse arguments
        argc = ParseArgumentList();
    }

    EmitOp(OpCode.CallConstructor);
    EmitU16((ushort)argc);
}
```

## Automatic Semicolon Insertion (ASI)

JavaScript allows omitting semicolons in certain cases:

```javascript
return    // ASI inserts semicolon here
42        // This is a separate statement!
```

The parser implements ASI:

```csharp
private void ParseOptionalSemicolon()
{
    if (Match(TokenType.Semicolon))
        return;

    // ASI: semicolon inserted if:
    // 1. Newline before current token
    // 2. Current token is '}'
    // 3. Current token is EOF
    if (_currentToken.HasLineTerminatorBefore ||
        Check(TokenType.RightBrace) ||
        Check(TokenType.EOF))
    {
        return; // Semicolon automatically inserted
    }

    throw new JSSyntaxError("Expected semicolon");
}
```

## Connection to JavaScript Semantics

### Hoisting

The parser handles JavaScript's hoisting by:
1. Scanning the function for all `var` declarations during function entry
2. Initializing them to `undefined` before executing any statements
3. Assignment happens at the declaration site

### Block Scoping

`let` and `const` create new scope entries:

```csharp
public void ParseBlockStatement()
{
    _currentFunction.PushScope();  // Enter block scope
    
    Expect(TokenType.LeftBrace);
    while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
    {
        ParseStatement();
    }
    Expect(TokenType.RightBrace);
    
    _currentFunction.PopScope();   // Exit block scope
}
```

### Statement vs Expression

JavaScript distinguishes statements (which have side effects) from expressions (which produce values):

```javascript
// Expression: produces a value
x + 1

// Expression statement: expression evaluated for side effects, value discarded
x + 1;

// Statement: controls program flow
if (x) { y; }
```

The parser handles expression statements by parsing the expression then emitting `Drop`:

```csharp
public void ParseExpressionStatement()
{
    ParseExpression();
    EmitOp(OpCode.Drop);  // Discard the expression result
    ExpectSemicolon();
}
```

## Files Changed

- `src/QuickJS.Core/Parser.cs` - Extended with statement parsing (~700 lines)
- `tests/QuickJS.Tests/StatementParserTests.cs` - 58 tests for all statement types

## Test Coverage

```
StatementParserTests:
  - Variable declarations (var, let, const)
  - If/else statements
  - While and do-while loops
  - For loops (standard, for-in, for-of)
  - Switch statements with cases
  - Try/catch/finally
  - Return, throw, break, continue
  - Labeled statements
  - Block statements
  - Complex nested structures
  
Total: 58 tests (116 runs across 2 frameworks)
```

## Next Steps

Step 4.3 will implement function parsing:
- Function declarations and expressions
- Arrow functions
- Parameters with default values
- Rest parameters
- Generator functions
- Async functions
