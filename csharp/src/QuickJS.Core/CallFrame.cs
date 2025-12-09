// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace QuickJS;

/// <summary>
/// Represents a call stack frame during JavaScript execution.
/// </summary>
/// <remarks>
/// <para>
/// Each function call creates a new CallFrame that tracks:
/// </para>
/// <list type="bullet">
/// <item><b>Local Variables</b>: Values of variables declared in the function</item>
/// <item><b>Arguments</b>: Values of parameters passed to the function</item>
/// <item><b>Closure References</b>: References to captured variables from outer scopes</item>
/// <item><b>Return Address</b>: Where to continue after the function returns</item>
/// </list>
/// <para>
/// Based on JSStackFrame from QuickJS (quickjs.c).
/// </para>
/// </remarks>
public sealed class CallFrame
{
    #region Fields

    /// <summary>The function being executed.</summary>
    private readonly JSFunctionDef? _function;

    /// <summary>Local variable storage.</summary>
    private readonly JSValue[] _locals;

    /// <summary>Argument storage.</summary>
    private readonly JSValue[] _args;

    /// <summary>Closure variable references.</summary>
    private readonly JSVarRef[] _varRefs;

    /// <summary>The 'this' value for this call.</summary>
    private JSValue _thisValue;

    /// <summary>The program counter (saved for return).</summary>
    private int _savedPC;

    /// <summary>The stack pointer (saved for return).</summary>
    private int _savedSP;

    /// <summary>The parent call frame.</summary>
    private readonly CallFrame? _parent;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new call frame for a function call.
    /// </summary>
    /// <param name="function">The function being called.</param>
    /// <param name="thisValue">The 'this' value.</param>
    /// <param name="args">The arguments passed to the function.</param>
    /// <param name="varRefs">Closure variable references.</param>
    /// <param name="parent">The parent call frame, if any.</param>
    public CallFrame(
        JSFunctionDef? function,
        JSValue thisValue,
        JSValue[]? args = null,
        JSVarRef[]? varRefs = null,
        CallFrame? parent = null)
    {
        _function = function;
        _thisValue = thisValue;
        _parent = parent;

        // Initialize locals array
        int localCount = function?.Vars?.Count ?? 0;
        _locals = localCount > 0 ? new JSValue[localCount] : Array.Empty<JSValue>();

        // Initialize locals to undefined
        for (int i = 0; i < _locals.Length; i++)
        {
            _locals[i] = JSValue.Undefined;
        }

        // Initialize arguments
        int argCount = function?.ArgCount ?? 0;
        _args = args ?? (argCount > 0 ? new JSValue[argCount] : Array.Empty<JSValue>());

        // Pad arguments with undefined if not enough were passed
        if (_args.Length < argCount)
        {
            var paddedArgs = new JSValue[argCount];
            Array.Copy(_args, paddedArgs, _args.Length);
            for (int i = _args.Length; i < argCount; i++)
            {
                paddedArgs[i] = JSValue.Undefined;
            }
            _args = paddedArgs;
        }

        // Initialize var refs
        _varRefs = varRefs ?? Array.Empty<JSVarRef>();
    }

    /// <summary>
    /// Creates a global/top-level call frame.
    /// </summary>
    /// <param name="localCount">Number of local variables.</param>
    /// <param name="argCount">Number of arguments.</param>
    /// <param name="varRefCount">Number of closure variable references.</param>
    public CallFrame(int localCount = 0, int argCount = 0, int varRefCount = 0)
    {
        _function = null;
        _thisValue = JSValue.Undefined;
        _parent = null;
        _locals = localCount > 0 ? new JSValue[localCount] : Array.Empty<JSValue>();
        _args = argCount > 0 ? new JSValue[argCount] : Array.Empty<JSValue>();
        _varRefs = varRefCount > 0 ? new JSVarRef[varRefCount] : Array.Empty<JSVarRef>();

        // Initialize locals to undefined
        for (int i = 0; i < _locals.Length; i++)
        {
            _locals[i] = JSValue.Undefined;
        }

        // Initialize args to undefined
        for (int i = 0; i < _args.Length; i++)
        {
            _args[i] = JSValue.Undefined;
        }

        // Initialize var refs
        for (int i = 0; i < _varRefs.Length; i++)
        {
            _varRefs[i] = new JSVarRef();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the function being executed, or null for global code.
    /// </summary>
    public JSFunctionDef? Function => _function;

    /// <summary>
    /// Gets the parent call frame.
    /// </summary>
    public CallFrame? Parent => _parent;

    /// <summary>
    /// Gets or sets the 'this' value for this call.
    /// </summary>
    public JSValue ThisValue
    {
        get => _thisValue;
        set => _thisValue = value;
    }

    /// <summary>
    /// Gets or sets the saved program counter.
    /// </summary>
    public int SavedPC
    {
        get => _savedPC;
        set => _savedPC = value;
    }

    /// <summary>
    /// Gets or sets the saved stack pointer.
    /// </summary>
    public int SavedSP
    {
        get => _savedSP;
        set => _savedSP = value;
    }

    /// <summary>
    /// Gets the number of local variables.
    /// </summary>
    public int LocalCount => _locals.Length;

    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int ArgCount => _args.Length;

    /// <summary>
    /// Gets the number of var refs.
    /// </summary>
    public int VarRefCount => _varRefs.Length;

    /// <summary>
    /// Gets direct access to the locals array for var ref creation.
    /// </summary>
    internal JSValue[] LocalsArray => _locals;

    /// <summary>
    /// Gets direct access to the args array.
    /// </summary>
    internal JSValue[] ArgsArray => _args;

    #endregion

    #region Local Variable Access

    /// <summary>
    /// Gets a local variable by index.
    /// </summary>
    /// <param name="index">The local variable index.</param>
    /// <returns>The variable value.</returns>
    public JSValue GetLocal(int index)
    {
        if (index >= 0 && index < _locals.Length)
        {
            return _locals[index];
        }
        return JSValue.Undefined;
    }

    /// <summary>
    /// Sets a local variable by index.
    /// </summary>
    /// <param name="index">The local variable index.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if successful, false if index out of range.</returns>
    public bool SetLocal(int index, JSValue value)
    {
        if (index >= 0 && index < _locals.Length)
        {
            _locals[index] = value;
            return true;
        }
        return false;
    }

    #endregion

    #region Argument Access

    /// <summary>
    /// Gets an argument by index.
    /// </summary>
    /// <param name="index">The argument index.</param>
    /// <returns>The argument value.</returns>
    public JSValue GetArg(int index)
    {
        if (index >= 0 && index < _args.Length)
        {
            return _args[index];
        }
        return JSValue.Undefined;
    }

    /// <summary>
    /// Sets an argument by index.
    /// </summary>
    /// <param name="index">The argument index.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if successful, false if index out of range.</returns>
    public bool SetArg(int index, JSValue value)
    {
        if (index >= 0 && index < _args.Length)
        {
            _args[index] = value;
            return true;
        }
        return false;
    }

    #endregion

    #region Closure Variable Access

    /// <summary>
    /// Gets a closure variable reference by index.
    /// </summary>
    /// <param name="index">The var ref index.</param>
    /// <returns>The var ref, or null if index out of range.</returns>
    public JSVarRef? GetVarRef(int index)
    {
        if (index >= 0 && index < _varRefs.Length)
        {
            return _varRefs[index];
        }
        return null;
    }

    /// <summary>
    /// Sets a closure variable reference by index.
    /// </summary>
    /// <param name="index">The var ref index.</param>
    /// <param name="varRef">The var ref to set.</param>
    /// <returns>True if successful, false if index out of range.</returns>
    public bool SetVarRef(int index, JSVarRef varRef)
    {
        if (index >= 0 && index < _varRefs.Length)
        {
            _varRefs[index] = varRef;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the value of a closure variable by index.
    /// </summary>
    /// <param name="index">The var ref index.</param>
    /// <returns>The variable value.</returns>
    public JSValue GetVarRefValue(int index)
    {
        if (index >= 0 && index < _varRefs.Length && _varRefs[index] != null)
        {
            return _varRefs[index].Value;
        }
        return JSValue.Undefined;
    }

    /// <summary>
    /// Sets the value of a closure variable by index.
    /// </summary>
    /// <param name="index">The var ref index.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if successful.</returns>
    public bool SetVarRefValue(int index, JSValue value)
    {
        if (index >= 0 && index < _varRefs.Length && _varRefs[index] != null)
        {
            _varRefs[index].Value = value;
            return true;
        }
        return false;
    }

    #endregion

    #region Frame Management

    /// <summary>
    /// Detaches all var refs that point to this frame's locals.
    /// Called when the function returns.
    /// </summary>
    public void DetachVarRefs()
    {
        // Find all var refs that reference our locals and detach them
        // This is typically done through the closure's var ref list
        foreach (var varRef in _varRefs)
        {
            varRef?.Detach();
        }
    }

    /// <summary>
    /// Creates a var ref for a local variable.
    /// </summary>
    /// <param name="localIndex">The local variable index.</param>
    /// <param name="isLexical">Whether this is a lexical (let/const) variable.</param>
    /// <param name="isConst">Whether this variable is const.</param>
    /// <returns>The var ref.</returns>
    public JSVarRef CreateVarRefForLocal(int localIndex, bool isLexical = false, bool isConst = false)
    {
        return new JSVarRef(_locals, localIndex, isLexical, isConst);
    }

    #endregion
}
