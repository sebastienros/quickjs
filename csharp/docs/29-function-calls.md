# Step 6.5: Interpreter Function Calls

## Overview

This step teaches the interpreter to **invoke functions**—both bytecode and native C# delegates—by creating call frames, binding `this`, passing arguments, and handling returns. We wire up the `call` / `return` opcodes and the `OP_call_constructor` path for `new`.

## Key Concepts

### Call Frames

A call frame tracks per-invocation state (see Step 6.3): locals, args, var refs, `thisValue`, return address (`savedPC`), and saved stack pointer. Calls push a new frame; returns pop it and restore the caller’s PC/SP.

### Function Kinds

| Kind | Representation | Notes |
|------|----------------|-------|
| Bytecode function | `JSFunction` with `JSFunctionDef` | Executed by interpreter |
| Native function | `JSCFunction` delegate | Called directly; result wrapped in `JSValue` |
| Bound function | `JSBoundFunction` | Stores target, bound `this`, and bound args |
| Constructor | Bytecode or native with `FunctionKind.Constructor` | Sets up `new.target` and instance allocation |

### `this` Binding

- **Function call** (`f()`): `this` is `undefined` in strict mode; otherwise global object (Step 7.1 will define it).
- **Method call** (`obj.f()`): `this` is the base object of the property reference.
- **Constructor call** (`new F()`): `this` is the newly allocated object; `new.target` is `F`.

### Arguments Object

For now, the compiler emits direct arg accesses (`GetArg`/`PutArg`). Creating the `arguments` object happens in a later step; but the call machinery must pass the actual argument list length.

## Call Opcodes

| Opcode              | Stack Effect                              | Description |
|---------------------|-------------------------------------------|-------------|
| `call argc`         | callee arg0…argN-1 → result               | Invoke function (`this` = `undefined`) |
| `tail_call argc`    | callee arg0…argN-1 → result               | Tail-call form (currently executed like `call`) |
| `call_method atom argc` | obj arg0…argN-1 → result              | Invoke method (`this` = `obj`, callee loaded from property `atom`) |
| `tail_call_method atom argc` | obj arg0…argN-1 → result          | Tail-call form (currently executed like `call_method`) |
| `call_constructor argc` | ctor newTarget arg0…argN-1 → result    | Constructor call (allocates `this`, passes `new.target`) |
| `apply flags`       | callee this argsArray → result            | Invoke using an argument array (used for spreads) |
| `apply_eval flags`  | callee this argsArray → result            | Eval-specific apply (currently behaves like `apply`) |
| `return`            | value →                                   | Return value to caller |
| `return_undef`      | —                                         | Return `undefined` |
| `ret`               | value? →                                  | Internal helper used by try/catch |

> QuickJS encodes additional call flags (has_this, is_tail, etc.). In this C# port the common call shapes are represented by distinct opcodes (`call`, `call_method`, `call_constructor`, `apply`).

## Implementation Outline

1. **Decode operands**: `argc` for fixed-arity calls, or a reserved `flags` field for `apply`/`apply_eval`.
2. **Collect call site values**:
   - `call`/`tail_call`: pop `argc` args + callee
   - `call_method`/`tail_call_method`: pop `argc` args + receiver object; load callee from property
   - `apply`/`apply_eval`: pop `argsArray`, `this`, `callee` and expand into an argument list
3. **Resolve call target**:
   - If callee is `JSFunction` (bytecode): create a new `CallFrame`, set `savedPC/SP`, `thisValue`, `args`, `newTarget`, then jump into callee’s bytecode by setting `pc = 0` and `code = callee.Bytecode`.
   - If callee is native: call the delegate (`JSCFunction`) with the runtime/context, `thisValue`, argument span, and `newTarget`; push the returned `JSValue` (or throw on exception).
   - If callee is bound: prepend bound args, override `this` with bound `this`, then recurse into call logic.
4. **Tail call**: If `tail_call` flag set and callee is bytecode, reuse the current frame (replace `func`, `locals`, etc.) instead of pushing a new one.
5. **Return**: `return`/`return_undef` pop the current frame, restore caller `pc`/`stackPointer`, and push the return value (or `undefined`).
6. **Constructor result**: If a constructor returns a non-object, return the allocated `this` instead (per JS spec).
7. **`arguments` object**: `OP_special_object` creates mapped (non-strict) or unmapped (strict) arguments objects backed by the call frame's args.

## Bytecode Examples

### Simple Call

```text
; evaluates f(1, 2)
... push callee f ...
Push1
Push2
call argc=2
```

### Method Call (compiler side)

```text
... push receiver obj ...
Push1
Push2
call_method "f" argc=2
```

### Constructor Call

```text
... push ctor ...
... allocate this (Object.create(ctor.prototype)) ...
Push arg0 ...
call_constructor argc=1
```

## Spread Arguments

When a call expression contains `...` (argument spread), the compiler lowers it to:

1. Build an argument array at runtime using `array_from`, `define_array_el`, and `append`.
2. Call via `apply` (or `apply_eval` for eval-specific call sites).

Current limitation: `append` supports spreading **arrays** only (general iterables via `Symbol.iterator` are not implemented yet).

## QuickJS References

- Interpreter call handling: `CASE(OP_call)` et al. in `quickjs.c` (≈ lines 17872–17980)
- Constructor logic: `OP_call_constructor` and `JS_RunFunction`
- Tail calls: `OP_tail_call` handling in interpreter
- Bound functions: `JS_CallInternal` path for `JS_TAG_OBJECT` with class `JS_CLASS_BOUND_FUNCTION`

## Arguments Object

- **Mapped** (`SpecialObjectType.MappedArguments`): non-strict mode; `arguments[i]` aliases formal parameters; exposes `callee`.
- **Unmapped** (`SpecialObjectType.Arguments`): strict mode; no aliasing; `callee` is `undefined`.
- Implemented via `JSArgumentsObject` with per-index read/write hooks into the current `CallFrame`.

## C# vs QuickJS Mapping

| Concept | QuickJS (C) | C# Implementation |
|---------|-------------|-------------------|
| Call opcode | `CASE(OP_call)` | `Interpreter.Execute` `OpCode.Call`/`CallN` branch |
| Method call | `OP_call_method` uses `call_argv[-2]` as this | `CallMethod` pops `this` before callee, passes to `CallFunction` |
| Constructor | `OP_call_constructor` | `CallConstructor` allocates `this` (prototype if available), returns object or `this` |
| Tail call | `OP_tail_call(*)` | Treated as normal call (no reuse yet) |
| `this` binding | Passed to `JS_CallInternal` | Stored in `CallFrame.ThisValue`, exposed via `OpCode.PushThis` |
| Constant pool functions | `OP_fclosure/OP_push_const` | `PushConst` loads `JSFunction` instances from `JSFunctionDef.Constants` |

## Tests to Add (when implementing)

- Call bytecode functions with varying arity (including fewer/more args)
- Method calls with correct `this`
- Bound functions preserving `this` and bound args
- Constructor returning object vs primitive
- Tail calls (verify no stack growth)
- Native function invocation via `JSCFunction`

## Next Steps

With calls working, we can implement **closures** in Step 6.6 to capture variables across nested functions.
