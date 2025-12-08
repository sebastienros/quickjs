# Step 1.4: Atom Table

## Overview

This step implements the **Atom Table** - a core component of QuickJS that provides efficient string interning. Atoms are lightweight integer identifiers that represent unique strings, enabling O(1) string comparison and reduced memory usage.

## What are Atoms?

In JavaScript engines, property names, variable names, and identifiers appear repeatedly throughout code. Instead of storing and comparing full strings, QuickJS uses **atoms** - 32-bit unsigned integers that serve as indices into a string table.

### Benefits of Atoms

1. **Fast Comparison**: Comparing two atoms is a simple integer comparison (O(1)) instead of character-by-character string comparison (O(n))
2. **Memory Efficiency**: Each unique string is stored exactly once
3. **Hash Key Optimization**: Atoms can be used directly as hash keys for property lookup

### C Implementation Reference

From `quickjs.c`:
```c
typedef uint32_t JSAtom;

typedef struct JSAtomStruct {
    int ref_count;
    JSAtomKindEnum atom_type;
    uint32_t hash;
    int len;
    char str[0]; // variable length
} JSAtomStruct;
```

## C# Implementation

### JSAtom Struct

The `JSAtom` struct is a lightweight wrapper around a `uint` value:

```csharp
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct JSAtom : IEquatable<JSAtom>
{
    private readonly uint _value;

    internal JSAtom(uint value) => _value = value;

    internal uint Value => _value;

    public bool IsEmpty => _value == 0;

    public static JSAtom Empty => default;

    // Equality operators...
}
```

**Design Decisions:**
- `readonly struct`: Ensures immutability and enables compiler optimizations
- `internal` constructor: Atoms should only be created by `AtomTable`
- `internal Value` property: Exposes raw value for internal operations
- `DebuggerDisplay`: Shows "Atom(42)" format in debugger

### AtomTable Class

The `AtomTable` manages the bidirectional mapping between strings and atoms:

```csharp
public sealed class AtomTable
{
    private readonly Dictionary<string, uint> _stringToAtom;
    private readonly Dictionary<uint, string> _atomToString;
    private readonly ReaderWriterLockSlim _lock;
    private uint _nextAtomId;

    public AtomTable()
    {
        // Initialize with built-in atoms from quickjs-atom.h
        InitializeBuiltInAtoms();
    }
}
```

**Thread Safety**: Uses `ReaderWriterLockSlim` for concurrent read access with exclusive write access.

### Built-in Atoms

QuickJS pre-populates approximately 230 built-in atoms from `quickjs-atom.h`. These include:

| Category | Examples |
|----------|----------|
| Keywords | `if`, `else`, `for`, `while`, `function`, `class` |
| Built-in Properties | `length`, `prototype`, `constructor`, `__proto__` |
| Type Names | `Object`, `Array`, `String`, `Number`, `Boolean` |
| Common Methods | `toString`, `valueOf`, `hasOwnProperty`, `apply` |
| Symbol Properties | `Symbol.iterator`, `Symbol.toStringTag` |
| Error Types | `Error`, `TypeError`, `RangeError`, `SyntaxError` |

Example from `quickjs-atom.h`:
```c
DEF(null, "null")
DEF(false, "false")
DEF(true, "true")
DEF(if, "if")
DEF(else, "else")
// ... ~230 more
```

### API Surface

```csharp
public sealed class AtomTable
{
    // Get or create an atom for a string
    public JSAtom GetOrCreateAtom(string value);

    // Try to get an existing atom (doesn't create new ones)
    public bool TryGetAtom(string value, out JSAtom atom);

    // Get the string for an atom
    public string GetString(JSAtom atom);

    // Try to get string (returns false for invalid atoms)
    public bool TryGetString(JSAtom atom, out string? value);

    // Check if an atom represents a built-in value
    public bool IsBuiltIn(JSAtom atom);

    // Get count of atoms
    public int Count { get; }

    // Get count of built-in atoms
    public int BuiltInCount { get; }
}
```

## File Structure

```
src/QuickJS.Core/
├── JSAtom.cs           # Atom identifier struct
├── AtomTable.cs        # String interning table
└── Properties/
    └── AssemblyInfo.cs # InternalsVisibleTo for tests
```

## Usage Examples

```csharp
var table = new AtomTable();

// Get atoms for property names
var lengthAtom = table.GetOrCreateAtom("length");
var prototypeAtom = table.GetOrCreateAtom("prototype");

// Compare atoms (fast integer comparison)
if (propertyAtom == lengthAtom)
{
    // Handle length property
}

// Get string back from atom
string propertyName = table.GetString(propertyAtom);

// Check if already interned
if (table.TryGetAtom("constructor", out var ctorAtom))
{
    // "constructor" is a built-in atom
}
```

## Testing

The test suite covers:

1. **JSAtom struct behavior**: Empty atom, equality, hashing, ToString
2. **AtomTable construction**: Built-in atoms initialization
3. **String interning**: GetOrCreateAtom idempotency
4. **Lookup operations**: TryGetAtom, TryGetString
5. **Thread safety**: Concurrent access from multiple threads
6. **Edge cases**: Empty strings, Unicode, special characters

### Sample Tests

```csharp
[Fact]
public void AtomTable_GetOrCreateAtom_ReturnsSameAtomForSameString()
{
    var table = new AtomTable();
    var atom1 = table.GetOrCreateAtom("test");
    var atom2 = table.GetOrCreateAtom("test");
    Assert.Equal(atom1, atom2);
}

[Fact]
public async Task AtomTable_ConcurrentAccess_IsThreadSafe()
{
    var table = new AtomTable();
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => Task.Run(() => table.GetOrCreateAtom("shared")))
        .ToArray();

    var results = await Task.WhenAll(tasks);

    Assert.True(results.All(a => a == results[0]));
}
```

## Implementation Notes

### Why Not Just Use string.Intern()?

While .NET provides `string.Intern()` for string interning, we implement our own atom table because:

1. **Numeric IDs**: We need integer identifiers for fast comparison and use as hash keys
2. **Built-in Atoms**: We need specific atom values for built-in identifiers (matching QuickJS behavior)
3. **Scope Control**: The atom table is scoped to the runtime instance, not the entire AppDomain
4. **Reverse Lookup**: We need efficient atom-to-string lookup

### Memory Considerations

- Each unique string is stored once
- Atoms are never deallocated in the current implementation (matches QuickJS behavior for simplicity)
- For long-running applications with many dynamic property names, consider implementing reference counting

## Next Steps

With the atom table in place, we can now implement:

1. **Step 1.5**: Exception hierarchy for JavaScript errors
2. Future: Property access using atoms for fast lookup
3. Future: Object property maps using atom keys
