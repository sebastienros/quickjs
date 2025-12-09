// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript execution context.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="JSContext"/> is an isolated JavaScript execution environment within a
/// <see cref="JSRuntime"/>. Each context has its own:
/// </para>
/// <list type="bullet">
/// <item><description>Global object with built-in properties and functions</description></item>
/// <item><description>Class prototypes (Object.prototype, Array.prototype, etc.)</description></item>
/// <item><description>Loaded modules</description></item>
/// <item><description>Random state for Math.random()</description></item>
/// </list>
/// <para>
/// Multiple contexts in the same runtime share:
/// </para>
/// <list type="bullet">
/// <item><description>The atom table (interned strings)</description></item>
/// <item><description>Class definitions</description></item>
/// <item><description>Memory limits and tracking</description></item>
/// </list>
/// <para>
/// This class mirrors QuickJS's <c>struct JSContext</c>:
/// </para>
/// <code>
/// struct JSContext {
///     JSGCObjectHeader header;           // GC header
///     JSRuntime *rt;                     // Parent runtime
///     JSValue *class_proto;              // Class prototypes
///     JSValue global_obj;                // Global object
///     JSValue global_var_obj;            // Global let/const
///     struct list_head loaded_modules;   // Loaded modules
///     uint64_t random_state;             // Random state
///     int interrupt_counter;             // Interrupt checking
///     ...
/// };
/// </code>
/// <para>
/// Contexts are created via <see cref="JSRuntime.CreateContext"/> and should be
/// disposed when no longer needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var runtime = new JSRuntime();
/// using var context = runtime.CreateContext();
/// 
/// // Set a global variable
/// context.SetGlobalProperty("x", JSValue.FromInt32(42));
/// 
/// // Get the global object
/// var global = context.GlobalObject;
/// </code>
/// </example>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class JSContext : IDisposable
{
    #region Fields

    // The parent runtime
    private readonly JSRuntime _runtime;

    // The global object
    private JSObject _globalObject;

    // Global variable object for let/const at top level
    private JSObject _globalVarObject;

    // Class prototypes indexed by class ID
    private readonly JSObject?[] _classPrototypes;

    // Loaded modules
    private readonly Dictionary<string, JSModuleDef> _loadedModules;

    // Random state for Math.random()
    private ulong _randomState;

    // Error prototypes by error type
    private readonly Dictionary<JSErrorType, JSObject> _errorPrototypes = new();

    // Current exception for this context (separate from runtime exception)
    private JSValue _currentException = JSValue.Undefined;
    private bool _hasException;

    // Microtask queue for Promises
    private readonly Queue<Action> _microtasks = new();

    // Disposal tracking
    private bool _isDisposed;

    // User-defined opaque data
    private object? _userOpaque;

    // Strict mode flag
    private bool _strictMode;

    // The current stack frame (for call stack tracking)
    private JSCallFrame? _currentStackFrame;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new context within the specified runtime.
    /// </summary>
    /// <param name="runtime">The parent runtime.</param>
    /// <exception cref="ArgumentNullException">Thrown if runtime is null.</exception>
    internal JSContext(JSRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        // Initialize class prototypes array
        _classPrototypes = new JSObject?[runtime.ClassCount];

        // Initialize modules dictionary
        _loadedModules = new Dictionary<string, JSModuleDef>(StringComparer.Ordinal);

        // Initialize random state
        _randomState = (ulong)DateTime.UtcNow.Ticks;

        // Create global objects
        _globalObject = new JSObject(null, JSClassId.GlobalObject);
        _globalVarObject = new JSObject(null, JSClassId.Object);

        // Initialize basic objects
        InitializeBasicObjects();

        // Install built-ins (Object, Function, etc.)
        InitializeBuiltins();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the parent runtime.
    /// </summary>
    public JSRuntime Runtime => _runtime;

    /// <summary>
    /// Gets the global object.
    /// </summary>
    public JSObject GlobalObject => _globalObject;

    /// <summary>
    /// Gets the global variable object (for let/const declarations at top level).
    /// </summary>
    public JSObject GlobalVarObject => _globalVarObject;

    /// <summary>
    /// Gets a value indicating whether this context is in strict mode.
    /// </summary>
    public bool StrictMode
    {
        get => _strictMode;
        set => _strictMode = value;
    }

    /// <summary>
    /// Gets a value indicating whether this context has an unhandled exception.
    /// </summary>
    public bool HasException => _hasException;

    /// <summary>
    /// Gets the number of loaded modules.
    /// </summary>
    public int LoadedModuleCount => _loadedModules.Count;

    /// <summary>
    /// Gets or sets user-defined data associated with this context.
    /// </summary>
    public object? UserOpaque
    {
        get => _userOpaque;
        set => _userOpaque = value;
    }

    /// <summary>
    /// Gets a value indicating whether this context has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Gets the current call frame.
    /// </summary>
    internal JSCallFrame? CurrentCallFrame => _currentStackFrame;

    #endregion

    #region Class Prototypes

    /// <summary>
    /// Sets the prototype for a class.
    /// </summary>
    /// <param name="classId">The class ID.</param>
    /// <param name="prototype">The prototype object.</param>
    public void SetClassPrototype(JSClassId classId, JSObject? prototype)
    {
        ThrowIfDisposed();

        int index = (int)classId;
        if (index < 0 || index >= _classPrototypes.Length)
            throw new ArgumentOutOfRangeException(nameof(classId));

        _classPrototypes[index] = prototype;
    }

    /// <summary>
    /// Gets the prototype for a class.
    /// </summary>
    /// <param name="classId">The class ID.</param>
    /// <returns>The prototype object, or null if not set.</returns>
    public JSObject? GetClassPrototype(JSClassId classId)
    {
        int index = (int)classId;
        if (index < 0 || index >= _classPrototypes.Length)
            return null;

        return _classPrototypes[index];
    }

    #endregion

    #region Global Object

    /// <summary>
    /// Gets a property from the global object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value.</returns>
    public JSValue GetGlobalProperty(string propertyName)
    {
        ThrowIfDisposed();
        return _globalObject.Get(propertyName);
    }

    /// <summary>
    /// Sets a property on the global object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns><c>true</c> if the property was set successfully.</returns>
    public bool SetGlobalProperty(string propertyName, JSValue value)
    {
        ThrowIfDisposed();
        return _globalObject.Set(propertyName, value);
    }

    /// <summary>
    /// Defines a property on the global object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="descriptor">The property descriptor.</param>
    /// <returns><c>true</c> if the property was defined successfully.</returns>
    public bool DefineGlobalProperty(string propertyName, PropertyDescriptor descriptor)
    {
        ThrowIfDisposed();
        return _globalObject.DefineProperty(propertyName, descriptor);
    }

    /// <summary>
    /// Checks if the global object has a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns><c>true</c> if the property exists.</returns>
    public bool HasGlobalProperty(string propertyName)
    {
        ThrowIfDisposed();
        return _globalObject.HasProperty(propertyName);
    }

    /// <summary>
    /// Deletes a property from the global object.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns><c>true</c> if the property was deleted.</returns>
    public bool DeleteGlobalProperty(string propertyName)
    {
        ThrowIfDisposed();
        return _globalObject.Delete(propertyName);
    }

    /// <summary>
    /// Registers a native function on the global object.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The native function delegate.</param>
    /// <param name="length">The function's declared length (parameter count).</param>
    public void RegisterGlobalFunction(string name, JSCFunction function, int length = 0)
    {
        ThrowIfDisposed();

        var jsFunc = new JSFunction(function, name, length, GetClassPrototype(JSClassId.CFunction));
        _globalObject.Set(name, JSValue.FromObject(jsFunc));
    }

    /// <summary>
    /// Registers a native function with magic value on the global object.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The native function delegate with magic.</param>
    /// <param name="length">The function's declared length (parameter count).</param>
    /// <param name="magic">The magic value to pass to the function.</param>
    public void RegisterGlobalFunction(string name, JSCFunctionMagic function, int length, int magic)
    {
        ThrowIfDisposed();

        var jsFunc = new JSFunction(function, magic, name, length, GetClassPrototype(JSClassId.CFunction));
        _globalObject.Set(name, JSValue.FromObject(jsFunc));
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Sets the current exception for this context.
    /// </summary>
    /// <param name="exception">The exception value.</param>
    public void SetException(JSValue exception)
    {
        _currentException = exception;
        _hasException = true;
    }

    /// <summary>
    /// Gets and clears the current exception.
    /// </summary>
    /// <returns>The exception value, or <see cref="JSValue.Undefined"/> if none.</returns>
    public JSValue GetAndClearException()
    {
        if (!_hasException)
            return JSValue.Undefined;

        var exception = _currentException;
        _currentException = JSValue.Undefined;
        _hasException = false;
        return exception;
    }

    /// <summary>
    /// Clears the current exception.
    /// </summary>
    public void ClearException()
    {
        _currentException = JSValue.Undefined;
        _hasException = false;
    }

    /// <summary>
    /// Enqueues a microtask (used by Promises).
    /// </summary>
    internal void EnqueueMicrotask(Action action)
    {
        _microtasks.Enqueue(action);
    }

    /// <summary>
    /// Runs all queued microtasks.
    /// </summary>
    public void RunMicrotasks()
    {
        while (_microtasks.Count > 0)
        {
            var task = _microtasks.Dequeue();
            task();
        }
    }

    /// <summary>
    /// Throws a JavaScript error.
    /// </summary>
    /// <param name="errorType">The type of error.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A value representing the exception (typically <see cref="JSValue.Exception"/>).</returns>
    public JSValue ThrowError(JSErrorType errorType, string message)
    {
        // Create an error object to hold the exception info
        JSObject proto;
        if (!_errorPrototypes.TryGetValue(errorType, out proto!))
        {
            proto = GetClassPrototype(JSClassId.Error)!;
        }

        var errorObject = new JSObject(proto, JSClassId.Error);
        errorObject.Set("name", JSValue.FromString(errorType.ToString()));
        errorObject.Set("message", JSValue.FromString(message));
        SetException(JSValue.FromObject(errorObject));
        return JSValue.Exception;
    }

    /// <summary>
    /// Throws a TypeError.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>An exception value.</returns>
    public JSValue ThrowTypeError(string message) => ThrowError(JSErrorType.TypeError, message);

    /// <summary>
    /// Throws a ReferenceError.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>An exception value.</returns>
    public JSValue ThrowReferenceError(string message) => ThrowError(JSErrorType.ReferenceError, message);

    /// <summary>
    /// Throws a SyntaxError.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>An exception value.</returns>
    public JSValue ThrowSyntaxError(string message) => ThrowError(JSErrorType.SyntaxError, message);

    /// <summary>
    /// Throws a RangeError.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>An exception value.</returns>
    public JSValue ThrowRangeError(string message) => ThrowError(JSErrorType.RangeError, message);

    #endregion

    #region Random

    /// <summary>
    /// Gets the next random number (for Math.random()).
    /// </summary>
    /// <returns>A random double between 0 (inclusive) and 1 (exclusive).</returns>
    public double GetRandomValue()
    {
        // xorshift64* algorithm
        ulong s = _randomState;
        s ^= s >> 12;
        s ^= s << 25;
        s ^= s >> 27;
        _randomState = s;
        return (s * 0x2545F4914F6CDD1DUL) * (1.0 / 18446744073709551616.0);
    }

    /// <summary>
    /// Sets the random state (for seeding Math.random()).
    /// </summary>
    /// <param name="seed">The seed value.</param>
    public void SetRandomSeed(ulong seed)
    {
        _randomState = seed != 0 ? seed : 1;
    }

    #endregion

    #region Module Management

    /// <summary>
    /// Registers a module in this context.
    /// </summary>
    /// <param name="moduleName">The module specifier/name.</param>
    /// <param name="module">The module definition.</param>
    public void RegisterModule(string moduleName, JSModuleDef module)
    {
        ThrowIfDisposed();
        _loadedModules[moduleName] = module;
    }

    /// <summary>
    /// Gets a loaded module by name.
    /// </summary>
    /// <param name="moduleName">The module specifier/name.</param>
    /// <returns>The module definition, or null if not loaded.</returns>
    public JSModuleDef? GetModule(string moduleName)
    {
        _loadedModules.TryGetValue(moduleName, out var module);
        return module;
    }

    /// <summary>
    /// Checks if a module is loaded.
    /// </summary>
    /// <param name="moduleName">The module specifier/name.</param>
    /// <returns><c>true</c> if the module is loaded.</returns>
    public bool HasModule(string moduleName)
    {
        return _loadedModules.ContainsKey(moduleName);
    }

    /// <summary>
    /// Gets all loaded module names.
    /// </summary>
    /// <returns>An enumerable of module names.</returns>
    public IEnumerable<string> GetLoadedModuleNames()
    {
        return _loadedModules.Keys;
    }

    #endregion

    #region Stack Frame Management

    /// <summary>
    /// Pushes a new call frame.
    /// </summary>
    /// <param name="frame">The call frame to push.</param>
    internal void PushCallFrame(JSCallFrame frame)
    {
        frame.PreviousFrame = _currentStackFrame;
        _currentStackFrame = frame;
    }

    /// <summary>
    /// Pops the current call frame.
    /// </summary>
    /// <returns>The popped call frame.</returns>
    internal JSCallFrame? PopCallFrame()
    {
        var frame = _currentStackFrame;
        if (frame != null)
        {
            _currentStackFrame = frame.PreviousFrame;
        }
        return frame;
    }

    /// <summary>
    /// Gets the current call stack depth.
    /// </summary>
    /// <returns>The stack depth.</returns>
    public int GetStackDepth()
    {
        int depth = 0;
        var frame = _currentStackFrame;
        while (frame != null)
        {
            depth++;
            frame = frame.PreviousFrame;
        }
        return depth;
    }

    /// <summary>
    /// Gets a stack trace from the current execution point.
    /// </summary>
    /// <returns>A stack trace object.</returns>
    public JSStackTrace GetStackTrace()
    {
        var trace = new JSStackTrace();
        var frame = _currentStackFrame;

        while (frame != null)
        {
            var location = new SourceLocation(
                frame.FileName ?? "<unknown>",
                frame.LineNumber,
                frame.ColumnNumber
            );
            trace.AddFrame(new JSStackFrame(
                functionName: frame.FunctionName,
                location: location
            ));
            frame = frame.PreviousFrame;
        }

        return trace;
    }

    #endregion

    #region Atom Convenience Methods

    /// <summary>
    /// Interns a string and returns its atom.
    /// </summary>
    /// <param name="value">The string to intern.</param>
    /// <returns>The interned atom.</returns>
    public JSAtom InternAtom(string value)
    {
        return _runtime.InternAtom(value);
    }

    /// <summary>
    /// Gets the string value for an atom.
    /// </summary>
    /// <param name="atom">The atom.</param>
    /// <returns>The string value, or null if not found.</returns>
    public string? GetAtomString(JSAtom atom)
    {
        return _runtime.GetAtomString(atom);
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes basic objects (Object.prototype, etc.)
    /// </summary>
    private void InitializeBasicObjects()
    {
        // Create Object.prototype (has no prototype itself)
        var objectPrototype = new JSObject(null, JSClassId.Object);
        objectPrototype.SetImmutablePrototype();
        SetClassPrototype(JSClassId.Object, objectPrototype);

        // Create Function.prototype
        var functionPrototype = new JSObject(objectPrototype, JSClassId.Object);
        SetClassPrototype(JSClassId.CFunction, functionPrototype);
        SetClassPrototype(JSClassId.BytecodeFunction, functionPrototype);

        // Create Array.prototype
        var arrayPrototype = new JSObject(objectPrototype, JSClassId.Array);
        SetClassPrototype(JSClassId.Array, arrayPrototype);

        // Create Error.prototype
        var errorPrototype = new JSObject(objectPrototype, JSClassId.Error);
        SetClassPrototype(JSClassId.Error, errorPrototype);

        // Set up global object prototype chain
        _globalObject.SetPrototype(objectPrototype);
    }

    /// <summary>
    /// Installs built-in constructors and global objects.
    /// </summary>
    private void InitializeBuiltins()
    {
        InitializeObjectConstructor();
        InitializeFunctionConstructor();
        InitializeErrorConstructors();
        InitializeNumberAndMath();
        InitializeStringConstructor();
        InitializeArrayConstructor();
        InitializeRegExpConstructor();
        InitializeJSON();
        InitializePromise();
        InitializeCollections();
    }

    private void InitializeObjectConstructor()
    {
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        JSValue ObjectCtor(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (arg.IsObject)
                return arg; // return the object as-is

            if (arg.IsNull || arg.IsUndefined)
                return JSValue.FromObject(new JSObject(objectProto, JSClassId.Object));

            // Box primitive wrappers
            if (arg.IsString)
            {
                var obj = new JSObject(objectProto, JSClassId.String) { InternalValue = arg };
                return JSValue.FromObject(obj);
            }
            if (arg.IsNumber)
            {
                var obj = new JSObject(objectProto, JSClassId.Number) { InternalValue = arg };
                return JSValue.FromObject(obj);
            }
            if (arg.IsBool)
            {
                var obj = new JSObject(objectProto, JSClassId.Boolean) { InternalValue = arg };
                return JSValue.FromObject(obj);
            }

            return JSValue.FromObject(new JSObject(objectProto, JSClassId.Object));
        }

        var functionProto = GetClassPrototype(JSClassId.CFunction);
        var objectCtorFunc = new JSFunction(ObjectCtor, "Object", 1, functionProto);

        // Object.create(proto)
        JSValue ObjectCreate(JSValue thisVal, JSValue[] args)
        {
            var protoVal = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (protoVal.IsNull)
                return JSValue.FromObject(new JSObject(null, JSClassId.Object));
            if (!protoVal.IsObject)
                return ThrowTypeError("Object prototype may only be an Object or null");
            var protoObj = protoVal.AsObject();
            return JSValue.FromObject(new JSObject(protoObj, JSClassId.Object));
        }

        // Object.prototype.hasOwnProperty
        JSValue HasOwnProperty(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject)
                return JSValue.False;
            var key = args.Length > 0 ? args[0].ToString() : "";
            var obj = thisVal.AsObject();
            return JSValue.FromBoolean(obj.HasOwnProperty(key));
        }

        // Object.prototype.toString
        JSValue ObjectProtoToString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject)
                return JSValue.FromString("[object " + thisVal.Tag + "]");
            var obj = thisVal.AsObject();
            var className = obj.ClassId.ToString();
            return JSValue.FromString($"[object {className}]");
        }

        // Object.keys
        JSValue ObjectKeys(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return ThrowTypeError("Object.keys called on non-object");
            var obj = arg.AsObject();
            var result = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            uint i = 0;
            foreach (var kvp in obj.GetOwnProperties())
            {
                if (kvp.Value.IsEnumerable)
                {
                    result.Set(i++, JSValue.FromString(kvp.Key));
                }
            }
            return JSValue.FromObject(result);
        }

        // Object.values
        JSValue ObjectValues(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return ThrowTypeError("Object.values called on non-object");
            var obj = arg.AsObject();
            var result = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            uint i = 0;
            foreach (var kvp in obj.GetOwnProperties())
            {
                if (kvp.Value.IsEnumerable)
                {
                    result.Set(i++, kvp.Value.Value);
                }
            }
            return JSValue.FromObject(result);
        }

        // Object.entries
        JSValue ObjectEntries(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return ThrowTypeError("Object.entries called on non-object");
            var obj = arg.AsObject();
            var result = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            uint i = 0;
            foreach (var kvp in obj.GetOwnProperties())
            {
                if (kvp.Value.IsEnumerable)
                {
                    var entry = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
                    entry.Set(0u, JSValue.FromString(kvp.Key));
                    entry.Set(1u, kvp.Value.Value);
                    result.Set(i++, JSValue.FromObject(entry));
                }
            }
            return JSValue.FromObject(result);
        }

        // Object.assign
        JSValue ObjectAssign(JSValue thisVal, JSValue[] args)
        {
            if (args.Length == 0)
                return ThrowTypeError("Cannot convert undefined or null to object");
            var target = args[0];
            if (!target.IsObject)
                return ThrowTypeError("Object.assign target must be an object");
            var targetObj = target.AsObject();
            for (int i = 1; i < args.Length; i++)
            {
                var src = args[i];
                if (src.IsNull || src.IsUndefined)
                    continue;
                if (!src.IsObject)
                    continue;
                var srcObj = src.AsObject();
                foreach (var kvp in srcObj.GetOwnProperties())
                {
                    if (kvp.Value.IsEnumerable)
                    {
                        targetObj.Set(kvp.Key, kvp.Value.Value);
                    }
                }
            }
            return target;
        }

        // Object.getPrototypeOf
        JSValue ObjectGetPrototypeOf(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return ThrowTypeError("Object.getPrototypeOf called on non-object");
            var proto = arg.AsObject().Prototype;
            return proto != null ? JSValue.FromObject(proto) : JSValue.Null;
        }

        // Object.setPrototypeOf
        JSValue ObjectSetPrototypeOf(JSValue thisVal, JSValue[] args)
        {
            var obj = args.Length > 0 ? args[0] : JSValue.Undefined;
            var proto = args.Length > 1 ? args[1] : JSValue.Undefined;
            if (!obj.IsObject)
                return ThrowTypeError("Object.setPrototypeOf called on non-object");
            JSObject? protoObj = null;
            if (proto.IsObject)
                protoObj = proto.AsObject();
            else if (!proto.IsNull)
                return ThrowTypeError("Object prototype may only be an Object or null");
            if (!obj.AsObject().SetPrototype(protoObj))
                return ThrowTypeError("Cannot set prototype");
            return obj;
        }

        // Object.is (SameValue comparison)
        JSValue ObjectIs(JSValue thisVal, JSValue[] args)
        {
            var x = args.Length > 0 ? args[0] : JSValue.Undefined;
            var y = args.Length > 1 ? args[1] : JSValue.Undefined;
            return JSValue.FromBoolean(JSValueConversion.SameValue(x, y));
        }

        // Object.freeze
        JSValue ObjectFreeze(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return arg; // Primitives are returned as-is in ES6
            arg.AsObject().Freeze();
            return arg;
        }

        // Object.seal
        JSValue ObjectSeal(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return arg; // Primitives are returned as-is
            arg.AsObject().Seal();
            return arg;
        }

        // Object.preventExtensions
        JSValue ObjectPreventExtensions(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return arg;
            arg.AsObject().PreventExtensions();
            return arg;
        }

        // Object.isExtensible
        JSValue ObjectIsExtensible(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return JSValue.False;
            return JSValue.FromBoolean(arg.AsObject().IsExtensible);
        }

        // Object.isFrozen
        JSValue ObjectIsFrozen(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return JSValue.True; // Primitives are considered frozen
            return JSValue.FromBoolean(arg.AsObject().IsFrozen);
        }

        // Object.isSealed
        JSValue ObjectIsSealed(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return JSValue.True; // Primitives are considered sealed
            return JSValue.FromBoolean(arg.AsObject().IsSealed);
        }

        // Object.fromEntries
        JSValue ObjectFromEntries(JSValue thisVal, JSValue[] args)
        {
            var iterable = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!iterable.IsObject)
                return ThrowTypeError("Object.fromEntries requires an iterable");
            var result = new JSObject(objectProto, JSClassId.Object);
            var arr = iterable.AsObject();
            // Simple array-like iteration
            var length = arr.Get("length");
            if (length.IsNumber)
            {
                var len = length.ToInt32();
                for (int i = 0; i < len; i++)
                {
                    var entry = arr.Get((uint)i);
                    if (entry.IsObject)
                    {
                        var entryObj = entry.AsObject();
                        var key = JSValueConversion.ToString(entryObj.Get(0));
                        var value = entryObj.Get(1);
                        result.Set(key, value);
                    }
                }
            }
            return JSValue.FromObject(result);
        }

        // Object.hasOwn (ES2022)
        JSValue ObjectHasOwn(JSValue thisVal, JSValue[] args)
        {
            var obj = args.Length > 0 ? args[0] : JSValue.Undefined;
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";
            if (!obj.IsObject)
                return ThrowTypeError("Object.hasOwn called on non-object");
            return JSValue.FromBoolean(obj.AsObject().HasOwnProperty(key));
        }

        // Object.getOwnPropertyNames
        JSValue ObjectGetOwnPropertyNames(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!arg.IsObject)
                return ThrowTypeError("Object.getOwnPropertyNames called on non-object");
            var obj = arg.AsObject();
            var result = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            uint i = 0;
            foreach (var name in obj.GetOwnPropertyNames())
            {
                result.Set(i++, JSValue.FromString(name));
            }
            return JSValue.FromObject(result);
        }

        // Object.getOwnPropertyDescriptor
        JSValue ObjectGetOwnPropertyDescriptor(JSValue thisVal, JSValue[] args)
        {
            var obj = args.Length > 0 ? args[0] : JSValue.Undefined;
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";
            if (!obj.IsObject)
                return ThrowTypeError("Object.getOwnPropertyDescriptor called on non-object");
            var targetObj = obj.AsObject();
            if (!targetObj.TryGetOwnPropertyDescriptor(key, out var desc))
                return JSValue.Undefined;
            // Return a descriptor object
            var descObj = new JSObject(objectProto, JSClassId.Object);
            descObj.Set("value", desc.Value);
            descObj.Set("writable", JSValue.FromBoolean(desc.IsWritable));
            descObj.Set("enumerable", JSValue.FromBoolean(desc.IsEnumerable));
            descObj.Set("configurable", JSValue.FromBoolean(desc.IsConfigurable));
            return JSValue.FromObject(descObj);
        }

        // Object.defineProperty
        JSValue ObjectDefineProperty(JSValue thisVal, JSValue[] args)
        {
            var obj = args.Length > 0 ? args[0] : JSValue.Undefined;
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";
            var desc = args.Length > 2 ? args[2] : JSValue.Undefined;
            if (!obj.IsObject)
                return ThrowTypeError("Object.defineProperty called on non-object");
            if (!desc.IsObject)
                return ThrowTypeError("Property descriptor must be an object");
            var targetObj = obj.AsObject();
            var descObj = desc.AsObject();
            
            var value = descObj.Get("value");
            var writableVal = descObj.Get("writable");
            var enumerableVal = descObj.Get("enumerable");
            var configurableVal = descObj.Get("configurable");
            
            var flags = PropertyFlags.None;
            if (!writableVal.IsUndefined && writableVal.IsTrue)
                flags |= PropertyFlags.Writable;
            if (!enumerableVal.IsUndefined && enumerableVal.IsTrue)
                flags |= PropertyFlags.Enumerable;
            if (!configurableVal.IsUndefined && configurableVal.IsTrue)
                flags |= PropertyFlags.Configurable;
            
            var propDesc = PropertyDescriptor.Data(value, flags);
            targetObj.DefineProperty(key, propDesc);
            return obj;
        }

        // Object.prototype.valueOf
        JSValue ObjectProtoValueOf(JSValue thisVal, JSValue[] args)
        {
            return thisVal;
        }

        // Object.prototype.isPrototypeOf
        JSValue ObjectProtoIsPrototypeOf(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject)
                return JSValue.False;
            var v = args.Length > 0 ? args[0] : JSValue.Undefined;
            if (!v.IsObject)
                return JSValue.False;
            var o = thisVal.AsObject();
            var current = v.AsObject().Prototype;
            while (current != null)
            {
                if (ReferenceEquals(current, o))
                    return JSValue.True;
                current = current.Prototype;
            }
            return JSValue.False;
        }

        // Object.prototype.propertyIsEnumerable
        JSValue ObjectProtoPropertyIsEnumerable(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject)
                return JSValue.False;
            var key = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "";
            var obj = thisVal.AsObject();
            if (!obj.TryGetOwnPropertyDescriptor(key, out var desc))
                return JSValue.False;
            return JSValue.FromBoolean(desc.IsEnumerable);
        }

        // Constructor.prototype
        objectCtorFunc.Set("prototype", JSValue.FromObject(objectProto));

        // Attach global Object
        _globalObject.Set("Object", JSValue.FromObject(objectCtorFunc));

        // Attach Object static methods
        objectCtorFunc.Set("create", JSValue.FromObject(new JSFunction(ObjectCreate, "create", 2, functionProto)));
        objectCtorFunc.Set("keys", JSValue.FromObject(new JSFunction(ObjectKeys, "keys", 1, functionProto)));
        objectCtorFunc.Set("values", JSValue.FromObject(new JSFunction(ObjectValues, "values", 1, functionProto)));
        objectCtorFunc.Set("entries", JSValue.FromObject(new JSFunction(ObjectEntries, "entries", 1, functionProto)));
        objectCtorFunc.Set("assign", JSValue.FromObject(new JSFunction(ObjectAssign, "assign", 2, functionProto)));
        objectCtorFunc.Set("getPrototypeOf", JSValue.FromObject(new JSFunction(ObjectGetPrototypeOf, "getPrototypeOf", 1, functionProto)));
        objectCtorFunc.Set("setPrototypeOf", JSValue.FromObject(new JSFunction(ObjectSetPrototypeOf, "setPrototypeOf", 2, functionProto)));
        objectCtorFunc.Set("is", JSValue.FromObject(new JSFunction(ObjectIs, "is", 2, functionProto)));
        objectCtorFunc.Set("freeze", JSValue.FromObject(new JSFunction(ObjectFreeze, "freeze", 1, functionProto)));
        objectCtorFunc.Set("seal", JSValue.FromObject(new JSFunction(ObjectSeal, "seal", 1, functionProto)));
        objectCtorFunc.Set("preventExtensions", JSValue.FromObject(new JSFunction(ObjectPreventExtensions, "preventExtensions", 1, functionProto)));
        objectCtorFunc.Set("isExtensible", JSValue.FromObject(new JSFunction(ObjectIsExtensible, "isExtensible", 1, functionProto)));
        objectCtorFunc.Set("isFrozen", JSValue.FromObject(new JSFunction(ObjectIsFrozen, "isFrozen", 1, functionProto)));
        objectCtorFunc.Set("isSealed", JSValue.FromObject(new JSFunction(ObjectIsSealed, "isSealed", 1, functionProto)));
        objectCtorFunc.Set("fromEntries", JSValue.FromObject(new JSFunction(ObjectFromEntries, "fromEntries", 1, functionProto)));
        objectCtorFunc.Set("hasOwn", JSValue.FromObject(new JSFunction(ObjectHasOwn, "hasOwn", 2, functionProto)));
        objectCtorFunc.Set("getOwnPropertyNames", JSValue.FromObject(new JSFunction(ObjectGetOwnPropertyNames, "getOwnPropertyNames", 1, functionProto)));
        objectCtorFunc.Set("getOwnPropertyDescriptor", JSValue.FromObject(new JSFunction(ObjectGetOwnPropertyDescriptor, "getOwnPropertyDescriptor", 2, functionProto)));
        objectCtorFunc.Set("defineProperty", JSValue.FromObject(new JSFunction(ObjectDefineProperty, "defineProperty", 3, functionProto)));

        // Attach prototype methods
        objectProto.Set("constructor", JSValue.FromObject(objectCtorFunc));
        objectProto.Set("hasOwnProperty", JSValue.FromObject(new JSFunction(HasOwnProperty, "hasOwnProperty", 1, functionProto)));
        objectProto.Set("toString", JSValue.FromObject(new JSFunction(ObjectProtoToString, "toString", 0, functionProto)));
        objectProto.Set("valueOf", JSValue.FromObject(new JSFunction(ObjectProtoValueOf, "valueOf", 0, functionProto)));
        objectProto.Set("isPrototypeOf", JSValue.FromObject(new JSFunction(ObjectProtoIsPrototypeOf, "isPrototypeOf", 1, functionProto)));
        objectProto.Set("propertyIsEnumerable", JSValue.FromObject(new JSFunction(ObjectProtoPropertyIsEnumerable, "propertyIsEnumerable", 1, functionProto)));
    }

    private void InitializeFunctionConstructor()
    {
        var objectProto = GetClassPrototype(JSClassId.Object)!;
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;

        JSValue FunctionCtor(JSValue thisVal, JSValue[] args)
        {
            // Not yet supported to compile from strings
            return ThrowTypeError("Function constructor from string is not supported");
        }

        var functionCtorFunc = new JSFunction(FunctionCtor, "Function", 1, functionProto);
        functionCtorFunc.Set("prototype", JSValue.FromObject(functionProto));

        // Function.prototype.constructor = Function
        functionProto.Set("constructor", JSValue.FromObject(functionCtorFunc));

        // Attach global Function
        _globalObject.Set("Function", JSValue.FromObject(functionCtorFunc));
    }

    private void InitializeErrorConstructors()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var baseErrorProto = GetClassPrototype(JSClassId.Error)!;

        // Register base Error
        RegisterErrorConstructor(JSErrorType.Error, "Error", baseErrorProto, functionProto);

        // Derived errors
        RegisterErrorConstructor(JSErrorType.TypeError, "TypeError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.RangeError, "RangeError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.ReferenceError, "ReferenceError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.SyntaxError, "SyntaxError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.URIError, "URIError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.EvalError, "EvalError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.InternalError, "InternalError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
        RegisterErrorConstructor(JSErrorType.AggregateError, "AggregateError", new JSObject(baseErrorProto, JSClassId.Error), functionProto);
    }

    private void RegisterErrorConstructor(JSErrorType type, string name, JSObject proto, JSObject functionProto)
    {
        JSValue ErrorCtor(JSValue thisVal, JSValue[] args)
        {
            string message = args.Length > 0 ? args[0].ToString() ?? string.Empty : string.Empty;
            var errObj = new JSObject(proto, JSClassId.Error);
            errObj.Set("name", JSValue.FromString(name));
            if (!string.IsNullOrEmpty(message))
            {
                errObj.Set("message", JSValue.FromString(message));
            }
            return JSValue.FromObject(errObj);
        }

        var ctor = new JSFunction(ErrorCtor, name, 1, functionProto);
        ctor.Set("prototype", JSValue.FromObject(proto));
        proto.Set("constructor", JSValue.FromObject(ctor));
        proto.Set("name", JSValue.FromString(name));

        _globalObject.Set(name, JSValue.FromObject(ctor));
        _errorPrototypes[type] = proto;
    }

    private void InitializeNumberAndMath()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        // Number.prototype
        var numberProto = new JSObject(objectProto, JSClassId.Number);
        SetClassPrototype(JSClassId.Number, numberProto);

        JSValue NumberCtor(JSValue thisVal, JSValue[] args)
        {
            double num = args.Length > 0 ? JSValueConversion.ToNumber(args[0]) : 0.0;
            var numVal = JSValue.FromDouble(num);

            // If called with new, interpreter supplies an object as thisVal; set its internal value
            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.Object)
            {
                var wrapper = new JSObject(numberProto, JSClassId.Number) { InternalValue = numVal };
                return JSValue.FromObject(wrapper);
            }

            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.Number)
            {
                thisVal.AsObject().InternalValue = numVal;
                return thisVal;
            }

            return numVal;
        }

        var numberCtor = new JSFunction(NumberCtor, "Number", 1, functionProto);
        numberCtor.Set("prototype", JSValue.FromObject(numberProto));
        numberProto.Set("constructor", JSValue.FromObject(numberCtor));
        _globalObject.Set("Number", JSValue.FromObject(numberCtor));

        // Math object
        var mathObj = new JSObject(objectProto, JSClassId.Object);

        // Helper to add Math functions
        void AddMathFunc(string name, int length, Func<double[], double> impl)
        {
            JSValue Func(JSValue _, JSValue[] args)
            {
                var nums = new double[args.Length];
                for (int i = 0; i < args.Length; i++)
                    nums[i] = JSValueConversion.ToNumber(args[i]);
                return JSValue.FromDouble(impl(nums));
            }

            mathObj.Set(name, JSValue.FromObject(new JSFunction(Func, name, length, functionProto)));
        }

        // Math constants
        mathObj.Set("PI", JSValue.FromDouble(Math.PI));
        mathObj.Set("E", JSValue.FromDouble(Math.E));
        mathObj.Set("LN10", JSValue.FromDouble(Math.Log(10)));
        mathObj.Set("LN2", JSValue.FromDouble(Math.Log(2)));
        mathObj.Set("LOG2E", JSValue.FromDouble(1.0 / Math.Log(2)));
        mathObj.Set("LOG10E", JSValue.FromDouble(1.0 / Math.Log(10)));
        mathObj.Set("SQRT1_2", JSValue.FromDouble(Math.Sqrt(0.5)));
        mathObj.Set("SQRT2", JSValue.FromDouble(Math.Sqrt(2)));

        // Math functions
        AddMathFunc("abs", 1, xs => Math.Abs(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("floor", 1, xs => Math.Floor(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("ceil", 1, xs => Math.Ceiling(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("round", 1, xs => Math.Round(xs.Length > 0 ? xs[0] : double.NaN, MidpointRounding.AwayFromZero));
        AddMathFunc("max", 2, xs => xs.Length == 0 ? double.NegativeInfinity : xs.Max());
        AddMathFunc("min", 2, xs => xs.Length == 0 ? double.PositiveInfinity : xs.Min());
        AddMathFunc("pow", 2, xs => xs.Length >= 2 ? Math.Pow(xs[0], xs[1]) : double.NaN);
        AddMathFunc("sqrt", 1, xs => xs.Length > 0 ? Math.Sqrt(xs[0]) : double.NaN);
        AddMathFunc("sin", 1, xs => xs.Length > 0 ? Math.Sin(xs[0]) : double.NaN);
        AddMathFunc("cos", 1, xs => xs.Length > 0 ? Math.Cos(xs[0]) : double.NaN);
        AddMathFunc("tan", 1, xs => xs.Length > 0 ? Math.Tan(xs[0]) : double.NaN);
        AddMathFunc("asin", 1, xs => xs.Length > 0 ? Math.Asin(xs[0]) : double.NaN);
        AddMathFunc("acos", 1, xs => xs.Length > 0 ? Math.Acos(xs[0]) : double.NaN);
        AddMathFunc("atan", 1, xs => xs.Length > 0 ? Math.Atan(xs[0]) : double.NaN);
        AddMathFunc("atan2", 2, xs => xs.Length >= 2 ? Math.Atan2(xs[0], xs[1]) : double.NaN);
        AddMathFunc("exp", 1, xs => xs.Length > 0 ? Math.Exp(xs[0]) : double.NaN);
        AddMathFunc("log", 1, xs => xs.Length > 0 ? Math.Log(xs[0]) : double.NaN);
        AddMathFunc("log2", 1, xs => xs.Length > 0 ? Math.Log(xs[0]) / Math.Log(2) : double.NaN);
        AddMathFunc("log10", 1, xs => xs.Length > 0 ? Math.Log10(xs[0]) : double.NaN);
        AddMathFunc("sinh", 1, xs => xs.Length > 0 ? Math.Sinh(xs[0]) : double.NaN);
        AddMathFunc("cosh", 1, xs => xs.Length > 0 ? Math.Cosh(xs[0]) : double.NaN);
        AddMathFunc("tanh", 1, xs => xs.Length > 0 ? Math.Tanh(xs[0]) : double.NaN);
        AddMathFunc("asinh", 1, xs => xs.Length > 0 ? Math.Log(xs[0] + Math.Sqrt(xs[0] * xs[0] + 1)) : double.NaN);
        AddMathFunc("acosh", 1, xs => xs.Length > 0 ? Math.Log(xs[0] + Math.Sqrt(xs[0] * xs[0] - 1)) : double.NaN);
        AddMathFunc("atanh", 1, xs => xs.Length > 0 ? 0.5 * Math.Log((1 + xs[0]) / (1 - xs[0])) : double.NaN);
        AddMathFunc("expm1", 1, xs => xs.Length > 0 ? Math.Exp(xs[0]) - 1 : double.NaN);
        AddMathFunc("log1p", 1, xs => xs.Length > 0 ? Math.Log(1 + xs[0]) : double.NaN);
        AddMathFunc("cbrt", 1, xs => xs.Length > 0 ? Math.Pow(xs[0], 1.0 / 3.0) * Math.Sign(xs[0]) : double.NaN);
        AddMathFunc("hypot", 2, xs => xs.Length >= 2 ? Math.Sqrt(xs[0] * xs[0] + xs[1] * xs[1]) : xs.Length == 1 ? Math.Abs(xs[0]) : 0);
        AddMathFunc("trunc", 1, xs => xs.Length > 0 ? Math.Truncate(xs[0]) : double.NaN);
        AddMathFunc("sign", 1, xs => xs.Length > 0 ? Math.Sign(xs[0]) : double.NaN);
        AddMathFunc("fround", 1, xs => xs.Length > 0 ? (double)(float)xs[0] : double.NaN);
        AddMathFunc("imul", 2, xs => xs.Length >= 2 ? (double)((int)xs[0] * (int)xs[1]) : 0);
        AddMathFunc("clz32", 1, xs =>
        {
            if (xs.Length == 0) return 32;
            uint n = (uint)xs[0];
            if (n == 0) return 32;
            int count = 0;
            while ((n & 0x80000000) == 0)
            {
                count++;
                n <<= 1;
            }
            return count;
        });
        AddMathFunc("random", 0, xs =>
        {
            // Simple LCG for determinism
            _randomState = _randomState * 6364136223846793005UL + 1;
            // Use high 53 bits
            var val = (double)(_randomState >> 11) / (double)(1UL << 53);
            return val;
        });

        _globalObject.Set("Math", JSValue.FromObject(mathObj));
    }

    private void InitializeStringConstructor()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        var stringProto = new JSObject(objectProto, JSClassId.String);
        SetClassPrototype(JSClassId.String, stringProto);

        JSValue StringCtor(JSValue thisVal, JSValue[] args)
        {
            string str = args.Length > 0 ? JSValueConversion.ToString(args[0]) : string.Empty;
            var strVal = JSValue.FromString(str);

            // If called with new, wrap in String object
            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.Object)
            {
                var wrapper = new JSObject(stringProto, JSClassId.String) { InternalValue = strVal };
                return JSValue.FromObject(wrapper);
            }
            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.String)
            {
                thisVal.AsObject().InternalValue = strVal;
                return thisVal;
            }

            return strVal;
        }

        var stringCtor = new JSFunction(StringCtor, "String", 1, functionProto);
        stringCtor.Set("prototype", JSValue.FromObject(stringProto));
        stringProto.Set("constructor", JSValue.FromObject(stringCtor));

        // String.prototype.toString / valueOf
        JSValue StringProtoToString(JSValue thisVal, JSValue[] args)
        {
            if (thisVal.IsString)
                return thisVal;
            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.String)
                return thisVal.AsObject().InternalValue;
            return ThrowTypeError("String.prototype.toString called on non-string");
        }

        // Helper to get string from this value
        string GetStringValue(JSValue thisVal)
        {
            if (thisVal.IsString)
                return thisVal.ToString()!;
            if (thisVal.IsObject && thisVal.AsObject().ClassId == JSClassId.String)
                return thisVal.AsObject().InternalValue.ToString()!;
            return JSValueConversion.ToString(thisVal);
        }

        // String.prototype.charAt
        JSValue StringCharAt(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int index = args.Length > 0 ? args[0].ToInt32() : 0;
            if (index < 0 || index >= str.Length)
                return JSValue.FromString("");
            return JSValue.FromString(str[index].ToString());
        }

        // String.prototype.charCodeAt
        JSValue StringCharCodeAt(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int index = args.Length > 0 ? args[0].ToInt32() : 0;
            if (index < 0 || index >= str.Length)
                return JSValue.FromDouble(double.NaN);
            return JSValue.FromInt32(str[index]);
        }

        // String.prototype.concat
        JSValue StringConcat(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            foreach (var arg in args)
            {
                str += JSValueConversion.ToString(arg);
            }
            return JSValue.FromString(str);
        }

        // String.prototype.indexOf
        JSValue StringIndexOf(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var searchStr = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "undefined";
            int position = args.Length > 1 ? args[1].ToInt32() : 0;
            position = Math.Max(0, Math.Min(position, str.Length));
            int idx = str.IndexOf(searchStr, position, StringComparison.Ordinal);
            return JSValue.FromInt32(idx);
        }

        // String.prototype.lastIndexOf
        JSValue StringLastIndexOf(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var searchStr = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "undefined";
            int position = args.Length > 1 ? args[1].ToInt32() : str.Length;
            position = Math.Max(0, Math.Min(position, str.Length));
            if (position == 0 && searchStr.Length > 0)
                return JSValue.FromInt32(-1);
            int idx = str.LastIndexOf(searchStr, Math.Min(position + searchStr.Length - 1, str.Length - 1), StringComparison.Ordinal);
            return JSValue.FromInt32(idx);
        }

        // String.prototype.includes
        JSValue StringIncludes(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var searchStr = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "undefined";
            int position = args.Length > 1 ? args[1].ToInt32() : 0;
            position = Math.Max(0, Math.Min(position, str.Length));
            return JSValue.FromBoolean(str.IndexOf(searchStr, position, StringComparison.Ordinal) >= 0);
        }

        // String.prototype.startsWith
        JSValue StringStartsWith(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var searchStr = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "undefined";
            int position = args.Length > 1 ? args[1].ToInt32() : 0;
            position = Math.Max(0, Math.Min(position, str.Length));
            if (position + searchStr.Length > str.Length)
                return JSValue.False;
            return JSValue.FromBoolean(str.Substring(position).StartsWith(searchStr, StringComparison.Ordinal));
        }

        // String.prototype.endsWith
        JSValue StringEndsWith(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var searchStr = args.Length > 0 ? JSValueConversion.ToString(args[0]) : "undefined";
            int endPos = args.Length > 1 && !args[1].IsUndefined ? args[1].ToInt32() : str.Length;
            endPos = Math.Max(0, Math.Min(endPos, str.Length));
            if (searchStr.Length > endPos)
                return JSValue.False;
            return JSValue.FromBoolean(str.Substring(0, endPos).EndsWith(searchStr, StringComparison.Ordinal));
        }

        // String.prototype.slice
        JSValue StringSlice(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int len = str.Length;
            int start = args.Length > 0 ? args[0].ToInt32() : 0;
            int end = args.Length > 1 && !args[1].IsUndefined ? args[1].ToInt32() : len;
            if (start < 0) start = Math.Max(len + start, 0);
            if (end < 0) end = Math.Max(len + end, 0);
            start = Math.Min(start, len);
            end = Math.Min(end, len);
            if (end <= start) return JSValue.FromString("");
            return JSValue.FromString(str.Substring(start, end - start));
        }

        // String.prototype.substring
        JSValue StringSubstring(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int len = str.Length;
            int start = args.Length > 0 ? args[0].ToInt32() : 0;
            int end = args.Length > 1 && !args[1].IsUndefined ? args[1].ToInt32() : len;
            start = Math.Max(0, Math.Min(start, len));
            end = Math.Max(0, Math.Min(end, len));
            if (start > end) (start, end) = (end, start);
            return JSValue.FromString(str.Substring(start, end - start));
        }

        // String.prototype.substr (deprecated but still supported)
        JSValue StringSubstr(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int len = str.Length;
            int start = args.Length > 0 ? args[0].ToInt32() : 0;
            if (start < 0) start = Math.Max(len + start, 0);
            int length = args.Length > 1 && !args[1].IsUndefined ? args[1].ToInt32() : len - start;
            if (length <= 0 || start >= len) return JSValue.FromString("");
            length = Math.Min(length, len - start);
            return JSValue.FromString(str.Substring(start, length));
        }

        // String.prototype.toLowerCase
        JSValue StringToLowerCase(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            return JSValue.FromString(str.ToLowerInvariant());
        }

        // String.prototype.toUpperCase
        JSValue StringToUpperCase(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            return JSValue.FromString(str.ToUpperInvariant());
        }

        // String.prototype.trim
        JSValue StringTrim(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            return JSValue.FromString(str.Trim());
        }

        // String.prototype.trimStart
        JSValue StringTrimStart(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            return JSValue.FromString(str.TrimStart());
        }

        // String.prototype.trimEnd
        JSValue StringTrimEnd(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            return JSValue.FromString(str.TrimEnd());
        }

        // String.prototype.split
        JSValue StringSplit(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            var separator = args.Length > 0 ? args[0] : JSValue.Undefined;
            int limit = args.Length > 1 && !args[1].IsUndefined ? (int)JSValueConversion.ToNumber(args[1]) : int.MaxValue;
            
            var result = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            if (separator.IsUndefined)
            {
                result.Set(0u, JSValue.FromString(str));
                return JSValue.FromObject(result);
            }
            
            var sepStr = JSValueConversion.ToString(separator);
            string[] parts;
            if (string.IsNullOrEmpty(sepStr))
            {
                // Split into individual characters
                parts = new string[str.Length];
                for (int j = 0; j < str.Length; j++)
                    parts[j] = str[j].ToString();
            }
            else
            {
                parts = str.Split(new[] { sepStr }, StringSplitOptions.None);
            }
            
            uint idx = 0;
            foreach (var part in parts)
            {
                if (idx >= limit) break;
                result.Set(idx++, JSValue.FromString(part));
            }
            return JSValue.FromObject(result);
        }

        // String.prototype.repeat
        JSValue StringRepeat(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int count = args.Length > 0 ? args[0].ToInt32() : 0;
            if (count < 0)
                return ThrowRangeError("Invalid count value");
            if (count == 0 || str.Length == 0)
                return JSValue.FromString("");
            return JSValue.FromString(string.Concat(Enumerable.Repeat(str, count)));
        }

        // String.prototype.padStart
        JSValue StringPadStart(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int targetLen = args.Length > 0 ? args[0].ToInt32() : 0;
            if (targetLen <= str.Length)
                return JSValue.FromString(str);
            var padStr = args.Length > 1 && !args[1].IsUndefined ? JSValueConversion.ToString(args[1]) : " ";
            if (string.IsNullOrEmpty(padStr))
                return JSValue.FromString(str);
            int padLen = targetLen - str.Length;
            var pad = string.Concat(Enumerable.Repeat(padStr, (padLen / padStr.Length) + 1)).Substring(0, padLen);
            return JSValue.FromString(pad + str);
        }

        // String.prototype.padEnd
        JSValue StringPadEnd(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int targetLen = args.Length > 0 ? args[0].ToInt32() : 0;
            if (targetLen <= str.Length)
                return JSValue.FromString(str);
            var padStr = args.Length > 1 && !args[1].IsUndefined ? JSValueConversion.ToString(args[1]) : " ";
            if (string.IsNullOrEmpty(padStr))
                return JSValue.FromString(str);
            int padLen = targetLen - str.Length;
            var pad = string.Concat(Enumerable.Repeat(padStr, (padLen / padStr.Length) + 1)).Substring(0, padLen);
            return JSValue.FromString(str + pad);
        }

        // String.prototype.at (ES2022)
        JSValue StringAt(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            int index = args.Length > 0 ? args[0].ToInt32() : 0;
            if (index < 0) index = str.Length + index;
            if (index < 0 || index >= str.Length)
                return JSValue.Undefined;
            return JSValue.FromString(str[index].ToString());
        }

        // String.prototype.replace (simple, non-regex)
        JSValue StringReplace(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            if (args.Length < 2)
                return JSValue.FromString(str);
            var searchValue = JSValueConversion.ToString(args[0]);
            var replaceValue = JSValueConversion.ToString(args[1]);
            int idx = str.IndexOf(searchValue, StringComparison.Ordinal);
            if (idx < 0)
                return JSValue.FromString(str);
            return JSValue.FromString(str.Substring(0, idx) + replaceValue + str.Substring(idx + searchValue.Length));
        }

        // String.prototype.replaceAll
        JSValue StringReplaceAll(JSValue thisVal, JSValue[] args)
        {
            var str = GetStringValue(thisVal);
            if (args.Length < 2)
                return JSValue.FromString(str);
            var searchValue = JSValueConversion.ToString(args[0]);
            var replaceValue = JSValueConversion.ToString(args[1]);
            if (string.IsNullOrEmpty(searchValue))
                return JSValue.FromString(str);
            return JSValue.FromString(str.Replace(searchValue, replaceValue));
        }

        stringProto.Set("toString", JSValue.FromObject(new JSFunction(StringProtoToString, "toString", 0, functionProto)));
        stringProto.Set("valueOf", JSValue.FromObject(new JSFunction(StringProtoToString, "valueOf", 0, functionProto)));
        stringProto.Set("charAt", JSValue.FromObject(new JSFunction(StringCharAt, "charAt", 1, functionProto)));
        stringProto.Set("charCodeAt", JSValue.FromObject(new JSFunction(StringCharCodeAt, "charCodeAt", 1, functionProto)));
        stringProto.Set("concat", JSValue.FromObject(new JSFunction(StringConcat, "concat", 1, functionProto)));
        stringProto.Set("indexOf", JSValue.FromObject(new JSFunction(StringIndexOf, "indexOf", 1, functionProto)));
        stringProto.Set("lastIndexOf", JSValue.FromObject(new JSFunction(StringLastIndexOf, "lastIndexOf", 1, functionProto)));
        stringProto.Set("includes", JSValue.FromObject(new JSFunction(StringIncludes, "includes", 1, functionProto)));
        stringProto.Set("startsWith", JSValue.FromObject(new JSFunction(StringStartsWith, "startsWith", 1, functionProto)));
        stringProto.Set("endsWith", JSValue.FromObject(new JSFunction(StringEndsWith, "endsWith", 1, functionProto)));
        stringProto.Set("slice", JSValue.FromObject(new JSFunction(StringSlice, "slice", 2, functionProto)));
        stringProto.Set("substring", JSValue.FromObject(new JSFunction(StringSubstring, "substring", 2, functionProto)));
        stringProto.Set("substr", JSValue.FromObject(new JSFunction(StringSubstr, "substr", 2, functionProto)));
        stringProto.Set("toLowerCase", JSValue.FromObject(new JSFunction(StringToLowerCase, "toLowerCase", 0, functionProto)));
        stringProto.Set("toUpperCase", JSValue.FromObject(new JSFunction(StringToUpperCase, "toUpperCase", 0, functionProto)));
        stringProto.Set("toLocaleLowerCase", JSValue.FromObject(new JSFunction(StringToLowerCase, "toLocaleLowerCase", 0, functionProto)));
        stringProto.Set("toLocaleUpperCase", JSValue.FromObject(new JSFunction(StringToUpperCase, "toLocaleUpperCase", 0, functionProto)));
        stringProto.Set("trim", JSValue.FromObject(new JSFunction(StringTrim, "trim", 0, functionProto)));
        stringProto.Set("trimStart", JSValue.FromObject(new JSFunction(StringTrimStart, "trimStart", 0, functionProto)));
        stringProto.Set("trimEnd", JSValue.FromObject(new JSFunction(StringTrimEnd, "trimEnd", 0, functionProto)));
        stringProto.Set("trimLeft", JSValue.FromObject(new JSFunction(StringTrimStart, "trimLeft", 0, functionProto)));
        stringProto.Set("trimRight", JSValue.FromObject(new JSFunction(StringTrimEnd, "trimRight", 0, functionProto)));
        stringProto.Set("split", JSValue.FromObject(new JSFunction(StringSplit, "split", 2, functionProto)));
        stringProto.Set("repeat", JSValue.FromObject(new JSFunction(StringRepeat, "repeat", 1, functionProto)));
        stringProto.Set("padStart", JSValue.FromObject(new JSFunction(StringPadStart, "padStart", 1, functionProto)));
        stringProto.Set("padEnd", JSValue.FromObject(new JSFunction(StringPadEnd, "padEnd", 1, functionProto)));
        stringProto.Set("at", JSValue.FromObject(new JSFunction(StringAt, "at", 1, functionProto)));
        stringProto.Set("replace", JSValue.FromObject(new JSFunction(StringReplace, "replace", 2, functionProto)));
        stringProto.Set("replaceAll", JSValue.FromObject(new JSFunction(StringReplaceAll, "replaceAll", 2, functionProto)));

        _globalObject.Set("String", JSValue.FromObject(stringCtor));
    }

    private void InitializeArrayConstructor()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        var arrayProto = GetClassPrototype(JSClassId.Array);
        if (arrayProto == null)
        {
            arrayProto = new JSObject(objectProto, JSClassId.Array);
            SetClassPrototype(JSClassId.Array, arrayProto);
        }

        JSValue ArrayCtor(JSValue thisVal, JSValue[] args)
        {
            JSObject arr = new JSObject(arrayProto, JSClassId.Array);

            if (args.Length == 1 && args[0].IsNumber)
            {
                var len = JSValueConversion.ToNumber(args[0]);
                if (len < 0 || len > uint.MaxValue)
                    return ThrowRangeError("Invalid array length");
                arr.SetArrayLength((uint)len);
            }
            else
            {
                for (int i = 0; i < args.Length; i++)
                {
                    arr.Set((uint)i, args[i]);
                }
            }

            return JSValue.FromObject(arr);
        }

        var arrayCtorFn = new JSFunction(ArrayCtor, "Array", 1, functionProto);
        arrayCtorFn.Set("prototype", JSValue.FromObject(arrayProto));
        arrayProto.Set("constructor", JSValue.FromObject(arrayCtorFn));

        // Array.prototype.push
        JSValue ArrayPush(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.push called on non-array");
            var arr = thisVal.AsObject();
            foreach (var arg in args)
            {
                arr.Set(arr.ArrayLength, arg);
            }
            return JSValue.FromInt32((int)arr.ArrayLength);
        }

        // Array.prototype.pop
        JSValue ArrayPop(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.pop called on non-array");
            var arr = thisVal.AsObject();
            return arr.ArrayPop();
        }

        // Array.prototype.shift
        JSValue ArrayShift(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.shift called on non-array");
            var arr = thisVal.AsObject();
            var len = arr.ArrayLength;
            if (len == 0) return JSValue.Undefined;
            var first = arr.Get(0u);
            for (uint i = 1; i < len; i++)
            {
                arr.Set(i - 1, arr.Get(i));
            }
            arr.SetArrayLength(len - 1);
            return first;
        }

        // Array.prototype.unshift
        JSValue ArrayUnshift(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.unshift called on non-array");
            var arr = thisVal.AsObject();
            var len = arr.ArrayLength;
            var argCount = (uint)args.Length;
            // Shift existing elements
            for (uint i = len; i > 0; i--)
            {
                arr.Set(i - 1 + argCount, arr.Get(i - 1));
            }
            // Insert new elements at the beginning
            for (uint i = 0; i < argCount; i++)
            {
                arr.Set(i, args[i]);
            }
            return JSValue.FromInt32((int)(len + argCount));
        }

        // Array.prototype.slice
        JSValue ArraySlice(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.slice called on non-array");
            var arr = thisVal.AsObject();
            var len = (int)arr.ArrayLength;
            int start = args.Length > 0 ? args[0].ToInt32() : 0;
            int end = args.Length > 1 && !args[1].IsUndefined ? args[1].ToInt32() : len;
            if (start < 0) start = Math.Max(len + start, 0);
            if (end < 0) end = Math.Max(len + end, 0);
            start = Math.Min(start, len);
            end = Math.Min(end, len);
            var result = new JSObject(arrayProto, JSClassId.Array);
            uint k = 0;
            for (int i = start; i < end; i++)
            {
                result.Set(k++, arr.Get((uint)i));
            }
            return JSValue.FromObject(result);
        }

        // Array.prototype.splice
        JSValue ArraySplice(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.splice called on non-array");
            var arr = thisVal.AsObject();
            var len = (int)arr.ArrayLength;
            int start = args.Length > 0 ? args[0].ToInt32() : 0;
            if (start < 0) start = Math.Max(len + start, 0);
            start = Math.Min(start, len);
            int deleteCount = args.Length > 1 ? Math.Max(0, Math.Min(args[1].ToInt32(), len - start)) : len - start;
            
            // Create result array with deleted elements
            var result = new JSObject(arrayProto, JSClassId.Array);
            for (int i = 0; i < deleteCount; i++)
            {
                result.Set((uint)i, arr.Get((uint)(start + i)));
            }
            
            // Calculate items to insert
            var insertItems = args.Length > 2 ? args.Skip(2).ToArray() : Array.Empty<JSValue>();
            int insertCount = insertItems.Length;
            int diff = insertCount - deleteCount;
            
            if (diff < 0)
            {
                // Shifting elements left
                for (int i = start + deleteCount; i < len; i++)
                {
                    arr.Set((uint)(i + diff), arr.Get((uint)i));
                }
                arr.SetArrayLength((uint)(len + diff));
            }
            else if (diff > 0)
            {
                // Shifting elements right
                for (int i = len - 1; i >= start + deleteCount; i--)
                {
                    arr.Set((uint)(i + diff), arr.Get((uint)i));
                }
            }
            
            // Insert new items
            for (int i = 0; i < insertCount; i++)
            {
                arr.Set((uint)(start + i), insertItems[i]);
            }
            if (diff != 0 && insertCount + deleteCount == 0)
                arr.SetArrayLength((uint)(len + diff));
            else if (diff > 0)
                arr.SetArrayLength((uint)(len + diff));
            
            return JSValue.FromObject(result);
        }

        // Array.prototype.concat
        JSValue ArrayConcat(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.concat called on non-array");
            var arr = thisVal.AsObject();
            var result = new JSObject(arrayProto, JSClassId.Array);
            uint k = 0;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                result.Set(k++, arr.Get(i));
            }
            foreach (var arg in args)
            {
                if (arg.IsObject && arg.AsObject().ClassId == JSClassId.Array)
                {
                    var subArr = arg.AsObject();
                    for (uint i = 0; i < subArr.ArrayLength; i++)
                    {
                        result.Set(k++, subArr.Get(i));
                    }
                }
                else
                {
                    result.Set(k++, arg);
                }
            }
            return JSValue.FromObject(result);
        }

        // Array.prototype.join
        JSValue ArrayJoin(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.join called on non-array");
            var arr = thisVal.AsObject();
            var separator = args.Length > 0 && !args[0].IsUndefined ? JSValueConversion.ToString(args[0]) : ",";
            var parts = new List<string>();
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var val = arr.Get(i);
                parts.Add(val.IsNull || val.IsUndefined ? "" : JSValueConversion.ToString(val));
            }
            return JSValue.FromString(string.Join(separator, parts));
        }

        // Array.prototype.reverse
        JSValue ArrayReverse(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.reverse called on non-array");
            var arr = thisVal.AsObject();
            var len = arr.ArrayLength;
            for (uint i = 0; i < len / 2; i++)
            {
                var temp = arr.Get(i);
                arr.Set(i, arr.Get(len - 1 - i));
                arr.Set(len - 1 - i, temp);
            }
            return thisVal;
        }

        // Array.prototype.indexOf
        JSValue ArrayIndexOf(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.indexOf called on non-array");
            var arr = thisVal.AsObject();
            var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
            int fromIndex = args.Length > 1 ? args[1].ToInt32() : 0;
            var len = (int)arr.ArrayLength;
            if (fromIndex < 0) fromIndex = Math.Max(len + fromIndex, 0);
            for (int i = fromIndex; i < len; i++)
            {
                if (JSValueConversion.StrictEquals(arr.Get((uint)i), searchElement))
                    return JSValue.FromInt32(i);
            }
            return JSValue.FromInt32(-1);
        }

        // Array.prototype.lastIndexOf
        JSValue ArrayLastIndexOf(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.lastIndexOf called on non-array");
            var arr = thisVal.AsObject();
            var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
            var len = (int)arr.ArrayLength;
            int fromIndex = args.Length > 1 ? args[1].ToInt32() : len - 1;
            if (fromIndex < 0) fromIndex = len + fromIndex;
            for (int i = Math.Min(fromIndex, len - 1); i >= 0; i--)
            {
                if (JSValueConversion.StrictEquals(arr.Get((uint)i), searchElement))
                    return JSValue.FromInt32(i);
            }
            return JSValue.FromInt32(-1);
        }

        // Array.prototype.includes
        JSValue ArrayIncludes(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.includes called on non-array");
            var arr = thisVal.AsObject();
            var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
            int fromIndex = args.Length > 1 ? args[1].ToInt32() : 0;
            var len = (int)arr.ArrayLength;
            if (fromIndex < 0) fromIndex = Math.Max(len + fromIndex, 0);
            for (int i = fromIndex; i < len; i++)
            {
                if (JSValueConversion.SameValueZero(arr.Get((uint)i), searchElement))
                    return JSValue.True;
            }
            return JSValue.False;
        }

        // Array.prototype.forEach
        JSValue ArrayForEach(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.forEach called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                callback.CallNative(thisArg, new[] { arr.Get(i), JSValue.FromInt32((int)i), thisVal });
            }
            return JSValue.Undefined;
        }

        // Array.prototype.map
        JSValue ArrayMap(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.map called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            var result = new JSObject(arrayProto, JSClassId.Array);
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var mapped = callback.CallNative(thisArg, new[] { arr.Get(i), JSValue.FromInt32((int)i), thisVal });
                result.Set(i, mapped);
            }
            return JSValue.FromObject(result);
        }

        // Array.prototype.filter
        JSValue ArrayFilter(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.filter called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            var result = new JSObject(arrayProto, JSClassId.Array);
            uint k = 0;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var val = arr.Get(i);
                var keep = callback.CallNative(thisArg, new[] { val, JSValue.FromInt32((int)i), thisVal });
                if (JSValueConversion.ToBoolean(keep))
                    result.Set(k++, val);
            }
            return JSValue.FromObject(result);
        }

        // Array.prototype.reduce
        JSValue ArrayReduce(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.reduce called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var len = arr.ArrayLength;
            uint k = 0;
            JSValue accumulator;
            if (args.Length > 1)
            {
                accumulator = args[1];
            }
            else
            {
                if (len == 0)
                    return ThrowTypeError("Reduce of empty array with no initial value");
                accumulator = arr.Get(k++);
            }
            for (; k < len; k++)
            {
                accumulator = callback.CallNative(JSValue.Undefined, new[] { accumulator, arr.Get(k), JSValue.FromInt32((int)k), thisVal });
            }
            return accumulator;
        }

        // Array.prototype.reduceRight
        JSValue ArrayReduceRight(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.reduceRight called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var len = (int)arr.ArrayLength;
            int k = len - 1;
            JSValue accumulator;
            if (args.Length > 1)
            {
                accumulator = args[1];
            }
            else
            {
                if (len == 0)
                    return ThrowTypeError("Reduce of empty array with no initial value");
                accumulator = arr.Get((uint)k--);
            }
            for (; k >= 0; k--)
            {
                accumulator = callback.CallNative(JSValue.Undefined, new[] { accumulator, arr.Get((uint)k), JSValue.FromInt32(k), thisVal });
            }
            return accumulator;
        }

        // Array.prototype.every
        JSValue ArrayEvery(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.every called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var result = callback.CallNative(thisArg, new[] { arr.Get(i), JSValue.FromInt32((int)i), thisVal });
                if (!JSValueConversion.ToBoolean(result))
                    return JSValue.False;
            }
            return JSValue.True;
        }

        // Array.prototype.some
        JSValue ArraySome(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.some called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var result = callback.CallNative(thisArg, new[] { arr.Get(i), JSValue.FromInt32((int)i), thisVal });
                if (JSValueConversion.ToBoolean(result))
                    return JSValue.True;
            }
            return JSValue.False;
        }

        // Array.prototype.find
        JSValue ArrayFind(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.find called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var val = arr.Get(i);
                var result = callback.CallNative(thisArg, new[] { val, JSValue.FromInt32((int)i), thisVal });
                if (JSValueConversion.ToBoolean(result))
                    return val;
            }
            return JSValue.Undefined;
        }

        // Array.prototype.findIndex
        JSValue ArrayFindIndex(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.findIndex called on non-array");
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction callback))
                return ThrowTypeError("Callback is not a function");
            var arr = thisVal.AsObject();
            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            for (uint i = 0; i < arr.ArrayLength; i++)
            {
                var result = callback.CallNative(thisArg, new[] { arr.Get(i), JSValue.FromInt32((int)i), thisVal });
                if (JSValueConversion.ToBoolean(result))
                    return JSValue.FromInt32((int)i);
            }
            return JSValue.FromInt32(-1);
        }

        // Array.prototype.fill
        JSValue ArrayFill(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.fill called on non-array");
            var arr = thisVal.AsObject();
            var value = args.Length > 0 ? args[0] : JSValue.Undefined;
            var len = (int)arr.ArrayLength;
            int start = args.Length > 1 ? args[1].ToInt32() : 0;
            int end = args.Length > 2 && !args[2].IsUndefined ? args[2].ToInt32() : len;
            if (start < 0) start = Math.Max(len + start, 0);
            if (end < 0) end = Math.Max(len + end, 0);
            start = Math.Min(start, len);
            end = Math.Min(end, len);
            for (int i = start; i < end; i++)
            {
                arr.Set((uint)i, value);
            }
            return thisVal;
        }

        // Array.prototype.flat (simple, depth=1)
        JSValue ArrayFlat(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.flat called on non-array");
            var arr = thisVal.AsObject();
            int depth = args.Length > 0 ? args[0].ToInt32() : 1;
            var result = new JSObject(arrayProto, JSClassId.Array);
            FlattenArray(arr, result, depth, 0);
            return JSValue.FromObject(result);
        }

        void FlattenArray(JSObject source, JSObject target, int depth, uint targetIndex)
        {
            for (uint i = 0; i < source.ArrayLength; i++)
            {
                var val = source.Get(i);
                if (depth > 0 && val.IsObject && val.AsObject().ClassId == JSClassId.Array)
                {
                    FlattenArray(val.AsObject(), target, depth - 1, target.ArrayLength);
                }
                else
                {
                    target.Set(target.ArrayLength, val);
                }
            }
        }

        // Array.prototype.at (ES2022)
        JSValue ArrayAt(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Array)
                return ThrowTypeError("Array.prototype.at called on non-array");
            var arr = thisVal.AsObject();
            var len = (int)arr.ArrayLength;
            int index = args.Length > 0 ? args[0].ToInt32() : 0;
            if (index < 0) index = len + index;
            if (index < 0 || index >= len)
                return JSValue.Undefined;
            return arr.Get((uint)index);
        }

        // Array.prototype.toString
        JSValue ArrayToString(JSValue thisVal, JSValue[] args)
        {
            return ArrayJoin(thisVal, Array.Empty<JSValue>());
        }

        // Array.isArray static method
        JSValue ArrayIsArray(JSValue thisVal, JSValue[] args)
        {
            var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
            return JSValue.FromBoolean(arg.IsObject && arg.AsObject().ClassId == JSClassId.Array);
        }

        // Array.from static method
        JSValue ArrayFrom(JSValue thisVal, JSValue[] args)
        {
            var arrayLike = args.Length > 0 ? args[0] : JSValue.Undefined;
            JSFunction? mapFn = args.Length > 1 && args[1].IsObject ? args[1].AsObject() as JSFunction : null;
            var thisArg = args.Length > 2 ? args[2] : JSValue.Undefined;
            
            var result = new JSObject(arrayProto, JSClassId.Array);
            if (!arrayLike.IsObject)
                return JSValue.FromObject(result);
            
            var obj = arrayLike.AsObject();
            var lengthVal = obj.Get("length");
            if (!lengthVal.IsNumber)
                return JSValue.FromObject(result);
            
            var len = lengthVal.ToInt32();
            for (int i = 0; i < len; i++)
            {
                var val = obj.Get((uint)i);
                if (mapFn != null)
                    val = mapFn.CallNative(thisArg, new[] { val, JSValue.FromInt32(i) });
                result.Set((uint)i, val);
            }
            return JSValue.FromObject(result);
        }

        // Array.of static method
        JSValue ArrayOf(JSValue thisVal, JSValue[] args)
        {
            var result = new JSObject(arrayProto, JSClassId.Array);
            for (int i = 0; i < args.Length; i++)
            {
                result.Set((uint)i, args[i]);
            }
            return JSValue.FromObject(result);
        }

        arrayProto.Set("push", JSValue.FromObject(new JSFunction(ArrayPush, "push", 1, functionProto)));
        arrayProto.Set("pop", JSValue.FromObject(new JSFunction(ArrayPop, "pop", 0, functionProto)));
        arrayProto.Set("shift", JSValue.FromObject(new JSFunction(ArrayShift, "shift", 0, functionProto)));
        arrayProto.Set("unshift", JSValue.FromObject(new JSFunction(ArrayUnshift, "unshift", 1, functionProto)));
        arrayProto.Set("slice", JSValue.FromObject(new JSFunction(ArraySlice, "slice", 2, functionProto)));
        arrayProto.Set("splice", JSValue.FromObject(new JSFunction(ArraySplice, "splice", 2, functionProto)));
        arrayProto.Set("concat", JSValue.FromObject(new JSFunction(ArrayConcat, "concat", 1, functionProto)));
        arrayProto.Set("join", JSValue.FromObject(new JSFunction(ArrayJoin, "join", 1, functionProto)));
        arrayProto.Set("reverse", JSValue.FromObject(new JSFunction(ArrayReverse, "reverse", 0, functionProto)));
        arrayProto.Set("indexOf", JSValue.FromObject(new JSFunction(ArrayIndexOf, "indexOf", 1, functionProto)));
        arrayProto.Set("lastIndexOf", JSValue.FromObject(new JSFunction(ArrayLastIndexOf, "lastIndexOf", 1, functionProto)));
        arrayProto.Set("includes", JSValue.FromObject(new JSFunction(ArrayIncludes, "includes", 1, functionProto)));
        arrayProto.Set("forEach", JSValue.FromObject(new JSFunction(ArrayForEach, "forEach", 1, functionProto)));
        arrayProto.Set("map", JSValue.FromObject(new JSFunction(ArrayMap, "map", 1, functionProto)));
        arrayProto.Set("filter", JSValue.FromObject(new JSFunction(ArrayFilter, "filter", 1, functionProto)));
        arrayProto.Set("reduce", JSValue.FromObject(new JSFunction(ArrayReduce, "reduce", 1, functionProto)));
        arrayProto.Set("reduceRight", JSValue.FromObject(new JSFunction(ArrayReduceRight, "reduceRight", 1, functionProto)));
        arrayProto.Set("every", JSValue.FromObject(new JSFunction(ArrayEvery, "every", 1, functionProto)));
        arrayProto.Set("some", JSValue.FromObject(new JSFunction(ArraySome, "some", 1, functionProto)));
        arrayProto.Set("find", JSValue.FromObject(new JSFunction(ArrayFind, "find", 1, functionProto)));
        arrayProto.Set("findIndex", JSValue.FromObject(new JSFunction(ArrayFindIndex, "findIndex", 1, functionProto)));
        arrayProto.Set("fill", JSValue.FromObject(new JSFunction(ArrayFill, "fill", 1, functionProto)));
        arrayProto.Set("flat", JSValue.FromObject(new JSFunction(ArrayFlat, "flat", 0, functionProto)));
        arrayProto.Set("at", JSValue.FromObject(new JSFunction(ArrayAt, "at", 1, functionProto)));
        arrayProto.Set("toString", JSValue.FromObject(new JSFunction(ArrayToString, "toString", 0, functionProto)));

        // Static methods on Array constructor
        arrayCtorFn.Set("isArray", JSValue.FromObject(new JSFunction(ArrayIsArray, "isArray", 1, functionProto)));
        arrayCtorFn.Set("from", JSValue.FromObject(new JSFunction(ArrayFrom, "from", 1, functionProto)));
        arrayCtorFn.Set("of", JSValue.FromObject(new JSFunction(ArrayOf, "of", 0, functionProto)));

        _globalObject.Set("Array", JSValue.FromObject(arrayCtorFn));
    }

    private void InitializeRegExpConstructor()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        var regexProto = new JSObject(objectProto, JSClassId.RegExp);
        SetClassPrototype(JSClassId.RegExp, regexProto);

        JSValue RegExpCtor(JSValue thisVal, JSValue[] args)
        {
            // RegExp(pattern, flags)
            string pattern = args.Length > 0 ? JSValueConversion.ToString(args[0]) : string.Empty;
            string flags = args.Length > 1 ? JSValueConversion.ToString(args[1]) : string.Empty;

            var options = System.Text.RegularExpressions.RegexOptions.ECMAScript;
            bool global = false;
            bool ignoreCase = false;
            bool multiline = false;

            foreach (var ch in flags)
            {
                switch (ch)
                {
                    case 'g': global = true; break;
                    case 'i': ignoreCase = true; options |= System.Text.RegularExpressions.RegexOptions.IgnoreCase; break;
                    case 'm': multiline = true; options |= System.Text.RegularExpressions.RegexOptions.Multiline; break;
                    default:
                        return ThrowSyntaxError($"Invalid regular expression flag '{ch}'");
                }
            }

            var re = new System.Text.RegularExpressions.Regex(pattern, options);
            var obj = new JSObject(regexProto, JSClassId.RegExp)
            {
                HostData = re
            };
            obj.Set("source", JSValue.FromString(pattern));
            obj.Set("flags", JSValue.FromString(flags));
            obj.Set("global", JSValue.FromBoolean(global));
            obj.Set("ignoreCase", JSValue.FromBoolean(ignoreCase));
            obj.Set("multiline", JSValue.FromBoolean(multiline));
            obj.Set("lastIndex", JSValue.FromInt32(0));
            return JSValue.FromObject(obj);
        }

        var regexCtorFn = new JSFunction(RegExpCtor, "RegExp", 2, functionProto);
        regexCtorFn.Set("prototype", JSValue.FromObject(regexProto));
        regexProto.Set("constructor", JSValue.FromObject(regexCtorFn));

        // RegExp.prototype.exec
        JSValue RegExpExec(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.RegExp)
                return ThrowTypeError("RegExp.prototype.exec called on non-RegExp");

            var obj = thisVal.AsObject();
            var re = obj.HostData as System.Text.RegularExpressions.Regex;
            var input = args.Length > 0 ? JSValueConversion.ToString(args[0]) : string.Empty;
            bool global = obj.Get("global").ToBoolean();
            int startIndex = 0;
            if (global)
            {
                startIndex = obj.Get("lastIndex").ToInt32();
            }

            var match = re?.Match(input, Math.Max(0, Math.Min(startIndex, input.Length)));
            if (match == null || !match.Success)
            {
                if (global)
                    obj.Set("lastIndex", JSValue.FromInt32(0));
                return JSValue.Null;
            }

            if (global)
            {
                obj.Set("lastIndex", JSValue.FromInt32(match.Index + match.Length));
            }

            // Build result array
            var arr = new JSObject(GetClassPrototype(JSClassId.Array), JSClassId.Array);
            for (int i = 0; i < match.Groups.Count; i++)
            {
                var val = match.Groups[i].Value;
                arr.Set((uint)i, JSValue.FromString(val));
            }
            arr.Set("length", JSValue.FromInt32(match.Groups.Count));
            arr.Set("index", JSValue.FromInt32(match.Index));
            arr.Set("input", JSValue.FromString(input));
            return JSValue.FromObject(arr);
        }

        // RegExp.prototype.test
        JSValue RegExpTest(JSValue thisVal, JSValue[] args)
        {
            var res = RegExpExec(thisVal, args);
            return JSValue.FromBoolean(!res.IsNull);
        }

        regexProto.Set("exec", JSValue.FromObject(new JSFunction(RegExpExec, "exec", 1, functionProto)));
        regexProto.Set("test", JSValue.FromObject(new JSFunction(RegExpTest, "test", 1, functionProto)));
        regexProto.Set("toString", JSValue.FromObject(new JSFunction((thisVal, args) =>
        {
            if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.RegExp)
                return ThrowTypeError("RegExp.prototype.toString called on non-RegExp");
            var obj = thisVal.AsObject();
            var src = obj.Get("source").ToString();
            var flags = obj.Get("flags").ToString();
            return JSValue.FromString($"/{src}/{flags}");
        }, "toString", 0, functionProto)));

        _globalObject.Set("RegExp", JSValue.FromObject(regexCtorFn));
    }

    private void InitializeJSON()
    {
        var jsonObj = new JSObject(GetClassPrototype(JSClassId.Object), JSClassId.Object);

        JSValue JsonParse(JSValue thisVal, JSValue[] args)
        {
            var json = args.Length > 0 ? JSValueConversion.ToString(args[0]) : string.Empty;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var val = FromJsonElement(doc.RootElement);
                return val;
            }
            catch (System.Text.Json.JsonException ex)
            {
                return ThrowSyntaxError(ex.Message);
            }
        }

        JSValue JsonStringify(JSValue thisVal, JSValue[] args)
        {
            var value = args.Length > 0 ? args[0] : JSValue.Undefined;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(ToJsonCompatible(value));
                return JSValue.FromString(json);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return ThrowTypeError(ex.Message);
            }
        }

        jsonObj.Set("parse", JSValue.FromObject(new JSFunction(JsonParse, "parse", 1, GetClassPrototype(JSClassId.CFunction))));
        jsonObj.Set("stringify", JSValue.FromObject(new JSFunction(JsonStringify, "stringify", 1, GetClassPrototype(JSClassId.CFunction))));
        _globalObject.Set("JSON", JSValue.FromObject(jsonObj));
    }

    private static JSValue FromJsonElement(System.Text.Json.JsonElement elem)
    {
        switch (elem.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null:
                return JSValue.Null;
            case System.Text.Json.JsonValueKind.Undefined:
                return JSValue.Undefined;
            case System.Text.Json.JsonValueKind.False:
                return JSValue.False;
            case System.Text.Json.JsonValueKind.True:
                return JSValue.True;
            case System.Text.Json.JsonValueKind.Number:
                if (elem.TryGetInt64(out long l))
                    return JSValue.FromInt32((int)l);
                if (elem.TryGetDouble(out double d))
                    return JSValue.FromDouble(d);
                return JSValue.FromDouble(double.NaN);
            case System.Text.Json.JsonValueKind.String:
                return JSValue.FromString(elem.GetString() ?? string.Empty);
            case System.Text.Json.JsonValueKind.Array:
                var arr = new JSObject(null, JSClassId.Array);
                int idx = 0;
                foreach (var item in elem.EnumerateArray())
                {
                    arr.Set((uint)idx++, FromJsonElement(item));
                }
                arr.Set("length", JSValue.FromInt32(idx));
                return JSValue.FromObject(arr);
            case System.Text.Json.JsonValueKind.Object:
                var obj = new JSObject(null, JSClassId.Object);
                foreach (var prop in elem.EnumerateObject())
                {
                    obj.Set(prop.Name, FromJsonElement(prop.Value));
                }
                return JSValue.FromObject(obj);
            default:
                return JSValue.Undefined;
        }
    }

    private static object? ToJsonCompatible(JSValue value)
    {
        switch (value.Tag)
        {
            case JSValueType.Null:
            case JSValueType.Uninitialized:
            case JSValueType.Undefined:
                return null;
            case JSValueType.Bool:
                return value.IsTrue;
            case JSValueType.Int:
                return value.ToInt32();
            case JSValueType.Float64:
                return value.ToDouble();
            case JSValueType.String:
                return value.ToString();
            case JSValueType.Object:
                var obj = value.AsObject();
                if (obj.ClassId == JSClassId.Array)
                {
                    var max = (int)obj.ArrayLength;
                    var list = new List<object?>();
                    for (int i = 0; i < max; i++)
                    {
                        list.Add(ToJsonCompatible(obj.Get((uint)i)));
                    }
                    return list;
                }
                else
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var kvp in obj.GetOwnProperties())
                    {
                        dict[kvp.Key] = ToJsonCompatible(kvp.Value.Value);
                    }
                    return dict;
                }
            default:
                return null;
        }
    }

    private void InitializePromise()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        var promiseProto = new JSObject(objectProto, JSClassId.Object);

        const string StateKey = "[[PromiseState]]";
        const string ResultKey = "[[PromiseResult]]";
        const string Fulfilled = "fulfilled";
        const string Rejected = "rejected";
        const string Pending = "pending";
        const string FulfillReactionsKey = "[[FulfillReactions]]";
        const string RejectReactionsKey = "[[RejectReactions]]";

        JSValue PromiseCtor(JSValue thisVal, JSValue[] args)
        {
            if (args.Length == 0 || !args[0].IsObject || !(args[0].AsObject() is JSFunction executor))
            {
                return ThrowTypeError("Promise resolver is not a function");
            }

            var promise = new JSObject(promiseProto, JSClassId.Object);
            promise.Set(StateKey, JSValue.FromString(Pending));
            promise.Set(ResultKey, JSValue.Undefined);

            JSFunction resolveFn = new JSFunction((thisArg, a) =>
            {
                if (promise.Get(StateKey).ToString() != Pending)
                    return JSValue.Undefined;
                promise.Set(StateKey, JSValue.FromString(Fulfilled));
                promise.Set(ResultKey, a.Length > 0 ? a[0] : JSValue.Undefined);
                EnqueueMicrotask(() => RunPromiseReactions(promise, fulfilled: true));
                return JSValue.Undefined;
            }, name: "resolve", length: 1, prototype: functionProto);

            JSFunction rejectFn = new JSFunction((thisArg, a) =>
            {
                if (promise.Get(StateKey).ToString() != Pending)
                    return JSValue.Undefined;
                promise.Set(StateKey, JSValue.FromString(Rejected));
                promise.Set(ResultKey, a.Length > 0 ? a[0] : JSValue.Undefined);
                EnqueueMicrotask(() => RunPromiseReactions(promise, fulfilled: false));
                return JSValue.Undefined;
            }, name: "reject", length: 1, prototype: functionProto);

            // Executor
            executor.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(resolveFn), JSValue.FromObject(rejectFn) });

            return JSValue.FromObject(promise);
        }

        var promiseCtorFn = new JSFunction(PromiseCtor, "Promise", 1, functionProto);
        promiseCtorFn.Set("prototype", JSValue.FromObject(promiseProto));
        promiseProto.Set("constructor", JSValue.FromObject(promiseCtorFn));

        // then
        JSValue PromiseThen(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject)
                return ThrowTypeError("Promise.then called on non-object");
            var promise = thisVal.AsObject();

            var onFulfilled = args.Length > 0 && args[0].IsObject ? args[0].AsObject() as JSFunction : null;
            var onRejected = args.Length > 1 && args[1].IsObject ? args[1].AsObject() as JSFunction : null;

            var nextPromiseVal = PromiseCtor(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction((thisArg, a) => JSValue.Undefined, prototype: functionProto)) });
            var next = nextPromiseVal.AsObject();
            next.Set(StateKey, JSValue.FromString(Pending));
            next.Set(ResultKey, JSValue.Undefined);

            void Enqueue(JSObject target, string key, (JSObject promise, JSFunction? onFulfilled, JSFunction? onRejected, JSObject next) reaction)
            {
                var listVal = target.Get(key);
                List<(JSObject, JSFunction?, JSFunction?, JSObject)> list;
                if (listVal.IsObject && listVal.AsObject().HostData is List<(JSObject, JSFunction?, JSFunction?, JSObject)> hostList)
                {
                    list = hostList;
                }
                else
                {
                    list = new List<(JSObject, JSFunction?, JSFunction?, JSObject)>();
                    var holder = new JSObject(null, JSClassId.Object) { HostData = list };
                    target.Set(key, JSValue.FromObject(holder));
                }
                list.Add(reaction);
            }

            var state = promise.Get(StateKey).ToString();
            if (state == Fulfilled || state == Rejected)
            {
                EnqueueMicrotask(() => RunPromiseReaction((promise, onFulfilled, onRejected, next), state == Fulfilled));
            }
            else
            {
                Enqueue(promise, FulfillReactionsKey, (promise, onFulfilled, onRejected, next));
                Enqueue(promise, RejectReactionsKey, (promise, onFulfilled, onRejected, next));
            }

            return nextPromiseVal;
        }

        JSValue PromiseCatch(JSValue thisVal, JSValue[] args)
        {
            return PromiseThen(thisVal, new[] { JSValue.Undefined, args.Length > 0 ? args[0] : JSValue.Undefined });
        }

        JSValue PromiseFinally(JSValue thisVal, JSValue[] args)
        {
            var handler = args.Length > 0 && args[0].IsObject ? args[0].AsObject() as JSFunction : null;
            JSValue Wrapper(JSValue value, bool isRejection)
            {
                if (handler != null)
                    handler.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
                return value;
            }
            var onFulfilled = new JSFunction((_, a) => Wrapper(a.Length > 0 ? a[0] : JSValue.Undefined, false), "", 1, functionProto);
            var onRejected = new JSFunction((_, a) => Wrapper(a.Length > 0 ? a[0] : JSValue.Undefined, true), "", 1, functionProto);
            return PromiseThen(thisVal, new[] { JSValue.FromObject(onFulfilled), JSValue.FromObject(onRejected) });
        }

        promiseProto.Set("then", JSValue.FromObject(new JSFunction(PromiseThen, "then", 2, functionProto)));
        promiseProto.Set("catch", JSValue.FromObject(new JSFunction(PromiseCatch, "catch", 1, functionProto)));
        promiseProto.Set("finally", JSValue.FromObject(new JSFunction(PromiseFinally, "finally", 1, functionProto)));

        // Statics
        JSValue PromiseResolve(JSValue thisVal, JSValue[] args)
        {
            if (args.Length > 0 && args[0].IsObject && args[0].AsObject().Prototype == promiseProto)
                return args[0];
            var val = args.Length > 0 ? args[0] : JSValue.Undefined;
            var p = PromiseCtor(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction((_, a) => JSValue.Undefined, prototype: functionProto)) }).AsObject();
            p.Set(StateKey, JSValue.FromString(Fulfilled));
            p.Set(ResultKey, val);
            return JSValue.FromObject(p);
        }

        JSValue PromiseReject(JSValue thisVal, JSValue[] args)
        {
            var val = args.Length > 0 ? args[0] : JSValue.Undefined;
            var p = PromiseCtor(JSValue.Undefined, new[] { JSValue.FromObject(new JSFunction((_, a) => JSValue.Undefined, prototype: functionProto)) }).AsObject();
            p.Set(StateKey, JSValue.FromString(Rejected));
            p.Set(ResultKey, val);
            return JSValue.FromObject(p);
        }

        promiseCtorFn.Set("resolve", JSValue.FromObject(new JSFunction(PromiseResolve, "resolve", 1, functionProto)));
        promiseCtorFn.Set("reject", JSValue.FromObject(new JSFunction(PromiseReject, "reject", 1, functionProto)));

        _globalObject.Set("Promise", JSValue.FromObject(promiseCtorFn));

        void RunPromiseReactions(JSObject promise, bool fulfilled)
        {
            var key = fulfilled ? FulfillReactionsKey : RejectReactionsKey;
            var reactionsVal = promise.Get(key);
            if (!reactionsVal.IsObject) return;
            if (reactionsVal.AsObject().HostData is List<(JSObject, JSFunction?, JSFunction?, JSObject)> reactions)
            {
                foreach (var reaction in reactions.ToList())
                {
                    RunPromiseReaction(reaction, fulfilled);
                }
                reactions.Clear();
            }
        }

        void RunPromiseReaction((JSObject promise, JSFunction? onFulfilled, JSFunction? onRejected, JSObject next) reaction, bool fulfilled)
        {
            var (promise, onFulfilled, onRejected, next) = reaction;
            var result = promise.Get(ResultKey);

            try
            {
                JSValue handlerResult;
                var handler = fulfilled ? onFulfilled : onRejected;
                if (handler != null)
                {
                    handlerResult = handler.CallNative(JSValue.Undefined, new[] { result });
                }
                else
                {
                    handlerResult = result;
                }
                next.Set(StateKey, JSValue.FromString(Fulfilled));
                next.Set(ResultKey, handlerResult);
            }
            catch (Exception ex)
            {
                next.Set(StateKey, JSValue.FromString(Rejected));
                next.Set(ResultKey, JSValue.FromString(ex.Message));
            }
        }
    }

    private void InitializeCollections()
    {
        InitMap();
        InitSet();
        InitWeakMap();
        InitWeakSet();

        void InitMap()
        {
            var functionProto = GetClassPrototype(JSClassId.CFunction)!;
            var objectProto = GetClassPrototype(JSClassId.Object)!;
            var mapProto = new JSObject(objectProto, JSClassId.Map);
            SetClassPrototype(JSClassId.Map, mapProto);

            JSValue MapCtor(JSValue thisVal, JSValue[] args)
            {
                var mapObj = new JSObject(mapProto, JSClassId.Map)
                {
                    HostData = new Dictionary<JSValue, JSValue>(new JSValueComparer())
                };
                return JSValue.FromObject(mapObj);
            }

            var mapCtorFn = new JSFunction(MapCtor, "Map", 0, functionProto);
            mapCtorFn.Set("prototype", JSValue.FromObject(mapProto));
            mapProto.Set("constructor", JSValue.FromObject(mapCtorFn));

            JSValue MapSet(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.set called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                var value = args.Length > 1 ? args[1] : JSValue.Undefined;
                map[key] = value;
                return thisVal;
            }

            JSValue MapGet(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.get called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                return map.TryGetValue(key, out var val) ? val : JSValue.Undefined;
            }

            JSValue MapHas(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.has called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(map.ContainsKey(key));
            }

            JSValue MapDelete(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.delete called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                var removed = map.Remove(key);
                return JSValue.FromBoolean(removed);
            }

            JSValue MapClear(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.clear called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                map.Clear();
                return JSValue.Undefined;
            }

            JSValue MapSize(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Map)
                    return ThrowTypeError("Map.prototype.size getter called on non-Map");
                var map = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                return JSValue.FromInt32(map.Count);
            }

            mapProto.Set("set", JSValue.FromObject(new JSFunction(MapSet, "set", 2, functionProto)));
            mapProto.Set("get", JSValue.FromObject(new JSFunction(MapGet, "get", 1, functionProto)));
            mapProto.Set("has", JSValue.FromObject(new JSFunction(MapHas, "has", 1, functionProto)));
            mapProto.Set("delete", JSValue.FromObject(new JSFunction(MapDelete, "delete", 1, functionProto)));
            mapProto.Set("clear", JSValue.FromObject(new JSFunction(MapClear, "clear", 0, functionProto)));
            mapProto.Set("size", JSValue.FromObject(new JSFunction(MapSize, "size", 0, functionProto)));

            _globalObject.Set("Map", JSValue.FromObject(mapCtorFn));
        }

        void InitSet()
        {
            var functionProto = GetClassPrototype(JSClassId.CFunction)!;
            var objectProto = GetClassPrototype(JSClassId.Object)!;
            var setProto = new JSObject(objectProto, JSClassId.Set);
            SetClassPrototype(JSClassId.Set, setProto);

            JSValue SetCtor(JSValue thisVal, JSValue[] args)
            {
                var obj = new JSObject(setProto, JSClassId.Set)
                {
                    HostData = new HashSet<JSValue>(new JSValueComparer())
                };
                return JSValue.FromObject(obj);
            }

            var setCtorFn = new JSFunction(SetCtor, "Set", 0, functionProto);
            setCtorFn.Set("prototype", JSValue.FromObject(setProto));
            setProto.Set("constructor", JSValue.FromObject(setCtorFn));

            JSValue SetAdd(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Set)
                    return ThrowTypeError("Set.prototype.add called on non-Set");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                set.Add(val);
                return thisVal;
            }

            JSValue SetHas(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Set)
                    return ThrowTypeError("Set.prototype.has called on non-Set");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(set.Contains(val));
            }

            JSValue SetDelete(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Set)
                    return ThrowTypeError("Set.prototype.delete called on non-Set");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(set.Remove(val));
            }

            JSValue SetClear(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Set)
                    return ThrowTypeError("Set.prototype.clear called on non-Set");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                set.Clear();
                return JSValue.Undefined;
            }

            JSValue SetSize(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.Set)
                    return ThrowTypeError("Set.prototype.size getter called on non-Set");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                return JSValue.FromInt32(set.Count);
            }

            setProto.Set("add", JSValue.FromObject(new JSFunction(SetAdd, "add", 1, functionProto)));
            setProto.Set("has", JSValue.FromObject(new JSFunction(SetHas, "has", 1, functionProto)));
            setProto.Set("delete", JSValue.FromObject(new JSFunction(SetDelete, "delete", 1, functionProto)));
            setProto.Set("clear", JSValue.FromObject(new JSFunction(SetClear, "clear", 0, functionProto)));
            setProto.Set("size", JSValue.FromObject(new JSFunction(SetSize, "size", 0, functionProto)));

            _globalObject.Set("Set", JSValue.FromObject(setCtorFn));
        }

        void InitWeakMap()
        {
            var functionProto = GetClassPrototype(JSClassId.CFunction)!;
            var objectProto = GetClassPrototype(JSClassId.Object)!;
            var weakMapProto = new JSObject(objectProto, JSClassId.WeakMap);
            SetClassPrototype(JSClassId.WeakMap, weakMapProto);

            JSValue WeakMapCtor(JSValue thisVal, JSValue[] args)
            {
                var obj = new JSObject(weakMapProto, JSClassId.WeakMap)
                {
                    HostData = new Dictionary<JSValue, JSValue>(new JSValueComparer())
                };
                return JSValue.FromObject(obj);
            }

            var ctor = new JSFunction(WeakMapCtor, "WeakMap", 0, functionProto);
            ctor.Set("prototype", JSValue.FromObject(weakMapProto));
            weakMapProto.Set("constructor", JSValue.FromObject(ctor));

            JSValue WeakMapSet(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakMap)
                    return ThrowTypeError("WeakMap.prototype.set called on non-WeakMap");
                var dict = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                if (!key.IsObject)
                    return ThrowTypeError("Invalid value used as weak map key");
                var val = args.Length > 1 ? args[1] : JSValue.Undefined;
                dict[key] = val;
                return thisVal;
            }

            JSValue WeakMapGet(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakMap)
                    return ThrowTypeError("WeakMap.prototype.get called on non-WeakMap");
                var dict = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                return dict.TryGetValue(key, out var val) ? val : JSValue.Undefined;
            }

            JSValue WeakMapHas(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakMap)
                    return ThrowTypeError("WeakMap.prototype.has called on non-WeakMap");
                var dict = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(dict.ContainsKey(key));
            }

            JSValue WeakMapDelete(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakMap)
                    return ThrowTypeError("WeakMap.prototype.delete called on non-WeakMap");
                var dict = (Dictionary<JSValue, JSValue>)thisVal.AsObject().HostData!;
                var key = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(dict.Remove(key));
            }

            weakMapProto.Set("set", JSValue.FromObject(new JSFunction(WeakMapSet, "set", 2, functionProto)));
            weakMapProto.Set("get", JSValue.FromObject(new JSFunction(WeakMapGet, "get", 1, functionProto)));
            weakMapProto.Set("has", JSValue.FromObject(new JSFunction(WeakMapHas, "has", 1, functionProto)));
            weakMapProto.Set("delete", JSValue.FromObject(new JSFunction(WeakMapDelete, "delete", 1, functionProto)));

            _globalObject.Set("WeakMap", JSValue.FromObject(ctor));
        }

        void InitWeakSet()
        {
            var functionProto = GetClassPrototype(JSClassId.CFunction)!;
            var objectProto = GetClassPrototype(JSClassId.Object)!;
            var weakSetProto = new JSObject(objectProto, JSClassId.WeakSet);
            SetClassPrototype(JSClassId.WeakSet, weakSetProto);

            JSValue WeakSetCtor(JSValue thisVal, JSValue[] args)
            {
                var obj = new JSObject(weakSetProto, JSClassId.WeakSet)
                {
                    HostData = new HashSet<JSValue>(new JSValueComparer())
                };
                return JSValue.FromObject(obj);
            }

            var ctor = new JSFunction(WeakSetCtor, "WeakSet", 0, functionProto);
            ctor.Set("prototype", JSValue.FromObject(weakSetProto));
            weakSetProto.Set("constructor", JSValue.FromObject(ctor));

            JSValue WeakSetAdd(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakSet)
                    return ThrowTypeError("WeakSet.prototype.add called on non-WeakSet");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                if (!val.IsObject)
                    return ThrowTypeError("Invalid value used in weak set");
                set.Add(val);
                return thisVal;
            }

            JSValue WeakSetHas(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakSet)
                    return ThrowTypeError("WeakSet.prototype.has called on non-WeakSet");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(set.Contains(val));
            }

            JSValue WeakSetDelete(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject().ClassId != JSClassId.WeakSet)
                    return ThrowTypeError("WeakSet.prototype.delete called on non-WeakSet");
                var set = (HashSet<JSValue>)thisVal.AsObject().HostData!;
                var val = args.Length > 0 ? args[0] : JSValue.Undefined;
                return JSValue.FromBoolean(set.Remove(val));
            }

            weakSetProto.Set("add", JSValue.FromObject(new JSFunction(WeakSetAdd, "add", 1, functionProto)));
            weakSetProto.Set("has", JSValue.FromObject(new JSFunction(WeakSetHas, "has", 1, functionProto)));
            weakSetProto.Set("delete", JSValue.FromObject(new JSFunction(WeakSetDelete, "delete", 1, functionProto)));

            _globalObject.Set("WeakSet", JSValue.FromObject(ctor));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases all resources used by this context.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        // Remove from runtime
        _runtime.RemoveContext(this);

        // Clear modules
        _loadedModules.Clear();

        // Clear exception
        _currentException = JSValue.Undefined;
        _hasException = false;

        // Clear stack frames
        _currentStackFrame = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(JSContext));
    }

    #endregion

    #region Debugger Support

    private string DebuggerDisplay
    {
        get
        {
            var status = _isDisposed ? "Disposed" : $"{_loadedModules.Count} modules";
            if (_hasException)
                status += ", has exception";
            return $"JSContext [{status}]";
        }
    }

    #endregion
}

/// <summary>
/// Represents a module definition.
/// </summary>
/// <remarks>
/// This is a placeholder for module support that will be expanded later.
/// </remarks>
public sealed class JSModuleDef
{
    /// <summary>
    /// Gets the module name/specifier.
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// Gets a value indicating whether the module has been evaluated.
    /// </summary>
    public bool IsEvaluated { get; internal set; }

    /// <summary>
    /// Gets the module namespace object.
    /// </summary>
    public JSObject? Namespace { get; internal set; }

    /// <summary>
    /// Creates a new module definition.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    public JSModuleDef(string moduleName)
    {
        ModuleName = moduleName;
    }
}

/// <summary>
/// Represents a stack frame during JavaScript execution.
/// </summary>
/// <remarks>
/// This is the internal call frame used during bytecode interpretation.
/// It differs from <see cref="JSStackFrame"/> which is used for stack trace display.
/// </remarks>
public sealed class JSCallFrame
{
    /// <summary>
    /// Gets or sets the previous stack frame.
    /// </summary>
    public JSCallFrame? PreviousFrame { get; set; }

    /// <summary>
    /// Gets the current function being executed.
    /// </summary>
    public JSFunction? Function { get; }

    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the current line number.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets the current column number.
    /// </summary>
    public int ColumnNumber { get; set; }

    /// <summary>
    /// Gets or sets the current program counter (bytecode index).
    /// </summary>
    public int ProgramCounter { get; set; }

    /// <summary>
    /// Gets the arguments passed to the function.
    /// </summary>
    public JSValue[] Arguments { get; }

    /// <summary>
    /// Gets or sets the local variables.
    /// </summary>
    public JSValue[]? LocalVariables { get; set; }

    /// <summary>
    /// Gets the JavaScript mode flags (strict mode, etc.).
    /// </summary>
    public JSModeFlags Mode { get; }

    /// <summary>
    /// Creates a new stack frame.
    /// </summary>
    public JSCallFrame(
        JSFunction? function,
        string? functionName,
        string? fileName,
        JSValue[] arguments,
        JSModeFlags mode = JSModeFlags.None)
    {
        Function = function;
        FunctionName = functionName;
        FileName = fileName;
        Arguments = arguments;
        Mode = mode;
    }
}

/// <summary>
/// JavaScript execution mode flags.
/// </summary>
[Flags]
public enum JSModeFlags
{
    /// <summary>
    /// No special mode.
    /// </summary>
    None = 0,

    /// <summary>
    /// Strict mode is enabled.
    /// </summary>
    Strict = 1 << 0,

    /// <summary>
    /// Async function mode.
    /// </summary>
    Async = 1 << 2,

    /// <summary>
    /// Backtrace barrier - stop backtrace before this frame.
    /// </summary>
    BacktraceBarrier = 1 << 3
}
