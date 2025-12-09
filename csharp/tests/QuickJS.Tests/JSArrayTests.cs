// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSArray"/> - JavaScript Array object implementation.
/// </summary>
public class JSArrayTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_Default_CreatesEmptyArray()
    {
        var array = new JSArray();

        Assert.Equal(0U, array.Length);
        Assert.True(array.IsFastArray);
    }

    [Fact]
    public void Constructor_WithLength_CreatesArrayWithLength()
    {
        var array = new JSArray(10);

        Assert.Equal(10U, array.Length);
        Assert.True(array.IsFastArray);
    }

    [Fact]
    public void Constructor_WithElements_CreatesArrayWithElements()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(3U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    [Fact]
    public void Constructor_FromEnumerable_CreatesArrayFromCollection()
    {
        var values = new List<JSValue> { JSValue.FromString("a"), JSValue.FromString("b"), JSValue.FromString("c") };
        var array = new JSArray(values);

        Assert.Equal(3U, array.Length);
        Assert.Equal("a", array[0].ToString());
        Assert.Equal("b", array[1].ToString());
        Assert.Equal("c", array[2].ToString());
    }

    [Fact]
    public void Constructor_ZeroLength_CreatesEmptyArray()
    {
        var array = new JSArray(0U);

        Assert.Equal(0U, array.Length);
    }

    #endregion

    #region Length Property Tests

    [Fact]
    public void Length_Set_ExpandsArrayWithUndefined()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        array.Length = 5;

        Assert.Equal(5U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.True(array[2].IsUndefined);
        Assert.True(array[3].IsUndefined);
        Assert.True(array[4].IsUndefined);
    }

    [Fact]
    public void Length_Set_TruncatesArray()
    {
        var array = new JSArray(new[]
        {
            JSValue.FromInt32(1),
            JSValue.FromInt32(2),
            JSValue.FromInt32(3),
            JSValue.FromInt32(4),
            JSValue.FromInt32(5)
        });
        array.Length = 2;

        Assert.Equal(2U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
    }

    [Fact]
    public void Length_SetToZero_ClearsArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        array.Length = 0;

        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Length_SetToSame_NoChange()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        array.Length = 3;

        Assert.Equal(3U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    #endregion

    #region Indexer Tests

    [Fact]
    public void Indexer_Get_ReturnsElement()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(10), JSValue.FromInt32(20), JSValue.FromInt32(30) });

        Assert.Equal(10, array[0].ToInt32());
        Assert.Equal(20, array[1].ToInt32());
        Assert.Equal(30, array[2].ToInt32());
    }

    [Fact]
    public void Indexer_Get_OutOfBounds_ReturnsUndefined()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });

        Assert.True(array[10].IsUndefined);
        Assert.True(array[100].IsUndefined);
    }

    [Fact]
    public void Indexer_Set_UpdatesElement()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        array[1] = JSValue.FromInt32(42);

        Assert.Equal(42, array[1].ToInt32());
    }

    [Fact]
    public void Indexer_Set_ExpandsArray()
    {
        var array = new JSArray();
        array[5] = JSValue.FromInt32(100);

        Assert.Equal(6U, array.Length);
        Assert.True(array[0].IsUndefined);
        Assert.True(array[4].IsUndefined);
        Assert.Equal(100, array[5].ToInt32());
    }

    [Fact]
    public void Indexer_Int_Get_ReturnsElement()
    {
        var array = new JSArray(new[] { JSValue.FromString("first"), JSValue.FromString("second") });

        Assert.Equal("first", array[0].ToString());
        Assert.Equal("second", array[1].ToString());
    }

    [Fact]
    public void Indexer_Int_Set_UpdatesElement()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        array[0] = JSValue.FromInt32(999);

        Assert.Equal(999, array[0].ToInt32());
    }

    #endregion

    #region Push Tests

    [Fact]
    public void Push_SingleElement_AppendsAndReturnsLength()
    {
        var array = new JSArray();
        uint result = array.Push(JSValue.FromInt32(42));

        Assert.Equal(1U, result);
        Assert.Equal(1U, array.Length);
        Assert.Equal(42, array[0].ToInt32());
    }

    [Fact]
    public void Push_MultipleElements_AppendsAll()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1) });
        uint result = array.Push(JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4));

        Assert.Equal(4U, result);
        Assert.Equal(4U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
        Assert.Equal(4, array[3].ToInt32());
    }

    [Fact]
    public void Push_ToEmptyArray_CreatesElements()
    {
        var array = new JSArray();
        array.Push(JSValue.FromString("a"));
        array.Push(JSValue.FromString("b"));
        array.Push(JSValue.FromString("c"));

        Assert.Equal(3U, array.Length);
        Assert.Equal("a", array[0].ToString());
        Assert.Equal("b", array[1].ToString());
        Assert.Equal("c", array[2].ToString());
    }

    [Fact]
    public void Push_NoElements_ReturnsCurrentLength()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        uint result = array.Push();

        Assert.Equal(2U, result);
        Assert.Equal(2U, array.Length);
    }

    #endregion

    #region Pop Tests

    [Fact]
    public void Pop_RemovesAndReturnsLastElement()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSValue result = array.Pop();

        Assert.Equal(3, result.ToInt32());
        Assert.Equal(2U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
    }

    [Fact]
    public void Pop_EmptyArray_ReturnsUndefined()
    {
        var array = new JSArray();
        JSValue result = array.Pop();

        Assert.True(result.IsUndefined);
        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Pop_SingleElement_ReturnsElementAndMakesEmpty()
    {
        var array = new JSArray(new[] { JSValue.FromString("only") });
        JSValue result = array.Pop();

        Assert.Equal("only", result.ToString());
        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Pop_MultipleTimes_RemovesInOrder()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(3, array.Pop().ToInt32());
        Assert.Equal(2, array.Pop().ToInt32());
        Assert.Equal(1, array.Pop().ToInt32());
        Assert.True(array.Pop().IsUndefined);
    }

    #endregion

    #region Shift Tests

    [Fact]
    public void Shift_RemovesAndReturnsFirstElement()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSValue result = array.Shift();

        Assert.Equal(1, result.ToInt32());
        Assert.Equal(2U, array.Length);
        Assert.Equal(2, array[0].ToInt32());
        Assert.Equal(3, array[1].ToInt32());
    }

    [Fact]
    public void Shift_EmptyArray_ReturnsUndefined()
    {
        var array = new JSArray();
        JSValue result = array.Shift();

        Assert.True(result.IsUndefined);
        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Shift_SingleElement_ReturnsElementAndMakesEmpty()
    {
        var array = new JSArray(new[] { JSValue.FromString("first") });
        JSValue result = array.Shift();

        Assert.Equal("first", result.ToString());
        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Shift_MultipleTimes_RemovesFromFront()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(1, array.Shift().ToInt32());
        Assert.Equal(2, array.Shift().ToInt32());
        Assert.Equal(3, array.Shift().ToInt32());
        Assert.True(array.Shift().IsUndefined);
    }

    #endregion

    #region Unshift Tests

    [Fact]
    public void Unshift_SingleElement_PrependsAndReturnsLength()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(2), JSValue.FromInt32(3) });
        uint result = array.Unshift(JSValue.FromInt32(1));

        Assert.Equal(3U, result);
        Assert.Equal(3U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    [Fact]
    public void Unshift_MultipleElements_PrependsInOrder()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(3) });
        uint result = array.Unshift(JSValue.FromInt32(1), JSValue.FromInt32(2));

        Assert.Equal(3U, result);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    [Fact]
    public void Unshift_ToEmptyArray_CreatesElements()
    {
        var array = new JSArray();
        array.Unshift(JSValue.FromString("a"), JSValue.FromString("b"));

        Assert.Equal(2U, array.Length);
        Assert.Equal("a", array[0].ToString());
        Assert.Equal("b", array[1].ToString());
    }

    [Fact]
    public void Unshift_NoElements_ReturnsCurrentLength()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1) });
        uint result = array.Unshift();

        Assert.Equal(1U, result);
        Assert.Equal(1U, array.Length);
    }

    #endregion

    #region IndexOf Tests

    [Fact]
    public void IndexOf_ElementExists_ReturnsIndex()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(0, array.IndexOf(JSValue.FromInt32(1)));
        Assert.Equal(1, array.IndexOf(JSValue.FromInt32(2)));
        Assert.Equal(2, array.IndexOf(JSValue.FromInt32(3)));
    }

    [Fact]
    public void IndexOf_ElementNotExists_ReturnsMinusOne()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(-1, array.IndexOf(JSValue.FromInt32(99)));
    }

    [Fact]
    public void IndexOf_WithFromIndex_SearchesFromIndex()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(1), JSValue.FromInt32(2) });

        Assert.Equal(2, array.IndexOf(JSValue.FromInt32(1), 1));
        Assert.Equal(3, array.IndexOf(JSValue.FromInt32(2), 2));
    }

    [Fact]
    public void IndexOf_NegativeFromIndex_SearchesFromEnd()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(1) });

        Assert.Equal(2, array.IndexOf(JSValue.FromInt32(1), -1));
        Assert.Equal(0, array.IndexOf(JSValue.FromInt32(1), -3));
    }

    [Fact]
    public void IndexOf_EmptyArray_ReturnsMinusOne()
    {
        var array = new JSArray();

        Assert.Equal(-1, array.IndexOf(JSValue.FromInt32(1)));
    }

    [Fact]
    public void IndexOf_String_FindsMatch()
    {
        var array = new JSArray(new[] { JSValue.FromString("apple"), JSValue.FromString("banana"), JSValue.FromString("cherry") });

        Assert.Equal(1, array.IndexOf(JSValue.FromString("banana")));
    }

    #endregion

    #region LastIndexOf Tests

    [Fact]
    public void LastIndexOf_ElementExists_ReturnsLastIndex()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(1), JSValue.FromInt32(3) });

        Assert.Equal(2, array.LastIndexOf(JSValue.FromInt32(1)));
    }

    [Fact]
    public void LastIndexOf_ElementNotExists_ReturnsMinusOne()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.Equal(-1, array.LastIndexOf(JSValue.FromInt32(99)));
    }

    [Fact]
    public void LastIndexOf_WithFromIndex_SearchesBackwardsFromIndex()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(1), JSValue.FromInt32(2) });

        Assert.Equal(0, array.LastIndexOf(JSValue.FromInt32(1), 1));
    }

    [Fact]
    public void LastIndexOf_EmptyArray_ReturnsMinusOne()
    {
        var array = new JSArray();

        Assert.Equal(-1, array.LastIndexOf(JSValue.FromInt32(1)));
    }

    #endregion

    #region Includes Tests

    [Fact]
    public void Includes_ElementExists_ReturnsTrue()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.True(array.Includes(JSValue.FromInt32(2)));
    }

    [Fact]
    public void Includes_ElementNotExists_ReturnsFalse()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.False(array.Includes(JSValue.FromInt32(99)));
    }

    [Fact]
    public void Includes_WithFromIndex_SearchesFromIndex()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.False(array.Includes(JSValue.FromInt32(1), 1));
        Assert.True(array.Includes(JSValue.FromInt32(2), 1));
    }

    [Fact]
    public void Includes_NaN_FoundWithSameValueZero()
    {
        var nan = JSValue.FromDouble(double.NaN);
        var array = new JSArray(new[] { JSValue.FromInt32(1), nan, JSValue.FromInt32(3) });

        // includes uses SameValueZero which treats NaN === NaN
        Assert.True(array.Includes(JSValue.FromDouble(double.NaN)));
    }

    [Fact]
    public void Includes_EmptyArray_ReturnsFalse()
    {
        var array = new JSArray();

        Assert.False(array.Includes(JSValue.FromInt32(1)));
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Slice_NoArguments_CopiesEntireArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray slice = array.Slice();

        Assert.Equal(3U, slice.Length);
        Assert.Equal(1, slice[0].ToInt32());
        Assert.Equal(3, slice[2].ToInt32());
        Assert.NotSame(array, slice);
    }

    [Fact]
    public void Slice_WithStart_CopiesFromStart()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        JSArray slice = array.Slice(2);

        Assert.Equal(2U, slice.Length);
        Assert.Equal(3, slice[0].ToInt32());
        Assert.Equal(4, slice[1].ToInt32());
    }

    [Fact]
    public void Slice_WithStartAndEnd_CopiesRange()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4), JSValue.FromInt32(5) });
        JSArray slice = array.Slice(1, 4);

        Assert.Equal(3U, slice.Length);
        Assert.Equal(2, slice[0].ToInt32());
        Assert.Equal(3, slice[1].ToInt32());
        Assert.Equal(4, slice[2].ToInt32());
    }

    [Fact]
    public void Slice_NegativeStart_CountsFromEnd()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        JSArray slice = array.Slice(-2);

        Assert.Equal(2U, slice.Length);
        Assert.Equal(3, slice[0].ToInt32());
        Assert.Equal(4, slice[1].ToInt32());
    }

    [Fact]
    public void Slice_NegativeEnd_CountsFromEnd()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        JSArray slice = array.Slice(1, -1);

        Assert.Equal(2U, slice.Length);
        Assert.Equal(2, slice[0].ToInt32());
        Assert.Equal(3, slice[1].ToInt32());
    }

    [Fact]
    public void Slice_EmptyRange_ReturnsEmptyArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray slice = array.Slice(2, 2);

        Assert.Equal(0U, slice.Length);
    }

    [Fact]
    public void Slice_StartAfterEnd_ReturnsEmptyArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray slice = array.Slice(3, 1);

        Assert.Equal(0U, slice.Length);
    }

    #endregion

    #region Concat Tests

    [Fact]
    public void Concat_SingleArray_CreatesNewCombinedArray()
    {
        var array1 = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        var array2 = new JSArray(new[] { JSValue.FromInt32(3), JSValue.FromInt32(4) });
        JSArray result = array1.Concat(array2);

        Assert.Equal(4U, result.Length);
        Assert.Equal(1, result[0].ToInt32());
        Assert.Equal(2, result[1].ToInt32());
        Assert.Equal(3, result[2].ToInt32());
        Assert.Equal(4, result[3].ToInt32());
        Assert.NotSame(array1, result);
    }

    [Fact]
    public void Concat_MultipleArrays_CombinesAll()
    {
        var array1 = new JSArray(new[] { JSValue.FromInt32(1) });
        var array2 = new JSArray(new[] { JSValue.FromInt32(2) });
        var array3 = new JSArray(new[] { JSValue.FromInt32(3) });
        JSArray result = array1.Concat(array2, array3);

        Assert.Equal(3U, result.Length);
        Assert.Equal(1, result[0].ToInt32());
        Assert.Equal(2, result[1].ToInt32());
        Assert.Equal(3, result[2].ToInt32());
    }

    [Fact]
    public void Concat_EmptyArrays_ReturnsEmptyArray()
    {
        var array1 = new JSArray();
        var array2 = new JSArray();
        JSArray result = array1.Concat(array2);

        Assert.Equal(0U, result.Length);
    }

    [Fact]
    public void Concat_NoArguments_CopiesArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        JSArray result = array.Concat();

        Assert.Equal(2U, result.Length);
        Assert.NotSame(array, result);
    }

    #endregion

    #region Join Tests

    [Fact]
    public void Join_DefaultSeparator_JoinsWithComma()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        string result = array.Join();

        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void Join_CustomSeparator_UsesGivenSeparator()
    {
        var array = new JSArray(new[] { JSValue.FromString("a"), JSValue.FromString("b"), JSValue.FromString("c") });
        string result = array.Join("-");

        Assert.Equal("a-b-c", result);
    }

    [Fact]
    public void Join_EmptyArray_ReturnsEmptyString()
    {
        var array = new JSArray();
        string result = array.Join();

        Assert.Equal("", result);
    }

    [Fact]
    public void Join_SingleElement_ReturnsSingleElementString()
    {
        var array = new JSArray(new[] { JSValue.FromString("only") });
        string result = array.Join(",");

        Assert.Equal("only", result);
    }

    [Fact]
    public void Join_WithNullAndUndefined_TreatsAsEmpty()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.Null, JSValue.FromInt32(3) });
        string result = array.Join(",");

        Assert.Equal("1,,3", result);
    }

    #endregion

    #region Reverse Tests

    [Fact]
    public void Reverse_ReversesArrayInPlace()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray result = array.Reverse();

        Assert.Same(array, result);
        Assert.Equal(3, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(1, array[2].ToInt32());
    }

    [Fact]
    public void Reverse_EmptyArray_NoChange()
    {
        var array = new JSArray();
        array.Reverse();

        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void Reverse_SingleElement_NoChange()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1) });
        array.Reverse();

        Assert.Equal(1, array[0].ToInt32());
    }

    [Fact]
    public void Reverse_TwiceSameOrder()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        array.Reverse();
        array.Reverse();

        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
    }

    #endregion

    #region Fill Tests

    [Fact]
    public void Fill_FillsEntireArray()
    {
        var array = new JSArray(5U);
        array.Fill(JSValue.FromInt32(42));

        Assert.Equal(5U, array.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(42, array[(uint)i].ToInt32());
        }
    }

    [Fact]
    public void Fill_WithStartAndEnd_FillsRange()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4), JSValue.FromInt32(5) });
        array.Fill(JSValue.FromInt32(0), 1, 4);

        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(0, array[1].ToInt32());
        Assert.Equal(0, array[2].ToInt32());
        Assert.Equal(0, array[3].ToInt32());
        Assert.Equal(5, array[4].ToInt32());
    }

    [Fact]
    public void Fill_NegativeIndices_CountsFromEnd()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        array.Fill(JSValue.FromInt32(0), -3, -1);

        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(0, array[1].ToInt32());
        Assert.Equal(0, array[2].ToInt32());
        Assert.Equal(4, array[3].ToInt32());
    }

    [Fact]
    public void Fill_EmptyArray_NoChange()
    {
        var array = new JSArray();
        array.Fill(JSValue.FromInt32(1));

        Assert.Equal(0U, array.Length);
    }

    #endregion

    #region Splice Tests

    [Fact]
    public void Splice_DeleteElements_RemovesFromArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        JSArray deleted = array.Splice(1, 2);

        Assert.Equal(2U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(4, array[1].ToInt32());

        Assert.Equal(2U, deleted.Length);
        Assert.Equal(2, deleted[0].ToInt32());
        Assert.Equal(3, deleted[1].ToInt32());
    }

    [Fact]
    public void Splice_InsertElements_AddsToArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(4) });
        JSArray deleted = array.Splice(1, 0, JSValue.FromInt32(2), JSValue.FromInt32(3));

        Assert.Equal(4U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());
        Assert.Equal(4, array[3].ToInt32());

        Assert.Equal(0U, deleted.Length);
    }

    [Fact]
    public void Splice_ReplaceElements_ReplacesInArray()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray deleted = array.Splice(1, 1, JSValue.FromInt32(20));

        Assert.Equal(3U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(20, array[1].ToInt32());
        Assert.Equal(3, array[2].ToInt32());

        Assert.Equal(1U, deleted.Length);
        Assert.Equal(2, deleted[0].ToInt32());
    }

    [Fact]
    public void Splice_NegativeStart_CountsFromEnd()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3), JSValue.FromInt32(4) });
        array.Splice(-2, 1);

        Assert.Equal(3U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.Equal(2, array[1].ToInt32());
        Assert.Equal(4, array[2].ToInt32());
    }

    [Fact]
    public void Splice_DeleteCountExceedsLength_DeletesRemainder()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSArray deleted = array.Splice(1, 100);

        Assert.Equal(1U, array.Length);
        Assert.Equal(2U, deleted.Length);
    }

    #endregion

    #region ToArray Tests

    [Fact]
    public void ToArray_ReturnsArrayOfJSValues()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        JSValue[] result = array.ToArray();

        Assert.Equal(3, result.Length);
        Assert.Equal(1, result[0].ToInt32());
        Assert.Equal(2, result[1].ToInt32());
        Assert.Equal(3, result[2].ToInt32());
    }

    [Fact]
    public void ToArray_EmptyArray_ReturnsEmptyArray()
    {
        var array = new JSArray();
        JSValue[] result = array.ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void ToArray_ReturnsCopy()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        JSValue[] result = array.ToArray();

        result[0] = JSValue.FromInt32(999);
        Assert.Equal(1, array[0].ToInt32()); // Original unchanged
    }

    #endregion

    #region Enumeration Tests

    [Fact]
    public void GetEnumerator_EnumeratesElements()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        var elements = new List<int>();

        foreach (var element in array)
        {
            elements.Add(element.ToInt32());
        }

        Assert.Equal(new[] { 1, 2, 3 }, elements);
    }

    [Fact]
    public void GetEnumerator_EmptyArray_NoIterations()
    {
        var array = new JSArray();
        int count = 0;

        foreach (var element in array)
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    #endregion

    #region Fast Array Behavior Tests

    [Fact]
    public void IsFastArray_InitiallyTrue()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });

        Assert.True(array.IsFastArray);
    }

    [Fact]
    public void FastArray_PushPop_MaintainsFastPath()
    {
        var array = new JSArray();

        // Push many elements
        for (int i = 0; i < 100; i++)
        {
            array.Push(JSValue.FromInt32(i));
        }

        Assert.Equal(100U, array.Length);
        Assert.True(array.IsFastArray);

        // Pop all elements
        for (int i = 99; i >= 0; i--)
        {
            JSValue val = array.Pop();
            Assert.Equal(i, val.ToInt32());
        }

        Assert.Equal(0U, array.Length);
    }

    [Fact]
    public void SparseArray_SettingHighIndex_StillWorks()
    {
        var array = new JSArray();

        // Set sparse elements
        array[0] = JSValue.FromInt32(1);
        array[100] = JSValue.FromInt32(2);

        Assert.Equal(101U, array.Length);
        Assert.Equal(1, array[0].ToInt32());
        Assert.True(array[50].IsUndefined);
        Assert.Equal(2, array[100].ToInt32());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Array_DifferentTypes_StoresAll()
    {
        var array = new JSArray(new[]
        {
            JSValue.FromInt32(42),
            JSValue.FromString("hello"),
            JSValue.FromDouble(3.14),
            JSValue.True,
            JSValue.Null,
            JSValue.Undefined
        });

        Assert.Equal(6U, array.Length);
        Assert.Equal(42, array[0].ToInt32());
        Assert.Equal("hello", array[1].ToString());
        Assert.True(Math.Abs(3.14 - array[2].ToDouble()) < 0.001);
        Assert.True(array[3].IsBool && array[3].ToBoolean());
        Assert.True(array[4].IsNull);
        Assert.True(array[5].IsUndefined);
    }

    [Fact]
    public void Array_NestedArrays_WorksCorrectly()
    {
        var inner = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) });
        var outer = new JSArray(new[] { JSValue.FromObject(new JSObject()), JSValue.FromObject(inner) });

        Assert.Equal(2U, outer.Length);
    }

    [Fact]
    public void Contains_MatchingElement_ReturnsTrue()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });

        Assert.True(array.Contains(JSValue.FromInt32(2)));
        Assert.False(array.Contains(JSValue.FromInt32(99)));
    }

    [Fact]
    public void Array_ModifyDuringIteration_IndependentCopy()
    {
        var array = new JSArray(new[] { JSValue.FromInt32(1), JSValue.FromInt32(2), JSValue.FromInt32(3) });
        var copy = array.ToArray();

        array.Push(JSValue.FromInt32(4));

        Assert.Equal(3, copy.Length);
        Assert.Equal(4U, array.Length);
    }

    #endregion
}
