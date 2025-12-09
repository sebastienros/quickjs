# Step 7.2: Error Objects

## Goals
- Provide native error constructors: `Error`, `TypeError`, `RangeError`, `ReferenceError`, `SyntaxError`, `URIError`, `EvalError`, `InternalError`, `AggregateError`
- Hook `JSContext.ThrowError` to use specific prototypes

## Design
- Error prototypes are created during built-in initialization and stored in `_errorPrototypes`
- Each constructor:
  - Sets `name`
  - Optional `message` argument
  - Sets `.prototype`
  - Binds `prototype.constructor`

## Key Code
- `JSContext.InitializeErrorConstructors()`
- `JSContext.RegisterErrorConstructor(...)`
- `JSContext.ThrowError(...)` now picks prototype by `JSErrorType`

## Tests
- `BuiltinsErrorTests.ErrorConstructors_CreateObjectsWithNameAndMessage`
- `BuiltinsErrorTests.ThrowTypeError_UsesTypeErrorPrototype`

## Notes
- Stack traces and causes are not yet implemented
- Error subclassing behavior is minimal
