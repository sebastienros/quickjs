// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// A dynamic buffer for building bytecode during compilation.
/// </summary>
/// <remarks>
/// <para>
/// This class provides methods for emitting opcodes and their operands,
/// managing labels for control flow, and tracking source positions for debugging.
/// </para>
/// <para>
/// Based on the DynBuf and bytecode emission functions from QuickJS.
/// </para>
/// </remarks>
public sealed class ByteCodeBuffer
{
    private byte[] _buffer;
    private int _size;
    private readonly List<LabelInfo> _labels;
    private int _lastOpcodePosition = -1;

    /// <summary>
    /// Gets the current size (number of bytes written) of the buffer.
    /// </summary>
    public int Size => _size;

    /// <summary>
    /// Gets the capacity of the underlying buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the position of the last opcode emitted, or -1 if none.
    /// </summary>
    public int LastOpcodePosition => _lastOpcodePosition;

    /// <summary>
    /// Gets the number of labels defined.
    /// </summary>
    public int LabelCount => _labels.Count;

    /// <summary>
    /// Gets the position of a label by its index.
    /// </summary>
    /// <param name="labelIndex">The label index.</param>
    /// <returns>The bytecode position of the label.</returns>
    public int GetLabelPosition(int labelIndex)
    {
        if (labelIndex < 0 || labelIndex >= _labels.Count)
            throw new ArgumentOutOfRangeException(nameof(labelIndex));
        return _labels[labelIndex].Position;
    }

    /// <summary>
    /// Truncates the buffer to the specified size.
    /// Used when reparsing (e.g., arrow function detection).
    /// </summary>
    /// <param name="newSize">The new size of the buffer.</param>
    public void Truncate(int newSize)
    {
        if (newSize < 0)
            throw new ArgumentOutOfRangeException(nameof(newSize), "Size cannot be negative");
        if (newSize > _size)
            throw new ArgumentOutOfRangeException(nameof(newSize), "Cannot truncate to larger size");
        _size = newSize;
        if (_lastOpcodePosition >= newSize)
            _lastOpcodePosition = -1;

        // Drop label markers and relocations that point into truncated bytecode.
        for (int i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            if (label.Position >= newSize)
            {
                label.Position = -1;
            }

            RelocEntry? prev = null;
            var entry = label.FirstReloc;
            while (entry != null)
            {
                var next = entry.Next;
                if (entry.Address >= newSize)
                {
                    if (prev == null)
                    {
                        label.FirstReloc = next;
                    }
                    else
                    {
                        prev.Next = next;
                    }
                    if (label.ReferenceCount > 0)
                    {
                        label.ReferenceCount--;
                    }
                }
                else
                {
                    prev = entry;
                }
                entry = next;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteCodeBuffer"/> class.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the buffer.</param>
    public ByteCodeBuffer(int initialCapacity = 256)
    {
        if (initialCapacity < 16)
            initialCapacity = 16;
        _buffer = new byte[initialCapacity];
        _size = 0;
        _labels = new List<LabelInfo>();
    }

    #region Raw Byte Emission

    /// <summary>
    /// Ensures the buffer has enough capacity for the specified number of additional bytes.
    /// </summary>
    /// <param name="additionalBytes">The number of bytes to reserve.</param>
    private void EnsureCapacity(int additionalBytes)
    {
        int required = _size + additionalBytes;
        if (required <= _buffer.Length)
            return;

        int newCapacity = _buffer.Length;
        while (newCapacity < required)
            newCapacity *= 2;

        var newBuffer = new byte[newCapacity];
        Array.Copy(_buffer, 0, newBuffer, 0, _size);
        _buffer = newBuffer;
    }

    /// <summary>
    /// Emits a single byte.
    /// </summary>
    /// <param name="value">The byte value to emit.</param>
    public void EmitU8(byte value)
    {
        EnsureCapacity(1);
        _buffer[_size++] = value;
    }

    /// <summary>
    /// Emits a signed 8-bit integer.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    public void EmitI8(sbyte value)
    {
        EmitU8(unchecked((byte)value));
    }

    /// <summary>
    /// Emits a 16-bit unsigned integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    public void EmitU16(ushort value)
    {
        EnsureCapacity(2);
        _buffer[_size++] = (byte)(value & 0xFF);
        _buffer[_size++] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>
    /// Emits a 16-bit signed integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    public void EmitI16(short value)
    {
        EmitU16(unchecked((ushort)value));
    }

    /// <summary>
    /// Emits a 32-bit unsigned integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    public void EmitU32(uint value)
    {
        EnsureCapacity(4);
        _buffer[_size++] = (byte)(value & 0xFF);
        _buffer[_size++] = (byte)((value >> 8) & 0xFF);
        _buffer[_size++] = (byte)((value >> 16) & 0xFF);
        _buffer[_size++] = (byte)((value >> 24) & 0xFF);
    }

    /// <summary>
    /// Emits a 32-bit signed integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to emit.</param>
    public void EmitI32(int value)
    {
        EmitU32(unchecked((uint)value));
    }

    #endregion

    #region Opcode Emission

    /// <summary>
    /// Emits an opcode and records its position.
    /// </summary>
    /// <param name="opcode">The opcode to emit.</param>
    public void EmitOp(OpCode opcode)
    {
        _lastOpcodePosition = _size;
        EmitU8((byte)opcode);
    }

    /// <summary>
    /// Emits an atom reference (32-bit index).
    /// </summary>
    /// <param name="atom">The atom to emit.</param>
    public void EmitAtom(JSAtom atom)
    {
        EmitU32((uint)atom.Value);
    }

    /// <summary>
    /// Emits a local variable index (16-bit).
    /// </summary>
    /// <param name="index">The local variable index.</param>
    public void EmitLoc(ushort index)
    {
        EmitU16(index);
    }

    /// <summary>
    /// Emits an argument index (16-bit).
    /// </summary>
    /// <param name="index">The argument index.</param>
    public void EmitArg(ushort index)
    {
        EmitU16(index);
    }

    /// <summary>
    /// Emits a variable reference index (16-bit).
    /// </summary>
    /// <param name="index">The variable reference index.</param>
    public void EmitVarRef(ushort index)
    {
        EmitU16(index);
    }

    /// <summary>
    /// Emits a constant pool index (32-bit).
    /// </summary>
    /// <param name="index">The constant pool index.</param>
    public void EmitConst(int index)
    {
        EmitU32((uint)index);
    }

    #endregion

    #region Label Management

    /// <summary>
    /// Creates a new label and returns its index.
    /// </summary>
    /// <returns>The label index.</returns>
    public int DefineLabel()
    {
        int label = _labels.Count;
        _labels.Add(new LabelInfo());
        return label;
    }

    /// <summary>
    /// Gets the label info for a given label index.
    /// </summary>
    /// <param name="label">The label index.</param>
    /// <returns>The label info.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the label index is invalid.</exception>
    public LabelInfo GetLabel(int label)
    {
        if (label < 0 || label >= _labels.Count)
            throw new ArgumentOutOfRangeException(nameof(label), $"Invalid label index: {label}");
        return _labels[label];
    }

    /// <summary>
    /// Marks the current position as the target of a label.
    /// </summary>
    /// <param name="label">The label index to mark.</param>
    /// <remarks>
    /// Labels are non-executable markers used to resolve jumps.
    /// No bytes are emitted into the bytecode stream.
    /// </remarks>
    public void MarkLabel(int label)
    {
        var labelInfo = GetLabel(label);
        labelInfo.Position = _size;
    }

    /// <summary>
    /// Emits a jump instruction to a label.
    /// If the label is not yet marked, a relocation is recorded.
    /// </summary>
    /// <param name="opcode">The jump opcode (e.g., OP_goto, OP_if_true, OP_if_false).</param>
    /// <param name="label">The target label index.</param>
    public void EmitJump(OpCode opcode, int label)
    {
        var labelInfo = GetLabel(label);
        EmitOp(opcode);
        int addressPos = _size;
        EmitU32((uint)label);
        labelInfo.AddReference();

        // Always add a relocation - we'll resolve all jumps at the end
        labelInfo.AddRelocation(addressPos, 4);
    }

    /// <summary>
    /// Resolves all label references in the bytecode, converting label indices to relative offsets.
    /// This should be called after all bytecode has been emitted.
    /// </summary>
    public void ResolveLabels()
    {
        foreach (var labelInfo in _labels)
        {
            if (!labelInfo.IsMarked)
            {
                // Skip unmarked labels - they may be dead code
                continue;
            }

            // Process all relocations for this label
            var reloc = labelInfo.FirstReloc;
            while (reloc != null)
            {
                // Calculate relative offset from the position AFTER the address field
                // The jump instruction reads the offset from addressPos, and pc is at addressPos + 4
                // So the offset should be: targetPosition - (addressPos + 4)
                int targetPosition = labelInfo.Position;
                int jumpFrom = reloc.Address + reloc.Size; // Position after reading the offset
                int relativeOffset = targetPosition - jumpFrom;

                // Patch the bytecode with the relative offset
                PutI32(reloc.Address, relativeOffset);

                reloc = reloc.Next;
            }
        }
    }

    /// <summary>
    /// Sets a 32-bit signed value at the specified offset (little-endian).
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The value to set.</param>
    public void PutI32(int offset, int value)
    {
        if (offset < 0 || offset + 3 >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        _buffer[offset] = (byte)(value & 0xFF);
        _buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        _buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        _buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    /// <summary>
    /// Emits a goto instruction to a label.
    /// If the label doesn't exist, creates it first.
    /// </summary>
    /// <param name="label">The target label index, or -1 to create a new label.</param>
    /// <returns>The label index.</returns>
    public int EmitGoto(int label = -1)
    {
        if (label < 0)
            label = DefineLabel();
        EmitJump(OpCode.Goto, label);
        return label;
    }

    /// <summary>
    /// Emits a conditional jump (if_false) to a label.
    /// </summary>
    /// <param name="label">The target label index, or -1 to create a new label.</param>
    /// <returns>The label index.</returns>
    public int EmitIfFalse(int label = -1)
    {
        if (label < 0)
            label = DefineLabel();
        EmitJump(OpCode.IfFalse, label);
        return label;
    }

    /// <summary>
    /// Emits a conditional jump (if_true) to a label.
    /// </summary>
    /// <param name="label">The target label index, or -1 to create a new label.</param>
    /// <returns>The label index.</returns>
    public int EmitIfTrue(int label = -1)
    {
        if (label < 0)
            label = DefineLabel();
        EmitJump(OpCode.IfTrue, label);
        return label;
    }

    /// <summary>
    /// Updates the reference count for a label.
    /// </summary>
    /// <param name="label">The label index.</param>
    /// <param name="delta">The change in reference count (positive or negative).</param>
    /// <returns>The new reference count.</returns>
    public int UpdateLabelRefCount(int label, int delta)
    {
        var labelInfo = GetLabel(label);
        labelInfo.ReferenceCount += delta;
        return labelInfo.ReferenceCount;
    }

    #endregion

    #region Buffer Access

    /// <summary>
    /// Gets the bytecode as a byte array.
    /// </summary>
    /// <returns>A copy of the bytecode buffer.</returns>
    public byte[] ToArray()
    {
        var result = new byte[_size];
        Array.Copy(_buffer, 0, result, 0, _size);
        return result;
    }

    /// <summary>
    /// Gets the bytecode as a read-only span.
    /// </summary>
    /// <returns>A span over the bytecode.</returns>
    public ReadOnlySpan<byte> AsSpan() => new ReadOnlySpan<byte>(_buffer, 0, _size);

    /// <summary>
    /// Gets the byte at the specified offset.
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The byte value.</returns>
    public byte GetU8(int offset)
    {
        if (offset < 0 || offset >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return _buffer[offset];
    }

    /// <summary>
    /// Gets a 16-bit unsigned value at the specified offset (little-endian).
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The 16-bit value.</returns>
    public ushort GetU16(int offset)
    {
        if (offset < 0 || offset + 1 >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return (ushort)(_buffer[offset] | (_buffer[offset + 1] << 8));
    }

    /// <summary>
    /// Gets a 32-bit unsigned value at the specified offset (little-endian).
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <returns>The 32-bit value.</returns>
    public uint GetU32(int offset)
    {
        if (offset < 0 || offset + 3 >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return (uint)(_buffer[offset] |
                      (_buffer[offset + 1] << 8) |
                      (_buffer[offset + 2] << 16) |
                      (_buffer[offset + 3] << 24));
    }

    /// <summary>
    /// Sets a byte at the specified offset.
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The value to set.</param>
    public void PutU8(int offset, byte value)
    {
        if (offset < 0 || offset >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        _buffer[offset] = value;
    }

    /// <summary>
    /// Sets a 16-bit unsigned value at the specified offset (little-endian).
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The value to set.</param>
    public void PutU16(int offset, ushort value)
    {
        if (offset < 0 || offset + 1 >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        _buffer[offset] = (byte)(value & 0xFF);
        _buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>
    /// Sets a 32-bit unsigned value at the specified offset (little-endian).
    /// </summary>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The value to set.</param>
    public void PutU32(int offset, uint value)
    {
        if (offset < 0 || offset + 3 >= _size)
            throw new ArgumentOutOfRangeException(nameof(offset));
        _buffer[offset] = (byte)(value & 0xFF);
        _buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        _buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        _buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Clears the buffer, resetting it to empty.
    /// </summary>
    public void Clear()
    {
        _size = 0;
        _lastOpcodePosition = -1;
        _labels.Clear();
    }

    /// <summary>
    /// Returns a string representation showing the buffer size.
    /// </summary>
    public override string ToString() => $"ByteCodeBuffer(size={_size}, labels={_labels.Count})";

    #endregion
}
