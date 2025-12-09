# Step 7.7: JSON

## Goals
- Implement `JSON.parse` and `JSON.stringify`

## Design
- Backed by `System.Text.Json`
- Conversion helpers:
  - `FromJsonElement` → `JSValue`
  - `ToJsonCompatible` → CLR types
- Supports objects, arrays, strings, numbers, booleans, null

## Key Code
- `JSContext.InitializeJSON()`
- `JSObject.GetOwnProperties()` used for serialization

## Tests
- `BuiltinsJsonTests.JsonParse_ParsesObject`
- `BuiltinsJsonTests.JsonStringify_SerializesPrimitivesAndObjects`

## Notes
- Reviver/replacer not supported
- Formatting options not supported (always compact)
