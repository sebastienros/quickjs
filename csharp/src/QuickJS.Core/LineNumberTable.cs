// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Represents a mapping from a bytecode position to a source position.
/// </summary>
/// <remarks>
/// Based on LineNumberSlot from QuickJS.
/// </remarks>
public readonly struct LineNumberSlot
{
    /// <summary>
    /// Gets the bytecode program counter offset.
    /// </summary>
    public int Pc { get; }

    /// <summary>
    /// Gets the source position (offset in source string).
    /// </summary>
    public int SourcePosition { get; }

    /// <summary>
    /// Initializes a new line number slot.
    /// </summary>
    /// <param name="pc">The bytecode offset.</param>
    /// <param name="sourcePosition">The source position.</param>
    public LineNumberSlot(int pc, int sourcePosition)
    {
        Pc = pc;
        SourcePosition = sourcePosition;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"LineNumber(pc={Pc}, src={SourcePosition})";
}

/// <summary>
/// Tracks the mapping from bytecode positions to source positions.
/// </summary>
/// <remarks>
/// <para>
/// This is used for debugging and error messages to map runtime bytecode
/// positions back to the original source code location.
/// </para>
/// <para>
/// In QuickJS, this is encoded as a compact pc2line table in the final bytecode.
/// During compilation, we track the mappings as explicit entries.
/// </para>
/// </remarks>
public sealed class LineNumberTable
{
    private readonly List<LineNumberSlot> _slots;
    private int _lastPc = -1;
    private int _lastSourcePosition = -1;

    /// <summary>
    /// Gets the number of entries in the table.
    /// </summary>
    public int Count => _slots.Count;

    /// <summary>
    /// Initializes a new line number table.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity.</param>
    public LineNumberTable(int initialCapacity = 16)
    {
        _slots = new List<LineNumberSlot>(initialCapacity);
    }

    /// <summary>
    /// Records a line number entry if the source position has changed.
    /// </summary>
    /// <param name="pc">The bytecode program counter.</param>
    /// <param name="sourcePosition">The source position.</param>
    public void Add(int pc, int sourcePosition)
    {
        // Only add if source position changed
        if (sourcePosition != _lastSourcePosition)
        {
            _slots.Add(new LineNumberSlot(pc, sourcePosition));
            _lastPc = pc;
            _lastSourcePosition = sourcePosition;
        }
    }

    /// <summary>
    /// Gets the source position for a given bytecode offset.
    /// </summary>
    /// <param name="pc">The bytecode program counter.</param>
    /// <returns>The source position, or -1 if not found.</returns>
    public int GetSourcePosition(int pc)
    {
        // Binary search for the largest pc less than or equal to the target
        int left = 0;
        int right = _slots.Count - 1;
        int result = -1;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            var slot = _slots[mid];

            if (slot.Pc <= pc)
            {
                result = slot.SourcePosition;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all line number entries.
    /// </summary>
    /// <returns>A copy of all entries.</returns>
    public LineNumberSlot[] ToArray() => _slots.ToArray();

    /// <summary>
    /// Clears all entries.
    /// </summary>
    public void Clear()
    {
        _slots.Clear();
        _lastPc = -1;
        _lastSourcePosition = -1;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"LineNumberTable(count={_slots.Count})";
}
