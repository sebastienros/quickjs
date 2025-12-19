# Step 3.2: ByteCode Buffer

This step implements the bytecode buffer infrastructure for building executable bytecode during compilation. This mirrors QuickJS's `DynBuf` and related bytecode emission functions.

## Overview

When compiling JavaScript, QuickJS emits bytecode directly into a dynamic buffer. This step implements the C# equivalent:

- **ByteCodeBuffer**: A growable buffer for emitting opcodes and operands
- **LabelInfo**: Tracks jump targets and forward references
- **LineNumberTable**: Maps bytecode positions to source locations for debugging

## Key Concepts

### Dynamic Buffer

The `ByteCodeBuffer` class is a simple, growable byte array that provides type-safe methods for emitting different data sizes:

```csharp
var buffer = new ByteCodeBuffer();

// Emit an opcode
buffer.EmitOp(OpCode.PushI32);

// Emit a 32-bit constant
buffer.EmitI32(42);

// Emit another opcode
buffer.EmitOp(OpCode.Return);
```

### Little-Endian Encoding

All multi-byte values are stored in little-endian format (least significant byte first), matching QuickJS:

```csharp
buffer.EmitU32(0x12345678);
// Buffer contains: 0x78, 0x56, 0x34, 0x12
```

### Label Management

Labels handle control flow (jumps, branches, loops). The compilation process works in phases:

1. **Forward References**: When emitting a jump to an undefined label, a relocation is recorded
2. **Label Marking**: When the label position is known, it's marked
3. **Resolution**: Forward references are patched with actual offsets

```csharp
var buffer = new ByteCodeBuffer();

// Create a label for the end of an if-block
int endLabel = buffer.DefineLabel();

// Emit conditional jump (forward reference)
buffer.EmitJump(OpCode.IfFalse, endLabel);

// ... emit then-block bytecode ...

// Mark the label at this position
buffer.MarkLabel(endLabel);

// Later (during finalization), patch all forward references
buffer.ResolveLabels();
```

### Source Position Tracking

The `LineNumberTable` tracks where each bytecode instruction came from in the source:

```csharp
var lineTable = new LineNumberTable();

// Record that bytecode at PC 0 came from source position 10
lineTable.Add(0, 10);

// Record that bytecode at PC 15 came from source position 50
lineTable.Add(15, 50);

// Later, look up source position for a PC
int sourcePos = lineTable.GetSourcePosition(12); // Returns 10
```

## QuickJS Mapping

| QuickJS (C) | C# Implementation |
|-------------|-------------------|
| `DynBuf byte_code` | `ByteCodeBuffer` |
| `dbuf_putc()` | `EmitU8()` |
| `dbuf_put_u16()` | `EmitU16()` |
| `dbuf_put_u32()` | `EmitU32()` |
| `emit_op()` | `EmitOp()` |
| `emit_atom()` | `EmitAtom()` |
| `LabelSlot` | `LabelInfo` |
| `RelocEntry` | `RelocEntry` |
| `new_label()` | `DefineLabel()` |
| `emit_label()` | `MarkLabel()` |
| `emit_goto()` | `EmitGoto()` |
| `LineNumberSlot` | `LineNumberSlot` |
| `DynBuf pc2line` | `LineNumberTable` |

## Compilation Phases

In QuickJS, labels can exist as temporary bytecode markers (`OP_label`) during compilation.

In the C# port, label markers are **not emitted as executable bytecode**. Instead:

1. **Phase 1 (Position)**: `MarkLabel()` records the current bytecode offset in `LabelInfo.Position`.
2. **Phase 2 (Position2)**: Reserved for future optimization passes; currently unused.
3. **Phase 3 (Address)**: `ResolveLabels()` patches recorded relocations with final relative offsets.

The `LabelInfo` class tracks all three positions:

```csharp
public sealed class LabelInfo
{
    public int Position { get; set; } = -1;   // Phase 1
    public int Position2 { get; set; } = -1;  // Phase 2
    public int Address { get; set; } = -1;    // Phase 3
}
```

## Relocation Entries

When a jump references a label that hasn't been defined yet (forward jump), a relocation entry records where to patch:

```csharp
internal sealed class RelocEntry
{
    public RelocEntry? Next { get; set; }  // Linked list
    public int Address { get; }             // Where to patch
    public int Size { get; }                // 1, 2, or 4 bytes
}
```

Relocations form a linked list attached to each label, allowing multiple forward references to the same label.

## Buffer Operations

### Reading and Writing

The buffer supports random access for patching:

```csharp
// Read values
byte b = buffer.GetU8(offset);
ushort s = buffer.GetU16(offset);
uint i = buffer.GetU32(offset);

// Write values (patching)
buffer.PutU8(offset, newValue);
buffer.PutU16(offset, newValue);
buffer.PutU32(offset, newValue);
```

### Extracting Bytecode

```csharp
// Get a copy as an array
byte[] bytecode = buffer.ToArray();

// Get a span (no copy, .NET Standard 2.0+ / .NET Core)
ReadOnlySpan<byte> span = buffer.AsSpan();
```

## Example: Compiling a Simple Function

```javascript
function add(a, b) {
    return a + b;
}
```

Bytecode emission:

```csharp
var buffer = new ByteCodeBuffer();
var lineTable = new LineNumberTable();

// Track source position for 'return a + b'
lineTable.Add(buffer.Size, sourcePosition);

// Push argument 'a' (arg 0)
buffer.EmitOp(OpCode.GetArg);
buffer.EmitArg(0);

// Push argument 'b' (arg 1)
buffer.EmitOp(OpCode.GetArg);
buffer.EmitArg(1);

// Add
buffer.EmitOp(OpCode.Add);

// Return
buffer.EmitOp(OpCode.Return);
```

## Test Coverage

The implementation includes comprehensive tests for:

- **ByteCodeBuffer**: Emission, buffer growth, opcode tracking
- **Label Management**: Define, mark, jump, relocations
- **LineNumberTable**: Add entries, lookup, binary search
- **Integration**: Full bytecode sequences for functions and control flow

## Files Created

- `src/QuickJS.Core/ByteCodeBuffer.cs` - Main bytecode buffer class
- `src/QuickJS.Core/LabelInfo.cs` - Label and relocation types
- `src/QuickJS.Core/LineNumberTable.cs` - Source position tracking
- `tests/QuickJS.Tests/ByteCodeBufferTests.cs` - Buffer tests
- `tests/QuickJS.Tests/LabelAndLineNumberTests.cs` - Label and line number tests

## Next Steps

Step 3.3 will implement the **Constant Pool** for storing:
- String constants
- Number constants
- Function bytecode (nested functions)
- Regular expressions

This completes the bytecode infrastructure needed before implementing the actual compiler.
