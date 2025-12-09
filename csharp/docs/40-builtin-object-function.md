# Step 7.1: Object & Function Built-ins

## Goals
- Expose `Object` and `Function` constructors on the global object
- Wire prototypes: `Object.prototype`, `Function.prototype`
- Implement essential `Object` helpers: `Object.create`, `Object.prototype.hasOwnProperty`, `Object.prototype.toString`
- Stub `Function` constructor (string compilation not yet supported)

## Design
- Prototypes are created during context initialization (`InitializeBasicObjects`)
- Built-ins are installed via `InitializeBuiltins`
- `Object` constructor boxes primitives (String, Number, Boolean) and returns objects
- `Object.create(proto)` validates `proto` (object or null)
- `Function` constructor currently throws a `TypeError` when invoked with source strings

## Key Code
- `JSContext.InitializeBuiltins()`
  - `InitializeObjectConstructor()`
    - Global `Object`
    - `Object.create`
    - `Object.prototype.hasOwnProperty`
    - `Object.prototype.toString`
  - `InitializeFunctionConstructor()`

## Tests
- `BuiltinsObjectFunctionTests`
  - `ObjectConstructor_CreatesPlainObject`
  - `ObjectCreate_SetsPrototype`
  - `ObjectPrototype_HasOwnProperty_Works`
  - `FunctionConstructor_ThrowsNotSupported`

## Notes / Limitations
- `Function` constructor from strings is not implemented
- `Object.prototype.toString` reports `[object <ClassId>]`
- Additional Object helpers (assign, keys, etc.) are deferred
