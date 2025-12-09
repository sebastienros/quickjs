// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace QuickJS;

/// <summary>
/// Flags that control expression parsing behavior.
/// </summary>
[Flags]
public enum ParseFlags
{
    /// <summary>No special flags.</summary>
    None = 0,

    /// <summary>Allow function calls in postfix expressions.</summary>
    PostfixCall = 1 << 0,

    /// <summary>Allow the exponentiation operator (**).</summary>
    PowAllowed = 1 << 1,

    /// <summary>Forbid the exponentiation operator (**).</summary>
    PowForbidden = 1 << 2,

    /// <summary>Allow the 'in' operator (used in for-in loops).</summary>
    InAccepted = 1 << 3,
}

/// <summary>
/// JavaScript parser that compiles source code to bytecode.
/// </summary>
/// <remarks>
/// <para>
/// The parser is a recursive descent parser that directly emits bytecode
/// during parsing, following QuickJS's single-pass compilation model.
/// </para>
/// <para>
/// Unlike traditional parsers that build an AST first, this parser
/// generates bytecode as it parses, which is more memory-efficient
/// for JavaScript's typical use case of parsing and immediately executing.
/// </para>
/// </remarks>
public sealed class Parser
{
    private readonly Lexer _lexer;
    private readonly AtomTable _atoms;
    private Token _currentToken;
    private JSFunctionDef _currentFunction;

    /// <summary>
    /// Creates a new parser.
    /// </summary>
    /// <param name="source">The JavaScript source code.</param>
    /// <param name="fileName">The file name for error reporting.</param>
    /// <param name="atoms">The atom table for string interning.</param>
    public Parser(string source, string fileName, AtomTable atoms)
    {
        _lexer = new Lexer(source, fileName);
        _atoms = atoms ?? throw new ArgumentNullException(nameof(atoms));
        _currentToken = _lexer.NextToken();
        _currentFunction = new JSFunctionDef();
    }

    /// <summary>
    /// Gets the current token.
    /// </summary>
    public Token CurrentToken => _currentToken;

    /// <summary>
    /// Gets the current function definition being compiled.
    /// </summary>
    public JSFunctionDef CurrentFunction => _currentFunction;

    /// <summary>
    /// Gets the atom table.
    /// </summary>
    public AtomTable Atoms => _atoms;

    #region Token Handling

    /// <summary>
    /// Advances to the next token.
    /// </summary>
    public void NextToken()
    {
        _currentToken = _lexer.NextToken();
    }

    /// <summary>
    /// Returns true if the current token matches the expected type.
    /// </summary>
    public bool Check(TokenType type) => _currentToken.Type == type;

    /// <summary>
    /// Consumes the current token if it matches, otherwise throws.
    /// </summary>
    public void Expect(TokenType type)
    {
        if (!Check(type))
        {
            throw new JSSyntaxError(
                $"Expected {type} but got {_currentToken.Type}",
                _currentToken.Start);
        }
        NextToken();
    }

    /// <summary>
    /// Consumes the current token if it matches the expected type.
    /// </summary>
    /// <returns>True if the token matched and was consumed.</returns>
    public bool Match(TokenType type)
    {
        if (Check(type))
        {
            NextToken();
            return true;
        }
        return false;
    }

    #endregion

    #region Bytecode Emission

    private void EmitOp(OpCode op)
    {
        _currentFunction.ByteCode.EmitOp(op);
    }

    private void EmitU8(byte value)
    {
        _currentFunction.ByteCode.EmitU8(value);
    }

    private void EmitU16(ushort value)
    {
        _currentFunction.ByteCode.EmitU16(value);
    }

    private void EmitU32(uint value)
    {
        _currentFunction.ByteCode.EmitU32(value);
    }

    private void EmitI32(int value)
    {
        _currentFunction.ByteCode.EmitI32(value);
    }

    private void EmitAtom(JSAtom atom)
    {
        _currentFunction.ByteCode.EmitU32((uint)atom.Value);
    }

    private int NewLabel()
    {
        // Use ByteCodeBuffer's label system for consistency
        return _currentFunction.ByteCode.DefineLabel();
    }

    private void EmitLabel(int label)
    {
        // Mark the label at the current position in bytecode
        _currentFunction.ByteCode.MarkLabel(label);
    }

    private void EmitGoto(OpCode op, int label)
    {
        // Use ByteCodeBuffer's jump emission which handles label references
        _currentFunction.ByteCode.EmitJump(op, label);
    }

    #endregion

    #region Expression Parsing

    /// <summary>
    /// Parses an expression.
    /// </summary>
    public void ParseExpression()
    {
        ParseAssignExpression(ParseFlags.InAccepted);
    }

    /// <summary>
    /// Parses an assignment expression.
    /// In JavaScript, assignment is right-associative: a = b = c means a = (b = c)
    /// Assignment operators: = += -= *= /= %= **= &lt;&lt;= &gt;&gt;= &gt;&gt;&gt;= &amp;= ^= |= ??=
    /// </summary>
    public void ParseAssignExpression(ParseFlags flags = ParseFlags.None)
    {
        ParseConditionalExpression(flags);

        // Check for assignment operators
        OpCode compoundOp = OpCode.Nop;
        bool isAssignment = false;
        bool isCompound = false;

        switch (_currentToken.Type)
        {
            case TokenType.Assign:
                isAssignment = true;
                break;
            case TokenType.PlusAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Add;
                break;
            case TokenType.MinusAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Sub;
                break;
            case TokenType.AsteriskAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Mul;
                break;
            case TokenType.SlashAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Div;
                break;
            case TokenType.PercentAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Mod;
                break;
            case TokenType.PowerAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Pow;
                break;
            case TokenType.LeftShiftAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Shl;
                break;
            case TokenType.RightShiftAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Sar;
                break;
            case TokenType.UnsignedRightShiftAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Shr;
                break;
            case TokenType.AmpersandAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.And;
                break;
            case TokenType.CaretAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Xor;
                break;
            case TokenType.PipeAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Or;
                break;
            case TokenType.NullishCoalescingAssign:
                isAssignment = true;
                isCompound = true;
                compoundOp = OpCode.Nop; // Special handling needed
                break;
        }

        if (isAssignment)
        {
            NextToken(); // consume the assignment operator

            if (isCompound)
            {
                // For compound assignment like +=, we need to:
                // 1. Duplicate the reference (for property access)
                // 2. Get the current value
                // 3. Parse the right-hand side
                // 4. Apply the operation
                // 5. Store back

                // For now, emit a simple compound assignment pattern
                // In a full implementation, this would need to handle
                // property access vs simple variable assignment
                EmitOp(OpCode.Dup);
                ParseAssignExpression(flags); // Right-associative
                EmitOp(compoundOp);
            }
            else
            {
                // Simple assignment: parse RHS (right-associative)
                ParseAssignExpression(flags);
            }

            // The actual store operation depends on what the LHS was
            // For now, emit a generic put reference value operation
            // In a full implementation, we'd track whether LHS was a variable,
            // property access, etc. and emit PutVar, PutLoc, PutField accordingly
            EmitOp(OpCode.PutRefValue);
        }
    }

    /// <summary>
    /// Parses a conditional (ternary) expression: expr ? expr : expr
    /// </summary>
    public void ParseConditionalExpression(ParseFlags flags)
    {
        ParseCoalesceExpression(flags);

        if (Match(TokenType.Question))
        {
            int labelFalse = NewLabel();
            int labelEnd = NewLabel();

            // If the condition is false, jump to the false branch
            EmitGoto(OpCode.IfFalse, labelFalse);

            // True branch
            ParseAssignExpression();
            Expect(TokenType.Colon);

            // Jump past the false branch
            EmitGoto(OpCode.Goto, labelEnd);

            // False branch
            EmitLabel(labelFalse);
            ParseAssignExpression(flags);

            EmitLabel(labelEnd);
        }
    }

    /// <summary>
    /// Parses a nullish coalescing expression: expr ?? expr
    /// </summary>
    public void ParseCoalesceExpression(ParseFlags flags)
    {
        ParseLogicalOrExpression(flags);

        if (Check(TokenType.NullishCoalescing))
        {
            int labelEnd = NewLabel();

            while (Match(TokenType.NullishCoalescing))
            {
                // Duplicate the value
                EmitOp(OpCode.Dup);
                // Check if it's undefined or null
                EmitOp(OpCode.IsUndefinedOrNull);
                // If not undefined/null, skip to end
                EmitGoto(OpCode.IfFalse, labelEnd);
                // Drop the duplicated value
                EmitOp(OpCode.Drop);
                // Parse the right side
                ParseLogicalOrExpression(flags);
            }

            EmitLabel(labelEnd);
        }
    }

    /// <summary>
    /// Parses a logical OR expression: expr || expr
    /// </summary>
    public void ParseLogicalOrExpression(ParseFlags flags)
    {
        ParseLogicalAndExpression(flags);

        if (Check(TokenType.LogicalOr))
        {
            int labelEnd = NewLabel();

            while (Match(TokenType.LogicalOr))
            {
                EmitOp(OpCode.Dup);
                EmitGoto(OpCode.IfTrue, labelEnd);
                EmitOp(OpCode.Drop);
                ParseLogicalAndExpression(flags);
            }

            EmitLabel(labelEnd);
        }
    }

    /// <summary>
    /// Parses a logical AND expression: expr &amp;&amp; expr
    /// </summary>
    public void ParseLogicalAndExpression(ParseFlags flags)
    {
        ParseBitwiseOrExpression(flags);

        if (Check(TokenType.LogicalAnd))
        {
            int labelEnd = NewLabel();

            while (Match(TokenType.LogicalAnd))
            {
                EmitOp(OpCode.Dup);
                EmitGoto(OpCode.IfFalse, labelEnd);
                EmitOp(OpCode.Drop);
                ParseBitwiseOrExpression(flags);
            }

            EmitLabel(labelEnd);
        }
    }

    /// <summary>
    /// Parses a bitwise OR expression: expr | expr
    /// </summary>
    public void ParseBitwiseOrExpression(ParseFlags flags)
    {
        ParseBitwiseXorExpression(flags);

        while (Match(TokenType.Pipe))
        {
            ParseBitwiseXorExpression(flags);
            EmitOp(OpCode.Or);
        }
    }

    /// <summary>
    /// Parses a bitwise XOR expression: expr ^ expr
    /// </summary>
    public void ParseBitwiseXorExpression(ParseFlags flags)
    {
        ParseBitwiseAndExpression(flags);

        while (Match(TokenType.Caret))
        {
            ParseBitwiseAndExpression(flags);
            EmitOp(OpCode.Xor);
        }
    }

    /// <summary>
    /// Parses a bitwise AND expression: expr &amp; expr
    /// </summary>
    public void ParseBitwiseAndExpression(ParseFlags flags)
    {
        ParseEqualityExpression(flags);

        while (Match(TokenType.Ampersand))
        {
            ParseEqualityExpression(flags);
            EmitOp(OpCode.And);
        }
    }

    /// <summary>
    /// Parses an equality expression: expr == expr, expr != expr, etc.
    /// </summary>
    public void ParseEqualityExpression(ParseFlags flags)
    {
        ParseRelationalExpression(flags);

        while (true)
        {
            OpCode? op = _currentToken.Type switch
            {
                TokenType.Equal => OpCode.Eq,
                TokenType.NotEqual => OpCode.Neq,
                TokenType.StrictEqual => OpCode.StrictEq,
                TokenType.StrictNotEqual => OpCode.StrictNeq,
                _ => null
            };

            if (op == null) break;

            NextToken();
            ParseRelationalExpression(flags);
            EmitOp(op.Value);
        }
    }

    /// <summary>
    /// Parses a relational expression: expr &lt; expr, expr &gt; expr, etc.
    /// </summary>
    public void ParseRelationalExpression(ParseFlags flags)
    {
        ParseShiftExpression(flags);

        while (true)
        {
            OpCode? op = _currentToken.Type switch
            {
                TokenType.LessThan => OpCode.Lt,
                TokenType.GreaterThan => OpCode.Gt,
                TokenType.LessThanOrEqual => OpCode.Lte,
                TokenType.GreaterThanOrEqual => OpCode.Gte,
                TokenType.InstanceOf => OpCode.InstanceOf,
                TokenType.In when (flags & ParseFlags.InAccepted) != 0 => OpCode.In,
                _ => null
            };

            if (op == null) break;

            NextToken();
            ParseShiftExpression(flags);
            EmitOp(op.Value);
        }
    }

    /// <summary>
    /// Parses a shift expression: expr &lt;&lt; expr, expr &gt;&gt; expr, etc.
    /// </summary>
    public void ParseShiftExpression(ParseFlags flags)
    {
        ParseAdditiveExpression(flags);

        while (true)
        {
            OpCode? op = _currentToken.Type switch
            {
                TokenType.LeftShift => OpCode.Shl,
                TokenType.RightShift => OpCode.Sar,
                TokenType.UnsignedRightShift => OpCode.Shr,
                _ => null
            };

            if (op == null) break;

            NextToken();
            ParseAdditiveExpression(flags);
            EmitOp(op.Value);
        }
    }

    /// <summary>
    /// Parses an additive expression: expr + expr, expr - expr
    /// </summary>
    public void ParseAdditiveExpression(ParseFlags flags)
    {
        ParseMultiplicativeExpression(flags);

        while (true)
        {
            OpCode? op = _currentToken.Type switch
            {
                TokenType.Plus => OpCode.Add,
                TokenType.Minus => OpCode.Sub,
                _ => null
            };

            if (op == null) break;

            NextToken();
            ParseMultiplicativeExpression(flags);
            EmitOp(op.Value);
        }
    }

    /// <summary>
    /// Parses a multiplicative expression: expr * expr, expr / expr, expr % expr
    /// </summary>
    public void ParseMultiplicativeExpression(ParseFlags flags)
    {
        ParseExponentiationExpression(flags);

        while (true)
        {
            OpCode? op = _currentToken.Type switch
            {
                TokenType.Asterisk => OpCode.Mul,
                TokenType.Slash => OpCode.Div,
                TokenType.Percent => OpCode.Mod,
                _ => null
            };

            if (op == null) break;

            NextToken();
            ParseExponentiationExpression(flags);
            EmitOp(op.Value);
        }
    }

    /// <summary>
    /// Parses an exponentiation expression: expr ** expr
    /// </summary>
    public void ParseExponentiationExpression(ParseFlags flags)
    {
        // Exponentiation is right-associative
        // Check for unary operators that forbid ** without parentheses
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
            ParseExponentiationExpression(flags | ParseFlags.PowAllowed);
            EmitOp(OpCode.Pow);
        }
    }
    
    private static bool IsUnaryOperator(TokenType type)
    {
        return type == TokenType.Plus ||
               type == TokenType.Minus ||
               type == TokenType.LogicalNot ||
               type == TokenType.Tilde ||
               type == TokenType.TypeOf ||
               type == TokenType.Void ||
               type == TokenType.Delete;
    }

    /// <summary>
    /// Parses a unary expression: !expr, -expr, +expr, typeof expr, new expr, etc.
    /// </summary>
    public void ParseUnaryExpression(ParseFlags flags)
    {
        switch (_currentToken.Type)
        {
            case TokenType.New:
                ParseNewExpression();
                break;

            case TokenType.Plus:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Plus);
                break;

            case TokenType.Minus:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Neg);
                break;

            case TokenType.LogicalNot:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.LNot);
                break;

            case TokenType.Tilde:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Not);
                break;

            case TokenType.TypeOf:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.TypeOf);
                break;

            case TokenType.Void:
                NextToken();
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Drop);
                EmitOp(OpCode.Undefined);
                break;

            case TokenType.Delete:
                NextToken();
                // TODO: Handle delete properly (needs special handling for member expressions)
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Drop);
                EmitOp(OpCode.PushTrue);
                break;

            case TokenType.Increment:
                NextToken();
                // TODO: Handle pre-increment (++x)
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Inc);
                break;

            case TokenType.Decrement:
                NextToken();
                // TODO: Handle pre-decrement (--x)
                ParseUnaryExpression(ParseFlags.PowForbidden);
                EmitOp(OpCode.Dec);
                break;

            default:
                ParsePostfixExpression(flags | ParseFlags.PostfixCall);
                break;
        }
    }

    /// <summary>
    /// Parses a postfix expression: expr++, expr--, call, member access
    /// </summary>
    public void ParsePostfixExpression(ParseFlags flags)
    {
        ParsePrimaryExpression();

        // Handle postfix operations (++, --, calls, member access)
        while (true)
        {
            if (!_currentToken.HasLineTerminatorBefore)
            {
                if (Match(TokenType.Increment))
                {
                    // TODO: Handle post-increment (x++)
                    EmitOp(OpCode.PostInc);
                    continue;
                }
                if (Match(TokenType.Decrement))
                {
                    // TODO: Handle post-decrement (x--)
                    EmitOp(OpCode.PostDec);
                    continue;
                }
            }

            if ((flags & ParseFlags.PostfixCall) != 0)
            {
                if (Match(TokenType.LeftParen))
                {
                    int argCount = ParseArguments();
                    EmitOp(OpCode.Call);
                    EmitU16((ushort)argCount);
                    continue;
                }
            }

            if (Match(TokenType.Dot))
            {
                if (!Check(TokenType.Identifier))
                {
                    throw new JSSyntaxError(
                        $"Expected identifier after '.', got {_currentToken.Type}",
                        _currentToken.Start);
                }
                var name = (string)_currentToken.Value!;
                NextToken(); // consume the identifier
                var atom = _atoms.GetOrCreateAtom(name);

                // Check if this is a method call: obj.method()
                if ((flags & ParseFlags.PostfixCall) != 0 && Check(TokenType.LeftParen))
                {
                    NextToken(); // consume '('
                    int argCount = ParseArguments();
                    EmitOp(OpCode.CallMethod);
                    EmitAtom(atom);
                    EmitU16((ushort)argCount);
                }
                else
                {
                    EmitOp(OpCode.GetField);
                    EmitAtom(atom);
                }
                continue;
            }

            if (Match(TokenType.LeftBracket))
            {
                ParseExpression();
                Expect(TokenType.RightBracket);
                EmitOp(OpCode.GetArrayEl);
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// Parses function call arguments.
    /// </summary>
    private int ParseArguments()
    {
        int count = 0;

        if (!Check(TokenType.RightParen))
        {
            do
            {
                ParseAssignExpression();
                count++;
            }
            while (Match(TokenType.Comma));
        }

        Expect(TokenType.RightParen);
        return count;
    }

    /// <summary>
    /// Parses a primary expression (literals, identifiers, grouping).
    /// </summary>
    public void ParsePrimaryExpression()
    {
        switch (_currentToken.Type)
        {
            case TokenType.Number:
                EmitNumberLiteral();
                NextToken();
                break;

            case TokenType.String:
                EmitStringLiteral();
                NextToken();
                break;

            case TokenType.True:
                EmitOp(OpCode.PushTrue);
                NextToken();
                break;

            case TokenType.False:
                EmitOp(OpCode.PushFalse);
                NextToken();
                break;

            case TokenType.Null:
                EmitOp(OpCode.Null);
                NextToken();
                break;

            case TokenType.This:
                EmitOp(OpCode.ScopeGetVar);
                EmitAtom(_atoms.GetOrCreateAtom("this"));
                EmitU16(0);
                NextToken();
                break;

            case TokenType.Identifier:
                EmitIdentifier();
                NextToken();
                break;

            case TokenType.LeftParen:
                NextToken();
                ParseExpression();
                Expect(TokenType.RightParen);
                break;

            case TokenType.LeftBracket:
                ParseArrayLiteral();
                break;

            case TokenType.LeftBrace:
                ParseObjectLiteral();
                break;

            case TokenType.Function:
                // TODO: ParseFunctionExpression();
                throw new NotImplementedException("Function expressions not yet implemented");

            default:
                throw new JSSyntaxError(
                    $"Unexpected token: {_currentToken.Type}",
                    _currentToken.Start);
        }
    }

    private void EmitNumberLiteral()
    {
        var value = _currentToken.Value;

        if (value is int i32)
        {
            EmitOp(OpCode.PushI32);
            EmitI32(i32);
        }
        else if (value is long i64)
        {
            if (i64 >= int.MinValue && i64 <= int.MaxValue)
            {
                EmitOp(OpCode.PushI32);
                EmitI32((int)i64);
            }
            else
            {
                // BigInt or large number - add to constant pool
                int idx = _currentFunction.Constants.AddDouble((double)i64);
                EmitOp(OpCode.PushConst);
                EmitU32((uint)idx);
            }
        }
        else if (value is double d)
        {
            // Check if it's a small integer that fits
            if (d >= int.MinValue && d <= int.MaxValue && d == (int)d)
            {
                EmitOp(OpCode.PushI32);
                EmitI32((int)d);
            }
            else
            {
                int idx = _currentFunction.Constants.AddDouble(d);
                EmitOp(OpCode.PushConst);
                EmitU32((uint)idx);
            }
        }
        else
        {
            throw new JSSyntaxError(
                $"Invalid number literal: {value}",
                _currentToken.Start);
        }
    }

    private void EmitStringLiteral()
    {
        var value = (string)_currentToken.Value!;
        int idx = _currentFunction.Constants.AddString(value);
        EmitOp(OpCode.PushConst);
        EmitU32((uint)idx);
    }

    private void EmitIdentifier()
    {
        var name = (string)_currentToken.Value!;
        var atom = _atoms.GetOrCreateAtom(name);
        
        EmitOp(OpCode.ScopeGetVar);
        EmitAtom(atom);
        EmitU16(0); // scope level (will be resolved later)
    }

    private void ParseArrayLiteral()
    {
        NextToken(); // consume '['
        
        // Count elements first, then emit
        // For now, we'll use a simpler approach: push elements, then create array
        int elementCount = 0;
        
        // Parse all array elements and push them on the stack
        while (!Check(TokenType.RightBracket))
        {
            if (Check(TokenType.Comma))
            {
                // Elision (hole in array) - push undefined
                EmitOp(OpCode.Undefined);
                elementCount++;
                NextToken();
                continue;
            }

            // Parse the element
            ParseAssignExpression();
            elementCount++;

            if (!Match(TokenType.Comma))
                break;
        }

        Expect(TokenType.RightBracket);
        
        // Create array from elements on stack
        EmitOp(OpCode.ArrayFrom);
        EmitU16((ushort)elementCount);
    }

    private void ParseObjectLiteral()
    {
        NextToken(); // consume '{'
        EmitOp(OpCode.Object);

        while (!Check(TokenType.RightBrace))
        {
            // Parse property name
            JSAtom propertyName;
            
            if (Check(TokenType.Identifier) || Check(TokenType.String))
            {
                propertyName = _atoms.GetOrCreateAtom((string)_currentToken.Value!);
                NextToken();
            }
            else if (Check(TokenType.Number))
            {
                propertyName = _atoms.GetOrCreateAtom(_currentToken.Value!.ToString()!);
                NextToken();
            }
            else if (Check(TokenType.LeftBracket))
            {
                // Computed property name
                NextToken();
                ParseAssignExpression();
                Expect(TokenType.RightBracket);
                Expect(TokenType.Colon);
                ParseAssignExpression();
                EmitOp(OpCode.DefineArrayEl);
                
                if (!Match(TokenType.Comma))
                    break;
                continue;
            }
            else
            {
                throw new JSSyntaxError(
                    $"Expected property name, got {_currentToken.Type}",
                    _currentToken.Start);
            }

            Expect(TokenType.Colon);
            ParseAssignExpression();
            
            EmitOp(OpCode.DefineField);
            EmitAtom(propertyName);

            if (!Match(TokenType.Comma))
                break;
        }

        Expect(TokenType.RightBrace);
    }

    /// <summary>
    /// Parses a new expression: new Constructor() or new Constructor(args)
    /// In JavaScript, 'new' creates an instance of a constructor function.
    /// The precedence is tricky: 'new Foo.bar()' means 'new (Foo.bar)()'
    /// </summary>
    private void ParseNewExpression()
    {
        Expect(TokenType.New);

        // Handle 'new.target' meta-property
        if (Check(TokenType.Dot))
        {
            NextToken(); // consume '.'
            if (Check(TokenType.Identifier) && (string)_currentToken.Value! == "target")
            {
                NextToken(); // consume 'target'
                EmitOp(OpCode.ScopeGetVar);
                EmitAtom(_atoms.GetOrCreateAtom("new.target"));
                EmitU16(0);
                return;
            }
            throw new JSSyntaxError("Expected 'target' after 'new.'", _currentToken.Start);
        }

        // Parse the constructor expression (can be member expression)
        // We need to parse the constructor without consuming the call parens
        ParseMemberExpression();

        // Check for arguments
        int argc = 0;
        if (Check(TokenType.LeftParen))
        {
            NextToken(); // consume '('

            // Parse arguments
            while (!Check(TokenType.RightParen))
            {
                ParseAssignExpression();
                argc++;

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightParen);
        }

        // Emit the new/call instruction
        EmitOp(OpCode.CallConstructor);
        EmitU16((ushort)argc);
    }

    /// <summary>
    /// Parses a member expression without the call part.
    /// Used by 'new' to get the constructor before optional parens.
    /// </summary>
    private void ParseMemberExpression()
    {
        ParsePrimaryExpression();

        // Handle member access chains: obj.prop, obj[expr]
        while (true)
        {
            if (Match(TokenType.Dot))
            {
                // Property access: obj.prop
                if (!Check(TokenType.Identifier))
                {
                    throw new JSSyntaxError(
                        $"Expected property name after '.', got {_currentToken.Type}",
                        _currentToken.Start);
                }
                var name = (string)_currentToken.Value!;
                var atom = _atoms.GetOrCreateAtom(name);
                NextToken();

                EmitOp(OpCode.GetField);
                EmitAtom(atom);
            }
            else if (Match(TokenType.LeftBracket))
            {
                // Computed property access: obj[expr]
                ParseExpression();
                Expect(TokenType.RightBracket);
                EmitOp(OpCode.GetArrayEl);
            }
            else
            {
                break;
            }
        }
    }

    #endregion

    #region Statement Parsing

    /// <summary>
    /// Parses a program (sequence of statements).
    /// </summary>
    public void ParseProgram()
    {
        while (!Check(TokenType.EOF))
        {
            ParseStatement();
        }
    }

    /// <summary>
    /// Parses a statement.
    /// </summary>
    public void ParseStatement()
    {
        switch (_currentToken.Type)
        {
            case TokenType.LeftBrace:
                ParseBlockStatement();
                break;

            case TokenType.Var:
                ParseVarStatement();
                break;

            case TokenType.Let:
                ParseLetStatement();
                break;

            case TokenType.Const:
                ParseConstStatement();
                break;

            case TokenType.If:
                ParseIfStatement();
                break;

            case TokenType.While:
                ParseWhileStatement();
                break;

            case TokenType.Do:
                ParseDoWhileStatement();
                break;

            case TokenType.For:
                ParseForStatement();
                break;

            case TokenType.Return:
                ParseReturnStatement();
                break;

            case TokenType.Break:
                ParseBreakStatement();
                break;

            case TokenType.Continue:
                ParseContinueStatement();
                break;

            case TokenType.Throw:
                ParseThrowStatement();
                break;

            case TokenType.Try:
                ParseTryStatement();
                break;

            case TokenType.Switch:
                ParseSwitchStatement();
                break;

            case TokenType.Semicolon:
                // Empty statement
                NextToken();
                break;

            case TokenType.Function:
                ParseFunctionDeclaration();
                break;

            default:
                ParseExpressionStatement();
                break;
        }
    }

    /// <summary>
    /// Parses a block statement: { statements }
    /// </summary>
    public void ParseBlockStatement()
    {
        Expect(TokenType.LeftBrace);
        _currentFunction.PushScope();

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
        {
            ParseStatement();
        }

        _currentFunction.PopScope();
        Expect(TokenType.RightBrace);
    }

    /// <summary>
    /// Parses an expression statement: expr;
    /// </summary>
    public void ParseExpressionStatement()
    {
        ParseExpression();
        EmitOp(OpCode.Drop);  // Discard expression result
        ExpectSemicolon();
    }

    /// <summary>
    /// Expects a semicolon, with automatic semicolon insertion support.
    /// </summary>
    private void ExpectSemicolon()
    {
        if (Check(TokenType.Semicolon))
        {
            NextToken();
        }
        else if (!Check(TokenType.RightBrace) && !Check(TokenType.EOF) && !_currentToken.HasLineTerminatorBefore)
        {
            throw new JSSyntaxError(
                $"Expected semicolon, got {_currentToken.Type}",
                _currentToken.Start);
        }
        // Otherwise: automatic semicolon insertion
    }

    #endregion

    #region Variable Declarations

    /// <summary>
    /// Parses a var statement: var x = expr, y = expr;
    /// </summary>
    public void ParseVarStatement()
    {
        Expect(TokenType.Var);
        ParseVariableDeclarationList(JSVarKind.Normal, isLexical: false, isConst: false);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a let statement: let x = expr, y = expr;
    /// </summary>
    public void ParseLetStatement()
    {
        Expect(TokenType.Let);
        ParseVariableDeclarationList(JSVarKind.Normal, isLexical: true, isConst: false);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a const statement: const x = expr, y = expr;
    /// </summary>
    public void ParseConstStatement()
    {
        Expect(TokenType.Const);
        ParseVariableDeclarationList(JSVarKind.Normal, isLexical: true, isConst: true);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a list of variable declarations.
    /// </summary>
    private void ParseVariableDeclarationList(JSVarKind kind, bool isLexical, bool isConst)
    {
        do
        {
            if (!Check(TokenType.Identifier))
            {
                throw new JSSyntaxError(
                    $"Expected identifier in variable declaration, got {_currentToken.Type}",
                    _currentToken.Start);
            }

            var name = (string)_currentToken.Value!;
            var atom = _atoms.GetOrCreateAtom(name);
            NextToken();

            // Define the variable
            int varIdx = _currentFunction.AddVar(atom, kind, isConst, isLexical);

            if (Match(TokenType.Assign))
            {
                // Parse initializer
                ParseAssignExpression();

                // Store the value
                EmitOp(isLexical ? OpCode.ScopePutVarInit : OpCode.ScopePutVar);
                EmitAtom(atom);
                EmitU16((ushort)_currentFunction.ScopeLevel);
            }
            else if (isConst)
            {
                throw new JSSyntaxError(
                    "Missing initializer for const variable",
                    _currentToken.Start);
            }
            else if (isLexical)
            {
                // Let variables are initialized to undefined
                EmitOp(OpCode.Undefined);
                EmitOp(OpCode.ScopePutVarInit);
                EmitAtom(atom);
                EmitU16((ushort)_currentFunction.ScopeLevel);
            }
        }
        while (Match(TokenType.Comma));
    }

    #endregion

    #region Control Flow Statements

    /// <summary>
    /// Parses an if statement: if (expr) stmt [else stmt]
    /// </summary>
    public void ParseIfStatement()
    {
        Expect(TokenType.If);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);

        int labelElse = NewLabel();
        EmitGoto(OpCode.IfFalse, labelElse);

        ParseStatement();

        if (Match(TokenType.Else))
        {
            int labelEnd = NewLabel();
            EmitGoto(OpCode.Goto, labelEnd);
            EmitLabel(labelElse);
            ParseStatement();
            EmitLabel(labelEnd);
        }
        else
        {
            EmitLabel(labelElse);
        }
    }

    /// <summary>
    /// Parses a while statement: while (expr) stmt
    /// </summary>
    public void ParseWhileStatement()
    {
        Expect(TokenType.While);

        int labelContinue = NewLabel();
        int labelBreak = NewLabel();

        // Push break/continue context
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

    /// <summary>
    /// Parses a do-while statement: do stmt while (expr);
    /// </summary>
    public void ParseDoWhileStatement()
    {
        Expect(TokenType.Do);

        int labelBody = NewLabel();
        int labelContinue = NewLabel();
        int labelBreak = NewLabel();

        PushBreakContext(labelBreak, labelContinue);

        EmitLabel(labelBody);
        ParseStatement();

        EmitLabel(labelContinue);
        Expect(TokenType.While);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);

        EmitGoto(OpCode.IfTrue, labelBody);
        EmitLabel(labelBreak);

        PopBreakContext();
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a for statement: for (init; test; update) stmt
    /// </summary>
    public void ParseForStatement()
    {
        Expect(TokenType.For);
        Expect(TokenType.LeftParen);

        _currentFunction.PushScope();

        // Parse initialization
        if (!Check(TokenType.Semicolon))
        {
            if (Check(TokenType.Var))
            {
                NextToken();
                ParseVariableDeclarationList(JSVarKind.Normal, isLexical: false, isConst: false);
            }
            else if (Check(TokenType.Let))
            {
                NextToken();
                ParseVariableDeclarationList(JSVarKind.Normal, isLexical: true, isConst: false);
            }
            else if (Check(TokenType.Const))
            {
                NextToken();
                ParseVariableDeclarationList(JSVarKind.Normal, isLexical: true, isConst: true);
            }
            else
            {
                ParseExpression();
                EmitOp(OpCode.Drop);
            }
        }
        Expect(TokenType.Semicolon);

        int labelTest = NewLabel();
        int labelContinue = NewLabel();
        int labelBody = NewLabel();
        int labelBreak = NewLabel();

        PushBreakContext(labelBreak, labelContinue);

        // Test expression
        EmitLabel(labelTest);
        if (!Check(TokenType.Semicolon))
        {
            ParseExpression();
            EmitGoto(OpCode.IfFalse, labelBreak);
        }
        Expect(TokenType.Semicolon);

        // Skip to body, then come back for update
        EmitGoto(OpCode.Goto, labelBody);

        // Update expression
        EmitLabel(labelContinue);
        if (!Check(TokenType.RightParen))
        {
            ParseExpression();
            EmitOp(OpCode.Drop);
        }
        EmitGoto(OpCode.Goto, labelTest);
        Expect(TokenType.RightParen);

        // Body
        EmitLabel(labelBody);
        ParseStatement();
        EmitGoto(OpCode.Goto, labelContinue);

        EmitLabel(labelBreak);

        PopBreakContext();
        _currentFunction.PopScope();
    }

    /// <summary>
    /// Parses a return statement: return [expr];
    /// </summary>
    public void ParseReturnStatement()
    {
        Expect(TokenType.Return);

        if (!Check(TokenType.Semicolon) && !Check(TokenType.RightBrace) && 
            !Check(TokenType.EOF) && !_currentToken.HasLineTerminatorBefore)
        {
            ParseExpression();
            EmitOp(OpCode.Return);
        }
        else
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Return);
        }

        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a break statement: break [label];
    /// </summary>
    public void ParseBreakStatement()
    {
        Expect(TokenType.Break);

        // TODO: Handle labeled break
        if (_breakStack.Count == 0)
        {
            throw new JSSyntaxError(
                "break statement not inside loop or switch",
                _currentToken.Start);
        }

        EmitGoto(OpCode.Goto, _breakStack.Peek().BreakLabel);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a continue statement: continue [label];
    /// </summary>
    public void ParseContinueStatement()
    {
        Expect(TokenType.Continue);

        // TODO: Handle labeled continue
        if (_breakStack.Count == 0)
        {
            throw new JSSyntaxError(
                "continue statement not inside loop",
                _currentToken.Start);
        }

        var ctx = _breakStack.Peek();
        if (ctx.ContinueLabel < 0)
        {
            throw new JSSyntaxError(
                "continue statement not inside loop",
                _currentToken.Start);
        }

        EmitGoto(OpCode.Goto, ctx.ContinueLabel);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a throw statement: throw expr;
    /// </summary>
    public void ParseThrowStatement()
    {
        Expect(TokenType.Throw);

        if (_currentToken.HasLineTerminatorBefore)
        {
            throw new JSSyntaxError(
                "Line terminator not allowed after throw",
                _currentToken.Start);
        }

        ParseExpression();
        EmitOp(OpCode.Throw);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a try statement: try { } catch (e) { } finally { }
    /// </summary>
    public void ParseTryStatement()
    {
        Expect(TokenType.Try);

        int labelCatch = NewLabel();
        int labelFinally = NewLabel();
        int labelEnd = NewLabel();

        // Emit catch entry point
        EmitGoto(OpCode.Catch, labelCatch);

        // Try block
        ParseBlockStatement();

        // Normal exit: jump to finally
        EmitOp(OpCode.Drop);  // Drop catch offset
        EmitOp(OpCode.Undefined);  // Dummy value
        EmitGoto(OpCode.GoSub, labelFinally);
        EmitOp(OpCode.Drop);
        EmitGoto(OpCode.Goto, labelEnd);

        // Catch block
        bool hasCatch = false;
        if (Check(TokenType.Catch))
        {
            hasCatch = true;
            NextToken();
            EmitLabel(labelCatch);

            _currentFunction.PushScope();

            // Optional catch parameter
            if (Match(TokenType.LeftParen))
            {
                if (!Check(TokenType.Identifier))
                {
                    throw new JSSyntaxError(
                        "Expected identifier in catch clause",
                        _currentToken.Start);
                }

                var name = (string)_currentToken.Value!;
                var atom = _atoms.GetOrCreateAtom(name);
                NextToken();
                Expect(TokenType.RightParen);

                // Define catch variable and store exception
                _currentFunction.AddVar(atom, JSVarKind.Normal, false, true);
                EmitOp(OpCode.ScopePutVar);
                EmitAtom(atom);
                EmitU16((ushort)_currentFunction.ScopeLevel);
            }
            else
            {
                // No catch parameter - just drop the exception
                EmitOp(OpCode.Drop);
            }

            ParseBlockStatement();

            _currentFunction.PopScope();

            // Jump to finally
            EmitOp(OpCode.Undefined);
            EmitGoto(OpCode.GoSub, labelFinally);
            EmitOp(OpCode.Drop);
            EmitGoto(OpCode.Goto, labelEnd);
        }

        if (!hasCatch)
        {
            // No catch block - rethrow after finally
            EmitLabel(labelCatch);
            EmitGoto(OpCode.GoSub, labelFinally);
            EmitOp(OpCode.Throw);
        }

        // Finally block
        EmitLabel(labelFinally);
        if (Check(TokenType.Finally))
        {
            NextToken();
            ParseBlockStatement();
        }
        EmitOp(OpCode.Ret);  // Return from gosub

        EmitLabel(labelEnd);
    }

    /// <summary>
    /// Parses a switch statement: switch (expr) { case expr: stmts }
    /// </summary>
    public void ParseSwitchStatement()
    {
        Expect(TokenType.Switch);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);
        Expect(TokenType.LeftBrace);

        int labelBreak = NewLabel();
        int labelDefault = -1;
        var caseLabels = new System.Collections.Generic.List<int>();

        PushBreakContext(labelBreak, -1);  // Switch has break but no continue
        _currentFunction.PushScope();

        // First pass: collect case expressions and labels
        int labelNextCase = -1;
        while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
        {
            if (Check(TokenType.Case))
            {
                NextToken();

                if (labelNextCase >= 0)
                {
                    // Previous case falls through, skip comparison
                    int skipLabel = NewLabel();
                    EmitGoto(OpCode.Goto, skipLabel);
                    EmitLabel(labelNextCase);
                    caseLabels.Add(skipLabel);
                }
                else
                {
                    labelNextCase = NewLabel();
                }

                EmitOp(OpCode.Dup);  // Duplicate switch value
                ParseExpression();
                Expect(TokenType.Colon);
                EmitOp(OpCode.StrictEq);
                int caseLabel = NewLabel();
                EmitGoto(OpCode.IfTrue, caseLabel);
                
                // Chain to next case check or default
                int nextCheck = NewLabel();
                EmitGoto(OpCode.Goto, nextCheck);
                EmitLabel(caseLabel);
                labelNextCase = nextCheck;
            }
            else if (Check(TokenType.Default))
            {
                NextToken();
                Expect(TokenType.Colon);
                
                if (labelDefault >= 0)
                {
                    throw new JSSyntaxError(
                        "Duplicate default clause",
                        _currentToken.Start);
                }

                if (labelNextCase >= 0)
                {
                    EmitGoto(OpCode.Goto, labelNextCase);
                }
                labelDefault = NewLabel();
                EmitLabel(labelDefault);
                labelNextCase = -1;
            }
            else
            {
                // Case body statement
                ParseStatement();
            }
        }

        // Handle fall-through to end
        if (labelNextCase >= 0)
        {
            if (labelDefault >= 0)
            {
                EmitLabel(labelNextCase);
                EmitGoto(OpCode.Goto, labelDefault);
            }
            else
            {
                EmitLabel(labelNextCase);
            }
        }

        EmitLabel(labelBreak);
        EmitOp(OpCode.Drop);  // Drop switch expression

        _currentFunction.PopScope();
        PopBreakContext();
        Expect(TokenType.RightBrace);
    }

    /// <summary>
    /// Parses a function declaration: function name(params) { body }
    /// </summary>
    public void ParseFunctionDeclaration()
    {
        Expect(TokenType.Function);

        if (!Check(TokenType.Identifier))
        {
            throw new JSSyntaxError(
                "Expected function name",
                _currentToken.Start);
        }

        var name = (string)_currentToken.Value!;
        var atom = _atoms.GetOrCreateAtom(name);
        NextToken();

        // TODO: Full function parsing with new JSFunctionDef
        // For now, skip to the closing brace
        Expect(TokenType.LeftParen);
        int parenDepth = 1;
        while (parenDepth > 0 && !Check(TokenType.EOF))
        {
            if (Check(TokenType.LeftParen)) parenDepth++;
            else if (Check(TokenType.RightParen)) parenDepth--;
            NextToken();
        }

        Expect(TokenType.LeftBrace);
        int braceDepth = 1;
        while (braceDepth > 0 && !Check(TokenType.EOF))
        {
            if (Check(TokenType.LeftBrace)) braceDepth++;
            else if (Check(TokenType.RightBrace)) braceDepth--;
            NextToken();
        }
    }

    #endregion

    #region Break/Continue Context

    private readonly System.Collections.Generic.Stack<BreakContext> _breakStack = new();

    private struct BreakContext
    {
        public int BreakLabel;
        public int ContinueLabel;

        public BreakContext(int breakLabel, int continueLabel)
        {
            BreakLabel = breakLabel;
            ContinueLabel = continueLabel;
        }
    }

    private void PushBreakContext(int breakLabel, int continueLabel)
    {
        _breakStack.Push(new BreakContext(breakLabel, continueLabel));
    }

    private void PopBreakContext()
    {
        _breakStack.Pop();
    }

    #endregion
}
