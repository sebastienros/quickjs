// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="ByteCodeBuffer"/>.
/// </summary>
public class ByteCodeBufferTests
{
    #region Construction

    [Fact]
    public void Constructor_DefaultCapacity_CreatesEmptyBuffer()
    {
        var buffer = new ByteCodeBuffer();

        Assert.Equal(0, buffer.Size);
        Assert.True(buffer.Capacity >= 16);
        Assert.Equal(-1, buffer.LastOpcodePosition);
        Assert.Equal(0, buffer.LabelCount);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(1024)]
    public void Constructor_CustomCapacity_ReservesSpace(int capacity)
    {
        var buffer = new ByteCodeBuffer(capacity);

        Assert.Equal(0, buffer.Size);
        Assert.True(buffer.Capacity >= capacity);
    }

    [Fact]
    public void Constructor_SmallCapacity_UsesMinimum()
    {
        var buffer = new ByteCodeBuffer(1);

        Assert.True(buffer.Capacity >= 16);
    }

    #endregion

    #region Byte Emission

    [Fact]
    public void EmitU8_SingleByte_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitU8(0x42);

        Assert.Equal(1, buffer.Size);
        Assert.Equal(0x42, buffer.GetU8(0));
    }

    [Fact]
    public void EmitI8_SignedByte_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitI8(-10);

        Assert.Equal(1, buffer.Size);
        Assert.Equal(0xF6, buffer.GetU8(0)); // -10 as unsigned byte
    }

    [Fact]
    public void EmitU16_LittleEndian_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitU16(0x1234);

        Assert.Equal(2, buffer.Size);
        Assert.Equal(0x34, buffer.GetU8(0)); // Low byte first
        Assert.Equal(0x12, buffer.GetU8(1)); // High byte second
        Assert.Equal(0x1234, buffer.GetU16(0));
    }

    [Fact]
    public void EmitI16_SignedValue_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitI16(-1000);

        Assert.Equal(2, buffer.Size);
        Assert.Equal(0xFC18, buffer.GetU16(0)); // -1000 as unsigned
    }

    [Fact]
    public void EmitU32_LittleEndian_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitU32(0x12345678);

        Assert.Equal(4, buffer.Size);
        Assert.Equal(0x78, buffer.GetU8(0));
        Assert.Equal(0x56, buffer.GetU8(1));
        Assert.Equal(0x34, buffer.GetU8(2));
        Assert.Equal(0x12, buffer.GetU8(3));
        Assert.Equal(0x12345678u, buffer.GetU32(0));
    }

    [Fact]
    public void EmitI32_SignedValue_WritesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitI32(-1);

        Assert.Equal(4, buffer.Size);
        Assert.Equal(0xFFFFFFFF, buffer.GetU32(0));
    }

    [Fact]
    public void MultipleEmits_AccumulatesCorrectly()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitU8(0x01);
        buffer.EmitU16(0x0203);
        buffer.EmitU32(0x04050607);

        Assert.Equal(7, buffer.Size);
        Assert.Equal(0x01, buffer.GetU8(0));
        Assert.Equal(0x0203, buffer.GetU16(1));
        Assert.Equal(0x04050607u, buffer.GetU32(3));
    }

    [Fact]
    public void Emit_GrowsBuffer_WhenNeeded()
    {
        var buffer = new ByteCodeBuffer(16);

        // Write more than initial capacity
        for (int i = 0; i < 100; i++)
        {
            buffer.EmitU8((byte)i);
        }

        Assert.Equal(100, buffer.Size);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal((byte)i, buffer.GetU8(i));
        }
    }

    #endregion

    #region Opcode Emission

    [Fact]
    public void EmitOp_RecordsPosition()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitU8(0x00); // padding
        buffer.EmitOp(OpCode.PushI32);

        Assert.Equal(2, buffer.Size);
        Assert.Equal(1, buffer.LastOpcodePosition);
        Assert.Equal((byte)OpCode.PushI32, buffer.GetU8(1));
    }

    [Fact]
    public void EmitOp_MultipleOps_TracksLast()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitOp(OpCode.PushI32);
        buffer.EmitU32(42);
        buffer.EmitOp(OpCode.Drop);

        Assert.Equal(6, buffer.Size);
        Assert.Equal(5, buffer.LastOpcodePosition);
    }

    [Fact]
    public void EmitAtom_WritesValue()
    {
        var buffer = new ByteCodeBuffer();
        var atom = new JSAtom(12345);

        buffer.EmitAtom(atom);

        Assert.Equal(4, buffer.Size);
        Assert.Equal(12345u, buffer.GetU32(0));
    }

    [Fact]
    public void EmitLoc_WritesIndex()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitLoc(42);

        Assert.Equal(2, buffer.Size);
        Assert.Equal(42, buffer.GetU16(0));
    }

    [Fact]
    public void EmitConst_WritesIndex()
    {
        var buffer = new ByteCodeBuffer();

        buffer.EmitConst(999);

        Assert.Equal(4, buffer.Size);
        Assert.Equal(999u, buffer.GetU32(0));
    }

    #endregion

    #region Label Management

    [Fact]
    public void DefineLabel_ReturnsSequentialIndices()
    {
        var buffer = new ByteCodeBuffer();

        int label0 = buffer.DefineLabel();
        int label1 = buffer.DefineLabel();
        int label2 = buffer.DefineLabel();

        Assert.Equal(0, label0);
        Assert.Equal(1, label1);
        Assert.Equal(2, label2);
        Assert.Equal(3, buffer.LabelCount);
    }

    [Fact]
    public void GetLabel_ValidIndex_ReturnsLabelInfo()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        var info = buffer.GetLabel(label);

        Assert.NotNull(info);
        Assert.False(info.IsMarked);
        Assert.Equal(0, info.ReferenceCount);
    }

    [Fact]
    public void GetLabel_InvalidIndex_Throws()
    {
        var buffer = new ByteCodeBuffer();

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetLabel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetLabel(-1));
    }

    [Fact]
    public void MarkLabel_SetsPosition()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.EmitU8(0x00); // padding
        buffer.MarkLabel(label);

        var info = buffer.GetLabel(label);
        Assert.True(info.IsMarked);
        Assert.True(info.Position > 0);
    }

    [Fact]
    public void MarkLabel_EmitsLabelOpcode()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.MarkLabel(label);

        Assert.Equal((byte)OpCode.Label, buffer.GetU8(0));
        Assert.Equal(0u, buffer.GetU32(1)); // label index
    }

    [Fact]
    public void EmitJump_AddsReference()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.EmitJump(OpCode.Goto, label);

        var info = buffer.GetLabel(label);
        Assert.Equal(1, info.ReferenceCount);
    }

    [Fact]
    public void EmitJump_UnresolvedLabel_AddsRelocation()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.EmitJump(OpCode.Goto, label);

        var info = buffer.GetLabel(label);
        Assert.True(info.HasRelocations);
    }

    [Fact]
    public void EmitJump_ResolvedLabel_StillHasRelocation()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.MarkLabel(label);
        buffer.EmitJump(OpCode.Goto, label);

        var info = buffer.GetLabel(label);
        // Relocations are always added now so ResolveLabels can patch them uniformly
        Assert.True(info.HasRelocations);
    }

    [Fact]
    public void EmitGoto_CreatesLabel()
    {
        var buffer = new ByteCodeBuffer();

        int label = buffer.EmitGoto();

        Assert.Equal(0, label);
        Assert.Equal(1, buffer.LabelCount);
        Assert.Equal((byte)OpCode.Goto, buffer.GetU8(0));
    }

    [Fact]
    public void EmitGoto_UsesExistingLabel()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        int usedLabel = buffer.EmitGoto(label);

        Assert.Equal(label, usedLabel);
        Assert.Equal(1, buffer.LabelCount);
    }

    [Fact]
    public void EmitIfFalse_EmitsCorrectOpcode()
    {
        var buffer = new ByteCodeBuffer();

        int label = buffer.EmitIfFalse();

        Assert.Equal((byte)OpCode.IfFalse, buffer.GetU8(0));
        Assert.Equal(0, label);
    }

    [Fact]
    public void EmitIfTrue_EmitsCorrectOpcode()
    {
        var buffer = new ByteCodeBuffer();

        int label = buffer.EmitIfTrue();

        Assert.Equal((byte)OpCode.IfTrue, buffer.GetU8(0));
        Assert.Equal(0, label);
    }

    [Fact]
    public void UpdateLabelRefCount_ModifiesCount()
    {
        var buffer = new ByteCodeBuffer();
        int label = buffer.DefineLabel();

        buffer.UpdateLabelRefCount(label, 5);
        Assert.Equal(5, buffer.GetLabel(label).ReferenceCount);

        buffer.UpdateLabelRefCount(label, -2);
        Assert.Equal(3, buffer.GetLabel(label).ReferenceCount);
    }

    #endregion

    #region Buffer Access

    [Fact]
    public void ToArray_ReturnsCopy()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU32(0x12345678);

        var array = buffer.ToArray();

        Assert.Equal(4, array.Length);
        Assert.Equal(0x78, array[0]);
        Assert.Equal(0x56, array[1]);
        Assert.Equal(0x34, array[2]);
        Assert.Equal(0x12, array[3]);

        // Modifying array doesn't affect buffer
        array[0] = 0xFF;
        Assert.Equal(0x78, buffer.GetU8(0));
    }

    [Fact]
    public void AsSpan_ReturnsCorrectView()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU16(0x1234);

        var span = buffer.AsSpan();

        Assert.Equal(2, span.Length);
        Assert.Equal(0x34, span[0]);
        Assert.Equal(0x12, span[1]);
    }

    [Fact]
    public void GetU8_OutOfRange_Throws()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU8(0x42);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetU8(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetU8(-1));
    }

    [Fact]
    public void GetU16_OutOfRange_Throws()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU8(0x42);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetU16(0));
    }

    [Fact]
    public void GetU32_OutOfRange_Throws()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU16(0x1234);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetU32(0));
    }

    [Fact]
    public void PutU8_ModifiesBuffer()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU8(0x00);

        buffer.PutU8(0, 0xFF);

        Assert.Equal(0xFF, buffer.GetU8(0));
    }

    [Fact]
    public void PutU16_ModifiesBuffer()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU16(0x0000);

        buffer.PutU16(0, 0xABCD);

        Assert.Equal(0xABCD, buffer.GetU16(0));
    }

    [Fact]
    public void PutU32_ModifiesBuffer()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU32(0x00000000);

        buffer.PutU32(0, 0xDEADBEEF);

        Assert.Equal(0xDEADBEEF, buffer.GetU32(0));
    }

    [Fact]
    public void PutU8_OutOfRange_Throws()
    {
        var buffer = new ByteCodeBuffer();

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.PutU8(0, 0xFF));
    }

    #endregion

    #region Utilities

    [Fact]
    public void Clear_ResetsBuffer()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU32(0x12345678);
        buffer.DefineLabel();
        buffer.EmitOp(OpCode.Drop);

        buffer.Clear();

        Assert.Equal(0, buffer.Size);
        Assert.Equal(-1, buffer.LastOpcodePosition);
        Assert.Equal(0, buffer.LabelCount);
    }

    [Fact]
    public void ToString_ShowsInfo()
    {
        var buffer = new ByteCodeBuffer();
        buffer.EmitU32(0x12345678);
        buffer.DefineLabel();
        buffer.DefineLabel();

        var str = buffer.ToString();

        Assert.Contains("size=4", str);
        Assert.Contains("labels=2", str);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void SimpleFunction_EmitsCorrectBytecode()
    {
        // Simulating: function f() { return 42; }
        var buffer = new ByteCodeBuffer();

        buffer.EmitOp(OpCode.PushI32);
        buffer.EmitI32(42);
        buffer.EmitOp(OpCode.Return);

        Assert.Equal(6, buffer.Size);
        Assert.Equal((byte)OpCode.PushI32, buffer.GetU8(0));
        Assert.Equal(42u, buffer.GetU32(1));
        Assert.Equal((byte)OpCode.Return, buffer.GetU8(5));
    }

    [Fact]
    public void ConditionalJump_Forward()
    {
        // Simulating: if (x) { ... }
        var buffer = new ByteCodeBuffer();
        int endLabel = buffer.DefineLabel();

        // Push condition and jump if false
        buffer.EmitOp(OpCode.PushTrue);
        buffer.EmitJump(OpCode.IfFalse, endLabel);

        // Then block
        buffer.EmitOp(OpCode.PushI32);
        buffer.EmitI32(1);
        buffer.EmitOp(OpCode.Drop);

        // End label
        buffer.MarkLabel(endLabel);
        buffer.EmitOp(OpCode.Undefined);

        // Verify label was referenced
        Assert.Equal(1, buffer.GetLabel(endLabel).ReferenceCount);
        Assert.True(buffer.GetLabel(endLabel).IsMarked);
    }

    [Fact]
    public void Loop_BackwardJump()
    {
        // Simulating: while (true) { }
        var buffer = new ByteCodeBuffer();
        int loopLabel = buffer.DefineLabel();

        buffer.MarkLabel(loopLabel);
        buffer.EmitOp(OpCode.PushTrue);
        buffer.EmitJump(OpCode.IfTrue, loopLabel);

        Assert.Equal(1, buffer.GetLabel(loopLabel).ReferenceCount);
        Assert.True(buffer.GetLabel(loopLabel).IsMarked);
    }

    #endregion
}
