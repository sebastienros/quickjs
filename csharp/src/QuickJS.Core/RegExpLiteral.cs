// Licensed under the MIT License.

namespace QuickJS;

/// <summary>
/// Represents a parsed regular expression literal.
/// </summary>
public readonly struct RegExpLiteral
{
    /// <summary>
    /// Gets the regular expression pattern.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets the regular expression flags.
    /// </summary>
    public string Flags { get; }

    /// <summary>
    /// Creates a regular expression literal payload.
    /// </summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="flags">The flags text.</param>
    public RegExpLiteral(string pattern, string flags)
    {
        Pattern = pattern;
        Flags = flags;
    }
}
