// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace QuickJS;

/// <summary>
/// Represents an interned string identifier (atom) in the JavaScript engine.
/// Atoms are used for property names, variable names, and other identifiers
/// to enable fast comparison and reduce memory usage.
/// </summary>
/// <remarks>
/// <para>
/// In QuickJS, atoms are 32-bit unsigned integers that serve as indices into
/// an atom table. This allows:
/// - O(1) string comparison (just compare the atom values)
/// - Reduced memory usage (each unique string is stored only once)
/// - Fast property lookups (atoms can be used as hash keys)
/// </para>
/// <para>
/// Atoms are created via the <see cref="AtomTable"/> class, which manages
/// the string-to-atom mapping. Built-in atoms (keywords, common property names)
/// are pre-populated for efficiency.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct JSAtom : IEquatable<JSAtom>
{
    /// <summary>
    /// The internal atom value (index into the atom table).
    /// </summary>
    private readonly uint _value;

    /// <summary>
    /// Creates a new atom with the specified value.
    /// </summary>
    /// <param name="value">The atom index.</param>
    internal JSAtom(uint value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the raw atom value.
    /// </summary>
    internal uint Value => _value;

    /// <summary>
    /// Gets a value indicating whether this atom is empty (invalid).
    /// </summary>
    public bool IsEmpty => _value == 0;

    /// <summary>
    /// The empty/invalid atom (value 0).
    /// </summary>
    public static JSAtom Empty => default;

    #region Equality

    /// <inheritdoc />
    public bool Equals(JSAtom other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JSAtom other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (int)_value;

    /// <summary>
    /// Determines whether two atoms are equal.
    /// </summary>
    public static bool operator ==(JSAtom left, JSAtom right) => left._value == right._value;

    /// <summary>
    /// Determines whether two atoms are not equal.
    /// </summary>
    public static bool operator !=(JSAtom left, JSAtom right) => left._value != right._value;

    #endregion

    /// <inheritdoc />
    public override string ToString() => $"Atom({_value})";

    private string DebuggerDisplay => $"Atom({_value})";
}
