namespace QuickJS;

/// <summary>
/// Operand values for OP_special_object (mirrors QuickJS).
/// </summary>
public enum SpecialObjectType : byte
{
    /// <summary>Unmapped (strict) arguments object.</summary>
    Arguments = 0,
    /// <summary>Mapped (non-strict) arguments object.</summary>
    MappedArguments = 1,
    /// <summary>The current function object.</summary>
    ThisFunction = 2,
    /// <summary>new.target value.</summary>
    NewTarget = 3,
    /// <summary>home object (for super references).</summary>
    HomeObject = 4,
    /// <summary>var object (scripts).</summary>
    VarObject = 5,
    /// <summary>import.meta object.</summary>
    ImportMeta = 6,
}
