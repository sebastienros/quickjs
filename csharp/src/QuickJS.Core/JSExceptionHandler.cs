namespace QuickJS;

/// <summary>
/// Represents a try/catch/finally handler entry for a function.
/// </summary>
public sealed class JSExceptionHandler
{
    /// <summary>Start PC (inclusive) of protected region.</summary>
    public int StartPc { get; set; }
    /// <summary>End PC (exclusive) of protected region.</summary>
    public int EndPc { get; set; }
    /// <summary>Catch handler PC, or -1 if none.</summary>
    public int CatchPc { get; set; } = -1;
    /// <summary>Finally handler PC, or -1 if none.</summary>
    public int FinallyPc { get; set; } = -1;
    /// <summary>Stack depth to restore when entering handler.</summary>
    public int StackDepth { get; set; }

    /// <summary>Gets whether a catch handler is present.</summary>
    public bool HasCatch => CatchPc >= 0;
    /// <summary>Gets whether a finally handler is present.</summary>
    public bool HasFinally => FinallyPc >= 0;
}
