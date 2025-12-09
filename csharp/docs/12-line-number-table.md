# Step 3.5: Line Number Table

## Overview

The `LineNumberTable` tracks the mapping from bytecode positions (PC values) to source code locations. This is essential for:

- **Error messages**: Showing where in the source code an error occurred
- **Stack traces**: Displaying file:line:column for each call frame
- **Debugging**: Setting breakpoints and stepping through code

This step enhances the `LineNumberTable` with QuickJS's compact pc2line encoding format, which allows efficient storage of debug information in the compiled bytecode.

## Why Source Maps Matter in JavaScript

When your JavaScript code throws an error, you want to see something like:

```
TypeError: Cannot read property 'x' of undefined
    at processData (app.js:42:15)
    at main (app.js:10:5)
```

Not just raw bytecode offsets:

```
TypeError: Cannot read property 'x' of undefined
    at PC 0x1a4
    at PC 0x05f
```

The line number table makes human-readable error messages possible.

## The Problem: Size vs. Precision

A naive approach would store every bytecode offset's line and column:

```
PC 0: line 1, col 1
PC 1: line 1, col 5
PC 2: line 1, col 10
PC 3: line 2, col 1
...
```

For a 10KB bytecode file, this could add 40KB+ of debug data. That's 4x overhead!

## QuickJS's Solution: Delta Encoding

QuickJS uses a clever encoding that exploits patterns in real code:

1. **Most line changes are small**: Moving from line 5 to line 6 is common; jumping from line 5 to line 500 is rare
2. **PC increments are usually small**: Most instructions are 1-5 bytes
3. **Many instructions share the same source line**: A single line of JavaScript might compile to 10+ bytecode instructions

### The Compact Format

QuickJS encodes most entries in just 2 bytes:

**Byte 1 (Operation byte):**
- Encodes both PC delta (0-50) and line delta (-1 to +3) in a single byte
- Formula: `op = (line_delta + 1) + (pc_delta * 5) + 1`
- If the deltas don't fit, uses extended format (prefix byte = 0)

**Byte 2+ (Column delta):**
- Signed LEB128 encoding for the column change

### Extended Format

For larger jumps (e.g., jumping 100 lines), an extended format is used:
- Byte 1: `0` (indicates extended format)
- Following bytes: PC delta as unsigned LEB128
- Following bytes: Line delta as signed LEB128
- Following bytes: Column delta as signed LEB128

### LEB128 Encoding

LEB128 (Little Endian Base 128) is a variable-length integer encoding:
- Small numbers (0-127) fit in 1 byte
- Larger numbers use continuation bits

```
Value 127  → 0x7F (1 byte)
Value 128  → 0x80 0x01 (2 bytes)
Value 300  → 0xAC 0x02 (2 bytes)
Value -1   → 0x7F (signed LEB128)
```

## QuickJS Reference

From QuickJS `quickjs.c`:

```c
/* for the encoding of the pc2line table */
#define PC2LINE_BASE     (-1)
#define PC2LINE_RANGE    5
#define PC2LINE_OP_FIRST 1
#define PC2LINE_DIFF_PC_MAX ((255 - PC2LINE_OP_FIRST) / PC2LINE_RANGE)

typedef struct LineNumberSlot {
    uint32_t pc;
    uint32_t source_pos;
} LineNumberSlot;
```

The constants define:
- **Base (-1)**: Line deltas from -1 to +3 can be encoded compactly
- **Range (5)**: 5 possible line delta values per PC delta slot
- **OpFirst (1)**: 0 is reserved for extended format
- **DiffPCMax (50)**: Maximum PC delta in compact format: (255-1)/5 = 50

## Implementation

### Source Location Tracking

During compilation, we track raw source positions:

```csharp
public readonly struct LineNumberSlot
{
    public int Pc { get; }           // Bytecode offset
    public int SourcePosition { get; }  // Source text offset
}
```

### PC Source Location

After decoding, we have resolved line/column info:

```csharp
public readonly struct PCSourceLocation
{
    public int PC { get; }     // Bytecode offset
    public int Line { get; }   // 1-based line number
    public int Column { get; } // 1-based column number
}
```

### The Line Number Table

```csharp
public sealed class LineNumberTable
{
    // Base location for the function
    public int BaseLine { get; set; } = 1;
    public int BaseColumn { get; set; } = 1;
    
    // Add a PC to source position mapping
    public bool Add(int pc, int sourcePosition);
    
    // Get source position for a PC (binary search)
    public int GetSourcePosition(int pc);
    
    // Get line/column from source offset
    public static (int Line, int Column) GetLineColumn(string source, int position);
    
    // Find location with resolved line/column
    public PCSourceLocation FindLocation(int pc, string? sourceText);
    
    // Adjust PC values after bytecode optimization
    public void AdjustPC(int position, int delta);
    
    // Compact encoding
    public byte[] Encode(string sourceText);
    public static IEnumerable<PCSourceLocation> Decode(byte[] data);
    public static PCSourceLocation FindInEncoded(byte[] data, int targetPC);
}
```

### Deduplication

The table automatically deduplicates entries where the source position hasn't changed:

```csharp
public bool Add(int pc, int sourcePosition)
{
    // Only add if source position changed and PC is not decreasing
    if (sourcePosition == _lastSourcePosition || pc < _lastPc)
    {
        return false;
    }
    // ... add entry
}
```

This is important because many bytecode instructions may correspond to the same source position.

## How It's Used

### During Compilation

As bytecode is emitted, the current source position is recorded:

```csharp
// In JSFunctionDef during compilation
void EmitWithLineInfo(OpCode op, int sourcePos)
{
    LineNumbers.Add(ByteCode.Length, sourcePos);
    ByteCode.Emit(op);
}
```

### Generating Stack Traces

When an exception occurs:

```csharp
PCSourceLocation GetErrorLocation(int pc, byte[] pc2lineData, string source)
{
    if (pc2lineData != null)
    {
        return LineNumberTable.FindInEncoded(pc2lineData, pc);
    }
    // Fallback: use raw PC
    return new PCSourceLocation(pc, 0, 0);
}
```

### Line/Column Calculation

Converting a byte offset to line/column:

```csharp
public static (int Line, int Column) GetLineColumn(string source, int position)
{
    int line = 1;
    int column = 1;
    
    for (int i = 0; i < position; i++)
    {
        if (source[i] == '\n')
        {
            line++;
            column = 1;
        }
        else if (source[i] == '\r')
        {
            // Handle \r\n as single newline
            if (i + 1 < source.Length && source[i + 1] == '\n')
                i++;
            line++;
            column = 1;
        }
        else
        {
            column++;
        }
    }
    
    return (line, column);
}
```

## PC Adjustment After Optimization

When bytecode is optimized (e.g., dead code elimination), PC values in the line number table must be adjusted:

```csharp
// After removing 3 bytes at position 100
table.AdjustPC(100, 3);

// Entries with PC > 100 are reduced by 3
// Entries with PC <= 100 are unchanged
```

## Encoding Example

Consider this JavaScript:

```javascript
let x = 1;  // Line 1, bytes 0-10
let y = 2;  // Line 2, bytes 11-20
```

**Entries collected:**
```
PC 0:  source pos 0  → Line 1, Col 1
PC 11: source pos 11 → Line 2, Col 1
```

**Encoding:**
1. Base: line 1, col 1 → `[0x00, 0x00]` (0-based in LEB128)
2. Entry 2: PC delta = 11, line delta = +1, col delta = 0
   - Line delta +1 is in range (-1 to +3) ✓
   - But PC delta 11 exceeds DiffPCMax (50)? No, 11 ≤ 50 ✓
   - Compact: `op = (1 - (-1)) + (11 * 5) + 1 = 2 + 55 + 1 = 58` → `0x3A`
   - Column delta 0 → `0x00`

**Encoded bytes:** `[0x00, 0x00, 0x3A, 0x00]` (4 bytes total)

## Constants

```csharp
public static class PC2LineConstants
{
    public const int Base = -1;      // Min line delta for compact encoding
    public const int Range = 5;      // Number of line delta values
    public const int OpFirst = 1;    // First compact opcode (0 = extended)
    public const int DiffPCMax = 50; // Max PC delta for compact encoding
}
```

## Tests

The test suite covers:

1. **Basic operations**: Add, count, clear, indexer
2. **GetSourcePosition**: Binary search lookup
3. **GetLineColumn**: Line/column calculation with various newline styles
4. **FindLocation**: Full location resolution
5. **AdjustPC**: PC adjustment after optimization
6. **Encode/Decode**: Round-trip verification
7. **FindInEncoded**: Direct lookup in encoded data
8. **Compact vs Extended**: Encoding format selection

## File Changes

```
src/QuickJS.Core/
├── LineNumberTable.cs  # Enhanced with Encode/Decode, GetLineColumn
```

## Summary

The line number table is the bridge between bytecode and source code. QuickJS's compact delta encoding keeps debug information small while providing precise error locations. This implementation:

1. **Collects** source positions during compilation
2. **Deduplicates** redundant entries automatically
3. **Encodes** to a compact format for storage
4. **Decodes** for error messages and debugging
5. **Supports** bytecode optimization with PC adjustment

With this in place, our JavaScript engine can generate helpful error messages with file, line, and column information—just like the engines you use every day.
