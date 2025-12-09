# Step 7.4: String

## Goals
- Implement `String` constructor (primitive conversion + boxed objects)
- Provide `String.prototype.toString`/`valueOf`
- Support `length` and indexed access on String wrappers

## Design
- `String` constructor:
  - `String(x)` → primitive string via `ToString`
  - `new String(x)` → boxed object (`JSClassId.String`) with `InternalValue`
- JSObject special-cases `JSClassId.String` for:
  - `length` property
  - Indexed character access (`obj[0]` → first character)

## Key Code
- `JSContext.InitializeStringConstructor()`
- `JSObject.Get` overrides for `JSClassId.String`

## Tests
- `BuiltinsStringTests.StringConstructor_ReturnsPrimitive`
- `BuiltinsStringTests.NewString_ReturnsBoxedObjectWithLength`
- `BuiltinsStringTests.StringPrototypeToString_WorksForWrapper`

## Notes
- Additional String prototype methods (charAt, slice, etc.) are deferred
