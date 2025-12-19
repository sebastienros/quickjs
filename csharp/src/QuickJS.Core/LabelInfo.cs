// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace QuickJS;

/// <summary>
/// Represents a relocation entry for a forward reference to a label.
/// </summary>
/// <remarks>
/// When emitting a jump to a label that hasn't been defined yet,
/// a relocation entry records where the offset needs to be patched.
/// </remarks>
internal sealed class RelocEntry
{
    /// <summary>
    /// Gets or sets the next relocation entry in the linked list.
    /// </summary>
    public RelocEntry? Next { get; set; }

    /// <summary>
    /// Gets the address (offset in bytecode) where the label reference is stored.
    /// </summary>
    public int Address { get; }

    /// <summary>
    /// Gets the size of the label reference (1, 2, or 4 bytes).
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Initializes a new relocation entry.
    /// </summary>
    /// <param name="address">The bytecode offset where the reference is stored.</param>
    /// <param name="size">The size of the reference in bytes.</param>
    public RelocEntry(int address, int size)
    {
        Address = address;
        Size = size;
    }
}

/// <summary>
/// Represents a label in bytecode, used for jumps and control flow.
/// </summary>
/// <remarks>
/// <para>
/// Labels go through multiple phases during compilation:
/// </para>
/// <list type="bullet">
/// <item>Phase 1: `MarkLabel()` records the bytecode offset in <see cref="Position"/>.</item>
/// <item>Phase 2: Reserved for future optimization passes (<see cref="Position2"/>).</item>
/// <item>Phase 3: Final address resolution/patching (<see cref="Address"/>).</item>
/// </list>
/// <para>
/// Based on LabelSlot from QuickJS.
/// </para>
/// </remarks>
public sealed class LabelInfo
{
    /// <summary>
    /// Gets or sets the reference count for this label.
    /// A label with zero references can be removed.
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// Gets or sets the phase 1 position (initial bytecode offset).
    /// -1 means not yet resolved.
    /// </summary>
    public int Position { get; set; } = -1;

    /// <summary>
    /// Gets or sets the phase 2 position (after optimization).
    /// -1 means not yet resolved.
    /// </summary>
    public int Position2 { get; set; } = -1;

    /// <summary>
    /// Gets or sets the phase 3 address (final bytecode offset).
    /// -1 means not yet resolved.
    /// </summary>
    public int Address { get; set; } = -1;

    /// <summary>
    /// Gets or sets the first relocation entry for forward references.
    /// </summary>
    internal RelocEntry? FirstReloc { get; set; }

    /// <summary>
    /// Gets whether this label has been marked (has a defined position).
    /// </summary>
    public bool IsMarked => Position >= 0;

    /// <summary>
    /// Gets whether this label has any forward references needing patching.
    /// </summary>
    public bool HasRelocations => FirstReloc != null;

    /// <summary>
    /// Adds a reference to this label.
    /// </summary>
    public void AddReference() => ReferenceCount++;

    /// <summary>
    /// Removes a reference from this label.
    /// </summary>
    /// <returns>The new reference count.</returns>
    public int RemoveReference()
    {
        ReferenceCount--;
        return ReferenceCount;
    }

    /// <summary>
    /// Adds a relocation entry for a forward reference.
    /// </summary>
    /// <param name="address">The bytecode offset where the reference is stored.</param>
    /// <param name="size">The size of the reference (1, 2, or 4 bytes).</param>
    internal void AddRelocation(int address, int size)
    {
        var entry = new RelocEntry(address, size);
        entry.Next = FirstReloc;
        FirstReloc = entry;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"Label(pos={Position}, refs={ReferenceCount}, marked={IsMarked})";
}
