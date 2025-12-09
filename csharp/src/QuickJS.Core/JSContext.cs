// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        // Constructor.prototype
        objectCtorFunc.Set("prototype", JSValue.FromObject(objectProto));

        // Attach global Object
        _globalObject.Set("Object", JSValue.FromObject(objectCtorFunc));

        // Attach Object static methods
        objectCtorFunc.Set("create", JSValue.FromObject(new JSFunction(ObjectCreate, "create", 1, functionProto)));

        // Attach prototype methods
        objectProto.Set("constructor", JSValue.FromObject(objectCtorFunc));
        objectProto.Set("hasOwnProperty", JSValue.FromObject(new JSFunction(HasOwnProperty, "hasOwnProperty", 1, functionProto)));
        objectProto.Set("toString", JSValue.FromObject(new JSFunction(ObjectProtoToString, "toString", 0, functionProto)));
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

        // Math functions (subset)
        AddMathFunc("abs", 1, xs => Math.Abs(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("floor", 1, xs => Math.Floor(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("ceil", 1, xs => Math.Ceiling(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("round", 1, xs => Math.Round(xs.Length > 0 ? xs[0] : double.NaN));
        AddMathFunc("max", 2, xs => xs.Length == 0 ? double.NegativeInfinity : xs.Max());
        AddMathFunc("min", 2, xs => xs.Length == 0 ? double.PositiveInfinity : xs.Min());
        AddMathFunc("pow", 2, xs => xs.Length >= 2 ? Math.Pow(xs[0], xs[1]) : double.NaN);
        AddMathFunc("sqrt", 1, xs => xs.Length > 0 ? Math.Sqrt(xs[0]) : double.NaN);
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

        stringProto.Set("toString", JSValue.FromObject(new JSFunction(StringProtoToString, "toString", 0, functionProto)));
        stringProto.Set("valueOf", JSValue.FromObject(new JSFunction(StringProtoToString, "valueOf", 0, functionProto)));

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

        arrayProto.Set("push", JSValue.FromObject(new JSFunction(ArrayPush, "push", 1, functionProto)));
        arrayProto.Set("pop", JSValue.FromObject(new JSFunction(ArrayPop, "pop", 0, functionProto)));

        _globalObject.Set("Array", JSValue.FromObject(arrayCtorFn));
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
