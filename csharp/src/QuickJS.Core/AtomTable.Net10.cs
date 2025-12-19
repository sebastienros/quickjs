// Licensed under the MIT License.

#if NET10_0_OR_GREATER

using System;
using System.Collections.Generic;

namespace QuickJS;

public sealed partial class AtomTable
{
    // AlternateLookup for span-based lookups without allocating strings
    private Dictionary<string, JSAtom>.AlternateLookup<ReadOnlySpan<char>> _spanLookup;

    /// <summary>
    /// Initializes the span-based lookup. Must be called after the dictionary is created.
    /// </summary>
    partial void InitializeSpanLookup()
    {
        _spanLookup = _stringToAtom.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Gets or creates an atom for the specified character span.
    /// </summary>
    /// <param name="span">The character span to intern.</param>
    /// <returns>The atom representing the string.</returns>
    public JSAtom GetOrCreateAtom(ReadOnlySpan<char> span)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_spanLookup.TryGetValue(span, out JSAtom existing))
            {
                return existing;
            }

            _lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_spanLookup.TryGetValue(span, out existing))
                {
                    return existing;
                }

                // Only allocate a string when we need to add a new entry
                string str = new string(span);
                uint index = (uint)_atomToString.Count;
                var atom = new JSAtom(index);
                _atomToString.Add(str);
                _stringToAtom[str] = atom;

                return atom;
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
    /// Attempts to get an existing atom for the specified character span.
    /// </summary>
    /// <param name="span">The character span to look up.</param>
    /// <param name="atom">The atom if found.</param>
    /// <returns><c>true</c> if the string has an atom; otherwise, <c>false</c>.</returns>
    public bool TryGetAtom(ReadOnlySpan<char> span, out JSAtom atom)
    {
        return _spanLookup.TryGetValue(span, out atom);
    }

    /// <summary>
    /// Gets or creates the interned string for the specified character span.
    /// </summary>
    /// <param name="span">The character span to look up or intern.</param>
    /// <returns>The interned string from the atom table.</returns>
    /// <remarks>
    /// This method is designed for use by the Lexer to avoid string allocations.
    /// If the span already exists in the atom table, the existing string is returned.
    /// Otherwise, a new string is allocated, added to the table, and returned.
    /// </remarks>
    public string GetOrCreateString(ReadOnlySpan<char> span)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_spanLookup.TryGetValue(span, out JSAtom existing))
            {
                return _atomToString[(int)existing.Value];
            }

            _lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_spanLookup.TryGetValue(span, out existing))
                {
                    return _atomToString[(int)existing.Value];
                }

                // Allocate and intern the string
                string str = new string(span);
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
    public bool TryGetString(ReadOnlySpan<char> span, out string? str)
    {
        _lock.EnterReadLock();
        try
        {
            if (_spanLookup.TryGetValue(span, out JSAtom atom))
            {
                str = _atomToString[(int)atom.Value];
                return true;
            }

            str = null;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

#endif
