# Step 7.10: TypedArrays and ArrayBuffer

This step implements TypedArrays, ArrayBuffer, and DataView - JavaScript's binary data handling APIs for efficient low-level memory operations.

## Overview

Binary data in JavaScript is handled through three related concepts:
1. **ArrayBuffer** - A raw block of bytes
2. **TypedArray** - A view over an ArrayBuffer with a specific element type
3. **DataView** - A low-level view for reading/writing arbitrary types with explicit endianness

## ArrayBuffer

### Purpose

`ArrayBuffer` represents a fixed-size block of raw binary data. It cannot be directly manipulated - instead, you use views (TypedArray or DataView) to read/write the data.

### Implementation

```csharp
public class JSArrayBuffer : JSObject
{
    private byte[] _data;
    
    public int ByteLength { get; }
    public bool IsDetached { get; private set; }
    public bool IsResizable { get; }
    public int MaxByteLength { get; }
    
    public JSArrayBuffer(int byteLength)
    {
        _data = new byte[byteLength];
        ByteLength = byteLength;
    }
}
```

### Key Features

| Method/Property | Description |
|----------------|-------------|
| `byteLength` | Size of the buffer in bytes |
| `slice(begin, end)` | Creates a new ArrayBuffer with copied data |
| `transfer(newLength)` | Transfers data to new buffer and detaches original |
| `resize(newLength)` | Resizes a resizable buffer |

### Detached Buffers

A buffer can be "detached" (made inaccessible) via `transfer()`. This is important for:
- Transferring ownership between contexts
- Preventing accidental mutation
- Efficient memory management

```javascript
let buffer = new ArrayBuffer(8);
let newBuffer = buffer.transfer(16);
// buffer is now detached - all operations throw TypeError
```

## TypedArrays

### Purpose

TypedArrays provide array-like access to binary data with a specific element type. They share the same `ArrayBuffer` but interpret the bytes differently.

### Variants

| Type | Bytes | Range |
|------|-------|-------|
| `Int8Array` | 1 | -128 to 127 |
| `Uint8Array` | 1 | 0 to 255 |
| `Uint8ClampedArray` | 1 | 0 to 255 (clamped) |
| `Int16Array` | 2 | -32768 to 32767 |
| `Uint16Array` | 2 | 0 to 65535 |
| `Int32Array` | 4 | -2³¹ to 2³¹-1 |
| `Uint32Array` | 4 | 0 to 2³²-1 |
| `Float32Array` | 4 | IEEE 754 single |
| `Float64Array` | 8 | IEEE 754 double |
| `BigInt64Array` | 8 | -2⁶³ to 2⁶³-1 |
| `BigUint64Array` | 8 | 0 to 2⁶⁴-1 |

### Implementation

```csharp
public class JSTypedArray : JSObject
{
    public TypedArrayKind Kind { get; }
    public JSArrayBuffer Buffer { get; }
    public int ByteOffset { get; }
    public int ByteLength { get; }
    public int Length { get; }
    public int BytesPerElement { get; }
    
    public JSValue GetElement(int index);
    public void SetElement(int index, JSValue value);
}
```

### Constructor Forms

```javascript
// Create with length
new Int32Array(10);  // 10 elements, new buffer

// Create from existing buffer
new Int32Array(buffer);  // View entire buffer
new Int32Array(buffer, byteOffset, length);  // Partial view

// Create from array-like
new Int32Array([1, 2, 3]);

// Create from typed array (copies)
new Int32Array(otherTypedArray);
```

### Shared Buffer Example

Multiple TypedArrays can view the same buffer:

```javascript
let buffer = new ArrayBuffer(8);
let int32View = new Int32Array(buffer);
let uint8View = new Uint8Array(buffer);

int32View[0] = 0x12345678;
// uint8View now contains [0x78, 0x56, 0x34, 0x12] (little-endian)
```

### Uint8ClampedArray

Special variant that clamps values to 0-255 range instead of wrapping:

```javascript
let clamped = new Uint8ClampedArray(1);
clamped[0] = 300;  // Clamped to 255
clamped[0] = -50;  // Clamped to 0
```

## DataView

### Purpose

DataView provides low-level read/write access with explicit endianness control. Unlike TypedArrays which use the platform's native endianness, DataView lets you specify the byte order for each operation.

### Implementation

```csharp
public class JSDataView : JSObject
{
    public JSArrayBuffer Buffer { get; }
    public int ByteOffset { get; }
    public int ByteLength { get; }
    
    public int GetInt8(int byteOffset);
    public int GetInt16(int byteOffset, bool littleEndian = false);
    public int GetInt32(int byteOffset, bool littleEndian = false);
    public double GetFloat64(int byteOffset, bool littleEndian = false);
    // ... etc.
}
```

### Methods

| Get Methods | Set Methods |
|------------|-------------|
| `getInt8(offset)` | `setInt8(offset, value)` |
| `getUint8(offset)` | `setUint8(offset, value)` |
| `getInt16(offset, le?)` | `setInt16(offset, value, le?)` |
| `getUint16(offset, le?)` | `setUint16(offset, value, le?)` |
| `getInt32(offset, le?)` | `setInt32(offset, value, le?)` |
| `getUint32(offset, le?)` | `setUint32(offset, value, le?)` |
| `getFloat32(offset, le?)` | `setFloat32(offset, value, le?)` |
| `getFloat64(offset, le?)` | `setFloat64(offset, value, le?)` |
| `getBigInt64(offset, le?)` | `setBigInt64(offset, value, le?)` |
| `getBigUint64(offset, le?)` | `setBigUint64(offset, value, le?)` |

### Endianness

```javascript
let buffer = new ArrayBuffer(4);
let view = new DataView(buffer);

view.setInt32(0, 0x12345678, true);   // Little-endian
// Buffer: [0x78, 0x56, 0x34, 0x12]

view.setInt32(0, 0x12345678, false);  // Big-endian (default)
// Buffer: [0x12, 0x34, 0x56, 0x78]
```

## Design Decisions

### 1. Byte Array Storage

We use `byte[]` for ArrayBuffer storage:
- Simple and efficient in .NET
- Direct support for BitConverter operations
- Easy integration with Span<T> on modern frameworks

### 2. TypedArrayKind Enum

```csharp
public enum TypedArrayKind
{
    Int8,
    Uint8,
    Uint8Clamped,
    Int16,
    Uint16,
    Int32,
    Uint32,
    Float32,
    Float64,
    BigInt64,
    BigUint64
}
```

This allows a single `JSTypedArray` class with kind-specific behavior.

### 3. Range Checking

All operations validate bounds and throw appropriate errors:
- `RangeError` for out-of-bounds access
- `TypeError` for detached buffer access
- `TypeError` for wrong argument types

### 4. BigInt Support

`BigInt64Array` and `BigUint64Array` work with `JSValue.FromBigInt()`:
- Values are represented as Int64/UInt64 internally
- Interoperates with JavaScript BigInt type

## C# Implementation Notes

### Endianness Handling

```csharp
private static byte[] ReverseEndianness(byte[] bytes)
{
    var result = new byte[bytes.Length];
    for (int i = 0; i < bytes.Length; i++)
        result[i] = bytes[bytes.Length - 1 - i];
    return result;
}

public int GetInt32(int byteOffset, bool littleEndian = false)
{
    var bytes = new byte[4];
    Array.Copy(_buffer.GetData(), ByteOffset + byteOffset, bytes, 0, 4);
    
    if (littleEndian != BitConverter.IsLittleEndian)
        bytes = ReverseEndianness(bytes);
    
    return BitConverter.ToInt32(bytes, 0);
}
```

### Clamped Conversion

```csharp
private static byte ClampToByte(double value)
{
    if (double.IsNaN(value)) return 0;
    if (value <= 0) return 0;
    if (value >= 255) return 255;
    return (byte)Math.Round(value);
}
```

## Tests

The implementation includes 45 tests covering:

1. **ArrayBuffer Tests**
   - Constructor with size
   - ByteLength property
   - Slice operations
   - Detach behavior
   - Transfer operations
   - Resizable buffers

2. **TypedArray Tests**
   - All 12 variants
   - Constructor forms (length, buffer, array)
   - Element access and modification
   - Subarray creation
   - Fill and set operations
   - CopyWithin
   - Shared buffer access

3. **DataView Tests**
   - All get/set methods
   - Endianness variations
   - Bounds checking
   - BigInt operations

## Files Created/Modified

### New Files
- `src/QuickJS.Core/JSArrayBuffer.cs` - ArrayBuffer implementation
- `src/QuickJS.Core/JSTypedArray.cs` - TypedArray implementation with all variants
- `src/QuickJS.Core/JSDataView.cs` - DataView implementation
- `tests/QuickJS.Tests/TypedArraysTests.cs` - 45 comprehensive tests

### Modified Files
- `src/QuickJS.Core/JSContext.cs` - Added `InitializeTypedArrays()` method

## Usage Examples

### Binary File Handling

```javascript
let buffer = new ArrayBuffer(1024);
let view = new DataView(buffer);

// Write header
view.setUint32(0, 0x89504E47, false);  // PNG signature (big-endian)
view.setUint32(4, 0x0D0A1A0A, false);
```

### Image Data Processing

```javascript
let imageData = new Uint8ClampedArray(width * height * 4);

// Set pixel (RGBA)
function setPixel(x, y, r, g, b, a) {
    let offset = (y * width + x) * 4;
    imageData[offset] = r;
    imageData[offset + 1] = g;
    imageData[offset + 2] = b;
    imageData[offset + 3] = a;
}
```

### Float Array for WebGL

```javascript
let vertices = new Float32Array([
    -1.0, -1.0, 0.0,
     1.0, -1.0, 0.0,
     0.0,  1.0, 0.0
]);
```

## Next Steps

- Step 7.11: Date object implementation
- Step 7.12: Symbol and Reflect
- Step 7.13: Proxy

## References

- [ECMAScript TypedArray Specification](https://tc39.es/ecma262/#sec-typedarray-objects)
- [MDN ArrayBuffer](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/ArrayBuffer)
- [MDN TypedArray](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/TypedArray)
- [MDN DataView](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/DataView)
