# Step 7.5: Array

## Goals
- Implement `Array` constructor
- Support `length`, indexed elements, and basic mutators (`push`, `pop`)

## Design
- `Array` constructor:
  - `Array(len)` (single numeric arg) → sparse array with `length = len`
  - `Array(...items)` → array initialized with items
- JSObject tracks `_arrayLength` for `JSClassId.Array`
  - `Set(index, value)` updates length
  - `ArrayPop()` removes last element and decrements length
  - `SetArrayLength(uint)` truncates when shrinking
- `Array.prototype` methods:
  - `push(...items)`
  - `pop()`

## Key Code
- `JSObject` array length tracking and helpers
- `JSContext.InitializeArrayConstructor()`

## Tests
- `BuiltinsArrayTests.ArrayConstructor_WithElements_SetsLengthAndValues`
- `BuiltinsArrayTests.ArrayConstructor_WithLength_CreatesSparseArray`
- `BuiltinsArrayTests.ArrayPushPop_Works`

## Notes
- More Array.prototype methods (map, filter, etc.) are deferred
- Deleting elements doesn't shrink length (matches JS)
