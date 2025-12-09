// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
/// Represents a decoded line/column entry for a specific bytecode offset.
/// Used internally for pc2line encoding/decoding.
/// </summary>
public readonly struct PCSourceLocation
{
    /// <summary>
    /// The bytecode program counter offset.
    /// </summary>
    public int PC { get; }

    /// <summary>
    /// The 1-based line number in the source.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The 1-based column number in the source.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Creates a new PC source location.
    /// </summary>
    public PCSourceLocation(int pc, int line, int column)
    {
        PC = pc;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Returns a string representation of the source location.
    /// </summary>
    public override string ToString() => $"PC {PC}: line {Line}, col {Column}";
}/// <summary>
/// Constants for pc2line encoding, matching QuickJS.
/// The encoding uses a compact format where small line deltas can be encoded
/// in a single byte along with the PC delta.
/// </summary>
public static class PC2LineConstants
{
    /// <summary>
    /// Base value for line number delta encoding.
    /// A delta of -1 to +3 can be encoded compactly.
    /// </summary>
    public const int Base = -1;

    /// <summary>
    /// Range of line number deltas that can be encoded compactly.
    /// Values from Base to Base+Range-1 (-1 to +3) fit in the compact encoding.
    /// </summary>
    public const int Range = 5;

    /// <summary>
    /// First opcode value used for compact encoding.
    /// 0 is reserved for the extended encoding.
    /// </summary>
    public const int OpFirst = 1;

    /// <summary>
    /// Maximum PC delta that can be encoded in a single byte.
    /// Calculated as (255 - OpFirst) / Range = 50.
    /// </summary>
    public const int DiffPCMax = (255 - OpFirst) / Range;
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
/// <para>
/// The pc2line encoding uses a compact delta format:
/// - Small line deltas (-1 to +3) with small PC deltas (≤50) fit in 2 bytes
/// - Larger deltas use an extended format with LEB128 encoding
/// </para>
/// </remarks>
public sealed class LineNumberTable
{
    private readonly List<LineNumberSlot> _slots;
    private int _lastPc = -1;
    private int _lastSourcePosition = -1;

    /// <summary>
    /// Gets or sets the base line number (1-based) for the function.
    /// </summary>
    public int BaseLine { get; set; } = 1;

    /// <summary>
    /// Gets or sets the base column number (1-based) for the function.
    /// </summary>
    public int BaseColumn { get; set; } = 1;

    /// <summary>
    /// Gets the number of entries in the table.
    /// </summary>
    public int Count => _slots.Count;

    /// <summary>
    /// Gets all slots in the table.
    /// </summary>
    public IReadOnlyList<LineNumberSlot> Slots => _slots;

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
    /// <returns>True if an entry was added, false if deduplicated.</returns>
    public bool Add(int pc, int sourcePosition)
    {
        // Only add if source position changed and PC is not decreasing
        if (sourcePosition == _lastSourcePosition || pc < _lastPc)
        {
            return false;
        }

        _slots.Add(new LineNumberSlot(pc, sourcePosition));
        _lastPc = pc;
        _lastSourcePosition = sourcePosition;
        return true;
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
    /// Finds the source location for a given PC value.
    /// </summary>
    /// <param name="pc">The bytecode offset to look up.</param>
    /// <param name="sourceText">The source text for line/column calculation.</param>
    /// <returns>The source location.</returns>
    public PCSourceLocation FindLocation(int pc, string? sourceText)
    {
        if (_slots.Count == 0 || sourceText == null)
        {
            return new PCSourceLocation(pc, BaseLine, BaseColumn);
        }

        int sourcePos = GetSourcePosition(pc);
        if (sourcePos < 0)
        {
            return new PCSourceLocation(pc, BaseLine, BaseColumn);
        }

        var (line, column) = GetLineColumn(sourceText, sourcePos);
        return new PCSourceLocation(pc, line, column);
    }

    /// <summary>
    /// Computes line and column numbers from a source position.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="position">The byte offset into the source.</param>
    /// <returns>A tuple of (line, column) both 1-based.</returns>
    public static (int Line, int Column) GetLineColumn(string source, int position)
    {
        if (string.IsNullOrEmpty(source) || position < 0)
        {
            return (1, 1);
        }

        int line = 1;
        int column = 1;
        int pos = 0;

        // Clamp position to source length
        if (position > source.Length)
        {
            position = source.Length;
        }

        while (pos < position)
        {
            if (source[pos] == '\n')
            {
                line++;
                column = 1;
            }
            else if (source[pos] == '\r')
            {
                // Handle \r\n as single newline
                if (pos + 1 < source.Length && source[pos + 1] == '\n')
                {
                    pos++;
                }
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
            pos++;
        }

        return (line, column);
    }

    /// <summary>
    /// Adjusts PC values after bytecode modifications.
    /// Used when bytecode is optimized and instructions are removed.
    /// </summary>
    /// <param name="position">The bytecode position where removal occurred.</param>
    /// <param name="delta">The number of bytes removed.</param>
    public void AdjustPC(int position, int delta)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.Pc > position)
            {
                _slots[i] = new LineNumberSlot(slot.Pc - delta, slot.SourcePosition);
            }
        }
    }

    /// <summary>
    /// Gets all line number entries.
    /// </summary>
    /// <returns>A copy of all entries.</returns>
    public LineNumberSlot[] ToArray() => _slots.ToArray();

    /// <summary>
    /// Gets the slot at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The line number slot.</returns>
    public LineNumberSlot this[int index] => _slots[index];

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
    /// Encodes the line number table to the compact pc2line format.
    /// </summary>
    /// <param name="sourceText">The source text for line/column calculation.</param>
    /// <returns>The encoded bytes.</returns>
    public byte[] Encode(string sourceText)
    {
        var buffer = new List<byte>();

        // Write base line and column as LEB128 (0-based in encoding)
        WriteLEB128(buffer, BaseLine - 1);
        WriteLEB128(buffer, BaseColumn - 1);

        if (_slots.Count == 0 || string.IsNullOrEmpty(sourceText))
        {
            return buffer.ToArray();
        }

        int lastPC = 0;
        int lastLine = BaseLine;
        int lastColumn = BaseColumn;

        foreach (var slot in _slots)
        {
            var (line, column) = GetLineColumn(sourceText, slot.SourcePosition);

            int diffPC = slot.Pc - lastPC;
            int diffLine = line - lastLine;
            int diffColumn = column - lastColumn;

            if (diffPC < 0)
                continue;

            if (diffLine == 0 && diffColumn == 0)
                continue;

            // Try compact encoding
            if (diffLine >= PC2LineConstants.Base &&
                diffLine < PC2LineConstants.Base + PC2LineConstants.Range &&
                diffPC <= PC2LineConstants.DiffPCMax)
            {
                // Compact format: single byte encodes both PC delta and line delta
                int op = (diffLine - PC2LineConstants.Base) +
                         diffPC * PC2LineConstants.Range +
                         PC2LineConstants.OpFirst;
                buffer.Add((byte)op);
            }
            else
            {
                // Extended format: 0 prefix, then LEB128 values
                buffer.Add(0);
                WriteLEB128(buffer, diffPC);
                WriteSLEB128(buffer, diffLine);
            }

            // Column delta is always written as signed LEB128
            WriteSLEB128(buffer, diffColumn);

            lastPC = slot.Pc;
            lastLine = line;
            lastColumn = column;
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Decodes a pc2line buffer into source locations.
    /// </summary>
    /// <param name="data">The encoded pc2line data.</param>
    /// <returns>An enumerable of source locations.</returns>
    public static IEnumerable<PCSourceLocation> Decode(byte[] data)
    {
        if (data == null || data.Length == 0)
            yield break;

        int pos = 0;

        // Read base line and column
        int baseLine = (int)ReadLEB128(data, ref pos) + 1;
        int baseColumn = (int)ReadLEB128(data, ref pos) + 1;

        int pc = 0;
        int line = baseLine;
        int column = baseColumn;

        yield return new PCSourceLocation(pc, line, column);

        while (pos < data.Length)
        {
            int op = data[pos++];

            int diffPC;
            int diffLine;

            if (op == 0)
            {
                // Extended format
                diffPC = (int)ReadLEB128(data, ref pos);
                diffLine = ReadSLEB128(data, ref pos);
            }
            else
            {
                // Compact format
                op -= PC2LineConstants.OpFirst;
                diffPC = op / PC2LineConstants.Range;
                diffLine = (op % PC2LineConstants.Range) + PC2LineConstants.Base;
            }

            int diffColumn = ReadSLEB128(data, ref pos);

            pc += diffPC;
            line += diffLine;
            column += diffColumn;

            yield return new PCSourceLocation(pc, line, column);
        }
    }

    /// <summary>
    /// Finds the line and column for a given PC value from encoded data.
    /// </summary>
    /// <param name="data">The encoded pc2line data.</param>
    /// <param name="targetPC">The PC value to look up.</param>
    /// <returns>The source location.</returns>
    public static PCSourceLocation FindInEncoded(byte[] data, int targetPC)
    {
        if (data == null || data.Length == 0)
            return new PCSourceLocation(0, 0, 0);

        int pos = 0;

        // Read base line and column
        int baseLine = (int)ReadLEB128(data, ref pos) + 1;
        int baseColumn = (int)ReadLEB128(data, ref pos) + 1;

        int pc = 0;
        int line = baseLine;
        int column = baseColumn;
        int foundPC = 0;
        int foundLine = line;
        int foundColumn = column;

        while (pos < data.Length)
        {
            int op = data[pos++];

            int diffPC;
            int diffLine;

            if (op == 0)
            {
                diffPC = (int)ReadLEB128(data, ref pos);
                diffLine = ReadSLEB128(data, ref pos);
            }
            else
            {
                op -= PC2LineConstants.OpFirst;
                diffPC = op / PC2LineConstants.Range;
                diffLine = (op % PC2LineConstants.Range) + PC2LineConstants.Base;
            }

            int diffColumn = ReadSLEB128(data, ref pos);

            pc += diffPC;

            if (pc > targetPC)
            {
                return new PCSourceLocation(foundPC, foundLine, foundColumn);
            }

            line += diffLine;
            column += diffColumn;
            foundPC = pc;
            foundLine = line;
            foundColumn = column;
        }

        return new PCSourceLocation(foundPC, foundLine, foundColumn);
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"LineNumberTable(count={_slots.Count})";

    #region LEB128 Encoding

    private static void WriteLEB128(List<byte> buffer, int value)
    {
        uint uvalue = (uint)value;
        do
        {
            byte b = (byte)(uvalue & 0x7F);
            uvalue >>= 7;
            if (uvalue != 0)
                b |= 0x80;
            buffer.Add(b);
        }
        while (uvalue != 0);
    }

    private static void WriteSLEB128(List<byte> buffer, int value)
    {
        bool more = true;
        bool negative = value < 0;

        while (more)
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;

            // Sign extend for negative values
            if (negative)
                value |= -(1 << 24); // Extend sign bits

            // Check if more bytes needed
            if ((value == 0 && (b & 0x40) == 0) ||
                (value == -1 && (b & 0x40) != 0))
            {
                more = false;
            }
            else
            {
                b |= 0x80;
            }

            buffer.Add(b);
        }
    }

    private static uint ReadLEB128(byte[] data, ref int pos)
    {
        uint result = 0;
        int shift = 0;

        while (pos < data.Length)
        {
            byte b = data[pos++];
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }

        return result;
    }

    private static int ReadSLEB128(byte[] data, ref int pos)
    {
        int result = 0;
        int shift = 0;
        byte b = 0;

        do
        {
            if (pos >= data.Length)
                break;
            b = data[pos++];
            result |= (b & 0x7F) << shift;
            shift += 7;
        }
        while ((b & 0x80) != 0);

        // Sign extend
        if (shift < 32 && (b & 0x40) != 0)
        {
            result |= -(1 << shift);
        }

        return result;
    }

    #endregion
}
