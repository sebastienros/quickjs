// Licensed under the MIT License.

#if !NET10_0_OR_GREATER

using System;

namespace QuickJS;

public sealed partial class AtomTable
{
    /// <summary>
    /// Gets or creates an atom for the specified character span.
    /// </summary>
    /// <param name="span">The character span to intern.</param>
    /// <returns>The atom representing the string.</returns>
    /// <remarks>
    /// On frameworks prior to .NET 10, this allocates a string for the lookup.
    /// </remarks>
    public JSAtom GetOrCreateAtom(ReadOnlySpan<char> span)
    {
        // Fallback: allocate string for lookup
        return GetOrCreateAtom(span.ToString());
    }

    /// <summary>
    /// Attempts to get an existing atom for the specified character span.
    /// </summary>
    /// <param name="span">The character span to look up.</param>
    /// <param name="atom">The atom if found.</param>
    /// <returns><c>true</c> if the string has an atom; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// On frameworks prior to .NET 10, this allocates a string for the lookup.
    /// </remarks>
    public bool TryGetAtom(ReadOnlySpan<char> span, out JSAtom atom)
    {
        // Fallback: allocate string for lookup
        return TryGetAtom(span.ToString(), out atom);
    }

    /// <summary>
    /// Gets or creates the interned string for the specified character span.
    /// </summary>
    /// <param name="span">The character span to look up or intern.</param>
    /// <returns>The interned string from the atom table.</returns>
    /// <remarks>
    /// On frameworks prior to .NET 10, this allocates a string.
    /// The Lexer uses this to avoid repeated allocations for the same identifier.
    /// </remarks>
    public string GetOrCreateString(ReadOnlySpan<char> span)
    {
        // On legacy frameworks, we have to allocate a string for lookup
        string str = span.ToString();
        
        _lock.EnterUpgradeableReadLock();
        try
        {
            // Check if we already have this string interned
            if (_stringToAtom.TryGetValue(str, out JSAtom existing))
            {
                // Return the existing interned string (same instance)
                return _atomToString[(int)existing.Value];
            }

            _lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_stringToAtom.TryGetValue(str, out existing))
                {
                    return _atomToString[(int)existing.Value];
                }

                // Intern the new string
                uint index = (uint)_atomToString.Count;
                var atom = new JSAtom(index);
                _atomToString.Add(str);
                _stringToAtom[str] = atom;

                return str;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Attempts to get the interned string for the specified character span.
    /// </summary>
    /// <param name="span">The character span to look up.</param>
    /// <param name="str">The interned string if found.</param>
    /// <returns><c>true</c> if the string exists in the atom table; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// On frameworks prior to .NET 10, this allocates a string for the lookup.
    /// </remarks>
    public bool TryGetString(ReadOnlySpan<char> span, out string? str)
    {
        string key = span.ToString();
        if (TryGetAtom(key, out JSAtom atom))
        {
            return TryGetString(atom, out str);
        }

        str = null;
        return false;
    }
}

#endif
