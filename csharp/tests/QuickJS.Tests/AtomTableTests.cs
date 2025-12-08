// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Unit tests for <see cref="JSAtom"/> and <see cref="AtomTable"/>.
/// </summary>
public class AtomTableTests
{
    #region JSAtom Tests

    [Fact]
    public void JSAtom_Empty_HasValueZero()
    {
        var empty = JSAtom.Empty;
        Assert.Equal(0u, empty.Value);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void JSAtom_Default_IsEmpty()
    {
        JSAtom atom = default;
        Assert.True(atom.IsEmpty);
    }

    [Fact]
    public void JSAtom_Equality_SameValue_ReturnsTrue()
    {
        var a = new JSAtom(42);
        var b = new JSAtom(42);
        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a != b);
    }

    [Fact]
    public void JSAtom_Equality_DifferentValue_ReturnsFalse()
    {
        var a = new JSAtom(42);
        var b = new JSAtom(43);
        Assert.False(a == b);
        Assert.False(a.Equals(b));
        Assert.True(a != b);
    }

    [Fact]
    public void JSAtom_GetHashCode_SameValue_ReturnsSameHash()
    {
        var a = new JSAtom(42);
        var b = new JSAtom(42);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void JSAtom_ToString_ReturnsAtomValue()
    {
        var atom = new JSAtom(42);
        Assert.Equal("Atom(42)", atom.ToString());
    }

    #endregion

    #region AtomTable Construction Tests

    [Fact]
    public void AtomTable_Constructor_InitializesBuiltInAtoms()
    {
        var table = new AtomTable();

        // Should have many built-in atoms
        Assert.True(table.Count > 50);
    }

    [Fact]
    public void AtomTable_BuiltInAtoms_AreNotEmpty()
    {
        var table = new AtomTable();

        Assert.False(table.Null.IsEmpty);
        Assert.False(table.True.IsEmpty);
        Assert.False(table.False.IsEmpty);
        Assert.False(table.Undefined.IsEmpty);
        Assert.False(table.Length.IsEmpty);
        Assert.False(table.Prototype.IsEmpty);
        Assert.False(table.Constructor.IsEmpty);
    }

    [Fact]
    public void AtomTable_BuiltInAtoms_HaveCorrectStrings()
    {
        var table = new AtomTable();

        Assert.Equal("null", table.GetString(table.Null));
        Assert.Equal("true", table.GetString(table.True));
        Assert.Equal("false", table.GetString(table.False));
        Assert.Equal("undefined", table.GetString(table.Undefined));
        Assert.Equal("length", table.GetString(table.Length));
        Assert.Equal("prototype", table.GetString(table.Prototype));
        Assert.Equal("constructor", table.GetString(table.Constructor));
        Assert.Equal("toString", table.GetString(table.ToString_));
        Assert.Equal("valueOf", table.GetString(table.ValueOf));
        Assert.Equal("__proto__", table.GetString(table.Proto));
    }

    #endregion

    #region GetOrCreateAtom Tests

    [Fact]
    public void GetOrCreateAtom_NewString_CreatesNewAtom()
    {
        var table = new AtomTable();
        int initialCount = table.Count;

        var atom = table.GetOrCreateAtom("myCustomProperty");

        Assert.False(atom.IsEmpty);
        Assert.Equal(initialCount + 1, table.Count);
    }

    [Fact]
    public void GetOrCreateAtom_ExistingString_ReturnsSameAtom()
    {
        var table = new AtomTable();

        var atom1 = table.GetOrCreateAtom("testString");
        var atom2 = table.GetOrCreateAtom("testString");

        Assert.Equal(atom1, atom2);
    }

    [Fact]
    public void GetOrCreateAtom_BuiltInString_ReturnsBuiltInAtom()
    {
        var table = new AtomTable();

        var atom = table.GetOrCreateAtom("length");

        Assert.Equal(table.Length, atom);
    }

    [Fact]
    public void GetOrCreateAtom_Null_ThrowsArgumentNullException()
    {
        var table = new AtomTable();

        Assert.Throws<ArgumentNullException>(() => table.GetOrCreateAtom(null!));
    }

    [Fact]
    public void GetOrCreateAtom_DifferentStrings_ReturnDifferentAtoms()
    {
        var table = new AtomTable();

        var atom1 = table.GetOrCreateAtom("foo");
        var atom2 = table.GetOrCreateAtom("bar");

        Assert.NotEqual(atom1, atom2);
    }

    [Fact]
    public void GetOrCreateAtom_EmptyString_ReturnsValidAtom()
    {
        var table = new AtomTable();

        var atom = table.GetOrCreateAtom("");

        Assert.False(atom.IsEmpty); // Empty string has a valid non-zero atom
        Assert.Equal("", table.GetString(atom));
    }

    #endregion

    #region TryGetAtom Tests

    [Fact]
    public void TryGetAtom_ExistingString_ReturnsTrue()
    {
        var table = new AtomTable();
        table.GetOrCreateAtom("test");

        bool found = table.TryGetAtom("test", out JSAtom atom);

        Assert.True(found);
        Assert.False(atom.IsEmpty);
    }

    [Fact]
    public void TryGetAtom_NonExistingString_ReturnsFalse()
    {
        var table = new AtomTable();

        bool found = table.TryGetAtom("nonExistingProperty12345", out JSAtom atom);

        Assert.False(found);
        Assert.True(atom.IsEmpty);
    }

    [Fact]
    public void TryGetAtom_Null_ReturnsFalse()
    {
        var table = new AtomTable();

        bool found = table.TryGetAtom(null!, out JSAtom atom);

        Assert.False(found);
        Assert.True(atom.IsEmpty);
    }

    [Fact]
    public void TryGetAtom_BuiltIn_ReturnsTrue()
    {
        var table = new AtomTable();

        bool found = table.TryGetAtom("prototype", out JSAtom atom);

        Assert.True(found);
        Assert.Equal(table.Prototype, atom);
    }

    #endregion

    #region GetString / TryGetString Tests

    [Fact]
    public void GetString_ValidAtom_ReturnsString()
    {
        var table = new AtomTable();
        var atom = table.GetOrCreateAtom("hello");

        string str = table.GetString(atom);

        Assert.Equal("hello", str);
    }

    [Fact]
    public void GetString_EmptyAtom_ReturnsEmptyString()
    {
        var table = new AtomTable();

        // Atom 0 is reserved as empty
        string str = table.GetString(JSAtom.Empty);

        Assert.Equal("", str);
    }

    [Fact]
    public void GetString_InvalidAtom_ThrowsArgumentOutOfRangeException()
    {
        var table = new AtomTable();
        var invalidAtom = new JSAtom(99999);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetString(invalidAtom));
    }

    [Fact]
    public void TryGetString_ValidAtom_ReturnsTrue()
    {
        var table = new AtomTable();
        var atom = table.GetOrCreateAtom("world");

        bool found = table.TryGetString(atom, out string? str);

        Assert.True(found);
        Assert.Equal("world", str);
    }

    [Fact]
    public void TryGetString_InvalidAtom_ReturnsFalse()
    {
        var table = new AtomTable();
        var invalidAtom = new JSAtom(99999);

        bool found = table.TryGetString(invalidAtom, out string? str);

        Assert.False(found);
        Assert.Null(str);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async System.Threading.Tasks.Task AtomTable_ConcurrentAccess_IsThreadSafe()
    {
        var table = new AtomTable();
        var tasks = new System.Threading.Tasks.Task<JSAtom>[10];

        // Multiple threads trying to create the same atom
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() => table.GetOrCreateAtom("sharedString"));
        }

        var results = await System.Threading.Tasks.Task.WhenAll(tasks);

        // All should get the same atom
        var firstAtom = results[0];
        foreach (var atom in results)
        {
            Assert.Equal(firstAtom, atom);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void AtomTable_RoundTrip_StringToAtomToString()
    {
        var table = new AtomTable();
        string original = "myPropertyName";

        var atom = table.GetOrCreateAtom(original);
        string retrieved = table.GetString(atom);

        Assert.Equal(original, retrieved);
    }

    [Fact]
    public void AtomTable_Keywords_AreInterned()
    {
        var table = new AtomTable();

        // Keywords should already be interned
        Assert.True(table.TryGetAtom("if", out _));
        Assert.True(table.TryGetAtom("else", out _));
        Assert.True(table.TryGetAtom("for", out _));
        Assert.True(table.TryGetAtom("while", out _));
        Assert.True(table.TryGetAtom("function", out _));
        Assert.True(table.TryGetAtom("return", out _));
        Assert.True(table.TryGetAtom("var", out _));
        Assert.True(table.TryGetAtom("let", out _));
        Assert.True(table.TryGetAtom("const", out _));
        Assert.True(table.TryGetAtom("class", out _));
    }

    [Fact]
    public void AtomTable_CommonClassNames_AreInterned()
    {
        var table = new AtomTable();

        Assert.True(table.TryGetAtom("Object", out _));
        Assert.True(table.TryGetAtom("Array", out _));
        Assert.True(table.TryGetAtom("String", out _));
        Assert.True(table.TryGetAtom("Number", out _));
        Assert.True(table.TryGetAtom("Boolean", out _));
        Assert.True(table.TryGetAtom("Function", out _));
        Assert.True(table.TryGetAtom("Error", out _));
        Assert.True(table.TryGetAtom("Promise", out _));
    }

    #endregion
}
