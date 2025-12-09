// Licensed under the MIT License.

namespace QuickJS;

/// <summary>
/// Identifies the class/type of a JavaScript object.
/// </summary>
/// <remarks>
/// <para>
/// This enum mirrors QuickJS's JS_CLASS_* enum values. Each object has an associated
/// class ID that determines its behavior and internal representation.
/// </para>
/// <para>
/// The class ID is used to:
/// - Determine which prototype to use when creating objects
/// - Dispatch property access for exotic objects (Array, String, etc.)
/// - Select appropriate finalization logic for native resources
/// - Enable optimized fast paths for known object types
/// </para>
/// <para>
/// Values 1-48 are reserved for built-in classes (matching QuickJS).
/// User-defined classes start from <see cref="FirstUserClass"/>.
/// </para>
/// </remarks>
public enum JSClassId
{
    /// <summary>
    /// Invalid/uninitialized class ID.
    /// </summary>
    None = 0,

    // ========================================================================
    // Core Object Types
    // ========================================================================

    /// <summary>
    /// Plain JavaScript object. This is the base class for all objects.
    /// Must be first (class ID = 1) as required by QuickJS.
    /// </summary>
    Object = 1,

    /// <summary>
    /// JavaScript Array. Uses optimized indexed property storage.
    /// Has special behavior for the 'length' property.
    /// </summary>
    Array = 2,

    /// <summary>
    /// JavaScript Error object (including TypeError, RangeError, etc.).
    /// </summary>
    Error = 3,

    // ========================================================================
    // Primitive Wrapper Types
    // ========================================================================

    /// <summary>
    /// Boxed Number object (created via new Number()).
    /// </summary>
    Number = 4,

    /// <summary>
    /// Boxed String object (created via new String()).
    /// Has special indexed property access for characters.
    /// </summary>
    String = 5,

    /// <summary>
    /// Boxed Boolean object (created via new Boolean()).
    /// </summary>
    Boolean = 6,

    /// <summary>
    /// Boxed Symbol object (created via Object(symbol)).
    /// </summary>
    Symbol = 7,

    // ========================================================================
    // Arguments Objects
    // ========================================================================

    /// <summary>
    /// Unmapped arguments object (strict mode).
    /// </summary>
    Arguments = 8,

    /// <summary>
    /// Mapped arguments object (non-strict mode).
    /// Has special binding behavior with function parameters.
    /// </summary>
    MappedArguments = 9,

    // ========================================================================
    // Date
    // ========================================================================

    /// <summary>
    /// JavaScript Date object.
    /// </summary>
    Date = 10,

    // ========================================================================
    // Module and Function Types
    // ========================================================================

    /// <summary>
    /// ES Module namespace object.
    /// </summary>
    ModuleNamespace = 11,

    /// <summary>
    /// Native C/C# function.
    /// </summary>
    CFunction = 12,

    /// <summary>
    /// JavaScript function compiled to bytecode.
    /// </summary>
    BytecodeFunction = 13,

    /// <summary>
    /// Bound function (created via Function.prototype.bind()).
    /// </summary>
    BoundFunction = 14,

    /// <summary>
    /// Native function with associated data.
    /// </summary>
    CFunctionData = 15,

    /// <summary>
    /// Generator function (function*).
    /// </summary>
    GeneratorFunction = 16,

    /// <summary>
    /// For-in iterator object.
    /// </summary>
    ForInIterator = 17,

    /// <summary>
    /// Regular expression object.
    /// </summary>
    RegExp = 18,

    // ========================================================================
    // Binary Data Types
    // ========================================================================

    /// <summary>
    /// ArrayBuffer object - represents a raw binary data buffer.
    /// </summary>
    ArrayBuffer = 19,

    /// <summary>
    /// SharedArrayBuffer object - shared memory between workers.
    /// </summary>
    SharedArrayBuffer = 20,

    /// <summary>
    /// Uint8ClampedArray typed array.
    /// </summary>
    Uint8ClampedArray = 21,

    /// <summary>
    /// Int8Array typed array.
    /// </summary>
    Int8Array = 22,

    /// <summary>
    /// Uint8Array typed array.
    /// </summary>
    Uint8Array = 23,

    /// <summary>
    /// Int16Array typed array.
    /// </summary>
    Int16Array = 24,

    /// <summary>
    /// Uint16Array typed array.
    /// </summary>
    Uint16Array = 25,

    /// <summary>
    /// Int32Array typed array.
    /// </summary>
    Int32Array = 26,

    /// <summary>
    /// Uint32Array typed array.
    /// </summary>
    Uint32Array = 27,

    /// <summary>
    /// BigInt64Array typed array.
    /// </summary>
    BigInt64Array = 28,

    /// <summary>
    /// BigUint64Array typed array.
    /// </summary>
    BigUint64Array = 29,

    /// <summary>
    /// Float16Array typed array (ES proposal).
    /// </summary>
    Float16Array = 30,

    /// <summary>
    /// Float32Array typed array.
    /// </summary>
    Float32Array = 31,

    /// <summary>
    /// Float64Array typed array.
    /// </summary>
    Float64Array = 32,

    /// <summary>
    /// DataView object - provides a low-level interface for reading/writing
    /// binary data in an ArrayBuffer.
    /// </summary>
    DataView = 33,

    /// <summary>
    /// Boxed BigInt object (created via Object(bigint)).
    /// </summary>
    BigInt = 34,

    // ========================================================================
    // Collection Types
    // ========================================================================

    /// <summary>
    /// Map object - key-value collection with any key type.
    /// </summary>
    Map = 35,

    /// <summary>
    /// Set object - collection of unique values.
    /// </summary>
    Set = 36,

    /// <summary>
    /// WeakMap object - key-value collection with weak references to keys.
    /// </summary>
    WeakMap = 37,

    /// <summary>
    /// WeakSet object - collection of weakly held objects.
    /// </summary>
    WeakSet = 38,

    // ========================================================================
    // Iterator Types
    // ========================================================================

    /// <summary>
    /// Generic Iterator object.
    /// </summary>
    Iterator = 39,

    /// <summary>
    /// Iterator produced by Iterator.concat().
    /// </summary>
    IteratorConcat = 40,

    /// <summary>
    /// Iterator helper (from Iterator.prototype methods).
    /// </summary>
    IteratorHelper = 41,

    /// <summary>
    /// Wrapped iterator.
    /// </summary>
    IteratorWrap = 42,

    /// <summary>
    /// Map iterator (from Map.prototype.entries(), etc.).
    /// </summary>
    MapIterator = 43,

    /// <summary>
    /// Set iterator (from Set.prototype.values(), etc.).
    /// </summary>
    SetIterator = 44,

    /// <summary>
    /// Array iterator (from Array.prototype.values(), etc.).
    /// </summary>
    ArrayIterator = 45,

    /// <summary>
    /// String iterator (from String.prototype[Symbol.iterator]()).
    /// </summary>
    StringIterator = 46,

    /// <summary>
    /// RegExp string iterator (from String.prototype.matchAll()).
    /// </summary>
    RegExpStringIterator = 47,

    /// <summary>
    /// Generator object (created by calling a generator function).
    /// </summary>
    Generator = 48,

    /// <summary>
    /// Global object.
    /// </summary>
    GlobalObject = 49,

    // ========================================================================
    // Proxy and Reflect
    // ========================================================================

    /// <summary>
    /// Proxy object - enables custom behavior for fundamental operations.
    /// </summary>
    Proxy = 50,

    // ========================================================================
    // Promise Types
    // ========================================================================

    /// <summary>
    /// Promise object.
    /// </summary>
    Promise = 51,

    /// <summary>
    /// Internal resolve function for Promise.
    /// </summary>
    PromiseResolveFunction = 52,

    /// <summary>
    /// Internal reject function for Promise.
    /// </summary>
    PromiseRejectFunction = 53,

    // ========================================================================
    // Async Function Types
    // ========================================================================

    /// <summary>
    /// Async function.
    /// </summary>
    AsyncFunction = 54,

    /// <summary>
    /// Internal async function resolve continuation.
    /// </summary>
    AsyncFunctionResolve = 55,

    /// <summary>
    /// Internal async function reject continuation.
    /// </summary>
    AsyncFunctionReject = 56,

    /// <summary>
    /// Async-from-sync iterator adapter.
    /// </summary>
    AsyncFromSyncIterator = 57,

    /// <summary>
    /// Async generator function (async function*).
    /// </summary>
    AsyncGeneratorFunction = 58,

    /// <summary>
    /// Async generator object.
    /// </summary>
    AsyncGenerator = 59,

    // ========================================================================
    // User-Defined Classes
    // ========================================================================

    /// <summary>
    /// First ID available for user-defined classes.
    /// Register new classes starting from this value.
    /// </summary>
    FirstUserClass = 100,
}

/// <summary>
/// Extension methods for JSClassId.
/// </summary>
public static class JSClassIdExtensions
{
    /// <summary>
    /// Determines if the class ID represents a function type.
    /// </summary>
    public static bool IsFunction(this JSClassId classId)
    {
        return classId == JSClassId.CFunction ||
               classId == JSClassId.BytecodeFunction ||
               classId == JSClassId.BoundFunction ||
               classId == JSClassId.CFunctionData ||
               classId == JSClassId.GeneratorFunction ||
               classId == JSClassId.AsyncFunction ||
               classId == JSClassId.AsyncGeneratorFunction;
    }

    /// <summary>
    /// Determines if the class ID represents a typed array type.
    /// </summary>
    public static bool IsTypedArray(this JSClassId classId)
    {
        return classId >= JSClassId.Uint8ClampedArray && classId <= JSClassId.Float64Array;
    }

    /// <summary>
    /// Determines if the class ID represents an array-like object.
    /// </summary>
    public static bool IsArrayLike(this JSClassId classId)
    {
        return classId == JSClassId.Array ||
               classId == JSClassId.Arguments ||
               classId == JSClassId.MappedArguments ||
               classId.IsTypedArray();
    }

    /// <summary>
    /// Determines if the class ID represents a primitive wrapper object.
    /// </summary>
    public static bool IsPrimitiveWrapper(this JSClassId classId)
    {
        return classId == JSClassId.Number ||
               classId == JSClassId.String ||
               classId == JSClassId.Boolean ||
               classId == JSClassId.Symbol ||
               classId == JSClassId.BigInt;
    }
}
