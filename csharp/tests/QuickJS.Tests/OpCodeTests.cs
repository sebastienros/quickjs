// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for OpCode enum and OpCodes metadata.
/// </summary>
public class OpCodeTests
{
    // ========================================
    // OpCode enum tests
    // ========================================

    [Fact]
    public void Invalid_IsZero()
    {
        Assert.Equal(0, (int)OpCode.Invalid);
    }

    [Fact]
    public void OpCode_IsUShort()
    {
        // All opcodes should fit in a ushort (we use ushort to accommodate
        // both temporary and short opcodes which overlap in QuickJS but
        // are separate enum values in our implementation for clarity)
        foreach (OpCode opCode in Enum.GetValues<OpCode>())
        {
            Assert.True((int)opCode >= 0 && (int)opCode <= 65535);
        }
    }

    [Fact]
    public void OpCode_HasExpectedCount()
    {
        // Should have a reasonable number of opcodes
        // QuickJS has ~180 non-temporary opcodes, plus temporary and short opcodes
        var count = Enum.GetValues<OpCode>().Length;
        Assert.True(count > 100, $"Expected > 100 opcodes, got {count}");
        Assert.True(count < 300, $"Expected < 300 opcodes, got {count}");
    }

    // ========================================
    // Push value opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.PushI32, 5, 0, 1)]
    [InlineData(OpCode.PushConst, 5, 0, 1)]
    [InlineData(OpCode.Undefined, 1, 0, 1)]
    [InlineData(OpCode.Null, 1, 0, 1)]
    [InlineData(OpCode.PushThis, 1, 0, 1)]
    [InlineData(OpCode.PushFalse, 1, 0, 1)]
    [InlineData(OpCode.PushTrue, 1, 0, 1)]
    [InlineData(OpCode.Object, 1, 0, 1)]
    public void PushOpcodes_HaveCorrectStackEffect(OpCode opCode, int size, int pop, int push)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
        Assert.Equal(push - pop, info.StackDelta);
    }

    // ========================================
    // Stack manipulation opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.Drop, 1, 1, 0)]
    [InlineData(OpCode.Dup, 1, 1, 2)]
    [InlineData(OpCode.Dup2, 1, 2, 4)]
    [InlineData(OpCode.Swap, 1, 2, 2)]
    [InlineData(OpCode.Rot3L, 1, 3, 3)]
    [InlineData(OpCode.Nip, 1, 2, 1)]
    public void StackManipulationOpcodes_HaveCorrectStackEffect(OpCode opCode, int size, int pop, int push)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
    }

    // ========================================
    // Control flow opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.IfFalse, 5, 1, 0, OpCodeFormat.Label)]
    [InlineData(OpCode.IfTrue, 5, 1, 0, OpCodeFormat.Label)]
    [InlineData(OpCode.Goto, 5, 0, 0, OpCodeFormat.Label)]
    [InlineData(OpCode.Return, 1, 1, 0, OpCodeFormat.None)]
    [InlineData(OpCode.ReturnUndef, 1, 0, 0, OpCodeFormat.None)]
    [InlineData(OpCode.Throw, 1, 1, 0, OpCodeFormat.None)]
    public void ControlFlowOpcodes_HaveCorrectMetadata(OpCode opCode, int size, int pop, int push, OpCodeFormat format)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
        Assert.Equal(format, info.Format);
    }

    // ========================================
    // Binary operation opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.Add)]
    [InlineData(OpCode.Sub)]
    [InlineData(OpCode.Mul)]
    [InlineData(OpCode.Div)]
    [InlineData(OpCode.Mod)]
    [InlineData(OpCode.Pow)]
    [InlineData(OpCode.Shl)]
    [InlineData(OpCode.Sar)]
    [InlineData(OpCode.Shr)]
    [InlineData(OpCode.And)]
    [InlineData(OpCode.Or)]
    [InlineData(OpCode.Xor)]
    [InlineData(OpCode.Lt)]
    [InlineData(OpCode.Lte)]
    [InlineData(OpCode.Gt)]
    [InlineData(OpCode.Gte)]
    [InlineData(OpCode.Eq)]
    [InlineData(OpCode.Neq)]
    [InlineData(OpCode.StrictEq)]
    [InlineData(OpCode.StrictNeq)]
    public void BinaryOpcodes_Pop2Push1(OpCode opCode)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(1, info.Size);
        Assert.Equal(2, info.Pop);
        Assert.Equal(1, info.Push);
        Assert.Equal(-1, info.StackDelta);
    }

    // ========================================
    // Unary operation opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.Neg)]
    [InlineData(OpCode.Plus)]
    [InlineData(OpCode.Not)]
    [InlineData(OpCode.LNot)]
    [InlineData(OpCode.TypeOf)]
    [InlineData(OpCode.Dec)]
    [InlineData(OpCode.Inc)]
    public void UnaryOpcodes_Pop1Push1(OpCode opCode)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(1, info.Size);
        Assert.Equal(1, info.Pop);
        Assert.Equal(1, info.Push);
        Assert.Equal(0, info.StackDelta);
    }

    // ========================================
    // Local variable opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.GetLoc, 3, 0, 1, OpCodeFormat.Loc)]
    [InlineData(OpCode.PutLoc, 3, 1, 0, OpCodeFormat.Loc)]
    [InlineData(OpCode.SetLoc, 3, 1, 1, OpCodeFormat.Loc)]
    [InlineData(OpCode.GetLoc8, 2, 0, 1, OpCodeFormat.Loc8)]
    [InlineData(OpCode.PutLoc8, 2, 1, 0, OpCodeFormat.Loc8)]
    [InlineData(OpCode.GetLoc0, 1, 0, 1, OpCodeFormat.NoneLoc)]
    [InlineData(OpCode.PutLoc0, 1, 1, 0, OpCodeFormat.NoneLoc)]
    public void LocalVariableOpcodes_HaveCorrectMetadata(OpCode opCode, int size, int pop, int push, OpCodeFormat format)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
        Assert.Equal(format, info.Format);
    }

    // ========================================
    // Short integer push opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.PushMinus1, 1)]
    [InlineData(OpCode.Push0, 1)]
    [InlineData(OpCode.Push1, 1)]
    [InlineData(OpCode.Push2, 1)]
    [InlineData(OpCode.Push3, 1)]
    [InlineData(OpCode.Push4, 1)]
    [InlineData(OpCode.Push5, 1)]
    [InlineData(OpCode.Push6, 1)]
    [InlineData(OpCode.Push7, 1)]
    [InlineData(OpCode.PushI8, 2)]
    [InlineData(OpCode.PushI16, 3)]
    [InlineData(OpCode.PushI32, 5)]
    public void PushIntegerOpcodes_HaveCorrectSize(OpCode opCode, int expectedSize)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(expectedSize, info.Size);
        Assert.Equal(0, info.Pop);
        Assert.Equal(1, info.Push);
    }

    // ========================================
    // Property access opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.GetField, 5, 1, 1, OpCodeFormat.Atom)]
    [InlineData(OpCode.GetField2, 5, 1, 2, OpCodeFormat.Atom)]
    [InlineData(OpCode.PutField, 5, 2, 0, OpCodeFormat.Atom)]
    [InlineData(OpCode.GetArrayEl, 1, 2, 1, OpCodeFormat.None)]
    [InlineData(OpCode.PutArrayEl, 1, 3, 0, OpCodeFormat.None)]
    public void PropertyAccessOpcodes_HaveCorrectMetadata(OpCode opCode, int size, int pop, int push, OpCodeFormat format)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
        Assert.Equal(format, info.Format);
    }

    // ========================================
    // Call opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.Call, 3, OpCodeFormat.NPop)]
    [InlineData(OpCode.CallMethod, 3, OpCodeFormat.NPop)]
    [InlineData(OpCode.CallConstructor, 3, OpCodeFormat.NPop)]
    [InlineData(OpCode.TailCall, 3, OpCodeFormat.NPop)]
    [InlineData(OpCode.Call0, 1, OpCodeFormat.NPopX)]
    [InlineData(OpCode.Call1, 1, OpCodeFormat.NPopX)]
    [InlineData(OpCode.Call2, 1, OpCodeFormat.NPopX)]
    [InlineData(OpCode.Call3, 1, OpCodeFormat.NPopX)]
    public void CallOpcodes_HaveCorrectFormat(OpCode opCode, int size, OpCodeFormat format)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(format, info.Format);
    }

    // ========================================
    // Temporary opcodes
    // Note: Temporary opcodes overlap with short opcodes (they share the same
    // value range since they're mutually exclusive - temporary opcodes are
    // only used during compilation). We look up by name instead of value.
    // ========================================

    [Theory]
    [InlineData("enter_scope")]
    [InlineData("leave_scope")]
    [InlineData("label")]
    [InlineData("scope_get_var")]
    [InlineData("scope_put_var")]
    [InlineData("line_num")]
    public void TemporaryOpcodes_AreMarkedAsTemporary(string opCodeName)
    {
        Assert.True(OpCodes.TryGetByName(opCodeName, out var info), $"OpCode '{opCodeName}' not found");
        Assert.True(info.IsTemporary, $"OpCode '{opCodeName}' should be marked as temporary");
    }

    [Theory]
    [InlineData(OpCode.Add)]
    [InlineData(OpCode.Return)]
    [InlineData(OpCode.GetLoc)]
    [InlineData(OpCode.Goto)]
    public void FinalOpcodes_AreNotMarkedAsTemporary(OpCode opCode)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.False(info.IsTemporary);
    }

    // ========================================
    // OpCodes lookup tests
    // ========================================

    [Fact]
    public void GetInfo_ReturnsCorrectInfo()
    {
        var info = OpCodes.GetInfo(OpCode.Add);
        Assert.Equal(OpCode.Add, info.OpCode);
        Assert.Equal("add", info.Name);
        Assert.Equal(1, info.Size);
        Assert.Equal(2, info.Pop);
        Assert.Equal(1, info.Push);
    }

    [Fact]
    public void TryGetByName_FindsOpcode()
    {
        Assert.True(OpCodes.TryGetByName("add", out var info));
        Assert.Equal(OpCode.Add, info.OpCode);
    }

    [Fact]
    public void TryGetByName_IsCaseInsensitive()
    {
        Assert.True(OpCodes.TryGetByName("ADD", out var info1));
        Assert.True(OpCodes.TryGetByName("Add", out var info2));
        Assert.True(OpCodes.TryGetByName("add", out var info3));
        Assert.Equal(info1.OpCode, info2.OpCode);
        Assert.Equal(info2.OpCode, info3.OpCode);
    }

    [Fact]
    public void TryGetByName_ReturnsFalseForUnknown()
    {
        Assert.False(OpCodes.TryGetByName("unknown_opcode", out _));
    }

    [Fact]
    public void GetSize_ReturnsCorrectSize()
    {
        Assert.Equal(1, OpCodes.GetSize(OpCode.Add));
        Assert.Equal(5, OpCodes.GetSize(OpCode.PushI32));
        Assert.Equal(3, OpCodes.GetSize(OpCode.GetLoc));
        Assert.Equal(2, OpCodes.GetSize(OpCode.GetLoc8));
    }

    [Fact]
    public void GetStackDelta_ReturnsCorrectDelta()
    {
        Assert.Equal(-1, OpCodes.GetStackDelta(OpCode.Add)); // pop 2, push 1
        Assert.Equal(1, OpCodes.GetStackDelta(OpCode.Dup));  // pop 1, push 2
        Assert.Equal(-1, OpCodes.GetStackDelta(OpCode.Drop)); // pop 1, push 0
        Assert.Equal(0, OpCodes.GetStackDelta(OpCode.Swap)); // pop 2, push 2
    }

    [Fact]
    public void All_ReturnsAllOpcodes()
    {
        var all = OpCodes.All.ToList();
        Assert.True(all.Count > 100);
        Assert.Contains(all, i => i.OpCode == OpCode.Add);
        Assert.Contains(all, i => i.OpCode == OpCode.Return);
        Assert.Contains(all, i => i.OpCode == OpCode.Goto);
    }

    // ========================================
    // OpCodeInfo tests
    // ========================================

    [Fact]
    public void OpCodeInfo_ToString_ReturnsDescription()
    {
        var info = OpCodes.GetInfo(OpCode.Add);
        var str = info.ToString();
        Assert.Contains("add", str);
        Assert.Contains("size=1", str);
        Assert.Contains("pop=2", str);
        Assert.Contains("push=1", str);
    }

    [Fact]
    public void OpCodeInfo_StackDelta_CalculatedCorrectly()
    {
        var info = new OpCodeInfo(OpCode.Add, "add", 1, 2, 1, OpCodeFormat.None);
        Assert.Equal(-1, info.StackDelta);

        info = new OpCodeInfo(OpCode.Dup, "dup", 1, 1, 2, OpCodeFormat.None);
        Assert.Equal(1, info.StackDelta);

        info = new OpCodeInfo(OpCode.Nop, "nop", 1, 0, 0, OpCodeFormat.None);
        Assert.Equal(0, info.StackDelta);
    }

    // ========================================
    // OpCodeFormat tests
    // ========================================

    [Theory]
    [InlineData(OpCodeFormat.None, OpCode.Add)]
    [InlineData(OpCodeFormat.I32, OpCode.PushI32)]
    [InlineData(OpCodeFormat.Const, OpCode.PushConst)]
    [InlineData(OpCodeFormat.Atom, OpCode.GetField)]
    [InlineData(OpCodeFormat.Label, OpCode.Goto)]
    [InlineData(OpCodeFormat.Loc, OpCode.GetLoc)]
    [InlineData(OpCodeFormat.Arg, OpCode.GetArg)]
    [InlineData(OpCodeFormat.VarRef, OpCode.GetVarRef)]
    [InlineData(OpCodeFormat.U8, OpCode.SpecialObject)]
    [InlineData(OpCodeFormat.U16, OpCode.Rest)]
    public void OpCodeFormat_CorrectlyAssigned(OpCodeFormat expectedFormat, OpCode opCode)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(expectedFormat, info.Format);
    }

    // ========================================
    // Iteration and async opcodes
    // ========================================

    [Theory]
    [InlineData(OpCode.ForInStart, 1, 1, 1)]
    [InlineData(OpCode.ForOfStart, 1, 1, 3)]
    [InlineData(OpCode.ForInNext, 1, 1, 3)]
    [InlineData(OpCode.Yield, 1, 1, 2)]
    [InlineData(OpCode.Await, 1, 1, 1)]
    public void IterationOpcodes_HaveCorrectStackEffect(OpCode opCode, int size, int pop, int push)
    {
        var info = OpCodes.GetInfo(opCode);
        Assert.Equal(size, info.Size);
        Assert.Equal(pop, info.Pop);
        Assert.Equal(push, info.Push);
    }

    // ========================================
    // Nop opcode
    // ========================================

    [Fact]
    public void Nop_HasNoEffect()
    {
        var info = OpCodes.GetInfo(OpCode.Nop);
        Assert.Equal(1, info.Size);
        Assert.Equal(0, info.Pop);
        Assert.Equal(0, info.Push);
        Assert.Equal(0, info.StackDelta);
        Assert.False(info.IsTemporary);
    }

    // ========================================
    // Invalid opcode
    // ========================================

    [Fact]
    public void Invalid_HasCorrectMetadata()
    {
        var info = OpCodes.GetInfo(OpCode.Invalid);
        Assert.Equal(OpCode.Invalid, info.OpCode);
        Assert.Equal("invalid", info.Name);
        Assert.Equal(1, info.Size);
    }
}
