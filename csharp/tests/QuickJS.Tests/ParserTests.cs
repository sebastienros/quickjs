// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for the Parser class - expression compilation.
/// </summary>
public class ParserTests
{
    #region Token Handling

    [Fact]
    public void Parser_CanBeCreated()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 + 2", "test.js", atoms);

        Assert.NotNull(parser);
        Assert.NotNull(parser.CurrentFunction);
        Assert.NotNull(parser.Atoms);
    }

    [Fact]
    public void Parser_StartsWithFirstToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42", "test.js", atoms);

        Assert.Equal(TokenType.Number, parser.CurrentToken.Type);
        // Lexer returns numbers as long or double
        Assert.True(parser.CurrentToken.Value is long || parser.CurrentToken.Value is double);
    }

    [Fact]
    public void Parser_NextToken_AdvancesToNextToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 + 2", "test.js", atoms);

        Assert.Equal(TokenType.Number, parser.CurrentToken.Type);
        parser.NextToken();
        Assert.Equal(TokenType.Plus, parser.CurrentToken.Type);
        parser.NextToken();
        Assert.Equal(TokenType.Number, parser.CurrentToken.Type);
    }

    [Fact]
    public void Parser_Check_ReturnsTrueForMatchingToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42", "test.js", atoms);

        Assert.True(parser.Check(TokenType.Number));
        Assert.False(parser.Check(TokenType.String));
    }

    [Fact]
    public void Parser_Match_ConsumesMatchingToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("+ -", "test.js", atoms);

        Assert.True(parser.Match(TokenType.Plus));
        Assert.Equal(TokenType.Minus, parser.CurrentToken.Type);
    }

    [Fact]
    public void Parser_Match_ReturnsFalseAndDoesNotConsumeNonMatchingToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("+ -", "test.js", atoms);

        Assert.False(parser.Match(TokenType.Minus));
        Assert.Equal(TokenType.Plus, parser.CurrentToken.Type);
    }

    [Fact]
    public void Parser_Expect_ThrowsOnNonMatchingToken()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42", "test.js", atoms);

        var ex = Assert.Throws<JSSyntaxError>(() => parser.Expect(TokenType.String));
        Assert.Contains("Expected", ex.Message);
    }

    #endregion

    #region Number Literal Parsing

    [Fact]
    public void ParseExpression_IntegerLiteral_EmitsPushI32()
    {
        var atoms = new AtomTable();
        var parser = new Parser("42", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.True(bytecode.Length > 0);
        // First byte should be PushI32 opcode
        Assert.Equal((byte)OpCode.PushI32, bytecode[0]);
    }

    [Fact]
    public void ParseExpression_ZeroLiteral_EmitsPushI32()
    {
        var atoms = new AtomTable();
        var parser = new Parser("0", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Equal((byte)OpCode.PushI32, bytecode[0]);
    }

    [Fact]
    public void ParseExpression_NegativeNumber_EmitsNegation()
    {
        var atoms = new AtomTable();
        var parser = new Parser("-42", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should have PushI32 followed by Neg
        Assert.Contains((byte)OpCode.Neg, bytecode);
    }

    [Fact]
    public void ParseExpression_FloatLiteral_AddedToConstantPool()
    {
        var atoms = new AtomTable();
        var parser = new Parser("3.14", "test.js", atoms);
        parser.ParseExpression();

        // Float should be added to constant pool
        Assert.True(parser.CurrentFunction.Constants.Count > 0);
    }

    #endregion

    #region String Literal Parsing

    [Fact]
    public void ParseExpression_StringLiteral_AddedToConstantPool()
    {
        var atoms = new AtomTable();
        var parser = new Parser("\"hello\"", "test.js", atoms);
        parser.ParseExpression();

        Assert.True(parser.CurrentFunction.Constants.Count > 0);
        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Equal((byte)OpCode.PushConst, bytecode[0]);
    }

    [Fact]
    public void ParseExpression_EmptyString_AddedToConstantPool()
    {
        var atoms = new AtomTable();
        var parser = new Parser("\"\"", "test.js", atoms);
        parser.ParseExpression();

        Assert.True(parser.CurrentFunction.Constants.Count > 0);
    }

    #endregion

    #region Boolean and Null Literals

    [Fact]
    public void ParseExpression_TrueLiteral_EmitsPushTrue()
    {
        var atoms = new AtomTable();
        var parser = new Parser("true", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Equal((byte)OpCode.PushTrue, bytecode[0]);
    }

    [Fact]
    public void ParseExpression_FalseLiteral_EmitsPushFalse()
    {
        var atoms = new AtomTable();
        var parser = new Parser("false", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Equal((byte)OpCode.PushFalse, bytecode[0]);
    }

    [Fact]
    public void ParseExpression_NullLiteral_EmitsNull()
    {
        var atoms = new AtomTable();
        var parser = new Parser("null", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Equal((byte)OpCode.Null, bytecode[0]);
    }

    #endregion

    #region Binary Operators

    [Fact]
    public void ParseExpression_Addition_EmitsAdd()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 + 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should contain Add opcode somewhere
        Assert.Contains((byte)OpCode.Add, bytecode);
    }

    [Fact]
    public void ParseExpression_Subtraction_EmitsSub()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 - 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Sub, bytecode);
    }

    [Fact]
    public void ParseExpression_Multiplication_EmitsMul()
    {
        var atoms = new AtomTable();
        var parser = new Parser("4 * 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Mul, bytecode);
    }

    [Fact]
    public void ParseExpression_Division_EmitsDiv()
    {
        var atoms = new AtomTable();
        var parser = new Parser("10 / 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Div, bytecode);
    }

    [Fact]
    public void ParseExpression_Modulo_EmitsMod()
    {
        var atoms = new AtomTable();
        var parser = new Parser("10 % 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Mod, bytecode);
    }

    [Fact]
    public void ParseExpression_Exponentiation_EmitsPow()
    {
        var atoms = new AtomTable();
        var parser = new Parser("2 ** 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Pow, bytecode);
    }

    #endregion

    #region Comparison Operators

    [Fact]
    public void ParseExpression_LessThan_EmitsLt()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 < 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Lt, bytecode);
    }

    [Fact]
    public void ParseExpression_GreaterThan_EmitsGt()
    {
        var atoms = new AtomTable();
        var parser = new Parser("2 > 1", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Gt, bytecode);
    }

    [Fact]
    public void ParseExpression_LessThanOrEqual_EmitsLte()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 <= 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Lte, bytecode);
    }

    [Fact]
    public void ParseExpression_GreaterThanOrEqual_EmitsGte()
    {
        var atoms = new AtomTable();
        var parser = new Parser("2 >= 1", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Gte, bytecode);
    }

    [Fact]
    public void ParseExpression_Equal_EmitsEq()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 == 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Eq, bytecode);
    }

    [Fact]
    public void ParseExpression_NotEqual_EmitsNeq()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 != 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Neq, bytecode);
    }

    [Fact]
    public void ParseExpression_StrictEqual_EmitsStrictEq()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 === 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.StrictEq, bytecode);
    }

    [Fact]
    public void ParseExpression_StrictNotEqual_EmitsStrictNeq()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 !== 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.StrictNeq, bytecode);
    }

    #endregion

    #region Bitwise Operators

    [Fact]
    public void ParseExpression_BitwiseAnd_EmitsAnd()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 & 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.And, bytecode);
    }

    [Fact]
    public void ParseExpression_BitwiseOr_EmitsOr()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 | 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Or, bytecode);
    }

    [Fact]
    public void ParseExpression_BitwiseXor_EmitsXor()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 ^ 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Xor, bytecode);
    }

    [Fact]
    public void ParseExpression_BitwiseNot_EmitsNot()
    {
        var atoms = new AtomTable();
        var parser = new Parser("~5", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Not, bytecode);
    }

    [Fact]
    public void ParseExpression_LeftShift_EmitsShl()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 << 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Shl, bytecode);
    }

    [Fact]
    public void ParseExpression_RightShift_EmitsSar()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 >> 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Sar, bytecode);
    }

    [Fact]
    public void ParseExpression_UnsignedRightShift_EmitsShr()
    {
        var atoms = new AtomTable();
        var parser = new Parser("5 >>> 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Shr, bytecode);
    }

    #endregion

    #region Unary Operators

    [Fact]
    public void ParseExpression_UnaryPlus_EmitsPlus()
    {
        var atoms = new AtomTable();
        var parser = new Parser("+5", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Plus, bytecode);
    }

    [Fact]
    public void ParseExpression_UnaryMinus_EmitsNeg()
    {
        var atoms = new AtomTable();
        var parser = new Parser("-5", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Neg, bytecode);
    }

    [Fact]
    public void ParseExpression_LogicalNot_EmitsLNot()
    {
        var atoms = new AtomTable();
        var parser = new Parser("!true", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.LNot, bytecode);
    }

    [Fact]
    public void ParseExpression_TypeOf_EmitsTypeOf()
    {
        var atoms = new AtomTable();
        var parser = new Parser("typeof x", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.TypeOf, bytecode);
    }

    [Fact]
    public void ParseExpression_Void_EmitsDropAndUndefined()
    {
        var atoms = new AtomTable();
        var parser = new Parser("void 0", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Drop, bytecode);
        Assert.Contains((byte)OpCode.Undefined, bytecode);
    }

    #endregion

    #region Logical Operators

    [Fact]
    public void ParseExpression_LogicalAnd_GeneratesConditionalJumps()
    {
        var atoms = new AtomTable();
        var parser = new Parser("true && false", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should have jump instructions for short-circuit evaluation
        Assert.True(bytecode.Length > 2);
    }

    [Fact]
    public void ParseExpression_LogicalOr_GeneratesConditionalJumps()
    {
        var atoms = new AtomTable();
        var parser = new Parser("true || false", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.True(bytecode.Length > 2);
    }

    [Fact]
    public void ParseExpression_NullishCoalescing_GeneratesConditionalJumps()
    {
        var atoms = new AtomTable();
        var parser = new Parser("null ?? 42", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.True(bytecode.Length > 2);
    }

    #endregion

    #region Conditional Expression

    [Fact]
    public void ParseExpression_ConditionalExpression_GeneratesJumps()
    {
        var atoms = new AtomTable();
        var parser = new Parser("true ? 1 : 2", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should have jump instructions for conditional
        Assert.True(bytecode.Length > 4);
    }

    #endregion

    #region Grouping

    [Fact]
    public void ParseExpression_Parentheses_GroupsExpressions()
    {
        var atoms = new AtomTable();
        var parser = new Parser("(1 + 2) * 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Should have both Add and Mul
        Assert.Contains((byte)OpCode.Add, bytecode);
        Assert.Contains((byte)OpCode.Mul, bytecode);
    }

    #endregion

    #region Array Literals

    [Fact]
    public void ParseExpression_EmptyArrayLiteral_EmitsArrayFrom()
    {
        var atoms = new AtomTable();
        var parser = new Parser("[]", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.ArrayFrom, bytecode);
    }

    [Fact]
    public void ParseExpression_ArrayWithElements_EmitsArrayFrom()
    {
        var atoms = new AtomTable();
        var parser = new Parser("[1, 2, 3]", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.ArrayFrom, bytecode);
    }

    [Fact]
    public void ParseExpression_ArrayWithElision_EmitsUndefined()
    {
        var atoms = new AtomTable();
        var parser = new Parser("[1, , 3]", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Undefined, bytecode);
    }

    #endregion

    #region Object Literals

    [Fact]
    public void ParseExpression_EmptyObjectLiteral_EmitsObject()
    {
        var atoms = new AtomTable();
        var parser = new Parser("{}", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Object, bytecode);
    }

    [Fact]
    public void ParseExpression_ObjectWithProperty_EmitsDefineField()
    {
        var atoms = new AtomTable();
        var parser = new Parser("{x: 1}", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Object, bytecode);
        Assert.Contains((byte)OpCode.DefineField, bytecode);
    }

    #endregion

    #region Identifier Parsing

    [Fact]
    public void ParseExpression_Identifier_EmitsScopeGetVar()
    {
        var atoms = new AtomTable();
        var parser = new Parser("x", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.ScopeGetVar, bytecode);
    }

    [Fact]
    public void ParseExpression_This_EmitsScopeGetVar()
    {
        var atoms = new AtomTable();
        var parser = new Parser("this", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.ScopeGetVar, bytecode);
    }

    #endregion

    #region Member Access

    [Fact]
    public void ParseExpression_DotAccess_EmitsGetField()
    {
        var atoms = new AtomTable();
        var parser = new Parser("obj.prop", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.GetField, bytecode);
    }

    [Fact]
    public void ParseExpression_BracketAccess_EmitsGetArrayEl()
    {
        var atoms = new AtomTable();
        var parser = new Parser("arr[0]", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.GetArrayEl, bytecode);
    }

    #endregion

    #region Function Calls

    [Fact]
    public void ParseExpression_FunctionCall_EmitsCall()
    {
        var atoms = new AtomTable();
        var parser = new Parser("foo()", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Call, bytecode);
    }

    [Fact]
    public void ParseExpression_FunctionCallWithArgs_EmitsCall()
    {
        var atoms = new AtomTable();
        var parser = new Parser("foo(1, 2)", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Call, bytecode);
    }

    [Fact]
    public void ParseExpression_MethodCall_EmitsCallMethod()
    {
        var atoms = new AtomTable();
        var parser = new Parser("obj.method()", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.CallMethod, bytecode);
    }

    #endregion

    #region Operator Precedence

    [Fact]
    public void ParseExpression_MultiplicationBeforeAddition()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 + 2 * 3", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        // Both should be present
        Assert.Contains((byte)OpCode.Add, bytecode);
        Assert.Contains((byte)OpCode.Mul, bytecode);

        // Mul should come before Add (due to precedence)
        int mulIndex = System.Array.IndexOf(bytecode, (byte)OpCode.Mul);
        int addIndex = System.Array.IndexOf(bytecode, (byte)OpCode.Add);
        Assert.True(mulIndex < addIndex, "Mul should be emitted before Add due to precedence");
    }

    [Fact]
    public void ParseExpression_ComparisonBeforeEquality()
    {
        var atoms = new AtomTable();
        var parser = new Parser("1 < 2 == true", "test.js", atoms);
        parser.ParseExpression();

        var bytecode = parser.CurrentFunction.ByteCode.ToArray();
        Assert.Contains((byte)OpCode.Lt, bytecode);
        Assert.Contains((byte)OpCode.Eq, bytecode);

        int ltIndex = System.Array.IndexOf(bytecode, (byte)OpCode.Lt);
        int eqIndex = System.Array.IndexOf(bytecode, (byte)OpCode.Eq);
        Assert.True(ltIndex < eqIndex, "Lt should be emitted before Eq due to precedence");
    }

    #endregion

    #region Error Handling

    [Fact]
    public void ParseExpression_UnexpectedToken_ThrowsSyntaxError()
    {
        var atoms = new AtomTable();
        var parser = new Parser(")", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseExpression());
    }

    [Fact]
    public void ParseExpression_UnaryBeforePower_ThrowsSyntaxError()
    {
        var atoms = new AtomTable();
        // -x ** 2 is a syntax error in JavaScript (ambiguous)
        var parser = new Parser("-2 ** 2", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseExpression());
    }

    #endregion
}
