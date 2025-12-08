namespace QuickJS;

/// <summary>
/// Represents the type tag for a JavaScript value.
/// </summary>
/// <remarks>
/// <para>
/// This enum mirrors the JS_TAG_* constants from QuickJS's quickjs.h.
/// In the original C implementation, tags with negative values indicate
/// reference-counted heap objects, while non-negative tags indicate
/// immediate values that don't require memory management.
/// </para>
/// <para>
/// In this C# implementation, we don't need manual reference counting
/// as .NET's garbage collector handles memory management automatically.
/// However, we preserve the tag structure for compatibility and to
/// understand the original design.
/// </para>
/// </remarks>
public enum JSValueType
{
    // ========================================================================
    // Reference-counted types (negative tags in original C implementation)
    // These represent heap-allocated objects that require memory management.
    // In C#, the GC handles this automatically.
    // ========================================================================

    /// <summary>
    /// Arbitrary precision integer (BigInt).
    /// Represents integers larger than what can fit in a 32-bit int.
    /// </summary>
    BigInt = -9,

    /// <summary>
    /// JavaScript Symbol - a unique and immutable primitive value.
    /// Symbols are often used as object property keys.
    /// </summary>
    Symbol = -8,

    /// <summary>
    /// JavaScript String - a sequence of UTF-16 code units.
    /// </summary>
    String = -7,

    /// <summary>
    /// Internal rope structure for efficient string concatenation.
    /// Used internally by the engine.
    /// </summary>
    StringRope = -6,

    /// <summary>
    /// ES Module object. Used internally by the engine.
    /// </summary>
    Module = -3,

    /// <summary>
    /// Compiled function bytecode. Used internally by the engine.
    /// </summary>
    FunctionBytecode = -2,

    /// <summary>
    /// JavaScript Object - the base type for all objects including
    /// arrays, functions, dates, regular expressions, etc.
    /// </summary>
    Object = -1,

    // ========================================================================
    // Immediate types (non-negative tags in original C implementation)
    // These values are stored directly in the JSValue without heap allocation.
    // ========================================================================

    /// <summary>
    /// 32-bit signed integer.
    /// JavaScript numbers that can be exactly represented as int32 are
    /// stored in this more efficient format.
    /// </summary>
    Int = 0,

    /// <summary>
    /// Boolean value (true or false).
    /// </summary>
    Bool = 1,

    /// <summary>
    /// The null value - represents the intentional absence of any object value.
    /// </summary>
    Null = 2,

    /// <summary>
    /// The undefined value - indicates that a variable has not been assigned a value.
    /// </summary>
    Undefined = 3,

    /// <summary>
    /// Uninitialized value. Used internally for temporal dead zone (TDZ)
    /// checking with let/const declarations.
    /// </summary>
    Uninitialized = 4,

    /// <summary>
    /// Used internally for exception handling - stores catch block offsets.
    /// </summary>
    CatchOffset = 5,

    /// <summary>
    /// Indicates that an exception has been thrown.
    /// The actual exception object is stored in the context.
    /// </summary>
    Exception = 6,

    /// <summary>
    /// Small BigInt that fits in a single limb (32 or 64 bits depending on platform).
    /// An optimization to avoid heap allocation for small big integers.
    /// </summary>
    ShortBigInt = 7,

    /// <summary>
    /// 64-bit floating point number (IEEE 754 double precision).
    /// Used for all JavaScript numbers that cannot be represented as int32.
    /// </summary>
    Float64 = 8,
}

/// <summary>
/// Extension methods for <see cref="JSValueType"/>.
/// </summary>
public static class JSValueTypeExtensions
{
    /// <summary>
    /// Determines whether this value type requires heap allocation (reference type).
    /// </summary>
    /// <param name="type">The value type to check.</param>
    /// <returns>True if the type is heap-allocated; otherwise, false.</returns>
    public static bool IsHeapAllocated(this JSValueType type)
    {
        // In the original C implementation, negative tags indicate reference-counted types
        return (int)type < 0;
    }

    /// <summary>
    /// Determines whether this value type is an immediate value (stored inline).
    /// </summary>
    /// <param name="type">The value type to check.</param>
    /// <returns>True if the type is an immediate value; otherwise, false.</returns>
    public static bool IsImmediate(this JSValueType type)
    {
        return (int)type >= 0;
    }

    /// <summary>
    /// Determines whether this value type represents a number (Int, Float64, BigInt, or ShortBigInt).
    /// </summary>
    /// <param name="type">The value type to check.</param>
    /// <returns>True if the type is numeric; otherwise, false.</returns>
    public static bool IsNumber(this JSValueType type)
    {
        return type == JSValueType.Int
            || type == JSValueType.Float64
            || type == JSValueType.BigInt
            || type == JSValueType.ShortBigInt;
    }

    /// <summary>
    /// Determines whether this value type is used internally by the engine.
    /// </summary>
    /// <param name="type">The value type to check.</param>
    /// <returns>True if the type is internal; otherwise, false.</returns>
    public static bool IsInternal(this JSValueType type)
    {
        return type == JSValueType.Module
            || type == JSValueType.FunctionBytecode
            || type == JSValueType.CatchOffset
            || type == JSValueType.Uninitialized
            || type == JSValueType.StringRope;
    }
}
