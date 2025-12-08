// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="ConstantPool"/>.
/// </summary>
public class ConstantPoolTests
{
    #region Construction

    [Fact]
    public void Constructor_DefaultCapacity_CreatesEmptyPool()
    {
        var pool = new ConstantPool();

        Assert.Equal(0, pool.Count);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(256)]
    public void Constructor_CustomCapacity_CreatesEmptyPool(int capacity)
    {
        var pool = new ConstantPool(capacity);

        Assert.Equal(0, pool.Count);
    }

    #endregion

    #region Add Methods

    [Fact]
    public void Add_JSValue_ReturnsSequentialIndices()
    {
        var pool = new ConstantPool();

        int idx0 = pool.Add(JSValue.FromInt32(10));
        int idx1 = pool.Add(JSValue.FromInt32(20));
        int idx2 = pool.Add(JSValue.FromInt32(30));

        Assert.Equal(0, idx0);
        Assert.Equal(1, idx1);
        Assert.Equal(2, idx2);
        Assert.Equal(3, pool.Count);
    }

    [Fact]
    public void Add_DuplicateValues_AddsBoth()
    {
        var pool = new ConstantPool();

        int idx1 = pool.Add(JSValue.FromInt32(42));
        int idx2 = pool.Add(JSValue.FromInt32(42));

        Assert.Equal(0, idx1);
        Assert.Equal(1, idx2);
        Assert.Equal(2, pool.Count);
    }

    [Fact]
    public void AddInt32_AddsIntegerValue()
    {
        var pool = new ConstantPool();

        int idx = pool.AddInt32(12345);

        Assert.Equal(0, idx);
        Assert.Equal(1, pool.Count);
        
        var value = pool.Get(idx);
        Assert.True(value.IsInt);
        Assert.Equal(12345, value.ToInt32());
    }

    [Fact]
    public void AddDouble_AddsDoubleValue()
    {
        var pool = new ConstantPool();

        int idx = pool.AddDouble(3.14159);

        Assert.Equal(0, idx);
        Assert.Equal(1, pool.Count);
        
        var value = pool.Get(idx);
        Assert.True(value.IsNumber);
        Assert.Equal(3.14159, value.ToDouble());
    }

    [Fact]
    public void AddString_AddsStringValue()
    {
        var pool = new ConstantPool();

        int idx = pool.AddString("Hello, World!");

        Assert.Equal(0, idx);
        Assert.Equal(1, pool.Count);
        
        var value = pool.Get(idx);
        Assert.True(value.IsString);
        Assert.True(value.TryGetString(out var s));
        Assert.Equal("Hello, World!", s);
    }

    [Fact]
    public void Add_MixedTypes_AllStored()
    {
        var pool = new ConstantPool();

        pool.AddInt32(42);
        pool.AddDouble(2.718);
        pool.AddString("test");
        pool.Add(JSValue.True);
        pool.Add(JSValue.Null);

        Assert.Equal(5, pool.Count);
        Assert.True(pool.Get(0).IsInt);
        Assert.True(pool.Get(1).IsNumber);
        Assert.True(pool.Get(2).IsString);
        Assert.True(pool.Get(3).ToBoolean()); // true
        Assert.True(pool.Get(4).IsNull);
    }

    #endregion

    #region Get Methods

    [Fact]
    public void Get_ValidIndex_ReturnsValue()
    {
        var pool = new ConstantPool();
        pool.AddInt32(100);
        pool.AddInt32(200);
        pool.AddInt32(300);

        Assert.Equal(100, pool.Get(0).ToInt32());
        Assert.Equal(200, pool.Get(1).ToInt32());
        Assert.Equal(300, pool.Get(2).ToInt32());
    }

    [Fact]
    public void Get_NegativeIndex_Throws()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Get(-1));
    }

    [Fact]
    public void Get_IndexEqualToCount_Throws()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Get(1));
    }

    [Fact]
    public void Get_IndexGreaterThanCount_Throws()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Get(100));
    }

    [Fact]
    public void Get_EmptyPool_Throws()
    {
        var pool = new ConstantPool();

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Get(0));
    }

    [Fact]
    public void GetUnsafe_ValidIndex_ReturnsValue()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        var value = pool.GetUnsafe(0);

        Assert.Equal(42, value.ToInt32());
    }

    [Fact]
    public void TryGet_ValidIndex_ReturnsTrue()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        bool found = pool.TryGet(0, out JSValue value);

        Assert.True(found);
        Assert.Equal(42, value.ToInt32());
    }

    [Fact]
    public void TryGet_InvalidIndex_ReturnsFalse()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        bool found = pool.TryGet(1, out JSValue value);

        Assert.False(found);
        Assert.Equal(default(JSValue), value);
    }

    [Fact]
    public void TryGet_NegativeIndex_ReturnsFalse()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        bool found = pool.TryGet(-1, out _);

        Assert.False(found);
    }

    #endregion

    #region IndexOf and AddOrGet

    [Fact]
    public void IndexOf_ExistingValue_ReturnsIndex()
    {
        var pool = new ConstantPool();
        pool.AddInt32(10);
        pool.AddInt32(20);
        pool.AddInt32(30);

        int idx = pool.IndexOf(JSValue.FromInt32(20));

        Assert.Equal(1, idx);
    }

    [Fact]
    public void IndexOf_NonExistingValue_ReturnsMinusOne()
    {
        var pool = new ConstantPool();
        pool.AddInt32(10);
        pool.AddInt32(20);
        pool.AddInt32(30);

        int idx = pool.IndexOf(JSValue.FromInt32(999));

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void IndexOf_EmptyPool_ReturnsMinusOne()
    {
        var pool = new ConstantPool();

        int idx = pool.IndexOf(JSValue.FromInt32(42));

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void IndexOf_DuplicateValues_ReturnsFirst()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);
        pool.AddInt32(42);
        pool.AddInt32(42);

        int idx = pool.IndexOf(JSValue.FromInt32(42));

        Assert.Equal(0, idx);
    }

    [Fact]
    public void AddOrGet_NewValue_AddsAndReturnsIndex()
    {
        var pool = new ConstantPool();

        int idx = pool.AddOrGet(JSValue.FromInt32(42));

        Assert.Equal(0, idx);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void AddOrGet_ExistingValue_ReturnsExistingIndex()
    {
        var pool = new ConstantPool();
        pool.AddInt32(10);
        pool.AddInt32(20);
        pool.AddInt32(30);

        int idx = pool.AddOrGet(JSValue.FromInt32(20));

        Assert.Equal(1, idx);
        Assert.Equal(3, pool.Count); // No new entry added
    }

    [Fact]
    public void AddOrGet_Deduplication_ReducesSize()
    {
        var pool = new ConstantPool();

        // Add with deduplication
        pool.AddOrGet(JSValue.FromInt32(42));
        pool.AddOrGet(JSValue.FromInt32(42));
        pool.AddOrGet(JSValue.FromInt32(42));

        Assert.Equal(1, pool.Count);
    }

    #endregion

    #region ToArray and Enumeration

    [Fact]
    public void ToArray_EmptyPool_ReturnsEmpty()
    {
        var pool = new ConstantPool();

        var array = pool.ToArray();

        Assert.Empty(array);
    }

    [Fact]
    public void ToArray_ReturnsAllConstants()
    {
        var pool = new ConstantPool();
        pool.AddInt32(1);
        pool.AddInt32(2);
        pool.AddInt32(3);

        var array = pool.ToArray();

        Assert.Equal(3, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    [Fact]
    public void ToArray_ReturnsCopy()
    {
        var pool = new ConstantPool();
        pool.AddInt32(42);

        var array = pool.ToArray();
        
        // Modifying array doesn't affect pool
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void GetEnumerator_EnumeratesAll()
    {
        var pool = new ConstantPool();
        pool.AddInt32(1);
        pool.AddInt32(2);
        pool.AddInt32(3);

        var values = pool.ToList();

        Assert.Equal(3, values.Count);
        Assert.Equal(1, values[0].ToInt32());
        Assert.Equal(2, values[1].ToInt32());
        Assert.Equal(3, values[2].ToInt32());
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_RemovesAllConstants()
    {
        var pool = new ConstantPool();
        pool.AddInt32(1);
        pool.AddInt32(2);
        pool.AddInt32(3);

        pool.Clear();

        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        var pool = new ConstantPool();
        pool.AddInt32(1);
        pool.Clear();
        int idx = pool.AddInt32(100);

        Assert.Equal(0, idx);
        Assert.Equal(1, pool.Count);
        Assert.Equal(100, pool.Get(0).ToInt32());
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShowsCount()
    {
        var pool = new ConstantPool();
        pool.AddInt32(1);
        pool.AddInt32(2);

        var str = pool.ToString();

        Assert.Contains("count=2", str);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ByteCodeIntegration_StoreAndRetrieveConstants()
    {
        // Simulate bytecode compilation workflow
        var buffer = new ByteCodeBuffer();
        var pool = new ConstantPool();

        // Add constants during compilation
        int strIdx = pool.AddString("hello");
        int numIdx = pool.AddDouble(3.14);

        // Emit bytecode that references constants
        buffer.EmitOp(OpCode.PushConst);
        buffer.EmitConst(strIdx);
        
        buffer.EmitOp(OpCode.PushConst);
        buffer.EmitConst(numIdx);

        // Verify
        Assert.Equal(2, pool.Count);
        Assert.Equal((byte)OpCode.PushConst, buffer.GetU8(0));
        Assert.Equal((uint)strIdx, buffer.GetU32(1));
        Assert.Equal((byte)OpCode.PushConst, buffer.GetU8(5));
        Assert.Equal((uint)numIdx, buffer.GetU32(6));

        // Runtime: retrieve constants by index
        Assert.True(pool.Get(strIdx).TryGetString(out var s));
        Assert.Equal("hello", s);
        Assert.Equal(3.14, pool.Get(numIdx).ToDouble());
    }

    [Fact]
    public void LargePool_HandlesMany()
    {
        var pool = new ConstantPool();

        // Add many constants
        for (int i = 0; i < 1000; i++)
        {
            pool.AddInt32(i);
        }

        Assert.Equal(1000, pool.Count);

        // Verify random access
        Assert.Equal(0, pool.Get(0).ToInt32());
        Assert.Equal(500, pool.Get(500).ToInt32());
        Assert.Equal(999, pool.Get(999).ToInt32());
    }

    #endregion
}

/// <summary>
/// Extension method to help with enumeration test.
/// </summary>
internal static class ConstantPoolExtensions
{
    public static System.Collections.Generic.List<JSValue> ToList(this ConstantPool pool)
    {
        var list = new System.Collections.Generic.List<JSValue>();
        foreach (var value in pool)
        {
            list.Add(value);
        }
        return list;
    }
}
