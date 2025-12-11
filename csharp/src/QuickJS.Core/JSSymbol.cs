// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace QuickJS
{
    /// <summary>
    /// Represents a JavaScript Symbol primitive.
    /// Symbols are unique and immutable primitive values used as object property keys.
    /// </summary>
    public sealed class JSSymbol
    {
        private static int _nextId = 0;
        
        // Global symbol registry for Symbol.for() and Symbol.keyFor()
        private static readonly ConcurrentDictionary<string, JSSymbol> _globalRegistry = new();

        /// <summary>
        /// A unique identifier for this symbol (internal use).
        /// </summary>
        private readonly int _id;

        /// <summary>
        /// Gets the description of this symbol, if any.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets whether this symbol is in the global registry.
        /// </summary>
        public bool IsGlobal { get; }

        /// <summary>
        /// Creates a new unique symbol with an optional description.
        /// </summary>
        /// <param name="description">Optional description for debugging purposes.</param>
        public JSSymbol(string? description = null)
        {
            _id = Interlocked.Increment(ref _nextId);
            Description = description;
            IsGlobal = false;
        }

        /// <summary>
        /// Private constructor for global symbols.
        /// </summary>
        private JSSymbol(string description, bool isGlobal)
        {
            _id = Interlocked.Increment(ref _nextId);
            Description = description;
            IsGlobal = isGlobal;
        }

        /// <summary>
        /// Returns a symbol from the global symbol registry with the given key.
        /// If a symbol with that key doesn't exist, a new one is created.
        /// </summary>
        /// <param name="key">The key for the symbol in the global registry.</param>
        /// <returns>The symbol associated with the key.</returns>
        public static JSSymbol For(string key)
        {
            return _globalRegistry.GetOrAdd(key, k => new JSSymbol(k, isGlobal: true));
        }

        /// <summary>
        /// Returns the key for a symbol from the global registry, or null if not found.
        /// </summary>
        /// <param name="symbol">The symbol to look up.</param>
        /// <returns>The key, or null if the symbol is not in the global registry.</returns>
        public static string? KeyFor(JSSymbol symbol)
        {
            if (!symbol.IsGlobal) return null;
            return symbol.Description;
        }

        /// <summary>
        /// Returns a string representation of the symbol.
        /// </summary>
        public override string ToString()
        {
            return Description != null ? $"Symbol({Description})" : "Symbol()";
        }

        /// <summary>
        /// Determines whether this symbol equals another object.
        /// Symbols are only equal to themselves.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj);
        }

        /// <summary>
        /// Returns the hash code for this symbol.
        /// </summary>
        public override int GetHashCode()
        {
            return _id;
        }

        // ========================================================================
        // Well-Known Symbols
        // ========================================================================

        private static JSSymbol? _iterator;
        private static JSSymbol? _toStringTag;
        private static JSSymbol? _toPrimitive;
        private static JSSymbol? _hasInstance;
        private static JSSymbol? _isConcatSpreadable;
        private static JSSymbol? _species;
        private static JSSymbol? _match;
        private static JSSymbol? _matchAll;
        private static JSSymbol? _replace;
        private static JSSymbol? _search;
        private static JSSymbol? _split;
        private static JSSymbol? _unscopables;
        private static JSSymbol? _asyncIterator;

        /// <summary>
        /// Symbol.iterator - A method returning the default iterator for an object.
        /// </summary>
        public static JSSymbol Iterator => _iterator ??= new JSSymbol("Symbol.iterator");

        /// <summary>
        /// Symbol.asyncIterator - A method returning the default async iterator for an object.
        /// </summary>
        public static JSSymbol AsyncIterator => _asyncIterator ??= new JSSymbol("Symbol.asyncIterator");

        /// <summary>
        /// Symbol.toStringTag - A string value used for the default description of an object.
        /// </summary>
        public static JSSymbol ToStringTag => _toStringTag ??= new JSSymbol("Symbol.toStringTag");

        /// <summary>
        /// Symbol.toPrimitive - A method to convert an object to a primitive value.
        /// </summary>
        public static JSSymbol ToPrimitive => _toPrimitive ??= new JSSymbol("Symbol.toPrimitive");

        /// <summary>
        /// Symbol.hasInstance - A method for instanceof to use.
        /// </summary>
        public static JSSymbol HasInstance => _hasInstance ??= new JSSymbol("Symbol.hasInstance");

        /// <summary>
        /// Symbol.isConcatSpreadable - A Boolean for whether an object should be flattened to its array elements by Array.prototype.concat.
        /// </summary>
        public static JSSymbol IsConcatSpreadable => _isConcatSpreadable ??= new JSSymbol("Symbol.isConcatSpreadable");

        /// <summary>
        /// Symbol.species - A function-valued property that is the constructor function used to create derived objects.
        /// </summary>
        public static JSSymbol Species => _species ??= new JSSymbol("Symbol.species");

        /// <summary>
        /// Symbol.match - A regular expression method that matches a string against a regular expression.
        /// </summary>
        public static JSSymbol Match => _match ??= new JSSymbol("Symbol.match");

        /// <summary>
        /// Symbol.matchAll - A regular expression method that returns an iterator of matches.
        /// </summary>
        public static JSSymbol MatchAll => _matchAll ??= new JSSymbol("Symbol.matchAll");

        /// <summary>
        /// Symbol.replace - A regular expression method that replaces matched substrings.
        /// </summary>
        public static JSSymbol Replace => _replace ??= new JSSymbol("Symbol.replace");

        /// <summary>
        /// Symbol.search - A regular expression method that returns the index of a match.
        /// </summary>
        public static JSSymbol Search => _search ??= new JSSymbol("Symbol.search");

        /// <summary>
        /// Symbol.split - A regular expression method that splits a string at matched indices.
        /// </summary>
        public static JSSymbol Split => _split ??= new JSSymbol("Symbol.split");

        /// <summary>
        /// Symbol.unscopables - Properties that should be excluded from 'with' bindings.
        /// </summary>
        public static JSSymbol Unscopables => _unscopables ??= new JSSymbol("Symbol.unscopables");
    }
}
