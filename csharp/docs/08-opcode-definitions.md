# Step 3.1: OpCode Definitions

This step introduces the bytecode opcodes that form the instruction set of the QuickJS virtual machine. Unlike traditional interpreters that build an Abstract Syntax Tree (AST) and then interpret it, QuickJS uses a **one-pass bytecode compiler** that directly emits bytecode while parsing. This approach is more efficient and is how production JavaScript engines typically work.

## Understanding QuickJS's Approach

QuickJS compiles JavaScript source code directly into bytecode without building an intermediate AST. The bytecode is then executed by a stack-based virtual machine. This design:

1. **Reduces memory usage** - No need to store an AST in memory
2. **Improves parsing speed** - Single pass through the source code
3. **Enables optimization** - Bytecode can be optimized before execution
4. **Supports serialization** - Compiled bytecode can be saved and loaded

## OpCode Enum

The `OpCode` enum defines all bytecode instructions, based on `quickjs-opcode.h`:

```csharp
public enum OpCode : ushort
{
    // Invalid opcode (never emitted)
    Invalid = 0,

    // Push values onto the stack
    PushI32,        // Push a 32-bit signed integer
    PushConst,      // Push a constant from the constant pool
    Undefined,      // Push undefined
    Null,           // Push null
    PushTrue,       // Push true
    PushFalse,      // Push false
    
    // Stack manipulation
    Drop,           // Pop top of stack: a -> (empty)
    Dup,            // Duplicate top: a -> a a
    Swap,           // Swap top two: a b -> b a
    
    // Arithmetic operations
    Add,            // a + b
    Sub,            // a - b
    Mul,            // a * b
    Div,            // a / b
    
    // Control flow
    IfFalse,        // Jump if top of stack is false
    IfTrue,         // Jump if top of stack is true
    Goto,           // Unconditional jump
    Return,         // Return from function
    
    // ... 200+ more opcodes
}
```

### Opcode Categories

| Category | Description | Examples |
|----------|-------------|----------|
| Push | Push values onto stack | `PushI32`, `Undefined`, `Null`, `PushTrue` |
| Stack | Manipulate stack | `Drop`, `Dup`, `Swap`, `Rot3L` |
| Arithmetic | Math operations | `Add`, `Sub`, `Mul`, `Div`, `Pow` |
| Comparison | Compare values | `Lt`, `Lte`, `Gt`, `Gte`, `Eq`, `StrictEq` |
| Bitwise | Bit operations | `And`, `Or`, `Xor`, `Shl`, `Shr` |
| Control | Flow control | `IfFalse`, `Goto`, `Return`, `Throw` |
| Variables | Variable access | `GetLoc`, `PutLoc`, `GetVar`, `PutVar` |
| Properties | Object properties | `GetField`, `PutField`, `GetArrayEl` |
| Calls | Function calls | `Call`, `CallMethod`, `CallConstructor` |
| Short | Optimized common cases | `Push0`-`Push7`, `GetLoc0`-`GetLoc3` |
| Temporary | Used during compilation | `Label`, `EnterScope`, `ScopeGetVar` |

## OpCodeInfo Struct

Each opcode has associated metadata:

```csharp
public readonly struct OpCodeInfo
{
    public OpCode OpCode { get; }      // The opcode
    public string Name { get; }        // Name for debugging
    public byte Size { get; }          // Total bytes (opcode + operands)
    public byte Pop { get; }           // Values popped from stack
    public byte Push { get; }          // Values pushed onto stack
    public OpCodeFormat Format { get; } // Operand format
    public bool IsTemporary { get; }   // Removed during compilation?
    
    public int StackDelta => Push - Pop;
}
```

### Stack Effects

The VM maintains a stack. Each opcode has defined stack effects:

```
┌─────────────┬────────────────────────────────────┐
│ Opcode      │ Stack Effect                       │
├─────────────┼────────────────────────────────────┤
│ Push0       │ ( -- 0 )                           │
│ Add         │ ( a b -- result )                  │
│ Dup         │ ( a -- a a )                       │
│ Drop        │ ( a -- )                           │
│ Swap        │ ( a b -- b a )                     │
│ GetField    │ ( obj -- value )                   │
│ PutField    │ ( obj value -- )                   │
│ Call        │ ( func args... -- result )         │
└─────────────┴────────────────────────────────────┘
```

## OpCodeFormat Enum

Defines how operands are encoded:

```csharp
public enum OpCodeFormat : byte
{
    None,       // No operands (1 byte total)
    I8,         // 8-bit signed integer
    I16,        // 16-bit signed integer
    I32,        // 32-bit signed integer
    Const,      // 32-bit constant pool index
    Atom,       // 32-bit atom (interned string) index
    Label,      // 32-bit jump target
    Loc,        // 16-bit local variable index
    Arg,        // 16-bit argument index
    // ... more formats
}
```

## OpCodes Static Class

Provides lookup and metadata for all opcodes:

```csharp
public static class OpCodes
{
    // Get metadata by opcode
    public static OpCodeInfo GetInfo(OpCode opCode);
    
    // Get metadata by name (case-insensitive)
    public static bool TryGetByName(string name, out OpCodeInfo info);
    
    // Get size in bytes
    public static int GetSize(OpCode opCode);
    
    // Get stack delta (push - pop)
    public static int GetStackDelta(OpCode opCode);
    
    // All opcode infos
    public static IEnumerable<OpCodeInfo> All { get; }
}
```

## Example: Simple Expression Compilation

For the expression `1 + 2`:

```
Bytecode:
  00: push_1        ; Push 1 onto stack
  01: push_2        ; Push 2 onto stack
  02: add           ; Pop 2 values, push sum

Stack trace:
  []              ; Empty stack
  [1]             ; After push_1
  [1, 2]          ; After push_2  
  [3]             ; After add
```

## Temporary vs Final Opcodes

QuickJS uses temporary opcodes during compilation that are resolved in later phases:

| Phase | Description |
|-------|-------------|
| Phase 1 | Parse and emit temporary opcodes (`ScopeGetVar`, `Label`, etc.) |
| Phase 2 | Resolve variable scopes → convert to `GetLoc`, `GetVar`, etc. |
| Phase 3 | Resolve labels → convert to absolute jump offsets |
| Final | Only final opcodes remain in the bytecode |

### Value Range Overlap

Temporary opcodes and short opcodes **share the same value range** (starting at 178). This is intentional because they are mutually exclusive:

- **Temporary opcodes** are only used during compilation and are never present in final bytecode
- **Short opcodes** are optimizations that only appear in final bytecode

This design keeps all final opcodes within 256 values (0-255), allowing them to fit in a single byte for efficient bytecode encoding. The `EmitOp` method emits opcodes as single bytes.

```csharp
// In OpCode.cs:
Nop = 177,           // Last non-temporary opcode

// Temporary opcodes (used during compilation only)
EnterScope,          // = 178 (same range as short opcodes)
LeaveScope,
Label,
// ... more temporary opcodes

// Short opcodes (in final bytecode, same range)
PushMinus1 = 178,    // Explicitly starts at 178
Push0,               // = 179
Push1,               // = 180
// ... more short opcodes
```

## Short Opcodes

Optimizations for common cases (reduces bytecode size):

```csharp
// Instead of:
PushI32 (5 bytes total: 1 opcode + 4 for int32)

// Use for small integers:
Push0, Push1, Push2, ... Push7 (1 byte each)
PushI8 (2 bytes: 1 opcode + 1 for int8)
PushI16 (3 bytes: 1 opcode + 2 for int16)

// Instead of:
GetLoc (3 bytes: 1 opcode + 2 for index)

// Use for first 4 locals:
GetLoc0, GetLoc1, GetLoc2, GetLoc3 (1 byte each)
```

## Files Added

- `src/QuickJS.Core/OpCode.cs` - OpCode enum (262 opcodes)
- `src/QuickJS.Core/OpCodeInfo.cs` - OpCodeInfo struct and OpCodeFormat enum
- `src/QuickJS.Core/OpCodes.cs` - Static metadata and lookup
- `tests/QuickJS.Tests/OpCodeTests.cs` - Comprehensive tests

## Test Coverage

```
OpCode enum tests:
  - Invalid_IsZero
  - OpCode_IsUShort
  - OpCode_HasExpectedCount

Stack effect tests:
  - PushOpcodes_HaveCorrectStackEffect
  - StackManipulationOpcodes_HaveCorrectStackEffect
  - BinaryOpcodes_Pop2Push1
  - UnaryOpcodes_Pop1Push1

Metadata tests:
  - ControlFlowOpcodes_HaveCorrectMetadata
  - LocalVariableOpcodes_HaveCorrectMetadata
  - PropertyAccessOpcodes_HaveCorrectMetadata
  - TemporaryOpcodes_AreMarkedAsTemporary
  - FinalOpcodes_AreNotMarkedAsTemporary

Lookup tests:
  - GetInfo_ReturnsCorrectInfo
  - TryGetByName_FindsOpcode
  - TryGetByName_IsCaseInsensitive
  - GetSize_ReturnsCorrectSize
  - GetStackDelta_ReturnsCorrectDelta
```

## Next Steps

With opcodes defined, the next step is to create:
1. **BytecodeWriter** - Emit bytecode instructions
2. **FunctionBuilder** - Build functions during compilation (JSFunctionDef equivalent)
3. **Compiler** - Parse and compile JavaScript to bytecode

## References

- `quickjs-opcode.h` - Original QuickJS opcode definitions
- `quickjs.c` lines 21553-21590 - JSOpCode structure
- `quickjs.c` lines 1066-1100 - OPCodeEnum definition
