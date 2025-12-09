# Step 6.3: Variables and Properties

## Overview

This step extends the bytecode interpreter with variable and property access operations. These are essential for executing JavaScript code that uses local variables, function arguments, closures, and object properties.

## Key Concepts

### Call Frame

A **CallFrame** represents the execution state for a single function call:

```
┌─────────────────────────────────────────────────────────┐
│ CallFrame                                               │
├─────────────────────────────────────────────────────────┤
│ locals[]      - Local variables declared in function    │
│ args[]        - Function parameters                     │
│ varRefs[]     - Closure variable references            │
│ thisValue     - The 'this' binding                      │
│ savedPC       - Return address (program counter)        │
│ savedSP       - Return stack pointer                    │
│ parent        - Caller's frame                          │
└─────────────────────────────────────────────────────────┘
```

### Variable Categories

JavaScript variables fall into several categories at runtime:

| Category | Storage | Example | Access Method |
|----------|---------|---------|---------------|
| Local | `locals[]` | `let x = 5` | GetLoc/PutLoc |
| Argument | `args[]` | `function(a)` | GetArg/PutArg |
| Captured | `varRefs[]` | Closure vars | GetVarRef/PutVarRef |
| Global | Global object | `console` | GetVar/PutVar |

### Temporal Dead Zone (TDZ)

`let` and `const` variables have a "temporal dead zone" - they exist but cannot be accessed until initialized:

```javascript
console.log(x); // ReferenceError: Cannot access 'x' before initialization
let x = 5;
```

This is enforced with `GetLocCheck`/`PutLocCheck` opcodes that verify the variable is initialized.

## Implementation

### CallFrame Class

```csharp
public sealed class CallFrame
{
    private readonly JSValue[] _locals;      // Local variable storage
    private readonly JSValue[] _args;        // Argument storage
    private readonly JSVarRef[] _varRefs;    // Closure variable refs
    private JSValue _thisValue;              // 'this' binding
    
    // Get/set local by index
    public JSValue GetLocal(int index);
    public bool SetLocal(int index, JSValue value);
    
    // Get/set argument by index
    public JSValue GetArg(int index);
    public bool SetArg(int index, JSValue value);
    
    // Get/set closure variable by index
    public JSValue GetVarRefValue(int index);
    public bool SetVarRefValue(int index, JSValue value);
}
```

### Variable Opcodes

| Opcode | Stack Effect | Description |
|--------|--------------|-------------|
| `GetLoc idx` | -> value | Push local variable |
| `PutLoc idx` | value -> | Pop and store to local |
| `SetLoc idx` | value -> value | Store to local, keep on stack |
| `GetArg idx` | -> value | Push argument |
| `PutArg idx` | value -> | Pop and store to argument |
| `SetArg idx` | value -> value | Store to arg, keep on stack |
| `GetVarRef idx` | -> value | Push closure variable |
| `PutVarRef idx` | value -> | Pop and store to closure var |
| `SetVarRef idx` | value -> value | Store to closure var, keep on stack |

### TDZ-Checking Opcodes

| Opcode | Description |
|--------|-------------|
| `GetLocCheck idx` | Get local, throw if uninitialized |
| `PutLocCheck idx` | Put local, throw if uninitialized |
| `PutLocCheckInit idx` | Initialize local (first assignment) |
| `SetLocUninitialized idx` | Mark local as uninitialized |
| `GetVarRefCheck idx` | Get var ref with TDZ check |
| `PutVarRefCheck idx` | Put var ref with TDZ check |

### Short Opcodes

For performance, common indices have dedicated opcodes:

```
GetLoc0, GetLoc1, GetLoc2, GetLoc3    // First 4 locals
PutLoc0, PutLoc1, PutLoc2, PutLoc3
GetArg0, GetArg1, GetArg2, GetArg3    // First 4 arguments
GetLoc8 idx                            // Single-byte index (0-255)
```

### Property Opcodes

| Opcode | Stack Effect | Description |
|--------|--------------|-------------|
| `GetField atom` | obj -> value | Get named property |
| `GetField2 atom` | obj -> obj value | Get property, keep object |
| `PutField atom` | obj value -> | Set named property |
| `DefineField atom` | obj value -> obj | Define property, keep object |
| `GetArrayEl` | obj index -> value | Get by computed index |
| `GetArrayEl2` | obj index -> obj value | Get element, keep object |
| `PutArrayEl` | obj index value -> | Set by computed index |

## Example: Variable Lifecycle

```javascript
function example(x) {    // x stored in args[0]
    let y = x + 1;       // y stored in locals[0]
    const z = y * 2;     // z stored in locals[1] (const)
    return z;
}
```

Bytecode:
```
0: GetArg 0           ; Push x
2: Push1              ; Push 1
3: Add                ; x + 1
4: PutLocCheckInit 0  ; Initialize y (first assignment)
7: GetLoc 0           ; Push y
9: Push2              ; Push 2
10: Mul               ; y * 2
11: PutLocCheckInit 1 ; Initialize z
14: GetLocCheck 1     ; Push z (with TDZ check)
17: Return            ; Return z
```

## Example: Closure

```javascript
function outer() {
    let x = 10;          // Captured by inner
    return function inner() {
        return x + 1;    // Access via VarRef
    };
}
```

When `inner` executes:
1. `x` is accessed via `GetVarRef 0`
2. VarRef points to outer's `locals[0]` (or detached value if outer returned)

## Example: Property Access

```javascript
let obj = { name: "test" };
obj.count = 42;
console.log(obj.name);
```

Bytecode:
```
; obj.count = 42
GetLoc 0              ; Push obj
Push 42
PutField "count"      ; obj.count = 42

; obj.name
GetLoc 0              ; Push obj
GetField "name"       ; Push obj.name
```

## Property Access Implementation

### Named Properties (GetField/PutField)

```csharp
private JSValue GetPropertyValue(JSValue obj, string propertyName)
{
    // Handle null/undefined
    if (obj.IsNull || obj.IsUndefined)
        throw TypeError($"Cannot read property '{propertyName}' of {obj}");
    
    // Object property access
    if (obj.IsObject)
        return obj.AsObject().Get(propertyName);
    
    // String.length
    if (obj.IsString && propertyName == "length")
        return JSValue.FromInt32(obj.ToString().Length);
    
    return JSValue.Undefined;
}
```

### Computed Properties (GetArrayEl/PutArrayEl)

Handles both numeric indices and string keys:

```csharp
private JSValue GetElementValue(JSValue obj, JSValue index)
{
    // Fast path: integer index
    if (index.IsInt && obj.IsObject)
    {
        int idx = index.ToInt32();
        if (idx >= 0)
            return obj.AsObject().Get((uint)idx);
    }
    
    // Convert to property key
    string key = JSValueConversion.ToString(index);
    return GetPropertyValue(obj, key);
}
```

## Files Changed

| File | Changes |
|------|---------|
| `CallFrame.cs` | New - Call frame with locals/args/varRefs |
| `Interpreter.cs` | +600 lines - Variable and property operations |
| `CallFrameTests.cs` | New - 25 tests for CallFrame |
| `InterpreterVariableTests.cs` | New - 99 tests for variable/property ops |

## Test Coverage

- **CallFrame**: Constructor variations, local/arg/varRef access
- **Variable ops**: GetLoc/PutLoc/SetLoc, GetArg/PutArg, GetVarRef/PutVarRef
- **TDZ checks**: Uninitialized access detection, initialization
- **Property ops**: GetField/PutField, GetArrayEl/PutArrayEl
- **Edge cases**: Null/undefined access, prototype chain, string indexing

## Next Steps

**Step 6.4: Control Flow**
- Jump opcodes (Goto, IfTrue, IfFalse)
- Loop execution
- Exception handling with try/catch
