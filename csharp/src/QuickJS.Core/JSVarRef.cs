// Licensed under the MIT License.

using System.Diagnostics;

namespace QuickJS;

/// <summary>
/// Represents a reference to a captured variable in a closure.
/// </summary>
/// <remarks>
/// <para>
/// When a JavaScript function closes over variables from an outer scope, those variables
/// need to be accessible even after the outer function has returned. <see cref="JSVarRef"/>
/// provides this capability by tracking whether the variable is still on the stack or has
/// been "detached" (moved to the heap).
/// </para>
/// <para>
/// This mirrors QuickJS's <c>struct JSVarRef</c> from quickjs.c:
/// </para>
/// <code>
/// typedef struct JSVarRef {
///     JSGCObjectHeader header;
///     JSValue *pvalue;       // pointer to the value
///     union {
///         JSValue value;     // used when is_detached = TRUE
///         struct {
///             uint16_t var_ref_idx;
///             JSStackFrame *stack_frame;
///         };                  // used when is_detached = FALSE
///     };
/// } JSVarRef;
/// </code>
/// <para>
/// The lifecycle of a var ref:
/// <list type="number">
/// <item>Initially, the variable is on the stack and <see cref="IsDetached"/> is false</item>
/// <item>When the outer function returns, <see cref="Detach"/> is called</item>
/// <item>The value is copied from the stack and <see cref="IsDetached"/> becomes true</item>
/// <item>The closure can still access the value through this var ref</item>
/// </list>
/// </para>
/// <para>
/// Example:
/// <code>
/// function outer() {
///     let x = 10;           // x is on the stack
///     return function() {
///         return x;         // x is captured by var ref
///     };
/// }
/// let f = outer();          // outer returns, x is detached
/// f();                      // returns 10 via detached var ref
/// </code>
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class JSVarRef
{
    #region Fields

    /// <summary>
    /// The actual value storage for detached variables.
    /// </summary>
    private JSValue _value;

    /// <summary>
    /// The index in the source stack frame's variable array (when not detached).
    /// </summary>
    private int _varIndex;

    /// <summary>
    /// Whether the variable has been detached from the stack.
    /// </summary>
    private bool _isDetached;

    /// <summary>
    /// Whether this is a lexical variable (let/const at global scope).
    /// </summary>
    private bool _isLexical;

    /// <summary>
    /// Whether this variable is const (cannot be reassigned).
    /// </summary>
    private bool _isConst;

    /// <summary>
    /// Reference to the source stack frame's variables (when not detached).
    /// </summary>
    private JSValue[]? _stackVars;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new var ref pointing to a variable on the stack.
    /// </summary>
    /// <param name="stackVars">The stack frame's variable array.</param>
    /// <param name="varIndex">The index of the variable in the array.</param>
    /// <param name="isLexical">Whether this is a lexical (let/const) variable.</param>
    /// <param name="isConst">Whether this variable is const.</param>
    public JSVarRef(JSValue[] stackVars, int varIndex, bool isLexical = false, bool isConst = false)
    {
        _stackVars = stackVars;
        _varIndex = varIndex;
        _isLexical = isLexical;
        _isConst = isConst;
        _isDetached = false;
        _value = JSValue.Undefined;
    }

    /// <summary>
    /// Creates a detached var ref with an immediate value.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="isLexical">Whether this is a lexical (let/const) variable.</param>
    /// <param name="isConst">Whether this variable is const.</param>
    public JSVarRef(JSValue value, bool isLexical = false, bool isConst = false)
    {
        _value = value;
        _isDetached = true;
        _isLexical = isLexical;
        _isConst = isConst;
        _stackVars = null;
        _varIndex = -1;
    }

    /// <summary>
    /// Creates a default var ref (detached, undefined).
    /// </summary>
    public JSVarRef()
    {
        _value = JSValue.Undefined;
        _isDetached = true;
        _isLexical = false;
        _isConst = false;
        _stackVars = null;
        _varIndex = -1;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether this var ref has been detached from the stack.
    /// </summary>
    /// <remarks>
    /// When a function returns, captured variables are detached from the stack
    /// and their values are stored directly in the var ref.
    /// </remarks>
    public bool IsDetached => _isDetached;

    /// <summary>
    /// Gets whether this is a lexical (let/const) variable.
    /// </summary>
    /// <remarks>
    /// Lexical variables have different scoping rules and can be in the
    /// "temporal dead zone" before initialization.
    /// </remarks>
    public bool IsLexical => _isLexical;

    /// <summary>
    /// Gets whether this variable is const (cannot be reassigned).
    /// </summary>
    public bool IsConst => _isConst;

    /// <summary>
    /// Gets or sets the variable's value.
    /// </summary>
    /// <remarks>
    /// When not detached, reads from and writes to the stack frame.
    /// When detached, reads from and writes to the internal value storage.
    /// Setting a const variable will have no effect (the value is ignored).
    /// </remarks>
    public JSValue Value
    {
        get
        {
            if (_isDetached)
            {
                return _value;
            }
            else if (_stackVars != null && _varIndex >= 0 && _varIndex < _stackVars.Length)
            {
                return _stackVars[_varIndex];
            }
            else
            {
                return JSValue.Undefined;
            }
        }
        set
        {
            // Const variables cannot be reassigned
            if (_isConst)
            {
                return;
            }

            if (_isDetached)
            {
                _value = value;
            }
            else if (_stackVars != null && _varIndex >= 0 && _varIndex < _stackVars.Length)
            {
                _stackVars[_varIndex] = value;
            }
        }
    }

    /// <summary>
    /// Gets the variable index (only valid when not detached).
    /// </summary>
    public int VarIndex => _varIndex;

    #endregion

    #region Methods

    /// <summary>
    /// Detaches the var ref from the stack, copying the current value.
    /// </summary>
    /// <remarks>
    /// This is called when the function that owns the stack frame returns.
    /// After detaching, the var ref holds its own copy of the value.
    /// </remarks>
    public void Detach()
    {
        if (!_isDetached)
        {
            // Copy the value from the stack
            if (_stackVars != null && _varIndex >= 0 && _varIndex < _stackVars.Length)
            {
                _value = _stackVars[_varIndex];
            }
            else
            {
                _value = JSValue.Undefined;
            }

            // Clear the stack reference
            _stackVars = null;
            _varIndex = -1;
            _isDetached = true;
        }
    }

    /// <summary>
    /// Sets the value, throwing if this is a const variable.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns><c>true</c> if the value was set; <c>false</c> if this is a const variable.</returns>
    public bool TrySetValue(JSValue value)
    {
        if (_isConst)
        {
            return false;
        }

        Value = value;
        return true;
    }

    /// <summary>
    /// Creates a copy of this var ref, detached with the current value.
    /// </summary>
    /// <returns>A new detached var ref with the current value.</returns>
    public JSVarRef Clone()
    {
        return new JSVarRef(Value, _isLexical, _isConst);
    }

    /// <summary>
    /// Gets a debug string representation.
    /// </summary>
    private string DebuggerDisplay
    {
        get
        {
            var constStr = _isConst ? "const " : "";
            var lexStr = _isLexical ? "lexical " : "";
            var detachStr = _isDetached ? "detached" : $"stack[{_varIndex}]";
            return $"JSVarRef {{ {constStr}{lexStr}{detachStr}, Value = {Value} }}";
        }
    }

    #endregion
}
