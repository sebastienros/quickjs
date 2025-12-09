# Converting QuickJS to C#

This document provides guidance on converting the QuickJS JavaScript engine from C to C#.

> **Tutorial Project**: This conversion serves as a step-by-step tutorial for implementing a JavaScript engine. Each step corresponds to a **git commit** and includes detailed documentation explaining the concepts, design decisions, and implementation details. The plan may evolve as the implementation progresses.

## Target Frameworks

The implementation targets multiple .NET versions for broad compatibility:

| Target | Purpose |
|--------|---------|
| `netstandard2.0` | .NET Framework 4.8.1, Unity, Xamarin, older .NET Core |
| `net8.0` | Current LTS release with modern APIs |
| `net10.0` | Latest features and performance improvements |

### Compatibility Considerations

When targeting `netstandard2.0`, some modern C# features require polyfills or alternative implementations:

- **Span\<T\> / ReadOnlySpan\<T\>**: Available via `System.Memory` NuGet package
- **Index/Range operators**: Require polyfills or conditional compilation
- **Init-only setters**: Not available, use constructors
- **Records**: Not available, use classes with manual equality
- **Pattern matching**: Limited support, use traditional patterns
- **Default interface methods**: Not available

Use `#if` directives when needed:
```csharp
#if NET8_0_OR_GREATER
    // Modern implementation using Span<T> directly
#else
    // Fallback for netstandard2.0
#endif
```

## How to Follow This Tutorial

1. **Each step = One commit**: Every step in the plan results in a single, focused git commit
2. **Documentation-driven**: Each step includes explanations of *what* is being built and *why*
3. **Incremental complexity**: Concepts build upon each other progressively
4. **Working code at each step**: Every commit should result in compiling, testable code
5. **Learn by doing**: Follow along by implementing each step yourself, or study the commits

## Current Progress

| Phase | Status | Steps | Tests |
|-------|--------|-------|-------|
| Phase 1: Core Types | ✅ Complete | 5/5 | ~50 |
| Phase 2: Lexer | ✅ Complete | 2/2 | ~100 |
| Phase 3: Bytecode | ✅ Complete | 5/5 | ~120 |
| Phase 4: Parser | ✅ Complete | 8/8 | ~2000 |
| Phase 5: Runtime Objects | ✅ Complete | 4/4 | ~500 |
| Phase 6: VM & Interpreter | 🔄 In Progress | 3/7 | ~684 |

**Total Tests: 3634** (all passing, 0 skipped)

**Latest Commit**: `step-6.4-interpreter-control`

# Detailed Implementation Plan

> **Tutorial Format**: Each step below corresponds to a **single git commit** with its own documentation file in the `docs/` folder. The documentation explains the *what*, *why*, and *how* of each implementation step. This plan may be adjusted as the implementation progresses and new insights emerge.

## Commit Naming Convention

Each commit follows the pattern: `step-X.Y-short-description`

Example: `step-1.1-create-solution`, `step-2.3-lexer-numbers`

---

## Phase 1: Project Setup & Core Types

### Step 1.1: Create Solution Structure
**Commit**: `step-1.1-create-solution`  
**Doc**: `docs/01-project-setup.md`

- [x] Create `QuickJS.NET.sln` solution
- [x] Create `QuickJS.Core` class library project (multi-targeting)
- [x] Configure target frameworks: `netstandard2.0`, `net8.0`, `net10.0`
- [x] Create `QuickJS.Tests` xUnit test project
- [x] Set up project references and `.gitignore`
- [x] Add initial README with project goals

**Target Frameworks**:
- `netstandard2.0` - For .NET Framework 4.8.1 and broad compatibility
- `net8.0` - Current LTS release
- `net10.0` - Latest/preview support

**Learning Goals**: .NET solution structure, multi-targeting, project references, test project setup

---

### Step 1.2: Define JSValueType Enum ✅
**Commit**: `step-1.2-jsvalue-type`  
**Doc**: `docs/02-value-types.md`

- [x] Study how QuickJS represents values in `quickjs.h` (`JS_TAG_*` constants)
- [x] Implement `JSValueType` enum mirroring QuickJS tags
- [x] Document the design decision: tagged values vs object hierarchy
- [x] Explain JavaScript's dynamic typing model

**Learning Goals**: JavaScript type system, tagged union concept

---

### Step 1.3: Implement JSValue Struct ✅
**Commit**: `step-1.3-jsvalue-struct`  
**Doc**: `docs/02-value-types.md` (continued)

- [x] Implement `JSValue` struct with tag and payload
- [x] Add factory methods: `JSValue.Undefined`, `JSValue.Null`, `JSValue.FromInt32()`, etc.
- [x] Add type checking properties: `IsUndefined`, `IsNull`, `IsNumber`, etc.
- [x] Add conversion methods: `ToInt32()`, `ToDouble()`, `ToBoolean()`
- [x] Write comprehensive unit tests

**Learning Goals**: C# structs, value semantics, factory pattern

---

### Step 1.4: Implement Atom Table ✅
**Commit**: `step-1.4-atom-table`  
**Doc**: `docs/04-atom-table.md`

- [x] Study `quickjs-atom.h` to understand atom concept
- [x] Create `JSAtom` struct (lightweight identifier)
- [x] Create `AtomTable` class for string interning
- [x] Pre-populate with built-in atoms (keywords, common property names)
- [x] Write tests for atom creation and lookup

**Learning Goals**: String interning, hash tables, memory efficiency

---

### Step 1.5: Create JSException Class ✅
**Commit**: `step-1.5-exceptions`  
**Doc**: `docs/05-exception-hierarchy.md`

- [x] Create `JSException` base class
- [x] Create derived types: `JSSyntaxError`, `JSTypeError`, `JSReferenceError`, etc.
- [x] Include source location tracking (SourceLocation struct)
- [x] Design for stack trace support (JSStackFrame, JSStackTrace)

**Learning Goals**: Exception design, JavaScript error types

---

## Phase 2: Lexer/Tokenizer

### Step 2.1: Token Definitions ✅
**Commit**: `step-2.1-token-types`  
**Doc**: `docs/06-token-definitions.md`

- [x] Define `TokenType` enum (all JavaScript tokens)
- [x] Define `Token` class with type, value, and location
- [x] SourceLocation already defined in Step 1.5
- [x] Document JavaScript's lexical grammar

**Learning Goals**: Lexical analysis concepts, token classification

---

### Step 2.2: Complete Lexer Implementation ✅
**Commit**: `step-2.2-lexer`  
**Doc**: `docs/07-lexer-implementation.md`

- [x] Create `Lexer` class with string input
- [x] Implement identifier scanning with Unicode support
- [x] Implement keyword recognition (40+ keywords)
- [x] Parse all number formats (decimal, hex, binary, octal, scientific, BigInt)
- [x] Parse string literals with escape sequences
- [x] Parse template literals
- [x] Parse all operators and punctuation
- [x] Handle comments (single-line and multi-line)
- [x] Track line/column positions for error reporting
- [x] Track line terminators for ASI support
- [x] Comprehensive lexer tests

**Learning Goals**: State machine pattern, Unicode handling, escape sequences

---

## Phase 3: Bytecode & Virtual Machine

> **Note**: QuickJS uses a one-pass bytecode compiler rather than building an AST.
> This is more efficient and is how production JavaScript engines typically work.
> We follow this approach to stay true to the original QuickJS design.

### Step 3.1: OpCode Definitions ✅
**Commit**: `step-3.1-opcodes`  
**Doc**: `docs/08-opcode-definitions.md`

- [x] Create `OpCode` enum with all bytecode instructions (~262 opcodes)
- [x] Create `OpCodeFormat` enum for operand encoding formats
- [x] Create `OpCodeInfo` struct with size, stack effects, format
- [x] Create `OpCodes` static class for metadata lookup
- [x] Comprehensive opcode tests

**Learning Goals**: Stack-based VM design, bytecode instruction sets

---

### Step 3.2: Bytecode Buffer ✅
**Commit**: `step-3.2-bytecode-buffer`  
**Doc**: `docs/09-bytecode-buffer.md`

- [x] Create `ByteCodeBuffer` class (DynBuf equivalent)
- [x] Emit opcodes with various operand formats
- [x] Handle label placeholders for jumps
- [x] Implement bytecode patching for forward jumps
- [x] Create bytecode reader for verification

**Learning Goals**: Bytecode emission, jump patching

---

### Step 3.3: Constant Pool ✅
**Commit**: `step-3.3-constant-pool`  
**Doc**: `docs/10-constant-pool.md`

- [x] Create `ConstantPool` class for bytecode constants
- [x] Support integers, doubles, strings, atoms
- [x] Deduplicate constants for efficiency
- [x] Comprehensive constant pool tests

**Learning Goals**: Constant pool design, value deduplication

---

### Step 3.4: Function Definition ✅
**Commit**: `step-3.4-function-definition`  
**Doc**: `docs/11-function-definition.md`

- [x] Create `JSFunctionDef` class (JSFunctionDef equivalent)
- [x] Manage local variables and arguments
- [x] Manage constant pool (strings, numbers, nested functions)
- [x] Handle lexical scopes
- [x] Track closure variables

**Learning Goals**: Compilation context, symbol tables

---

### Step 3.5: Line Number Table ✅
**Commit**: `step-3.5-line-number-table`  
**Doc**: `docs/12-line-number-table.md`

- [x] Create `LineNumberTable` for debugging
- [x] Track bytecode offset to source line mapping
- [x] Support debug information generation

**Learning Goals**: Debug information, source mapping

---

## Phase 4: Parser & Compiler

> **Note**: The parser directly emits bytecode during parsing (one-pass compilation).

### Step 4.1: Expression Parser ✅
**Commit**: `step-4.1-expression-parser`  
**Doc**: `docs/13-parser.md`

- [x] Create `Parser` class consuming lexer tokens
- [x] Integrate with `JSFunctionDef` for bytecode emission
- [x] Implement token management (current, peek, advance)
- [x] Implement expect/match helpers
- [x] Parse literals (number, string, boolean, null, undefined)
- [x] Parse identifiers
- [x] Parse parenthesized expressions
- [x] Parse array literals
- [x] Parse object literals
- [x] Implement Pratt parser / precedence climbing
- [x] Parse unary operators
- [x] Parse binary operators with correct precedence
- [x] Parse ternary conditional
- [x] Parse assignment operators
- [x] Parse property access (dot notation)
- [x] Parse computed property access (bracket notation)
- [x] Parse function calls
- [x] Parse `new` expressions
- [x] Parse optional chaining (`?.`)
- [x] Comprehensive expression tests

**Learning Goals**: Recursive descent parsing, Pratt parsing, operator precedence

---

### Step 4.2: Statement Parser ✅
**Commit**: `step-4.2-statement-parser`  
**Doc**: `docs/14-statement-parser.md`

- [x] Parse expression statements
- [x] Parse block statements
- [x] Parse variable declarations (var, let, const)
- [x] Parse if/else statements
- [x] Parse for/while/do-while loops
- [x] Parse for-in/for-of loops
- [x] Parse switch statements
- [x] Parse try/catch/finally
- [x] Parse return/break/continue/throw
- [x] Comprehensive statement tests

**Learning Goals**: Statement parsing patterns, control flow

---

### Step 4.3: Function Parser ✅
**Commit**: `step-4.3-function-parser`  
**Doc**: `docs/15-function-parser.md`

- [x] Parse function declarations
- [x] Parse function expressions
- [x] Parse parameter lists (with defaults, rest)
- [x] Parse function bodies
- [x] Parse generator functions (function*)
- [x] Parse async functions
- [x] Nested function handling
- [x] Comprehensive function tests

**Learning Goals**: Function syntax variations, generators, async

---

### Step 4.4: Complete Expression Parser ✅
**Commit**: `step-4.4-complete-expression-parser`  
**Doc**: `docs/16-complete-expression-parser.md`

- [x] Parse arrow functions (with lookahead disambiguation)
- [x] Parse yield expressions (in generators)
- [x] Parse await expressions (in async functions)
- [x] Parse super expressions (super(), super.prop, super[expr])
- [x] Add lexer position save/restore for lookahead
- [x] Add PeekToken() and SkipParensToken() for arrow detection
- [x] Enable all arrow function test cases
- [x] Comprehensive expression tests

**Learning Goals**: Arrow function parsing, lookahead, context-sensitive parsing

---

### Step 4.5: Class Parser ✅
**Commit**: `step-4.5-class-parser`  
**Doc**: `docs/17-class-parser.md`

- [x] Parse class declarations
- [x] Parse class expressions
- [x] Parse constructor method
- [x] Parse instance and static methods
- [x] Parse getter/setter accessors
- [x] Parse field declarations
- [x] Parse private members (#private)
- [x] Parse computed property names
- [x] Parse async and generator methods
- [x] Parse static initialization blocks
- [x] Handle keywords as method names (get, set, static, async)
- [x] Write class tests (44 test methods)

**Learning Goals**: Class syntax, prototype chain setup, lookahead patterns

---

### Step 4.6: Module Parser ✅
**Commit**: `step-4.6-module-parser`  
**Doc**: `docs/18-module-parser.md`

- [x] Parse import declarations
- [x] Parse export declarations
- [x] Handle module vs script mode
- [x] Write module tests (68 tests)

**Learning Goals**: ES module system

---

### Step 4.7: Destructuring Parser ✅
**Commit**: `step-4.7-destructuring-parser`  
**Doc**: `docs/19-destructuring-parser.md`

- [x] Parse array destructuring patterns
- [x] Parse object destructuring patterns
- [x] Parse destructuring in variable declarations
- [x] Parse destructuring in function parameters
- [x] Parse destructuring in arrow functions
- [x] Write destructuring tests (49 tests)

**Learning Goals**: Pattern matching, destructuring assignment

---

### Step 4.8: Parser Error Recovery ✅
**Commit**: `step-4.8-parser-errors`  
**Doc**: `docs/20-error-recovery.md`

- [x] Implement diagnostic collection infrastructure
- [x] Create `ParseDiagnostic` class with severity, code, message, location
- [x] Create `DiagnosticBag` for collecting multiple diagnostics
- [x] Define error codes organized by category (JS1xxx-JS8xxx)
- [x] Create `ParserErrorMessages` for consistent formatting
- [x] Integrate diagnostics into Parser class
- [x] Write 26 diagnostic tests

**Learning Goals**: Error recovery strategies, diagnostic collection, error formatting

---

## Phase 5: Runtime Objects

### Step 5.1: JSObject Implementation ✅
**Commit**: `step-5.1-jsobject`  
**Doc**: `docs/21-jsobject.md`

- [x] Implement `PropertyDescriptor` class with PropertyFlags
- [x] Implement `JSClassId` enum with built-in class IDs
- [x] Implement `JSObject` class with property storage
- [x] Implement prototype chain lookup
- [x] Implement extensibility control (PreventExtensions, Seal, Freeze)
- [x] Add `JSValue.FromObject`, `TryGetObject`, `AsObject`
- [x] Write comprehensive object tests

**Learning Goals**: JavaScript object model, property descriptors, prototype chain

---

### Step 5.2: Prototype Chain ✅
**Commit**: `step-5.2-prototypes`  
**Doc**: `docs/22-prototype-chain.md`

- [x] Implement SetPrototype with validation and cycle detection
- [x] Implement SetPrototypeOrThrow for error handling
- [x] Implement Object.create with optional properties
- [x] Implement Object.assign for copying properties
- [x] Implement Object.keys/values/entries/fromEntries
- [x] Add immutable prototype flag for built-in objects
- [x] Write comprehensive prototype tests (40 tests)

**Learning Goals**: Prototype-based inheritance, Object static methods

---

### Step 5.3: JSFunction Runtime ✅
**Commit**: `step-5.3-jsfunction`  
**Doc**: `docs/23-jsfunction.md`

- [x] Create `JSVarRef` class for closure variable references
- [x] Create `JSFunction` class wrapping bytecode
- [x] Implement native C# function support (JSCFunction delegate)
- [x] Implement closures (captured variables)
- [x] Implement bound functions (Function.prototype.bind)
- [x] Write function runtime tests (144 new tests)

**Learning Goals**: Function objects, closures, bound functions

---

### Step 5.4: Array Object ✅
**Commit**: `step-5.4-array-object`  
**Doc**: `docs/24-array-object.md`

- [x] Implement JSArray with indexed properties
- [x] Implement length property behavior
- [x] Implement array methods (push, pop, slice, concat, etc.)
- [x] Implement array iteration (IEnumerable<JSValue>)
- [x] Write array tests (87 new tests)

**Learning Goals**: Array implementation, exotic objects, fast array optimization

---

## Phase 6: Virtual Machine & Interpreter

### Step 6.1: Runtime Infrastructure ✅
**Commit**: `step-6.1-runtime-setup`  
**Doc**: `docs/25-runtime-architecture.md`

- [x] Implement `JSRuntime` class
- [x] Implement `JSContext` class
- [x] Design memory limits and quotas
- [x] Document runtime vs context distinction

**Learning Goals**: Runtime architecture

---

### Step 6.2: Interpreter Loop - Basics ✅
**Commit**: `step-6.2-interpreter-basic`  
**Doc**: `docs/26-interpreter-loop.md`

- [x] Create `Interpreter` class
- [x] Implement basic stack operations
- [x] Implement arithmetic opcodes
- [x] Implement comparison opcodes
- [x] Create `JSValueConversion` class for type coercion
- [x] Execute simple expressions
- [x] Write interpreter tests (~358 new tests)

**Learning Goals**: Bytecode interpretation, VM stacks

---

### Step 6.3: Interpreter - Variables and Properties
**Commit**: `step-6.3-variables-properties`  
**Doc**: `docs/27-variables-properties.md`

- [x] Create `CallFrame` class for call-level state management
- [x] Implement variable load/store opcodes (GetLocal, PutLocal, GetArg, PutArg, GetVarRef, PutVarRef)
- [x] Implement property get/set opcodes (GetField, PutField, GetArrayEl, PutArrayEl)
- [x] Extend `Execute()` method to handle opcodes with operands
- [x] Write variable and property tests (~124 new tests)

**Learning Goals**: Variable resolution at runtime, property access

---

### Step 6.4: Interpreter - Control Flow
**Commit**: `step-6.4-interpreter-control`  
**Doc**: `docs/28-interpreter-control-flow.md`

- [ ] Implement jump opcodes
- [ ] Implement conditional jumps
- [ ] Execute if/else and loops
- [ ] Write control flow tests

**Learning Goals**: PC manipulation, loop execution

---

### Step 6.5: Interpreter - Function Calls
**Commit**: `step-6.5-interpreter-calls`  
**Doc**: `docs/29-function-calls.md`

- [ ] Implement call stack frames
- [ ] Implement call/return opcodes
- [ ] Handle arguments object
- [ ] Handle `this` binding
- [ ] Write function call tests

**Learning Goals**: Call stack management, `this` binding

---

### Step 6.6: Interpreter - Closures
**Commit**: `step-6.6-interpreter-closures`  
**Doc**: `docs/30-closures.md`

- [ ] Implement upvalue handling
- [ ] Implement closure creation
- [ ] Handle upvalue closing
- [ ] Write closure tests

**Learning Goals**: Closure implementation

---

### Step 6.7: Interpreter - Exceptions
**Commit**: `step-6.7-interpreter-exceptions`  
**Doc**: `docs/31-exception-handling-runtime.md`

- [ ] Implement exception tables lookup
- [ ] Implement stack unwinding
- [ ] Execute try/catch/finally
- [ ] Write exception tests

**Learning Goals**: Exception propagation

---

## Phase 7: Built-in Objects

### Step 7.1: Object and Function Constructors
**Commit**: `step-7.1-object-function`  
**Doc**: `docs/40-builtin-object-function.md`

- [ ] Implement `Object` constructor and methods
- [ ] Implement `Function` constructor and methods
- [ ] Write built-in tests

---

### Step 7.2: Error Objects
**Commit**: `step-7.2-error-objects`  
**Doc**: `docs/41-error-objects.md`

- [ ] Implement Error hierarchy
- [ ] Implement stack traces
- [ ] Write error tests

---

### Step 7.3: Number and Math
**Commit**: `step-7.3-number-math`  
**Doc**: `docs/42-number-math.md`

- [ ] Implement Number constructor and methods
- [ ] Implement Math object
- [ ] Write math tests

---

### Step 7.4: String
**Commit**: `step-7.4-string`  
**Doc**: `docs/43-string.md`

- [ ] Implement String constructor
- [ ] Implement all String.prototype methods
- [ ] Write string tests

---

### Step 7.5: Array
**Commit**: `step-7.5-array`  
**Doc**: `docs/44-array.md`

- [ ] Implement Array constructor
- [ ] Implement array methods (map, filter, reduce, etc.)
- [ ] Implement iterators
- [ ] Write array tests

---

### Step 7.6: RegExp
**Commit**: `step-7.6-regexp`  
**Doc**: `docs/45-regexp.md`

- [ ] Decide: port libregexp or wrap .NET regex
- [ ] Implement RegExp constructor
- [ ] Implement RegExp methods
- [ ] Write regexp tests

---

### Step 7.7: JSON
**Commit**: `step-7.7-json`  
**Doc**: `docs/46-json.md`

- [ ] Implement JSON.parse
- [ ] Implement JSON.stringify
- [ ] Write JSON tests

---

### Step 7.8: Promise
**Commit**: `step-7.8-promise`  
**Doc**: `docs/47-promises.md`

- [ ] Implement Promise constructor
- [ ] Implement microtask queue
- [ ] Implement then/catch/finally
- [ ] Implement Promise.all, race, etc.
- [ ] Write promise tests

---

### Step 7.9: Map, Set, WeakMap, WeakSet
**Commit**: `step-7.9-collections`  
**Doc**: `docs/48-collections.md`

- [ ] Implement Map and Set
- [ ] Implement WeakMap and WeakSet
- [ ] Write collection tests

---

### Step 7.10: TypedArrays and ArrayBuffer
**Commit**: `step-7.10-typed-arrays`  
**Doc**: `docs/49-typed-arrays.md`

- [ ] Implement ArrayBuffer
- [ ] Implement DataView
- [ ] Implement TypedArray variants
- [ ] Write typed array tests

---

### Step 7.11: Date
**Commit**: `step-7.11-date`  
**Doc**: `docs/50-date.md`

- [ ] Implement Date constructor
- [ ] Implement Date methods
- [ ] Write date tests

---

### Step 7.12: Symbol and Reflect
**Commit**: `step-7.12-symbol-reflect`  
**Doc**: `docs/51-symbol-reflect.md`

- [ ] Implement Symbol
- [ ] Implement well-known symbols
- [ ] Implement Reflect methods
- [ ] Write symbol/reflect tests

---

### Step 7.13: Proxy
**Commit**: `step-7.13-proxy`  
**Doc**: `docs/52-proxy.md`

- [ ] Implement Proxy constructor
- [ ] Implement all trap handlers
- [ ] Write proxy tests

---

## Phase 8: Standard Library

### Step 8.1: Console
**Commit**: `step-8.1-console`  
**Doc**: `docs/53-console.md`

- [ ] Implement console.log, warn, error, etc.
- [ ] Implement console.time/timeEnd
- [ ] Write console tests

---

### Step 8.2: Timers and Event Loop
**Commit**: `step-8.2-timers`  
**Doc**: `docs/54-event-loop.md`

- [ ] Implement setTimeout/clearTimeout
- [ ] Implement setInterval/clearInterval
- [ ] Implement event loop
- [ ] Write timer tests

---

### Step 8.3: Module Loader
**Commit**: `step-8.3-modules`  
**Doc**: `docs/55-module-loader.md`

- [ ] Implement ES module loading
- [ ] Implement dynamic import()
- [ ] Write module tests

---

## Phase 9: Advanced Features

### Step 9.1: Eval and Function Constructor
**Commit**: `step-9.1-eval`  
**Doc**: `docs/56-eval.md`

- [ ] Implement eval()
- [ ] Implement new Function()
- [ ] Write eval tests

---

### Step 9.2: Generators
**Commit**: `step-9.2-generators`  
**Doc**: `docs/57-generators.md`

- [ ] Implement generator functions
- [ ] Implement yield/yield*
- [ ] Write generator tests

---

### Step 9.3: Async/Await
**Commit**: `step-9.3-async-await`  
**Doc**: `docs/58-async-await.md`

- [ ] Implement async functions
- [ ] Implement await
- [ ] Implement async iterators
- [ ] Write async tests

---

### Step 9.4: BigInt
**Commit**: `step-9.4-bigint`  
**Doc**: `docs/59-bigint.md`

- [ ] Implement BigInt type
- [ ] Implement BigInt operations
- [ ] Write BigInt tests

---

## Phase 10: Integration & Polish

### Step 10.1: Public API Design
**Commit**: `step-10.1-public-api`  
**Doc**: `docs/60-public-api.md`

- [ ] Design clean public API
- [ ] Add XML documentation
- [ ] Write API usage examples

---

### Step 10.2: C# Interop
**Commit**: `step-10.2-interop`  
**Doc**: `docs/61-csharp-interop.md`

- [ ] Expose C# objects to JavaScript
- [ ] Call C# methods from JavaScript
- [ ] Write interop examples

---

### Step 10.3: Performance Optimization
**Commit**: `step-10.3-performance`  
**Doc**: `docs/62-performance.md`

- [ ] Profile hot paths
- [ ] Optimize interpreter loop
- [ ] Add inline caching (optional)

---

### Step 10.4: CLI Tool
**Commit**: `step-10.4-cli`  
**Doc**: `docs/63-cli-tool.md`

- [ ] Create qjs CLI equivalent
- [ ] Implement REPL
- [ ] Write CLI documentation

---

## Milestones Summary

| Milestone | Phases | Steps | Key Deliverable |
|-----------|--------|-------|-----------------|
| M1: Foundation | 1-2 | 1.1 - 2.9 | Core types + Complete Lexer |
| M2: Parsing | 3-4 | 3.1 - 4.9 | AST + Complete Parser |
| M3: Compilation | 5 | 5.1 - 5.7 | Bytecode Compiler |
| M4: Execution | 6 | 6.1 - 6.8 | Working Interpreter |
| M5: Compatibility | 7-8 | 7.1 - 8.3 | Built-in Objects + Standard Library |
| M6: Complete | 9-10 | 9.1 - 10.4 | Full Implementation |

---

## Progress Tracking

### Current Status

**Current Step**: Step 6.3 Complete  
**Last Commit**: `step-6.4-interpreter-control`  
**Next Step**: `step-6.5-interpreter-calls`

### Step Completion Log

Each completed step should be recorded here with its commit hash:

| Step | Commit | Date | Notes |
|------|--------|------|-------|
| 1.1 | `dd057de` | 2025-12-08 | Solution structure, multi-targeting, xUnit v3 MTP v2 |

### Phase Completion

- [x] **Phase 1**: Project Setup & Core Types (5 steps)
- [x] **Phase 2**: Lexer/Tokenizer (2 steps)
- [x] **Phase 3**: Bytecode & VM (5 steps)
- [x] **Phase 4**: Parser & Compiler (8 steps)
- [x] **Phase 5**: Runtime Objects (4 steps)
- [ ] **Phase 6**: Runtime & Interpreter (8 steps)
- [ ] **Phase 7**: Built-in Objects (13 steps)
- [ ] **Phase 8**: Standard Library (3 steps)
- [ ] **Phase 9**: Advanced Features (4 steps)
- [ ] **Phase 10**: Integration & Polish (4 steps)

**Total Steps**: 66

---

## Documentation Index

As each step is completed, documentation files will be added to the `docs/` folder:

| Doc | Title | Steps Covered |
|-----|-------|---------------|
| `01-project-setup.md` | Project Setup | 1.1 |
| `02-value-types.md` | JavaScript Value Types | 1.2, 1.3 |
| `04-atom-table.md` | Atoms and String Interning | 1.4 |
| `05-exception-hierarchy.md` | Error Handling | 1.5 |
| `06-token-definitions.md` | Token Definitions | 2.1 |
| `07-lexer-implementation.md` | Lexer Implementation | 2.2 |
| `08-opcode-definitions.md` | Bytecode OpCodes | 3.1 |
| `09-bytecode-buffer.md` | Bytecode Buffer | 3.2 |
| `10-constant-pool.md` | Constant Pool | 3.3 |
| `11-function-definition.md` | Function Definition | 3.4 |
| `12-line-number-table.md` | Line Number Table | 3.5 |
| `13-parser.md` | Expression Parser | 4.1 |
| `14-statement-parser.md` | Statement Parser | 4.2 |
| `15-function-parser.md` | Function Parser | 4.3 |
| `16-complete-expression-parser.md` | Complete Expression Parser | 4.4 |
| `17-class-parser.md` | Class Parser | 4.5 |
| `18-module-parser.md` | Module Parser | 4.6 |
| `19-destructuring-parser.md` | Destructuring Parser | 4.7 |
| `20-error-recovery.md` | Parser Error Recovery | 4.8 |
| `21-jsobject.md` | JSObject | 5.1 |
| `22-prototype-chain.md` | Prototype Chain | 5.2 |
| `23-jsfunction.md` | JSFunction Runtime | 5.3 |
| `24-array-object.md` | Array Object | 5.4 |
| `25-runtime-architecture.md` | Runtime Architecture | 6.1 |
| `26-interpreter-loop.md` | Interpreter Loop Basics | 6.2 |
| `27-variables-properties.md` | Variables and Properties | 6.3 |
| `28-interpreter-control-flow.md` | Control Flow | 6.4 |
| *...more as implementation progresses* | | |

---

## Notes for Contributors

1. **One step, one commit**: Keep commits focused and atomic
2. **Write docs first**: Document what you're about to build before coding
3. **Tests are required**: Each step should include relevant unit tests
4. **Plan evolves**: Feel free to split or merge steps as needed, but update this README
5. **Reference the original**: Compare with QuickJS C source to understand the design
