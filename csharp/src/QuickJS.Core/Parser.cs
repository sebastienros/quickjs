// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

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
    /// Parses an expression (comma expression - lowest precedence).
    /// The comma operator evaluates each operand and returns the last.
    /// </summary>
    public void ParseExpression()
    {
        ParseAssignExpression(ParseFlags.InAccepted);

        // Handle comma operator: expr, expr, expr
        while (Match(TokenType.Comma))
        {
            EmitOp(OpCode.Drop); // Discard previous value
            ParseAssignExpression(ParseFlags.InAccepted);
        }
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
    /// Also handles yield and await which have similar precedence.
    /// </summary>
    public void ParseUnaryExpression(ParseFlags flags)
    {
        switch (_currentToken.Type)
        {
            case TokenType.New:
                ParseNewExpression();
                break;

            case TokenType.Yield:
                // yield is a unary-like expression in generators
                ParseYieldExpression();
                break;

            case TokenType.Await:
                // await is a unary-like expression in async functions
                ParseAwaitExpression();
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
                ParseParenthesizedExpressionOrArrow();
                break;

            case TokenType.LeftBracket:
                ParseArrayLiteral();
                break;

            case TokenType.LeftBrace:
                ParseObjectLiteral();
                break;

            case TokenType.Function:
                ParseFunctionExpression();
                break;

            case TokenType.Async:
                // async function or async arrow
                ParseAsyncExpression();
                break;

            case TokenType.Yield:
                ParseYieldExpression();
                break;

            case TokenType.Await:
                ParseAwaitExpression();
                break;

            case TokenType.Super:
                ParseSuperExpression();
                break;

            default:
                throw new JSSyntaxError(
                    $"Unexpected token: {_currentToken.Type}",
                    _currentToken.Start);
        }
    }

    /// <summary>
    /// Parses a parenthesized expression or arrow function.
    /// This requires lookahead to distinguish: (expr) vs (params) => body
    /// </summary>
    private void ParseParenthesizedExpressionOrArrow()
    {
        NextToken(); // consume '('

        // Check for empty parens: () => ...
        if (Check(TokenType.RightParen))
        {
            NextToken(); // consume ')'
            if (!Check(TokenType.Arrow))
            {
                throw new JSSyntaxError(
                    "Unexpected token ')'",
                    _currentToken.Start);
            }
            // Empty arrow function - parse it directly with no params
            ParseArrowFunctionDirect(JSFunctionKind.Normal, new List<JSAtom>());
            return;
        }

        // Check for rest parameter: (...) => ...
        if (Check(TokenType.Ellipsis))
        {
            // This must be an arrow function with rest param
            ParseArrowFunctionWithParams(JSFunctionKind.Normal);
            return;
        }

        // Try to parse as expression first, but track identifiers for potential arrow params
        // We parse expressions but keep track of whether each position was a simple identifier
        int startPos = _currentFunction.ByteCode.Size;
        var potentialParams = new List<JSAtom>();
        bool couldBeArrowParams = true;

        // Parse first expression, tracking if it's a simple identifier
        if (Check(TokenType.Identifier))
        {
            var name = (string)_currentToken.Value!;
            potentialParams.Add(_atoms.GetOrCreateAtom(name));
        }
        else
        {
            couldBeArrowParams = false;
        }
        ParseAssignExpression();

        // Check what follows
        while (Check(TokenType.Comma))
        {
            NextToken(); // consume ','

            // Check for rest after comma: (a, ...rest) => ...
            if (Check(TokenType.Ellipsis))
            {
                // Must be arrow function with rest param
                // Truncate what we've parsed and use ParseArrowFunctionWithParams
                _currentFunction.ByteCode.Truncate(startPos);
                // Can't easily reparse from here - for now, error
                throw new JSSyntaxError(
                    "Rest parameters in arrow functions not yet supported in this context",
                    _currentToken.Start);
            }

            // Track if this is an identifier
            if (couldBeArrowParams && Check(TokenType.Identifier))
            {
                var name = (string)_currentToken.Value!;
                potentialParams.Add(_atoms.GetOrCreateAtom(name));
            }
            else
            {
                couldBeArrowParams = false;
            }

            EmitOp(OpCode.Drop); // Drop previous if it's comma expression
            ParseAssignExpression();
        }

        Expect(TokenType.RightParen);

        if (Check(TokenType.Arrow))
        {
            if (!couldBeArrowParams)
            {
                throw new JSSyntaxError(
                    "Invalid arrow function parameter list",
                    _currentToken.Start);
            }
            // It's an arrow function! Truncate emitted code and build arrow function
            _currentFunction.ByteCode.Truncate(startPos);
            ParseArrowFunctionDirect(JSFunctionKind.Normal, potentialParams);
            return;
        }

        // It was just a parenthesized expression - bytecode is already emitted
    }

    /// <summary>
    /// Parse arrow function directly when we already know the parameters.
    /// Used when we've already consumed (...params) and detected =>.
    /// </summary>
    private void ParseArrowFunctionDirect(JSFunctionKind kind, List<JSAtom> parameters)
    {
        // Save parent and create new function
        var parentFunction = _currentFunction;
        var newFunction = new JSFunctionDef(JSAtom.Empty);
        newFunction.Filename = parentFunction.Filename;
        newFunction.Parent = parentFunction;
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

        // Expect and consume arrow
        Expect(TokenType.Arrow);

        // Parse body
        _currentFunction.PushScope();

        if (Check(TokenType.LeftBrace))
        {
            ParseFunctionBody();
        }
        else
        {
            ParseAssignExpression();
            if ((kind & JSFunctionKind.Async) != 0)
                EmitOp(OpCode.ReturnAsync);
            else
                EmitOp(OpCode.Return);
        }

        _currentFunction.PopScope();

        // Finalize
        if (!EndsWithReturn())
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Return);
        }

        int funcIdx = parentFunction.AddChildFunction(newFunction);
        _currentFunction = parentFunction;

        EmitOp(OpCode.FClosure);
        EmitU16((ushort)funcIdx);
    }

    /// <summary>
    /// Parse arrow function when we already know we have arrow params pattern.
    /// Called when we see something like (...rest) or after detecting =>
    /// </summary>
    private void ParseArrowFunctionWithParams(JSFunctionKind kind)
    {
        // Save parent and create new function
        var parentFunction = _currentFunction;
        var newFunction = new JSFunctionDef(JSAtom.Empty);
        newFunction.Filename = parentFunction.Filename;
        newFunction.Parent = parentFunction;
        newFunction.FuncKind = kind;
        newFunction.FuncType = JSParseFunctionType.Arrow;

        ConfigureFunctionByType(newFunction, JSParseFunctionType.Arrow, kind);
        _currentFunction = newFunction;

        // Parse parameters (we're already past the '(')
        bool hasOptionalArg = false;
        bool hasSimpleParameterList = true;

        while (!Check(TokenType.RightParen))
        {
            bool isRest = Match(TokenType.Ellipsis);
            if (isRest) hasSimpleParameterList = false;

            if (Check(TokenType.Identifier))
            {
                var paramName = (string)_currentToken.Value!;
                var paramAtom = _atoms.GetOrCreateAtom(paramName);
                NextToken();

                int idx = _currentFunction.AddArg(paramAtom);

                if (isRest)
                {
                    // Rest parameter
                    EmitOp(OpCode.Rest);
                    EmitU16((ushort)idx);
                    EmitOp(OpCode.PutArg);
                    EmitU16((ushort)idx);
                    hasOptionalArg = true;
                }
                else if (Match(TokenType.Assign))
                {
                    // Default parameter
                    hasSimpleParameterList = false;
                    hasOptionalArg = true;

                    int label = NewLabel();
                    EmitOp(OpCode.GetArg);
                    EmitU16((ushort)idx);
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.Undefined);
                    EmitOp(OpCode.StrictEq);
                    EmitGoto(OpCode.IfFalse, label);
                    EmitOp(OpCode.Drop);
                    ParseAssignExpression();
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.PutArg);
                    EmitU16((ushort)idx);
                    EmitLabel(label);
                    EmitOp(OpCode.ScopePutVarInit);
                    EmitAtom(paramAtom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }
                else
                {
                    if (!hasOptionalArg)
                        _currentFunction.DefinedArgCount++;
                }
            }
            else
            {
                throw new JSSyntaxError(
                    $"Expected parameter name, got {_currentToken.Type}",
                    _currentToken.Start);
            }

            if (isRest && !Check(TokenType.RightParen))
            {
                throw new JSSyntaxError(
                    "Rest parameter must be last",
                    _currentToken.Start);
            }

            if (!Match(TokenType.Comma))
                break;
        }

        Expect(TokenType.RightParen);
        _currentFunction.HasSimpleParameterList = hasSimpleParameterList;

        // Expect and consume arrow
        Expect(TokenType.Arrow);

        // Parse body
        _currentFunction.PushScope();

        if (Check(TokenType.LeftBrace))
        {
            ParseFunctionBody();
        }
        else
        {
            ParseAssignExpression();
            if ((kind & JSFunctionKind.Async) != 0)
                EmitOp(OpCode.ReturnAsync);
            else
                EmitOp(OpCode.Return);
        }

        _currentFunction.PopScope();

        // Finalize
        if (!EndsWithReturn())
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Return);
        }

        int funcIdx = parentFunction.AddChildFunction(newFunction);
        _currentFunction = parentFunction;

        EmitOp(OpCode.FClosure);
        EmitU16((ushort)funcIdx);
    }
    /// <summary>
    /// Parses an async expression: async function or async arrow function
    /// </summary>
    private void ParseAsyncExpression()
    {
        NextToken(); // consume 'async'

        // Check if the NEXT token is preceded by a line terminator
        // If so, 'async' should be treated as an identifier (but we've consumed it)
        // According to ECMAScript, async [no LineTerminator here] ArrowFunction
        if (_currentToken.HasLineTerminatorBefore)
        {
            // There's a line terminator after 'async', so it should be an identifier
            // But we've already consumed 'async' - emit it as an identifier
            // We emit the atom for "async" directly
            var asyncAtom = _atoms.GetOrCreateAtom("async");
            EmitOp(OpCode.ScopeGetVar);
            EmitAtom(asyncAtom);
            EmitU16((ushort)_currentFunction.ScopeLevel);
            return;
        }

        if (Check(TokenType.Function))
        {
            // async function expression
            ParseFunction(JSParseFunctionType.Expression, JSFunctionKind.Async, JSAtom.Empty);
        }
        else if (Check(TokenType.LeftParen))
        {
            // async arrow function: async () => ... or async (params) => ...
            NextToken(); // consume '('
            ParseArrowFunctionWithParams(JSFunctionKind.Async);
        }
        else if (Check(TokenType.Identifier))
        {
            // async arrow function with single param: async x => ...
            var paramName = (string)_currentToken.Value!;
            var paramAtom = _atoms.GetOrCreateAtom(paramName);
            NextToken();

            if (!Check(TokenType.Arrow))
            {
                throw new JSSyntaxError(
                    "Expected '=>' after async parameter",
                    _currentToken.Start);
            }

            // Build the async arrow function
            var parentFunction = _currentFunction;
            var newFunction = new JSFunctionDef(JSAtom.Empty);
            newFunction.Filename = parentFunction.Filename;
            newFunction.Parent = parentFunction;
            newFunction.FuncKind = JSFunctionKind.Async;
            newFunction.FuncType = JSParseFunctionType.Arrow;

            ConfigureFunctionByType(newFunction, JSParseFunctionType.Arrow, JSFunctionKind.Async);
            _currentFunction = newFunction;

            _currentFunction.AddArg(paramAtom);
            _currentFunction.DefinedArgCount = 1;
            _currentFunction.HasSimpleParameterList = true;

            Expect(TokenType.Arrow);

            _currentFunction.PushScope();

            if (Check(TokenType.LeftBrace))
            {
                ParseFunctionBody();
            }
            else
            {
                ParseAssignExpression();
                EmitOp(OpCode.ReturnAsync);
            }

            _currentFunction.PopScope();

            if (!EndsWithReturn())
            {
                EmitOp(OpCode.Undefined);
                EmitOp(OpCode.Return);
            }

            int funcIdx = parentFunction.AddChildFunction(newFunction);
            _currentFunction = parentFunction;

            EmitOp(OpCode.FClosure);
            EmitU16((ushort)funcIdx);
        }
        else
        {
            throw new JSSyntaxError(
                "Expected function or arrow function after 'async'",
                _currentToken.Start);
        }
    }

    /// <summary>
    /// Parses a yield expression: yield, yield expr, or yield* expr
    /// </summary>
    private void ParseYieldExpression()
    {
        if ((_currentFunction.FuncKind & JSFunctionKind.Generator) == 0)
        {
            throw new JSSyntaxError(
                "yield expression is only valid in generator functions",
                _currentToken.Start);
        }

        NextToken(); // consume 'yield'

        bool isStar = Match(TokenType.Asterisk);

        // Check for expression after yield (respecting line terminator)
        if (!_currentToken.HasLineTerminatorBefore &&
            !Check(TokenType.Semicolon) &&
            !Check(TokenType.RightParen) &&
            !Check(TokenType.RightBracket) &&
            !Check(TokenType.RightBrace) &&
            !Check(TokenType.Comma) &&
            !Check(TokenType.Colon) &&
            !Check(TokenType.EOF))
        {
            ParseAssignExpression();
        }
        else
        {
            EmitOp(OpCode.Undefined);
        }

        if (isStar)
        {
            if ((_currentFunction.FuncKind & JSFunctionKind.Async) != 0)
            {
                EmitOp(OpCode.AsyncYieldStar);
            }
            else
            {
                EmitOp(OpCode.YieldStar);
            }
        }
        else
        {
            EmitOp(OpCode.Yield);
        }
    }

    /// <summary>
    /// Parses an await expression: await expr
    /// </summary>
    private void ParseAwaitExpression()
    {
        if ((_currentFunction.FuncKind & JSFunctionKind.Async) == 0)
        {
            throw new JSSyntaxError(
                "await expression is only valid in async functions",
                _currentToken.Start);
        }

        NextToken(); // consume 'await'

        // Parse the expression to await
        ParseUnaryExpression(ParseFlags.None);

        EmitOp(OpCode.Await);
    }

    /// <summary>
    /// Parses a super expression: super.prop, super[expr], or super(args)
    /// </summary>
    private void ParseSuperExpression()
    {
        NextToken(); // consume 'super'

        if (Check(TokenType.LeftParen))
        {
            // super() - constructor call
            if (!_currentFunction.SuperCallAllowed)
            {
                throw new JSSyntaxError(
                    "super() is only valid in derived class constructors",
                    _currentToken.Start);
            }

            // Get the super constructor
            EmitOp(OpCode.GetSuper);

            NextToken(); // consume '('
            int argCount = 0;

            if (!Check(TokenType.RightParen))
            {
                do
                {
                    ParseAssignExpression();
                    argCount++;
                }
                while (Match(TokenType.Comma));
            }

            Expect(TokenType.RightParen);

            // Call the super constructor
            EmitOp(OpCode.CallConstructor);
            EmitU16((ushort)argCount);
        }
        else if (Check(TokenType.Dot))
        {
            // super.property
            if (!_currentFunction.SuperAllowed)
            {
                throw new JSSyntaxError(
                    "super property access is only valid in methods",
                    _currentToken.Start);
            }

            NextToken(); // consume '.'

            if (!Check(TokenType.Identifier))
            {
                throw new JSSyntaxError(
                    "Expected property name after 'super.'",
                    _currentToken.Start);
            }

            var name = (string)_currentToken.Value!;
            var atom = _atoms.GetOrCreateAtom(name);
            NextToken();

            EmitOp(OpCode.GetSuper);
            EmitAtom(atom);
        }
        else if (Check(TokenType.LeftBracket))
        {
            // super[expr]
            if (!_currentFunction.SuperAllowed)
            {
                throw new JSSyntaxError(
                    "super property access is only valid in methods",
                    _currentToken.Start);
            }

            NextToken(); // consume '['
            ParseExpression();
            Expect(TokenType.RightBracket);

            EmitOp(OpCode.GetSuperValue);
        }
        else
        {
            throw new JSSyntaxError(
                "super must be followed by argument list or member access",
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

            case TokenType.Async:
                // async function declaration
                ParseAsyncFunctionDeclaration();
                break;

            default:
                ParseExpressionStatement();
                break;
        }
    }

    /// <summary>
    /// Parses an async function declaration: async function name() { }
    /// </summary>
    private void ParseAsyncFunctionDeclaration()
    {
        NextToken(); // consume 'async'

        // Check for line terminator after async (would make it an identifier)
        // According to ECMAScript, 'async [no LineTerminator here] function' is async function
        if (_currentToken.HasLineTerminatorBefore || !Check(TokenType.Function))
        {
            // Treat 'async' as an identifier used as an expression
            // This is a bit complex as we've already consumed 'async'
            // For now, require 'function' keyword after async in statement position
            throw new JSSyntaxError(
                "Expected 'function' after 'async' in statement position",
                _currentToken.Start);
        }

        // Parse as async function declaration
        ParseFunction(JSParseFunctionType.Statement, JSFunctionKind.Async, JSAtom.Empty);
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
    /// Also handles generators (function*) and async functions.
    /// </summary>
    public void ParseFunctionDeclaration()
    {
        ParseFunction(JSParseFunctionType.Statement, JSFunctionKind.Normal, JSAtom.Empty);
    }

    /// <summary>
    /// Parses a function expression: function(params) { body } or function name(params) { body }
    /// </summary>
    public void ParseFunctionExpression()
    {
        ParseFunction(JSParseFunctionType.Expression, JSFunctionKind.Normal, JSAtom.Empty);
    }

    /// <summary>
    /// Parses a function with the specified type and kind.
    /// This is the main function parsing entry point.
    /// </summary>
    /// <param name="funcType">The type of function (declaration, expression, arrow, etc.)</param>
    /// <param name="funcKind">The kind of function (normal, generator, async)</param>
    /// <param name="funcName">The function name (or null for anonymous)</param>
    public void ParseFunction(JSParseFunctionType funcType, JSFunctionKind funcKind, JSAtom funcName)
    {
        bool isExpression = funcType != JSParseFunctionType.Statement;

        // For statement/expression/var functions, consume 'function' keyword
        if (funcType == JSParseFunctionType.Statement ||
            funcType == JSParseFunctionType.Expression)
        {
            // Check for 'async' keyword before 'function'
            if (Check(TokenType.Async) && !_currentToken.HasLineTerminatorBefore)
            {
                NextToken();
                funcKind = JSFunctionKind.Async;
            }

            Expect(TokenType.Function);

            // Check for generator (function*)
            if (Match(TokenType.Asterisk))
            {
                funcKind |= JSFunctionKind.Generator;
            }

            // Parse function name
            if (Check(TokenType.Identifier))
            {
                var name = (string)_currentToken.Value!;
                funcName = _atoms.GetOrCreateAtom(name);
                NextToken();
            }
            else if (funcType == JSParseFunctionType.Statement)
            {
                throw new JSSyntaxError(
                    "Function name expected",
                    _currentToken.Start);
            }
        }
        else if (funcType != JSParseFunctionType.Arrow)
        {
            // For method, getter, setter - name is already parsed
        }

        // Save the parent function and create a new function definition
        var parentFunction = _currentFunction;
        var newFunction = new JSFunctionDef(funcName);
        newFunction.Filename = parentFunction.Filename;
        newFunction.Parent = parentFunction;
        newFunction.FuncKind = funcKind;
        newFunction.FuncType = funcType;

        // Set function properties based on type
        ConfigureFunctionByType(newFunction, funcType, funcKind);

        // Switch to the new function's context
        _currentFunction = newFunction;

        // Parse parameters
        if (funcType == JSParseFunctionType.Arrow && Check(TokenType.Identifier))
        {
            // Arrow function with single unparenthesized parameter: x => expr
            var paramName = (string)_currentToken.Value!;
            var paramAtom = _atoms.GetOrCreateAtom(paramName);
            _currentFunction.AddArg(paramAtom);
            _currentFunction.DefinedArgCount = 1;
            NextToken();
        }
        else if (funcType != JSParseFunctionType.ClassStaticInit)
        {
            // Parse parenthesized parameters
            ParseFunctionParameters(funcType);
        }

        // For generators, emit initial yield
        if ((funcKind & JSFunctionKind.Generator) != 0)
        {
            EmitOp(OpCode.InitialYield);
        }

        // Mark that we're in the function body
        _currentFunction.PushScope(); // Body scope

        // Parse function body
        if (funcType == JSParseFunctionType.Arrow)
        {
            Expect(TokenType.Arrow);

            if (Check(TokenType.LeftBrace))
            {
                // Arrow function with block body: () => { statements }
                ParseFunctionBody();
            }
            else
            {
                // Arrow function with expression body: () => expr
                ParseAssignExpression();

                if ((funcKind & JSFunctionKind.Async) != 0)
                {
                    EmitOp(OpCode.ReturnAsync);
                }
                else
                {
                    EmitOp(OpCode.Return);
                }
            }
        }
        else
        {
            ParseFunctionBody();
        }

        _currentFunction.PopScope();

        // Add implicit return undefined if needed
        if (!EndsWithReturn())
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Return);
        }

        // Add the function to the parent's child functions
        int funcIdx = parentFunction.AddChildFunction(newFunction);

        // Switch back to the parent function
        _currentFunction = parentFunction;

        // Emit code to create the function object (closure)
        EmitOp(OpCode.FClosure);
        EmitU16((ushort)funcIdx);

        // For function declarations, store in the variable
        if (funcType == JSParseFunctionType.Statement && !funcName.IsEmpty)
        {
            EmitOp(OpCode.ScopePutVar);
            EmitAtom(funcName);
            EmitU16((ushort)_currentFunction.ScopeLevel);
        }
    }

    /// <summary>
    /// Configures function properties based on its type.
    /// </summary>
    private void ConfigureFunctionByType(JSFunctionDef func, JSParseFunctionType funcType, JSFunctionKind funcKind)
    {
        // Set up binding properties
        func.HasArgumentsBinding = funcType != JSParseFunctionType.Arrow &&
                                   funcType != JSParseFunctionType.ClassStaticInit;
        func.HasThisBinding = func.HasArgumentsBinding;

        // Set up prototype property
        func.HasPrototype = (funcType == JSParseFunctionType.Statement ||
                             funcType == JSParseFunctionType.Expression) &&
                            funcKind == JSFunctionKind.Normal;

        // Set up home object (for super access)
        func.HasHomeObject = funcType == JSParseFunctionType.Method ||
                             funcType == JSParseFunctionType.Getter ||
                             funcType == JSParseFunctionType.Setter ||
                             funcType == JSParseFunctionType.ClassConstructor ||
                             funcType == JSParseFunctionType.DerivedClassConstructor;

        // Configure new.target, super access based on type
        if (funcType == JSParseFunctionType.Arrow && func.Parent != null)
        {
            // Arrow functions inherit from parent
            func.NewTargetAllowed = func.Parent.NewTargetAllowed;
            func.SuperCallAllowed = func.Parent.SuperCallAllowed;
            func.SuperAllowed = func.Parent.SuperAllowed;
            func.ArgumentsAllowed = func.Parent.ArgumentsAllowed;
        }
        else if (funcType == JSParseFunctionType.ClassStaticInit)
        {
            func.NewTargetAllowed = true;
            func.SuperCallAllowed = false;
            func.SuperAllowed = true;
            func.ArgumentsAllowed = false;
        }
        else
        {
            func.NewTargetAllowed = true;
            func.SuperCallAllowed = funcType == JSParseFunctionType.DerivedClassConstructor;
            func.SuperAllowed = func.HasHomeObject;
            func.ArgumentsAllowed = true;
        }

        func.IsDerivedClassConstructor = funcType == JSParseFunctionType.DerivedClassConstructor;
    }

    /// <summary>
    /// Parses function parameters: (param1, param2 = default, ...rest)
    /// </summary>
    private void ParseFunctionParameters(JSParseFunctionType funcType)
    {
        Expect(TokenType.LeftParen);

        bool hasOptionalArg = false;
        bool hasSimpleParameterList = true;
        bool hasParameterExpressions = false;

        while (!Check(TokenType.RightParen))
        {
            bool isRest = false;

            // Check for rest parameter
            if (Match(TokenType.Ellipsis))
            {
                if (funcType == JSParseFunctionType.Setter)
                {
                    throw new JSSyntaxError(
                        "Setter cannot have rest parameter",
                        _currentToken.Start);
                }
                isRest = true;
                hasSimpleParameterList = false;
            }

            // Check for destructuring pattern
            if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                hasSimpleParameterList = false;

                // Add unnamed arg for destructuring
                int idx = _currentFunction.AddArg(JSAtom.Empty);

                if (isRest)
                {
                    EmitOp(OpCode.Rest);
                    EmitU16((ushort)idx);
                }
                else
                {
                    EmitOp(OpCode.GetArg);
                    EmitU16((ushort)idx);
                }

                // Parse destructuring pattern
                ParseDestructuringPattern(isDeclaration: true, hasDefaultAllowed: true);

                if (!hasOptionalArg)
                {
                    _currentFunction.DefinedArgCount++;
                }
            }
            else if (Check(TokenType.Identifier))
            {
                // Simple parameter
                var paramName = (string)_currentToken.Value!;
                var paramAtom = _atoms.GetOrCreateAtom(paramName);
                NextToken();

                int idx = _currentFunction.AddArg(paramAtom);

                if (isRest)
                {
                    // Rest parameter: ...name
                    EmitOp(OpCode.Rest);
                    EmitU16((ushort)idx);
                    EmitOp(OpCode.PutArg);
                    EmitU16((ushort)idx);
                    hasSimpleParameterList = false;
                    hasOptionalArg = true;
                }
                else if (Check(TokenType.Assign))
                {
                    // Default parameter: name = expr
                    NextToken();
                    hasSimpleParameterList = false;
                    hasParameterExpressions = true;
                    hasOptionalArg = true;

                    // Generate code: if (arg === undefined) arg = default;
                    int label = NewLabel();
                    EmitOp(OpCode.GetArg);
                    EmitU16((ushort)idx);
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.Undefined);
                    EmitOp(OpCode.StrictEq);
                    EmitGoto(OpCode.IfFalse, label);
                    EmitOp(OpCode.Drop);
                    ParseAssignExpression();
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.PutArg);
                    EmitU16((ushort)idx);
                    EmitLabel(label);
                    EmitOp(OpCode.ScopePutVarInit);
                    EmitAtom(paramAtom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }
                else
                {
                    if (!hasOptionalArg)
                    {
                        _currentFunction.DefinedArgCount++;
                    }
                }
            }
            else
            {
                throw new JSSyntaxError(
                    $"Expected parameter name, got {_currentToken.Type}",
                    _currentToken.Start);
            }

            // Rest parameter must be last
            if (isRest && !Check(TokenType.RightParen))
            {
                throw new JSSyntaxError(
                    "Rest parameter must be last",
                    _currentToken.Start);
            }

            if (!Match(TokenType.Comma))
            {
                break;
            }
        }

        Expect(TokenType.RightParen);

        // Validate getter/setter parameter count
        if (funcType == JSParseFunctionType.Getter && _currentFunction.ArgCount != 0)
        {
            throw new JSSyntaxError(
                "Getter must have no parameters",
                _currentToken.Start);
        }
        if (funcType == JSParseFunctionType.Setter && _currentFunction.ArgCount != 1)
        {
            throw new JSSyntaxError(
                "Setter must have exactly one parameter",
                _currentToken.Start);
        }

        _currentFunction.HasSimpleParameterList = hasSimpleParameterList;
        _currentFunction.HasParameterExpressions = hasParameterExpressions;
    }

    /// <summary>
    /// Parses a destructuring pattern for parameters or assignments.
    /// </summary>
    private void ParseDestructuringPattern(bool isDeclaration, bool hasDefaultAllowed)
    {
        if (Check(TokenType.LeftBracket))
        {
            // Array destructuring: [a, b, c]
            NextToken();

            while (!Check(TokenType.RightBracket))
            {
                if (Check(TokenType.Comma))
                {
                    // Elision - skip element
                    NextToken();
                    EmitOp(OpCode.Drop);
                    continue;
                }

                bool isRest = Match(TokenType.Ellipsis);

                if (Check(TokenType.Identifier))
                {
                    var name = (string)_currentToken.Value!;
                    var atom = _atoms.GetOrCreateAtom(name);
                    NextToken();

                    if (isRest)
                    {
                        EmitOp(OpCode.ArrayFrom);
                        EmitU16(0); // Collect remaining
                    }
                    else
                    {
                        // Get next array element
                        EmitOp(OpCode.IteratorNext);
                    }

                    if (isDeclaration)
                    {
                        EmitOp(OpCode.ScopePutVarInit);
                        EmitAtom(atom);
                        EmitU16((ushort)_currentFunction.ScopeLevel);
                    }
                    else
                    {
                        EmitOp(OpCode.PutRefValue);
                    }
                }
                else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                {
                    // Nested destructuring
                    EmitOp(OpCode.IteratorNext);
                    ParseDestructuringPattern(isDeclaration, hasDefaultAllowed);
                }

                if (!Match(TokenType.Comma))
                {
                    break;
                }
            }

            Expect(TokenType.RightBracket);
        }
        else if (Check(TokenType.LeftBrace))
        {
            // Object destructuring: { a, b, c }
            NextToken();

            while (!Check(TokenType.RightBrace))
            {
                JSAtom propName;
                JSAtom varName;

                if (Check(TokenType.Identifier))
                {
                    var name = (string)_currentToken.Value!;
                    propName = _atoms.GetOrCreateAtom(name);
                    varName = propName;
                    NextToken();

                    // Check for renaming: { prop: newName }
                    if (Match(TokenType.Colon))
                    {
                        if (!Check(TokenType.Identifier))
                        {
                            throw new JSSyntaxError(
                                "Expected identifier after ':'",
                                _currentToken.Start);
                        }
                        var newName = (string)_currentToken.Value!;
                        varName = _atoms.GetOrCreateAtom(newName);
                        NextToken();
                    }

                    // Get the property from the object
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.GetField);
                    EmitAtom(propName);

                    // Check for default value
                    if (hasDefaultAllowed && Match(TokenType.Assign))
                    {
                        int label = NewLabel();
                        EmitOp(OpCode.Dup);
                        EmitOp(OpCode.Undefined);
                        EmitOp(OpCode.StrictEq);
                        EmitGoto(OpCode.IfFalse, label);
                        EmitOp(OpCode.Drop);
                        ParseAssignExpression();
                        EmitLabel(label);
                    }

                    // Store the value
                    if (isDeclaration)
                    {
                        EmitOp(OpCode.ScopePutVarInit);
                        EmitAtom(varName);
                        EmitU16((ushort)_currentFunction.ScopeLevel);
                    }
                    else
                    {
                        EmitOp(OpCode.PutRefValue);
                    }
                }

                if (!Match(TokenType.Comma))
                {
                    break;
                }
            }

            Expect(TokenType.RightBrace);
            EmitOp(OpCode.Drop); // Drop the source object
        }
    }

    /// <summary>
    /// Parses the function body enclosed in braces.
    /// </summary>
    private void ParseFunctionBody()
    {
        Expect(TokenType.LeftBrace);

        // Parse directives (like "use strict")
        while (Check(TokenType.String))
        {
            var directive = (string)_currentToken.Value!;
            if (directive == "use strict")
            {
                _currentFunction.IsStrict = true;
            }
            NextToken();
            if (!Match(TokenType.Semicolon) && !_currentToken.HasLineTerminatorBefore)
            {
                break;
            }
        }

        // Parse statements
        while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
        {
            ParseStatement();
        }

        Expect(TokenType.RightBrace);
    }

    /// <summary>
    /// Checks if the current bytecode ends with a return statement.
    /// </summary>
    private bool EndsWithReturn()
    {
        var buffer = _currentFunction.ByteCode;
        if (buffer.Size < 1)
            return false;

        // Get the byte at the last opcode position
        int lastPos = buffer.LastOpcodePosition;
        if (lastPos < 0)
            return false;

        var bytes = buffer.ToArray();
        var lastOp = (OpCode)bytes[lastPos];
        return lastOp == OpCode.Return || lastOp == OpCode.ReturnAsync;
    }

    /// <summary>
    /// Parses an arrow function: (params) => expr or (params) => { body }
    /// Called after the parameters have been parsed as an expression.
    /// </summary>
    public void ParseArrowFunction(JSFunctionKind kind = JSFunctionKind.Normal)
    {
        ParseFunction(JSParseFunctionType.Arrow, kind, JSAtom.Empty);
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
