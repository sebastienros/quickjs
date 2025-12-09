// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Generator state enumeration.
/// </summary>
/// <remarks>
/// From QuickJS (quickjs.c):
/// <code>
/// typedef enum {
///     JS_GENERATOR_STATE_SUSPENDED_START,
///     JS_GENERATOR_STATE_SUSPENDED_YIELD,
///     JS_GENERATOR_STATE_SUSPENDED_YIELD_STAR,
///     JS_GENERATOR_STATE_EXECUTING,
///     JS_GENERATOR_STATE_COMPLETED,
/// } JSGeneratorStateEnum;
/// </code>
/// </remarks>
public enum GeneratorState
{
    /// <summary>Generator created but not yet started.</summary>
    SuspendedStart,

    /// <summary>Generator suspended at a yield expression.</summary>
    SuspendedYield,

    /// <summary>Generator suspended at a yield* expression.</summary>
    SuspendedYieldStar,

    /// <summary>Generator is currently executing.</summary>
    Executing,

    /// <summary>Generator has completed (returned or threw).</summary>
    Completed
}

/// <summary>
/// Represents the execution state of a generator or async function.
/// </summary>
/// <remarks>
/// <para>
/// Based on JSAsyncFunctionState from QuickJS (quickjs.c):
/// </para>
/// <code>
/// typedef struct JSAsyncFunctionState {
///     JSGCObjectHeader header;
///     JSValue this_val;
///     int argc;
///     BOOL throw_flag;
///     BOOL is_completed;
///     JSValue resolving_funcs[2];
///     JSStackFrame frame;
/// } JSAsyncFunctionState;
/// </code>
/// <para>
/// This state preserves all execution context between yield points,
/// allowing the generator to resume from where it left off.
/// </para>
/// </remarks>
public sealed class GeneratorFunctionState
{
    /// <summary>
    /// Gets or sets the 'this' value for the generator function.
    /// </summary>
    public JSValue ThisValue { get; set; }

    /// <summary>
    /// Gets or sets the function definition being executed.
    /// </summary>
    public JSFunctionDef FunctionDef { get; }

    /// <summary>
    /// Gets or sets the function arguments.
    /// </summary>
    public JSValue[] Args { get; }

    /// <summary>
    /// Gets or sets whether an exception should be thrown when resumed.
    /// </summary>
    public bool ThrowFlag { get; set; }

    /// <summary>
    /// Gets or sets whether the function has completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the current program counter (bytecode offset).
    /// </summary>
    public int ProgramCounter { get; set; }

    /// <summary>
    /// Gets the operand stack.
    /// </summary>
    public List<JSValue> Stack { get; } = new List<JSValue>();

    /// <summary>
    /// Gets the local variables.
    /// </summary>
    public JSValue[] Locals { get; }

    /// <summary>
    /// Gets or sets the closure variable references.
    /// </summary>
    public JSVarRef[]? VarRefs { get; set; }

    /// <summary>
    /// Gets or sets the value to throw (when ThrowFlag is true).
    /// </summary>
    public JSValue ThrowValue { get; set; }

    /// <summary>
    /// Creates a new generator function state.
    /// </summary>
    public GeneratorFunctionState(JSFunctionDef functionDef, JSValue thisValue, JSValue[] args, JSVarRef[]? varRefs)
    {
        FunctionDef = functionDef ?? throw new ArgumentNullException(nameof(functionDef));
        ThisValue = thisValue;
        Args = args ?? Array.Empty<JSValue>();
        VarRefs = varRefs;
        ProgramCounter = 0;
        ThrowValue = JSValue.Undefined;

        // Initialize locals (Vars in JSFunctionDef)
        int localCount = functionDef.Vars.Count;
        Locals = new JSValue[localCount];
        for (int i = 0; i < localCount; i++)
        {
            Locals[i] = JSValue.Undefined;
        }
    }
}

/// <summary>
/// Represents a JavaScript Generator object.
/// </summary>
/// <remarks>
/// <para>
/// From QuickJS (quickjs.c):
/// </para>
/// <code>
/// typedef struct JSGeneratorData {
///     JSGeneratorStateEnum state;
///     JSAsyncFunctionState *func_state;
/// } JSGeneratorData;
/// </code>
/// <para>
/// A generator is created when a generator function is called. It maintains
/// the execution state and provides the iterator protocol methods (next, return, throw).
/// </para>
/// </remarks>
public sealed class JSGenerator : JSObject
{
    #region Constants

    /// <summary>Magic value for next() method.</summary>
    internal const int MagicNext = 0;

    /// <summary>Magic value for return() method.</summary>
    internal const int MagicReturn = 1;

    /// <summary>Magic value for throw() method.</summary>
    internal const int MagicThrow = 2;

    #endregion

    #region Fields

    private readonly JSContext _context;
    private GeneratorState _state;
    private GeneratorFunctionState? _funcState;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new generator object from a function state.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="funcState">The generator function state.</param>
    /// <param name="prototype">The generator prototype.</param>
    public JSGenerator(JSContext context, GeneratorFunctionState funcState, JSObject? prototype)
        : base(prototype, JSClassId.Generator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _funcState = funcState ?? throw new ArgumentNullException(nameof(funcState));
        _state = GeneratorState.SuspendedStart;
    }

    /// <summary>
    /// Creates a new generator object from a generator function.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="func">The generator function.</param>
    /// <param name="thisVal">The 'this' value.</param>
    /// <param name="args">The function arguments.</param>
    public JSGenerator(JSContext context, JSFunction func, JSValue thisVal, JSValue[] args)
        : base(context.GetGeneratorPrototype(), JSClassId.Generator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        if (func?.FunctionDef == null)
            throw new ArgumentNullException(nameof(func));

        _funcState = new GeneratorFunctionState(func.FunctionDef, thisVal, args, func.VarRefs);
        _state = GeneratorState.SuspendedStart;
    }

    #endregion

    #region Static Initialization

    /// <summary>
    /// Initializes the Generator prototype with next, return, throw methods.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="prototype">The generator prototype object.</param>
    public static void InitializeGeneratorPrototype(JSContext context, JSObject prototype)
    {
        var functionProto = context.GetClassPrototype(JSClassId.CFunction);

        // Generator.prototype.next
        JSValue NextMethod(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSGenerator gen)
            {
                context.ThrowTypeError("Generator.prototype.next called on non-generator");
                return JSValue.Exception;
            }
            var value = args.Length > 0 ? args[0] : JSValue.Undefined;
            return gen.Next(value);
        }
        prototype.Set("next", JSValue.FromObject(new JSFunction(NextMethod, "next", 1, functionProto)));

        // Generator.prototype.return
        JSValue ReturnMethod(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSGenerator gen)
            {
                context.ThrowTypeError("Generator.prototype.return called on non-generator");
                return JSValue.Exception;
            }
            var value = args.Length > 0 ? args[0] : JSValue.Undefined;
            return gen.Return(value);
        }
        prototype.Set("return", JSValue.FromObject(new JSFunction(ReturnMethod, "return", 1, functionProto)));

        // Generator.prototype.throw
        JSValue ThrowMethod(JSValue thisVal, JSValue[] args)
        {
            if (!thisVal.IsObject || thisVal.AsObject() is not JSGenerator gen)
            {
                context.ThrowTypeError("Generator.prototype.throw called on non-generator");
                return JSValue.Exception;
            }
            var exception = args.Length > 0 ? args[0] : JSValue.Undefined;
            return gen.Throw(exception);
        }
        prototype.Set("throw", JSValue.FromObject(new JSFunction(ThrowMethod, "throw", 1, functionProto)));

        // Set toStringTag
        prototype.Set("constructor", JSValue.Undefined); // Generator.prototype.constructor is undefined per spec
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current generator state.
    /// </summary>
    public GeneratorState State => _state;

    /// <summary>
    /// Gets the function state.
    /// </summary>
    internal GeneratorFunctionState? FunctionState => _funcState;

    #endregion

    #region Iterator Protocol Methods

    /// <summary>
    /// Implements generator.next(value).
    /// </summary>
    /// <param name="value">The value to send into the generator.</param>
    /// <returns>An iterator result object {value, done}.</returns>
    public JSValue Next(JSValue value)
    {
        return Resume(value, MagicNext);
    }

    /// <summary>
    /// Implements generator.return(value).
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>An iterator result object {value, done}.</returns>
    public JSValue Return(JSValue value)
    {
        return Resume(value, MagicReturn);
    }

    /// <summary>
    /// Implements generator.throw(exception).
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>An iterator result object {value, done}.</returns>
    public JSValue Throw(JSValue exception)
    {
        return Resume(exception, MagicThrow);
    }

    #endregion

    #region Execution

    /// <summary>
    /// Resumes generator execution.
    /// </summary>
    /// <remarks>
    /// Based on js_generator_next from QuickJS (quickjs.c lines 20481-20556).
    /// </remarks>
    private JSValue Resume(JSValue arg, int magic)
    {
        bool done = true;
        JSValue result;

        switch (_state)
        {
            case GeneratorState.SuspendedStart:
                if (magic == MagicNext)
                {
                    // Start execution
                    result = ExecuteGenerator(arg, false);
                    done = _state == GeneratorState.Completed;
                }
                else if (magic == MagicReturn)
                {
                    // Return immediately
                    FreeGeneratorStack();
                    result = arg;
                    done = true;
                }
                else // MagicThrow
                {
                    // Throw immediately
                    FreeGeneratorStack();
                    _context.ThrowError(JSErrorType.Error, JSValueConversion.ToString(arg));
                    return JSValue.Exception;
                }
                break;

            case GeneratorState.SuspendedYield:
            case GeneratorState.SuspendedYieldStar:
                if (magic == MagicThrow && _state == GeneratorState.SuspendedYield)
                {
                    // Throw into the generator
                    _funcState!.ThrowFlag = true;
                    _funcState.ThrowValue = arg;
                }
                else
                {
                    // Send value into generator (replaces yield result)
                    _funcState!.ThrowFlag = false;
                }

                result = ExecuteGenerator(arg, magic != MagicThrow);
                done = _state == GeneratorState.Completed;

                if (_state == GeneratorState.SuspendedYieldStar)
                {
                    // For yield*, return the value/done pair directly
                    return result;
                }
                break;

            case GeneratorState.Completed:
                // Generator already finished
                switch (magic)
                {
                    case MagicNext:
                        result = JSValue.Undefined;
                        break;
                    case MagicReturn:
                        result = arg;
                        break;
                    case MagicThrow:
                        _context.ThrowError(JSErrorType.Error, JSValueConversion.ToString(arg));
                        return JSValue.Exception;
                    default:
                        result = JSValue.Undefined;
                        break;
                }
                done = true;
                break;

            case GeneratorState.Executing:
                _context.ThrowTypeError("cannot invoke a running generator");
                return JSValue.Exception;

            default:
                result = JSValue.Undefined;
                break;
        }

        return CreateIteratorResult(result, done);
    }

    /// <summary>
    /// Executes the generator until the next yield or completion.
    /// </summary>
    private JSValue ExecuteGenerator(JSValue sendValue, bool useSendValue)
    {
        if (_funcState == null)
            return JSValue.Undefined;

        _state = GeneratorState.Executing;

        try
        {
            var (result, returnType) = ExecuteGeneratorBytecode(sendValue, useSendValue);

            switch (returnType)
            {
                case GeneratorReturnType.Yield:
                    _state = GeneratorState.SuspendedYield;
                    return result;

                case GeneratorReturnType.YieldStar:
                    _state = GeneratorState.SuspendedYieldStar;
                    return result;

                case GeneratorReturnType.Return:
                case GeneratorReturnType.Exception:
                    FreeGeneratorStack();
                    return result;

                default:
                    FreeGeneratorStack();
                    return result;
            }
        }
        catch
        {
            FreeGeneratorStack();
            throw;
        }
    }

    /// <summary>
    /// Executes generator bytecode until yield or completion.
    /// </summary>
    private (JSValue Value, GeneratorReturnType ReturnType) ExecuteGeneratorBytecode(JSValue sendValue, bool useSendValue)
    {
        if (_funcState == null)
            return (JSValue.Undefined, GeneratorReturnType.Return);

        // If throw flag is set, throw the exception
        if (_funcState.ThrowFlag)
        {
            _funcState.ThrowFlag = false;
            _context.SetException(_funcState.ThrowValue);
            _funcState.IsCompleted = true;
            return (JSValue.Exception, GeneratorReturnType.Exception);
        }

        var bytecode = _funcState.FunctionDef.ByteCode.ToArray();
        int pc = _funcState.ProgramCounter;

        // If resuming from yield, the send value replaces the yield result
        if (useSendValue && pc > 0)
        {
            _funcState.Stack.Add(sendValue);
        }

        // Create an interpreter-like execution loop
        while (pc < bytecode.Length && !_context.HasException)
        {
            var opcode = (OpCode)bytecode[pc++];

            switch (opcode)
            {
                case OpCode.Yield:
                    {
                        // Save state and return the yielded value
                        var yieldValue = PopStack();
                        _funcState.ProgramCounter = pc;
                        return (yieldValue, GeneratorReturnType.Yield);
                    }

                case OpCode.YieldStar:
                    {
                        // Save state and return for delegating yield
                        var iterValue = PopStack();
                        _funcState.ProgramCounter = pc;
                        return (iterValue, GeneratorReturnType.YieldStar);
                    }

                case OpCode.InitialYield:
                    {
                        // Initial yield just suspends at the start
                        _funcState.ProgramCounter = pc;
                        return (JSValue.Undefined, GeneratorReturnType.Yield);
                    }

                case OpCode.Return:
                    {
                        var returnValue = PopStack();
                        _funcState.IsCompleted = true;
                        return (returnValue, GeneratorReturnType.Return);
                    }

                case OpCode.ReturnUndef:
                    {
                        _funcState.IsCompleted = true;
                        return (JSValue.Undefined, GeneratorReturnType.Return);
                    }

                // Basic stack operations
                case OpCode.PushI32:
                    {
                        int value = ReadI32(bytecode, ref pc);
                        PushStack(JSValue.FromInt32(value));
                    }
                    break;

                case OpCode.Push0:
                    PushStack(JSValue.FromInt32(0));
                    break;

                case OpCode.Push1:
                    PushStack(JSValue.FromInt32(1));
                    break;

                case OpCode.PushMinus1:
                    PushStack(JSValue.FromInt32(-1));
                    break;

                case OpCode.PushConst:
                    {
                        int constIdx = ReadI32(bytecode, ref pc);
                        PushStack(_funcState.FunctionDef.Constants.Get(constIdx));
                    }
                    break;

                case OpCode.PushConst8:
                    {
                        int constIdx = bytecode[pc++];
                        PushStack(_funcState.FunctionDef.Constants.Get(constIdx));
                    }
                    break;

                case OpCode.Undefined:
                    PushStack(JSValue.Undefined);
                    break;

                case OpCode.Null:
                    PushStack(JSValue.Null);
                    break;

                case OpCode.PushTrue:
                    PushStack(JSValue.True);
                    break;

                case OpCode.PushFalse:
                    PushStack(JSValue.False);
                    break;

                case OpCode.Drop:
                    PopStack();
                    break;

                case OpCode.Dup:
                    {
                        var top = PeekStack();
                        PushStack(top);
                    }
                    break;

                case OpCode.Dup2:
                    {
                        var second = _funcState.Stack[_funcState.Stack.Count - 2];
                        var first = _funcState.Stack[_funcState.Stack.Count - 1];
                        PushStack(second);
                        PushStack(first);
                    }
                    break;

                case OpCode.Nip:
                    {
                        var top = PopStack();
                        PopStack(); // Remove second
                        PushStack(top);
                    }
                    break;

                case OpCode.Nip1:
                    {
                        var top = PopStack();
                        var second = PopStack();
                        PopStack(); // Remove third
                        PushStack(second);
                        PushStack(top);
                    }
                    break;

                // Arithmetic
                case OpCode.Add:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        // String concatenation or numeric addition
                        if (a.IsString || b.IsString)
                            PushStack(JSValue.FromString(JSValueConversion.ToString(a) + JSValueConversion.ToString(b)));
                        else
                            PushStack(JSValue.FromDouble(a.ToDouble() + b.ToDouble()));
                    }
                    break;

                case OpCode.Sub:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromDouble(a.ToDouble() - b.ToDouble()));
                    }
                    break;

                case OpCode.Mul:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromDouble(a.ToDouble() * b.ToDouble()));
                    }
                    break;

                case OpCode.Div:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromDouble(a.ToDouble() / b.ToDouble()));
                    }
                    break;

                case OpCode.Inc:
                    {
                        var a = PopStack();
                        PushStack(JSValue.FromDouble(a.ToDouble() + 1));
                    }
                    break;

                case OpCode.Dec:
                    {
                        var a = PopStack();
                        PushStack(JSValue.FromDouble(a.ToDouble() - 1));
                    }
                    break;

                // Local variable access
                case OpCode.GetLoc:
                    {
                        int idx = ReadU16(bytecode, ref pc);
                        PushStack(idx < _funcState.Locals.Length ? _funcState.Locals[idx] : JSValue.Undefined);
                    }
                    break;

                case OpCode.GetLoc0:
                    PushStack(_funcState.Locals.Length > 0 ? _funcState.Locals[0] : JSValue.Undefined);
                    break;

                case OpCode.GetLoc1:
                    PushStack(_funcState.Locals.Length > 1 ? _funcState.Locals[1] : JSValue.Undefined);
                    break;

                case OpCode.GetLoc2:
                    PushStack(_funcState.Locals.Length > 2 ? _funcState.Locals[2] : JSValue.Undefined);
                    break;

                case OpCode.GetLoc3:
                    PushStack(_funcState.Locals.Length > 3 ? _funcState.Locals[3] : JSValue.Undefined);
                    break;

                case OpCode.PutLoc:
                    {
                        int idx = ReadU16(bytecode, ref pc);
                        var val = PopStack();
                        if (idx < _funcState.Locals.Length)
                            _funcState.Locals[idx] = val;
                    }
                    break;

                case OpCode.PutLoc0:
                    if (_funcState.Locals.Length > 0)
                        _funcState.Locals[0] = PopStack();
                    else
                        PopStack();
                    break;

                case OpCode.PutLoc1:
                    if (_funcState.Locals.Length > 1)
                        _funcState.Locals[1] = PopStack();
                    else
                        PopStack();
                    break;

                case OpCode.PutLoc2:
                    if (_funcState.Locals.Length > 2)
                        _funcState.Locals[2] = PopStack();
                    else
                        PopStack();
                    break;

                case OpCode.PutLoc3:
                    if (_funcState.Locals.Length > 3)
                        _funcState.Locals[3] = PopStack();
                    else
                        PopStack();
                    break;

                // Argument access
                case OpCode.GetArg:
                    {
                        int idx = ReadU16(bytecode, ref pc);
                        PushStack(idx < _funcState.Args.Length ? _funcState.Args[idx] : JSValue.Undefined);
                    }
                    break;

                case OpCode.GetArg0:
                    PushStack(_funcState.Args.Length > 0 ? _funcState.Args[0] : JSValue.Undefined);
                    break;

                case OpCode.GetArg1:
                    PushStack(_funcState.Args.Length > 1 ? _funcState.Args[1] : JSValue.Undefined);
                    break;

                case OpCode.GetArg2:
                    PushStack(_funcState.Args.Length > 2 ? _funcState.Args[2] : JSValue.Undefined);
                    break;

                case OpCode.GetArg3:
                    PushStack(_funcState.Args.Length > 3 ? _funcState.Args[3] : JSValue.Undefined);
                    break;

                // Comparisons
                case OpCode.Lt:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromBoolean(a.ToDouble() < b.ToDouble()));
                    }
                    break;

                case OpCode.Lte:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromBoolean(a.ToDouble() <= b.ToDouble()));
                    }
                    break;

                case OpCode.Gt:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromBoolean(a.ToDouble() > b.ToDouble()));
                    }
                    break;

                case OpCode.Gte:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromBoolean(a.ToDouble() >= b.ToDouble()));
                    }
                    break;

                case OpCode.Eq:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        // Simplified abstract equality - full implementation would handle type coercion
                        bool equal = AbstractEquality(a, b);
                        PushStack(JSValue.FromBoolean(equal));
                    }
                    break;

                case OpCode.StrictEq:
                    {
                        var b = PopStack();
                        var a = PopStack();
                        PushStack(JSValue.FromBoolean(JSValueConversion.StrictEquals(a, b)));
                    }
                    break;

                // Control flow
                case OpCode.Goto:
                case OpCode.Goto8:
                case OpCode.Goto16:
                    {
                        int offset;
                        if (opcode == OpCode.Goto8)
                            offset = (sbyte)bytecode[pc++];
                        else if (opcode == OpCode.Goto16)
                            offset = ReadI16(bytecode, ref pc);
                        else
                            offset = ReadI32(bytecode, ref pc);
                        pc += offset - (opcode == OpCode.Goto8 ? 2 : opcode == OpCode.Goto16 ? 3 : 5);
                    }
                    break;

                case OpCode.IfTrue:
                case OpCode.IfTrue8:
                    {
                        var cond = PopStack();
                        int offset;
                        if (opcode == OpCode.IfTrue8)
                            offset = (sbyte)bytecode[pc++];
                        else
                            offset = ReadI32(bytecode, ref pc);

                        if (JSValueConversion.ToBoolean(cond))
                            pc += offset - (opcode == OpCode.IfTrue8 ? 2 : 5);
                    }
                    break;

                case OpCode.IfFalse:
                case OpCode.IfFalse8:
                    {
                        var cond = PopStack();
                        int offset;
                        if (opcode == OpCode.IfFalse8)
                            offset = (sbyte)bytecode[pc++];
                        else
                            offset = ReadI32(bytecode, ref pc);

                        if (!JSValueConversion.ToBoolean(cond))
                            pc += offset - (opcode == OpCode.IfFalse8 ? 2 : 5);
                    }
                    break;

                // For other opcodes, we need to handle them or skip
                default:
                    {
                        // Skip unknown opcodes based on their expected size
                        // This is not ideal but allows basic generators to work
                        var info = OpCodes.GetInfo(opcode);
                        if (info.Size > 1)
                        {
                            pc += info.Size - 1;
                        }
                        // For simplicity, push undefined for operations we don't handle
                        // A full implementation would handle all opcodes
                    }
                    break;
            }
        }

        // Reached end of bytecode
        _funcState.IsCompleted = true;
        return (PopStack(), GeneratorReturnType.Return);
    }

    #region Stack Helpers

    private void PushStack(JSValue value)
    {
        _funcState!.Stack.Add(value);
    }

    private JSValue PopStack()
    {
        if (_funcState!.Stack.Count == 0)
            return JSValue.Undefined;

        int lastIdx = _funcState.Stack.Count - 1;
        var value = _funcState.Stack[lastIdx];
        _funcState.Stack.RemoveAt(lastIdx);
        return value;
    }

    private JSValue PeekStack()
    {
        if (_funcState!.Stack.Count == 0)
            return JSValue.Undefined;

        return _funcState.Stack[_funcState.Stack.Count - 1];
    }

    #endregion

    #region Bytecode Reading Helpers

    private static int ReadI32(byte[] bytecode, ref int pc)
    {
        int value = bytecode[pc] |
                    (bytecode[pc + 1] << 8) |
                    (bytecode[pc + 2] << 16) |
                    (bytecode[pc + 3] << 24);
        pc += 4;
        return value;
    }

    private static ushort ReadU16(byte[] bytecode, ref int pc)
    {
        ushort value = (ushort)(bytecode[pc] | (bytecode[pc + 1] << 8));
        pc += 2;
        return value;
    }

    private static short ReadI16(byte[] bytecode, ref int pc)
    {
        short value = (short)(bytecode[pc] | (bytecode[pc + 1] << 8));
        pc += 2;
        return value;
    }

    /// <summary>
    /// Simplified abstract equality comparison.
    /// </summary>
    private static bool AbstractEquality(JSValue x, JSValue y)
    {
        // Same type - use strict equality
        if (x.Tag == y.Tag)
        {
            return JSValueConversion.StrictEquals(x, y);
        }

        // null == undefined
        if ((x.IsNull && y.IsUndefined) || (x.IsUndefined && y.IsNull))
            return true;

        // Number comparisons
        if (x.IsNumber && y.IsNumber)
            return x.ToDouble() == y.ToDouble();

        // String/Number comparison
        if (x.IsNumber && y.IsString)
            return x.ToDouble() == JSValueConversion.ToNumber(y);
        if (x.IsString && y.IsNumber)
            return JSValueConversion.ToNumber(x) == y.ToDouble();

        // Boolean conversion
        if (x.IsBool)
            return AbstractEquality(JSValue.FromDouble(x.IsTrue ? 1 : 0), y);
        if (y.IsBool)
            return AbstractEquality(x, JSValue.FromDouble(y.IsTrue ? 1 : 0));

        // Object reference equality
        if (x.IsObject && y.IsObject)
            return ReferenceEquals(x.AsObject(), y.AsObject());

        return false;
    }

    #endregion

    /// <summary>
    /// Frees the generator stack and marks it as completed.
    /// </summary>
    private void FreeGeneratorStack()
    {
        if (_state == GeneratorState.Completed)
            return;

        _funcState = null;
        _state = GeneratorState.Completed;
    }

    /// <summary>
    /// Creates an iterator result object {value, done}.
    /// </summary>
    private JSValue CreateIteratorResult(JSValue value, bool done)
    {
        var result = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Object);
        result.Set("value", value);
        result.Set("done", JSValue.FromBoolean(done));
        return JSValue.FromObject(result);
    }

    #endregion
}

/// <summary>
/// Return type from generator execution.
/// </summary>
internal enum GeneratorReturnType
{
    /// <summary>Normal yield.</summary>
    Yield,

    /// <summary>Delegating yield (yield*).</summary>
    YieldStar,

    /// <summary>Normal return.</summary>
    Return,

    /// <summary>Exception thrown.</summary>
    Exception
}
