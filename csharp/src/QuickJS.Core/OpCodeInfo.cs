// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace QuickJS;

/// <summary>
/// Specifies the operand format for an opcode.
/// Determines how many bytes follow the opcode and how they should be interpreted.
/// </summary>
public enum OpCodeFormat : byte
{
    /// <summary>No operands (1 byte total).</summary>
    None,

    /// <summary>No operands, but encodes an int value in the opcode itself.</summary>
    NoneInt,

    /// <summary>No operands, local index encoded in opcode.</summary>
    NoneLoc,

    /// <summary>No operands, argument index encoded in opcode.</summary>
    NoneArg,

    /// <summary>No operands, var ref index encoded in opcode.</summary>
    NoneVarRef,

    /// <summary>8-bit unsigned operand.</summary>
    U8,

    /// <summary>8-bit signed operand.</summary>
    I8,

    /// <summary>8-bit local variable index.</summary>
    Loc8,

    /// <summary>8-bit constant pool index.</summary>
    Const8,

    /// <summary>8-bit label offset.</summary>
    Label8,

    /// <summary>16-bit unsigned operand.</summary>
    U16,

    /// <summary>16-bit signed operand.</summary>
    I16,

    /// <summary>16-bit label offset.</summary>
    Label16,

    /// <summary>16-bit argument count (npop).</summary>
    NPop,

    /// <summary>Variable npop format.</summary>
    NPopX,

    /// <summary>16-bit npop with u16.</summary>
    NPopU16,

    /// <summary>16-bit local variable index.</summary>
    Loc,

    /// <summary>16-bit argument index.</summary>
    Arg,

    /// <summary>16-bit variable reference index.</summary>
    VarRef,

    /// <summary>32-bit unsigned operand.</summary>
    U32,

    /// <summary>32-bit signed operand.</summary>
    I32,

    /// <summary>32-bit constant pool index.</summary>
    Const,

    /// <summary>32-bit label offset.</summary>
    Label,

    /// <summary>32-bit atom index.</summary>
    Atom,

    /// <summary>Atom followed by 8-bit operand.</summary>
    AtomU8,

    /// <summary>Atom followed by 16-bit operand.</summary>
    AtomU16,

    /// <summary>Atom, label, and 8-bit operand.</summary>
    AtomLabelU8,

    /// <summary>Atom, label, and 16-bit operand.</summary>
    AtomLabelU16,

    /// <summary>Label followed by 16-bit operand.</summary>
    LabelU16,
}

/// <summary>
/// Metadata about an opcode including its size and stack effects.
/// </summary>
/// <remarks>
/// This mirrors the JSOpCode structure from QuickJS.
/// </remarks>
public readonly struct OpCodeInfo
{
    /// <summary>
    /// Gets the opcode this info describes.
    /// </summary>
    public OpCode OpCode { get; }

    /// <summary>
    /// Gets the name of the opcode for debugging/disassembly.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the total size in bytes (opcode + operands).
    /// </summary>
    public byte Size { get; }

    /// <summary>
    /// Gets the number of values popped from the stack.
    /// </summary>
    /// <remarks>
    /// For call opcodes, arguments are not counted in this value.
    /// </remarks>
    public byte Pop { get; }

    /// <summary>
    /// Gets the number of values pushed onto the stack.
    /// </summary>
    public byte Push { get; }

    /// <summary>
    /// Gets the operand format.
    /// </summary>
    public OpCodeFormat Format { get; }

    /// <summary>
    /// Gets whether this is a temporary opcode (removed during compilation).
    /// </summary>
    public bool IsTemporary { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpCodeInfo"/> struct.
    /// </summary>
    public OpCodeInfo(OpCode opCode, string name, byte size, byte pop, byte push, OpCodeFormat format, bool isTemporary = false)
    {
        OpCode = opCode;
        Name = name;
        Size = size;
        Pop = pop;
        Push = push;
        Format = format;
        IsTemporary = isTemporary;
    }

    /// <summary>
    /// Gets the net stack effect (push - pop).
    /// </summary>
    public int StackDelta => Push - Pop;

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"{Name} (size={Size}, pop={Pop}, push={Push})";
}
