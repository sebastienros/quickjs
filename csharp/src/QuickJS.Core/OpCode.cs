// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace QuickJS;

/// <summary>
/// Bytecode opcodes for the QuickJS virtual machine.
/// These follow the stack-based VM design where operations pop operands
/// from the stack and push results.
/// </summary>
/// <remarks>
/// <para>
/// Based on quickjs-opcode.h from the original QuickJS implementation.
/// Each opcode has associated metadata in <see cref="OpCodeInfo"/>.
/// </para>
/// <para>
/// The temporary opcodes (used during compilation) share the same value range
/// as short opcodes because they are mutually exclusive - temporary opcodes
/// are resolved before short opcodes are emitted.
/// </para>
/// </remarks>
public enum OpCode : ushort
{
    // ========================================
    // Invalid opcode (never emitted)
    // ========================================

    /// <summary>Invalid opcode, never emitted in valid bytecode.</summary>
    Invalid = 0,

    // ========================================
    // Push values onto the stack
    // ========================================

    /// <summary>Push a 32-bit signed integer. Format: i32</summary>
    PushI32,

    /// <summary>Push a constant from the constant pool. Format: const</summary>
    PushConst,

    /// <summary>Push a function closure from the constant pool. Format: const</summary>
    FClosure,

    /// <summary>Push an atom value. Format: atom</summary>
    PushAtomValue,

    /// <summary>Create a private symbol from an atom. Format: atom</summary>
    PrivateSymbol,

    /// <summary>Push undefined onto the stack.</summary>
    Undefined,

    /// <summary>Push null onto the stack.</summary>
    Null,

    /// <summary>Push 'this' value (only used at start of function).</summary>
    PushThis,

    /// <summary>Push false onto the stack.</summary>
    PushFalse,

    /// <summary>Push true onto the stack.</summary>
    PushTrue,

    /// <summary>Push an empty object onto the stack.</summary>
    Object,

    /// <summary>Push a special object (arguments, etc.). Format: u8</summary>
    SpecialObject,

    /// <summary>Push rest parameters. Format: u16</summary>
    Rest,

    // ========================================
    // Stack manipulation
    // ========================================

    /// <summary>Drop top of stack: a -> (empty)</summary>
    Drop,

    /// <summary>Remove second item: a b -> b</summary>
    Nip,

    /// <summary>Remove third item: a b c -> b c</summary>
    Nip1,

    /// <summary>Duplicate top: a -> a a</summary>
    Dup,

    /// <summary>Duplicate second: a b -> a a b</summary>
    Dup1,

    /// <summary>Duplicate top two: a b -> a b a b</summary>
    Dup2,

    /// <summary>Duplicate top three: a b c -> a b c a b c</summary>
    Dup3,

    /// <summary>Insert duplicate: obj a -> a obj a</summary>
    Insert2,

    /// <summary>Insert duplicate: obj prop a -> a obj prop a</summary>
    Insert3,

    /// <summary>Insert duplicate: this obj prop a -> a this obj prop a</summary>
    Insert4,

    /// <summary>Permute: obj a b -> a obj b</summary>
    Perm3,

    /// <summary>Permute: obj prop a b -> a obj prop b</summary>
    Perm4,

    /// <summary>Permute: this obj prop a b -> a this obj prop b</summary>
    Perm5,

    /// <summary>Swap top two: a b -> b a</summary>
    Swap,

    /// <summary>Swap pairs: a b c d -> c d a b</summary>
    Swap2,

    /// <summary>Rotate left 3: x a b -> a b x</summary>
    Rot3L,

    /// <summary>Rotate right 3: a b x -> x a b</summary>
    Rot3R,

    /// <summary>Rotate left 4: x a b c -> a b c x</summary>
    Rot4L,

    /// <summary>Rotate left 5: x a b c d -> a b c d x</summary>
    Rot5L,

    // ========================================
    // Function calls
    // ========================================

    /// <summary>Call constructor: func new.target args -> ret. Format: npop</summary>
    CallConstructor,

    /// <summary>Call function: func args -> ret. Format: npop</summary>
    Call,

    /// <summary>Tail call: func args -> (returns). Format: npop</summary>
    TailCall,

    /// <summary>Call method: obj.method args -> ret. Format: npop</summary>
    CallMethod,

    /// <summary>Tail call method. Format: npop</summary>
    TailCallMethod,

    /// <summary>Create array from arguments. Format: npop</summary>
    ArrayFrom,

    /// <summary>Apply function with arguments array. Format: u16</summary>
    Apply,

    /// <summary>Return from function with value on stack.</summary>
    Return,

    /// <summary>Return undefined from function.</summary>
    ReturnUndef,

    /// <summary>Check constructor return value.</summary>
    CheckCtorReturn,

    /// <summary>Check if called as constructor.</summary>
    CheckCtor,

    /// <summary>Initialize constructor.</summary>
    InitCtor,

    /// <summary>Check private brand: this_obj func -> this_obj func</summary>
    CheckBrand,

    /// <summary>Add private brand: this_obj home_obj -></summary>
    AddBrand,

    /// <summary>Return from async function.</summary>
    ReturnAsync,

    /// <summary>Throw exception with value on stack.</summary>
    Throw,

    /// <summary>Throw a specific error. Format: atom_u8</summary>
    ThrowError,

    /// <summary>Eval function call. Format: npop_u16</summary>
    Eval,

    /// <summary>Apply eval: func array -> ret. Format: u16</summary>
    ApplyEval,

    /// <summary>Create RegExp from pattern and bytecode.</summary>
    RegExp,

    /// <summary>Get super constructor.</summary>
    GetSuper,

    /// <summary>Dynamic module import.</summary>
    Import,

    // ========================================
    // Variable access
    // ========================================

    /// <summary>Get variable, push undefined if not found. Format: var_ref</summary>
    GetVarUndef,

    /// <summary>Get variable, throw if not found. Format: var_ref</summary>
    GetVar,

    /// <summary>Put (assign) variable. Format: var_ref</summary>
    PutVar,

    /// <summary>Initialize global lexical variable. Format: var_ref</summary>
    PutVarInit,

    /// <summary>Get reference value: ref -> ref value</summary>
    GetRefValue,

    /// <summary>Put reference value: ref value -></summary>
    PutRefValue,

    // ========================================
    // Property access
    // ========================================

    /// <summary>Get property by name: obj -> value. Format: atom</summary>
    GetField,

    /// <summary>Get property, keep object: obj -> obj value. Format: atom</summary>
    GetField2,

    /// <summary>Set property by name: obj value ->. Format: atom</summary>
    PutField,

    /// <summary>Get private field: obj prop -> value</summary>
    GetPrivateField,

    /// <summary>Put private field: obj value prop -></summary>
    PutPrivateField,

    /// <summary>Define private field: obj prop value -> obj</summary>
    DefinePrivateField,

    /// <summary>Get array element: obj index -> value</summary>
    GetArrayEl,

    /// <summary>Get array element, keep object: obj index -> obj value</summary>
    GetArrayEl2,

    /// <summary>Get array element, keep both: obj index -> obj index value</summary>
    GetArrayEl3,

    /// <summary>Put array element: obj index value -></summary>
    PutArrayEl,

    /// <summary>Get super property: this obj prop -> value</summary>
    GetSuperValue,

    /// <summary>Put super property: this obj prop value -></summary>
    PutSuperValue,

    /// <summary>Define property: obj value -> obj. Format: atom</summary>
    DefineField,

    /// <summary>Set function name. Format: atom</summary>
    SetName,

    /// <summary>Set computed function name.</summary>
    SetNameComputed,

    /// <summary>Set prototype: ctor proto -> ctor</summary>
    SetProto,

    /// <summary>Set home object: func home -> func home</summary>
    SetHomeObject,

    /// <summary>Define array element: array index value -> array index</summary>
    DefineArrayEl,

    /// <summary>Append enumerated object, update length.</summary>
    Append,

    /// <summary>Copy data properties. Format: u8</summary>
    CopyDataProperties,

    /// <summary>Define method. Format: atom_u8</summary>
    DefineMethod,

    /// <summary>Define method with computed name. Format: u8</summary>
    DefineMethodComputed,

    /// <summary>Define class: parent ctor -> ctor proto. Format: atom_u8</summary>
    DefineClass,

    /// <summary>Define class with computed name. Format: atom_u8</summary>
    DefineClassComputed,

    // ========================================
    // Local/argument/closure variable access
    // ========================================

    /// <summary>Get local variable. Format: loc</summary>
    GetLoc,

    /// <summary>Put local variable. Format: loc</summary>
    PutLoc,

    /// <summary>Set local variable (keep value on stack). Format: loc</summary>
    SetLoc,

    /// <summary>Get argument. Format: arg</summary>
    GetArg,

    /// <summary>Put argument. Format: arg</summary>
    PutArg,

    /// <summary>Set argument (keep value on stack). Format: arg</summary>
    SetArg,

    /// <summary>Get closure variable reference. Format: var_ref</summary>
    GetVarRef,

    /// <summary>Put closure variable reference. Format: var_ref</summary>
    PutVarRef,

    /// <summary>Set closure variable reference. Format: var_ref</summary>
    SetVarRef,

    /// <summary>Mark local as uninitialized. Format: loc</summary>
    SetLocUninitialized,

    /// <summary>Get local with TDZ check. Format: loc</summary>
    GetLocCheck,

    /// <summary>Put local with TDZ check. Format: loc</summary>
    PutLocCheck,

    /// <summary>Put local check for initialization. Format: loc</summary>
    PutLocCheckInit,

    /// <summary>Get local with 'this' check. Format: loc</summary>
    GetLocCheckThis,

    /// <summary>Get var ref with TDZ check. Format: var_ref</summary>
    GetVarRefCheck,

    /// <summary>Put var ref with TDZ check. Format: var_ref</summary>
    PutVarRefCheck,

    /// <summary>Put var ref check for initialization. Format: var_ref</summary>
    PutVarRefCheckInit,

    /// <summary>Close over local variable. Format: loc</summary>
    CloseLoc,

    // ========================================
    // Control flow
    // ========================================

    /// <summary>Jump if false. Format: label</summary>
    IfFalse,

    /// <summary>Jump if true. Format: label</summary>
    IfTrue,

    /// <summary>Unconditional jump. Format: label</summary>
    Goto,

    /// <summary>Catch exception handler. Format: label</summary>
    Catch,

    /// <summary>Jump to finally block subroutine. Format: label</summary>
    GoSub,

    /// <summary>Return from finally block.</summary>
    Ret,

    /// <summary>Nip catch handler: catch ... a -> a</summary>
    NipCatch,

    // ========================================
    // Type conversion
    // ========================================

    /// <summary>Convert to object.</summary>
    ToObject,

    /// <summary>Convert to property key.</summary>
    ToPropKey,

    /// <summary>Convert to BigInt.</summary>
    ToBigInt,

    // ========================================
    // With statement variable access
    // ========================================

    /// <summary>With statement get variable. Format: atom_label_u8</summary>
    WithGetVar,

    /// <summary>With statement put variable. Format: atom_label_u8</summary>
    WithPutVar,

    /// <summary>With statement delete variable. Format: atom_label_u8</summary>
    WithDeleteVar,

    /// <summary>With statement make reference. Format: atom_label_u8</summary>
    WithMakeRef,

    /// <summary>With statement get reference. Format: atom_label_u8</summary>
    WithGetRef,

    // ========================================
    // Reference creation
    // ========================================

    /// <summary>Make local reference. Format: atom_u16</summary>
    MakeLocRef,

    /// <summary>Make argument reference. Format: atom_u16</summary>
    MakeArgRef,

    /// <summary>Make var ref reference. Format: atom_u16</summary>
    MakeVarRefRef,

    /// <summary>Make variable reference. Format: atom</summary>
    MakeVarRef,

    // ========================================
    // Iteration
    // ========================================

    /// <summary>Start for-in iteration.</summary>
    ForInStart,

    /// <summary>Start for-of iteration.</summary>
    ForOfStart,

    /// <summary>Start for-await-of iteration.</summary>
    ForAwaitOfStart,

    /// <summary>Get next for-in value.</summary>
    ForInNext,

    /// <summary>Get next for-of value. Format: u8</summary>
    ForOfNext,

    /// <summary>Get next for-await-of value.</summary>
    ForAwaitOfNext,

    /// <summary>Check iterator result is object.</summary>
    IteratorCheckObject,

    /// <summary>Get iterator value and done flag.</summary>
    IteratorGetValueDone,

    /// <summary>Close iterator.</summary>
    IteratorClose,

    /// <summary>Call iterator next.</summary>
    IteratorNext,

    /// <summary>Call iterator method. Format: u8</summary>
    IteratorCall,

    /// <summary>Initial yield in generator.</summary>
    InitialYield,

    /// <summary>Yield value from generator.</summary>
    Yield,

    /// <summary>Yield* delegation.</summary>
    YieldStar,

    /// <summary>Async yield* delegation.</summary>
    AsyncYieldStar,

    /// <summary>Await promise.</summary>
    Await,

    // ========================================
    // Arithmetic/logic unary operations
    // ========================================

    /// <summary>Negate: -a</summary>
    Neg,

    /// <summary>Unary plus: +a</summary>
    Plus,

    /// <summary>Decrement: --a or a--</summary>
    Dec,

    /// <summary>Increment: ++a or a++</summary>
    Inc,

    /// <summary>Post-decrement: a--</summary>
    PostDec,

    /// <summary>Post-increment: a++</summary>
    PostInc,

    /// <summary>Decrement local. Format: loc8</summary>
    DecLoc,

    /// <summary>Increment local. Format: loc8</summary>
    IncLoc,

    /// <summary>Add to local. Format: loc8</summary>
    AddLoc,

    /// <summary>Bitwise NOT: ~a</summary>
    Not,

    /// <summary>Logical NOT: !a</summary>
    LNot,

    /// <summary>typeof operator.</summary>
    TypeOf,

    /// <summary>delete property: obj prop -> bool</summary>
    Delete,

    /// <summary>delete variable. Format: atom</summary>
    DeleteVar,

    // ========================================
    // Arithmetic/logic binary operations
    // ========================================

    /// <summary>Multiply: a * b</summary>
    Mul,

    /// <summary>Divide: a / b</summary>
    Div,

    /// <summary>Modulo: a % b</summary>
    Mod,

    /// <summary>Add: a + b</summary>
    Add,

    /// <summary>Subtract: a - b</summary>
    Sub,

    /// <summary>Power: a ** b</summary>
    Pow,

    /// <summary>Left shift: a &lt;&lt; b</summary>
    Shl,

    /// <summary>Signed right shift: a >> b</summary>
    Sar,

    /// <summary>Unsigned right shift: a >>> b</summary>
    Shr,

    /// <summary>Less than: a &lt; b</summary>
    Lt,

    /// <summary>Less than or equal: a &lt;= b</summary>
    Lte,

    /// <summary>Greater than: a > b</summary>
    Gt,

    /// <summary>Greater than or equal: a >= b</summary>
    Gte,

    /// <summary>instanceof operator.</summary>
    InstanceOf,

    /// <summary>in operator: prop in obj</summary>
    In,

    /// <summary>Equality: a == b</summary>
    Eq,

    /// <summary>Inequality: a != b</summary>
    Neq,

    /// <summary>Strict equality: a === b</summary>
    StrictEq,

    /// <summary>Strict inequality: a !== b</summary>
    StrictNeq,

    /// <summary>Bitwise AND: a &amp; b</summary>
    And,

    /// <summary>Bitwise XOR: a ^ b</summary>
    Xor,

    /// <summary>Bitwise OR: a | b</summary>
    Or,

    /// <summary>Check if undefined or null.</summary>
    IsUndefinedOrNull,

    /// <summary>Private field in operator.</summary>
    PrivateIn,

    /// <summary>Push BigInt from 32-bit value. Format: i32</summary>
    PushBigIntI32,

    /// <summary>No operation.</summary>
    Nop,

    // ========================================
    // Temporary opcodes (removed during compilation phases)
    // ========================================

    /// <summary>Enter lexical scope. Format: u16 (temporary, removed in phase 2)</summary>
    EnterScope,

    /// <summary>Leave lexical scope. Format: u16 (temporary, removed in phase 2)</summary>
    LeaveScope,

    /// <summary>Label marker. Format: label (temporary, removed in phase 3)</summary>
    Label,

    /// <summary>Scope get variable (undefined if missing). Format: atom_u16 (temporary)</summary>
    ScopeGetVarUndef,

    /// <summary>Scope get variable. Format: atom_u16 (temporary)</summary>
    ScopeGetVar,

    /// <summary>Scope put variable. Format: atom_u16 (temporary)</summary>
    ScopePutVar,

    /// <summary>Scope delete variable. Format: atom_u16 (temporary)</summary>
    ScopeDeleteVar,

    /// <summary>Scope make reference. Format: atom_label_u16 (temporary)</summary>
    ScopeMakeRef,

    /// <summary>Scope get reference. Format: atom_u16 (temporary)</summary>
    ScopeGetRef,

    /// <summary>Scope put variable init. Format: atom_u16 (temporary)</summary>
    ScopePutVarInit,

    /// <summary>Scope get var with this check. Format: atom_u16 (temporary)</summary>
    ScopeGetVarCheckThis,

    /// <summary>Scope get private field. Format: atom_u16 (temporary)</summary>
    ScopeGetPrivateField,

    /// <summary>Scope get private field (keep obj). Format: atom_u16 (temporary)</summary>
    ScopeGetPrivateField2,

    /// <summary>Scope put private field. Format: atom_u16 (temporary)</summary>
    ScopePutPrivateField,

    /// <summary>Scope private field in. Format: atom_u16 (temporary)</summary>
    ScopeInPrivateField,

    /// <summary>Get field with optional chaining. Format: atom (temporary)</summary>
    GetFieldOptChain,

    /// <summary>Get array element with optional chaining. (temporary)</summary>
    GetArrayElOptChain,

    /// <summary>Set class name. Format: u32 (temporary)</summary>
    SetClassName,

    /// <summary>Line number marker. Format: u32 (temporary, removed in phase 3)</summary>
    LineNum,

    // ========================================
    // Short opcodes (optimization for common cases)
    // ========================================

    /// <summary>Push -1.</summary>
    PushMinus1,

    /// <summary>Push 0.</summary>
    Push0,

    /// <summary>Push 1.</summary>
    Push1,

    /// <summary>Push 2.</summary>
    Push2,

    /// <summary>Push 3.</summary>
    Push3,

    /// <summary>Push 4.</summary>
    Push4,

    /// <summary>Push 5.</summary>
    Push5,

    /// <summary>Push 6.</summary>
    Push6,

    /// <summary>Push 7.</summary>
    Push7,

    /// <summary>Push 8-bit signed integer. Format: i8</summary>
    PushI8,

    /// <summary>Push 16-bit signed integer. Format: i16</summary>
    PushI16,

    /// <summary>Push constant (8-bit index). Format: const8</summary>
    PushConst8,

    /// <summary>Push closure (8-bit index). Format: const8</summary>
    FClosure8,

    /// <summary>Push empty string.</summary>
    PushEmptyString,

    /// <summary>Get local (8-bit index). Format: loc8</summary>
    GetLoc8,

    /// <summary>Put local (8-bit index). Format: loc8</summary>
    PutLoc8,

    /// <summary>Set local (8-bit index). Format: loc8</summary>
    SetLoc8,

    /// <summary>Get local 0.</summary>
    GetLoc0,

    /// <summary>Get local 1.</summary>
    GetLoc1,

    /// <summary>Get local 2.</summary>
    GetLoc2,

    /// <summary>Get local 3.</summary>
    GetLoc3,

    /// <summary>Put local 0.</summary>
    PutLoc0,

    /// <summary>Put local 1.</summary>
    PutLoc1,

    /// <summary>Put local 2.</summary>
    PutLoc2,

    /// <summary>Put local 3.</summary>
    PutLoc3,

    /// <summary>Set local 0.</summary>
    SetLoc0,

    /// <summary>Set local 1.</summary>
    SetLoc1,

    /// <summary>Set local 2.</summary>
    SetLoc2,

    /// <summary>Set local 3.</summary>
    SetLoc3,

    /// <summary>Get argument 0.</summary>
    GetArg0,

    /// <summary>Get argument 1.</summary>
    GetArg1,

    /// <summary>Get argument 2.</summary>
    GetArg2,

    /// <summary>Get argument 3.</summary>
    GetArg3,

    /// <summary>Put argument 0.</summary>
    PutArg0,

    /// <summary>Put argument 1.</summary>
    PutArg1,

    /// <summary>Put argument 2.</summary>
    PutArg2,

    /// <summary>Put argument 3.</summary>
    PutArg3,

    /// <summary>Set argument 0.</summary>
    SetArg0,

    /// <summary>Set argument 1.</summary>
    SetArg1,

    /// <summary>Set argument 2.</summary>
    SetArg2,

    /// <summary>Set argument 3.</summary>
    SetArg3,

    /// <summary>Get var ref 0.</summary>
    GetVarRef0,

    /// <summary>Get var ref 1.</summary>
    GetVarRef1,

    /// <summary>Get var ref 2.</summary>
    GetVarRef2,

    /// <summary>Get var ref 3.</summary>
    GetVarRef3,

    /// <summary>Put var ref 0.</summary>
    PutVarRef0,

    /// <summary>Put var ref 1.</summary>
    PutVarRef1,

    /// <summary>Put var ref 2.</summary>
    PutVarRef2,

    /// <summary>Put var ref 3.</summary>
    PutVarRef3,

    /// <summary>Set var ref 0.</summary>
    SetVarRef0,

    /// <summary>Set var ref 1.</summary>
    SetVarRef1,

    /// <summary>Set var ref 2.</summary>
    SetVarRef2,

    /// <summary>Set var ref 3.</summary>
    SetVarRef3,

    /// <summary>Get .length property.</summary>
    GetLength,

    /// <summary>Jump if false (8-bit offset). Format: label8</summary>
    IfFalse8,

    /// <summary>Jump if true (8-bit offset). Format: label8</summary>
    IfTrue8,

    /// <summary>Unconditional jump (8-bit offset). Format: label8</summary>
    Goto8,

    /// <summary>Unconditional jump (16-bit offset). Format: label16</summary>
    Goto16,

    /// <summary>Call with 0 arguments.</summary>
    Call0,

    /// <summary>Call with 1 argument.</summary>
    Call1,

    /// <summary>Call with 2 arguments.</summary>
    Call2,

    /// <summary>Call with 3 arguments.</summary>
    Call3,

    /// <summary>Check if value is undefined.</summary>
    IsUndefined,

    /// <summary>Check if value is null.</summary>
    IsNull,

    /// <summary>Check if typeof is 'undefined'.</summary>
    TypeOfIsUndefined,

    /// <summary>Check if typeof is 'function'.</summary>
    TypeOfIsFunction,
}
