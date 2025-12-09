# Step 7.9: Collections (Map/Set/WeakMap/WeakSet)

## Goals
- Implement Map, Set, WeakMap, WeakSet constructors and core methods

## Design
- Store data in host collections via `JSObject.HostData`
  - `Map`/`WeakMap` → `Dictionary<JSValue, JSValue>` (simplified weak behavior)
  - `Set`/`WeakSet` → `HashSet<JSValue>`
- Methods:
  - Map: `set`, `get`, `has`, `delete`, `clear`, `size`
  - Set: `add`, `has`, `delete`, `clear`, `size`
- Weak collections: key/value must be objects (no GC semantics)

## Key Code
- `JSContext.InitializeCollections()`
- `JSValueComparer` for key equality

## Tests
- `BuiltinsCollectionsTests.Map_BasicOperations`
- `BuiltinsCollectionsTests.Set_BasicOperations`

## Notes
- Iterators and full spec behaviors (insertion order, forEach) are not implemented
- Weak collection GC semantics are not modeled
