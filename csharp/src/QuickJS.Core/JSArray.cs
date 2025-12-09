// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript Array object with optimized indexed property storage.
/// </summary>
/// <remarks>
/// <para>
/// JavaScript arrays are exotic objects with special [[DefineOwnProperty]] behavior
/// for the <c>length</c> property and indexed properties. This class implements:
/// </para>
/// <list type="bullet">
/// <item><description>Fast array storage for dense arrays (like QuickJS's fast_array)</description></item>
/// <item><description>Automatic <c>length</c> property synchronization</description></item>
/// <item><description>Array prototype methods (push, pop, slice, etc.)</description></item>
/// <item><description>Fallback to sparse storage for arrays with holes</description></item>
/// </list>
/// <para>
/// In QuickJS, arrays use the following storage:
/// </para>
/// <code>
/// struct { /* JS_CLASS_ARRAY */
///     union {
///         uint32_t size;      // allocated size
///     } u1;
///     union {
///         JSValue *values;    // actual values
///     } u;
///     uint32_t count;         // number of elements
/// } array;
/// </code>
/// <para>
/// The <c>fast_array</c> flag indicates whether the optimized storage is used.
/// When elements are deleted or when getters/setters are defined on indices,
/// the array falls back to regular property storage.
/// </para>
/// </remarks>
[DebuggerDisplay("Array({Length})")]
[DebuggerTypeProxy(typeof(JSArrayDebugView))]
public class JSArray : JSObject, IEnumerable<JSValue>
{
    #region Fields

    /// <summary>
    /// Fast array storage for dense arrays.
    /// </summary>
    private JSValue[] _elements;

    /// <summary>
    /// The number of elements in the fast array.
    /// </summary>
    private uint _count;

    /// <summary>
    /// Whether the fast array mode is active.
    /// When false, falls back to indexed properties on the base JSObject.
    /// </summary>
    private bool _isFastArray;

    /// <summary>
    /// Default initial capacity for fast arrays.
    /// </summary>
    private const int DefaultCapacity = 4;

    /// <summary>
    /// Maximum array length per ECMAScript spec (2^32 - 1).
    /// </summary>
    public const uint MaxArrayLength = 0xFFFFFFFF;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an empty array.
    /// </summary>
    /// <param name="prototype">The array prototype, or null.</param>
    public JSArray(JSObject? prototype = null)
        : base(prototype, JSClassId.Array)
    {
        _elements = Array.Empty<JSValue>();
        _count = 0;
        _isFastArray = true;
    }

    /// <summary>
    /// Creates an array with the specified initial length.
    /// </summary>
    /// <param name="length">The initial length.</param>
    /// <param name="prototype">The array prototype, or null.</param>
    public JSArray(uint length, JSObject? prototype = null)
        : base(prototype, JSClassId.Array)
    {
        if (length > 0)
        {
            _elements = new JSValue[Math.Min(length, (uint)DefaultCapacity * 16)];
            // Initialize with undefined
            for (int i = 0; i < _elements.Length; i++)
            {
                _elements[i] = JSValue.Undefined;
            }
        }
        else
        {
            _elements = Array.Empty<JSValue>();
        }
        _count = length;
        _isFastArray = true;
    }

    /// <summary>
    /// Creates an array from the specified values.
    /// </summary>
    /// <param name="values">The initial values.</param>
    /// <param name="prototype">The array prototype, or null.</param>
    public JSArray(JSValue[] values, JSObject? prototype = null)
        : base(prototype, JSClassId.Array)
    {
        if (values == null || values.Length == 0)
        {
            _elements = Array.Empty<JSValue>();
            _count = 0;
        }
        else
        {
            _elements = new JSValue[values.Length];
            Array.Copy(values, _elements, values.Length);
            _count = (uint)values.Length;
        }
        _isFastArray = true;
    }

    /// <summary>
    /// Creates an array from the specified values.
    /// </summary>
    /// <param name="values">The initial values.</param>
    /// <param name="prototype">The array prototype, or null.</param>
    public JSArray(IEnumerable<JSValue> values, JSObject? prototype = null)
        : base(prototype, JSClassId.Array)
    {
        var list = new List<JSValue>(values);
        if (list.Count == 0)
        {
            _elements = Array.Empty<JSValue>();
            _count = 0;
        }
        else
        {
            _elements = list.ToArray();
            _count = (uint)_elements.Length;
        }
        _isFastArray = true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the length of the array.
    /// </summary>
    /// <remarks>
    /// Setting the length to a smaller value truncates the array.
    /// Setting it to a larger value extends the array with undefined values.
    /// </remarks>
    public uint Length
    {
        get => _count;
        set => SetLength(value);
    }

    /// <summary>
    /// Gets whether this array is using fast array storage.
    /// </summary>
    public bool IsFastArray => _isFastArray;

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The element at the index, or undefined if out of range.</returns>
    public JSValue this[uint index]
    {
        get => GetElement(index);
        set => SetElement(index, value);
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The element at the index, or undefined if out of range.</returns>
    public JSValue this[int index]
    {
        get => index >= 0 ? GetElement((uint)index) : JSValue.Undefined;
        set
        {
            if (index >= 0)
                SetElement((uint)index, value);
        }
    }

    #endregion

    #region Element Access

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The element, or undefined if out of range.</returns>
    public JSValue GetElement(uint index)
    {
        if (_isFastArray)
        {
            if (index < _count && index < (uint)_elements.Length)
            {
                return _elements[index];
            }
            return JSValue.Undefined;
        }
        else
        {
            // Fall back to indexed property lookup
            return Get(index);
        }
    }

    /// <summary>
    /// Sets the element at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The value to set.</param>
    /// <returns><c>true</c> if the element was set; <c>false</c> if the array is not extensible.</returns>
    public bool SetElement(uint index, JSValue value)
    {
        if (_isFastArray)
        {
            // Check if we need to extend
            if (index >= (uint)_elements.Length)
            {
                if (!IsExtensible)
                    return false;

                EnsureCapacity(index + 1);
            }

            _elements[index] = value;

            // Update count if needed
            if (index >= _count)
            {
                _count = index + 1;
            }

            return true;
        }
        else
        {
            // Fall back to indexed property
            return Set(index, value);
        }
    }

    /// <summary>
    /// Checks whether the array has an element at the specified index.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <returns><c>true</c> if the element exists and is not undefined.</returns>
    public bool HasElement(uint index)
    {
        if (_isFastArray)
        {
            return index < _count && index < (uint)_elements.Length;
        }
        else
        {
            return HasOwnProperty(index);
        }
    }

    /// <summary>
    /// Deletes the element at the specified index.
    /// </summary>
    /// <param name="index">The index to delete.</param>
    /// <returns><c>true</c> if the element was deleted.</returns>
    public bool DeleteElement(uint index)
    {
        if (_isFastArray)
        {
            if (index < _count && index < (uint)_elements.Length)
            {
                _elements[index] = JSValue.Undefined;
                return true;
            }
            return true; // Non-existent elements are considered deleted
        }
        else
        {
            return Delete(index);
        }
    }

    #endregion

    #region Length Management

    /// <summary>
    /// Sets the length of the array.
    /// </summary>
    private void SetLength(uint newLength)
    {
        if (newLength == _count)
            return;

        if (_isFastArray)
        {
            if (newLength < _count)
            {
                // Truncate: clear elements beyond new length
                for (uint i = newLength; i < _count && i < (uint)_elements.Length; i++)
                {
                    _elements[i] = JSValue.Undefined;
                }
            }
            else if (newLength > (uint)_elements.Length)
            {
                // Extend capacity
                EnsureCapacity(newLength);
            }

            _count = newLength;
        }
        else
        {
            // For sparse arrays, we need to delete properties beyond new length
            if (newLength < _count)
            {
                for (uint i = newLength; i < _count; i++)
                {
                    Delete(i);
                }
            }
            _count = newLength;
        }
    }

    /// <summary>
    /// Ensures the internal array has at least the specified capacity.
    /// </summary>
    private void EnsureCapacity(uint minCapacity)
    {
        if (minCapacity > MaxArrayLength)
        {
            throw new InvalidOperationException("Array length exceeds maximum allowed length");
        }

        if ((uint)_elements.Length >= minCapacity)
            return;

        // Calculate new capacity (grow by 1.5x or to minCapacity, whichever is larger)
        uint newCapacity = (uint)_elements.Length;
        if (newCapacity == 0)
            newCapacity = DefaultCapacity;

        while (newCapacity < minCapacity)
        {
            newCapacity = Math.Min(newCapacity + newCapacity / 2 + 1, MaxArrayLength);
            if (newCapacity < minCapacity && newCapacity == MaxArrayLength)
                break;
        }

        if (newCapacity < minCapacity)
            newCapacity = minCapacity;

        // Resize
        var newElements = new JSValue[newCapacity];
        if (_elements.Length > 0)
        {
            Array.Copy(_elements, newElements, _elements.Length);
        }

        // Initialize new slots to undefined
        for (int i = _elements.Length; i < newElements.Length; i++)
        {
            newElements[i] = JSValue.Undefined;
        }

        _elements = newElements;
    }

    #endregion

    #region Stack Operations (push, pop, shift, unshift)

    /// <summary>
    /// Appends one or more elements to the end of the array and returns the new length.
    /// </summary>
    /// <param name="values">The values to append.</param>
    /// <returns>The new length of the array.</returns>
    public uint Push(params JSValue[] values)
    {
        if (values == null || values.Length == 0)
            return _count;

        uint newLength = _count + (uint)values.Length;
        EnsureCapacity(newLength);

        for (int i = 0; i < values.Length; i++)
        {
            _elements[_count + i] = values[i];
        }

        _count = newLength;
        return _count;
    }

    /// <summary>
    /// Removes the last element from the array and returns it.
    /// </summary>
    /// <returns>The removed element, or undefined if the array is empty.</returns>
    public JSValue Pop()
    {
        if (_count == 0)
            return JSValue.Undefined;

        uint lastIndex = _count - 1;
        JSValue value;

        if (_isFastArray && lastIndex < (uint)_elements.Length)
        {
            value = _elements[lastIndex];
            _elements[lastIndex] = JSValue.Undefined;
        }
        else
        {
            value = Get(lastIndex);
            Delete(lastIndex);
        }

        _count--;
        return value;
    }

    /// <summary>
    /// Removes the first element from the array and returns it, shifting all other elements down.
    /// </summary>
    /// <returns>The removed element, or undefined if the array is empty.</returns>
    public JSValue Shift()
    {
        if (_count == 0)
            return JSValue.Undefined;

        JSValue first;

        if (_isFastArray)
        {
            first = _elements[0];

            // Shift all elements down
            for (uint i = 0; i < _count - 1 && i < (uint)_elements.Length - 1; i++)
            {
                _elements[i] = _elements[i + 1];
            }

            if (_count - 1 < (uint)_elements.Length)
            {
                _elements[_count - 1] = JSValue.Undefined;
            }
        }
        else
        {
            first = Get(0);
            // Shift using property operations
            for (uint i = 0; i < _count - 1; i++)
            {
                Set(i, Get(i + 1));
            }
            Delete(_count - 1);
        }

        _count--;
        return first;
    }

    /// <summary>
    /// Adds one or more elements to the beginning of the array and returns the new length.
    /// </summary>
    /// <param name="values">The values to add.</param>
    /// <returns>The new length of the array.</returns>
    public uint Unshift(params JSValue[] values)
    {
        if (values == null || values.Length == 0)
            return _count;

        uint insertCount = (uint)values.Length;
        uint newLength = _count + insertCount;
        EnsureCapacity(newLength);

        if (_isFastArray)
        {
            // Shift existing elements up
            for (int i = (int)_count - 1; i >= 0; i--)
            {
                _elements[i + values.Length] = _elements[i];
            }

            // Insert new values at the beginning
            for (int i = 0; i < values.Length; i++)
            {
                _elements[i] = values[i];
            }
        }
        else
        {
            // Shift using property operations
            for (int i = (int)_count - 1; i >= 0; i--)
            {
                Set((uint)(i + values.Length), Get((uint)i));
            }

            for (int i = 0; i < values.Length; i++)
            {
                Set((uint)i, values[i]);
            }
        }

        _count = newLength;
        return _count;
    }

    #endregion

    #region Slice and Splice

    /// <summary>
    /// Returns a shallow copy of a portion of the array.
    /// </summary>
    /// <param name="start">The start index (inclusive). Negative values count from the end.</param>
    /// <param name="end">The end index (exclusive). Negative values count from the end. Defaults to length.</param>
    /// <returns>A new array containing the extracted elements.</returns>
    public JSArray Slice(int start = 0, int? end = null)
    {
        int len = (int)_count;

        // Normalize start
        int actualStart = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);

        // Normalize end
        int actualEnd = end.HasValue
            ? (end.Value < 0 ? Math.Max(len + end.Value, 0) : Math.Min(end.Value, len))
            : len;

        int count = Math.Max(0, actualEnd - actualStart);
        var result = new JSArray((uint)count, Prototype);

        for (int i = 0; i < count; i++)
        {
            result._elements[i] = GetElement((uint)(actualStart + i));
        }
        result._count = (uint)count;

        return result;
    }

    /// <summary>
    /// Changes the contents of an array by removing or replacing existing elements and/or adding new elements.
    /// </summary>
    /// <param name="start">The index at which to start changing the array.</param>
    /// <param name="deleteCount">The number of elements to remove.</param>
    /// <param name="items">The elements to add.</param>
    /// <returns>An array containing the deleted elements.</returns>
    public JSArray Splice(int start, int deleteCount, params JSValue[] items)
    {
        int len = (int)_count;

        // Normalize start
        int actualStart = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);

        // Clamp delete count
        int actualDeleteCount = Math.Min(Math.Max(deleteCount, 0), len - actualStart);

        // Create result array with deleted items
        var deleted = new JSArray((uint)actualDeleteCount, Prototype);
        for (int i = 0; i < actualDeleteCount; i++)
        {
            deleted._elements[i] = GetElement((uint)(actualStart + i));
        }
        deleted._count = (uint)actualDeleteCount;

        int insertCount = items?.Length ?? 0;
        int delta = insertCount - actualDeleteCount;

        if (_isFastArray)
        {
            if (delta > 0)
            {
                // Need to expand and shift right
                EnsureCapacity((uint)(len + delta));
                for (int i = len - 1; i >= actualStart + actualDeleteCount; i--)
                {
                    _elements[i + delta] = _elements[i];
                }
            }
            else if (delta < 0)
            {
                // Shift left
                for (int i = actualStart + actualDeleteCount; i < len; i++)
                {
                    _elements[i + delta] = _elements[i];
                }
                // Clear the freed slots
                for (int i = len + delta; i < len; i++)
                {
                    _elements[i] = JSValue.Undefined;
                }
            }

            // Insert new items
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    _elements[actualStart + i] = items[i];
                }
            }

            _count = (uint)(len + delta);
        }
        else
        {
            // Handle using property operations for sparse arrays
            // This is less efficient but correct
            if (delta != 0)
            {
                if (delta > 0)
                {
                    // Shift right
                    for (int i = len - 1; i >= actualStart + actualDeleteCount; i--)
                    {
                        Set((uint)(i + delta), Get((uint)i));
                    }
                }
                else
                {
                    // Shift left
                    for (int i = actualStart + actualDeleteCount; i < len; i++)
                    {
                        Set((uint)(i + delta), Get((uint)i));
                    }
                    for (int i = len + delta; i < len; i++)
                    {
                        Delete((uint)i);
                    }
                }
            }

            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Set((uint)(actualStart + i), items[i]);
                }
            }

            _count = (uint)(len + delta);
        }

        return deleted;
    }

    #endregion

    #region Search Methods

    /// <summary>
    /// Returns the first index at which a given element can be found, or -1 if not present.
    /// </summary>
    /// <param name="searchElement">The element to locate.</param>
    /// <param name="fromIndex">The index to start searching from.</param>
    /// <returns>The index of the element, or -1 if not found.</returns>
    public int IndexOf(JSValue searchElement, int fromIndex = 0)
    {
        int len = (int)_count;
        if (len == 0)
            return -1;

        int start = fromIndex < 0 ? Math.Max(len + fromIndex, 0) : Math.Min(fromIndex, len);

        for (int i = start; i < len; i++)
        {
            JSValue element = GetElement((uint)i);
            if (element.Equals(searchElement))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Returns the last index at which a given element can be found, or -1 if not present.
    /// </summary>
    /// <param name="searchElement">The element to locate.</param>
    /// <param name="fromIndex">The index to start searching backwards from.</param>
    /// <returns>The index of the element, or -1 if not found.</returns>
    public int LastIndexOf(JSValue searchElement, int? fromIndex = null)
    {
        int len = (int)_count;
        if (len == 0)
            return -1;

        int start = fromIndex.HasValue
            ? (fromIndex.Value < 0 ? len + fromIndex.Value : Math.Min(fromIndex.Value, len - 1))
            : len - 1;

        for (int i = start; i >= 0; i--)
        {
            JSValue element = GetElement((uint)i);
            if (element.Equals(searchElement))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Determines whether the array includes a certain element.
    /// </summary>
    /// <param name="searchElement">The element to search for.</param>
    /// <param name="fromIndex">The index to start searching from.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Includes(JSValue searchElement, int fromIndex = 0)
    {
        int len = (int)_count;
        if (len == 0)
            return false;

        int start = fromIndex < 0 ? Math.Max(len + fromIndex, 0) : Math.Min(fromIndex, len);

        for (int i = start; i < len; i++)
        {
            JSValue element = GetElement((uint)i);
            // includes uses SameValueZero, which treats NaN === NaN
            if (SameValueZero(element, searchElement))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Implements the SameValueZero comparison (used by includes).
    /// </summary>
    private static bool SameValueZero(JSValue x, JSValue y)
    {
        if (x.Tag != y.Tag)
            return false;

        if (x.IsNumber && y.IsNumber)
        {
            double dx = x.ToDouble();
            double dy = y.ToDouble();
            if (double.IsNaN(dx) && double.IsNaN(dy))
                return true;
            return dx == dy;
        }

        return x.Equals(y);
    }

    #endregion

    #region Concatenation and Joining

    /// <summary>
    /// Merges two or more arrays into a new array.
    /// </summary>
    /// <param name="arrays">Arrays to concatenate.</param>
    /// <returns>A new array containing all elements.</returns>
    public JSArray Concat(params JSArray[] arrays)
    {
        uint totalLength = _count;
        if (arrays != null)
        {
            foreach (var arr in arrays)
            {
                if (arr != null)
                    totalLength += arr._count;
            }
        }

        var result = new JSArray(totalLength, Prototype);

        // Copy this array
        uint idx = 0;
        for (uint i = 0; i < _count; i++)
        {
            result._elements[idx++] = GetElement(i);
        }

        // Copy other arrays
        if (arrays != null)
        {
            foreach (var arr in arrays)
            {
                if (arr != null)
                {
                    for (uint i = 0; i < arr._count; i++)
                    {
                        result._elements[idx++] = arr.GetElement(i);
                    }
                }
            }
        }

        result._count = totalLength;
        return result;
    }

    /// <summary>
    /// Joins all elements of the array into a string.
    /// </summary>
    /// <param name="separator">The separator to use between elements.</param>
    /// <returns>A string with all elements joined.</returns>
    public string Join(string separator = ",")
    {
        if (_count == 0)
            return "";

        var parts = new string[_count];
        for (uint i = 0; i < _count; i++)
        {
            JSValue element = GetElement(i);
            if (element.IsUndefined || element.IsNull)
                parts[i] = "";
            else
                parts[i] = element.ToString();
        }

        return string.Join(separator, parts);
    }

    #endregion

    #region Reverse and Fill

    /// <summary>
    /// Reverses the array in place.
    /// </summary>
    /// <returns>The reversed array (same instance).</returns>
    public JSArray Reverse()
    {
        if (_count <= 1)
            return this;

        if (_isFastArray)
        {
            uint left = 0;
            uint right = _count - 1;
            while (left < right)
            {
                JSValue temp = _elements[left];
                _elements[left] = _elements[right];
                _elements[right] = temp;
                left++;
                right--;
            }
        }
        else
        {
            uint left = 0;
            uint right = _count - 1;
            while (left < right)
            {
                JSValue leftVal = Get(left);
                JSValue rightVal = Get(right);
                Set(left, rightVal);
                Set(right, leftVal);
                left++;
                right--;
            }
        }

        return this;
    }

    /// <summary>
    /// Fills all elements from a start index to an end index with a static value.
    /// </summary>
    /// <param name="value">The value to fill with.</param>
    /// <param name="start">The start index.</param>
    /// <param name="end">The end index (exclusive).</param>
    /// <returns>The modified array (same instance).</returns>
    public JSArray Fill(JSValue value, int start = 0, int? end = null)
    {
        int len = (int)_count;

        int actualStart = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);
        int actualEnd = end.HasValue
            ? (end.Value < 0 ? Math.Max(len + end.Value, 0) : Math.Min(end.Value, len))
            : len;

        if (_isFastArray)
        {
            for (int i = actualStart; i < actualEnd; i++)
            {
                _elements[i] = value;
            }
        }
        else
        {
            for (int i = actualStart; i < actualEnd; i++)
            {
                Set((uint)i, value);
            }
        }

        return this;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates an array from an iterable or array-like object.
    /// </summary>
    /// <param name="values">The values to create the array from.</param>
    /// <returns>A new array.</returns>
    public static JSArray From(IEnumerable<JSValue> values)
    {
        return new JSArray(values);
    }

    /// <summary>
    /// Creates an array from the specified elements.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <returns>A new array.</returns>
    public static JSArray Of(params JSValue[] elements)
    {
        return new JSArray(elements);
    }

    /// <summary>
    /// Determines whether the given value is an array.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><c>true</c> if the value is an array; otherwise, <c>false</c>.</returns>
    public static bool IsArray(JSValue value)
    {
        if (value.TryGetObject(out var obj))
        {
            return obj is JSArray || obj.ClassId == JSClassId.Array;
        }
        return false;
    }

    #endregion

    #region IEnumerable Implementation

    /// <summary>
    /// Returns an enumerator that iterates through the array.
    /// </summary>
    public IEnumerator<JSValue> GetEnumerator()
    {
        for (uint i = 0; i < _count; i++)
        {
            yield return GetElement(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Conversion

    /// <summary>
    /// Converts the array to a native array.
    /// </summary>
    /// <returns>A new array containing all elements.</returns>
    public JSValue[] ToArray()
    {
        var result = new JSValue[_count];
        for (uint i = 0; i < _count; i++)
        {
            result[i] = GetElement(i);
        }
        return result;
    }

    /// <summary>
    /// Returns a string representation of the array.
    /// </summary>
    public override string ToString()
    {
        return Join(",");
    }

    #endregion

    #region Debug Support

    /// <summary>
    /// Debug view for the array.
    /// </summary>
    private sealed class JSArrayDebugView
    {
        private readonly JSArray _array;

        public JSArrayDebugView(JSArray array)
        {
            _array = array;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public JSValue[] Items => _array.ToArray();
    }

    #endregion
}
