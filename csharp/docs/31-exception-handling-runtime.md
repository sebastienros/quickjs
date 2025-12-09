# Step 6.7: Exception Handling at Runtime

## Overview

This step equips the interpreter to **throw and catch exceptions**, unwinding call frames and jumping to `catch`/`finally` blocks defined in the compiled bytecode. We mirror QuickJS’s exception table strategy and integrate with the C# `JSException` hierarchy.

## Key Concepts

### Exception Tables

Each `JSFunctionDef` carries an **exception table** with entries:

| Field | Description |
|-------|-------------|
| `start_pc`, `end_pc` | Protected bytecode region |
| `catch_pc` | Offset to `catch` handler (or -1) |
| `finally_pc` | Offset to `finally` handler (or -1) |
| `stack_depth` | Stack depth to restore on enter |
| `var_scope_idx` | Scope metadata (for `catch` bindings) |

The interpreter scans the table to find the innermost handler covering the throw site.

### Unwinding

When a throw occurs:

1. Pop operand stack to the handler’s `stack_depth`.
2. If `catch_pc` present: jump there, push the exception value, and continue.
3. Else if `finally_pc` present: jump there, remember pending exception.
4. Else: pop the current frame and propagate to the caller.

### Throw Sources

- `OP_throw` opcode (explicit `throw expr`)
- Implicit runtime errors (TypeError, ReferenceError, etc.) signaled by returning a `JSValue` tagged as exception or by throwing a `JSException` in C#.

### `ret` Helper

QuickJS uses `OP_ret` to return control to exception handlers in `finally` blocks and to share the return/throw pathway.

## Exception Opcodes

| Opcode     | Stack Effect | Description |
|------------|--------------|-------------|
| `throw`    | value →      | Throw JS exception value |
| `catch`    | int32        | Internal jump used by compiler for try/catch |
| `ret`      | value? →     | Internal return used during unwinding |
| `return_async` | value? → | Async return (future step) |

> The `catch` opcode is emitted by the compiler to jump to the table entry; the interpreter handles it like a specialized `goto` during unwinding.

## Implementation Outline

1. **Represent Exception**: Use `JSValue.IsException` flag (mirroring QuickJS) plus thrown `JSException` instances in C#.
2. **`OP_throw`**: Pop value, mark it as exception (or wrap if not already), and invoke `UnwindAndCatch`.
3. **`UnwindAndCatch`**:
   - Scan current function’s exception table for a matching entry (PC within `[start_pc, end_pc)`).
   - If found, trim stack to `stack_depth`, set `pc = catch_pc` (or `finally_pc`), and push the exception value (for `catch`).
   - If not found, pop the frame and continue with caller.
4. **Finally semantics**: Track a **pending exception/return** flag so `finally` can rethrow after executing.
5. **Return path**: On `return`, if inside a `finally`, route through the same unwinding helper so `finally` executes.

## Bytecode Example

```text
try {
  ...
} catch (e) {
  ...
} finally {
  ...
}

; Compiler emits exception table entry and labels
OP_try_enter L_catch L_finally
...
OP_goto L_finally_end
L_catch:
  ... handler ...
  OP_goto L_finally_end
L_finally:
  ... cleanup ...
L_finally_end:
```

## QuickJS References

- Exception table definitions: `JSFunctionBytecode` and `JSFunctionDef` in `quickjs.c`
- Interpreter handlers: `CASE(OP_throw)`, `CASE(OP_catch)`, `CASE(OP_ret)` (≈ lines 18516+)
- Unwinding helper: `js_unwind_function` / `handle_exception` paths

## Tests to Add (when implementing)

- `throw` with primitive and object values
- `try/catch` binding the error value
- `try/finally` ensuring cleanup runs for throw and return
- Nested try/catch/finally
- Exception propagation across frames

## Next Steps

With core interpreter complete through exceptions, we can move on to **built-in objects** in Phase 7.
