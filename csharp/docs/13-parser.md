# Step 4.1: Expression Parser

This step implements the expression parser component that directly emits bytecode during parsing. This follows QuickJS's single-pass compilation model where bytecode is generated as the source code is parsed.

## Why Expression Parsing Matters

JavaScript is an expression-oriented language. Nearly everything can be an expression:
- `1 + 2` - arithmetic
- `x && y` - logical
- `a ? b : c` - conditional
- `obj.prop` - member access
- `fn(x, y)` - function calls

Understanding how expressions compile helps you understand:
1. How operator precedence works at the bytecode level
2. Why JavaScript evaluates expressions left-to-right (mostly)
3. How short-circuit evaluation is implemented

## Parser Architecture

### Single-Pass Compilation

Unlike many compilers that build an Abstract Syntax Tree (AST) first, QuickJS compiles directly to bytecode:

```
Traditional:  Source → Tokens → AST → Bytecode
QuickJS:      Source → Tokens → Bytecode (directly)
```

This is more memory-efficient for JavaScript's typical use case: parse once, execute immediately.

### Recursive Descent Parsing

The parser uses recursive descent, where each grammar rule becomes a parsing method:

```csharp
public void ParseExpression()
{
    ParseAssignExpression(ParseFlags.InAccepted);
}

public void ParseAssignExpression(ParseFlags flags)
{
    ParseConditionalExpression(flags);
    // Handle =, +=, -=, etc.
}

public void ParseConditionalExpression(ParseFlags flags)
{
    ParseCoalesceExpression(flags);
    // Handle ? :
}
```

## Operator Precedence

JavaScript has well-defined operator precedence. Our parser implements this through a chain of parsing functions, from lowest to highest precedence:

| Precedence | Operators | Parser Method |
|------------|-----------|---------------|
| Lowest | `=, +=, -=` | `ParseAssignExpression` |
| | `? :` | `ParseConditionalExpression` |
| | `??` | `ParseCoalesceExpression` |
| | `||` | `ParseLogicalOrExpression` |
| | `&&` | `ParseLogicalAndExpression` |
| | `|` | `ParseBitwiseOrExpression` |
| | `^` | `ParseBitwiseXorExpression` |
| | `&` | `ParseBitwiseAndExpression` |
| | `==, !=, ===, !==` | `ParseEqualityExpression` |
| | `<, >, <=, >=, in, instanceof` | `ParseRelationalExpression` |
| | `<<, >>, >>>` | `ParseShiftExpression` |
| | `+, -` | `ParseAdditiveExpression` |
| | `*, /, %` | `ParseMultiplicativeExpression` |
| | `**` | `ParseExponentiationExpression` |
| | `!, ~, +, -, typeof, void, delete` | `ParseUnaryExpression` |
| Highest | `++, --, (), [], .` | `ParsePostfixExpression` |

### How Precedence Works

Consider `1 + 2 * 3`:

1. `ParseAdditiveExpression` calls `ParseMultiplicativeExpression`
2. `ParseMultiplicativeExpression` parses `1`
3. Returns to `ParseAdditiveExpression`, sees `+`
4. Calls `ParseMultiplicativeExpression` for right side
5. `ParseMultiplicativeExpression` parses `2 * 3`
6. Emits `Mul` for `2 * 3`
7. Returns, then emits `Add`

Result: `2 * 3` is evaluated first (Mul before Add in bytecode).

## Short-Circuit Evaluation

Logical operators (`&&`, `||`, `??`) use short-circuit evaluation:

```javascript
a && b    // If a is falsy, don't evaluate b
a || b    // If a is truthy, don't evaluate b
a ?? b    // If a is not null/undefined, don't evaluate b
```

This requires conditional jumps:

```csharp
public void ParseLogicalAndExpression(ParseFlags flags)
{
    ParseBitwiseOrExpression(flags);

    while (Match(TokenType.LogicalAnd))
    {
        int labelEnd = NewLabel();
        EmitOp(OpCode.Dup);           // Duplicate value for testing
        EmitGoto(OpCode.IfFalse, labelEnd);  // Jump if false
        EmitOp(OpCode.Drop);          // Drop the duplicate
        ParseBitwiseOrExpression(flags);
        EmitLabel(labelEnd);
    }
}
```

Bytecode for `a && b`:
```
ScopeGetVar "a"
Dup                    # Stack: a, a
IfFalse label_end      # If falsy, skip to end
Drop                   # Remove duplicate
ScopeGetVar "b"        # Stack: b
label_end:             # Stack: result (a if falsy, b if truthy)
```

## Conditional Expression

The ternary operator `a ? b : c` compiles to:

```csharp
public void ParseConditionalExpression(ParseFlags flags)
{
    ParseCoalesceExpression(flags);

    if (Match(TokenType.Question))
    {
        int labelElse = NewLabel();
        int labelEnd = NewLabel();

        EmitGoto(OpCode.IfFalse, labelElse);
        ParseAssignExpression(flags);  // True branch
        EmitGoto(OpCode.Goto, labelEnd);
        EmitLabel(labelElse);
        Expect(TokenType.Colon);
        ParseAssignExpression(flags);  // False branch
        EmitLabel(labelEnd);
    }
}
```

## Unary Operators and Exponentiation

JavaScript has a special rule: unary operators before `**` require parentheses:

```javascript
-2 ** 2    // SyntaxError!
(-2) ** 2  // OK: 4
-(2 ** 2)  // OK: -4
```

We detect this by tracking if we started with a unary operator:

```csharp
public void ParseExponentiationExpression(ParseFlags flags)
{
    bool hasUnaryPrefix = IsUnaryOperator(_currentToken.Type);
    
    ParseUnaryExpression(flags);

    if (Match(TokenType.Power))
    {
        if (hasUnaryPrefix)
        {
            throw new JSSyntaxError(
                "Unary operator before ** requires parentheses",
                _currentToken.Start);
        }
        ParseExponentiationExpression(flags);  // Right-associative
        EmitOp(OpCode.Pow);
    }
}
```

## Member Access and Method Calls

Property access (`obj.prop`) and method calls (`obj.method()`) have different bytecode:

```csharp
if (Match(TokenType.Dot))
{
    var name = (string)_currentToken.Value!;
    NextToken();
    var atom = _atoms.GetOrCreateAtom(name);

    // Check if this is a method call: obj.method()
    if (Check(TokenType.LeftParen))
    {
        NextToken();
        int argCount = ParseArguments();
        EmitOp(OpCode.CallMethod);  // Optimized for method calls
        EmitAtom(atom);
        EmitU16((ushort)argCount);
    }
    else
    {
        EmitOp(OpCode.GetField);
        EmitAtom(atom);
    }
}
```

The `CallMethod` opcode is more efficient than `GetField` + `Call` because it:
1. Avoids creating an intermediate closure
2. Correctly binds `this` to the object

## Array and Object Literals

### Array Literals

```javascript
[1, 2, 3]
```

Compiles to:
1. Push each element onto the stack
2. Emit `ArrayFrom` with element count

```csharp
private void ParseArrayLiteral()
{
    int elementCount = 0;
    
    while (!Check(TokenType.RightBracket))
    {
        if (Check(TokenType.Comma))
        {
            // Elision (hole) - push undefined
            EmitOp(OpCode.Undefined);
            elementCount++;
            NextToken();
            continue;
        }
        
        ParseAssignExpression();
        elementCount++;
        
        if (!Match(TokenType.Comma)) break;
    }

    EmitOp(OpCode.ArrayFrom);
    EmitU16((ushort)elementCount);
}
```

### Object Literals

```javascript
{x: 1, y: 2}
```

Compiles to:
1. Create empty object
2. Define each property

## Parse Flags

The parser uses flags to control context-sensitive parsing:

```csharp
[Flags]
public enum ParseFlags
{
    None = 0,
    PostfixCall = 1 << 0,    // Allow function calls
    PowAllowed = 1 << 1,     // ** is allowed
    PowForbidden = 1 << 2,   // ** after unary is an error
    InAccepted = 1 << 3,     // Allow 'in' operator (not in for-in)
}
```

For example, in `for (var x in obj)`, the `in` is not an operator, so we parse with `InAccepted` cleared.

## Bytecode Emission

The parser emits bytecode through helper methods:

```csharp
private void EmitOp(OpCode op)
{
    _currentFunction.ByteCode.EmitOp(op);
}

private int NewLabel()
{
    return _currentFunction.ByteCode.DefineLabel();
}

private void EmitLabel(int label)
{
    _currentFunction.ByteCode.MarkLabel(label);
}

private void EmitGoto(OpCode op, int label)
{
    _currentFunction.ByteCode.EmitJump(op, label);
}
```

## Example: Complete Expression Compilation

For `1 + 2 * 3 > 5 ? "yes" : "no"`:

```
PushI32 1
PushI32 2  
PushI32 3
Mul                  # 2 * 3 = 6
Add                  # 1 + 6 = 7
PushI32 5
Gt                   # 7 > 5 = true
IfFalse label_else
PushConst 0          # "yes"
Goto label_end
label_else:
PushConst 1          # "no"
label_end:
```

## Testing

The parser tests verify:
- Token handling (NextToken, Check, Match, Expect)
- Literal parsing (numbers, strings, booleans, null)
- Binary operators (arithmetic, bitwise, comparison, equality)
- Unary operators (+, -, !, ~, typeof, void, delete)
- Logical operators with short-circuit evaluation
- Conditional expressions
- Array and object literals
- Member access and method calls
- Operator precedence
- Error handling

## What's Next

The expression parser is the foundation. Upcoming steps will add:
- Statement parsing (if, while, for, etc.)
- Variable declarations (var, let, const)
- Function declarations and expressions
- Class syntax
- Module import/export

## Files Changed

- `src/QuickJS.Core/Parser.cs` - New expression parser (~900 lines)
- `tests/QuickJS.Tests/ParserTests.cs` - 63 new tests

## References

- [ECMAScript Expressions](https://tc39.es/ecma262/#sec-ecmascript-language-expressions)
- [Operator Precedence](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Operators/Operator_precedence)
- QuickJS `quickjs.c`: `js_parse_expr_binary`, `js_parse_postfix_expr`, `js_parse_unary`
