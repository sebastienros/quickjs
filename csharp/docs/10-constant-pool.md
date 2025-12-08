# Step 3.3: Constant Pool

This step implements the constant pool for storing values referenced by bytecode during execution.

## Overview

The constant pool stores values that cannot be encoded directly in bytecode:

- **String literals** (variable length)
- **Large numbers** (floats, BigInts)
- **Nested function bytecode** (closures)
- **Regular expressions**

During compilation, these values are added to the pool and their indices are embedded in the bytecode. At runtime, instructions like `OP_push_const` retrieve values by index.

## Key Concepts

### Index-Based Access

Values in the constant pool are referenced by 32-bit indices:

```csharp
var pool = new ConstantPool();

// During compilation: add constants
int strIdx = pool.AddString("hello");
int numIdx = pool.AddDouble(3.14159);

// Emit bytecode with indices
buffer.EmitOp(OpCode.PushConst);
buffer.EmitConst(strIdx);  // 32-bit index

// At runtime: retrieve by index
JSValue str = pool.Get(strIdx);
JSValue num = pool.Get(numIdx);
```

### No Automatic Deduplication

By default, the constant pool does not deduplicate values:

```csharp
int idx1 = pool.AddString("hello");
int idx2 = pool.AddString("hello");
// idx1 != idx2, pool.Count == 2
```

For optional deduplication, use `AddOrGet`:

```csharp
int idx1 = pool.AddOrGet(JSValue.FromString("hello"));
int idx2 = pool.AddOrGet(JSValue.FromString("hello"));
// idx1 == idx2, pool.Count == 1
```

## QuickJS Mapping

| QuickJS (C) | C# Implementation |
|-------------|-------------------|
| `JSValue *cpool` | `ConstantPool._constants` |
| `int cpool_count` | `ConstantPool.Count` |
| `int cpool_size` | Internal capacity |
| `cpool_add()` | `Add()` / `AddInt32()` / `AddDouble()` / `AddString()` |
| `cpool[idx]` | `Get(idx)` / `GetUnsafe(idx)` |

## API Reference

### Adding Constants

```csharp
// Add any JSValue
int Add(JSValue value);

// Type-specific helpers
int AddInt32(int value);
int AddDouble(double value);
int AddString(string value);

// With deduplication
int AddOrGet(JSValue value);
```

### Retrieving Constants

```csharp
// With bounds checking
JSValue Get(int index);

// Without bounds checking (performance)
JSValue GetUnsafe(int index);

// Safe retrieval
bool TryGet(int index, out JSValue value);
```

### Searching

```csharp
// Find existing value (O(n) search)
int IndexOf(JSValue value);
```

### Conversion

```csharp
// Get all constants
JSValue[] ToArray();

// Enumerate
foreach (var constant in pool) { ... }
```

## Bytecode Usage

The constant pool integrates with bytecode through index-based references:

```javascript
// JavaScript source
const message = "Hello, World!";
const pi = 3.14159;
```

Compilation:
```csharp
// Add constants to pool
int msgIdx = pool.AddString("Hello, World!");
int piIdx = pool.AddDouble(3.14159);

// Emit bytecode
buffer.EmitOp(OpCode.PushConst);
buffer.EmitConst(msgIdx);
buffer.EmitOp(OpCode.PutLocCheck);
buffer.EmitLoc(messageLocalIdx);

buffer.EmitOp(OpCode.PushConst);
buffer.EmitConst(piIdx);
buffer.EmitOp(OpCode.PutLocCheck);
buffer.EmitLoc(piLocalIdx);
```

Runtime bytecode (hex):
```
02          ; OP_push_const
00 00 00 00 ; constant index 0 (message string)
...
02          ; OP_push_const
01 00 00 00 ; constant index 1 (pi value)
```

## Performance Considerations

### Why No Automatic Deduplication?

1. **Compilation speed**: Searching for duplicates is O(n) per add
2. **Memory trade-off**: Small pools don't benefit much
3. **Semantic correctness**: Some values may intentionally differ (e.g., different NaN representations)

Use `AddOrGet` when deduplication is worth the cost (many duplicate strings).

### GetUnsafe for Hot Paths

At runtime, the interpreter may access the constant pool frequently. Use `GetUnsafe` when the index is known to be valid:

```csharp
// In interpreter loop (index known valid from bytecode validation)
JSValue value = pool.GetUnsafe(constIndex);
```

## Test Coverage

- **Construction**: Default and custom capacity
- **Add Methods**: Sequential indices, duplicate handling, type-specific helpers
- **Get Methods**: Valid/invalid indices, unsafe access, TryGet pattern
- **IndexOf/AddOrGet**: Searching, deduplication
- **Integration**: ByteCode + ConstantPool workflow

## Files Created

- `src/QuickJS.Core/ConstantPool.cs` - Main constant pool class
- `tests/QuickJS.Tests/ConstantPoolTests.cs` - 35 tests
- `docs/10-constant-pool.md` - This documentation

## Next Steps

Step 3.4 will implement **JSFunctionDef**, the function definition structure that brings together:
- ByteCodeBuffer (bytecode)
- ConstantPool (constants)
- LabelSlots (labels)
- Variable definitions (locals, args, closures)
- Debug info (source mapping)

This is the central compilation context used when parsing and compiling JavaScript functions.
