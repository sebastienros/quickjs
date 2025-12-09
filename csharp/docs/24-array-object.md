# Step 5.4: Array Object Implementation

## Overview

This step implements `JSArray`, the JavaScript Array exotic object. Arrays are among the most commonly used JavaScript objects, requiring special handling for their `length` property and optimized indexed element storage.

## What We Built

### JSArray Class

The `JSArray` class extends `JSObject` with specialized behavior for array operations:

```
JSArray : JSObject, IEnumerable<JSValue>
├── Fast array storage (_elements, _count)
├── Length property with special semantics
├── Array mutator methods (Push, Pop, Shift, Unshift)
├── Array accessor methods (Slice, Concat, IndexOf)
└── IEnumerable implementation for iteration
```

### Key Features

1. **Fast Array Storage**: Dense arrays use contiguous storage for O(1) indexed access
2. **Automatic Length Sync**: The `length` property stays synchronized with array modifications
3. **Array Methods**: Core JavaScript array prototype methods
4. **Enumeration**: Standard .NET enumeration support

## QuickJS C Reference

In QuickJS, arrays use a special storage structure:

```c
// quickjs.c - Array storage in JSObject union
struct { /* JS_CLASS_ARRAY, JS_CLASS_ARGUMENTS */
    union {
        uint32_t size;          // allocated size
    } u1;
    union {
        JSValue *values;        // actual element storage
    } u;
    uint32_t count;             // number of elements
} array;

// fast_array flag (quickjs.c:943)
uint8_t fast_array : 1; /* TRUE if u.array is used for get/put */
```

The `fast_array` flag indicates optimized storage is active. When elements are deleted or getters/setters defined, arrays fall back to property-based storage.

## Implementation Details

### Storage Architecture

```csharp
public class JSArray : JSObject, IEnumerable<JSValue>
{
    // Fast array storage
    private JSValue[] _elements;      // Contiguous element storage
    private uint _count;              // Actual element count
    private bool _isFastArray;        // Using optimized storage?
    
    // Construction
    public JSArray()                           // Empty array
    public JSArray(uint length)                // Pre-sized array
    public JSArray(JSValue[] values)           // From elements
    public JSArray(IEnumerable<JSValue> values) // From collection
}
```

### Length Property Behavior

The `length` property has exotic behavior per ECMAScript:

```csharp
public uint Length
{
    get => _count;
    set
    {
        if (value < _count)
        {
            // Truncate: remove elements beyond new length
            for (uint i = value; i < _count; i++)
                _elements[i] = default;
            _count = value;
        }
        else if (value > _count)
        {
            // Extend: fill with undefined
            EnsureCapacity(value);
            for (uint i = _count; i < value; i++)
                _elements[i] = JSValue.Undefined;
            _count = value;
        }
    }
}
```

### Indexed Access

```csharp
public JSValue this[uint index]
{
    get
    {
        if (_isFastArray && index < _count)
            return _elements[index];
        return JSValue.Undefined;
    }
    set
    {
        if (_isFastArray)
        {
            // Extend array if needed
            if (index >= _count)
            {
                EnsureCapacity(index + 1);
                for (uint i = _count; i < index; i++)
                    _elements[i] = JSValue.Undefined;
                _count = index + 1;
            }
            _elements[index] = value;
        }
    }
}
```

### Array Mutator Methods

```csharp
// Push: Add elements to end, return new length
public uint Push(params JSValue[] values)
{
    EnsureCapacity(_count + (uint)values.Length);
    foreach (var value in values)
        _elements[_count++] = value;
    return _count;
}

// Pop: Remove and return last element
public JSValue Pop()
{
    if (_count == 0) return JSValue.Undefined;
    return _elements[--_count];
}

// Shift: Remove and return first element
public JSValue Shift()
{
    if (_count == 0) return JSValue.Undefined;
    var first = _elements[0];
    Array.Copy(_elements, 1, _elements, 0, (int)_count - 1);
    _count--;
    return first;
}

// Unshift: Add elements to beginning
public uint Unshift(params JSValue[] values)
{
    if (values.Length > 0)
    {
        EnsureCapacity(_count + (uint)values.Length);
        Array.Copy(_elements, 0, _elements, values.Length, (int)_count);
        for (int i = 0; i < values.Length; i++)
            _elements[i] = values[i];
        _count += (uint)values.Length;
    }
    return _count;
}
```

### Array Accessor Methods

```csharp
// Slice: Create shallow copy of portion
public JSArray Slice(int start = 0, int? end = null)
{
    int len = (int)_count;
    int s = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);
    int e = end.HasValue 
        ? (end.Value < 0 ? Math.Max(len + end.Value, 0) : Math.Min(end.Value, len))
        : len;
    
    var result = new JSArray();
    for (int i = s; i < e; i++)
        result.Push(_elements[i]);
    return result;
}

// Concat: Combine arrays
public JSArray Concat(params JSArray[] arrays)
{
    var result = Slice(); // Copy this array
    foreach (var arr in arrays)
    {
        for (uint i = 0; i < arr.Length; i++)
            result.Push(arr[i]);
    }
    return result;
}

// IndexOf: Find element index
public int IndexOf(JSValue searchElement, int fromIndex = 0)
{
    int start = fromIndex < 0 
        ? Math.Max((int)_count + fromIndex, 0) 
        : Math.Min(fromIndex, (int)_count);
        
    for (int i = start; i < _count; i++)
    {
        if (_elements[i].Equals(searchElement))
            return i;
    }
    return -1;
}

// Includes: Check if element exists (uses SameValueZero)
public bool Includes(JSValue searchElement, int fromIndex = 0)
{
    // SameValueZero treats NaN === NaN
    for (int i = fromIndex; i < _count; i++)
    {
        if (SameValueZero(_elements[i], searchElement))
            return true;
    }
    return false;
}
```

### In-Place Mutators

```csharp
// Reverse: Reverse array in place
public JSArray Reverse()
{
    int left = 0, right = (int)_count - 1;
    while (left < right)
    {
        var temp = _elements[left];
        _elements[left++] = _elements[right];
        _elements[right--] = temp;
    }
    return this;
}

// Fill: Fill range with value
public JSArray Fill(JSValue value, int start = 0, int? end = null)
{
    int len = (int)_count;
    int s = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);
    int e = end.HasValue 
        ? (end.Value < 0 ? Math.Max(len + end.Value, 0) : Math.Min(end.Value, len))
        : len;
        
    for (int i = s; i < e; i++)
        _elements[i] = value;
    return this;
}

// Splice: Remove/insert elements
public JSArray Splice(int start, int deleteCount, params JSValue[] items)
{
    // Returns deleted elements, modifies array in place
    // ... implementation handles negative indices, extends/shrinks array
}
```

### Join and String Conversion

```csharp
public string Join(string separator = ",")
{
    if (_count == 0) return "";
    
    var sb = new StringBuilder();
    for (uint i = 0; i < _count; i++)
    {
        if (i > 0) sb.Append(separator);
        var elem = _elements[i];
        if (!elem.IsNull && !elem.IsUndefined)
            sb.Append(elem.ToString());
    }
    return sb.ToString();
}
```

## Capacity Management

Arrays automatically grow when needed:

```csharp
private void EnsureCapacity(uint minCapacity)
{
    if (minCapacity <= _elements.Length)
        return;
        
    // Grow by 1.5x or to minCapacity, whichever is larger
    uint newCapacity = Math.Max(
        (uint)(_elements.Length * 1.5),
        minCapacity
    );
    
    var newElements = new JSValue[newCapacity];
    Array.Copy(_elements, newElements, (int)_count);
    _elements = newElements;
}
```

## Class Hierarchy

```
JSObject
    │
    └── JSArray (JS_CLASS_ARRAY)
            │
            ├── Fast array mode (_isFastArray = true)
            │   └── Contiguous _elements[] storage
            │
            └── Slow array mode (_isFastArray = false)
                └── Property-based indexed storage (inherited from JSObject)
```

## Usage Examples

### Creating Arrays

```csharp
// Empty array
var empty = new JSArray();

// Pre-sized array
var sized = new JSArray(10);  // Length 10, filled with undefined

// From values
var nums = new JSArray(new[] { 
    JSValue.FromInt32(1), 
    JSValue.FromInt32(2), 
    JSValue.FromInt32(3) 
});

// Mixed types
var mixed = new JSArray(new[] {
    JSValue.FromInt32(42),
    JSValue.FromString("hello"),
    JSValue.True,
    JSValue.Null
});
```

### Array Operations

```csharp
var arr = new JSArray();

// Push/Pop (stack behavior)
arr.Push(JSValue.FromInt32(1));      // [1], returns 1
arr.Push(JSValue.FromInt32(2));      // [1, 2], returns 2
var last = arr.Pop();                 // [1], last = 2

// Unshift/Shift (queue behavior)
arr.Unshift(JSValue.FromInt32(0));   // [0, 1], returns 2
var first = arr.Shift();              // [1], first = 0

// Indexed access
arr[0] = JSValue.FromString("a");    // ["a"]
arr[5] = JSValue.FromString("f");    // ["a", undefined, undefined, undefined, undefined, "f"]
```

### Searching

```csharp
var arr = new JSArray(new[] { 
    JSValue.FromInt32(1), 
    JSValue.FromInt32(2), 
    JSValue.FromInt32(3) 
});

int idx = arr.IndexOf(JSValue.FromInt32(2));  // 1
bool has = arr.Includes(JSValue.FromInt32(3)); // true
bool hasNot = arr.Contains(JSValue.FromInt32(99)); // false
```

### Iteration

```csharp
// Using foreach
foreach (var element in array)
{
    Console.WriteLine(element);
}

// Using ToArray
JSValue[] values = array.ToArray();
```

## Test Coverage

The test suite covers 87 scenarios:

| Category | Tests | Description |
|----------|-------|-------------|
| Construction | 5 | Empty, sized, from values, from enumerable |
| Length | 4 | Expand, truncate, set to zero |
| Indexer | 6 | Get, set, expand, out-of-bounds |
| Push | 4 | Single, multiple, empty, no args |
| Pop | 4 | Remove last, empty, single element |
| Shift | 4 | Remove first, empty, single element |
| Unshift | 4 | Single, multiple, empty array |
| IndexOf | 6 | Found, not found, from index, strings |
| LastIndexOf | 4 | Found, not found, from index |
| Includes | 5 | Found, not found, NaN handling |
| Slice | 7 | Full copy, range, negative indices |
| Concat | 4 | Single, multiple, empty arrays |
| Join | 5 | Default separator, custom, empty |
| Reverse | 4 | In place, empty, single element |
| Fill | 4 | Full, range, negative indices |
| Splice | 5 | Delete, insert, replace |
| ToArray | 3 | Return copy, empty, modification |
| Enumeration | 2 | Foreach, empty |
| Fast Array | 3 | Initial state, push/pop, sparse |
| Edge Cases | 4 | Mixed types, nested arrays |

## Files Changed

| File | Lines | Description |
|------|-------|-------------|
| `src/QuickJS.Core/JSArray.cs` | ~1030 | Array implementation |
| `tests/QuickJS.Tests/JSArrayTests.cs` | ~1040 | 87 test cases |
| `docs/24-array-object.md` | ~400 | This documentation |

## Performance Considerations

1. **Fast Path**: Dense arrays use contiguous storage for O(1) access
2. **Capacity Growth**: 1.5x growth factor balances memory vs allocation overhead
3. **Shift/Unshift**: O(n) due to element copying - use Push/Pop when possible
4. **Sparse Arrays**: Very high indices extend the backing array, use slow mode for truly sparse data

## Next Steps

Step 5.4 completes Phase 5 (Object Model). The next phase will focus on:

- **Phase 6**: Complete runtime environment
- **Phase 7**: Module system
- **Phase 8**: Standard library built-ins
