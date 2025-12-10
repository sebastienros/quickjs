// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace QuickJS;

/// <summary>
/// Delegate type for native C# functions that can be called from JavaScript.
/// </summary>
/// <param name="thisArg">The <c>this</c> value for the call.</param>
/// <param name="args">The arguments passed to the function.</param>
/// <returns>The return value.</returns>
public delegate JSValue JSCFunction(JSValue thisArg, JSValue[] args);

/// <summary>
/// Delegate type for native C# functions with a magic value for overloading.
/// </summary>
/// <param name="thisArg">The <c>this</c> value for the call.</param>
/// <param name="args">The arguments passed to the function.</param>
/// <param name="magic">An integer "magic" value for distinguishing overloads.</param>
/// <returns>The return value.</returns>
public delegate JSValue JSCFunctionMagic(JSValue thisArg, JSValue[] args, int magic);

/// <summary>
/// Represents a JavaScript function object.
/// </summary>
/// <remarks>
/// <para>
/// A JavaScript function is an object that can be called. Functions in JavaScript
/// are first-class values - they can be assigned to variables, passed as arguments,
/// and returned from other functions.
/// </para>
/// <para>
/// This class mirrors QuickJS's function handling, which supports three main types:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>Bytecode Functions</term>
/// <description>
/// Functions compiled from JavaScript source code. They have a <see cref="JSFunctionDef"/>
/// containing the bytecode and can close over variables from outer scopes.
/// </description>
/// </item>
/// <item>
/// <term>C/Native Functions</term>
/// <description>
/// Functions implemented in C# (or C in QuickJS). These use <see cref="JSCFunction"/>
/// delegates and are typically used for built-in functions like <c>console.log</c>.
/// </description>
/// </item>
/// <item>
/// <term>Bound Functions</term>
/// <description>
/// Functions created by <c>Function.prototype.bind()</c>. They wrap another function
/// with a fixed <c>this</c> value and optionally prepended arguments.
/// </description>
/// </item>
/// </list>
/// <para>
/// In QuickJS, function data is stored in the JSObject union:
/// </para>
/// <code>
/// struct { /* JS_CLASS_BYTECODE_FUNCTION */
///     struct JSFunctionBytecode *function_bytecode;
///     JSVarRef **var_refs;
///     JSObject *home_object; /* for 'super' access */
/// } func;
/// 
/// struct { /* JS_CLASS_C_FUNCTION */
///     JSContext *realm;
///     JSCFunctionType c_function;
///     uint8_t length;
///     uint8_t cproto;
///     int16_t magic;
/// } cfunc;
/// </code>
/// <para>
/// This C# implementation extends <see cref="JSObject"/> and stores the function-specific
/// data directly as fields.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class JSFunction : JSObject
{
    #region Fields

    // The function definition (bytecode, metadata). Null for native functions.
    private readonly JSFunctionDef? _functionDef;

    // The native C# function delegate. Null for bytecode functions.
    private readonly JSCFunction? _nativeFunction;

    // The native function with magic value. Null for most functions.
    private readonly JSCFunctionMagic? _nativeFunctionMagic;

    // The magic value for native functions (used to distinguish overloads).
    private readonly int _magic;

    // Closure variable references - captured variables from outer scopes.
    private readonly JSVarRef[]? _varRefs;

    // The home object for super references (used by methods).
    private JSObject? _homeObject;

    // For bound functions: the target function being wrapped.
    private readonly JSFunction? _boundTarget;

    // For bound functions: the fixed 'this' value.
    private readonly JSValue _boundThis;

    // For bound functions: prepended arguments.
    private readonly JSValue[]? _boundArgs;

    // The function name (may differ from FunctionDef.FuncName for anonymous functions).
    private string? _name;

    // The declared length (number of parameters).
    private int _length;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a bytecode function from a function definition.
    /// </summary>
    /// <param name="functionDef">The compiled function definition.</param>
    /// <param name="varRefs">Captured variable references from outer scopes.</param>
    /// <param name="prototype">The function's prototype (usually Function.prototype).</param>
    public JSFunction(JSFunctionDef functionDef, JSVarRef[]? varRefs = null, JSObject? prototype = null)
        : base(prototype, JSClassId.BytecodeFunction)
    {
        _functionDef = functionDef ?? throw new ArgumentNullException(nameof(functionDef));
        _varRefs = varRefs;
        _boundThis = JSValue.Undefined;
        _length = functionDef.ArgCount;

        // Set the name from the function definition
        if (!functionDef.FuncName.IsEmpty)
        {
            _name = functionDef.FuncName.ToString();
        }
    }

    /// <summary>
    /// Creates a native (C#) function.
    /// </summary>
    /// <param name="function">The native function delegate.</param>
    /// <param name="name">The function name.</param>
    /// <param name="length">The declared number of parameters.</param>
    /// <param name="prototype">The function's prototype (usually Function.prototype).</param>
    public JSFunction(JSCFunction function, string? name = null, int length = 0, JSObject? prototype = null)
        : base(prototype, JSClassId.CFunction)
    {
        _nativeFunction = function ?? throw new ArgumentNullException(nameof(function));
        _name = name;
        _length = length;
        _boundThis = JSValue.Undefined;
    }

    /// <summary>
    /// Creates a native (C#) function with a magic value.
    /// </summary>
    /// <param name="function">The native function delegate with magic.</param>
    /// <param name="magic">The magic value for this function.</param>
    /// <param name="name">The function name.</param>
    /// <param name="length">The declared number of parameters.</param>
    /// <param name="prototype">The function's prototype (usually Function.prototype).</param>
    public JSFunction(JSCFunctionMagic function, int magic, string? name = null, int length = 0, JSObject? prototype = null)
        : base(prototype, JSClassId.CFunction)
    {
        _nativeFunctionMagic = function ?? throw new ArgumentNullException(nameof(function));
        _magic = magic;
        _name = name;
        _length = length;
        _boundThis = JSValue.Undefined;
    }

    /// <summary>
    /// Creates a bound function (internal constructor).
    /// </summary>
    private JSFunction(JSFunction target, JSValue boundThis, JSValue[]? boundArgs, JSObject? prototype)
        : base(prototype, JSClassId.BoundFunction)
    {
        _boundTarget = target ?? throw new ArgumentNullException(nameof(target));
        _boundThis = boundThis;
        _boundArgs = boundArgs;

        // Per spec, bound function's length is target.length - bound args count
        int targetLength = target.Length;
        int boundArgsCount = boundArgs?.Length ?? 0;
        _length = Math.Max(0, targetLength - boundArgsCount);

        // Name is "bound " + target name
        _name = "bound " + (target.Name ?? "");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the function definition for bytecode functions, or <c>null</c> for native/bound functions.
    /// </summary>
    public JSFunctionDef? FunctionDef => _functionDef;

    /// <summary>
    /// Gets the native function delegate, or <c>null</c> for bytecode/bound functions.
    /// </summary>
    public JSCFunction? NativeFunction => _nativeFunction;

    /// <summary>
    /// Gets the native function with magic delegate, or <c>null</c> if not applicable.
    /// </summary>
    public JSCFunctionMagic? NativeFunctionMagic => _nativeFunctionMagic;

    /// <summary>
    /// Gets the magic value for native functions.
    /// </summary>
    public int Magic => _magic;

    /// <summary>
    /// Gets the closure variable references.
    /// </summary>
    public JSVarRef[]? VarRefs => _varRefs;

    /// <summary>
    /// Gets or sets the home object for super references.
    /// </summary>
    /// <remarks>
    /// The home object is set when a method is defined on an object literal or class.
    /// It's used to resolve <c>super.property</c> and <c>super.method()</c> references.
    /// </remarks>
    public JSObject? HomeObject
    {
        get => _homeObject;
        set => _homeObject = value;
    }

    /// <summary>
    /// Gets the bound target function, or <c>null</c> if not a bound function.
    /// </summary>
    public JSFunction? BoundTarget => _boundTarget;

    /// <summary>
    /// Gets the bound <c>this</c> value, or <c>undefined</c> if not a bound function.
    /// </summary>
    public JSValue BoundThis => _boundThis;

    /// <summary>
    /// Gets the bound arguments, or <c>null</c> if none.
    /// </summary>
    public JSValue[]? BoundArgs => _boundArgs;

    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string? Name => _name;

    /// <summary>
    /// Gets the declared number of parameters (the "length" property).
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets whether this is a bytecode function.
    /// </summary>
    public bool IsBytecodeFunction => _functionDef != null;

    /// <summary>
    /// Gets whether this is a native (C#) function.
    /// </summary>
    public bool IsNativeFunction => _nativeFunction != null || _nativeFunctionMagic != null;

    /// <summary>
    /// Gets whether this is a bound function.
    /// </summary>
    public bool IsBoundFunction => _boundTarget != null;

    /// <summary>
    /// Gets whether this function is strict mode.
    /// </summary>
    public bool IsStrict => _functionDef?.IsStrict ?? false;

    /// <summary>
    /// Gets whether this function is an arrow function.
    /// </summary>
    public bool IsArrowFunction => _functionDef?.FuncType == JSParseFunctionType.Arrow;

    /// <summary>
    /// Gets whether this function is a generator.
    /// </summary>
    public bool IsGenerator => _functionDef?.FuncKind == JSFunctionKind.Generator ||
                               _functionDef?.FuncKind == JSFunctionKind.AsyncGenerator;

    /// <summary>
    /// Gets whether this function is async.
    /// </summary>
    public bool IsAsync => _functionDef?.FuncKind == JSFunctionKind.Async ||
                           _functionDef?.FuncKind == JSFunctionKind.AsyncGenerator;

    /// <summary>
    /// Gets whether this function has a prototype property.
    /// </summary>
    /// <remarks>
    /// Arrow functions and methods don't have their own prototype property.
    /// Regular function declarations and expressions do.
    /// </remarks>
    public bool HasPrototypeProperty => _functionDef?.HasPrototype ?? (!IsArrowFunction && !IsBoundFunction);

    /// <summary>
    /// Gets whether this function has its own <c>this</c> binding.
    /// </summary>
    /// <remarks>
    /// Arrow functions don't have their own <c>this</c> - they inherit from the enclosing scope.
    /// </remarks>
    public bool HasThisBinding => _functionDef?.HasThisBinding ?? !IsArrowFunction;

    #endregion

    #region Closure Support

    /// <summary>
    /// Gets a captured variable reference by index.
    /// </summary>
    /// <param name="index">The index in the closure variable array.</param>
    /// <returns>The var ref, or <c>null</c> if the index is invalid.</returns>
    public JSVarRef? GetVarRef(int index)
    {
        if (_varRefs != null && index >= 0 && index < _varRefs.Length)
        {
            return _varRefs[index];
        }
        return null;
    }

    /// <summary>
    /// Gets the value of a captured variable by index.
    /// </summary>
    /// <param name="index">The index in the closure variable array.</param>
    /// <returns>The value, or <c>undefined</c> if the index is invalid.</returns>
    public JSValue GetClosureValue(int index)
    {
        var varRef = GetVarRef(index);
        return varRef?.Value ?? JSValue.Undefined;
    }

    /// <summary>
    /// Sets the value of a captured variable by index.
    /// </summary>
    /// <param name="index">The index in the closure variable array.</param>
    /// <param name="value">The value to set.</param>
    /// <returns><c>true</c> if the value was set; <c>false</c> if the index is invalid or the variable is const.</returns>
    public bool SetClosureValue(int index, in JSValue value)
    {
        var varRef = GetVarRef(index);
        if (varRef != null)
        {
            return varRef.TrySetValue(value);
        }
        return false;
    }

    #endregion

    #region Bind

    /// <summary>
    /// Creates a new bound function with a fixed <c>this</c> value.
    /// </summary>
    /// <param name="thisArg">The value to use as <c>this</c> when calling the function.</param>
    /// <returns>A new bound function.</returns>
    public JSFunction Bind(in JSValue thisArg)
    {
        return Bind(thisArg, null);
    }

    /// <summary>
    /// Creates a new bound function with a fixed <c>this</c> value and prepended arguments.
    /// </summary>
    /// <param name="thisArg">The value to use as <c>this</c> when calling the function.</param>
    /// <param name="boundArgs">Arguments to prepend to each call.</param>
    /// <returns>A new bound function.</returns>
    public JSFunction Bind(in JSValue thisArg, params JSValue[]? boundArgs)
    {
        // If this is already a bound function, we bind to the underlying target
        // but combine the bound this and args
        if (_boundTarget != null)
        {
            // The outer bind's thisArg is ignored for already-bound functions
            // Combine the bound args
            JSValue[]? combinedArgs = null;
            if (_boundArgs != null || boundArgs != null)
            {
                int existingCount = _boundArgs?.Length ?? 0;
                int newCount = boundArgs?.Length ?? 0;
                combinedArgs = new JSValue[existingCount + newCount];
                if (_boundArgs != null)
                {
                    Array.Copy(_boundArgs, combinedArgs, existingCount);
                }
                if (boundArgs != null)
                {
                    Array.Copy(boundArgs, 0, combinedArgs, existingCount, newCount);
                }
            }

            return new JSFunction(_boundTarget, _boundThis, combinedArgs, Prototype);
        }

        // Copy the bound args to prevent external modification
        JSValue[]? argsCopy = null;
        if (boundArgs != null && boundArgs.Length > 0)
        {
            argsCopy = new JSValue[boundArgs.Length];
            Array.Copy(boundArgs, argsCopy, boundArgs.Length);
        }

        return new JSFunction(this, thisArg, argsCopy, Prototype);
    }

    #endregion

    #region Call Support

    /// <summary>
    /// Resolves the actual target function and adjusted arguments for a call.
    /// </summary>
    /// <remarks>
    /// For bound functions, this unwraps to the underlying target and prepends bound arguments.
    /// </remarks>
    /// <param name="thisArg">The <c>this</c> value passed to the call.</param>
    /// <param name="args">The arguments passed to the call.</param>
    /// <param name="resolvedThis">The resolved <c>this</c> value.</param>
    /// <param name="resolvedArgs">The resolved arguments (including bound args).</param>
    /// <returns>The ultimate target function to call.</returns>
    public JSFunction ResolveForCall(JSValue thisArg, JSValue[] args, out JSValue resolvedThis, out JSValue[] resolvedArgs)
    {
        if (_boundTarget != null)
        {
            // Use bound this
            resolvedThis = _boundThis;

            // Prepend bound args
            if (_boundArgs != null && _boundArgs.Length > 0)
            {
                resolvedArgs = new JSValue[_boundArgs.Length + args.Length];
                Array.Copy(_boundArgs, resolvedArgs, _boundArgs.Length);
                Array.Copy(args, 0, resolvedArgs, _boundArgs.Length, args.Length);
            }
            else
            {
                resolvedArgs = args;
            }

            // Recursively resolve in case target is also bound
            return _boundTarget.ResolveForCall(resolvedThis, resolvedArgs, out resolvedThis, out resolvedArgs);
        }

        // Not a bound function
        resolvedThis = thisArg;
        resolvedArgs = args;
        return this;
    }

    /// <summary>
    /// Invokes a native function with the specified <c>this</c> value and arguments.
    /// </summary>
    /// <param name="thisArg">The <c>this</c> value.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The return value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this is not a native function.</exception>
    public JSValue CallNative(JSValue thisArg, JSValue[] args)
    {
        // Resolve bound functions first
        var target = ResolveForCall(thisArg, args, out var resolvedThis, out var resolvedArgs);

        if (target._nativeFunction != null)
        {
            return target._nativeFunction(resolvedThis, resolvedArgs);
        }
        else if (target._nativeFunctionMagic != null)
        {
            return target._nativeFunctionMagic(resolvedThis, resolvedArgs, target._magic);
        }
        else
        {
            throw new InvalidOperationException("Cannot call a non-native function with CallNative. Use the VM for bytecode functions.");
        }
    }


    #endregion

    #region Function Creation Helpers

    /// <summary>
    /// Creates a function from a function definition with captured variables.
    /// </summary>
    /// <param name="functionDef">The function definition.</param>
    /// <param name="outerVarRefs">Variable references from the outer function.</param>
    /// <param name="prototype">The function prototype.</param>
    /// <returns>The new function.</returns>
    public static JSFunction CreateFromDef(JSFunctionDef functionDef, JSVarRef[]? outerVarRefs = null, JSObject? prototype = null)
    {
        // Create var refs for this function's closure variables
        JSVarRef[]? varRefs = null;

        if (functionDef.ClosureVars.Count > 0 && outerVarRefs != null)
        {
            var closureVars = functionDef.ClosureVars;
            varRefs = new JSVarRef[closureVars.Count];

            for (int i = 0; i < closureVars.Count; i++)
            {
                var closureVar = closureVars[i];

                // The closure var's var_idx refers to the parent's var_refs array
                if (closureVar.VarIndex >= 0 && closureVar.VarIndex < outerVarRefs.Length)
                {
                    varRefs[i] = outerVarRefs[closureVar.VarIndex];
                }
                else
                {
                    varRefs[i] = new JSVarRef(); // Default undefined
                }
            }
        }

        return new JSFunction(functionDef, varRefs, prototype);
    }

    /// <summary>
    /// Creates a native function.
    /// </summary>
    /// <param name="function">The native function delegate.</param>
    /// <param name="name">The function name.</param>
    /// <param name="length">The declared number of parameters.</param>
    /// <param name="prototype">The function prototype.</param>
    /// <returns>The new function.</returns>
    public static JSFunction CreateNative(JSCFunction function, string? name = null, int length = 0, JSObject? prototype = null)
    {
        return new JSFunction(function, name, length, prototype);
    }

    /// <summary>
    /// Creates a native function with a magic value.
    /// </summary>
    /// <param name="function">The native function delegate with magic.</param>
    /// <param name="magic">The magic value.</param>
    /// <param name="name">The function name.</param>
    /// <param name="length">The declared number of parameters.</param>
    /// <param name="prototype">The function prototype.</param>
    /// <returns>The new function.</returns>
    public static JSFunction CreateNativeWithMagic(JSCFunctionMagic function, int magic, string? name = null, int length = 0, JSObject? prototype = null)
    {
        return new JSFunction(function, magic, name, length, prototype);
    }

    #endregion

    #region Debug Support

    /// <summary>
    /// Gets a debug display string.
    /// </summary>
    private string DebuggerDisplay
    {
        get
        {
            string type;
            if (IsBoundFunction)
            {
                type = "bound";
            }
            else if (IsNativeFunction)
            {
                type = "native";
            }
            else if (IsArrowFunction)
            {
                type = "arrow";
            }
            else if (IsGenerator)
            {
                type = IsAsync ? "async generator" : "generator";
            }
            else if (IsAsync)
            {
                type = "async";
            }
            else
            {
                type = "function";
            }

            return $"[{type}] {Name ?? "(anonymous)"}({Length})";
        }
    }

    /// <summary>
    /// Returns a string representation of the function.
    /// </summary>
    public override string ToString()
    {
        return $"function {Name ?? ""}() {{ [native code] }}";
    }

    #endregion
}
