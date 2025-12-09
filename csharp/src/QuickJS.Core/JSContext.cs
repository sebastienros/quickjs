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
        InitializeTypedArrays();
        InitializeDate();
        InitializeSymbol();
        InitializeReflect();
        InitializeProxy();
        InitializeConsole();
        InitializeTimers();
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

    private void InitializeTypedArrays()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        // Initialize ArrayBuffer
        InitializeArrayBuffer();

        // Initialize DataView
        InitializeDataView();

        // Initialize all TypedArray types
        InitializeTypedArrayType(TypedArrayKind.Int8, "Int8Array");
        InitializeTypedArrayType(TypedArrayKind.Uint8, "Uint8Array");
        InitializeTypedArrayType(TypedArrayKind.Uint8Clamped, "Uint8ClampedArray");
        InitializeTypedArrayType(TypedArrayKind.Int16, "Int16Array");
        InitializeTypedArrayType(TypedArrayKind.Uint16, "Uint16Array");
        InitializeTypedArrayType(TypedArrayKind.Int32, "Int32Array");
        InitializeTypedArrayType(TypedArrayKind.Uint32, "Uint32Array");
        InitializeTypedArrayType(TypedArrayKind.Float32, "Float32Array");
        InitializeTypedArrayType(TypedArrayKind.Float64, "Float64Array");
        InitializeTypedArrayType(TypedArrayKind.BigInt64, "BigInt64Array");
        InitializeTypedArrayType(TypedArrayKind.BigUint64, "BigUint64Array");

        void InitializeArrayBuffer()
        {
            var arrayBufferProto = new JSObject(objectProto, JSClassId.ArrayBuffer);
            SetClassPrototype(JSClassId.ArrayBuffer, arrayBufferProto);

            JSValue ArrayBufferCtor(JSValue thisVal, JSValue[] args)
            {
                var length = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                if (length < 0)
                    return ThrowRangeError("Invalid array buffer length");
                var buffer = new JSArrayBuffer(length);
                buffer.SetPrototype(arrayBufferProto);
                return JSValue.FromObject(buffer);
            }

            var ctor = new JSFunction(ArrayBufferCtor, "ArrayBuffer", 1, functionProto);
            ctor.Set("prototype", JSValue.FromObject(arrayBufferProto));
            arrayBufferProto.Set("constructor", JSValue.FromObject(ctor));

            // ArrayBuffer.isView
            JSValue ArrayBufferIsView(JSValue thisVal, JSValue[] args)
            {
                var arg = args.Length > 0 ? args[0] : JSValue.Undefined;
                if (!arg.IsObject) return JSValue.False;
                var obj = arg.AsObject();
                return JSValue.FromBoolean(
                    obj.ClassId.IsTypedArray() || 
                    obj.ClassId == JSClassId.DataView);
            }

            // ArrayBuffer.prototype.byteLength getter
            JSValue ArrayBufferByteLength(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.byteLength called on non-ArrayBuffer");
                return JSValue.FromInt32(ab.ByteLength);
            }

            // ArrayBuffer.prototype.slice
            JSValue ArrayBufferSlice(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.slice called on non-ArrayBuffer");
                var begin = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                int? end = args.Length > 1 && !args[1].IsUndefined 
                    ? JSValueConversion.ToInt32(args[1]) 
                    : (int?)null;
                try
                {
                    var result = ab.Slice(begin, end);
                    result.SetPrototype(arrayBufferProto);
                    return JSValue.FromObject(result);
                }
                catch (InvalidOperationException)
                {
                    return ThrowTypeError("ArrayBuffer is detached");
                }
            }

            // ArrayBuffer.prototype.transfer
            JSValue ArrayBufferTransfer(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.transfer called on non-ArrayBuffer");
                int? newLength = args.Length > 0 && !args[0].IsUndefined 
                    ? JSValueConversion.ToInt32(args[0]) 
                    : (int?)null;
                try
                {
                    var result = ab.Transfer(newLength);
                    result.SetPrototype(arrayBufferProto);
                    return JSValue.FromObject(result);
                }
                catch (InvalidOperationException ex)
                {
                    return ThrowTypeError(ex.Message);
                }
            }

            // ArrayBuffer.prototype.resize
            JSValue ArrayBufferResize(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.resize called on non-ArrayBuffer");
                var newLength = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                try
                {
                    ab.Resize(newLength);
                    return JSValue.Undefined;
                }
                catch (InvalidOperationException ex)
                {
                    return ThrowTypeError(ex.Message);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return ThrowRangeError("Invalid array buffer length");
                }
            }

            // ArrayBuffer.prototype.detached getter
            JSValue ArrayBufferDetached(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.detached called on non-ArrayBuffer");
                return JSValue.FromBoolean(ab.IsDetached);
            }

            // ArrayBuffer.prototype.resizable getter
            JSValue ArrayBufferResizable(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.resizable called on non-ArrayBuffer");
                return JSValue.FromBoolean(ab.Resizable);
            }

            // ArrayBuffer.prototype.maxByteLength getter
            JSValue ArrayBufferMaxByteLength(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSArrayBuffer ab)
                    return ThrowTypeError("ArrayBuffer.prototype.maxByteLength called on non-ArrayBuffer");
                return JSValue.FromInt32(ab.MaxByteLength < 0 ? ab.ByteLength : ab.MaxByteLength);
            }

            ctor.Set("isView", JSValue.FromObject(new JSFunction(ArrayBufferIsView, "isView", 1, functionProto)));
            arrayBufferProto.Set("byteLength", JSValue.FromObject(new JSFunction(ArrayBufferByteLength, "byteLength", 0, functionProto)));
            arrayBufferProto.Set("slice", JSValue.FromObject(new JSFunction(ArrayBufferSlice, "slice", 2, functionProto)));
            arrayBufferProto.Set("transfer", JSValue.FromObject(new JSFunction(ArrayBufferTransfer, "transfer", 0, functionProto)));
            arrayBufferProto.Set("resize", JSValue.FromObject(new JSFunction(ArrayBufferResize, "resize", 1, functionProto)));
            arrayBufferProto.Set("detached", JSValue.FromObject(new JSFunction(ArrayBufferDetached, "detached", 0, functionProto)));
            arrayBufferProto.Set("resizable", JSValue.FromObject(new JSFunction(ArrayBufferResizable, "resizable", 0, functionProto)));
            arrayBufferProto.Set("maxByteLength", JSValue.FromObject(new JSFunction(ArrayBufferMaxByteLength, "maxByteLength", 0, functionProto)));

            _globalObject.Set("ArrayBuffer", JSValue.FromObject(ctor));
        }

        void InitializeDataView()
        {
            var dataViewProto = new JSObject(objectProto, JSClassId.DataView);
            SetClassPrototype(JSClassId.DataView, dataViewProto);

            JSValue DataViewCtor(JSValue thisVal, JSValue[] args)
            {
                if (args.Length == 0 || !args[0].IsObject || args[0].AsObject() is not JSArrayBuffer buffer)
                    return ThrowTypeError("First argument must be an ArrayBuffer");
                
                var byteOffset = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                int? byteLength = args.Length > 2 && !args[2].IsUndefined 
                    ? JSValueConversion.ToInt32(args[2]) 
                    : (int?)null;

                try
                {
                    var dv = new JSDataView(buffer, byteOffset, byteLength);
                    dv.SetPrototype(dataViewProto);
                    return JSValue.FromObject(dv);
                }
                catch (InvalidOperationException ex)
                {
                    return ThrowTypeError(ex.Message);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return ThrowRangeError("Invalid DataView offset or length");
                }
            }

            var ctor = new JSFunction(DataViewCtor, "DataView", 1, functionProto);
            ctor.Set("prototype", JSValue.FromObject(dataViewProto));
            dataViewProto.Set("constructor", JSValue.FromObject(ctor));

            // DataView getters
            JSValue DataViewBuffer(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.buffer called on non-DataView");
                return JSValue.FromObject(dv.Buffer);
            }

            JSValue DataViewByteLength(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.byteLength called on non-DataView");
                return JSValue.FromInt32(dv.ByteLength);
            }

            JSValue DataViewByteOffset(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.byteOffset called on non-DataView");
                return JSValue.FromInt32(dv.ByteOffset);
            }

            // DataView get methods
            JSValue DataViewGetInt8(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getInt8 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                try { return JSValue.FromInt32(dv.GetInt8(offset)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetUint8(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getUint8 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                try { return JSValue.FromInt32(dv.GetUint8(offset)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetInt16(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getInt16 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromInt32(dv.GetInt16(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetUint16(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getUint16 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromInt32(dv.GetUint16(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetInt32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getInt32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromInt32(dv.GetInt32(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetUint32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getUint32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromDouble(dv.GetUint32(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetFloat32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getFloat32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromDouble(dv.GetFloat32(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetFloat64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getFloat64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromDouble(dv.GetFloat64(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetBigInt64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getBigInt64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromDouble(dv.GetBigInt64(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewGetBigUint64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.getBigUint64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var littleEndian = args.Length > 1 && args[1].IsTrue;
                try { return JSValue.FromDouble((double)dv.GetBigUint64(offset, littleEndian)); }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            // DataView set methods
            JSValue DataViewSetInt8(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setInt8 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (sbyte)JSValueConversion.ToInt32(args[1]) : (sbyte)0;
                try { dv.SetInt8(offset, value); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetUint8(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setUint8 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (byte)JSValueConversion.ToInt32(args[1]) : (byte)0;
                try { dv.SetUint8(offset, value); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetInt16(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setInt16 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (short)JSValueConversion.ToInt32(args[1]) : (short)0;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetInt16(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetUint16(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setUint16 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (ushort)JSValueConversion.ToInt32(args[1]) : (ushort)0;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetUint16(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetInt32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setInt32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetInt32(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetUint32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setUint32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (uint)JSValueConversion.ToInt32(args[1]) : 0u;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetUint32(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetFloat32(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setFloat32 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (float)JSValueConversion.ToNumber(args[1]) : 0f;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetFloat32(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetFloat64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setFloat64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? JSValueConversion.ToNumber(args[1]) : 0.0;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetFloat64(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetBigInt64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setBigInt64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (long)JSValueConversion.ToNumber(args[1]) : 0L;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetBigInt64(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            JSValue DataViewSetBigUint64(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSDataView dv)
                    return ThrowTypeError("DataView.prototype.setBigUint64 called on non-DataView");
                var offset = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var value = args.Length > 1 ? (ulong)JSValueConversion.ToNumber(args[1]) : 0UL;
                var littleEndian = args.Length > 2 && args[2].IsTrue;
                try { dv.SetBigUint64(offset, value, littleEndian); return JSValue.Undefined; }
                catch { return ThrowRangeError("Offset is outside the bounds of the DataView"); }
            }

            dataViewProto.Set("buffer", JSValue.FromObject(new JSFunction(DataViewBuffer, "buffer", 0, functionProto)));
            dataViewProto.Set("byteLength", JSValue.FromObject(new JSFunction(DataViewByteLength, "byteLength", 0, functionProto)));
            dataViewProto.Set("byteOffset", JSValue.FromObject(new JSFunction(DataViewByteOffset, "byteOffset", 0, functionProto)));
            dataViewProto.Set("getInt8", JSValue.FromObject(new JSFunction(DataViewGetInt8, "getInt8", 1, functionProto)));
            dataViewProto.Set("getUint8", JSValue.FromObject(new JSFunction(DataViewGetUint8, "getUint8", 1, functionProto)));
            dataViewProto.Set("getInt16", JSValue.FromObject(new JSFunction(DataViewGetInt16, "getInt16", 1, functionProto)));
            dataViewProto.Set("getUint16", JSValue.FromObject(new JSFunction(DataViewGetUint16, "getUint16", 1, functionProto)));
            dataViewProto.Set("getInt32", JSValue.FromObject(new JSFunction(DataViewGetInt32, "getInt32", 1, functionProto)));
            dataViewProto.Set("getUint32", JSValue.FromObject(new JSFunction(DataViewGetUint32, "getUint32", 1, functionProto)));
            dataViewProto.Set("getFloat32", JSValue.FromObject(new JSFunction(DataViewGetFloat32, "getFloat32", 1, functionProto)));
            dataViewProto.Set("getFloat64", JSValue.FromObject(new JSFunction(DataViewGetFloat64, "getFloat64", 1, functionProto)));
            dataViewProto.Set("getBigInt64", JSValue.FromObject(new JSFunction(DataViewGetBigInt64, "getBigInt64", 1, functionProto)));
            dataViewProto.Set("getBigUint64", JSValue.FromObject(new JSFunction(DataViewGetBigUint64, "getBigUint64", 1, functionProto)));
            dataViewProto.Set("setInt8", JSValue.FromObject(new JSFunction(DataViewSetInt8, "setInt8", 2, functionProto)));
            dataViewProto.Set("setUint8", JSValue.FromObject(new JSFunction(DataViewSetUint8, "setUint8", 2, functionProto)));
            dataViewProto.Set("setInt16", JSValue.FromObject(new JSFunction(DataViewSetInt16, "setInt16", 2, functionProto)));
            dataViewProto.Set("setUint16", JSValue.FromObject(new JSFunction(DataViewSetUint16, "setUint16", 2, functionProto)));
            dataViewProto.Set("setInt32", JSValue.FromObject(new JSFunction(DataViewSetInt32, "setInt32", 2, functionProto)));
            dataViewProto.Set("setUint32", JSValue.FromObject(new JSFunction(DataViewSetUint32, "setUint32", 2, functionProto)));
            dataViewProto.Set("setFloat32", JSValue.FromObject(new JSFunction(DataViewSetFloat32, "setFloat32", 2, functionProto)));
            dataViewProto.Set("setFloat64", JSValue.FromObject(new JSFunction(DataViewSetFloat64, "setFloat64", 2, functionProto)));
            dataViewProto.Set("setBigInt64", JSValue.FromObject(new JSFunction(DataViewSetBigInt64, "setBigInt64", 2, functionProto)));
            dataViewProto.Set("setBigUint64", JSValue.FromObject(new JSFunction(DataViewSetBigUint64, "setBigUint64", 2, functionProto)));

            _globalObject.Set("DataView", JSValue.FromObject(ctor));
        }

        void InitializeTypedArrayType(TypedArrayKind kind, string name)
        {
            var classId = JSTypedArray.GetClassId(kind);
            var bytesPerElement = JSTypedArray.GetBytesPerElement(kind);
            var typedArrayProto = new JSObject(objectProto, classId);
            SetClassPrototype(classId, typedArrayProto);

            JSValue TypedArrayCtor(JSValue thisVal, JSValue[] args)
            {
                if (args.Length == 0)
                {
                    // new TypedArray() - empty array
                    var arr = new JSTypedArray(kind, 0);
                    arr.SetPrototype(typedArrayProto);
                    return JSValue.FromObject(arr);
                }

                var arg0 = args[0];
                
                // new TypedArray(length)
                if (arg0.IsNumber)
                {
                    var length = JSValueConversion.ToInt32(arg0);
                    if (length < 0)
                        return ThrowRangeError("Invalid typed array length");
                    var arr = new JSTypedArray(kind, length);
                    arr.SetPrototype(typedArrayProto);
                    return JSValue.FromObject(arr);
                }

                // new TypedArray(buffer [, byteOffset [, length]])
                if (arg0.IsObject && arg0.AsObject() is JSArrayBuffer buffer)
                {
                    var byteOffset = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                    int? length = args.Length > 2 && !args[2].IsUndefined 
                        ? JSValueConversion.ToInt32(args[2]) 
                        : (int?)null;
                    try
                    {
                        var arr = new JSTypedArray(kind, buffer, byteOffset, length);
                        arr.SetPrototype(typedArrayProto);
                        return JSValue.FromObject(arr);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return ThrowTypeError(ex.Message);
                    }
                    catch (ArgumentException ex)
                    {
                        return ThrowRangeError(ex.Message);
                    }
                }

                // new TypedArray(typedArray) - copy from another typed array
                if (arg0.IsObject && arg0.AsObject() is JSTypedArray srcTypedArray)
                {
                    var arr = new JSTypedArray(kind, srcTypedArray.Length);
                    arr.SetPrototype(typedArrayProto);
                    for (int i = 0; i < srcTypedArray.Length; i++)
                    {
                        arr.SetElement(i, srcTypedArray.GetElement(i));
                    }
                    return JSValue.FromObject(arr);
                }

                // new TypedArray(arrayLike) - from array-like object
                if (arg0.IsObject)
                {
                    var srcObj = arg0.AsObject();
                    var lengthVal = srcObj.Get("length");
                    var length = lengthVal.IsNumber ? JSValueConversion.ToInt32(lengthVal) : 0;
                    var arr = new JSTypedArray(kind, length);
                    arr.SetPrototype(typedArrayProto);
                    for (int i = 0; i < length; i++)
                    {
                        arr.SetElement(i, srcObj.Get((uint)i));
                    }
                    return JSValue.FromObject(arr);
                }

                return ThrowTypeError("Invalid argument for typed array constructor");
            }

            var ctor = new JSFunction(TypedArrayCtor, name, 0, functionProto);
            ctor.Set("prototype", JSValue.FromObject(typedArrayProto));
            ctor.Set("BYTES_PER_ELEMENT", JSValue.FromInt32(bytesPerElement));
            typedArrayProto.Set("constructor", JSValue.FromObject(ctor));
            typedArrayProto.Set("BYTES_PER_ELEMENT", JSValue.FromInt32(bytesPerElement));

            // TypedArray.of(...items)
            JSValue TypedArrayOf(JSValue thisVal, JSValue[] args)
            {
                var arr = new JSTypedArray(kind, args.Length);
                arr.SetPrototype(typedArrayProto);
                for (int i = 0; i < args.Length; i++)
                {
                    arr.SetElement(i, args[i]);
                }
                return JSValue.FromObject(arr);
            }

            // TypedArray.from(source [, mapFn [, thisArg]])
            JSValue TypedArrayFrom(JSValue thisVal, JSValue[] args)
            {
                if (args.Length == 0)
                    return ThrowTypeError("TypedArray.from requires at least 1 argument");
                
                var source = args[0];
                if (!source.IsObject)
                    return ThrowTypeError("TypedArray.from requires an array-like object");
                
                var srcObj = source.AsObject();
                var lengthVal = srcObj.Get("length");
                var length = lengthVal.IsNumber ? JSValueConversion.ToInt32(lengthVal) : 0;
                var arr = new JSTypedArray(kind, length);
                arr.SetPrototype(typedArrayProto);

                // TODO: Support mapFn parameter
                for (int i = 0; i < length; i++)
                {
                    arr.SetElement(i, srcObj.Get((uint)i));
                }
                return JSValue.FromObject(arr);
            }

            // TypedArray.prototype.buffer
            JSValue TypedArrayBuffer(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.buffer called on non-{name}");
                return JSValue.FromObject(ta.Buffer);
            }

            // TypedArray.prototype.byteLength
            JSValue TypedArrayByteLength(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.byteLength called on non-{name}");
                return JSValue.FromInt32(ta.ByteLength);
            }

            // TypedArray.prototype.byteOffset
            JSValue TypedArrayByteOffset(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.byteOffset called on non-{name}");
                return JSValue.FromInt32(ta.ByteOffset);
            }

            // TypedArray.prototype.length
            JSValue TypedArrayLength(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.length called on non-{name}");
                return JSValue.FromInt32(ta.Length);
            }

            // TypedArray.prototype.set(array [, offset])
            JSValue TypedArraySet(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.set called on non-{name}");
                if (args.Length == 0)
                    return ThrowTypeError("set requires at least 1 argument");
                var offset = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                
                if (args[0].IsObject && args[0].AsObject() is JSTypedArray srcTa)
                {
                    for (int i = 0; i < srcTa.Length; i++)
                    {
                        ta.SetElement(offset + i, srcTa.GetElement(i));
                    }
                }
                else if (args[0].IsObject)
                {
                    var srcObj = args[0].AsObject();
                    var lengthVal = srcObj.Get("length");
                    var length = lengthVal.IsNumber ? JSValueConversion.ToInt32(lengthVal) : 0;
                    for (int i = 0; i < length; i++)
                    {
                        ta.SetElement(offset + i, srcObj.Get((uint)i));
                    }
                }
                return JSValue.Undefined;
            }

            // TypedArray.prototype.subarray([begin [, end]])
            JSValue TypedArraySubarray(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.subarray called on non-{name}");
                var begin = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                int? end = args.Length > 1 && !args[1].IsUndefined 
                    ? JSValueConversion.ToInt32(args[1]) 
                    : (int?)null;
                var result = ta.Subarray(begin, end);
                result.SetPrototype(typedArrayProto);
                return JSValue.FromObject(result);
            }

            // TypedArray.prototype.slice([begin [, end]])
            JSValue TypedArraySlice(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.slice called on non-{name}");
                var begin = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                int? end = args.Length > 1 && !args[1].IsUndefined 
                    ? JSValueConversion.ToInt32(args[1]) 
                    : (int?)null;
                var result = ta.Slice(begin, end);
                result.SetPrototype(typedArrayProto);
                return JSValue.FromObject(result);
            }

            // TypedArray.prototype.fill(value [, start [, end]])
            JSValue TypedArrayFill(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.fill called on non-{name}");
                var value = args.Length > 0 ? args[0] : JSValue.Undefined;
                var start = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                int? end = args.Length > 2 && !args[2].IsUndefined 
                    ? JSValueConversion.ToInt32(args[2]) 
                    : (int?)null;
                ta.Fill(value, start, end);
                return thisVal;
            }

            // TypedArray.prototype.copyWithin(target, start [, end])
            JSValue TypedArrayCopyWithin(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.copyWithin called on non-{name}");
                var target = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                var start = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                int? end = args.Length > 2 && !args[2].IsUndefined 
                    ? JSValueConversion.ToInt32(args[2]) 
                    : (int?)null;
                ta.CopyWithin(target, start, end);
                return thisVal;
            }

            // TypedArray.prototype.reverse()
            JSValue TypedArrayReverse(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.reverse called on non-{name}");
                ta.Reverse();
                return thisVal;
            }

            // TypedArray.prototype.sort([compareFunction])
            JSValue TypedArraySort(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.sort called on non-{name}");
                // TODO: Support compareFunction
                ta.Sort();
                return thisVal;
            }

            // TypedArray.prototype.indexOf(searchElement [, fromIndex])
            JSValue TypedArrayIndexOf(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.indexOf called on non-{name}");
                var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
                var fromIndex = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                return JSValue.FromInt32(ta.IndexOf(searchElement, fromIndex));
            }

            // TypedArray.prototype.lastIndexOf(searchElement [, fromIndex])
            JSValue TypedArrayLastIndexOf(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.lastIndexOf called on non-{name}");
                var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
                int? fromIndex = args.Length > 1 && !args[1].IsUndefined 
                    ? JSValueConversion.ToInt32(args[1]) 
                    : (int?)null;
                return JSValue.FromInt32(ta.LastIndexOf(searchElement, fromIndex));
            }

            // TypedArray.prototype.includes(searchElement [, fromIndex])
            JSValue TypedArrayIncludes(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.includes called on non-{name}");
                var searchElement = args.Length > 0 ? args[0] : JSValue.Undefined;
                var fromIndex = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
                return JSValue.FromBoolean(ta.Includes(searchElement, fromIndex));
            }

            // TypedArray.prototype.join([separator])
            JSValue TypedArrayJoin(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.join called on non-{name}");
                var separator = args.Length > 0 && !args[0].IsUndefined 
                    ? JSValueConversion.ToString(args[0]) 
                    : ",";
                return JSValue.FromString(ta.Join(separator));
            }

            // TypedArray.prototype.at(index)
            JSValue TypedArrayAt(JSValue thisVal, JSValue[] args)
            {
                if (!thisVal.IsObject || thisVal.AsObject() is not JSTypedArray ta)
                    return ThrowTypeError($"{name}.prototype.at called on non-{name}");
                var index = args.Length > 0 ? JSValueConversion.ToInt32(args[0]) : 0;
                if (index < 0) index = ta.Length + index;
                if (index < 0 || index >= ta.Length) return JSValue.Undefined;
                return ta.GetElement(index);
            }

            ctor.Set("of", JSValue.FromObject(new JSFunction(TypedArrayOf, "of", 0, functionProto)));
            ctor.Set("from", JSValue.FromObject(new JSFunction(TypedArrayFrom, "from", 1, functionProto)));
            typedArrayProto.Set("buffer", JSValue.FromObject(new JSFunction(TypedArrayBuffer, "buffer", 0, functionProto)));
            typedArrayProto.Set("byteLength", JSValue.FromObject(new JSFunction(TypedArrayByteLength, "byteLength", 0, functionProto)));
            typedArrayProto.Set("byteOffset", JSValue.FromObject(new JSFunction(TypedArrayByteOffset, "byteOffset", 0, functionProto)));
            typedArrayProto.Set("length", JSValue.FromObject(new JSFunction(TypedArrayLength, "length", 0, functionProto)));
            typedArrayProto.Set("set", JSValue.FromObject(new JSFunction(TypedArraySet, "set", 1, functionProto)));
            typedArrayProto.Set("subarray", JSValue.FromObject(new JSFunction(TypedArraySubarray, "subarray", 0, functionProto)));
            typedArrayProto.Set("slice", JSValue.FromObject(new JSFunction(TypedArraySlice, "slice", 0, functionProto)));
            typedArrayProto.Set("fill", JSValue.FromObject(new JSFunction(TypedArrayFill, "fill", 1, functionProto)));
            typedArrayProto.Set("copyWithin", JSValue.FromObject(new JSFunction(TypedArrayCopyWithin, "copyWithin", 2, functionProto)));
            typedArrayProto.Set("reverse", JSValue.FromObject(new JSFunction(TypedArrayReverse, "reverse", 0, functionProto)));
            typedArrayProto.Set("sort", JSValue.FromObject(new JSFunction(TypedArraySort, "sort", 1, functionProto)));
            typedArrayProto.Set("indexOf", JSValue.FromObject(new JSFunction(TypedArrayIndexOf, "indexOf", 1, functionProto)));
            typedArrayProto.Set("lastIndexOf", JSValue.FromObject(new JSFunction(TypedArrayLastIndexOf, "lastIndexOf", 1, functionProto)));
            typedArrayProto.Set("includes", JSValue.FromObject(new JSFunction(TypedArrayIncludes, "includes", 1, functionProto)));
            typedArrayProto.Set("join", JSValue.FromObject(new JSFunction(TypedArrayJoin, "join", 1, functionProto)));
            typedArrayProto.Set("at", JSValue.FromObject(new JSFunction(TypedArrayAt, "at", 1, functionProto)));

            _globalObject.Set(name, JSValue.FromObject(ctor));
        }
    }

    private void InitializeDate()
    {
        var objectProto = GetClassPrototype(JSClassId.Object)!;
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;

        // Create Date.prototype
        var dateProto = new JSObject(objectProto, JSClassId.Date);
        _classPrototypes[(int)JSClassId.Date] = dateProto;

        // Date constructor
        JSValue DateCtor(JSValue thisVal, JSValue[] args)
        {
            // new Date() - current time
            if (args.Length == 0)
            {
                var date = new JSDate();
                date.SetPrototype(dateProto);
                return JSValue.FromObject(date);
            }

            // new Date(value) - parse or time value
            if (args.Length == 1)
            {
                var arg = args[0];
                
                // Date string
                if (arg.IsString)
                {
                    var timeValue = JSDate.Parse(JSValueConversion.ToString(arg));
                    var date = new JSDate(timeValue);
                    date.SetPrototype(dateProto);
                    return JSValue.FromObject(date);
                }
                
                // Time value in milliseconds
                if (arg.IsNumber)
                {
                    var date = new JSDate(arg.IsInt ? arg.ToInt32() : arg.ToDouble());
                    date.SetPrototype(dateProto);
                    return JSValue.FromObject(date);
                }

                // Another Date object
                if (arg.IsObject && arg.AsObject() is JSDate srcDate)
                {
                    var date = new JSDate(srcDate.TimeValue);
                    date.SetPrototype(dateProto);
                    return JSValue.FromObject(date);
                }

                return JSValue.FromObject(new JSDate(double.NaN));
            }

            // new Date(year, monthIndex [, day [, hours [, minutes [, seconds [, milliseconds]]]]])
            var year = JSValueConversion.ToInt32(args[0]);
            var month = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : 0;
            var day = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : 1;
            var hours = args.Length > 3 ? JSValueConversion.ToInt32(args[3]) : 0;
            var minutes = args.Length > 4 ? JSValueConversion.ToInt32(args[4]) : 0;
            var seconds = args.Length > 5 ? JSValueConversion.ToInt32(args[5]) : 0;
            var milliseconds = args.Length > 6 ? JSValueConversion.ToInt32(args[6]) : 0;

            var dateObj = new JSDate(year, month, day, hours, minutes, seconds, milliseconds);
            dateObj.SetPrototype(dateProto);
            return JSValue.FromObject(dateObj);
        }

        var dateCtor = new JSFunction(DateCtor, "Date", 7, functionProto);
        dateCtor.SetPrototype(functionProto);

        // Date.now()
        JSValue DateNow(JSValue thisVal, JSValue[] args)
        {
            return JSValue.FromDouble(JSDate.Now());
        }

        // Date.parse(dateString)
        JSValue DateParse(JSValue thisVal, JSValue[] args)
        {
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(JSDate.Parse(JSValueConversion.ToString(args[0])));
        }

        // Date.UTC(year, month [, day [, hours [, minutes [, seconds [, milliseconds]]]]])
        JSValue DateUTC(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 2) return JSValue.FromDouble(double.NaN);
            var year = JSValueConversion.ToInt32(args[0]);
            var month = JSValueConversion.ToInt32(args[1]);
            var day = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : 1;
            var hours = args.Length > 3 ? JSValueConversion.ToInt32(args[3]) : 0;
            var minutes = args.Length > 4 ? JSValueConversion.ToInt32(args[4]) : 0;
            var seconds = args.Length > 5 ? JSValueConversion.ToInt32(args[5]) : 0;
            var milliseconds = args.Length > 6 ? JSValueConversion.ToInt32(args[6]) : 0;
            return JSValue.FromDouble(JSDate.UTC(year, month, day, hours, minutes, seconds, milliseconds));
        }

        dateCtor.Set("now", JSValue.FromObject(new JSFunction(DateNow, "now", 0, functionProto)));
        dateCtor.Set("parse", JSValue.FromObject(new JSFunction(DateParse, "parse", 1, functionProto)));
        dateCtor.Set("UTC", JSValue.FromObject(new JSFunction(DateUTC, "UTC", 7, functionProto)));

        // Date.prototype methods

        // Getters (local time)
        JSValue GetFullYear(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getFullYear called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetFullYear());
        }

        JSValue GetMonth(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getMonth called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetMonth());
        }

        JSValue GetDate(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getDate called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetDate());
        }

        JSValue GetDay(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getDay called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetDay());
        }

        JSValue GetHours(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getHours called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetHours());
        }

        JSValue GetMinutes(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getMinutes called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetMinutes());
        }

        JSValue GetSeconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getSeconds called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetSeconds());
        }

        JSValue GetMilliseconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getMilliseconds called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetMilliseconds());
        }

        JSValue GetTime(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getTime called on non-Date");
            return JSValue.FromDouble(date.GetTime());
        }

        JSValue GetTimezoneOffset(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getTimezoneOffset called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetTimezoneOffset());
        }

        // Getters (UTC time)
        JSValue GetUTCFullYear(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCFullYear called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCFullYear());
        }

        JSValue GetUTCMonth(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCMonth called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCMonth());
        }

        JSValue GetUTCDate(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCDate called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCDate());
        }

        JSValue GetUTCDay(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCDay called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCDay());
        }

        JSValue GetUTCHours(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCHours called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCHours());
        }

        JSValue GetUTCMinutes(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCMinutes called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCMinutes());
        }

        JSValue GetUTCSeconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCSeconds called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCSeconds());
        }

        JSValue GetUTCMilliseconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.getUTCMilliseconds called on non-Date");
            return date.IsInvalid ? JSValue.FromDouble(double.NaN) : JSValue.FromInt32(date.GetUTCMilliseconds());
        }

        // Setters (local time)
        JSValue SetFullYear(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setFullYear called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var year = JSValueConversion.ToInt32(args[0]);
            int? month = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? day = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            return JSValue.FromDouble(date.SetFullYear(year, month, day));
        }

        JSValue SetMonth(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setMonth called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var month = JSValueConversion.ToInt32(args[0]);
            int? day = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            return JSValue.FromDouble(date.SetMonth(month, day));
        }

        JSValue SetDate(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setDate called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(date.SetDate(JSValueConversion.ToInt32(args[0])));
        }

        JSValue SetHours(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setHours called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var hours = JSValueConversion.ToInt32(args[0]);
            int? minutes = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? seconds = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            int? milliseconds = args.Length > 3 ? JSValueConversion.ToInt32(args[3]) : (int?)null;
            return JSValue.FromDouble(date.SetHours(hours, minutes, seconds, milliseconds));
        }

        JSValue SetMinutes(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setMinutes called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var minutes = JSValueConversion.ToInt32(args[0]);
            int? seconds = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? milliseconds = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            return JSValue.FromDouble(date.SetMinutes(minutes, seconds, milliseconds));
        }

        JSValue SetSeconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setSeconds called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var seconds = JSValueConversion.ToInt32(args[0]);
            int? milliseconds = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            return JSValue.FromDouble(date.SetSeconds(seconds, milliseconds));
        }

        JSValue SetMilliseconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setMilliseconds called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(date.SetMilliseconds(JSValueConversion.ToInt32(args[0])));
        }

        JSValue SetTime(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setTime called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(date.SetTime(JSValueConversion.ToNumber(args[0])));
        }

        // Setters (UTC time)
        JSValue SetUTCFullYear(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCFullYear called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var year = JSValueConversion.ToInt32(args[0]);
            int? month = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? day = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            return JSValue.FromDouble(date.SetUTCFullYear(year, month, day));
        }

        JSValue SetUTCMonth(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCMonth called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var month = JSValueConversion.ToInt32(args[0]);
            int? day = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            return JSValue.FromDouble(date.SetUTCMonth(month, day));
        }

        JSValue SetUTCDate(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCDate called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(date.SetUTCDate(JSValueConversion.ToInt32(args[0])));
        }

        JSValue SetUTCHours(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCHours called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var hours = JSValueConversion.ToInt32(args[0]);
            int? minutes = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? seconds = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            int? milliseconds = args.Length > 3 ? JSValueConversion.ToInt32(args[3]) : (int?)null;
            return JSValue.FromDouble(date.SetUTCHours(hours, minutes, seconds, milliseconds));
        }

        JSValue SetUTCMinutes(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCMinutes called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var minutes = JSValueConversion.ToInt32(args[0]);
            int? seconds = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            int? milliseconds = args.Length > 2 ? JSValueConversion.ToInt32(args[2]) : (int?)null;
            return JSValue.FromDouble(date.SetUTCMinutes(minutes, seconds, milliseconds));
        }

        JSValue SetUTCSeconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCSeconds called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            var seconds = JSValueConversion.ToInt32(args[0]);
            int? milliseconds = args.Length > 1 ? JSValueConversion.ToInt32(args[1]) : (int?)null;
            return JSValue.FromDouble(date.SetUTCSeconds(seconds, milliseconds));
        }

        JSValue SetUTCMilliseconds(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.setUTCMilliseconds called on non-Date");
            if (args.Length == 0) return JSValue.FromDouble(double.NaN);
            return JSValue.FromDouble(date.SetUTCMilliseconds(JSValueConversion.ToInt32(args[0])));
        }

        // Conversion methods
        JSValue ToDateString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toDateString called on non-Date");
            return JSValue.FromString(date.ToDateString());
        }

        JSValue ToTimeString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toTimeString called on non-Date");
            return JSValue.FromString(date.ToTimeString());
        }

        JSValue ToISOString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toISOString called on non-Date");
            try
            {
                return JSValue.FromString(date.ToISOString());
            }
            catch (JSRangeError)
            {
                return ThrowRangeError("Invalid time value");
            }
        }

        JSValue ToUTCString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toUTCString called on non-Date");
            return JSValue.FromString(date.ToUTCString());
        }

        JSValue ToJSON(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toJSON called on non-Date");
            if (date.IsInvalid)
                return JSValue.Null;
            try
            {
                return JSValue.FromString(date.ToISOString());
            }
            catch
            {
                return JSValue.Null;
            }
        }

        JSValue DateToString(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.toString called on non-Date");
            return JSValue.FromString(date.ToString());
        }

        JSValue ValueOf(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSDate date)
                return ThrowTypeError("Date.prototype.valueOf called on non-Date");
            return JSValue.FromDouble(date.ValueOf());
        }

        // Register prototype methods - Getters (local)
        dateProto.Set("getFullYear", JSValue.FromObject(new JSFunction(GetFullYear, "getFullYear", 0, functionProto)));
        dateProto.Set("getMonth", JSValue.FromObject(new JSFunction(GetMonth, "getMonth", 0, functionProto)));
        dateProto.Set("getDate", JSValue.FromObject(new JSFunction(GetDate, "getDate", 0, functionProto)));
        dateProto.Set("getDay", JSValue.FromObject(new JSFunction(GetDay, "getDay", 0, functionProto)));
        dateProto.Set("getHours", JSValue.FromObject(new JSFunction(GetHours, "getHours", 0, functionProto)));
        dateProto.Set("getMinutes", JSValue.FromObject(new JSFunction(GetMinutes, "getMinutes", 0, functionProto)));
        dateProto.Set("getSeconds", JSValue.FromObject(new JSFunction(GetSeconds, "getSeconds", 0, functionProto)));
        dateProto.Set("getMilliseconds", JSValue.FromObject(new JSFunction(GetMilliseconds, "getMilliseconds", 0, functionProto)));
        dateProto.Set("getTime", JSValue.FromObject(new JSFunction(GetTime, "getTime", 0, functionProto)));
        dateProto.Set("getTimezoneOffset", JSValue.FromObject(new JSFunction(GetTimezoneOffset, "getTimezoneOffset", 0, functionProto)));

        // Register prototype methods - Getters (UTC)
        dateProto.Set("getUTCFullYear", JSValue.FromObject(new JSFunction(GetUTCFullYear, "getUTCFullYear", 0, functionProto)));
        dateProto.Set("getUTCMonth", JSValue.FromObject(new JSFunction(GetUTCMonth, "getUTCMonth", 0, functionProto)));
        dateProto.Set("getUTCDate", JSValue.FromObject(new JSFunction(GetUTCDate, "getUTCDate", 0, functionProto)));
        dateProto.Set("getUTCDay", JSValue.FromObject(new JSFunction(GetUTCDay, "getUTCDay", 0, functionProto)));
        dateProto.Set("getUTCHours", JSValue.FromObject(new JSFunction(GetUTCHours, "getUTCHours", 0, functionProto)));
        dateProto.Set("getUTCMinutes", JSValue.FromObject(new JSFunction(GetUTCMinutes, "getUTCMinutes", 0, functionProto)));
        dateProto.Set("getUTCSeconds", JSValue.FromObject(new JSFunction(GetUTCSeconds, "getUTCSeconds", 0, functionProto)));
        dateProto.Set("getUTCMilliseconds", JSValue.FromObject(new JSFunction(GetUTCMilliseconds, "getUTCMilliseconds", 0, functionProto)));

        // Register prototype methods - Setters (local)
        dateProto.Set("setFullYear", JSValue.FromObject(new JSFunction(SetFullYear, "setFullYear", 3, functionProto)));
        dateProto.Set("setMonth", JSValue.FromObject(new JSFunction(SetMonth, "setMonth", 2, functionProto)));
        dateProto.Set("setDate", JSValue.FromObject(new JSFunction(SetDate, "setDate", 1, functionProto)));
        dateProto.Set("setHours", JSValue.FromObject(new JSFunction(SetHours, "setHours", 4, functionProto)));
        dateProto.Set("setMinutes", JSValue.FromObject(new JSFunction(SetMinutes, "setMinutes", 3, functionProto)));
        dateProto.Set("setSeconds", JSValue.FromObject(new JSFunction(SetSeconds, "setSeconds", 2, functionProto)));
        dateProto.Set("setMilliseconds", JSValue.FromObject(new JSFunction(SetMilliseconds, "setMilliseconds", 1, functionProto)));
        dateProto.Set("setTime", JSValue.FromObject(new JSFunction(SetTime, "setTime", 1, functionProto)));

        // Register prototype methods - Setters (UTC)
        dateProto.Set("setUTCFullYear", JSValue.FromObject(new JSFunction(SetUTCFullYear, "setUTCFullYear", 3, functionProto)));
        dateProto.Set("setUTCMonth", JSValue.FromObject(new JSFunction(SetUTCMonth, "setUTCMonth", 2, functionProto)));
        dateProto.Set("setUTCDate", JSValue.FromObject(new JSFunction(SetUTCDate, "setUTCDate", 1, functionProto)));
        dateProto.Set("setUTCHours", JSValue.FromObject(new JSFunction(SetUTCHours, "setUTCHours", 4, functionProto)));
        dateProto.Set("setUTCMinutes", JSValue.FromObject(new JSFunction(SetUTCMinutes, "setUTCMinutes", 3, functionProto)));
        dateProto.Set("setUTCSeconds", JSValue.FromObject(new JSFunction(SetUTCSeconds, "setUTCSeconds", 2, functionProto)));
        dateProto.Set("setUTCMilliseconds", JSValue.FromObject(new JSFunction(SetUTCMilliseconds, "setUTCMilliseconds", 1, functionProto)));

        // Register prototype methods - Conversion
        dateProto.Set("toDateString", JSValue.FromObject(new JSFunction(ToDateString, "toDateString", 0, functionProto)));
        dateProto.Set("toTimeString", JSValue.FromObject(new JSFunction(ToTimeString, "toTimeString", 0, functionProto)));
        dateProto.Set("toISOString", JSValue.FromObject(new JSFunction(ToISOString, "toISOString", 0, functionProto)));
        dateProto.Set("toUTCString", JSValue.FromObject(new JSFunction(ToUTCString, "toUTCString", 0, functionProto)));
        dateProto.Set("toJSON", JSValue.FromObject(new JSFunction(ToJSON, "toJSON", 1, functionProto)));
        dateProto.Set("toString", JSValue.FromObject(new JSFunction(DateToString, "toString", 0, functionProto)));
        dateProto.Set("valueOf", JSValue.FromObject(new JSFunction(ValueOf, "valueOf", 0, functionProto)));

        // Also add toGMTString as alias for toUTCString
        dateProto.Set("toGMTString", JSValue.FromObject(new JSFunction(ToUTCString, "toGMTString", 0, functionProto)));

        _globalObject.Set("Date", JSValue.FromObject(dateCtor));
    }

    private void InitializeSymbol()
    {
        var objectProto = GetClassPrototype(JSClassId.Object)!;
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;

        // Symbol is not a constructor (cannot use new)
        // Symbol([description]) - creates a new unique symbol
        JSValue SymbolFunc(JSValue thisVal, JSValue[] args)
        {
            var description = args.Length > 0 && !args[0].IsUndefined
                ? JSValueConversion.ToString(args[0])
                : null;
            return JSValue.FromSymbol(new JSSymbol(description));
        }

        var symbolFunc = new JSFunction(SymbolFunc, "Symbol", 0, functionProto);
        symbolFunc.SetPrototype(functionProto);

        // Symbol.for(key) - returns a symbol from the global registry
        JSValue SymbolFor(JSValue thisVal, JSValue[] args)
        {
            if (args.Length == 0)
                return ThrowTypeError("Symbol.for requires an argument");
            var key = JSValueConversion.ToString(args[0]);
            return JSValue.FromSymbol(JSSymbol.For(key));
        }

        // Symbol.keyFor(sym) - returns the key for a global symbol
        JSValue SymbolKeyFor(JSValue thisVal, JSValue[] args)
        {
            if (args.Length == 0 || !args[0].IsSymbol)
                return ThrowTypeError("Symbol.keyFor requires a symbol argument");
            var sym = args[0].AsSymbol();
            var key = JSSymbol.KeyFor(sym);
            return key != null ? JSValue.FromString(key) : JSValue.Undefined;
        }

        symbolFunc.Set("for", JSValue.FromObject(new JSFunction(SymbolFor, "for", 1, functionProto)));
        symbolFunc.Set("keyFor", JSValue.FromObject(new JSFunction(SymbolKeyFor, "keyFor", 1, functionProto)));

        // Well-known symbols
        symbolFunc.Set("iterator", JSValue.FromSymbol(JSSymbol.Iterator));
        symbolFunc.Set("asyncIterator", JSValue.FromSymbol(JSSymbol.AsyncIterator));
        symbolFunc.Set("toStringTag", JSValue.FromSymbol(JSSymbol.ToStringTag));
        symbolFunc.Set("toPrimitive", JSValue.FromSymbol(JSSymbol.ToPrimitive));
        symbolFunc.Set("hasInstance", JSValue.FromSymbol(JSSymbol.HasInstance));
        symbolFunc.Set("isConcatSpreadable", JSValue.FromSymbol(JSSymbol.IsConcatSpreadable));
        symbolFunc.Set("species", JSValue.FromSymbol(JSSymbol.Species));
        symbolFunc.Set("match", JSValue.FromSymbol(JSSymbol.Match));
        symbolFunc.Set("matchAll", JSValue.FromSymbol(JSSymbol.MatchAll));
        symbolFunc.Set("replace", JSValue.FromSymbol(JSSymbol.Replace));
        symbolFunc.Set("search", JSValue.FromSymbol(JSSymbol.Search));
        symbolFunc.Set("split", JSValue.FromSymbol(JSSymbol.Split));
        symbolFunc.Set("unscopables", JSValue.FromSymbol(JSSymbol.Unscopables));

        _globalObject.Set("Symbol", JSValue.FromObject(symbolFunc));
    }

    private void InitializeReflect()
    {
        var objectProto = GetClassPrototype(JSClassId.Object)!;
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;

        // Reflect is not a constructor, just a namespace object
        var reflectObj = new JSObject(objectProto, JSClassId.Object);

        // Reflect.apply(target, thisArg, args)
        JSValue ReflectApply(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.apply requires a function");

            var target = args[0].AsObject();
            if (target is not JSFunction fn)
                return ThrowTypeError("Reflect.apply requires a function");

            var thisArg = args.Length > 1 ? args[1] : JSValue.Undefined;
            var argsArray = args.Length > 2 && args[2].IsObject && args[2].AsObject() is JSArray arr
                ? arr.ToArray()
                : Array.Empty<JSValue>();

            return fn.CallNative(thisArg, argsArray);
        }

        // Reflect.construct(target, args [, newTarget])
        JSValue ReflectConstruct(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.construct requires a constructor");

            var target = args[0].AsObject();
            if (target is not JSFunction fn)
                return ThrowTypeError("Reflect.construct requires a constructor");

            var argsArray = args.Length > 1 && args[1].IsObject && args[1].AsObject() is JSArray arr
                ? arr.ToArray()
                : Array.Empty<JSValue>();

            // Simple construction (ignores newTarget for now)
            return fn.CallNative(JSValue.Undefined, argsArray);
        }

        // Reflect.defineProperty(target, key, descriptor)
        JSValue ReflectDefineProperty(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.defineProperty requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";
            var desc = args.Length > 2 && args[2].IsObject ? args[2].AsObject() : null;

            if (desc == null)
                return JSValue.False;

            try
            {
                var valueVal = desc.Get("value");
                var writableVal = desc.Get("writable");
                var enumerableVal = desc.Get("enumerable");
                var configurableVal = desc.Get("configurable");
                var getVal = desc.Get("get");
                var setVal = desc.Get("set");

                var flags = PropertyFlags.None;
                if (!writableVal.IsUndefined && JSValueConversion.ToBoolean(writableVal))
                    flags |= PropertyFlags.Writable;
                if (!enumerableVal.IsUndefined && JSValueConversion.ToBoolean(enumerableVal))
                    flags |= PropertyFlags.Enumerable;
                if (!configurableVal.IsUndefined && JSValueConversion.ToBoolean(configurableVal))
                    flags |= PropertyFlags.Configurable;

                if (!getVal.IsUndefined || !setVal.IsUndefined)
                {
                    // Accessor descriptor
                    var getter = getVal.IsUndefined ? JSValue.Undefined : getVal;
                    var setter = setVal.IsUndefined ? JSValue.Undefined : setVal;
                    target.DefineProperty(key, new PropertyDescriptor(getter, setter, flags));
                }
                else
                {
                    // Data descriptor
                    target.DefineProperty(key, new PropertyDescriptor(valueVal, flags));
                }
                return JSValue.True;
            }
            catch
            {
                return JSValue.False;
            }
        }

        // Reflect.deleteProperty(target, key)
        JSValue ReflectDeleteProperty(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.deleteProperty requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";

            try
            {
                return JSValue.FromBoolean(target.Delete(key));
            }
            catch
            {
                return JSValue.False;
            }
        }

        // Reflect.get(target, key [, receiver])
        JSValue ReflectGet(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.get requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";

            return target.Get(key);
        }

        // Reflect.getOwnPropertyDescriptor(target, key)
        JSValue ReflectGetOwnPropertyDescriptor(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.getOwnPropertyDescriptor requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";

            if (!target.TryGetOwnPropertyDescriptor(key, out var desc) || desc == null)
                return JSValue.Undefined;

            var descObj = new JSObject(objectProto, JSClassId.Object);
            descObj.Set("value", desc.Value);
            descObj.Set("writable", JSValue.FromBoolean(desc.IsWritable));
            descObj.Set("enumerable", JSValue.FromBoolean(desc.IsEnumerable));
            descObj.Set("configurable", JSValue.FromBoolean(desc.IsConfigurable));
            return JSValue.FromObject(descObj);
        }

        // Reflect.getPrototypeOf(target)
        JSValue ReflectGetPrototypeOf(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.getPrototypeOf requires an object");

            var target = args[0].AsObject();
            var proto = target.Prototype;
            return proto != null ? JSValue.FromObject(proto) : JSValue.Null;
        }

        // Reflect.has(target, key)
        JSValue ReflectHas(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.has requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";

            return JSValue.FromBoolean(target.HasProperty(key));
        }

        // Reflect.isExtensible(target)
        JSValue ReflectIsExtensible(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.isExtensible requires an object");

            var target = args[0].AsObject();
            return JSValue.FromBoolean(target.IsExtensible);
        }

        // Reflect.ownKeys(target)
        JSValue ReflectOwnKeys(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.ownKeys requires an object");

            var target = args[0].AsObject();
            var keys = target.GetOwnPropertyNames();
            var array = new JSArray();
            foreach (var key in keys)
            {
                array.Push(JSValue.FromString(key));
            }
            return JSValue.FromObject(array);
        }

        // Reflect.preventExtensions(target)
        JSValue ReflectPreventExtensions(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.preventExtensions requires an object");

            var target = args[0].AsObject();
            try
            {
                target.PreventExtensions();
                return JSValue.True;
            }
            catch
            {
                return JSValue.False;
            }
        }

        // Reflect.set(target, key, value [, receiver])
        JSValue ReflectSet(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.set requires an object");

            var target = args[0].AsObject();
            var key = args.Length > 1 ? JSValueConversion.ToString(args[1]) : "";
            var value = args.Length > 2 ? args[2] : JSValue.Undefined;

            try
            {
                target.Set(key, value);
                return JSValue.True;
            }
            catch
            {
                return JSValue.False;
            }
        }

        // Reflect.setPrototypeOf(target, proto)
        JSValue ReflectSetPrototypeOf(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !args[0].IsObject)
                return ThrowTypeError("Reflect.setPrototypeOf requires an object");

            var target = args[0].AsObject();
            var proto = args.Length > 1 && args[1].IsObject ? args[1].AsObject() : null;

            try
            {
                target.SetPrototype(proto);
                return JSValue.True;
            }
            catch
            {
                return JSValue.False;
            }
        }

        reflectObj.Set("apply", JSValue.FromObject(new JSFunction(ReflectApply, "apply", 3, functionProto)));
        reflectObj.Set("construct", JSValue.FromObject(new JSFunction(ReflectConstruct, "construct", 2, functionProto)));
        reflectObj.Set("defineProperty", JSValue.FromObject(new JSFunction(ReflectDefineProperty, "defineProperty", 3, functionProto)));
        reflectObj.Set("deleteProperty", JSValue.FromObject(new JSFunction(ReflectDeleteProperty, "deleteProperty", 2, functionProto)));
        reflectObj.Set("get", JSValue.FromObject(new JSFunction(ReflectGet, "get", 2, functionProto)));
        reflectObj.Set("getOwnPropertyDescriptor", JSValue.FromObject(new JSFunction(ReflectGetOwnPropertyDescriptor, "getOwnPropertyDescriptor", 2, functionProto)));
        reflectObj.Set("getPrototypeOf", JSValue.FromObject(new JSFunction(ReflectGetPrototypeOf, "getPrototypeOf", 1, functionProto)));
        reflectObj.Set("has", JSValue.FromObject(new JSFunction(ReflectHas, "has", 2, functionProto)));
        reflectObj.Set("isExtensible", JSValue.FromObject(new JSFunction(ReflectIsExtensible, "isExtensible", 1, functionProto)));
        reflectObj.Set("ownKeys", JSValue.FromObject(new JSFunction(ReflectOwnKeys, "ownKeys", 1, functionProto)));
        reflectObj.Set("preventExtensions", JSValue.FromObject(new JSFunction(ReflectPreventExtensions, "preventExtensions", 1, functionProto)));
        reflectObj.Set("set", JSValue.FromObject(new JSFunction(ReflectSet, "set", 3, functionProto)));
        reflectObj.Set("setPrototypeOf", JSValue.FromObject(new JSFunction(ReflectSetPrototypeOf, "setPrototypeOf", 2, functionProto)));

        _globalObject.Set("Reflect", JSValue.FromObject(reflectObj));
    }

    private void InitializeProxy()
    {
        var functionProto = GetClassPrototype(JSClassId.CFunction)!;
        var objectProto = GetClassPrototype(JSClassId.Object)!;

        // Proxy constructor: new Proxy(target, handler)
        JSValue ProxyCtor(JSValue thisVal, JSValue[] args)
        {
            // Proxy must be called with 'new'
            if (!thisVal.IsObject)
                return ThrowTypeError("Proxy constructor requires 'new'");

            if (args.Length < 2)
                return ThrowTypeError("Proxy requires target and handler arguments");

            if (!args[0].IsObject)
                return ThrowTypeError("Proxy target must be an object");

            if (!args[1].IsObject)
                return ThrowTypeError("Proxy handler must be an object");

            var target = args[0].AsObject();
            var handler = args[1].AsObject();

            // Check if target is a revoked proxy
            if (target is JSProxy targetProxy && targetProxy.IsRevoked)
                return ThrowTypeError("Cannot create Proxy with a revoked proxy as target");

            // Check if handler is a revoked proxy
            if (handler is JSProxy handlerProxy && handlerProxy.IsRevoked)
                return ThrowTypeError("Cannot create Proxy with a revoked proxy as handler");

            var proxy = new JSProxy(target, handler, objectProto);
            return JSValue.FromObject(proxy);
        }

        // Proxy.revocable(target, handler)
        JSValue ProxyRevocable(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 2)
                return ThrowTypeError("Proxy.revocable requires target and handler arguments");

            if (!args[0].IsObject)
                return ThrowTypeError("Proxy target must be an object");

            if (!args[1].IsObject)
                return ThrowTypeError("Proxy handler must be an object");

            var target = args[0].AsObject();
            var handler = args[1].AsObject();

            var proxy = new JSProxy(target, handler, objectProto);

            // Create the result object { proxy, revoke }
            var result = new JSObject(objectProto, JSClassId.Object);
            result.Set("proxy", JSValue.FromObject(proxy));

            // Create revoke function
            JSValue RevokeFn(JSValue thisArg, JSValue[] fnArgs)
            {
                proxy.Revoke();
                return JSValue.Undefined;
            }

            result.Set("revoke", JSValue.FromObject(new JSFunction(RevokeFn, "revoke", 0, functionProto)));

            return JSValue.FromObject(result);
        }

        var proxyCtor = new JSFunction(ProxyCtor, "Proxy", 2, functionProto);
        proxyCtor.Set("revocable", JSValue.FromObject(new JSFunction(ProxyRevocable, "revocable", 2, functionProto)));

        _globalObject.Set("Proxy", JSValue.FromObject(proxyCtor));
    }

    #endregion

    #region Console Initialization

    private JSConsole? _console;

    /// <summary>
    /// Gets the console instance for this context.
    /// </summary>
    public JSConsole Console => _console ?? throw new InvalidOperationException("Console not initialized");

    /// <summary>
    /// Event raised when console output is written.
    /// </summary>
    public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput;

    private void InitializeConsole()
    {
        var objectProto = GetClassPrototype(JSClassId.Object);

        // Create console object - it sets up all its methods internally
        _console = new JSConsole(objectProto);

        // Subscribe to console output events
        _console.Output += (sender, e) => ConsoleOutput?.Invoke(this, e);

        // Set console on global object
        _globalObject.Set("console", JSValue.FromObject(_console));
    }

    #endregion

    #region Timer Initialization

    private JSEventLoop? _eventLoop;

    /// <summary>
    /// Gets the event loop for this context.
    /// </summary>
    public JSEventLoop EventLoop => _eventLoop ?? throw new InvalidOperationException("Event loop not initialized");

    private void InitializeTimers()
    {
        // Create event loop
        _eventLoop = new JSEventLoop(this);

        // Helper function to check if a value is a function
        bool IsFunction(JSValue value) => value.IsObject && value.AsObject() is JSFunction;

        // setTimeout(callback, delay, ...args)
        JSValue SetTimeoutFn(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !IsFunction(args[0]))
            {
                return JSValue.FromInt32(0);
            }

            var callback = args[0].AsObject() as JSFunction;
            if (callback == null)
            {
                return JSValue.FromInt32(0);
            }

            var delay = args.Length > 1 ? args[1].ToInt32() : 0;
            var callbackArgs = args.Length > 2 ? SliceArray(args, 2) : Array.Empty<JSValue>();

            var id = _eventLoop!.SetTimeout(callback, delay, callbackArgs);
            return JSValue.FromInt32(id);
        }

        // clearTimeout(id)
        JSValue ClearTimeoutFn(JSValue thisVal, JSValue[] args)
        {
            if (args.Length > 0)
            {
                var id = args[0].ToInt32();
                _eventLoop!.ClearTimeout(id);
            }
            return JSValue.Undefined;
        }

        // setInterval(callback, delay, ...args)
        JSValue SetIntervalFn(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !IsFunction(args[0]))
            {
                return JSValue.FromInt32(0);
            }

            var callback = args[0].AsObject() as JSFunction;
            if (callback == null)
            {
                return JSValue.FromInt32(0);
            }

            var delay = args.Length > 1 ? args[1].ToInt32() : 0;
            var callbackArgs = args.Length > 2 ? SliceArray(args, 2) : Array.Empty<JSValue>();

            var id = _eventLoop!.SetInterval(callback, delay, callbackArgs);
            return JSValue.FromInt32(id);
        }

        // clearInterval(id)
        JSValue ClearIntervalFn(JSValue thisVal, JSValue[] args)
        {
            if (args.Length > 0)
            {
                var id = args[0].ToInt32();
                _eventLoop!.ClearInterval(id);
            }
            return JSValue.Undefined;
        }

        // queueMicrotask(callback)
        JSValue QueueMicrotaskFn(JSValue thisVal, JSValue[] args)
        {
            if (args.Length < 1 || !IsFunction(args[0]))
            {
                throw new JSTypeError("queueMicrotask requires a function argument");
            }

            var callback = args[0].AsObject() as JSFunction;
            if (callback == null)
            {
                throw new JSTypeError("queueMicrotask requires a function argument");
            }

            _eventLoop!.EnqueueMicrotask(() =>
            {
                try
                {
                    callback.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
                }
                catch
                {
                    // Microtask errors are silently ignored in standard behavior
                }
            });

            return JSValue.Undefined;
        }

        // Register global timer functions
        var setTimeout = new JSFunction(SetTimeoutFn, "setTimeout", 1);
        var clearTimeout = new JSFunction(ClearTimeoutFn, "clearTimeout", 1);
        var setInterval = new JSFunction(SetIntervalFn, "setInterval", 1);
        var clearInterval = new JSFunction(ClearIntervalFn, "clearInterval", 1);
        var queueMicrotask = new JSFunction(QueueMicrotaskFn, "queueMicrotask", 1);

        _globalObject.Set("setTimeout", JSValue.FromObject(setTimeout));
        _globalObject.Set("clearTimeout", JSValue.FromObject(clearTimeout));
        _globalObject.Set("setInterval", JSValue.FromObject(setInterval));
        _globalObject.Set("clearInterval", JSValue.FromObject(clearInterval));
        _globalObject.Set("queueMicrotask", JSValue.FromObject(queueMicrotask));
    }

    private static JSValue[] SliceArray(JSValue[] array, int start)
    {
        if (start >= array.Length)
        {
            return Array.Empty<JSValue>();
        }
        var result = new JSValue[array.Length - start];
        Array.Copy(array, start, result, 0, result.Length);
        return result;
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

        // Clear event loop timers
        _eventLoop?.ClearAllTimers();
        _eventLoop?.ClearAllMicrotasks();

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
