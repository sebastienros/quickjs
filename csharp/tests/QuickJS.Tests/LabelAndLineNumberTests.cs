// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
}
