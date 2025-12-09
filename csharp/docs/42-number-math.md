# Step 7.3: Number & Math

## Goals
- Implement `Number` constructor (primitive conversion + boxed objects)
- Provide `Math` global with core constants and functions

## Design
- `Number` constructor:
  - `Number(x)` → numeric primitive via `ToNumber`
  - `new Number(x)` → boxed object (`JSClassId.Number`) with `InternalValue`
- `Math` object:
  - Constants: `PI`, `E`
  - Functions: `abs`, `floor`, `ceil`, `round`, `max`, `min`, `pow`, `sqrt`, `random`
  - `random` uses a simple LCG based on `_randomState`
- Wrapper conversion support added to `JSValueConversion.ToNumber/ToString`

## Key Code
- `JSContext.InitializeNumberAndMath()`
- `JSValueConversion` wrapper handling

## Tests
- `BuiltinsNumberMathTests.NumberConstructor_ReturnsPrimitive`
- `BuiltinsNumberMathTests.NewNumber_ReturnsBoxedObject`
- `BuiltinsNumberMathTests.MathFunctions_Work`
- `BuiltinsNumberMathTests.MathRandom_ReturnsInRange`

## Notes
- Floating-point edge cases follow System.Math behavior
- Additional Math functions (sin, cos, etc.) can be added later
