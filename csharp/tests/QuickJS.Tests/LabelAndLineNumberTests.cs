// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="LabelInfo"/> and <see cref="RelocEntry"/>.
/// </summary>
public class LabelInfoTests
{
    #region LabelInfo Construction

    [Fact]
    public void LabelInfo_DefaultState()
    {
        var label = new LabelInfo();

        Assert.Equal(0, label.ReferenceCount);
        Assert.Equal(-1, label.Position);
        Assert.Equal(-1, label.Position2);
        Assert.Equal(-1, label.Address);
        Assert.False(label.IsMarked);
        Assert.False(label.HasRelocations);
    }

    #endregion

    #region Reference Counting

    [Fact]
    public void AddReference_IncrementsCount()
    {
        var label = new LabelInfo();

        label.AddReference();

        Assert.Equal(1, label.ReferenceCount);
    }

    [Fact]
    public void AddReference_MultipleTimes()
    {
        var label = new LabelInfo();

        label.AddReference();
        label.AddReference();
        label.AddReference();

        Assert.Equal(3, label.ReferenceCount);
    }

    [Fact]
    public void RemoveReference_DecrementsCount()
    {
        var label = new LabelInfo();
        label.ReferenceCount = 3;

        int result = label.RemoveReference();

        Assert.Equal(2, result);
        Assert.Equal(2, label.ReferenceCount);
    }

    [Fact]
    public void RemoveReference_CanGoNegative()
    {
        var label = new LabelInfo();

        label.RemoveReference();

        Assert.Equal(-1, label.ReferenceCount);
    }

    #endregion

    #region Position and Marking

    [Fact]
    public void IsMarked_FalseWhenPositionNegative()
    {
        var label = new LabelInfo { Position = -1 };

        Assert.False(label.IsMarked);
    }

    [Fact]
    public void IsMarked_TrueWhenPositionZero()
    {
        var label = new LabelInfo { Position = 0 };

        Assert.True(label.IsMarked);
    }

    [Fact]
    public void IsMarked_TrueWhenPositionPositive()
    {
        var label = new LabelInfo { Position = 42 };

        Assert.True(label.IsMarked);
    }

    [Fact]
    public void MultiplePhasePositions()
    {
        var label = new LabelInfo
        {
            Position = 100,
            Position2 = 80,
            Address = 50
        };

        Assert.True(label.IsMarked);
        Assert.Equal(100, label.Position);
        Assert.Equal(80, label.Position2);
        Assert.Equal(50, label.Address);
    }

    #endregion

    #region Relocations

    [Fact]
    public void HasRelocations_FalseWhenEmpty()
    {
        var label = new LabelInfo();

        Assert.False(label.HasRelocations);
    }

    [Fact]
    public void AddRelocation_SetsHasRelocations()
    {
        var label = new LabelInfo();

        label.AddRelocation(10, 4);

        Assert.True(label.HasRelocations);
    }

    [Fact]
    public void AddRelocation_CreatesLinkedList()
    {
        var label = new LabelInfo();

        label.AddRelocation(10, 4);
        label.AddRelocation(20, 4);
        label.AddRelocation(30, 2);

        // Verify linked list structure (LIFO order)
        var first = label.FirstReloc;
        Assert.NotNull(first);
        Assert.Equal(30, first!.Address);
        Assert.Equal(2, first.Size);

        var second = first.Next;
        Assert.NotNull(second);
        Assert.Equal(20, second!.Address);
        Assert.Equal(4, second.Size);

        var third = second.Next;
        Assert.NotNull(third);
        Assert.Equal(10, third!.Address);
        Assert.Equal(4, third.Size);

        Assert.Null(third.Next);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShowsInfo()
    {
        var label = new LabelInfo
        {
            Position = 42,
            ReferenceCount = 3
        };

        var str = label.ToString();

        Assert.Contains("pos=42", str);
        Assert.Contains("refs=3", str);
        Assert.Contains("marked=True", str);
    }

    [Fact]
    public void ToString_UnmarkedLabel()
    {
        var label = new LabelInfo();

        var str = label.ToString();

        Assert.Contains("marked=False", str);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="LineNumberSlot"/> and <see cref="LineNumberTable"/>.
/// </summary>
public class LineNumberTableTests
{
    #region LineNumberSlot

    [Fact]
    public void LineNumberSlot_StoresValues()
    {
        var slot = new LineNumberSlot(100, 50);

        Assert.Equal(100, slot.Pc);
        Assert.Equal(50, slot.SourcePosition);
    }

    [Fact]
    public void LineNumberSlot_ToString()
    {
        var slot = new LineNumberSlot(100, 50);

        var str = slot.ToString();

        Assert.Contains("pc=100", str);
        Assert.Contains("src=50", str);
    }

    #endregion

    #region LineNumberTable Construction

    [Fact]
    public void LineNumberTable_DefaultEmpty()
    {
        var table = new LineNumberTable();

        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void LineNumberTable_CustomCapacity()
    {
        var table = new LineNumberTable(100);

        Assert.Equal(0, table.Count);
    }

    #endregion

    #region Adding Entries

    [Fact]
    public void Add_FirstEntry_AddsToTable()
    {
        var table = new LineNumberTable();

        table.Add(0, 10);

        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void Add_DifferentSourcePosition_AddsEntry()
    {
        var table = new LineNumberTable();

        table.Add(0, 10);
        table.Add(5, 20);
        table.Add(10, 30);

        Assert.Equal(3, table.Count);
    }

    [Fact]
    public void Add_SameSourcePosition_DoesNotAddEntry()
    {
        var table = new LineNumberTable();

        table.Add(0, 10);
        table.Add(5, 10);  // Same source position
        table.Add(10, 10); // Same source position

        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void Add_ChangingSourcePosition_AddsEntries()
    {
        var table = new LineNumberTable();

        table.Add(0, 10);
        table.Add(5, 10);  // Same - not added
        table.Add(10, 20); // Different - added
        table.Add(15, 20); // Same - not added
        table.Add(20, 30); // Different - added

        Assert.Equal(3, table.Count);
    }

    #endregion

    #region GetSourcePosition

    [Fact]
    public void GetSourcePosition_EmptyTable_ReturnsMinusOne()
    {
        var table = new LineNumberTable();

        Assert.Equal(-1, table.GetSourcePosition(0));
    }

    [Fact]
    public void GetSourcePosition_ExactMatch()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(10, 20);
        table.Add(20, 30);

        Assert.Equal(20, table.GetSourcePosition(10));
    }

    [Fact]
    public void GetSourcePosition_BetweenEntries()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(10, 20);
        table.Add(20, 30);

        // PC 15 is between entries at PC 10 and PC 20
        Assert.Equal(20, table.GetSourcePosition(15));
    }

    [Fact]
    public void GetSourcePosition_BeforeFirstEntry()
    {
        var table = new LineNumberTable();
        table.Add(10, 100);

        Assert.Equal(-1, table.GetSourcePosition(5));
    }

    [Fact]
    public void GetSourcePosition_AtFirstEntry()
    {
        var table = new LineNumberTable();
        table.Add(0, 100);

        Assert.Equal(100, table.GetSourcePosition(0));
    }

    [Fact]
    public void GetSourcePosition_AfterLastEntry()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(10, 20);
        table.Add(20, 30);

        Assert.Equal(30, table.GetSourcePosition(100));
    }

    #endregion

    #region ToArray

    [Fact]
    public void ToArray_EmptyTable_ReturnsEmpty()
    {
        var table = new LineNumberTable();

        var array = table.ToArray();

        Assert.Empty(array);
    }

    [Fact]
    public void ToArray_ReturnsAllEntries()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(5, 20);
        table.Add(10, 30);

        var array = table.ToArray();

        Assert.Equal(3, array.Length);
        Assert.Equal(0, array[0].Pc);
        Assert.Equal(10, array[0].SourcePosition);
        Assert.Equal(5, array[1].Pc);
        Assert.Equal(20, array[1].SourcePosition);
        Assert.Equal(10, array[2].Pc);
        Assert.Equal(30, array[2].SourcePosition);
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_ResetsTable()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(5, 20);

        table.Clear();

        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Clear();
        table.Add(100, 200);

        Assert.Equal(1, table.Count);
        Assert.Equal(200, table.GetSourcePosition(100));
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShowsCount()
    {
        var table = new LineNumberTable();
        table.Add(0, 10);
        table.Add(5, 20);

        var str = table.ToString();

        Assert.Contains("count=2", str);
    }

    #endregion

    #region GetLineColumn

    [Fact]
    public void GetLineColumn_EmptySource_ReturnsOneOne()
    {
        var (line, column) = LineNumberTable.GetLineColumn("", 0);
        Assert.Equal(1, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void GetLineColumn_PositionZero_ReturnsOneOne()
    {
        var (line, column) = LineNumberTable.GetLineColumn("hello world", 0);
        Assert.Equal(1, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void GetLineColumn_MiddleOfLine_ReturnsCorrectColumn()
    {
        var (line, column) = LineNumberTable.GetLineColumn("hello world", 6);
        Assert.Equal(1, line);
        Assert.Equal(7, column); // "world" starts at column 7
    }

    [Fact]
    public void GetLineColumn_AfterNewline_ReturnsNextLine()
    {
        var (line, column) = LineNumberTable.GetLineColumn("line1\nline2", 6);
        Assert.Equal(2, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void GetLineColumn_MultipleNewlines_CountsCorrectly()
    {
        var (line, column) = LineNumberTable.GetLineColumn("a\nb\nc\nd", 6);
        Assert.Equal(4, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void GetLineColumn_CRLFNewline_TreatedAsSingle()
    {
        var (line, column) = LineNumberTable.GetLineColumn("line1\r\nline2", 7);
        Assert.Equal(2, line);
        Assert.Equal(1, column);
    }

    #endregion

    #region FindLocation

    [Fact]
    public void FindLocation_EmptyTable_ReturnsBase()
    {
        var table = new LineNumberTable
        {
            BaseLine = 10,
            BaseColumn = 5
        };
        
        var loc = table.FindLocation(0, "test");
        
        Assert.Equal(0, loc.PC);
        Assert.Equal(10, loc.Line);
        Assert.Equal(5, loc.Column);
    }

    [Fact]
    public void FindLocation_ValidEntry_ReturnsCorrectLocation()
    {
        var table = new LineNumberTable();
        table.Add(0, 0);    // Line 1
        table.Add(10, 6);   // After newline = Line 2
        
        var loc = table.FindLocation(10, "hello\nworld");
        
        Assert.Equal(10, loc.PC);
        Assert.Equal(2, loc.Line);
        Assert.Equal(1, loc.Column);
    }

    #endregion

    #region AdjustPC

    [Fact]
    public void AdjustPC_ReducesPCAfterPosition()
    {
        var table = new LineNumberTable();
        table.Add(0, 0);
        table.Add(10, 50);
        table.Add(20, 100);
        
        table.AdjustPC(5, 3); // Remove 3 bytes at position 5
        
        Assert.Equal(0, table[0].Pc);   // Before position, unchanged
        Assert.Equal(7, table[1].Pc);   // 10 - 3 = 7
        Assert.Equal(17, table[2].Pc);  // 20 - 3 = 17
    }

    #endregion

    #region Encode/Decode

    [Fact]
    public void Encode_EmptyTable_ReturnsBaseOnly()
    {
        var table = new LineNumberTable
        {
            BaseLine = 1,
            BaseColumn = 1
        };
        
        byte[] encoded = table.Encode("");
        
        Assert.Equal(2, encoded.Length); // Just base line and column
        Assert.Equal(0, encoded[0]); // Line 0 (base 1 - 1)
        Assert.Equal(0, encoded[1]); // Column 0 (base 1 - 1)
    }

    [Fact]
    public void Encode_Decode_Roundtrip_PreservesData()
    {
        var source = "line1\nline2\nline3";
        var table = new LineNumberTable { BaseLine = 1, BaseColumn = 1 };
        table.Add(0, 0);    // Line 1, Col 1
        table.Add(5, 6);    // Line 2, Col 1
        table.Add(10, 12);  // Line 3, Col 1
        
        byte[] encoded = table.Encode(source);
        var decoded = LineNumberTable.Decode(encoded).ToList();
        
        Assert.True(decoded.Count >= 3);
        
        // First entry is the base
        Assert.Equal(0, decoded[0].PC);
        Assert.Equal(1, decoded[0].Line);
        Assert.Equal(1, decoded[0].Column);
    }

    [Fact]
    public void FindInEncoded_ReturnsCorrectLocation()
    {
        var source = "let x = 1;\nlet y = 2;\nlet z = 3;";
        var table = new LineNumberTable { BaseLine = 1, BaseColumn = 1 };
        table.Add(0, 0);    // Line 1
        table.Add(10, 11);  // Line 2
        table.Add(20, 22);  // Line 3
        
        byte[] encoded = table.Encode(source);
        
        var loc = LineNumberTable.FindInEncoded(encoded, 15);
        Assert.Equal(2, loc.Line); // Should be in line 2
    }

    [Fact]
    public void Decode_EmptyData_ReturnsEmpty()
    {
        var decoded = LineNumberTable.Decode(System.Array.Empty<byte>()).ToList();
        Assert.Empty(decoded);
    }

    #endregion

    #region PC2LineConstants

    [Fact]
    public void PC2LineConstants_MatchQuickJS()
    {
        // These constants match QuickJS exactly
        Assert.Equal(-1, PC2LineConstants.Base);
        Assert.Equal(5, PC2LineConstants.Range);
        Assert.Equal(1, PC2LineConstants.OpFirst);
        Assert.Equal(50, PC2LineConstants.DiffPCMax); // (255 - 1) / 5 = 50
    }

    #endregion

    #region PCSourceLocation

    [Fact]
    public void PCSourceLocation_ToString_FormatsCorrectly()
    {
        var loc = new PCSourceLocation(10, 5, 3);
        Assert.Equal("PC 10: line 5, col 3", loc.ToString());
    }

    [Fact]
    public void PCSourceLocation_Properties_Work()
    {
        var loc = new PCSourceLocation(100, 42, 7);
        Assert.Equal(100, loc.PC);
        Assert.Equal(42, loc.Line);
        Assert.Equal(7, loc.Column);
    }

    #endregion

    #region Compact vs Extended Encoding

    [Fact]
    public void Encode_SmallDeltas_UsesCompactFormat()
    {
        // Line delta of 1 (within -1 to +3 range) and small PC delta
        var source = "a\nb";
        var table = new LineNumberTable { BaseLine = 1, BaseColumn = 1 };
        table.Add(0, 0);  // Line 1
        table.Add(5, 2);  // Line 2 (delta +1)
        
        byte[] encoded = table.Encode(source);
        
        // Compact encoding should be small - base (2 bytes) + one entry
        Assert.True(encoded.Length <= 5);
    }

    [Fact]
    public void Encode_LargeLineJump_UsesExtendedFormat()
    {
        // Create source with many lines
        var source = string.Join("\n", Enumerable.Repeat("x", 100));
        var table = new LineNumberTable { BaseLine = 1, BaseColumn = 1 };
        table.Add(0, 0);   // Line 1
        table.Add(10, 99); // Line 50 (large jump, outside -1 to +3)
        
        byte[] encoded = table.Encode(source);
        
        // Extended format uses 0 prefix byte
        // Check if there's a 0 byte after the header
        bool hasZeroPrefix = false;
        for (int i = 2; i < encoded.Length; i++)
        {
            if (encoded[i] == 0)
            {
                hasZeroPrefix = true;
                break;
            }
        }
        Assert.True(hasZeroPrefix);
    }

    #endregion
}
