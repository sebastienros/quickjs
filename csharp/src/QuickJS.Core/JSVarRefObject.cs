namespace QuickJS;

/// <summary>
/// Wraps a JSVarRef so it can be carried on the operand stack as a JSValue (object).
/// Internal use only.
/// </summary>
internal sealed class JSVarRefObject : JSObject
{
    public JSVarRef VarRef { get; }

    public JSVarRefObject(JSVarRef varRef) : base(null, JSClassId.Object)
    {
        VarRef = varRef;
    }
}
