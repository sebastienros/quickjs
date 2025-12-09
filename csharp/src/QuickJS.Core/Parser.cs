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
    /// </summary>
    public void ParseAssignExpression(ParseFlags flags = ParseFlags.None)
    {
        ParseConditionalExpression(flags);
        // TODO: Handle assignment operators (=, +=, -=, etc.)
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
    /// Parses a unary expression: !expr, -expr, +expr, typeof expr, etc.
    /// </summary>
    public void ParseUnaryExpression(ParseFlags flags)
    {
        switch (_currentToken.Type)
        {
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

    #endregion
}
