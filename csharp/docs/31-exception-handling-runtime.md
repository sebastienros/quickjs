# Step 6.7: Exception Handling at Runtime

## Overview

This step equips the interpreter to **throw and catch exceptions**, unwinding call frames and jumping to `catch`/`finally` blocks defined in the compiled bytecode. We mirror QuickJS’s exception table strategy and integrate with the C# `JSException` hierarchy.

## Key Concepts

### Exception Tables

Each `JSFunctionDef` carries an **exception table** (`JSExceptionHandler`) with entries:

| Field | Description |
|-------|-------------|
| `StartPc`, `EndPc` | Protected bytecode region (inclusive/exclusive) |
| `CatchPc` | Handler PC or `-1` if none |
| `FinallyPc` | Handler PC or `-1` if none |
| `StackDepth` | Operand stack depth to restore |

> The interpreter scans the table to find the innermost handler covering the throw site.

### Unwinding

When a throw occurs (`OpCode.Throw`):

1. Pop operand stack to the handler’s `StackDepth`.
2. If `CatchPc` present: jump there **and push the exception value**.
3. Else if `FinallyPc` present: jump there and store a **pending throw**.
4. Else: detach var refs, pop the current frame, and propagate to the caller.

### Throw Sources

- `OpCode.Throw` pops a value and routes through `HandleThrow`
- Runtime helpers may call `_context.ThrowError(...)` to raise a `JSValue.Exception`

### `ret` Helper

QuickJS uses `OP_ret` to return control to exception handlers in `finally` blocks and to share the return/throw pathway. We mirror this via `OpCode.Ret` and a `_pendingAction` state (`Return` vs `Throw`).

## Exception Opcodes

| Opcode     | Stack Effect | Description |
|------------|--------------|-------------|
| `throw`    | value →      | Throw JS exception value |
| `catch`    | int32        | Compiler-emitted jump target label (treated like `goto` when implemented) |
| `ret`      | value? →     | Internal return used during unwinding |
| `return_async` | value? → | Async return (future step) |

> The `catch` opcode is emitted by the compiler to jump to the table entry; the interpreter handles it like a specialized `goto` during unwinding.

## Implementation Outline

1. **Exception Table**: `JSFunctionDef.ExceptionHandlers : List<JSExceptionHandler>`
2. **`OpCode.Throw`** → `HandleThrow`:
  - Find innermost handler covering `throwPc`
  - Restore stack, jump to `CatchPc`/`FinallyPc`, push exception for catch
  - If none, detach var refs, pop frame, propagate
3. **`OpCode.Return` / `OpCode.ReturnUndef`** → `HandleReturn`:
  - If in a protected region with `FinallyPc`, stash pending return and jump to `finally`
  - Otherwise finalize return value and exit
4. **`OpCode.Ret`**:
  - Completes pending return/throw after `finally` executes

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

## Tests

- `InterpreterExceptionTests.TryCatch_HandlesThrow`
- `InterpreterExceptionTests.TryFinally_ReturnsFromFinally`

## Next Steps

With core interpreter complete through exceptions, move on to **built-in objects** in Phase 7.
