// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Represents a location in JavaScript source code.
/// </summary>
/// <remarks>
/// Used for error messages and debugging to identify where in the source
/// code an error occurred or where a function was defined.
/// </remarks>
public readonly struct SourceLocation : IEquatable<SourceLocation>
{
    /// <summary>
    /// Gets the source file name or identifier.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the 1-based line number in the source file.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number in the source line.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Creates a new source location.
    /// </summary>
    /// <param name="fileName">The source file name.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    public SourceLocation(string fileName, int line, int column)
    {
        FileName = fileName ?? string.Empty;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets a value indicating whether this location is empty/unknown.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(FileName) && Line == 0 && Column == 0;

    /// <summary>
    /// Gets an empty/unknown source location.
    /// </summary>
    public static SourceLocation Empty => default;

    /// <inheritdoc />
    public bool Equals(SourceLocation other)
        => FileName == other.FileName && Line == other.Line && Column == other.Column;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is SourceLocation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
#if NET8_0_OR_GREATER
        return HashCode.Combine(FileName, Line, Column);
#else
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (FileName?.GetHashCode() ?? 0);
            hash = hash * 31 + Line;
            hash = hash * 31 + Column;
            return hash;
        }
#endif
    }

    /// <summary>
    /// Determines whether two source locations are equal.
    /// </summary>
    public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

    /// <summary>
    /// Determines whether two source locations are not equal.
    /// </summary>
    public static bool operator !=(SourceLocation left, SourceLocation right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsEmpty)
            return "<unknown>";

        if (Column > 0)
            return $"{FileName}:{Line}:{Column}";

        if (Line > 0)
            return $"{FileName}:{Line}";

        return FileName;
    }
}
