// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Represents a non-allocating slice of a string.
/// </summary>
/// <remarks>
/// This is used by the tokenizer to reference segments of the source code without creating new strings.
/// Converting a slice to a string allocates.
/// </remarks>
public readonly struct SourceSlice
{
    /// <summary>
    /// An empty slice.
    /// </summary>
    public static readonly SourceSlice Empty = new SourceSlice(string.Empty);

    /// <summary>
    /// The source string this slice references.
    /// </summary>
    public readonly string Source;

    /// <summary>
    /// The start index within <see cref="Source"/>.
    /// </summary>
    public readonly int Start;

    /// <summary>
    /// The length of the slice.
    /// </summary>
    public readonly int Length;

    /// <summary>
    /// Creates a slice that covers the entire <paramref name="source"/>.
    /// </summary>
    public SourceSlice(string source)
    {
        Source = source ?? string.Empty;
        Start = 0;
        Length = Source.Length;
    }

    /// <summary>
    /// Implicitly converts a <see cref="string"/> to a <see cref="SourceSlice"/> covering the entire string.
    /// </summary>
    public static implicit operator SourceSlice(string source)
    {
        return new SourceSlice(source);
    }

    /// <summary>
    /// Creates a slice over <paramref name="source"/>.
    /// </summary>
    public SourceSlice(string source, int start, int length)
    {
        Source = source ?? string.Empty;
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets whether this slice is empty.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Gets the slice as a non-allocating span.
    /// </summary>
    public ReadOnlySpan<char> Span => Source.AsSpan(Start, Length);

    /// <summary>
    /// Materializes the slice into a new string.
    /// </summary>
    public override string ToString()
    {
        if (Length == 0)
            return string.Empty;

        return Source.Substring(Start, Length);
    }
}
