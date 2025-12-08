// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// A constant pool for storing values used during bytecode execution.
/// </summary>
/// <remarks>
/// <para>
/// The constant pool stores values that are referenced by bytecode instructions:
/// </para>
/// <list type="bullet">
/// <item>String constants</item>
/// <item>Number constants (floats, BigInts)</item>
/// <item>Nested function bytecode</item>
/// <item>Regular expression patterns</item>
/// </list>
/// <para>
/// During compilation, constants are added to the pool and their indices
/// are embedded in the bytecode. At runtime, <c>OP_push_const</c> loads
/// a value from the pool by index.
/// </para>
/// <para>
/// Based on the <c>cpool</c> array in QuickJS's JSFunctionDef and JSFunctionBytecode.
/// </para>
/// </remarks>
public sealed class ConstantPool
{
    private readonly List<JSValue> _constants;

    /// <summary>
    /// Gets the number of constants in the pool.
    /// </summary>
    public int Count => _constants.Count;

    /// <summary>
    /// Initializes a new constant pool.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity.</param>
    public ConstantPool(int initialCapacity = 16)
    {
        _constants = new List<JSValue>(initialCapacity);
    }

    /// <summary>
    /// Adds a value to the constant pool.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index of the constant in the pool.</returns>
    /// <remarks>
    /// This method does not check for duplicates. In QuickJS, the constant pool
    /// may contain duplicate values; deduplication is optional.
    /// </remarks>
    public int Add(JSValue value)
    {
        int index = _constants.Count;
        _constants.Add(value);
        return index;
    }

    /// <summary>
    /// Adds a 32-bit integer constant to the pool.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>The index of the constant in the pool.</returns>
    public int AddInt32(int value)
    {
        return Add(JSValue.FromInt32(value));
    }

    /// <summary>
    /// Adds a double-precision floating point constant to the pool.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>The index of the constant in the pool.</returns>
    public int AddDouble(double value)
    {
        return Add(JSValue.FromDouble(value));
    }

    /// <summary>
    /// Adds a string constant to the pool.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>The index of the constant in the pool.</returns>
    public int AddString(string value)
    {
        return Add(JSValue.FromString(value));
    }

    /// <summary>
    /// Gets the constant at the specified index.
    /// </summary>
    /// <param name="index">The constant pool index.</param>
    /// <returns>The constant value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the index is invalid.</exception>
    public JSValue Get(int index)
    {
        if (index < 0 || index >= _constants.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Invalid constant pool index: {index}");
        return _constants[index];
    }

    /// <summary>
    /// Gets the constant at the specified index without bounds checking.
    /// </summary>
    /// <param name="index">The constant pool index.</param>
    /// <returns>The constant value.</returns>
    /// <remarks>
    /// Use this for performance-critical paths when the index is known to be valid.
    /// </remarks>
    public JSValue GetUnsafe(int index)
    {
        return _constants[index];
    }

    /// <summary>
    /// Tries to get the constant at the specified index.
    /// </summary>
    /// <param name="index">The constant pool index.</param>
    /// <param name="value">The constant value if found.</param>
    /// <returns><c>true</c> if the index is valid; otherwise, <c>false</c>.</returns>
    public bool TryGet(int index, out JSValue value)
    {
        if (index >= 0 && index < _constants.Count)
        {
            value = _constants[index];
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Finds the index of a value in the pool, or -1 if not found.
    /// </summary>
    /// <param name="value">The value to search for.</param>
    /// <returns>The index, or -1 if not found.</returns>
    /// <remarks>
    /// This is an O(n) search. Use sparingly, as constant pools are typically
    /// indexed by compile-time known indices, not searched at runtime.
    /// </remarks>
    public int IndexOf(JSValue value)
    {
        for (int i = 0; i < _constants.Count; i++)
        {
            if (_constants[i].Equals(value))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Adds a value only if it doesn't already exist, returning the existing index if found.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index of the value (existing or new).</returns>
    /// <remarks>
    /// This provides optional deduplication, which can reduce bytecode size
    /// when the same constant appears multiple times.
    /// </remarks>
    public int AddOrGet(JSValue value)
    {
        int existing = IndexOf(value);
        if (existing >= 0)
            return existing;
        return Add(value);
    }

    /// <summary>
    /// Gets all constants as an array.
    /// </summary>
    /// <returns>A copy of all constants.</returns>
    public JSValue[] ToArray()
    {
        return _constants.ToArray();
    }

    /// <summary>
    /// Gets an enumerator over all constants.
    /// </summary>
    /// <returns>An enumerator.</returns>
    public IEnumerator<JSValue> GetEnumerator()
    {
        return _constants.GetEnumerator();
    }

    /// <summary>
    /// Clears all constants from the pool.
    /// </summary>
    public void Clear()
    {
        _constants.Clear();
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"ConstantPool(count={_constants.Count})";
}
