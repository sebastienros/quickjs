# Step 6.4: Interpreter Control Flow

## Overview

This step extends the bytecode interpreter with **program counter (PC) manipulation** so it can execute `if`/`else`, loops, `switch`, short-circuit boolean logic, and non-local jumps used by `break`/`continue` and optional chaining. We add conditional and unconditional jump opcodes and wire them into the interpreter loop.

## Key Concepts

### Program Counter and Offsets

The interpreter maintains a **program counter (PC)** pointing to the current bytecode instruction. Jump opcodes add a signed offset to the PC to branch to a new location. QuickJS encodes offsets in 8-bit and 16-bit forms for compactness.

```
+--------+---------+-----------------------------+
| opcode | offset  | semantics                   |
+--------+---------+-----------------------------+
| goto   | int32   | pc += offset                |
| goto16 | int16   | pc += offset                |
| goto8  | int8    | pc += offset                |
| if_*   | int32/i8| pop condition, branch if ?  |
+--------+---------+-----------------------------+
```

### Truthiness

Conditional jumps evaluate JavaScript **truthiness**. Use the same `ToBoolean` coercion logic as comparisons and logical operators.

### Break/Continue Labels

Loops and `switch` statements emit labels for **break** and **continue** targets. The compiler records them in a stack (`BlockEnv` in C, analogous metadata in C#) so `break`/`continue` opcodes can jump to the correct bytecode address.

### Short-Circuiting

Logical AND/OR emit conditional jumps to avoid evaluating the RHS when the result is already known. Nullish coalescing and optional chaining use jump regions that skip dereferences when a `null`/`undefined` guard triggers.

## Control Flow Opcodes

| Opcode        | Operand   | Stack Effect      | Description                             |
|---------------|-----------|-------------------|-----------------------------------------|
| `goto`        | int32     | —                 | Unconditional jump                      |
| `goto16`      | int16     | —                 | Short unconditional jump                |
| `goto8`       | int8      | —                 | Tiny unconditional jump                 |
| `if_true`     | int32     | cond →            | Jump if truthy                          |
| `if_false`    | int32     | cond →            | Jump if falsy                           |
| `if_true8/16` | int8/16   | cond →            | Size-optimized variants                 |
| `catch`       | int32     | exception →       | Jump to catch handler (Step 6.7)        |
| `ret`         | —         | value →           | Internal return to caller frame         |

> QuickJS also emits peephole-optimized patterns (e.g., `if_false8`) during bytecode finalization (`optimize_jumps` in `quickjs.c`).

## Implementation Outline

1. **Decode offsets**: Read signed offsets according to opcode width (1, 2, or 4 bytes). Apply to `pc` *after* advancing past the operand.
2. **Truthiness**: Use `JSValueConversion.ToBoolean()` to evaluate conditions. Pop the condition before branching.
3. **Unconditional jumps**: Update `pc` directly. Do not touch the operand stack.
4. **Conditional jumps**: Pop condition, compute truthiness, branch accordingly.
5. **Break/Continue**: The compiler emits `goto` to loop labels; interpreter just executes jumps. (No extra runtime work beyond jumping.)
6. **Switch**: Compiler emits comparison + conditional jumps to case bodies and a default label. Interpreter only sees jumps.
7. **Optional chaining / nullish coalescing**: Compiler emits guard branches using `if_false`/`if_true`; interpreter already supports them once implemented.

## Bytecode Examples

### if / else

```text
0:  ...evaluate condition...
X:  if_false L_else
    ...then block...
    goto L_end
L_else:
    ...else block...
L_end:
```

### while loop

```text
L_test:
    ...evaluate condition...
    if_false L_end
    ...body...
    goto L_test
L_end:
```

### short-circuit AND (`a && b`)

```text
...eval a...
if_false L_skip   ; if a falsy, skip b
...eval b...
; result of b on stack
L_skip:
```

## QuickJS References

- Bytecode definitions: `quickjs-opcode.h` (`OP_goto*`, `OP_if_true*`, `OP_if_false*`)
- Interpreter switch: `quickjs.c` `CASE(OP_goto)` (≈ line 18416), `CASE(OP_if_true)` (≈ 18433)
- Jump optimization: `optimize_jumps` in `quickjs.c`
- Break/continue handling in compiler: `emit_break` / `emit_continue` (≈ lines 27707+)

## C# vs QuickJS Mapping

| Concept | QuickJS (C) | C# Implementation |
|---------|-------------|-------------------|
| Program Counter | `pc` pointer into bytecode | `pc` local in `Interpreter.Execute` |
| Stack | `sp` array of `JSValue` | `_stack` array + `_stackPointer` |
| `OP_goto*` | `pc += offset` | same; `Goto`, `Goto8`, `Goto16` cases read signed offsets |
| `OP_if_true/false*` | pop cond, `ToBoolean`, branch | `JSValueConversion.ToBoolean` then adjust `pc` |
| Labels | Patched to offsets by compiler (`optimize_jumps`) | Tests patch raw offsets; compiler will emit offsets once implemented |

## Tests to Add (when implementing)

- `if/else` branches with truthy/falsy values
- `while`, `do/while`, and `for` loops (including `break`/`continue`)
- `switch` with fallthrough and `default`
- Short-circuit `&&`, `||`, `??`, and optional chaining
- Peephole jump size reductions (indirectly via bytecode inspection)

## Next Steps

With control flow jumps in place, the interpreter can run structured programs. Next we’ll add **function calls** (stack frames, `this` binding, and argument handling) in Step 6.5.
