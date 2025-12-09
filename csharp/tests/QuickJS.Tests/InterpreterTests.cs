// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for the Interpreter class.
/// </summary>
public class InterpreterTests
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly Interpreter _interpreter;

    public InterpreterTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
        _interpreter = new Interpreter(_context);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithContext_CreatesInterpreter()
    {
        var interpreter = new Interpreter(_context);
        Assert.NotNull(interpreter);
        Assert.Same(_context, interpreter.Context);
    }

    [Fact]
    public void Constructor_WithStackSize_SetsMaxStackSize()
    {
        var interpreter = new Interpreter(_context, 1024);
        Assert.Equal(1024, interpreter.MaxStackSize);
    }

    [Fact]
    public void Constructor_DefaultStackSize_Is512()
    {
        Assert.Equal(512, _interpreter.MaxStackSize);
    }

    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Interpreter(null!));
    }

    #endregion

    #region Stack Basic Operations

    [Fact]
    public void Push_Value_IncreasesStackPointer()
    {
        Assert.Equal(0, _interpreter.StackPointer);
        _interpreter.Push(JSValue.FromInt32(42));
        Assert.Equal(1, _interpreter.StackPointer);
    }

    [Fact]
    public void Pop_Value_ReturnsAndRemoves()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        var value = _interpreter.Pop();
        Assert.Equal(42, value.ToInt32());
        Assert.Equal(0, _interpreter.StackPointer);
    }

    [Fact]
    public void Peek_Value_ReturnsWithoutRemoving()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        var value = _interpreter.Peek();
        Assert.Equal(42, value.ToInt32());
        Assert.Equal(1, _interpreter.StackPointer);
    }

    [Fact]
    public void PeekAt_ReturnsValueAtDepth()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        
        Assert.Equal(3, _interpreter.PeekAt(0).ToInt32());
        Assert.Equal(2, _interpreter.PeekAt(1).ToInt32());
        Assert.Equal(1, _interpreter.PeekAt(2).ToInt32());
    }

    [Fact]
    public void IsStackEmpty_EmptyStack_ReturnsTrue()
    {
        Assert.True(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void IsStackEmpty_NonEmptyStack_ReturnsFalse()
    {
        _interpreter.Push(JSValue.Undefined);
        Assert.False(_interpreter.IsStackEmpty);
    }

    [Fact]
    public void ClearStack_EmptiesStack()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.ClearStack();
        Assert.Equal(0, _interpreter.StackPointer);
    }

    [Fact]
    public void GetStackSnapshot_ReturnsCurrentStack()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        var snapshot = _interpreter.GetStackSnapshot();
        Assert.Equal(2, snapshot.Length);
        Assert.Equal(1, snapshot[0].ToInt32());
        Assert.Equal(2, snapshot[1].ToInt32());
    }

    #endregion

    #region Stack Manipulation Operations

    [Fact]
    public void Drop_RemovesTopValue()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Drop();
        Assert.Equal(1, _interpreter.StackPointer);
        Assert.Equal(1, _interpreter.Peek().ToInt32());
    }

    [Fact]
    public void Dup_DuplicatesTop()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Dup();
        Assert.Equal(2, _interpreter.StackPointer);
        Assert.Equal(42, _interpreter.Pop().ToInt32());
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Dup2_DuplicatesTopTwo()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Dup2();
        Assert.Equal(4, _interpreter.StackPointer);
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Dup3_DuplicatesTopThree()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Dup3();
        Assert.Equal(6, _interpreter.StackPointer);
    }

    [Fact]
    public void Dup1_DuplicatesSecond()
    {
        // a b -> a a b
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Dup1();
        Assert.Equal(3, _interpreter.StackPointer);
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Nip_RemovesSecond()
    {
        // a b -> b
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Nip();
        Assert.Equal(1, _interpreter.StackPointer);
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Nip1_RemovesThird()
    {
        // a b c -> b c
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Nip1();
        Assert.Equal(2, _interpreter.StackPointer);
        Assert.Equal(3, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Swap_SwapsTopTwo()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Swap();
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Rot3L_RotatesLeft()
    {
        // x a b -> a b x (231)
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Rot3L();
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(3, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Rot3R_RotatesRight()
    {
        // a b x -> x a b (312)
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Rot3R();
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(3, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Perm3_Permutes()
    {
        // obj a b -> a obj b (213)
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Perm3();
        Assert.Equal(3, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Insert2_InsertsAtSecond()
    {
        // obj a -> a obj a
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Insert2();
        Assert.Equal(3, _interpreter.StackPointer);
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Insert3_InsertsAtThird()
    {
        // obj prop a -> a obj prop a
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(3));
        _interpreter.Insert3();
        Assert.Equal(4, _interpreter.StackPointer);
        Assert.Equal(3, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(3, _interpreter.Pop().ToInt32());
    }

    #endregion

    #region Push Operations

    [Fact]
    public void PushUndefined_PushesUndefined()
    {
        _interpreter.PushUndefined();
        Assert.True(_interpreter.Pop().IsUndefined);
    }

    [Fact]
    public void PushNull_PushesNull()
    {
        _interpreter.PushNull();
        Assert.True(_interpreter.Pop().IsNull);
    }

    [Fact]
    public void PushTrue_PushesTrue()
    {
        _interpreter.PushTrue();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void PushFalse_PushesFalse()
    {
        _interpreter.PushFalse();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void PushI32_PushesInteger()
    {
        _interpreter.PushI32(42);
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void PushDouble_PushesDouble()
    {
        _interpreter.PushDouble(3.14);
        Assert.Equal(3.14, _interpreter.Pop().ToDouble(), 10);
    }

    [Fact]
    public void PushString_PushesString()
    {
        _interpreter.PushString("hello");
        Assert.Equal("hello", _interpreter.Pop().ToString());
    }

    #endregion

    #region Arithmetic Operations - Addition

    [Fact]
    public void Add_Integers_ReturnsSum()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(20));
        _interpreter.Add();
        Assert.Equal(30, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Add_IntegersOverflow_ReturnsDouble()
    {
        _interpreter.Push(JSValue.FromInt32(int.MaxValue));
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Add();
        var result = _interpreter.Pop();
        Assert.True(result.IsNumber);
        Assert.Equal((double)int.MaxValue + 1, result.ToDouble());
    }

    [Fact]
    public void Add_Doubles_ReturnsSum()
    {
        _interpreter.Push(JSValue.FromDouble(1.5));
        _interpreter.Push(JSValue.FromDouble(2.5));
        _interpreter.Add();
        Assert.Equal(4.0, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void Add_Strings_Concatenates()
    {
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.Push(JSValue.FromString(" world"));
        _interpreter.Add();
        Assert.Equal("hello world", _interpreter.Pop().ToString());
    }

    [Fact]
    public void Add_StringAndNumber_Concatenates()
    {
        _interpreter.Push(JSValue.FromString("value: "));
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Add();
        Assert.Equal("value: 42", _interpreter.Pop().ToString());
    }

    #endregion

    #region Arithmetic Operations - Subtraction

    [Fact]
    public void Sub_Integers_ReturnsDifference()
    {
        _interpreter.Push(JSValue.FromInt32(30));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Sub();
        Assert.Equal(20, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Sub_IntegersNegativeResult_Works()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(30));
        _interpreter.Sub();
        Assert.Equal(-20, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Sub_Doubles_ReturnsDifference()
    {
        _interpreter.Push(JSValue.FromDouble(5.5));
        _interpreter.Push(JSValue.FromDouble(2.5));
        _interpreter.Sub();
        Assert.Equal(3.0, _interpreter.Pop().ToDouble());
    }

    #endregion

    #region Arithmetic Operations - Multiplication

    [Fact]
    public void Mul_Integers_ReturnsProduct()
    {
        _interpreter.Push(JSValue.FromInt32(6));
        _interpreter.Push(JSValue.FromInt32(7));
        _interpreter.Mul();
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Mul_Doubles_ReturnsProduct()
    {
        _interpreter.Push(JSValue.FromDouble(2.5));
        _interpreter.Push(JSValue.FromDouble(4.0));
        _interpreter.Mul();
        Assert.Equal(10.0, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void Mul_IntegerOverflow_ReturnsDouble()
    {
        _interpreter.Push(JSValue.FromInt32(int.MaxValue));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Mul();
        var result = _interpreter.Pop();
        Assert.True(result.IsNumber);
    }

    #endregion

    #region Arithmetic Operations - Division

    [Fact]
    public void Div_Integers_ReturnsDouble()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(4));
        _interpreter.Div();
        Assert.Equal(2.5, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void Div_ByZero_ReturnsInfinity()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Div();
        Assert.Equal(double.PositiveInfinity, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void Div_ZeroByZero_ReturnsNaN()
    {
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Div();
        Assert.True(double.IsNaN(_interpreter.Pop().ToDouble()));
    }

    #endregion

    #region Arithmetic Operations - Modulo

    [Fact]
    public void Mod_PositiveIntegers_ReturnsRemainder()
    {
        _interpreter.Push(JSValue.FromInt32(17));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Mod();
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Mod_Doubles_ReturnsRemainder()
    {
        _interpreter.Push(JSValue.FromDouble(5.5));
        _interpreter.Push(JSValue.FromDouble(2.0));
        _interpreter.Mod();
        Assert.Equal(1.5, _interpreter.Pop().ToDouble(), 10);
    }

    #endregion

    #region Arithmetic Operations - Power

    [Fact]
    public void Pow_Integers_ReturnsPower()
    {
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Pow();
        Assert.Equal(1024.0, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void Pow_FractionalExponent_Works()
    {
        _interpreter.Push(JSValue.FromInt32(4));
        _interpreter.Push(JSValue.FromDouble(0.5));
        _interpreter.Pow();
        Assert.Equal(2.0, _interpreter.Pop().ToDouble());
    }

    #endregion

    #region Unary Operations

    [Fact]
    public void Plus_Number_ReturnsNumber()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Plus();
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Neg_PositiveInteger_ReturnsNegative()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Neg();
        Assert.Equal(-42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Neg_NegativeInteger_ReturnsPositive()
    {
        _interpreter.Push(JSValue.FromInt32(-42));
        _interpreter.Neg();
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Neg_Zero_ReturnsNegativeZero()
    {
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Neg();
        var result = _interpreter.Pop().ToDouble();
        Assert.Equal(0.0, result);
        // Check it's -0 by testing 1/-0 = -Infinity
        Assert.Equal(double.NegativeInfinity, 1.0 / result);
    }

    [Fact]
    public void Inc_Integer_AddsOne()
    {
        _interpreter.Push(JSValue.FromInt32(41));
        _interpreter.Inc();
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Dec_Integer_SubtractsOne()
    {
        _interpreter.Push(JSValue.FromInt32(43));
        _interpreter.Dec();
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    #endregion

    #region Bitwise Operations

    [Fact]
    public void Not_Integer_ReturnsBitwiseNot()
    {
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Not();
        Assert.Equal(-1, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void And_Integers_ReturnsBitwiseAnd()
    {
        _interpreter.Push(JSValue.FromInt32(0b1100));
        _interpreter.Push(JSValue.FromInt32(0b1010));
        _interpreter.And();
        Assert.Equal(0b1000, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Or_Integers_ReturnsBitwiseOr()
    {
        _interpreter.Push(JSValue.FromInt32(0b1100));
        _interpreter.Push(JSValue.FromInt32(0b1010));
        _interpreter.Or();
        Assert.Equal(0b1110, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Xor_Integers_ReturnsBitwiseXor()
    {
        _interpreter.Push(JSValue.FromInt32(0b1100));
        _interpreter.Push(JSValue.FromInt32(0b1010));
        _interpreter.Xor();
        Assert.Equal(0b0110, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Shl_Integer_ShiftsLeft()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(4));
        _interpreter.Shl();
        Assert.Equal(16, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Sar_Integer_ShiftsRightSigned()
    {
        _interpreter.Push(JSValue.FromInt32(-16));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.Sar();
        Assert.Equal(-4, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void Shr_Integer_ShiftsRightUnsigned()
    {
        _interpreter.Push(JSValue.FromInt32(-1));
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Shr();
        // -1 >>> 1 = 0x7FFFFFFF in JavaScript
        var result = _interpreter.Pop();
        Assert.Equal(0x7FFFFFFF, result.ToInt32());
    }

    #endregion

    #region Comparison Operations - Less Than

    [Fact]
    public void Lt_SmallerFirst_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Lt();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Lt_LargerFirst_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Lt();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void Lt_Equal_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Lt();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region Comparison Operations - Less Than or Equal

    [Fact]
    public void Lte_SmallerFirst_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Lte();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Lte_Equal_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Lte();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Lte_LargerFirst_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Lte();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region Comparison Operations - Greater Than

    [Fact]
    public void Gt_LargerFirst_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Gt();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Gt_SmallerFirst_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Gt();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region Comparison Operations - Greater Than or Equal

    [Fact]
    public void Gte_LargerFirst_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Gte();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Gte_Equal_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Gte();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    #endregion

    #region Equality Operations

    [Fact]
    public void Eq_SameIntegers_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Eq_DifferentIntegers_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromInt32(43));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void Eq_NullAndUndefined_ReturnsTrue()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.Push(JSValue.Undefined);
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Eq_NumberAndString_ComparesAsNumbers()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromString("42"));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Neq_DifferentValues_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromInt32(43));
        _interpreter.Neq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void StrictEq_SameValues_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void StrictEq_NumberAndString_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromString("42"));
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void StrictEq_NullAndUndefined_ReturnsFalse()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.Push(JSValue.Undefined);
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void StrictNeq_DifferentTypes_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromString("42"));
        _interpreter.StrictNeq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    #endregion

    #region Logical Operations

    [Fact]
    public void LogicalNot_True_ReturnsFalse()
    {
        _interpreter.Push(JSValue.True);
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void LogicalNot_False_ReturnsTrue()
    {
        _interpreter.Push(JSValue.False);
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void LogicalNot_TruthyNumber_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void LogicalNot_Zero_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void LogicalNot_EmptyString_ReturnsTrue()
    {
        _interpreter.Push(JSValue.FromString(""));
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void LogicalNot_NonEmptyString_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.LogicalNot();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void IsUndefinedOrNull_Undefined_ReturnsTrue()
    {
        _interpreter.Push(JSValue.Undefined);
        _interpreter.IsUndefinedOrNull();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void IsUndefinedOrNull_Null_ReturnsTrue()
    {
        _interpreter.Push(JSValue.Null);
        _interpreter.IsUndefinedOrNull();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void IsUndefinedOrNull_Number_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.IsUndefinedOrNull();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region OpCode Execution

    [Fact]
    public void ExecuteOpCode_Undefined_PushesUndefined()
    {
        _interpreter.ExecuteOpCode(OpCode.Undefined);
        Assert.True(_interpreter.Pop().IsUndefined);
    }

    [Fact]
    public void ExecuteOpCode_Null_PushesNull()
    {
        _interpreter.ExecuteOpCode(OpCode.Null);
        Assert.True(_interpreter.Pop().IsNull);
    }

    [Fact]
    public void ExecuteOpCode_PushTrue_PushesTrue()
    {
        _interpreter.ExecuteOpCode(OpCode.PushTrue);
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void ExecuteOpCode_PushFalse_PushesFalse()
    {
        _interpreter.ExecuteOpCode(OpCode.PushFalse);
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void ExecuteOpCode_Add_AddsValues()
    {
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.Push(JSValue.FromInt32(20));
        _interpreter.ExecuteOpCode(OpCode.Add);
        Assert.Equal(30, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void ExecuteOpCode_Sub_SubtractsValues()
    {
        _interpreter.Push(JSValue.FromInt32(30));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.ExecuteOpCode(OpCode.Sub);
        Assert.Equal(20, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void ExecuteOpCode_Mul_MultipliesValues()
    {
        _interpreter.Push(JSValue.FromInt32(6));
        _interpreter.Push(JSValue.FromInt32(7));
        _interpreter.ExecuteOpCode(OpCode.Mul);
        Assert.Equal(42, _interpreter.Pop().ToInt32());
    }

    [Fact]
    public void ExecuteOpCode_Div_DividesValues()
    {
        _interpreter.Push(JSValue.FromInt32(84));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.ExecuteOpCode(OpCode.Div);
        Assert.Equal(42.0, _interpreter.Pop().ToDouble());
    }

    [Fact]
    public void ExecuteOpCode_Lt_ComparesValues()
    {
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Push(JSValue.FromInt32(10));
        _interpreter.ExecuteOpCode(OpCode.Lt);
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void ExecuteOpCode_StrictEq_ComparesStrict()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.ExecuteOpCode(OpCode.StrictEq);
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void ExecuteOpCode_Drop_DropsValue()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.ExecuteOpCode(OpCode.Drop);
        Assert.Equal(1, _interpreter.StackPointer);
    }

    [Fact]
    public void ExecuteOpCode_Dup_DuplicatesValue()
    {
        _interpreter.Push(JSValue.FromInt32(42));
        _interpreter.ExecuteOpCode(OpCode.Dup);
        Assert.Equal(2, _interpreter.StackPointer);
    }

    [Fact]
    public void ExecuteOpCode_Swap_SwapsValues()
    {
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Push(JSValue.FromInt32(2));
        _interpreter.ExecuteOpCode(OpCode.Swap);
        Assert.Equal(1, _interpreter.Pop().ToInt32());
        Assert.Equal(2, _interpreter.Pop().ToInt32());
    }

    #endregion

    #region NaN Comparisons

    [Fact]
    public void Lt_NaN_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromDouble(double.NaN));
        _interpreter.Push(JSValue.FromInt32(5));
        _interpreter.Lt();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void Eq_NaN_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromDouble(double.NaN));
        _interpreter.Push(JSValue.FromDouble(double.NaN));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    [Fact]
    public void StrictEq_NaN_ReturnsFalse()
    {
        _interpreter.Push(JSValue.FromDouble(double.NaN));
        _interpreter.Push(JSValue.FromDouble(double.NaN));
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region String Comparisons

    [Fact]
    public void Lt_Strings_ComparesLexicographically()
    {
        _interpreter.Push(JSValue.FromString("abc"));
        _interpreter.Push(JSValue.FromString("abd"));
        _interpreter.Lt();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Eq_Strings_ComparesContent()
    {
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void StrictEq_Strings_ComparesContent()
    {
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.Push(JSValue.FromString("hello"));
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    #endregion

    #region Type Coercion in Comparisons

    [Fact]
    public void Eq_BooleanAndNumber_CoercesBooleanToNumber()
    {
        _interpreter.Push(JSValue.True);
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void Eq_FalseAndZero_ReturnsTrue()
    {
        _interpreter.Push(JSValue.False);
        _interpreter.Push(JSValue.FromInt32(0));
        _interpreter.Eq();
        Assert.True(_interpreter.Pop().IsTrue);
    }

    [Fact]
    public void StrictEq_BooleanAndNumber_ReturnsFalse()
    {
        _interpreter.Push(JSValue.True);
        _interpreter.Push(JSValue.FromInt32(1));
        _interpreter.StrictEq();
        Assert.True(_interpreter.Pop().IsFalse);
    }

    #endregion

    #region AvailableStackSpace

    [Fact]
    public void AvailableStackSpace_EmptyStack_ReturnsMaxSize()
    {
        Assert.Equal(_interpreter.MaxStackSize, _interpreter.AvailableStackSpace);
    }

    [Fact]
    public void AvailableStackSpace_DecrementsOnPush()
    {
        int initial = _interpreter.AvailableStackSpace;
        _interpreter.Push(JSValue.Undefined);
        Assert.Equal(initial - 1, _interpreter.AvailableStackSpace);
    }

    #endregion
}
