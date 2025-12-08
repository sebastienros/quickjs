// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Manages the atom table for string interning.
/// </summary>
/// <remarks>
/// <para>
/// The atom table is a bidirectional mapping between strings and atom indices.
/// When a string is interned, it receives a unique atom ID that can be used
/// for fast comparison and as a property key.
/// </para>
/// <para>
/// Built-in atoms (keywords, common property names like "length", "prototype", etc.)
/// are pre-populated during construction. User strings are added dynamically.
/// </para>
/// </remarks>
public sealed class AtomTable
{
    // String -> Atom mapping for fast lookup
    private readonly Dictionary<string, JSAtom> _stringToAtom;

    // Atom -> String mapping for reverse lookup
    private readonly List<string> _atomToString;

    // Lock for thread safety (optional, but good practice)
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a new atom table with built-in atoms pre-populated.
    /// </summary>
    public AtomTable()
    {
        _stringToAtom = new Dictionary<string, JSAtom>(StringComparer.Ordinal);
        _atomToString = new List<string>();

        // Reserve index 0 as "empty/invalid"
        _atomToString.Add(string.Empty);

        // Pre-populate built-in atoms
        InitializeBuiltInAtoms();
    }

    /// <summary>
    /// Gets the total number of atoms in the table (including the empty atom).
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _atomToString.Count;
            }
        }
    }

    /// <summary>
    /// Gets or creates an atom for the specified string.
    /// </summary>
    /// <param name="str">The string to intern.</param>
    /// <returns>The atom representing the string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="str"/> is null.</exception>
    public JSAtom GetOrCreateAtom(string str)
    {
        if (str is null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        lock (_lock)
        {
            if (_stringToAtom.TryGetValue(str, out JSAtom existing))
            {
                return existing;
            }

            uint index = (uint)_atomToString.Count;
            var atom = new JSAtom(index);
            _atomToString.Add(str);
            _stringToAtom[str] = atom;
            return atom;
        }
    }

    /// <summary>
    /// Attempts to get an existing atom for the specified string.
    /// </summary>
    /// <param name="str">The string to look up.</param>
    /// <param name="atom">The atom if found.</param>
    /// <returns><c>true</c> if the string has an atom; otherwise, <c>false</c>.</returns>
    public bool TryGetAtom(string str, out JSAtom atom)
    {
        if (str is null)
        {
            atom = JSAtom.Empty;
            return false;
        }

        lock (_lock)
        {
            return _stringToAtom.TryGetValue(str, out atom);
        }
    }

    /// <summary>
    /// Gets the string represented by an atom.
    /// </summary>
    /// <param name="atom">The atom to look up.</param>
    /// <returns>The string represented by the atom.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the atom is invalid.</exception>
    public string GetString(JSAtom atom)
    {
        lock (_lock)
        {
            uint index = atom.Value;
            if (index >= (uint)_atomToString.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(atom), $"Invalid atom: {atom}");
            }

            return _atomToString[(int)index];
        }
    }

    /// <summary>
    /// Attempts to get the string represented by an atom.
    /// </summary>
    /// <param name="atom">The atom to look up.</param>
    /// <param name="str">The string if found.</param>
    /// <returns><c>true</c> if the atom is valid; otherwise, <c>false</c>.</returns>
    public bool TryGetString(JSAtom atom, out string? str)
    {
        lock (_lock)
        {
            uint index = atom.Value;
            if (index < (uint)_atomToString.Count)
            {
                str = _atomToString[(int)index];
                return true;
            }

            str = null;
            return false;
        }
    }

    #region Built-in Atoms

    // Built-in atom accessors (most commonly used atoms)
    // These match the order in quickjs-atom.h

    /// <summary>Gets the atom for "null".</summary>
    public JSAtom Null { get; private set; }

    /// <summary>Gets the atom for "false".</summary>
    public JSAtom False { get; private set; }

    /// <summary>Gets the atom for "true".</summary>
    public JSAtom True { get; private set; }

    /// <summary>Gets the atom for "if".</summary>
    public JSAtom If { get; private set; }

    /// <summary>Gets the atom for "else".</summary>
    public JSAtom Else { get; private set; }

    /// <summary>Gets the atom for "return".</summary>
    public JSAtom Return { get; private set; }

    /// <summary>Gets the atom for "var".</summary>
    public JSAtom Var { get; private set; }

    /// <summary>Gets the atom for "this".</summary>
    public JSAtom This { get; private set; }

    /// <summary>Gets the atom for "function".</summary>
    public JSAtom Function { get; private set; }

    /// <summary>Gets the atom for "let".</summary>
    public JSAtom Let { get; private set; }

    /// <summary>Gets the atom for "const".</summary>
    public JSAtom Const { get; private set; }

    /// <summary>Gets the atom for "class".</summary>
    public JSAtom Class { get; private set; }

    /// <summary>Gets the atom for "undefined".</summary>
    public JSAtom Undefined { get; private set; }

    /// <summary>Gets the atom for "length".</summary>
    public JSAtom Length { get; private set; }

    /// <summary>Gets the atom for "prototype".</summary>
    public JSAtom Prototype { get; private set; }

    /// <summary>Gets the atom for "constructor".</summary>
    public JSAtom Constructor { get; private set; }

    /// <summary>Gets the atom for "toString".</summary>
    public JSAtom ToString_ { get; private set; }

    /// <summary>Gets the atom for "valueOf".</summary>
    public JSAtom ValueOf { get; private set; }

    /// <summary>Gets the atom for "name".</summary>
    public JSAtom Name { get; private set; }

    /// <summary>Gets the atom for "message".</summary>
    public JSAtom Message { get; private set; }

    /// <summary>Gets the atom for "__proto__".</summary>
    public JSAtom Proto { get; private set; }

    private void InitializeBuiltInAtoms()
    {
        // Keywords (in order from quickjs-atom.h)
        Null = GetOrCreateAtom("null");
        False = GetOrCreateAtom("false");
        True = GetOrCreateAtom("true");
        If = GetOrCreateAtom("if");
        Else = GetOrCreateAtom("else");
        Return = GetOrCreateAtom("return");
        Var = GetOrCreateAtom("var");
        This = GetOrCreateAtom("this");
        GetOrCreateAtom("delete");
        GetOrCreateAtom("void");
        GetOrCreateAtom("typeof");
        GetOrCreateAtom("new");
        GetOrCreateAtom("in");
        GetOrCreateAtom("instanceof");
        GetOrCreateAtom("do");
        GetOrCreateAtom("while");
        GetOrCreateAtom("for");
        GetOrCreateAtom("break");
        GetOrCreateAtom("continue");
        GetOrCreateAtom("switch");
        GetOrCreateAtom("case");
        GetOrCreateAtom("default");
        GetOrCreateAtom("throw");
        GetOrCreateAtom("try");
        GetOrCreateAtom("catch");
        GetOrCreateAtom("finally");
        Function = GetOrCreateAtom("function");
        GetOrCreateAtom("debugger");
        GetOrCreateAtom("with");
        Class = GetOrCreateAtom("class");
        Const = GetOrCreateAtom("const");
        GetOrCreateAtom("enum");
        GetOrCreateAtom("export");
        GetOrCreateAtom("extends");
        GetOrCreateAtom("import");
        GetOrCreateAtom("super");
        GetOrCreateAtom("implements");
        GetOrCreateAtom("interface");
        Let = GetOrCreateAtom("let");
        GetOrCreateAtom("package");
        GetOrCreateAtom("private");
        GetOrCreateAtom("protected");
        GetOrCreateAtom("public");
        GetOrCreateAtom("static");
        GetOrCreateAtom("yield");
        GetOrCreateAtom("await");

        // Empty string
        GetOrCreateAtom("");

        // Common identifiers
        GetOrCreateAtom("keys");
        GetOrCreateAtom("size");
        Length = GetOrCreateAtom("length");
        GetOrCreateAtom("fileName");
        GetOrCreateAtom("lineNumber");
        GetOrCreateAtom("columnNumber");
        Message = GetOrCreateAtom("message");
        GetOrCreateAtom("cause");
        GetOrCreateAtom("errors");
        GetOrCreateAtom("stack");
        Name = GetOrCreateAtom("name");
        ToString_ = GetOrCreateAtom("toString");
        GetOrCreateAtom("toLocaleString");
        ValueOf = GetOrCreateAtom("valueOf");
        GetOrCreateAtom("eval");
        Prototype = GetOrCreateAtom("prototype");
        Constructor = GetOrCreateAtom("constructor");
        GetOrCreateAtom("configurable");
        GetOrCreateAtom("writable");
        GetOrCreateAtom("enumerable");
        GetOrCreateAtom("value");
        GetOrCreateAtom("get");
        GetOrCreateAtom("set");
        GetOrCreateAtom("of");
        Proto = GetOrCreateAtom("__proto__");
        Undefined = GetOrCreateAtom("undefined");
        GetOrCreateAtom("number");
        GetOrCreateAtom("boolean");
        GetOrCreateAtom("string");
        GetOrCreateAtom("object");
        GetOrCreateAtom("symbol");
        GetOrCreateAtom("arguments");
        GetOrCreateAtom("callee");
        GetOrCreateAtom("caller");

        // Common class names
        GetOrCreateAtom("Object");
        GetOrCreateAtom("Array");
        GetOrCreateAtom("Error");
        GetOrCreateAtom("Number");
        GetOrCreateAtom("String");
        GetOrCreateAtom("Boolean");
        GetOrCreateAtom("Symbol");
        GetOrCreateAtom("Math");
        GetOrCreateAtom("JSON");
        GetOrCreateAtom("Date");
        GetOrCreateAtom("Function");
        GetOrCreateAtom("RegExp");
        GetOrCreateAtom("Map");
        GetOrCreateAtom("Set");
        GetOrCreateAtom("WeakMap");
        GetOrCreateAtom("WeakSet");
        GetOrCreateAtom("Promise");
        GetOrCreateAtom("Proxy");
        GetOrCreateAtom("ArrayBuffer");
        GetOrCreateAtom("DataView");
        GetOrCreateAtom("BigInt");

        // Error types
        GetOrCreateAtom("EvalError");
        GetOrCreateAtom("RangeError");
        GetOrCreateAtom("ReferenceError");
        GetOrCreateAtom("SyntaxError");
        GetOrCreateAtom("TypeError");
        GetOrCreateAtom("URIError");
        GetOrCreateAtom("AggregateError");

        // Well-known symbols (as strings for now)
        GetOrCreateAtom("Symbol.iterator");
        GetOrCreateAtom("Symbol.toStringTag");
        GetOrCreateAtom("Symbol.toPrimitive");
        GetOrCreateAtom("Symbol.hasInstance");
        GetOrCreateAtom("Symbol.species");
    }

    #endregion
}
