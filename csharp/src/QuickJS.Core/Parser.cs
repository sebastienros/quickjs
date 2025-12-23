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
    private readonly string _fileName;
    private readonly string _source;
    private Token _currentToken;
    private JSFunctionDef _currentFunction;
    private bool _isModule;
    private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
    private readonly List<int> _withScopeStack = new List<int>();
    
    // LHS tracking for assignment
    private enum LhsKind { None, GlobalVar, LocalVar, Argument, Property }
    private LhsKind _lastLhsKind = LhsKind.None;
    private JSAtom _lastLhsAtom = JSAtom.Empty;
    private int _lastLhsIndex = -1;
    private int _lastLhsBytecodePos = -1;  // Position before LHS bytecode
    private bool _lastLhsWithScope = false;
    private bool _lastLhsIsComputed = false;
    private const string WithScopeVarName = "__with__";

    /// <summary>
    /// Creates a new parser.
    /// </summary>
    /// <param name="source">The JavaScript source code.</param>
    /// <param name="fileName">The file name for error reporting.</param>
    /// <param name="atoms">The atom table for string interning.</param>
    /// <param name="isModule">True if parsing as ES module, false for script mode.</param>
    public Parser(string source, string fileName, AtomTable atoms, bool isModule = false)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _fileName = fileName ?? "<anonymous>";
        _atoms = atoms ?? throw new ArgumentNullException(nameof(atoms));
        _lexer = new Lexer(source, _fileName);
        _currentToken = _lexer.NextToken();
        _currentFunction = new JSFunctionDef();
        _isModule = isModule;
        
        // For global/module evaluation, var declarations should create global properties
        // Similar to QuickJS C: fd->is_global_var = (fd->eval_type == JS_EVAL_TYPE_GLOBAL) || ...
        _currentFunction.IsGlobalVar = true;
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

    /// <summary>
    /// Gets the diagnostics collected during parsing.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    #region Token Handling

    /// <summary>
    /// Advances to the next token.
    /// </summary>
    public void NextToken()
    {
        _currentToken = _lexer.NextToken();
    }

    private void NextTemplateToken()
    {
        _currentToken = _lexer.ScanTemplateToken();
    }

    /// <summary>
    /// Returns true if the current token matches the expected type.
    /// </summary>
    public bool Check(TokenType type) => _currentToken.Type == type;

    /// <summary>
    /// Returns true if the current token can be used as a property name.
    /// This includes identifiers, strings, numbers, and reserved words (keywords).
    /// In JavaScript, reserved words are valid as property names.
    /// </summary>
    private bool IsPropertyNameToken()
    {
        return _currentToken.Type == TokenType.Identifier ||
               _currentToken.Type == TokenType.String ||
               _currentToken.Type == TokenType.Number ||
               IsKeyword(_currentToken.Type);
    }

    /// <summary>
    /// Returns true if the token type is a keyword that can be used as a property name.
    /// </summary>
    private static bool IsKeyword(TokenType type)
    {
        return type == TokenType.If ||
               type == TokenType.Else ||
               type == TokenType.For ||
               type == TokenType.While ||
               type == TokenType.Do ||
               type == TokenType.Switch ||
               type == TokenType.Case ||
               type == TokenType.Default ||
               type == TokenType.Break ||
               type == TokenType.Continue ||
               type == TokenType.Return ||
               type == TokenType.Throw ||
               type == TokenType.Try ||
               type == TokenType.Catch ||
               type == TokenType.Finally ||
               type == TokenType.Function ||
               type == TokenType.Var ||
               type == TokenType.Let ||
               type == TokenType.Const ||
               type == TokenType.Class ||
               type == TokenType.Extends ||
               type == TokenType.New ||
               type == TokenType.This ||
               type == TokenType.Super ||
               type == TokenType.Import ||
               type == TokenType.Export ||
               type == TokenType.TypeOf ||
               type == TokenType.InstanceOf ||
               type == TokenType.In ||
               type == TokenType.Of ||
               type == TokenType.Void ||
               type == TokenType.Delete ||
               type == TokenType.Null ||
               type == TokenType.True ||
               type == TokenType.False ||
               type == TokenType.With ||
               type == TokenType.Debugger ||
               type == TokenType.Static ||
               type == TokenType.Yield ||
               type == TokenType.Await ||
               type == TokenType.Async;
    }

    /// <summary>
    /// Gets the name of the current token as a property name.
    /// Works for identifiers, strings, numbers, and keywords.
    /// </summary>
    private string GetPropertyName()
    {
        if (_currentToken.Type == TokenType.Identifier)
            return _currentToken.Text.ToString();
        if (_currentToken.Type == TokenType.String)
            return (string)_currentToken.Value!;
        if (_currentToken.Type == TokenType.Number)
            return _currentToken.Value!.ToString()!;
        // For keywords, use the token type name in lowercase
        return _currentToken.Type.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Reports a syntax error with detailed diagnostic information.
    /// </summary>
    /// <param name="code">The error code (e.g., "JS2001").</param>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location of the error.</param>
    private JSSyntaxError ReportError(string code, string message, SourceLocation location)
    {
        // Add to diagnostics
        var context = ParserErrorMessages.GetSourceContext(_source, (location.Line - 1) * 80 + location.Column);
        var diagnostic = new ParseDiagnostic(
            DiagnosticSeverity.Error,
            code,
            message,
            location,
            _fileName,
            context);
        _diagnostics.Add(diagnostic);

        // Return the exception (caller can throw it)
        return new JSSyntaxError(message, location);
    }

    /// <summary>
    /// Reports a syntax error at the current token position.
    /// </summary>
    private JSSyntaxError ReportError(string code, string message)
    {
        return ReportError(code, message, _currentToken.Start);
    }

    /// <summary>
    /// Reports an "unexpected token" error at the current position.
    /// </summary>
    private JSSyntaxError ReportUnexpectedToken()
    {
        var message = ParserErrorMessages.UnexpectedToken(_currentToken.Type, _currentToken.Value);
        return ReportError(ParseErrorCode.UnexpectedToken, message);
    }

    /// <summary>
    /// Reports an "expected X, got Y" error at the current position.
    /// </summary>
    private JSSyntaxError ReportExpectedToken(TokenType expected)
    {
        var message = ParserErrorMessages.ExpectedGot(expected, _currentToken.Type, _currentToken.Value);
        return ReportError(ParseErrorCode.ExpectedToken, message);
    }

    /// <summary>
    /// Consumes the current token if it matches, otherwise throws.
    /// </summary>
    public void Expect(TokenType type)
    {
        if (!Check(type))
        {
            throw ReportExpectedToken(type);
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

    /// <summary>
    /// Peeks at the next token without consuming the current one.
    /// Used for arrow function detection (e.g., x => ...).
    /// </summary>
    /// <param name="noLineTerminator">If true, returns LineTerminator if newline encountered.</param>
    /// <returns>The type of the next token.</returns>
    private TokenType PeekToken(bool noLineTerminator)
    {
        return _lexer.SimplePeekToken(noLineTerminator);
    }

    /// <summary>
    /// Scans forward through balanced parentheses/brackets/braces and returns
    /// the token type that follows the closing delimiter.
    /// This is used for arrow function detection: (params) => ...
    /// </summary>
    /// <param name="noLineTerminator">If true, returns LineTerminator if newline encountered after closing paren.</param>
    /// <returns>The token type following the balanced group.</returns>
    private TokenType SkipParensToken(bool noLineTerminator)
    {
        // Save current position
        var savedPos = _lexer.SavePosition();
        var savedToken = _currentToken;

        // Track nesting with a stack
        var stack = new Stack<TokenType>();
        stack.Push(TokenType.EOF); // Sentinel

        // Process tokens until we find the matching close or EOF
        while (true)
        {
            switch (_currentToken.Type)
            {
                case TokenType.LeftParen:
                case TokenType.LeftBracket:
                case TokenType.LeftBrace:
                    stack.Push(_currentToken.Type);
                    break;

                case TokenType.RightParen:
                    if (stack.Peek() != TokenType.LeftParen)
                        goto done;
                    stack.Pop();
                    break;

                case TokenType.RightBracket:
                    if (stack.Peek() != TokenType.LeftBracket)
                        goto done;
                    stack.Pop();
                    break;

                case TokenType.RightBrace:
                    if (stack.Peek() != TokenType.LeftBrace)
                        goto done;
                    stack.Pop();
                    break;

                case TokenType.EOF:
                    goto done;
            }

            NextToken();

            // Check if we've closed all brackets (stack only has sentinel)
            if (stack.Count == 1)
            {
                // We've matched the opening paren - get the next token type
                TokenType result;
                if (noLineTerminator && _currentToken.HasLineTerminatorBefore)
                {
                    result = TokenType.LineTerminator;
                }
                else
                {
                    result = _currentToken.Type;
                }

                // Restore position
                _lexer.RestorePosition(savedPos);
                _currentToken = savedToken;
                return result;
            }
        }

    done:
        // Restore position on error/mismatch
        _lexer.RestorePosition(savedPos);
        _currentToken = savedToken;
        return TokenType.EOF;
    }

    /// <summary>
    /// Looks ahead to determine whether the current for-header contains 'in' or 'of'
    /// at top-level before the first semicolon or closing paren.
    /// </summary>
    private TokenType PeekForInOfToken()
    {
        var savedPos = _lexer.SavePosition();
        var savedToken = _currentToken;
        int depth = 0;

        while (true)
        {
            var type = _currentToken.Type;
            if (depth == 0 && (type == TokenType.In || type == TokenType.Of))
            {
                _lexer.RestorePosition(savedPos);
                _currentToken = savedToken;
                return type;
            }

            if (depth == 0 && (type == TokenType.Semicolon || type == TokenType.RightParen || type == TokenType.EOF))
            {
                break;
            }

            switch (type)
            {
                case TokenType.LeftParen:
                case TokenType.LeftBracket:
                case TokenType.LeftBrace:
                    depth++;
                    break;
                case TokenType.RightParen:
                case TokenType.RightBracket:
                case TokenType.RightBrace:
                    if (depth > 0)
                    {
                        depth--;
                    }
                    break;
            }

            NextToken();
        }

        _lexer.RestorePosition(savedPos);
        _currentToken = savedToken;
        return TokenType.EOF;
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

    private void EmitWithVarOp(OpCode op, JSAtom atom, int label, bool isWith)
    {
        EmitOp(op);
        EmitAtom(atom);
        int diffPos = _currentFunction.ByteCode.Size;
        EmitU32(0);
        EmitU8(isWith ? (byte)1 : (byte)0);

        var labelInfo = _currentFunction.ByteCode.GetLabel(label);
        labelInfo.AddReference();
        labelInfo.AddRelocation(diffPos, 0);
    }

    private void EmitWithPutVarChain(JSAtom atom, int labelDone)
    {
        for (int i = _withScopeStack.Count - 1; i >= 0; i--)
        {
            EmitOp(OpCode.Dup);
            EmitOp(OpCode.GetLoc);
            EmitU16((ushort)_withScopeStack[i]);
            EmitWithVarOp(OpCode.WithPutVar, atom, labelDone, isWith: true);
            EmitOp(OpCode.Drop);
        }
    }

    private void EmitWithDeleteVarChain(JSAtom atom, int labelDone)
    {
        for (int i = _withScopeStack.Count - 1; i >= 0; i--)
        {
            EmitOp(OpCode.GetLoc);
            EmitU16((ushort)_withScopeStack[i]);
            EmitWithVarOp(OpCode.WithDeleteVar, atom, labelDone, isWith: true);
        }
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
            // Save LHS info before consuming assignment operator
            var lhsKind = _lastLhsKind;
            var lhsAtom = _lastLhsAtom;
            var lhsIndex = _lastLhsIndex;
            var lhsBytecodePos = _lastLhsBytecodePos;
            _lastLhsKind = LhsKind.None;
            var lhsWithScope = _lastLhsWithScope;
            _lastLhsWithScope = false;
            var lhsIsComputed = _lastLhsIsComputed;
            _lastLhsIsComputed = false;
            
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
                
                // Emit appropriate store based on LHS kind
                switch (lhsKind)
                {
                    case LhsKind.GlobalVar:
                        // Stack: old_value new_value, but we need obj new_value
                        // Truncate bytecode to before the GetField, emit PushThis + value + PutField
                        EmitOp(OpCode.Swap);  // new_value old_value
                        EmitOp(OpCode.Drop);  // new_value
                        EmitGlobalObject();
                        EmitOp(OpCode.Swap);  // this new_value
                        EmitOp(OpCode.PutField);
                        EmitAtom(lhsAtom);
                        EmitOp(OpCode.Drop);  // Result of compound assignment (value stays)
                        break;
                    case LhsKind.LocalVar:
                        EmitOp(OpCode.Swap);
                        EmitOp(OpCode.Drop);
                        EmitOp(OpCode.SetLoc);
                        EmitU16((ushort)lhsIndex);
                        break;
                    case LhsKind.Argument:
                        EmitOp(OpCode.Swap);
                        EmitOp(OpCode.Drop);
                        EmitOp(OpCode.SetArg);
                        EmitU16((ushort)lhsIndex);
                        break;
                    default:
                        EmitOp(OpCode.PutRefValue);
                        break;
                }
            }
            else
            {
                // Simple assignment: rewrite the bytecode
                // Truncate to before the LHS get operation, then emit store
                if (lhsKind != LhsKind.None && lhsBytecodePos >= 0)
                {
                    // Truncate bytecode to remove the get operation
                    _currentFunction.ByteCode.Truncate(lhsBytecodePos);
                }
                
                // Parse RHS (right-associative)
                ParseAssignExpression(flags);

                int labelDone = -1;
                if (lhsWithScope && _withScopeStack.Count > 0)
                {
                    labelDone = NewLabel();
                    EmitWithPutVarChain(lhsAtom, labelDone);
                }

                // Emit appropriate store based on LHS kind
                switch (lhsKind)
                {
                    case LhsKind.GlobalVar:
                        // Stack: value. Need: this value
                        EmitGlobalObject();
                        EmitOp(OpCode.Swap);  // this value
                        EmitOp(OpCode.PutField);
                        EmitAtom(lhsAtom);
                        // PutField consumes both, but assignment should return the value
                        // Re-push the value by getting it again
                        EmitGlobalObject();
                        EmitOp(OpCode.GetField);
                        EmitAtom(lhsAtom);
                        break;
                    case LhsKind.LocalVar:
                        EmitOp(OpCode.SetLoc);
                        EmitU16((ushort)lhsIndex);
                        break;
                    case LhsKind.Argument:
                        EmitOp(OpCode.SetArg);
                        EmitU16((ushort)lhsIndex);
                        break;
                    case LhsKind.Property:
                        if (lhsIsComputed)
                        {
                            EmitOp(OpCode.PutArrayEl);
                        }
                        else
                        {
                            EmitOp(OpCode.PutField);
                            EmitAtom(lhsAtom);
                        }
                        break;
                    default:
                        // Fallback to PutRefValue (might not work for all cases)
                        EmitOp(OpCode.PutRefValue);
                        break;
                }

                if (labelDone >= 0)
                {
                    EmitLabel(labelDone);
                }
            }
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
                throw ReportError(
                    ParseErrorCode.UnexpectedToken,
                    "Unary operator before '**' requires parentheses");
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
                if (Check(TokenType.Identifier))
                {
                    var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    if (PeekToken(noLineTerminator: false) == TokenType.Dot ||
                        PeekToken(noLineTerminator: false) == TokenType.LeftBracket)
                    {
                        ParsePostfixExpression(ParseFlags.None);
                        EmitOp(OpCode.Delete);
                    }
                    else
                    {
                        NextToken();
                        if (_withScopeStack.Count > 0)
                        {
                            int labelDone = NewLabel();
                            EmitWithDeleteVarChain(atom, labelDone);
                            EmitOp(OpCode.DeleteVar);
                            EmitAtom(atom);
                            EmitLabel(labelDone);
                        }
                        else
                        {
                            EmitOp(OpCode.DeleteVar);
                            EmitAtom(atom);
                        }
                    }
                }
                else
                {
                    ParsePostfixExpression(ParseFlags.None);
                    EmitOp(OpCode.Delete);
                }
                break;

            case TokenType.Increment:
                NextToken();
                ParsePostfixExpression(ParseFlags.None);
                if (_lastLhsKind != LhsKind.None)
                {
                    EmitPrefixUpdate(OpCode.Inc);
                }
                else
                {
                    EmitOp(OpCode.Inc);
                }
                break;

            case TokenType.Decrement:
                NextToken();
                ParsePostfixExpression(ParseFlags.None);
                if (_lastLhsKind != LhsKind.None)
                {
                    EmitPrefixUpdate(OpCode.Dec);
                }
                else
                {
                    EmitOp(OpCode.Dec);
                }
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
                    if (_lastLhsKind != LhsKind.None)
                    {
                        EmitPostfixUpdate(OpCode.Inc);
                    }
                    else
                    {
                        EmitOp(OpCode.PostInc);
                    }
                    continue;
                }
                if (Match(TokenType.Decrement))
                {
                    if (_lastLhsKind != LhsKind.None)
                    {
                        EmitPostfixUpdate(OpCode.Dec);
                    }
                    else
                    {
                        EmitOp(OpCode.PostDec);
                    }
                    continue;
                }
            }

            if ((flags & ParseFlags.PostfixCall) != 0)
            {
                if (Match(TokenType.LeftParen))
                {
                    int argCount = ParseArguments(out bool hasSpread);
                    if (!hasSpread)
                    {
                        EmitOp(OpCode.Call);
                        EmitU16((ushort)argCount);
                    }
                    else
                    {
                        // Stack: callee, argsArray
                        // Apply expects: callee, this, argsArray
                        EmitOp(OpCode.Undefined);
                        EmitOp(OpCode.Swap);
                        EmitOp(OpCode.Apply);
                        EmitU16(0);
                    }
                    continue;
                }
                if (Check(TokenType.Template))
                {
                    ParseTaggedTemplate(hasThisValue: false);
                    continue;
                }
            }

            if (Match(TokenType.Dot))
            {
                if (!Check(TokenType.Identifier))
                {
                    throw ReportError(
                        ParseErrorCode.ExpectedIdentifier,
                        ParserErrorMessages.ExpectedIdentifier(_currentToken.Type));
                }
                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken(); // consume the identifier

                _lastLhsKind = LhsKind.Property;
                _lastLhsAtom = atom;
                _lastLhsIndex = -1;
                _lastLhsBytecodePos = _currentFunction.ByteCode.Size;
                _lastLhsIsComputed = false;

                // Check if this is a method call: obj.method()
                if ((flags & ParseFlags.PostfixCall) != 0 && Check(TokenType.LeftParen))
                {
                    NextToken(); // consume '('
                    int argCount = ParseArguments(out bool hasSpread);
                    if (!hasSpread)
                    {
                        EmitOp(OpCode.CallMethod);
                        EmitAtom(atom);
                        EmitU16((ushort)argCount);
                    }
                    else
                    {
                        // Preserve existing call order (args evaluated before property lookup)
                        // to match the current (CallMethod-based) behavior.
                        // Stack: obj, argsArray
                        EmitOp(OpCode.Swap);      // argsArray, obj
                        EmitOp(OpCode.Dup);       // argsArray, obj, obj
                        EmitOp(OpCode.GetField);
                        EmitAtom(atom);           // argsArray, obj, callee
                        EmitOp(OpCode.Swap);      // argsArray, callee, obj
                        EmitOp(OpCode.Rot3L);     // callee, obj, argsArray
                        EmitOp(OpCode.Apply);
                        EmitU16(0);
                    }
                }
                else if ((flags & ParseFlags.PostfixCall) != 0 && Check(TokenType.Template))
                {
                    EmitOp(OpCode.GetField2);
                    EmitAtom(atom);
                    EmitOp(OpCode.Swap);
                    ParseTaggedTemplate(hasThisValue: true);
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

                _lastLhsKind = LhsKind.Property;
                _lastLhsAtom = JSAtom.Empty;
                _lastLhsIndex = -1;
                _lastLhsBytecodePos = _currentFunction.ByteCode.Size;
                _lastLhsIsComputed = true;

                EmitOp(OpCode.GetArrayEl);
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// Parses function call arguments.
    /// </summary>
    private int ParseArguments(out bool hasSpread)
    {
        int count = 0;
        hasSpread = false;

        if (!Check(TokenType.RightParen))
        {
            while (true)
            {
                if (Check(TokenType.Comma))
                {
                    throw ReportError(
                        ParseErrorCode.ExpectedExpression,
                        ParserErrorMessages.ExpectedExpression(_currentToken.Type));
                }

                if (!hasSpread)
                {
                    if (Match(TokenType.Ellipsis))
                    {
                        // Convert already-pushed args into an array and start appending.
                        EmitOp(OpCode.ArrayFrom);
                        EmitU16((ushort)count);
                        EmitOp(OpCode.PushI32);
                        EmitI32(count);

                        ParseAssignExpression();
                        EmitOp(OpCode.Append);
                        hasSpread = true;
                    }
                    else
                    {
                        ParseAssignExpression();
                        count++;
                    }
                }
                else
                {
                    if (Match(TokenType.Ellipsis))
                    {
                        ParseAssignExpression();
                        EmitOp(OpCode.Append);
                    }
                    else
                    {
                        ParseAssignExpression();
                        EmitOp(OpCode.DefineArrayEl);
                    }
                }
                if (!Match(TokenType.Comma))
                {
                    break;
                }
                if (Check(TokenType.RightParen))
                {
                    break;
                }
            }
        }

        Expect(TokenType.RightParen);

        if (hasSpread)
        {
            // Stack: argsArray, nextIndex
            // Leave just argsArray.
            EmitOp(OpCode.Drop);
            return 0;
        }
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

            case TokenType.Slash:
                _currentToken = _lexer.ScanRegExpLiteral(
                    _currentToken.Start,
                    _currentToken.Text.Start,
                    _currentToken.HasLineTerminatorBefore);
                if (_currentToken.Type != TokenType.RegExp)
                {
                    throw ReportError(ParseErrorCode.UnterminatedRegExp, "Unterminated regular expression literal");
                }
                goto case TokenType.RegExp;

            case TokenType.RegExp:
                EmitRegExpLiteral((RegExpLiteral)_currentToken.Value!);
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
                // Check for single-param arrow function: x => ...
                if (PeekToken(true) == TokenType.Arrow)
                {
                    // It's an arrow function with single identifier param
                    var paramAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken(); // consume identifier
                    ParseArrowFunctionDirect(JSFunctionKind.Normal, new List<JSAtom> { paramAtom });
                }
                else
                {
                    EmitIdentifier();
                    NextToken();
                }
                break;

            case TokenType.LeftParen:
                // Check if this is an arrow function: (...) => ...
                if (SkipParensToken(true) == TokenType.Arrow)
                {
                    // It's an arrow function - parse it
                    ParseArrowFunction(JSFunctionKind.Normal);
                }
                else
                {
                    // It's a parenthesized expression
                    ParseParenthesizedExpression();
                }
                break;

            case TokenType.LeftBracket:
                ParseArrayLiteral();
                break;

            case TokenType.LeftBrace:
                ParseObjectLiteral();
                break;

            case TokenType.Template:
                ParseTemplateLiteral();
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

            case TokenType.Class:
                // class expression
                ParseClass(isExpression: true);
                break;

            default:
                throw ReportUnexpectedToken();
        }
    }

    private TemplateLiteralToken GetTemplateToken()
    {
        if (_currentToken.Type != TokenType.Template ||
            _currentToken.Value is not TemplateLiteralToken template)
        {
            throw ReportUnexpectedToken();
        }
        return template;
    }

    private void EmitTemplateObject(IReadOnlyList<string> cookedParts, IReadOnlyList<string> rawParts)
    {
        for (int i = 0; i < cookedParts.Count; i++)
        {
            EmitStringLiteral(cookedParts[i]);
        }
        EmitOp(OpCode.ArrayFrom);
        EmitU16((ushort)cookedParts.Count);

        for (int i = 0; i < rawParts.Count; i++)
        {
            EmitStringLiteral(rawParts[i]);
        }
        EmitOp(OpCode.ArrayFrom);
        EmitU16((ushort)rawParts.Count);

        EmitOp(OpCode.DefineField);
        EmitAtom(_atoms.GetOrCreateAtom("raw"));
    }

    private void ParseTemplateLiteral()
    {
        bool hasValue = false;

        while (true)
        {
            var part = GetTemplateToken();
            if (!hasValue && (part.Cooked.Length > 0 || !part.IsTail))
            {
                EmitStringLiteral(part.Cooked);
                hasValue = true;
            }
            else if (hasValue && part.Cooked.Length > 0)
            {
                EmitStringLiteral(part.Cooked);
                EmitOp(OpCode.Add);
            }

            if (part.IsTail)
                break;

            NextToken(); // enter template expression
            ParseAssignExpression();
            if (!hasValue)
            {
                EmitStringLiteral(string.Empty);
                hasValue = true;
            }
            EmitOp(OpCode.Add);

            if (!Check(TokenType.RightBrace))
                throw ReportExpectedToken(TokenType.RightBrace);
            NextTemplateToken();
        }

        if (!hasValue)
        {
            EmitStringLiteral(string.Empty);
        }

        NextToken();
    }

    private void ParseTaggedTemplate(bool hasThisValue)
    {
        var cookedParts = new List<string>();
        var rawParts = new List<string>();

        EmitOp(OpCode.ArrayFrom);
        EmitU16(0);
        EmitOp(OpCode.PushI32);
        EmitI32(1);

        while (true)
        {
            var part = GetTemplateToken();
            cookedParts.Add(part.Cooked);
            rawParts.Add(part.Raw);

            if (part.IsTail)
                break;

            NextToken(); // enter template expression
            ParseAssignExpression();
            EmitOp(OpCode.DefineArrayEl);

            if (!Check(TokenType.RightBrace))
                throw ReportExpectedToken(TokenType.RightBrace);
            NextTemplateToken();
        }

        NextToken(); // consume tail

        EmitOp(OpCode.Drop); // drop nextIndex

        EmitOp(OpCode.PushI32);
        EmitI32(0);
        EmitTemplateObject(cookedParts, rawParts);
        EmitOp(OpCode.DefineArrayEl);
        EmitOp(OpCode.Drop);

        if (hasThisValue)
        {
            EmitOp(OpCode.Apply);
            EmitU16(0);
        }
        else
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Swap);
            EmitOp(OpCode.Apply);
            EmitU16(0);
        }
    }

    /// <summary>
    /// Parses a simple parenthesized expression: (expr)
    /// Called when we've already determined this is NOT an arrow function.
    /// </summary>
    private void ParseParenthesizedExpression()
    {
        NextToken(); // consume '('
        ParseExpression();
        Expect(TokenType.RightParen);
    }

    /// <summary>
    /// Parses an arrow function starting at '('.
    /// Called when we've already determined this IS an arrow function via lookahead.
    /// </summary>
    private void ParseArrowFunction(JSFunctionKind kind)
    {
        NextToken(); // consume '('

        // Check for empty params: () => ...
        if (Check(TokenType.RightParen))
        {
            NextToken(); // consume ')'
            ParseArrowFunctionDirect(kind, new List<JSAtom>());
            return;
        }

        // Parse parameters directly (we know this is an arrow function)
        ParseArrowFunctionWithParams(kind);
    }

    /// <summary>
    /// Parse arrow function directly when we already know the parameters.
    /// Used when we've already consumed (...params) and detected =>.
    /// </summary>
    private void ParseArrowFunctionDirect(JSFunctionKind kind, List<JSAtom> parameters)
    {
        // Save parent and create new function
        var parentFunction = _currentFunction;
        var savedWithScopes = _withScopeStack.ToArray();
        _withScopeStack.Clear();
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

        // Resolve labels before adding to parent
        _currentFunction.ByteCode.ResolveLabels();

        parentFunction.AddChildFunction(newFunction);
        int funcConstIdx = newFunction.ParentCPoolIndex;
        _currentFunction = parentFunction;
        _withScopeStack.Clear();
        _withScopeStack.AddRange(savedWithScopes);
        _withScopeStack.Clear();
        _withScopeStack.AddRange(savedWithScopes);

        EmitOp(OpCode.FClosure);
        EmitU32((uint)funcConstIdx);
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
                var paramAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
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
            else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                // Destructuring parameter: ([a, b]) => ... or ({x, y}) => ...
                hasSimpleParameterList = false;
                hasOptionalArg = true;

                // Create synthetic argument to receive the value
                var syntheticName = $"<destructuring:{_currentFunction.Args.Count}>";
                var syntheticAtom = _atoms.GetOrCreateAtom(syntheticName);
                int idx = _currentFunction.AddArg(syntheticAtom);

                // Get the argument value onto the stack
                EmitOp(OpCode.GetArg);
                EmitU16((ushort)idx);

                // Parse destructuring pattern and bind to local variables
                ParseDestructuringPattern(false, isRest);
            }
            else
            {
                throw ReportError(
                    ParseErrorCode.ExpectedIdentifier,
                    ParserErrorMessages.ExpectedIdentifier(_currentToken.Type));
            }

            if (isRest && !Check(TokenType.RightParen))
            {
                throw ReportError(
                    ParseErrorCode.RestParameterNotLast,
                    "Rest parameter must be last");
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

        // Resolve labels before adding to parent
        _currentFunction.ByteCode.ResolveLabels();

        parentFunction.AddChildFunction(newFunction);
        int funcConstIdx = newFunction.ParentCPoolIndex;
        _currentFunction = parentFunction;

        EmitOp(OpCode.FClosure);
        EmitU32((uint)funcConstIdx);
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
            ParseArrowFunction(JSFunctionKind.Async);
        }
        else if (Check(TokenType.Identifier))
        {
            // async arrow function with single param: async x => ...
            // Check for => after the identifier
            if (PeekToken(true) == TokenType.Arrow)
            {
                var paramAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken(); // consume identifier
                ParseArrowFunctionDirect(JSFunctionKind.Async, new List<JSAtom> { paramAtom });
            }
            else
            {
                throw new JSSyntaxError(
                    "Expected '=>' after async parameter",
                    _currentToken.Start);
            }
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

            var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
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

    // ========================
    // Class Parsing
    // ========================

    /// <summary>
    /// Parses a class declaration: class ClassName { ... }
    /// </summary>
    private void ParseClassDeclaration()
    {
        ParseClass(isExpression: false);
    }

    /// <summary>
    /// Parses a class expression: class { ... } or class ClassName { ... }
    /// </summary>
    private void ParseClassExpression()
    {
        ParseClass(isExpression: true);
    }

    /// <summary>
    /// Core class parsing logic, shared by declaration and expression.
    /// Follows QuickJS js_parse_class implementation.
    /// </summary>
    private void ParseClass(bool isExpression)
    {
        NextToken(); // consume 'class'

        JSAtom className = JSAtom.Empty;
        bool hasName = false;

        // Parse optional/required class name
        if (Check(TokenType.Identifier))
        {
            className = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            hasName = true;
            NextToken();
        }
        else if (!isExpression)
        {
            throw new JSSyntaxError(
                "Class declaration requires a name",
                _currentToken.Start);
        }

        // If it's a declaration, define the variable for the class (like let)
        if (!isExpression && hasName)
        {
            _currentFunction.AddVar(className, JSVarKind.Normal, isConst: false, isLexical: true);
        }

        // Parse optional extends clause
        bool hasHeritage = false;
        if (Match(TokenType.Extends))
        {
            // Parse the parent class expression
            ParseMemberExpression();
            hasHeritage = true;
        }
        else
        {
            // No parent - push undefined as the parent
            EmitOp(OpCode.Undefined);
        }

        // Define the class (pushes constructor function and prototype on stack)
        EmitOp(OpCode.DefineClass);
        EmitAtom(className);
        EmitU8((byte)(hasHeritage ? 1 : 0));

        // Expect class body
        Expect(TokenType.LeftBrace);

        // Parse class body
        ParseClassBody();

        Expect(TokenType.RightBrace);

        // The DefineClass opcode leaves the constructor on the stack.
        // If it's a declaration, store it in the variable.
        if (!isExpression && hasName)
        {
            EmitOp(OpCode.ScopePutVar);
            EmitAtom(className);
            EmitU16((ushort)_currentFunction.ScopeLevel);
        }
        // If it's an expression, the constructor stays on the stack as the result.
    }

    /// <summary>
    /// Parses the body of a class (methods, getters, setters, static members)
    /// </summary>
    private void ParseClassBody()
    {
        while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
        {
            // Skip semicolons (empty class elements)
            if (Match(TokenType.Semicolon))
                continue;

            ParseClassElement();
        }
    }

    /// <summary>
    /// Parses a single class element (method, getter, setter, field)
    /// </summary>
    private void ParseClassElement()
    {
        bool isStatic = false;
        bool isGetter = false;
        bool isSetter = false;
        bool isGenerator = false;
        bool isAsync = false;
        bool isComputed = false;
        bool isPrivate = false;
        JSAtom methodName = JSAtom.Empty;

        // Check for static keyword
        if (Check(TokenType.Static))
        {
            // Could be 'static' as a modifier or 'static' as a method name
            // Look ahead to see if it's followed by a method definition
            // Save position and token BEFORE consuming 'static' so we can restore it properly
            var savedToken = _currentToken;
            var savedPos = _lexer.SavePosition();
            NextToken(); // consume 'static'

            if (Check(TokenType.LeftBrace))
            {
                // static { ... } - static initialization block
                ParseStaticBlock();
                return;
            }
            else if (Check(TokenType.LeftParen) || Check(TokenType.Semicolon))
            {
                // 'static' is the method name itself - restore and continue to property name parsing
                _lexer.RestorePosition(savedPos);
                _currentToken = savedToken;
            }
            else
            {
                // 'static' is a modifier
                isStatic = true;
            }
        }

        // Check for async
        if (Check(TokenType.Async))
        {
            // Look ahead to determine if 'async' is a modifier or method name
            // Save position BEFORE consuming 'async' so we can restore it properly
            var savedToken = _currentToken;
            var savedPos = _lexer.SavePosition();
            NextToken(); // consume 'async'

            if (!_currentToken.HasLineTerminatorBefore &&
                !Check(TokenType.LeftParen) &&
                !Check(TokenType.Colon) &&
                !Check(TokenType.Assign) &&
                !Check(TokenType.Semicolon) &&
                !Check(TokenType.RightBrace))
            {
                // 'async' is a modifier
                isAsync = true;
            }
            else
            {
                // 'async' is the method name - restore and continue to property name parsing
                _lexer.RestorePosition(savedPos);
                _currentToken = savedToken;
            }
        }

        // Check for generator (*)
        if (Match(TokenType.Asterisk))
        {
            isGenerator = true;
        }

        // Check for get/set
        if (Check(TokenType.Identifier))
        {
            var nameSpan = _currentToken.Text.Span;
            bool isGetOrSet = nameSpan.SequenceEqual("get") || nameSpan.SequenceEqual("set");
            if (isGetOrSet)
            {
                // Save position BEFORE consuming get/set so we can restore it properly
                var savedToken = _currentToken;
                var savedPos = _lexer.SavePosition();
                NextToken(); // consume get/set

                // If followed by property name, it's a getter/setter
                if (Check(TokenType.Identifier) ||
                    Check(TokenType.String) ||
                    Check(TokenType.Number) ||
                    Check(TokenType.LeftBracket) ||
                    Check(TokenType.PrivateName))
                {
                    isGetter = nameSpan.SequenceEqual("get");
                    isSetter = nameSpan.SequenceEqual("set");
                }
                else
                {
                    // 'get' or 'set' is the method name - restore and continue to property name parsing
                    _lexer.RestorePosition(savedPos);
                    _currentToken = savedToken;
                }
            }
        }

        // Parse the property name
        if (Check(TokenType.LeftBracket))
        {
            // Computed property name: [expr]
            isComputed = true;
            NextToken(); // consume '['
            ParseAssignExpression();
            Expect(TokenType.RightBracket);
        }
        else if (Check(TokenType.PrivateName))
        {
            // Private field/method: #name
            isPrivate = true;
            methodName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            NextToken();
        }
        else if (IsPropertyNameToken())
        {
            // Regular property name (identifier, string, number, or keyword)
            if (Check(TokenType.Identifier) || IsKeyword(_currentToken.Type))
            {
                methodName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            }
            else if (Check(TokenType.String))
            {
                methodName = _atoms.GetOrCreateAtom((string)_currentToken.Value!);
            }
            else if (Check(TokenType.Number))
            {
                methodName = _atoms.GetOrCreateAtom(_currentToken.Value!.ToString()!);
            }
            else
            {
                // Fallback for any future property-name token types
                methodName = _atoms.GetOrCreateAtom(GetPropertyName());
            }
            NextToken();
        }
        else
        {
            throw new JSSyntaxError(
                "Expected method name",
                _currentToken.Start);
        }

        // Check if this is a field (has initializer or semicolon without parens)
        if (!Check(TokenType.LeftParen) && !isGetter && !isSetter)
        {
            // This is a class field
            ParseClassField(methodName, isStatic, isPrivate, isComputed);
            return;
        }

        // Parse the method
        ParseClassMethod(methodName, isStatic, isGetter, isSetter, isAsync, isGenerator, isPrivate, isComputed);
    }

    /// <summary>
    /// Parses a class field: name = value;
    /// </summary>
    private void ParseClassField(JSAtom fieldName, bool isStatic, bool isPrivate, bool isComputed)
    {
        // Field value initializer (optional)
        if (Match(TokenType.Assign))
        {
            ParseAssignExpression();
        }
        else
        {
            EmitOp(OpCode.Undefined);
        }

        // Emit field definition
        byte flags = 0;
        if (isStatic) flags |= 0x01;
        if (isPrivate) flags |= 0x02;

        if (isComputed)
        {
            EmitOp(OpCode.DefineField);
            EmitU8(flags);
        }
        else
        {
            EmitOp(OpCode.DefineField);
            EmitAtom(fieldName);
            EmitU8(flags);
        }

        // Optional semicolon
        Match(TokenType.Semicolon);
    }

    /// <summary>
    /// Parses a class method definition
    /// </summary>
    private void ParseClassMethod(
        JSAtom methodName,
        bool isStatic,
        bool isGetter,
        bool isSetter,
        bool isAsync,
        bool isGenerator,
        bool isPrivate,
        bool isComputed)
    {
        // Determine the function kind
        var funcKind = JSFunctionKind.Normal;
        if (isAsync) funcKind |= JSFunctionKind.Async;
        if (isGenerator) funcKind |= JSFunctionKind.Generator;

        // Determine parse type
        var parseType = JSParseFunctionType.Method;
        if (isGetter) parseType = JSParseFunctionType.Getter;
        if (isSetter) parseType = JSParseFunctionType.Setter;

        // Determine if this is a constructor
        bool isConstructor = !isStatic && !isGetter && !isSetter && !isPrivate &&
                             methodName == _atoms.GetOrCreateAtom("constructor");

        if (isConstructor)
        {
            parseType = JSParseFunctionType.Method;
            funcKind = JSFunctionKind.Normal;
            if (isAsync || isGenerator)
            {
                throw new JSSyntaxError(
                    "Constructor cannot be async or a generator",
                    _currentToken.Start);
            }
        }

        // Parse the method function
        ParseFunction(parseType, funcKind, methodName);

        // Emit method definition
        byte flags = 0;
        if (isStatic) flags |= 0x01;
        if (isGetter) flags |= 0x02;
        if (isSetter) flags |= 0x04;
        if (isPrivate) flags |= 0x08;

        if (isComputed)
        {
            EmitOp(OpCode.DefineMethodComputed);
            EmitU8(flags);
        }
        else
        {
            EmitOp(OpCode.DefineMethod);
            EmitAtom(methodName);
            EmitU8(flags);
        }
    }

    /// <summary>
    /// Parses a static initialization block: static { ... }
    /// </summary>
    private void ParseStaticBlock()
    {
        // Create a new function for the static block
        var parentFunction = _currentFunction;
        var savedWithScopes = _withScopeStack.ToArray();
        _withScopeStack.Clear();
        var staticBlockName = _atoms.GetOrCreateAtom("static_block");
        var blockFunction = new JSFunctionDef(staticBlockName);
        _currentFunction = blockFunction;
        _currentFunction.PushScope();

        Expect(TokenType.LeftBrace);

        // Parse the block body
        while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
        {
            ParseStatement();
        }

        Expect(TokenType.RightBrace);

        _currentFunction.PopScope();

        // Finalize the static block function
        if (!EndsWithReturn())
        {
            EmitOp(OpCode.Undefined);
            EmitOp(OpCode.Return);
        }

        // Resolve labels before adding to parent
        _currentFunction.ByteCode.ResolveLabels();

        parentFunction.AddChildFunction(blockFunction);
        int funcConstIdx = blockFunction.ParentCPoolIndex;
        _currentFunction = parentFunction;
        _withScopeStack.Clear();
        _withScopeStack.AddRange(savedWithScopes);

        // Emit the static block execution
        EmitOp(OpCode.FClosure);
        EmitU32((uint)funcConstIdx);
        EmitOp(OpCode.CallMethod);
        EmitU16(0); // no arguments
        EmitOp(OpCode.Drop); // discard result
    }

    // ========================
    // Module Parsing (ES Modules)
    // ========================

    /// <summary>
    /// Parses an import declaration.
    /// Follows QuickJS js_parse_import implementation.
    /// </summary>
    /// <remarks>
    /// Import forms:
    /// - import 'module';                       // Side effect import
    /// - import defaultExport from 'module';    // Default import
    /// - import { name } from 'module';         // Named import
    /// - import { name as alias } from 'module'; // Named import with alias
    /// - import * as ns from 'module';          // Namespace import
    /// - import defaultExport, { name } from 'module'; // Combined
    /// </remarks>
    private void ParseImportDeclaration()
    {
        if (!_isModule)
        {
            throw new JSSyntaxError(
                "import declaration is only allowed in module mode",
                _currentToken.Start);
        }

        NextToken(); // consume 'import'

        // Check for dynamic import expression: import(expr)
        if (Check(TokenType.LeftParen))
        {
            // This is actually a call expression: import(...)
            // Restore and parse as expression
            throw new JSSyntaxError(
                "Dynamic import must be used as an expression, not a statement",
                _currentToken.Start);
        }

        // Check for side-effect-only import: import 'module';
        if (Check(TokenType.String))
        {
            var moduleName = (string)_currentToken.Value!;
            var moduleAtom = _atoms.GetOrCreateAtom(moduleName);
            NextToken();

            // Register the module import (no bindings)
            _currentFunction.AddModuleRequest(moduleAtom);

            ExpectSemicolon();
            return;
        }

        // Parse import clause(s)
        bool hasDefaultImport = false;

        // Check for default import: import defaultExport from 'module'
        if (Check(TokenType.Identifier))
        {
            var localAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            var defaultAtom = _atoms.GetOrCreateAtom("default");
            NextToken();

            // Add the import binding
            _currentFunction.AddImportBinding(localAtom, defaultAtom);
            hasDefaultImport = true;

            // Check for comma (combined with named imports)
            if (!Match(TokenType.Comma))
            {
                // Just default import, expect 'from'
                ExpectContextualKeyword("from");
                ParseFromClause();
                ExpectSemicolon();
                return;
            }
        }

        // Check for namespace import: import * as ns from 'module'
        if (Check(TokenType.Asterisk))
        {
            NextToken(); // consume '*'

            ExpectContextualKeyword("as");

            if (!Check(TokenType.Identifier))
            {
                throw new JSSyntaxError(
                    "Expected identifier after 'as'",
                    _currentToken.Start);
            }

            var localAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            var starAtom = _atoms.GetOrCreateAtom("*");
            NextToken();

            // Add namespace import binding
            _currentFunction.AddImportBinding(localAtom, starAtom);

            ExpectContextualKeyword("from");
            ParseFromClause();
            ExpectSemicolon();
            return;
        }

        // Parse named imports: import { name, name as alias } from 'module'
        if (Check(TokenType.LeftBrace))
        {
            NextToken(); // consume '{'

            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
            {
                JSAtom importName;
                JSAtom localName;

                // Parse the imported name (can be string or identifier)
                if (Check(TokenType.String))
                {
                    var name = (string)_currentToken.Value!;
                    importName = _atoms.GetOrCreateAtom(name);
                    NextToken();
                }
                else if (Check(TokenType.Identifier) || IsKeyword(_currentToken.Type))
                {
                    importName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();
                }
                else
                {
                    throw new JSSyntaxError(
                        "Expected identifier or string in import specifier",
                        _currentToken.Start);
                }

                // Check for 'as alias'
                if (CheckContextualKeyword("as"))
                {
                    NextToken(); // consume 'as'

                    if (!Check(TokenType.Identifier))
                    {
                        throw new JSSyntaxError(
                            "Expected identifier after 'as'",
                            _currentToken.Start);
                    }

                    localName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();
                }
                else
                {
                    // No alias, local name is same as import name
                    localName = importName;
                }

                // Add the import binding
                _currentFunction.AddImportBinding(localName, importName);

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBrace);
        }
        else if (!hasDefaultImport)
        {
            throw new JSSyntaxError(
                "Expected import specifier",
                _currentToken.Start);
        }

        ExpectContextualKeyword("from");
        ParseFromClause();
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses the 'from "module"' clause.
    /// </summary>
    private void ParseFromClause()
    {
        if (!Check(TokenType.String))
        {
            throw new JSSyntaxError(
                "Expected module specifier string",
                _currentToken.Start);
        }

        var moduleName = (string)_currentToken.Value!;
        var moduleAtom = _atoms.GetOrCreateAtom(moduleName);
        NextToken();

        // Register the module request
        _currentFunction.AddModuleRequest(moduleAtom);
    }

    /// <summary>
    /// Parses an export declaration.
    /// Follows QuickJS js_parse_export implementation.
    /// </summary>
    /// <remarks>
    /// Export forms:
    /// - export { name };                    // Named export
    /// - export { name as alias };           // Named export with alias
    /// - export var/let/const name = ...;   // Variable export
    /// - export function name() { }          // Function export
    /// - export class Name { }               // Class export
    /// - export default expression;          // Default export
    /// - export * from 'module';             // Re-export all
    /// - export { name } from 'module';      // Re-export named
    /// </remarks>
    private void ParseExportDeclaration()
    {
        if (!_isModule)
        {
            throw new JSSyntaxError(
                "export declaration is only allowed in module mode",
                _currentToken.Start);
        }

        NextToken(); // consume 'export'

        // export class Name { }
        if (Check(TokenType.Class))
        {
            ParseClassDeclaration();
            // The class name is automatically exported
            return;
        }

        // export function name() { } or export async function name() { }
        if (Check(TokenType.Function) ||
            (Check(TokenType.Async) && PeekToken(true) == TokenType.Function))
        {
            if (Check(TokenType.Async))
                ParseAsyncFunctionDeclaration();
            else
                ParseFunctionDeclaration();
            return;
        }

        // export var/let/const
        if (Check(TokenType.Var) || Check(TokenType.Let) || Check(TokenType.Const))
        {
            ParseStatement();
            return;
        }

        // export default
        if (Check(TokenType.Default))
        {
            NextToken(); // consume 'default'

            // export default class { } or export default class Name { }
            if (Check(TokenType.Class))
            {
                ParseClassExpression();
                var defaultAtom = _atoms.GetOrCreateAtom("default");
                _currentFunction.AddExportEntry(defaultAtom, defaultAtom);
                ExpectSemicolon();
                return;
            }

            // export default function() { } or export default function name() { }
            if (Check(TokenType.Function) ||
                (Check(TokenType.Async) && PeekToken(true) == TokenType.Function))
            {
                // Parse as expression (anonymous allowed)
                ParseFunction(JSParseFunctionType.Expression, JSFunctionKind.Normal, JSAtom.Empty);
                var defaultAtom = _atoms.GetOrCreateAtom("default");
                _currentFunction.AddExportEntry(defaultAtom, defaultAtom);
                ExpectSemicolon();
                return;
            }

            // export default expression;
            ParseAssignExpression();
            var defAtom = _atoms.GetOrCreateAtom("default");
            _currentFunction.AddExportEntry(defAtom, defAtom);

            // Store in hidden _default_ variable
            var hiddenDefault = _atoms.GetOrCreateAtom("_default_");
            _currentFunction.AddVar(hiddenDefault, JSVarKind.Normal, isConst: false, isLexical: true);
            EmitOp(OpCode.ScopePutVarInit);
            EmitAtom(hiddenDefault);
            EmitU16((ushort)_currentFunction.ScopeLevel);

            ExpectSemicolon();
            return;
        }

        // export * from 'module'
        if (Check(TokenType.Asterisk))
        {
            NextToken(); // consume '*'

            // Check for 'as ns' (export * as ns from 'module')
            if (CheckContextualKeyword("as"))
            {
                NextToken(); // consume 'as'

                if (!Check(TokenType.Identifier))
                {
                    throw new JSSyntaxError(
                        "Expected identifier after 'as'",
                        _currentToken.Start);
                }

                var exportAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                var starAtom = _atoms.GetOrCreateAtom("*");
                NextToken();

                ExpectContextualKeyword("from");
                ParseFromClause();

                _currentFunction.AddExportEntry(starAtom, exportAtom);
            }
            else
            {
                ExpectContextualKeyword("from");
                ParseFromClause();
                // Star export - all exports from module are re-exported
            }

            ExpectSemicolon();
            return;
        }

        // export { name, name as alias } [from 'module']
        if (Check(TokenType.LeftBrace))
        {
            NextToken(); // consume '{'

            var exports = new List<(JSAtom localName, JSAtom exportName)>();

            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
            {
                JSAtom localName;
                JSAtom exportName;

                // Parse the local name
                if (Check(TokenType.Identifier) || IsKeyword(_currentToken.Type))
                {
                    localName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();
                }
                else
                {
                    throw new JSSyntaxError(
                        "Expected identifier in export specifier",
                        _currentToken.Start);
                }

                // Check for 'as alias'
                if (CheckContextualKeyword("as"))
                {
                    NextToken(); // consume 'as'

                    if (Check(TokenType.String))
                    {
                        var alias = (string)_currentToken.Value!;
                        exportName = _atoms.GetOrCreateAtom(alias);
                        NextToken();
                    }
                    else if (Check(TokenType.Identifier) || IsKeyword(_currentToken.Type))
                    {
                        exportName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                        NextToken();
                    }
                    else
                    {
                        throw new JSSyntaxError(
                            "Expected identifier or string after 'as'",
                            _currentToken.Start);
                    }
                }
                else
                {
                    exportName = localName;
                }

                exports.Add((localName, exportName));

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBrace);

            // Check for re-export: export { name } from 'module'
            if (CheckContextualKeyword("from"))
            {
                NextToken(); // consume 'from'
                ParseFromClause();
                // This is a re-export, bindings come from the module
            }

            // Add all export entries
            foreach (var (localName, exportName) in exports)
            {
                _currentFunction.AddExportEntry(localName, exportName);
            }

            ExpectSemicolon();
            return;
        }

        throw new JSSyntaxError(
            "Invalid export syntax",
            _currentToken.Start);
    }

    /// <summary>
    /// Checks if the current token is a contextual keyword (identifier with specific name).
    /// </summary>
    private bool CheckContextualKeyword(string keyword)
    {
        return Check(TokenType.Identifier) && _currentToken.Text.Span.SequenceEqual(keyword);
    }

    /// <summary>
    /// Expects and consumes a contextual keyword.
    /// </summary>
    private void ExpectContextualKeyword(string keyword)
    {
        if (!CheckContextualKeyword(keyword))
        {
            throw new JSSyntaxError(
                $"Expected '{keyword}'",
                _currentToken.Start);
        }
        NextToken();
    }

    private void EmitNumberLiteral()
    {
        var value = _currentToken.Value;
        var text = _currentToken.Text.Span;

        // Check if this is a BigInt literal (ends with 'n')
        bool isBigInt = text.Length > 0 && text[text.Length - 1] == 'n';

        if (isBigInt)
        {
            // BigInt literal
            if (value is long l64)
            {
                // Small BigInt - emit as short BigInt
                if (l64 >= int.MinValue && l64 <= int.MaxValue)
                {
                    EmitOp(OpCode.PushI32);
                    EmitI32((int)l64);
                    // Convert int to BigInt
                    EmitOp(OpCode.ToBigInt);
                }
                else
                {
                    // Larger BigInt - add to constant pool as JSBigInt
                    var bigInt = new JSBigInt(l64);
                    int idx = _currentFunction.Constants.AddBigInt(bigInt);
                    EmitOp(OpCode.PushConst);
                    EmitU32((uint)idx);
                }
            }
            else if (value is int i32)
            {
                EmitOp(OpCode.PushI32);
                EmitI32(i32);
                EmitOp(OpCode.ToBigInt);
            }
            else if (value is System.Numerics.BigInteger bigVal)
            {
                // Very large BigInt - add to constant pool
                var bigInt = new JSBigInt(bigVal);
                int idx = _currentFunction.Constants.AddBigInt(bigInt);
                EmitOp(OpCode.PushConst);
                EmitU32((uint)idx);
            }
            else
            {
                throw new JSSyntaxError(
                    $"Invalid BigInt literal: {text.ToString()}",
                    _currentToken.Start);
            }
        }
        else if (value is int i32)
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
                // Large number - add to constant pool
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

    private void EmitStringLiteral(string value)
    {
        int idx = _currentFunction.Constants.AddString(value);
        EmitOp(OpCode.PushConst);
        EmitU32((uint)idx);
    }

    private void EmitRegExpLiteral(RegExpLiteral literal)
    {
        EmitStringLiteral(literal.Pattern);
        EmitStringLiteral(literal.Flags);
        EmitOp(OpCode.RegExp);
    }

    private void EmitIdentifier()
    {
        var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);

        var argumentsAtom = _atoms.GetOrCreateAtom("arguments");
        if (atom == argumentsAtom && _currentFunction.HasArgumentsBinding)
        {
            _lastLhsKind = LhsKind.None;
            _lastLhsAtom = JSAtom.Empty;
            _lastLhsIndex = -1;
            _lastLhsBytecodePos = -1;
            _lastLhsWithScope = false;
            _lastLhsIsComputed = false;

            EmitOp(OpCode.SpecialObject);
            EmitU8((byte)(_currentFunction.IsStrict ? SpecialObjectType.Arguments : SpecialObjectType.MappedArguments));
            return;
        }
        
        // Track LHS info for potential assignment
        _lastLhsBytecodePos = _currentFunction.ByteCode.Size;
        _lastLhsWithScope = _withScopeStack.Count > 0;

        LhsKind resolvedKind;
        int resolvedIndex = -1;

        // First check if this is a function argument
        int argIdx = _currentFunction.FindArg(atom);
        if (argIdx >= 0)
        {
            resolvedKind = LhsKind.Argument;
            resolvedIndex = argIdx;
        }
        else
        {
            // Check if this is a known local variable
            int varIdx = _currentFunction.FindVar(atom);
            if (varIdx >= 0)
            {
                var varDef = _currentFunction.GetVarDef(varIdx);
                if (_currentFunction.IsGlobalVar && !varDef.IsLexical)
                {
                    resolvedKind = LhsKind.GlobalVar;
                }
                else
                {
                    resolvedKind = LhsKind.LocalVar;
                    resolvedIndex = varIdx;
                }
            }
            else
            {
                // Fall back to lexical variables in active scopes
                int lexIdx = -1;
                for (int i = _currentFunction.Vars.Count - 1; i >= 0; i--)
                {
                    var varDef = _currentFunction.Vars[i];
                    if (varDef.Name.Equals(atom) && varDef.ScopeLevel <= _currentFunction.ScopeLevel)
                    {
                        lexIdx = i;
                        break;
                    }
                }

                if (lexIdx >= 0)
                {
                    resolvedKind = LhsKind.LocalVar;
                    resolvedIndex = lexIdx;
                }
                else
                {
                    resolvedKind = LhsKind.GlobalVar;
                }
            }
        }

        _lastLhsKind = resolvedKind;
        _lastLhsAtom = atom;
        _lastLhsIndex = resolvedIndex;

        if (_withScopeStack.Count > 0)
        {
            int labelDone = NewLabel();
            for (int i = _withScopeStack.Count - 1; i >= 0; i--)
            {
                EmitOp(OpCode.GetLoc);
                EmitU16((ushort)_withScopeStack[i]);
                EmitWithVarOp(OpCode.WithGetVar, atom, labelDone, isWith: true);
            }

            EmitIdentifierGet(resolvedKind, atom, resolvedIndex);
            EmitLabel(labelDone);
        }
        else
        {
            EmitIdentifierGet(resolvedKind, atom, resolvedIndex);
        }
    }

    private void EmitIdentifierGet(LhsKind kind, JSAtom atom, int index)
    {
        switch (kind)
        {
            case LhsKind.Argument:
                switch (index)
                {
                    case 0: EmitOp(OpCode.GetArg0); break;
                    case 1: EmitOp(OpCode.GetArg1); break;
                    case 2: EmitOp(OpCode.GetArg2); break;
                    case 3: EmitOp(OpCode.GetArg3); break;
                    default:
                        EmitOp(OpCode.GetArg);
                        EmitU16((ushort)index);
                        break;
                }
                break;
            case LhsKind.LocalVar:
                EmitOp(OpCode.GetLoc);
                EmitU16((ushort)index);
                break;
            case LhsKind.GlobalVar:
                EmitGlobalObject();
                EmitOp(OpCode.GetField);
                EmitAtom(atom);
                break;
        }
    }

    private void EmitGlobalObject()
    {
        EmitOp(OpCode.SpecialObject);
        EmitU8((byte)SpecialObjectType.VarObject);
    }

    private void EmitPrefixUpdate(OpCode updateOp)
    {
        var lhsKind = _lastLhsKind;
        var lhsAtom = _lastLhsAtom;
        var lhsIndex = _lastLhsIndex;
        _lastLhsKind = LhsKind.None;

        EmitOp(updateOp);

        switch (lhsKind)
        {
            case LhsKind.GlobalVar:
                EmitOp(OpCode.Dup);
                EmitGlobalObject();
                EmitOp(OpCode.Swap);
                EmitOp(OpCode.PutField);
                EmitAtom(lhsAtom);
                break;
            case LhsKind.LocalVar:
                EmitOp(OpCode.SetLoc);
                EmitU16((ushort)lhsIndex);
                break;
            case LhsKind.Argument:
                EmitOp(OpCode.SetArg);
                EmitU16((ushort)lhsIndex);
                break;
        }
    }

    private void EmitPostfixUpdate(OpCode updateOp)
    {
        var lhsKind = _lastLhsKind;
        var lhsAtom = _lastLhsAtom;
        var lhsIndex = _lastLhsIndex;
        _lastLhsKind = LhsKind.None;

        EmitOp(OpCode.Dup);
        EmitOp(updateOp);

        switch (lhsKind)
        {
            case LhsKind.GlobalVar:
                EmitGlobalObject();
                EmitOp(OpCode.Swap);
                EmitOp(OpCode.PutField);
                EmitAtom(lhsAtom);
                break;
            case LhsKind.LocalVar:
                EmitOp(OpCode.SetLoc);
                EmitU16((ushort)lhsIndex);
                break;
            case LhsKind.Argument:
                EmitOp(OpCode.SetArg);
                EmitU16((ushort)lhsIndex);
                break;
        }

        EmitOp(OpCode.Drop);
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
            
            if (Check(TokenType.Identifier))
            {
                propertyName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();
            }
            else if (Check(TokenType.String))
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
            if (Check(TokenType.Identifier) && _currentToken.Text.Span.SequenceEqual("target"))
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
        EmitOp(OpCode.Dup); // newTarget is the same as constructor

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
                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();

                _lastLhsKind = LhsKind.Property;
                _lastLhsAtom = atom;
                _lastLhsIndex = -1;
                _lastLhsBytecodePos = _currentFunction.ByteCode.Size;
                _lastLhsIsComputed = false;

                EmitOp(OpCode.GetField);
                EmitAtom(atom);
            }
            else if (Match(TokenType.LeftBracket))
            {
                // Computed property access: obj[expr]
                ParseExpression();
                Expect(TokenType.RightBracket);

                _lastLhsKind = LhsKind.Property;
                _lastLhsAtom = JSAtom.Empty;
                _lastLhsIndex = -1;
                _lastLhsBytecodePos = _currentFunction.ByteCode.Size;
                _lastLhsIsComputed = true;

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
        if (_isModule)
        {
            while (!Check(TokenType.EOF))
            {
                ParseStatement(preserveCompletionValue: false);
            }
        }
        else
        {
            // Track the completion value of the script (like QuickJS eval_ret).
            // Ensure exactly one value is left on the stack when the program finishes.
            EmitOp(OpCode.Undefined);
            while (!Check(TokenType.EOF))
            {
                // Drop previous statement's completion value (initially undefined)
                // before evaluating the next statement.
                EmitOp(OpCode.Drop);
                ParseStatement(preserveCompletionValue: true);
            }
        }
        
        // Resolve labels to convert label indices to relative offsets
        _currentFunction.ByteCode.ResolveLabels();
    }

    /// <summary>
    /// Parses a statement.
    /// </summary>
    public void ParseStatement()
    {
        ParseStatement(preserveCompletionValue: false);
    }

    private void ParseStatement(bool preserveCompletionValue)
    {
        if (Check(TokenType.Identifier) && PeekToken(noLineTerminator: false) == TokenType.Colon)
        {
            ParseLabeledStatement(preserveCompletionValue);
            return;
        }

        switch (_currentToken.Type)
        {
            case TokenType.LeftBrace:
                ParseBlockStatement(preserveCompletionValue);
                break;

            case TokenType.Var:
                ParseVarStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Let:
                ParseLetStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Const:
                ParseConstStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.If:
                ParseIfStatement(preserveCompletionValue);
                break;

            case TokenType.While:
                ParseWhileStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Do:
                ParseDoWhileStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.For:
                ParseForStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Return:
                ParseReturnStatement();
                break;

            case TokenType.With:
                ParseWithStatement(preserveCompletionValue);
                break;

            case TokenType.Break:
                ParseBreakStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Continue:
                ParseContinueStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Throw:
                ParseThrowStatement();
                break;

            case TokenType.Try:
                ParseTryStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Switch:
                ParseSwitchStatement();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Semicolon:
                // Empty statement
                NextToken();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Function:
                ParseFunctionDeclaration();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Async:
                // async function declaration
                ParseAsyncFunctionDeclaration();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Class:
                // class declaration
                ParseClassDeclaration();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Import:
                // import declaration (module only)
                ParseImportDeclaration();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            case TokenType.Export:
                // export declaration (module only)
                ParseExportDeclaration();
                if (preserveCompletionValue) EmitOp(OpCode.Undefined);
                break;

            default:
                ParseExpressionStatement(preserveCompletionValue);
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
    public void ParseBlockStatement(bool preserveCompletionValue = false)
    {
        Expect(TokenType.LeftBrace);
        _currentFunction.PushScope();

        if (preserveCompletionValue)
        {
            EmitOp(OpCode.Undefined);
            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
            {
                EmitOp(OpCode.Drop);
                ParseStatement(preserveCompletionValue: true);
            }
        }
        else
        {
            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF))
            {
                ParseStatement(preserveCompletionValue: false);
            }
        }

        _currentFunction.PopScope();
        Expect(TokenType.RightBrace);
    }

    /// <summary>
    /// Parses an expression statement: expr;
    /// </summary>
    public void ParseExpressionStatement(bool preserveCompletionValue = false)
    {
        ParseExpression();
        if (!preserveCompletionValue)
        {
            EmitOp(OpCode.Drop);  // Discard expression result
        }
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
    /// Supports both simple identifiers and destructuring patterns.
    /// </summary>
    private void ParseVariableDeclarationList(JSVarKind kind, bool isLexical, bool isConst)
    {
        do
        {
            // Check for destructuring pattern
            if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                // Destructuring requires an initializer
                ParseDestructuringBindingPattern(kind, isLexical, isConst);
                
                if (!Match(TokenType.Assign))
                {
                    throw new JSSyntaxError(
                        "Destructuring requires an initializer",
                        _currentToken.Start);
                }

                // Parse initializer
                ParseAssignExpression();

                // Apply destructuring to the value on stack
                ApplyDestructuringAssignment(isLexical);
            }
            else if (Check(TokenType.Identifier))
            {
                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();

                // For global var declarations (non-lexical in global scope), 
                // set a property on the global object instead of a local variable.
                // This matches JavaScript semantics where top-level var creates globals.
                bool useGlobalProperty = _currentFunction.IsGlobalVar && !isLexical;
                
                // Still add var to track it, but we won't use local storage for global vars
                int varIdx = _currentFunction.AddVar(atom, kind, isConst, isLexical);

                if (Match(TokenType.Assign))
                {
                    // Parse initializer
                    ParseAssignExpression();

                    if (useGlobalProperty)
                    {
                        // Stack: [value]
                        // Get global object (this in global scope), then set property
                        EmitGlobalObject();              // Stack: [value, globalThis]
                        EmitOp(OpCode.Swap);             // Stack: [globalThis, value]
                        EmitOp(OpCode.PutField);         // Stack: [] (PutField pops obj and value)
                        EmitAtom(atom);
                    }
                    else
                    {
                        // Store the value using PutLoc (direct local variable access)
                        EmitOp(OpCode.PutLoc);
                        EmitU16((ushort)varIdx);
                    }
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
                    EmitOp(OpCode.PutLoc);
                    EmitU16((ushort)varIdx);
                }
                else if (useGlobalProperty)
                {
                    // Initialize global var to undefined
                    EmitOp(OpCode.Undefined);
                    EmitGlobalObject();
                    EmitOp(OpCode.Swap);
                    EmitOp(OpCode.PutField);             // Stack: [] (PutField pops obj and value)
                    EmitAtom(atom);
                }
            }
            else
            {
                throw new JSSyntaxError(
                    $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                    _currentToken.Start);
            }
        }
        while (Match(TokenType.Comma));
    }

    /// <summary>
    /// Stack for collecting destructuring binding names during pattern parsing.
    /// </summary>
    private readonly System.Collections.Generic.List<DestructuringBinding> _destructuringBindings = new();

    /// <summary>
    /// Represents a binding in a destructuring pattern.
    /// </summary>
    private struct DestructuringBinding
    {
        public JSAtom Name;
        public bool HasDefault;
        public bool IsRest;
        public DestructuringBindingType Type;
    }

    private enum DestructuringBindingType
    {
        ArrayElement,
        ArrayRest,
        ObjectProperty,
        ObjectShorthand,
        ObjectRest
    }

    /// <summary>
    /// Parses a destructuring binding pattern and collects bindings for later variable definition.
    /// Does NOT consume the pattern - just parses and records the structure.
    /// </summary>
    private void ParseDestructuringBindingPattern(JSVarKind kind, bool isLexical, bool isConst)
    {
        _destructuringBindings.Clear();
        CollectDestructuringBindings(kind, isLexical, isConst);
    }

    private void SkipDestructuringPatternTokens()
    {
        if (Check(TokenType.LeftBracket))
        {
            NextToken();

            while (!Check(TokenType.RightBracket))
            {
                if (Check(TokenType.Comma))
                {
                    NextToken();
                    continue;
                }

                bool isRest = Match(TokenType.Ellipsis);

                if (Check(TokenType.Identifier))
                {
                    NextToken();
                }
                else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                {
                    SkipDestructuringPatternTokens();
                }
                else if (!Check(TokenType.RightBracket))
                {
                    throw new JSSyntaxError(
                        $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                        _currentToken.Start);
                }

                if (Check(TokenType.Assign))
                {
                    NextToken();
                    SkipExpression();
                }

                if (isRest && !Check(TokenType.RightBracket))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBracket);
        }
        else if (Check(TokenType.LeftBrace))
        {
            NextToken();

            while (!Check(TokenType.RightBrace))
            {
                bool isRest = Match(TokenType.Ellipsis);

                if (isRest)
                {
                    if (Check(TokenType.Identifier))
                    {
                        NextToken();
                    }
                    else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                    {
                        SkipDestructuringPatternTokens();
                    }
                    else
                    {
                        throw new JSSyntaxError(
                            $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                            _currentToken.Start);
                    }
                }
                else
                {
                    if (Check(TokenType.LeftBracket))
                    {
                        NextToken();
                        SkipExpression();
                        Expect(TokenType.RightBracket);
                    }
                    else if (Check(TokenType.Identifier) || Check(TokenType.String) || Check(TokenType.Number))
                    {
                        NextToken();
                    }
                    else
                    {
                        throw new JSSyntaxError(
                            $"Expected property name, got {_currentToken.Type}",
                            _currentToken.Start);
                    }

                    if (Match(TokenType.Colon))
                    {
                        if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                        {
                            SkipDestructuringPatternTokens();
                        }
                        else if (Check(TokenType.Identifier))
                        {
                            NextToken();
                        }
                        else
                        {
                            throw new JSSyntaxError(
                                $"Expected identifier in destructuring pattern, got {_currentToken.Type}",
                                _currentToken.Start);
                        }
                    }
                }

                if (Check(TokenType.Assign))
                {
                    NextToken();
                    SkipExpression();
                }

                if (isRest && !Check(TokenType.RightBrace))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBrace);
        }
    }

    /// <summary>
    /// Recursively collects destructuring bindings from the pattern.
    /// </summary>
    private void CollectDestructuringBindings(JSVarKind kind, bool isLexical, bool isConst)
    {
        if (Check(TokenType.LeftBracket))
        {
            // Array destructuring: [a, b, ...rest]
            NextToken();

            while (!Check(TokenType.RightBracket))
            {
                // Handle elision (empty slots)
                if (Check(TokenType.Comma))
                {
                    NextToken();
                    continue;
                }

                bool isRest = Match(TokenType.Ellipsis);

                if (Check(TokenType.Identifier))
                {
                    var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();

                    // Define the variable
                    _currentFunction.AddVar(atom, kind, isConst, isLexical);

                    _destructuringBindings.Add(new DestructuringBinding
                    {
                        Name = atom,
                        IsRest = isRest,
                        HasDefault = Check(TokenType.Assign),
                        Type = isRest ? DestructuringBindingType.ArrayRest : DestructuringBindingType.ArrayElement
                    });

                    // Skip default value syntax for now (we'll handle it during assignment)
                    if (Check(TokenType.Assign))
                    {
                        NextToken();
                        SkipExpression();
                    }
                }
                else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                {
                    // Nested pattern
                    CollectDestructuringBindings(kind, isLexical, isConst);
                }
                else if (!Check(TokenType.RightBracket))
                {
                    throw new JSSyntaxError(
                        $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                        _currentToken.Start);
                }

                if (isRest && !Check(TokenType.RightBracket))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBracket);
        }
        else if (Check(TokenType.LeftBrace))
        {
            // Object destructuring: { a, b: c, ...rest }
            NextToken();

            while (!Check(TokenType.RightBrace))
            {
                bool isRest = Match(TokenType.Ellipsis);

                if (Check(TokenType.Identifier))
                {
                    var propAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();

                    JSAtom varAtom;
                    DestructuringBindingType bindingType;

                    if (isRest)
                    {
                        // Rest: ...rest
                        varAtom = propAtom;
                        bindingType = DestructuringBindingType.ObjectRest;
                        _currentFunction.AddVar(varAtom, kind, isConst, isLexical);
                    }
                    else if (Match(TokenType.Colon))
                    {
                        // Renaming: { prop: newName }
                        if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                        {
                            // Nested pattern
                            CollectDestructuringBindings(kind, isLexical, isConst);
                            
                            if (!Match(TokenType.Comma))
                                break;
                            continue;
                        }

                        if (!Check(TokenType.Identifier))
                        {
                            throw new JSSyntaxError(
                                "Expected identifier after ':'",
                                _currentToken.Start);
                        }
                        varAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                        NextToken();
                        bindingType = DestructuringBindingType.ObjectProperty;
                        _currentFunction.AddVar(varAtom, kind, isConst, isLexical);
                    }
                    else
                    {
                        // Shorthand: { prop }
                        varAtom = propAtom;
                        bindingType = DestructuringBindingType.ObjectShorthand;
                        _currentFunction.AddVar(varAtom, kind, isConst, isLexical);
                    }

                    _destructuringBindings.Add(new DestructuringBinding
                    {
                        Name = varAtom,
                        IsRest = isRest,
                        HasDefault = Check(TokenType.Assign),
                        Type = bindingType
                    });

                    // Skip default value syntax
                    if (Check(TokenType.Assign))
                    {
                        NextToken();
                        SkipExpression();
                    }
                }
                else if (!Check(TokenType.RightBrace))
                {
                    throw new JSSyntaxError(
                        $"Expected identifier in destructuring pattern, got {_currentToken.Type}",
                        _currentToken.Start);
                }

                if (isRest && !Check(TokenType.RightBrace))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }

                if (!Match(TokenType.Comma))
                    break;
            }

            Expect(TokenType.RightBrace);
        }
    }

    /// <summary>
    /// Skips an expression without generating bytecode.
    /// Used for collecting destructuring pattern structure.
    /// </summary>
    private void SkipExpression()
    {
        int depth = 0;
        while (!Check(TokenType.EOF))
        {
            if (Check(TokenType.LeftParen) || Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                depth++;
            }
            else if (Check(TokenType.RightParen) || Check(TokenType.RightBracket) || Check(TokenType.RightBrace))
            {
                if (depth == 0)
                    break;
                depth--;
            }
            else if (depth == 0 && (Check(TokenType.Comma) || Check(TokenType.Semicolon)))
            {
                break;
            }
            NextToken();
        }
    }

    /// <summary>
    /// Applies destructuring assignment from value on stack.
    /// The value to destructure should be on the stack.
    /// </summary>
    private void ApplyDestructuringAssignment(bool isLexical)
    {
        // For now, we emit a simplified version
        // A full implementation would match the collected bindings
        // and emit appropriate bytecode for each element
        
        // Drop the source value for now (placeholder implementation)
        EmitOp(OpCode.Drop);
    }

    #endregion

    #region Control Flow Statements

    /// <summary>
    /// Parses an if statement: if (expr) stmt [else stmt]
    /// </summary>
    public void ParseIfStatement(bool preserveCompletionValue = false)
    {
        Expect(TokenType.If);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);

        int labelElse = NewLabel();
        EmitGoto(OpCode.IfFalse, labelElse);

        ParseStatement(preserveCompletionValue);

        if (Match(TokenType.Else))
        {
            int labelEnd = NewLabel();
            EmitGoto(OpCode.Goto, labelEnd);
            EmitLabel(labelElse);
            ParseStatement(preserveCompletionValue);
            EmitLabel(labelEnd);
        }
        else
        {
            EmitLabel(labelElse);
            if (preserveCompletionValue)
            {
                // If the condition is false and there is no else, completion is undefined.
                EmitOp(OpCode.Undefined);
            }
        }
    }

    /// <summary>
    /// Parses a while statement: while (expr) stmt
    /// </summary>
    public void ParseWhileStatement(string? labelName = null)
    {
        Expect(TokenType.While);

        int labelContinue = NewLabel();
        int labelBreak = NewLabel();

        // Push break/continue context
        PushBreakContext(labelBreak, labelContinue, labelName);

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
    public void ParseDoWhileStatement(string? labelName = null)
    {
        Expect(TokenType.Do);

        int labelBody = NewLabel();
        int labelContinue = NewLabel();
        int labelBreak = NewLabel();

        PushBreakContext(labelBreak, labelContinue, labelName);

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
    public void ParseForStatement(string? labelName = null)
    {
        Expect(TokenType.For);
        bool isAwait = Match(TokenType.Await);
        Expect(TokenType.LeftParen);

        var forInOfToken = PeekForInOfToken();
        if (forInOfToken == TokenType.In || forInOfToken == TokenType.Of)
        {
            ParseForInOfStatement(isAwait, labelName);
            return;
        }

        if (isAwait)
        {
            throw new JSSyntaxError(
                "for await only supports for-of",
                _currentToken.Start);
        }

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

        PushBreakContext(labelBreak, labelContinue, labelName);

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

    private void ParseWithStatement(bool preserveCompletionValue)
    {
        Expect(TokenType.With);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);
        EmitOp(OpCode.ToObject);

        _currentFunction.PushScope();
        var withAtom = _atoms.GetOrCreateAtom(WithScopeVarName);
        int withIdx = _currentFunction.AddVar(withAtom, JSVarKind.Normal, isConst: false, isLexical: true);
        EmitOp(OpCode.PutLoc);
        EmitU16((ushort)withIdx);

        _withScopeStack.Add(withIdx);
        ParseStatement(preserveCompletionValue);
        _withScopeStack.RemoveAt(_withScopeStack.Count - 1);
        _currentFunction.PopScope();
    }

    private void ParseLabeledStatement(bool preserveCompletionValue)
    {
        string labelName = _currentToken.Text.ToString();

        foreach (var entry in _breakStack)
        {
            if (entry.LabelName == labelName)
            {
                throw new JSSyntaxError(
                    "Duplicate label name",
                    _currentToken.Start);
            }
        }

        NextToken();
        Expect(TokenType.Colon);

        int labelBreak = NewLabel();

        switch (_currentToken.Type)
        {
            case TokenType.For:
                ParseForStatement(labelName);
                break;
            case TokenType.While:
                ParseWhileStatement(labelName);
                break;
            case TokenType.Do:
                ParseDoWhileStatement(labelName);
                break;
            case TokenType.Switch:
                ParseSwitchStatement(labelName);
                break;
            default:
                PushBreakContext(labelBreak, -1, labelName);
                ParseStatement(preserveCompletionValue);
                EmitLabel(labelBreak);
                PopBreakContext();
                break;
        }
    }

    private struct ForInOfTarget
    {
        public bool IsDestructuring;
        public bool IsLexical;
        public LhsKind Kind;
        public JSAtom Atom;
        public int Index;
    }

    private void EmitForInOfAssignment(in ForInOfTarget target)
    {
        if (target.IsDestructuring)
        {
            ApplyDestructuringAssignment(target.IsLexical);
            return;
        }

        switch (target.Kind)
        {
            case LhsKind.GlobalVar:
                EmitGlobalObject();
                EmitOp(OpCode.Swap);
                EmitOp(OpCode.PutField);
                EmitAtom(target.Atom);
                break;
            case LhsKind.LocalVar:
                EmitOp(OpCode.SetLoc);
                EmitU16((ushort)target.Index);
                break;
            case LhsKind.Argument:
                EmitOp(OpCode.SetArg);
                EmitU16((ushort)target.Index);
                break;
            default:
                EmitOp(OpCode.PutRefValue);
                break;
        }
    }

    private ForInOfTarget ParseForInOfLeftHandSide(bool isAwait, out bool isForOf)
    {
        bool isDeclaration = false;
        bool isLexical = false;
        bool isConst = false;
        var target = new ForInOfTarget();

        if (Check(TokenType.Var) || Check(TokenType.Let) || Check(TokenType.Const))
        {
            isDeclaration = true;
            if (Match(TokenType.Var))
            {
                isLexical = false;
            }
            else if (Match(TokenType.Let))
            {
                isLexical = true;
            }
            else
            {
                Expect(TokenType.Const);
                isLexical = true;
                isConst = true;
            }

            if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                ParseDestructuringBindingPattern(JSVarKind.Normal, isLexical, isConst);
                target.IsDestructuring = true;
                target.IsLexical = isLexical;
            }
            else if (Check(TokenType.Identifier))
            {
                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();

                int varIdx = _currentFunction.AddVar(atom, JSVarKind.Normal, isConst, isLexical);
                bool useGlobalProperty = _currentFunction.IsGlobalVar && !isLexical;
                target.Kind = useGlobalProperty ? LhsKind.GlobalVar : LhsKind.LocalVar;
                target.Atom = atom;
                target.Index = varIdx;
            }
            else
            {
                throw new JSSyntaxError(
                    $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                    _currentToken.Start);
            }
        }
        else if (Check(TokenType.Identifier))
        {
            var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
            NextToken();

            int argIdx = _currentFunction.FindArg(atom);
            if (argIdx >= 0)
            {
                target.Kind = LhsKind.Argument;
                target.Atom = atom;
                target.Index = argIdx;
            }
            else
            {
                int varIdx = _currentFunction.FindVar(atom);
                if (varIdx >= 0)
                {
                    var varDef = _currentFunction.GetVarDef(varIdx);
                    bool useGlobalProperty = _currentFunction.IsGlobalVar && !varDef.IsLexical;
                    target.Kind = useGlobalProperty ? LhsKind.GlobalVar : LhsKind.LocalVar;
                    target.Index = varIdx;
                }
                else
                {
                    target.Kind = LhsKind.GlobalVar;
                }
                target.Atom = atom;
            }
        }
        else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
        {
            if (isDeclaration)
            {
                ParseDestructuringBindingPattern(JSVarKind.Normal, isLexical, isConst);
                target.IsDestructuring = true;
                target.IsLexical = isLexical;
            }
            else
            {
                SkipDestructuringPatternTokens();
                target.IsDestructuring = true;
                target.IsLexical = false;
            }
        }
        else
        {
            throw new JSSyntaxError(
                $"Expected identifier or destructuring pattern, got {_currentToken.Type}",
                _currentToken.Start);
        }

        if (Match(TokenType.Assign))
        {
            throw new JSSyntaxError(
                "Initializer not allowed in for-in/of declaration",
                _currentToken.Start);
        }

        if (Match(TokenType.Comma))
        {
            throw new JSSyntaxError(
                "Only a single binding is allowed in for-in/of",
                _currentToken.Start);
        }

        if (Match(TokenType.Of))
        {
            isForOf = true;
        }
        else if (Match(TokenType.In))
        {
            isForOf = false;
        }
        else
        {
            throw new JSSyntaxError(
                "Expected 'in' or 'of' in for statement",
                _currentToken.Start);
        }

        if (isAwait && !isForOf)
        {
            throw new JSSyntaxError(
                "for await only supports for-of",
                _currentToken.Start);
        }

        return target;
    }

    private void ParseForInOfStatement(bool isAwait, string? labelName)
    {
        _currentFunction.PushScope();

        int labelNext = NewLabel();
        int labelBody = NewLabel();
        int labelContinue = NewLabel();
        int labelBreak = NewLabel();
        int labelExpr = NewLabel();

        EmitGoto(OpCode.Goto, labelExpr);
        EmitLabel(labelNext);

        var target = ParseForInOfLeftHandSide(isAwait, out bool isForOf);
        EmitForInOfAssignment(target);
        EmitGoto(OpCode.Goto, labelBody);

        EmitLabel(labelExpr);
        if (isForOf)
        {
            ParseAssignExpression();
            EmitOp(isAwait ? OpCode.ForAwaitOfStart : OpCode.ForOfStart);
        }
        else
        {
            ParseExpression();
            EmitOp(OpCode.ForInStart);
        }
        EmitGoto(OpCode.Goto, labelContinue);
        Expect(TokenType.RightParen);

        PushBreakContext(labelBreak, labelContinue, labelName);
        EmitLabel(labelBody);
        ParseStatement();

        EmitLabel(labelContinue);
        if (isForOf)
        {
            if (isAwait)
            {
                EmitOp(OpCode.ForAwaitOfNext);
                EmitOp(OpCode.Await);
                EmitOp(OpCode.IteratorGetValueDone);
            }
            else
            {
                EmitOp(OpCode.ForOfNext);
                EmitU8(0);
            }
        }
        else
        {
            EmitOp(OpCode.ForInNext);
        }
        EmitGoto(OpCode.IfFalse, labelNext);
        EmitOp(OpCode.Drop);

        EmitLabel(labelBreak);
        if (isForOf)
        {
            EmitOp(OpCode.IteratorClose);
        }
        else
        {
            EmitOp(OpCode.Drop);
        }
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

        string? labelName = null;
        if (Check(TokenType.Identifier) && !_currentToken.HasLineTerminatorBefore)
        {
            labelName = _currentToken.Text.ToString();
            NextToken();
        }

        BreakContext ctx;
        if (labelName == null)
        {
            if (_breakStack.Count == 0)
            {
                throw new JSSyntaxError(
                    "break statement not inside loop or switch",
                    _currentToken.Start);
            }
            ctx = _breakStack.Peek();
        }
        else if (!TryFindBreakContext(labelName, requireContinue: false, out ctx))
        {
            throw new JSSyntaxError(
                $"Undefined label '{labelName}'",
                _currentToken.Start);
        }

        EmitGoto(OpCode.Goto, ctx.BreakLabel);
        ExpectSemicolon();
    }

    /// <summary>
    /// Parses a continue statement: continue [label];
    /// </summary>
    public void ParseContinueStatement()
    {
        Expect(TokenType.Continue);

        string? labelName = null;
        if (Check(TokenType.Identifier) && !_currentToken.HasLineTerminatorBefore)
        {
            labelName = _currentToken.Text.ToString();
            NextToken();
        }

        BreakContext ctx;
        if (labelName == null)
        {
            if (_breakStack.Count == 0)
            {
                throw new JSSyntaxError(
                    "continue statement not inside loop",
                    _currentToken.Start);
            }
            ctx = _breakStack.Peek();
        }
        else if (!TryFindBreakContext(labelName, requireContinue: true, out ctx))
        {
            throw new JSSyntaxError(
                $"Undefined label '{labelName}'",
                _currentToken.Start);
        }

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

                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();
                Expect(TokenType.RightParen);

                // Define catch variable and store exception.
                int catchVarIndex = _currentFunction.AddVar(atom, JSVarKind.Normal, false, true);
                EmitOp(OpCode.PutLoc);
                EmitU16((ushort)catchVarIndex);
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
    public void ParseSwitchStatement(string? labelName = null)
    {
        Expect(TokenType.Switch);
        Expect(TokenType.LeftParen);
        ParseExpression();
        Expect(TokenType.RightParen);
        Expect(TokenType.LeftBrace);

        int labelBreak = NewLabel();
        int labelDefault = -1;
        var caseLabels = new System.Collections.Generic.List<int>();

        PushBreakContext(labelBreak, -1, labelName);  // Switch has break but no continue
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
                funcName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
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
        var savedWithScopes = _withScopeStack.ToArray();
        _withScopeStack.Clear();
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
            var paramAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
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

        // Resolve labels to convert label indices to relative offsets
        _currentFunction.ByteCode.ResolveLabels();

        // Add the function to the parent's child functions
        parentFunction.AddChildFunction(newFunction);
        int funcConstIdx = newFunction.ParentCPoolIndex;

        // Switch back to the parent function
        _currentFunction = parentFunction;
        _withScopeStack.Clear();
        _withScopeStack.AddRange(savedWithScopes);

        // Emit code to create the function object (closure)
        EmitOp(OpCode.FClosure);
        EmitU32((uint)funcConstIdx);

        // For function declarations, store in the variable
        if (funcType == JSParseFunctionType.Statement && !funcName.IsEmpty)
        {
            // For global scope (IsGlobalVar), store as a global property
            // For local scope, use PutLoc
            if (_currentFunction.IsGlobalVar)
            {
                // Stack: [function]
                // Store as global property: globalThis.funcName = function
                EmitGlobalObject();              // Stack: [function, globalThis]
                EmitOp(OpCode.Swap);             // Stack: [globalThis, function]
                EmitOp(OpCode.PutField);         // Stack: [] (PutField pops obj and value)
                EmitAtom(funcName);
            }
            else
            {
                // Local function declaration - use PutLoc
                int varIdx = _currentFunction.AddVar(funcName, JSVarKind.FunctionDecl, false, false);
                EmitOp(OpCode.PutLoc);
                EmitU16((ushort)varIdx);
            }
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
                var paramAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
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
    /// Expects the source value to be on the stack.
    /// </summary>
    private void ParseDestructuringPattern(bool isDeclaration, bool hasDefaultAllowed)
    {
        if (Check(TokenType.LeftBracket))
        {
            ParseArrayDestructuringPattern(isDeclaration, hasDefaultAllowed);
        }
        else if (Check(TokenType.LeftBrace))
        {
            ParseObjectDestructuringPattern(isDeclaration, hasDefaultAllowed);
        }
    }

    /// <summary>
    /// Parses array destructuring pattern: [a, b, c] or [a, , b] or [...rest]
    /// </summary>
    private void ParseArrayDestructuringPattern(bool isDeclaration, bool hasDefaultAllowed)
    {
        NextToken(); // consume '['

        // Start iterator on the source array
        EmitOp(OpCode.ForOfStart);

        int elementIndex = 0;
        while (!Check(TokenType.RightBracket))
        {
            // Handle elision (holes): [a, , b]
            if (Check(TokenType.Comma))
            {
                NextToken();
                // Skip this element
                EmitOp(OpCode.ForOfNext);
                EmitU8(0);
                EmitOp(OpCode.Drop);
                EmitOp(OpCode.Drop);
                elementIndex++;
                continue;
            }

            bool isRest = Match(TokenType.Ellipsis);

            if (Check(TokenType.Identifier))
            {
                var atom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();

                if (isRest)
                {
                    // Rest element: [...rest]
                    EmitOp(OpCode.ArrayFrom);
                    EmitU16(0); // Collect remaining into array
                }
                else
                {
                    // Get next element from iterator
                    EmitOp(OpCode.ForOfNext);
                    EmitU8(0);
                    EmitOp(OpCode.Drop); // drop done flag
                }

                // Check for default value: [a = 1]
                if (hasDefaultAllowed && !isRest && Match(TokenType.Assign))
                {
                    int labelHasValue = NewLabel();
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.Undefined);
                    EmitOp(OpCode.StrictEq);
                    EmitGoto(OpCode.IfFalse, labelHasValue);
                    EmitOp(OpCode.Drop);
                    ParseAssignExpression();
                    EmitLabel(labelHasValue);
                }

                // Define variable if declaration, otherwise assign
                if (isDeclaration)
                {
                    _currentFunction.AddVar(atom, JSVarKind.Normal, isConst: false, isLexical: true);
                    EmitOp(OpCode.ScopePutVarInit);
                    EmitAtom(atom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }
                else
                {
                    // For assignment patterns, we'd need the lvalue reference
                    EmitOp(OpCode.ScopePutVar);
                    EmitAtom(atom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }
            }
            else if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
            {
                // Nested destructuring pattern
                EmitOp(OpCode.ForOfNext);
                EmitU8(0);
                EmitOp(OpCode.Drop); // drop done flag
                ParseDestructuringPattern(isDeclaration, hasDefaultAllowed);
            }

            if (isRest)
            {
                // Rest must be last
                if (!Check(TokenType.RightBracket))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }
                break;
            }

            elementIndex++;

            if (!Match(TokenType.Comma))
                break;
        }

        Expect(TokenType.RightBracket);
        
        // Close iterator
        EmitOp(OpCode.IteratorClose);
    }

    /// <summary>
    /// Parses object destructuring pattern: { a, b: c, d = 1, ...rest }
    /// </summary>
    private void ParseObjectDestructuringPattern(bool isDeclaration, bool hasDefaultAllowed)
    {
        NextToken(); // consume '{'

        // Convert to object (throws if null/undefined)
        EmitOp(OpCode.ToObject);

        while (!Check(TokenType.RightBrace))
        {
            bool isRest = Match(TokenType.Ellipsis);

            if (isRest)
            {
                // Rest element: { ...rest }
                if (!Check(TokenType.Identifier))
                {
                    throw new JSSyntaxError(
                        "Expected identifier after '...'",
                        _currentToken.Start);
                }
                var restAtom = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                NextToken();

                // Copy remaining enumerable properties
                EmitOp(OpCode.Object);
                EmitOp(OpCode.CopyDataProperties);
                EmitU8(0);

                if (isDeclaration)
                {
                    _currentFunction.AddVar(restAtom, JSVarKind.Normal, isConst: false, isLexical: true);
                    EmitOp(OpCode.ScopePutVarInit);
                    EmitAtom(restAtom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }
                else
                {
                    EmitOp(OpCode.ScopePutVar);
                    EmitAtom(restAtom);
                    EmitU16((ushort)_currentFunction.ScopeLevel);
                }

                // Rest must be last
                if (!Check(TokenType.RightBrace))
                {
                    throw new JSSyntaxError(
                        "Rest element must be last",
                        _currentToken.Start);
                }
                break;
            }

            if (!Check(TokenType.Identifier) && !Check(TokenType.String) && !Check(TokenType.Number))
            {
                throw new JSSyntaxError(
                    "Expected property name",
                    _currentToken.Start);
            }

            JSAtom propName;
            JSAtom varName;

            if (Check(TokenType.Identifier))
            {
                propName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                varName = propName;
                NextToken();
            }
            else if (Check(TokenType.String))
            {
                propName = _atoms.GetOrCreateAtom((string)_currentToken.Value!);
                varName = propName;
                NextToken();
            }
            else // Number
            {
                propName = _atoms.GetOrCreateAtom(_currentToken.Value!.ToString()!);
                varName = propName;
                NextToken();
            }

            // Check for renaming: { prop: newName } or nested: { prop: { a, b } }
            if (Match(TokenType.Colon))
            {
                if (Check(TokenType.LeftBracket) || Check(TokenType.LeftBrace))
                {
                    // Nested pattern: { prop: { a, b } }
                    EmitOp(OpCode.Dup);
                    EmitOp(OpCode.GetField);
                    EmitAtom(propName);
                    ParseDestructuringPattern(isDeclaration, hasDefaultAllowed);
                    
                    if (!Match(TokenType.Comma))
                        break;
                    continue;
                }
                else if (Check(TokenType.Identifier))
                {
                    // Renaming: { prop: newName }
                    varName = _atoms.GetOrCreateAtom(_currentToken.Text.Span);
                    NextToken();
                }
                else
                {
                    throw new JSSyntaxError(
                        "Expected identifier or destructuring pattern after ':'",
                        _currentToken.Start);
                }
            }

            // Get the property from the source object
            EmitOp(OpCode.Dup);
            EmitOp(OpCode.GetField);
            EmitAtom(propName);

            // Check for default value: { a = 1 }
            if (hasDefaultAllowed && Match(TokenType.Assign))
            {
                int labelHasValue = NewLabel();
                EmitOp(OpCode.Dup);
                EmitOp(OpCode.Undefined);
                EmitOp(OpCode.StrictEq);
                EmitGoto(OpCode.IfFalse, labelHasValue);
                EmitOp(OpCode.Drop);
                ParseAssignExpression();
                EmitLabel(labelHasValue);
            }

            // Store the value
            if (isDeclaration)
            {
                _currentFunction.AddVar(varName, JSVarKind.Normal, isConst: false, isLexical: true);
                EmitOp(OpCode.ScopePutVarInit);
                EmitAtom(varName);
                EmitU16((ushort)_currentFunction.ScopeLevel);
            }
            else
            {
                EmitOp(OpCode.ScopePutVar);
                EmitAtom(varName);
                EmitU16((ushort)_currentFunction.ScopeLevel);
            }

            if (!Match(TokenType.Comma))
                break;
        }

        Expect(TokenType.RightBrace);
        EmitOp(OpCode.Drop); // Drop the source object
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

    #endregion

    #region Break/Continue Context

    private readonly System.Collections.Generic.Stack<BreakContext> _breakStack = new();

    private struct BreakContext
    {
        public int BreakLabel;
        public int ContinueLabel;
        public string? LabelName;

        public BreakContext(int breakLabel, int continueLabel, string? labelName)
        {
            BreakLabel = breakLabel;
            ContinueLabel = continueLabel;
            LabelName = labelName;
        }
    }

    private void PushBreakContext(int breakLabel, int continueLabel, string? labelName = null)
    {
        _breakStack.Push(new BreakContext(breakLabel, continueLabel, labelName));
    }

    private void PopBreakContext()
    {
        _breakStack.Pop();
    }

    private bool TryFindBreakContext(string labelName, bool requireContinue, out BreakContext context)
    {
        foreach (var entry in _breakStack)
        {
            if (entry.LabelName == labelName)
            {
                if (!requireContinue || entry.ContinueLabel >= 0)
                {
                    context = entry;
                    return true;
                }
                break;
            }
        }

        context = default;
        return false;
    }

    #endregion
}
