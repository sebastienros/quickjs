// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace QuickJS;

/// <summary>
/// Represents a JavaScript runtime environment.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="JSRuntime"/> is the top-level container for JavaScript execution.
/// It manages shared resources that are used across multiple <see cref="JSContext"/> instances:
/// </para>
/// <list type="bullet">
/// <item><description>The atom table (interned strings/identifiers)</description></item>
/// <item><description>The class registry (object types like Array, Function, etc.)</description></item>
/// <item><description>Memory allocation limits and tracking</description></item>
/// <item><description>Stack size limits for recursion protection</description></item>
/// <item><description>The job queue for Promise microtasks</description></item>
/// <item><description>Interrupt handling for long-running scripts</description></item>
/// </list>
/// <para>
/// <b>Threading Model:</b> A <see cref="JSRuntime"/> instance and all its associated
/// <see cref="JSContext"/> instances must be used from a single thread only.
/// The runtime is not thread-safe. For multi-threaded scenarios, create separate
/// runtime instances per thread.
/// </para>
/// <para>
/// In QuickJS, the relationship between runtime and context is:
/// </para>
/// <code>
/// JSRuntime (1) ──── (N) JSContext
/// </code>
/// <para>
/// A single runtime can host multiple contexts, allowing isolated JavaScript
/// environments to share memory efficiently. Each context has its own global object
/// and built-in objects (Object, Array, etc.) but shares atoms and class definitions.
/// </para>
/// <para>
/// This class mirrors QuickJS's <c>struct JSRuntime</c> which includes:
/// </para>
/// <code>
/// struct JSRuntime {
///     JSMallocState malloc_state;        // Memory tracking
///     uint32_t *atom_hash;               // Atom hash table
///     JSAtomStruct **atom_array;         // Atom array
///     JSClass *class_array;              // Class definitions
///     struct list_head context_list;     // Contexts in this runtime
///     struct list_head gc_obj_list;      // GC object list
///     size_t stack_size;                 // Stack limit
///     JSValue current_exception;         // Current exception
///     struct list_head job_list;         // Promise job queue
///     ...
/// };
/// </code>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class JSRuntime : IDisposable
{
    #region Constants

    /// <summary>
    /// Default stack size limit in bytes (256 KB).
    /// </summary>
    public const long DefaultStackSize = 256 * 1024;

    /// <summary>
    /// Default GC threshold in bytes (256 KB).
    /// </summary>
    public const long DefaultGCThreshold = 256 * 1024;

    /// <summary>
    /// Indicates no memory limit.
    /// </summary>
    public const long NoMemoryLimit = -1;

    #endregion

    #region Fields

    // Memory tracking
    private long _memoryLimit = NoMemoryLimit;
    private long _memoryUsed;
    private long _gcThreshold = DefaultGCThreshold;
    private long _allocationCount;

    // Stack limitation
    private long _maxStackSize = DefaultStackSize;

    // Atom table for string interning
    private readonly AtomTable _atomTable;

    // Class registry
    private readonly List<JSClassDef> _classes;
    private readonly Dictionary<string, JSClassId> _classNameToId;
    private readonly ReaderWriterLockSlim _classesLock = new ReaderWriterLockSlim();

    // List of contexts in this runtime
    private readonly List<JSContext> _contexts;
    private readonly ReaderWriterLockSlim _contextsLock = new ReaderWriterLockSlim();

    // Current exception for the runtime
    private JSValue _currentException = JSValue.Undefined;
    private bool _hasException;

    // Job queue for Promise microtasks
    private readonly Queue<JSJob> _jobQueue;

    // Interrupt handling
    private volatile bool _interruptRequested;
    private int _interruptCounter;
    private int _interruptInterval = 10000; // Check every N opcodes

    // Disposal tracking
    private bool _isDisposed;

    // User-defined opaque data
    private object? _userOpaque;

    // Thread safety for memory tracking
    private readonly object _memoryLock = new object();

    // Runtime info string
    private string? _runtimeInfo;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new JavaScript runtime with default settings.
    /// </summary>
    public JSRuntime()
    {
        _atomTable = new AtomTable();
        _classes = new List<JSClassDef>();
        _classNameToId = new Dictionary<string, JSClassId>();
        _contexts = new();
        _jobQueue = new Queue<JSJob>();

        // Register standard classes
        InitializeStandardClasses();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of atoms currently interned.
    /// </summary>
    public int AtomCount => _atomTable.Count;

    /// <summary>
    /// Gets the number of registered classes.
    /// </summary>
    public int ClassCount
    {
        get
        {
            _classesLock.EnterReadLock();
            try
            {
                return _classes.Count;
            }
            finally
            {
                _classesLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the number of contexts using this runtime.
    /// </summary>
    public int ContextCount
    {
        get
        {
            _contextsLock.EnterReadLock();
            try
            {
                return _contexts.Count;
            }
            finally
            {
                _contextsLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets or sets the memory limit in bytes. Use <see cref="NoMemoryLimit"/> (-1) for no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than -1.</exception>
    public long MemoryLimit
    {
        get => _memoryLimit;
        set
        {
            if (value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), "Memory limit must be -1 (no limit) or a positive value.");
            _memoryLimit = value;
        }
    }

    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    public long MemoryUsed => Interlocked.Read(ref _memoryUsed);

    /// <summary>
    /// Gets the total number of allocations made.
    /// </summary>
    public long AllocationCount => Interlocked.Read(ref _allocationCount);

    /// <summary>
    /// Gets or sets the GC threshold in bytes. When memory usage exceeds this,
    /// garbage collection may be triggered. Use -1 to disable automatic GC.
    /// </summary>
    public long GCThreshold
    {
        get => _gcThreshold;
        set => _gcThreshold = value;
    }

    /// <summary>
    /// Gets or sets the maximum stack size in bytes for recursion protection.
    /// Use 0 to disable stack checking.
    /// </summary>
    public long MaxStackSize
    {
        get => _maxStackSize;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Stack size must be non-negative.");
            _maxStackSize = value;
        }
    }

    /// <summary>
    /// Gets or sets an interrupt interval. The interrupt handler is checked
    /// every N operations.
    /// </summary>
    public int InterruptInterval
    {
        get => _interruptInterval;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Interrupt interval must be at least 1.");
            _interruptInterval = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether an interrupt has been requested.
    /// </summary>
    public bool IsInterruptRequested => _interruptRequested;

    /// <summary>
    /// Gets a value indicating whether the runtime has an unhandled exception.
    /// </summary>
    public bool HasException => _hasException;

    /// <summary>
    /// Gets or sets user-defined data associated with this runtime.
    /// </summary>
    public object? UserOpaque
    {
        get => _userOpaque;
        set => _userOpaque = value;
    }

    /// <summary>
    /// Gets or sets an informational string describing this runtime.
    /// </summary>
    public string? RuntimeInfo
    {
        get => _runtimeInfo;
        set => _runtimeInfo = value;
    }

    /// <summary>
    /// Gets the atom table for this runtime.
    /// </summary>
    internal AtomTable AtomTable => _atomTable;

    /// <summary>
    /// Gets a value indicating whether this runtime has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Gets the number of pending jobs in the job queue.
    /// </summary>
    public int PendingJobCount => _jobQueue.Count;

    #endregion

    #region Context Management

    /// <summary>
    /// Creates a new context within this runtime.
    /// </summary>
    /// <returns>A new <see cref="JSContext"/> instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the runtime has been disposed.</exception>
    public JSContext CreateContext()
    {
        ThrowIfDisposed();

        var context = new JSContext(this);
        _contextsLock.EnterWriteLock();
        try
        {
            _contexts.Add(context);
        }
        finally
        {
            _contextsLock.ExitWriteLock();
        }
        return context;
    }

    /// <summary>
    /// Removes a context from this runtime.
    /// </summary>
    /// <param name="context">The context to remove.</param>
    /// <returns><c>true</c> if the context was removed; otherwise, <c>false</c>.</returns>
    internal bool RemoveContext(JSContext context)
    {
        _contextsLock.EnterWriteLock();
        try
        {
            return _contexts.Remove(context);
        }
        finally
        {
            _contextsLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all contexts in this runtime.
    /// </summary>
    /// <returns>An enumerable of contexts.</returns>
    public IEnumerable<JSContext> GetContexts()
    {
        _contextsLock.EnterReadLock();
        try
        {
            return _contexts.ToArray();
        }
        finally
        {
            _contextsLock.ExitReadLock();
        }
    }

    #endregion

    #region Class Registration

    /// <summary>
    /// Registers a new class and returns its class ID.
    /// </summary>
    /// <param name="className">The name of the class.</param>
    /// <param name="finalizer">Optional finalizer for instances of this class.</param>
    /// <param name="call">Optional call handler for callable objects.</param>
    /// <returns>The newly registered class ID.</returns>
    /// <exception cref="ArgumentNullException">Thrown if className is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a class with this name already exists.</exception>
    public JSClassId RegisterClass(string className, Action<JSObject>? finalizer = null, Func<JSValue, JSValue[], JSValue>? call = null)
    {
        if (className == null)
            throw new ArgumentNullException(nameof(className));

        ThrowIfDisposed();

        _classesLock.EnterWriteLock();
        try
        {
            if (_classNameToId.ContainsKey(className))
                throw new InvalidOperationException($"Class '{className}' is already registered.");

            var classId = (JSClassId)_classes.Count;
            var classDef = new JSClassDef(classId, className, finalizer, call);
            _classes.Add(classDef);
            _classNameToId[className] = classId;

            return classId;
        }
        finally
        {
            _classesLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the class definition for a class ID.
    /// </summary>
    /// <param name="classId">The class ID.</param>
    /// <returns>The class definition, or null if not found.</returns>
    public JSClassDef? GetClass(JSClassId classId)
    {
        _classesLock.EnterReadLock();
        try
        {
            int index = (int)classId;
            if (index >= 0 && index < _classes.Count)
                return _classes[index];
            return null;
        }
        finally
        {
            _classesLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the class ID for a class name.
    /// </summary>
    /// <param name="className">The class name.</param>
    /// <returns>The class ID if found; otherwise, <see cref="JSClassId.None"/>.</returns>
    public JSClassId GetClassId(string className)
    {
        _classesLock.EnterReadLock();
        try
        {
            if (_classNameToId.TryGetValue(className, out var classId))
                return classId;
            return JSClassId.None;
        }
        finally
        {
            _classesLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Initializes the standard built-in classes.
    /// </summary>
    private void InitializeStandardClasses()
    {
        // Register standard JavaScript classes in order
        // These match the JSClassId enum values
        RegisterClass("Object");           // JSClassId.Object = 0
        RegisterClass("Array");            // JSClassId.Array = 1
        RegisterClass("Error");            // JSClassId.Error = 2
        RegisterClass("Number");           // JSClassId.Number = 3
        RegisterClass("String");           // JSClassId.String = 4
        RegisterClass("Boolean");          // JSClassId.Boolean = 5
        RegisterClass("Symbol");           // JSClassId.Symbol = 6
        RegisterClass("Arguments");        // JSClassId.Arguments = 7
        RegisterClass("MappedArguments");  // JSClassId.MappedArguments = 8
        RegisterClass("Date");             // JSClassId.Date = 9
        RegisterClass("RegExp");           // JSClassId.RegExp = 10
        RegisterClass("Promise");          // JSClassId.Promise = 11
        RegisterClass("Function");         // JSClassId.CFunction = 12
        RegisterClass("FunctionData");     // JSClassId.CFunctionData = 13
        RegisterClass("BoundFunction");    // JSClassId.BoundFunction = 14
        RegisterClass("GeneratorFunction"); // JSClassId.GeneratorFunction = 15
        RegisterClass("ForInIterator");    // JSClassId.ForInIterator = 16
        RegisterClass("RegExpStringIterator"); // JSClassId.RegExpStringIterator = 17
        RegisterClass("Generator");        // JSClassId.Generator = 18
        RegisterClass("Global");           // JSClassId.Global = 19
        RegisterClass("BigInt");           // JSClassId.BigInt = 20
        RegisterClass("ArrayBuffer");      // JSClassId.ArrayBuffer = 21
        RegisterClass("SharedArrayBuffer"); // JSClassId.SharedArrayBuffer = 22
        RegisterClass("Uint8ClampedArray"); // JSClassId.Uint8ClampedArray = 23
        RegisterClass("Int8Array");        // JSClassId.Int8Array = 24
        RegisterClass("Uint8Array");       // JSClassId.Uint8Array = 25
        RegisterClass("Int16Array");       // JSClassId.Int16Array = 26
        RegisterClass("Uint16Array");      // JSClassId.Uint16Array = 27
        RegisterClass("Int32Array");       // JSClassId.Int32Array = 28
        RegisterClass("Uint32Array");      // JSClassId.Uint32Array = 29
        RegisterClass("BigInt64Array");    // JSClassId.BigInt64Array = 30
        RegisterClass("BigUint64Array");   // JSClassId.BigUint64Array = 31
        RegisterClass("Float16Array");     // JSClassId.Float16Array = 32
        RegisterClass("Float32Array");     // JSClassId.Float32Array = 33
        RegisterClass("Float64Array");     // JSClassId.Float64Array = 34
        RegisterClass("DataView");         // JSClassId.DataView = 35
        RegisterClass("Map");              // JSClassId.Map = 36
        RegisterClass("Set");              // JSClassId.Set = 37
        RegisterClass("WeakMap");          // JSClassId.WeakMap = 38
        RegisterClass("WeakSet");          // JSClassId.WeakSet = 39
        RegisterClass("MapIterator");      // JSClassId.MapIterator = 40
        RegisterClass("SetIterator");      // JSClassId.SetIterator = 41
        RegisterClass("ArrayIterator");    // JSClassId.ArrayIterator = 42
        RegisterClass("StringIterator");   // JSClassId.StringIterator = 43
        RegisterClass("Proxy");            // JSClassId.Proxy = 44
        RegisterClass("Module");           // JSClassId.ModuleNS = 45
        RegisterClass("BytecodeFunction"); // JSClassId.BytecodeFunction = 46
        RegisterClass("AsyncFunction");    // JSClassId.AsyncFunction = 47
        RegisterClass("AsyncGeneratorFunction"); // JSClassId.AsyncGeneratorFunction = 48
        RegisterClass("AsyncGenerator");   // JSClassId.AsyncGenerator = 49
        RegisterClass("WeakRef");          // JSClassId.WeakRef = 50
        RegisterClass("FinalizationRegistry"); // JSClassId.FinalizationRegistry = 51
    }

    #endregion

    #region Atom Management

    /// <summary>
    /// Interns a string and returns its atom.
    /// </summary>
    /// <param name="value">The string to intern.</param>
    /// <returns>The interned atom.</returns>
    public JSAtom InternAtom(string value)
    {
        ThrowIfDisposed();
        return _atomTable.GetOrCreateAtom(value);
    }

    /// <summary>
    /// Gets the string value for an atom.
    /// </summary>
    /// <param name="atom">The atom.</param>
    /// <returns>The string value, or null if not found.</returns>
    public string? GetAtomString(JSAtom atom)
    {
        return _atomTable.GetString(atom);
    }

    #endregion

    #region Memory Management

    // NOTE: RecordAllocation/RecordDeallocation are public API infrastructure for memory limiting.
    // These methods are intentionally not called automatically from object constructors because:
    // 1. Estimating managed object sizes accurately in .NET is complex and imprecise
    // 2. Tracking every allocation would add overhead to the critical path
    // 3. Deallocation tracking is difficult without custom weak reference handling
    //
    // The API is exposed for:
    // - Users who want to implement custom memory budgets for sandboxed JS execution
    // - Future integration with explicit allocation sites (ArrayBuffer, large strings, etc.)
    // - Testing memory limit enforcement behavior
    //
    // To enforce memory limits, call RecordAllocation before creating large objects
    // (e.g., ArrayBuffer, large arrays) and check the return value.

    /// <summary>
    /// Records a memory allocation for tracking and limit enforcement.
    /// </summary>
    /// <param name="size">The size of the allocation in bytes.</param>
    /// <returns><c>true</c> if the allocation is allowed; <c>false</c> if it would exceed the memory limit.</returns>
    /// <remarks>
    /// <para>
    /// This method is part of the memory limiting infrastructure. It is not called automatically
    /// by the runtime for every object allocation because accurately tracking managed memory
    /// is complex in .NET. Instead, it is intended for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Explicit tracking of large allocations (e.g., ArrayBuffer, typed arrays)</description></item>
    /// <item><description>Custom memory budgets in sandboxed execution scenarios</description></item>
    /// <item><description>Integration with host applications that need allocation control</description></item>
    /// </list>
    /// <para>
    /// When <see cref="MemoryLimit"/> is set, this method returns <c>false</c> if the allocation
    /// would exceed the limit, allowing the caller to throw an out-of-memory error.
    /// </para>
    /// </remarks>
    public bool RecordAllocation(long size)
    {
        if (size <= 0) return true;

        lock (_memoryLock)
        {
            if (_memoryLimit != NoMemoryLimit && _memoryUsed + size > _memoryLimit)
                return false;

            _memoryUsed += size;
            _allocationCount++;
            return true;
        }
    }

    /// <summary>
    /// Records a memory deallocation for tracking purposes.
    /// </summary>
    /// <param name="size">The size of the deallocation in bytes.</param>
    /// <remarks>
    /// This is the counterpart to <see cref="RecordAllocation"/>. Call this when releasing
    /// tracked memory (e.g., when an ArrayBuffer is garbage collected or explicitly released).
    /// See <see cref="RecordAllocation"/> for details on the memory tracking infrastructure.
    /// </remarks>
    public void RecordDeallocation(long size)
    {
        if (size <= 0) return;

        lock (_memoryLock)
        {
            _memoryUsed = Math.Max(0, _memoryUsed - size);
        }
    }

    /// <summary>
    /// Checks if automatic garbage collection should be triggered based on the GC threshold.
    /// </summary>
    /// <returns><c>true</c> if GC should be triggered.</returns>
    public bool ShouldTriggerGC()
    {
        if (_gcThreshold < 0) return false;
        return _memoryUsed >= _gcThreshold;
    }

    /// <summary>
    /// Runs garbage collection on all contexts in this runtime.
    /// </summary>
    public void RunGC()
    {
        ThrowIfDisposed();

        // In a real implementation, this would traverse all GC-able objects
        // and free unreachable ones. For now, we just trigger .NET GC.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Gets memory statistics for this runtime.
    /// </summary>
    /// <returns>A memory statistics object.</returns>
    public JSMemoryStats GetMemoryStats()
    {
        return new JSMemoryStats(
            memoryUsed: _memoryUsed,
            memoryLimit: _memoryLimit,
            allocationCount: _allocationCount,
            atomCount: _atomTable.Count,
            contextCount: _contexts.Count,
            classCount: _classes.Count
        );
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Sets the current exception for this runtime.
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

    #endregion

    #region Interrupt Handling

    /// <summary>
    /// Requests an interrupt. The next interrupt check will return true.
    /// </summary>
    public void RequestInterrupt()
    {
        _interruptRequested = true;
    }

    /// <summary>
    /// Clears the interrupt request.
    /// </summary>
    public void ClearInterrupt()
    {
        _interruptRequested = false;
    }

    /// <summary>
    /// Checks if execution should be interrupted. Called periodically by the interpreter.
    /// </summary>
    /// <returns><c>true</c> if execution should stop.</returns>
    public bool CheckInterrupt()
    {
        _interruptCounter++;
        if (_interruptCounter >= _interruptInterval)
        {
            _interruptCounter = 0;
            return _interruptRequested;
        }
        return false;
    }

    #endregion

    #region Job Queue

    /// <summary>
    /// Enqueues a job (microtask) to be executed.
    /// </summary>
    /// <param name="function">The function to call.</param>
    /// <param name="args">Arguments to pass to the function.</param>
    /// <param name="context">The context in which to execute the job.</param>
    public void EnqueueJob(JSFunction function, JSValue[] args, JSContext context)
    {
        ThrowIfDisposed();
        _jobQueue.Enqueue(new JSJob(function, args, context));
    }

    /// <summary>
    /// Executes all pending jobs in the queue.
    /// </summary>
    /// <returns>The number of jobs executed.</returns>
    public int ExecutePendingJobs()
    {
        ThrowIfDisposed();

        int count = 0;
        while (_jobQueue.Count > 0)
        {
            var job = _jobQueue.Dequeue();
            try
            {
                // Execute the job - use CallNative for native functions
                job.Function.CallNative(JSValue.Undefined, job.Arguments);
                count++;
            }
            catch (Exception ex)
            {
                // Store the exception as a string value for now
                SetException(JSValue.FromString(ex.Message));
            }

            // Check for interrupt
            if (_interruptRequested)
                break;
        }

        return count;
    }

    /// <summary>
    /// Checks if there are pending jobs.
    /// </summary>
    /// <returns><c>true</c> if there are pending jobs.</returns>
    public bool HasPendingJobs()
    {
        return _jobQueue.Count > 0;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases all resources used by this runtime.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        // Dispose all contexts
        JSContext[] contextsCopy;
        _contextsLock.EnterWriteLock();
        try
        {
            contextsCopy = _contexts.ToArray();
            _contexts.Clear();
        }
        finally
        {
            _contextsLock.ExitWriteLock();
        }

        foreach (var context in contextsCopy)
        {
            context.Dispose();
        }

        // Dispose the contexts lock
        _contextsLock.Dispose();

        // Dispose the classes lock
        _classesLock.Dispose();

        // Clear the job queue
        _jobQueue.Clear();

        // Clear exception
        _currentException = JSValue.Undefined;
        _hasException = false;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(JSRuntime));
    }

    #endregion

    #region Debugger Support

    private string DebuggerDisplay
    {
        get
        {
            if (_isDisposed)
                return "JSRuntime [Disposed]";

            _contextsLock.EnterReadLock();
            try
            {
                return $"JSRuntime [{_contexts.Count} contexts, {_atomTable.Count} atoms]";
            }
            finally
            {
                _contextsLock.ExitReadLock();
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents a class definition in the runtime.
/// </summary>
public sealed class JSClassDef
{
    /// <summary>
    /// Gets the class ID.
    /// </summary>
    public JSClassId ClassId { get; }

    /// <summary>
    /// Gets the class name.
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// Gets the finalizer for instances of this class.
    /// </summary>
    public Action<JSObject>? Finalizer { get; }

    /// <summary>
    /// Gets the call handler for callable objects.
    /// </summary>
    public Func<JSValue, JSValue[], JSValue>? Call { get; }

    /// <summary>
    /// Creates a new class definition.
    /// </summary>
    public JSClassDef(JSClassId classId, string className, Action<JSObject>? finalizer = null, Func<JSValue, JSValue[], JSValue>? call = null)
    {
        ClassId = classId;
        ClassName = className;
        Finalizer = finalizer;
        Call = call;
    }
}

/// <summary>
/// Represents a pending job (microtask) in the job queue.
/// </summary>
internal readonly struct JSJob
{
    public JSFunction Function { get; }
    public JSValue[] Arguments { get; }
    public JSContext Context { get; }

    public JSJob(JSFunction function, JSValue[] arguments, JSContext context)
    {
        Function = function;
        Arguments = arguments;
        Context = context;
    }
}

/// <summary>
/// Contains memory statistics for a runtime.
/// </summary>
public readonly struct JSMemoryStats
{
    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    public long MemoryUsed { get; }

    /// <summary>
    /// Gets the memory limit in bytes (-1 for no limit).
    /// </summary>
    public long MemoryLimit { get; }

    /// <summary>
    /// Gets the total number of allocations.
    /// </summary>
    public long AllocationCount { get; }

    /// <summary>
    /// Gets the number of interned atoms.
    /// </summary>
    public int AtomCount { get; }

    /// <summary>
    /// Gets the number of contexts.
    /// </summary>
    public int ContextCount { get; }

    /// <summary>
    /// Gets the number of registered classes.
    /// </summary>
    public int ClassCount { get; }

    /// <summary>
    /// Creates a new memory stats instance.
    /// </summary>
    public JSMemoryStats(long memoryUsed, long memoryLimit, long allocationCount, int atomCount, int contextCount, int classCount)
    {
        MemoryUsed = memoryUsed;
        MemoryLimit = memoryLimit;
        AllocationCount = allocationCount;
        AtomCount = atomCount;
        ContextCount = contextCount;
        ClassCount = classCount;
    }
}
