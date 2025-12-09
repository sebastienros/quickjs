# Step 6.6: Closures

## Overview

Closures allow inner functions to **capture variables** from their enclosing scope. QuickJS models captures with **VarRefs**—lightweight objects pointing to a variable slot in an outer frame. This step wires up opcodes to create, access, and close VarRefs so closures observe the latest values even after the outer function returns.

## Key Concepts

### VarRef (Upvalue)

A `JSVarRef` holds a reference to a variable slot:

- `IsClosed`: whether the VarRef has been detached from the outer frame
- `Value`: current value (inline when closed; pointer when open)

When the outer frame goes away, open VarRefs are **closed** by copying the variable’s value into the VarRef.

### Capture Strategy

- The compiler marks variables that need capturing.
- For each captured variable, emit `OP_make_var_ref <slot>` to create a VarRef bound to the outer frame’s slot.
- When creating an inner function, pass its VarRefs so it can access them via indices.

## Closure Opcodes

| Opcode            | Operand | Stack Effect      | Description |
|-------------------|---------|-------------------|-------------|
| `make_var_ref`    | slot    | → varref          | Create VarRef for outer slot |
| `get_var_ref`     | idx     | → value           | Read captured variable |
| `put_var_ref`     | idx     | value →           | Write captured variable |
| `close_var_ref`   | idx     | —                 | Close VarRef (copy value) |
| `closure`         | funcIdx | varrefN…varref0 → func | Create closure with captured VarRefs |

> In QuickJS, `OP_closure` consumes a variable-length list of VarRefs; the func’s `closure_var_count` tells how many to pop.

## Implementation Outline

1. **VarRef class**: Already introduced in Step 5.3. Ensure it supports open vs closed state and indirection into a `CallFrame` slot.
2. **`make_var_ref`**: Given a slot index, allocate a `JSVarRef` that points to the current frame (`CallFrame`) and slot. Push it on the stack.
3. **`closure`**:
   - Operand references a `JSFunction` template (from the constant pool / function table).
   - Pop `closure_var_count` VarRefs, attach them to the new `JSFunction` instance, and push the function.
4. **Access opcodes**: `get_var_ref`/`put_var_ref` read/write through the `JSVarRef` abstraction (respecting TDZ if needed).
5. **Closing VarRefs**: When a frame is about to be popped, iterate its open VarRefs and close them (copy value, null out frame pointer).

## Bytecode Example

```text
function outer() {
  let x = 1;               ; locals[0]
  return function inner() {
    return x + 1;
  };
}

; Bytecode sketch
OP_push1            ; x = 1
OP_put_loc 0
OP_make_var_ref 0   ; capture x
OP_closure f_inner 1 ; pops varref, creates closure
OP_return
```

## QuickJS References

- VarRef structure and helpers: `JSVarRef` in `quickjs.c`
- Interpreter opcodes: `OP_make_var_ref`, `OP_get_var_ref_value`, `OP_put_var_ref_value`, `OP_close_var_ref`, `OP_closure`
- Frame closing: `close_var_refs` invoked when popping frames

## Tests to Add (when implementing)

- Simple closure reading a captured `let`/`const`
- Writing through closures (counter example)
- Nested closures (three levels deep)
- Closing behavior after outer returns
- TDZ behavior for captured `let`/`const`

## Next Steps

With closures captured, we can implement **exception handling** in Step 6.7 to unwind frames safely.
