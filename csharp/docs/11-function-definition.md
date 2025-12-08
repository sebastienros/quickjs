# Step 3.4: Function Definition (JSFunctionDef)

## Overview

`JSFunctionDef` is the central compilation context used during JavaScript function parsing and bytecode generation. Every function in JavaScript—from the global script to arrow functions, methods, generators, and async functions—gets its own `JSFunctionDef` instance during compilation.

This step introduces the data structures that track everything needed to compile a JavaScript function: its variables, arguments, scopes, closures, nested functions, and the bytecode being generated.

## How JavaScript Functions Work

### Every Script is a Function

In JavaScript engines, even your top-level code is wrapped in an implicit function:

```javascript
// This code:
var x = 1;
console.log(x);

// Is internally treated like:
(function() {
  var x = 1;
  console.log(x);
})();
```

This is why `JSFunctionDef` is the central compilation unit—it handles everything from global scripts to the smallest arrow function.

### Variable Scoping in JavaScript

JavaScript has two fundamentally different scoping mechanisms:

**Function-scoped variables (`var`):**
```javascript
function example() {
  if (true) {
    var x = 1;  // Hoisted to function scope
  }
  console.log(x); // Works! x is accessible
}
```

**Block-scoped variables (`let`, `const`):**
```javascript
function example() {
  if (true) {
    let y = 1;  // Only in this block
  }
  console.log(y); // ReferenceError!
}
```

Our `JSVarDef` structure tracks this distinction with the `IsLexical` flag.

### Closures: The Heart of JavaScript

Closures are JavaScript's most powerful feature. When an inner function references a variable from an outer function, that variable must be "captured":

```javascript
function outer() {
  let counter = 0;
  return function inner() {
    counter++;        // References outer's variable
    return counter;
  };
}
const fn = outer();
fn(); // 1
fn(); // 2
```

The `counter` variable lives on even after `outer()` returns because `inner()` "closes over" it. Our `JSClosureVar` structure tracks which variables are captured and how to access them at runtime.

## QuickJS Structure Reference

From QuickJS `quickjs.c` (lines 21365-21520):

```c
typedef struct JSFunctionDef {
    JSContext *ctx;
    JSFunctionDef *parent;        // Enclosing function
    int parent_cpool_idx;         // Index in parent's constant pool
    
    JSAtom func_name;
    JSFunctionKindEnum func_kind;
    
    // Bytecode generation
    DynBuf byte_code;             // ByteCodeBuffer
    
    // Variables  
    JSVarDef *vars;               // Function-level variables
    JSVarDef *args;               // Function arguments
    JSClosureVar *closure_var;    // Captured variables
    
    // Lexical scopes (for let/const)
    JSVarScope *scopes;
    int scope_level;
    int scope_first;
    
    // Constant pool
    JSValue *cpool;               // ConstantPool
    
    // Labels for jumps
    LabelSlot *label_slots;
    
    // Flags
    BOOL has_eval_call;
    BOOL has_arguments_binding;
    BOOL is_arrow_function;
    // ... many more
} JSFunctionDef;
```

## Implementation

### Variable Definition (`JSVarDef`)

Tracks a single variable or argument declaration:

```csharp
public readonly struct JSVarDef
{
    public JSAtom Name { get; init; }
    public JSVarKind Kind { get; init; }
    public int ScopeLevel { get; init; }
    public int ScopeNext { get; init; }
    public bool IsConst { get; init; }
    public bool IsLexical { get; init; }
    public bool IsCaptured { get; init; }
}
```

**Key fields:**
- `Kind`: Distinguishes regular variables from function declarations, catch bindings, etc.
- `ScopeLevel`: For lexical variables, indicates which block scope they belong to
- `ScopeNext`: Links variables in the same scope (forming a linked list)
- `IsLexical`: True for `let`/`const`, false for `var`
- `IsCaptured`: True if referenced by a nested function (becomes a closure variable)

### Variable Kinds (`JSVarKind`)

JavaScript has several special variable binding types:

```csharp
public enum JSVarKind : byte
{
    Normal,              // Regular var/let/const
    FunctionDecl,        // function foo() {}
    NewFunctionDecl,     // Hoisted function at block level
    Catch,               // catch (e) binding
    FunctionName,        // Named function expression's name
    PrivateField,        // #privateField
    PrivateMethod,       // #privateMethod()
    PrivateGetter,       // get #prop()
    PrivateSetter,       // set #prop()
}
```

### Closure Variables (`JSClosureVar`)

When a nested function references an outer variable, we create a closure entry:

```csharp
public readonly struct JSClosureVar
{
    public JSAtom Name { get; init; }
    public JSClosureType Type { get; init; }
    public int VarIndex { get; init; }
    public bool IsArg { get; init; }
    public bool IsConst { get; init; }
    public bool IsLexical { get; init; }
}
```

**Closure types:**
```csharp
public enum JSClosureType : byte
{
    Local,         // From parent's local variables
    Arg,           // From parent's arguments
    ParentLocal,   // Already a closure in parent (transitive)
    ParentArg,     // Argument that's a closure in parent
    ModuleDecl,    // Module-level declaration
    ModuleImport,  // Imported binding
}
```

### Scope Chain (`JSVarScope`)

Lexical scopes form a chain for `let`/`const` lookups:

```csharp
public struct JSVarScope
{
    public int Parent;  // Index of enclosing scope (-1 if none)
    public int First;   // First variable in this scope (-1 if empty)
}
```

### Function Definition (`JSFunctionDef`)

The complete compilation context:

```csharp
public class JSFunctionDef
{
    // Identity
    public JSAtom FunctionName { get; set; }
    public JSFunctionKind FunctionKind { get; set; }
    
    // Hierarchy (functions can be nested)
    public JSFunctionDef? Parent { get; set; }
    public List<JSFunctionDef> Children { get; }
    
    // Variable tracking
    public List<JSVarDef> Vars { get; }      // Local variables
    public List<JSVarDef> Args { get; }      // Arguments
    public List<JSClosureVar> ClosureVars { get; }  // Captured variables
    
    // Scope management
    public List<JSVarScope> Scopes { get; }
    public int ScopeLevel { get; set; }
    
    // Bytecode generation (from previous steps)
    public ByteCodeBuffer ByteCode { get; }
    public ConstantPool ConstantPool { get; }
    public List<LabelInfo> Labels { get; }
    
    // JavaScript semantics flags
    public bool HasArgumentsBinding { get; set; }
    public bool HasThisBinding { get; set; }
    public bool IsArrowFunction { get; set; }
    public bool IsAsync { get; set; }
    public bool IsGenerator { get; set; }
    // ... more flags
}
```

## How Compilation Uses JSFunctionDef

### Step 1: Create Function Context

When the parser encounters a function:

```javascript
function greet(name) {
  let message = "Hello, " + name;
  return message;
}
```

A new `JSFunctionDef` is created:

```csharp
var fd = new JSFunctionDef
{
    FunctionName = atoms.GetAtom("greet"),
    FunctionKind = JSFunctionKind.Normal,
    HasThisBinding = true,
};
```

### Step 2: Parse Arguments

Arguments are added in order:

```csharp
fd.AddArg(atoms.GetAtom("name")); // index 0
```

### Step 3: Parse Body and Track Variables

As the parser encounters declarations:

```csharp
// For: let message = ...
fd.AddVar(atoms.GetAtom("message"), isLexical: true);
```

### Step 4: Generate Bytecode

Using the components from previous steps:

```csharp
fd.EmitOp(OpCode.GetArg);
fd.EmitU16(0); // argument index

fd.EmitOp(OpCode.Push);
fd.EmitAtom(atoms.GetAtom("Hello, "));

fd.EmitOp(OpCode.Add);

fd.EmitOp(OpCode.PutVar);
fd.EmitU16(0); // variable index
```

### Step 5: Handle Closures

When a nested function references an outer variable:

```javascript
function outer() {
  let x = 1;
  function inner() { return x; }
  return inner;
}
```

```csharp
// When parsing inner(), x is found in outer's variables
int closureIdx = innerFd.AddClosureVar(
    atoms.GetAtom("x"),
    JSClosureType.Local,
    varIndex: 0
);

// Mark x as captured in outer
outerFd.Vars[0] = outerFd.Vars[0] with { IsCaptured = true };
```

## JavaScript Semantics Flags

### `HasArgumentsBinding`

```javascript
function f() {
  console.log(arguments[0]); // Uses arguments object
}
```

When `arguments` is referenced, this flag is set and the runtime creates the arguments object.

### `HasThisBinding`

Regular functions have their own `this`:
```javascript
function f() { return this; } // Has this binding
const arrow = () => this;     // Uses enclosing this
```

Arrow functions inherit `this` from their lexical scope.

### `IsArrowFunction`, `IsAsync`, `IsGenerator`

These affect how the function is called and what operations are valid inside:

```javascript
// Arrow: no this, no arguments, no new
const arrow = () => {};

// Async: can await, returns Promise
async function f() { await x; }

// Generator: can yield, returns Iterator
function* g() { yield 1; }

// Async Generator: both
async function* ag() { yield await x; }
```

## File Structure

```
src/QuickJS.Core/
├── JSVarDef.cs         # Variable and closure structs
├── JSFunctionDef.cs    # Main compilation context
├── ByteCodeBuffer.cs   # Bytecode emission (Step 3.2)
├── ConstantPool.cs     # Constant storage (Step 3.3)
└── OpCode.cs           # Bytecode operations (Step 3.1)
```

## Tests

The test suite covers:

1. **Function creation and properties** - Names, kinds, types
2. **Variable management** - Adding vars, lexical vs function scope
3. **Argument handling** - Adding and tracking arguments
4. **Closure variables** - Capturing outer variables
5. **Scope chains** - Push/pop lexical scopes
6. **Label management** - For control flow
7. **Bytecode integration** - Emitting through the function context
8. **Constant pool integration** - Adding constants through the function context
9. **Parent-child relationships** - Nested functions

## Why This Design?

### Single-Pass Compilation

QuickJS compiles JavaScript in a single pass without building a full AST. This means:

1. Variables are discovered as they're parsed
2. Closures are resolved when the function definition ends
3. Bytecode is generated directly during parsing

`JSFunctionDef` accumulates all this information as parsing proceeds.

### Memory Efficiency

Using indices instead of pointers:
- `ScopeLevel` is an index, not a pointer
- `VarIndex` references into a list
- `Parent` is a reference, but `ParentConstantPoolIndex` is an index

This matches QuickJS's approach and makes serialization straightforward.

### Incremental Complexity

Each step builds on previous ones:
- **OpCode** (3.1): What operations exist
- **ByteCodeBuffer** (3.2): How to write bytecode
- **ConstantPool** (3.3): Where to store constants
- **JSFunctionDef** (3.4): The context that ties it all together

## Next Steps

With `JSFunctionDef` in place, we can:

1. **Implement the parser** - Create `JSFunctionDef` instances as functions are parsed
2. **Add variable resolution** - Look up variables in the scope chain
3. **Handle closures** - Detect and record captured variables
4. **Generate bytecode** - Compile expressions and statements

## Summary

`JSFunctionDef` is the compilation context for JavaScript functions. It tracks:

- **Variables**: Function-scoped (`var`) and block-scoped (`let`/`const`)
- **Arguments**: Formal parameters
- **Closures**: Variables captured from outer functions
- **Scopes**: Lexical block scopes for `let`/`const`
- **Bytecode**: The generated instructions
- **Constants**: Literals and atom references
- **Labels**: Jump targets for control flow

This structure enables QuickJS's efficient single-pass compilation of JavaScript to bytecode.
