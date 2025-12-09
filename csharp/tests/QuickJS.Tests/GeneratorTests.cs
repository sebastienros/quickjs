// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for generator functions and the JSGenerator class (Step 9.2).
/// </summary>
public class GeneratorTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public GeneratorTests()
    {
        _context = _runtime.CreateContext();
    }

    #region Generator Creation Tests

    [Fact]
    public void JSGenerator_CanBeCreated()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.InitialYield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var state = new GeneratorFunctionState(funcDef, JSValue.Undefined, Array.Empty<JSValue>(), null);
        var gen = new JSGenerator(_context, state, _context.GetGeneratorPrototype());

        Assert.NotNull(gen);
        Assert.Equal(GeneratorState.SuspendedStart, gen.State);
    }

    [Fact]
    public void JSGenerator_CreatedFromFunction()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.InitialYield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        Assert.NotNull(gen);
        Assert.Equal(GeneratorState.SuspendedStart, gen.State);
    }

    [Fact]
    public void GeneratorPrototype_Exists()
    {
        var proto = _context.GetGeneratorPrototype();
        Assert.NotNull(proto);
    }

    [Fact]
    public void GeneratorPrototype_HasNextMethod()
    {
        var proto = _context.GetGeneratorPrototype()!;
        var nextVal = proto.Get("next");
        Assert.True(nextVal.IsObject);
        Assert.IsType<JSFunction>(nextVal.AsObject());
    }

    [Fact]
    public void GeneratorPrototype_HasReturnMethod()
    {
        var proto = _context.GetGeneratorPrototype()!;
        var returnVal = proto.Get("return");
        Assert.True(returnVal.IsObject);
        Assert.IsType<JSFunction>(returnVal.AsObject());
    }

    [Fact]
    public void GeneratorPrototype_HasThrowMethod()
    {
        var proto = _context.GetGeneratorPrototype()!;
        var throwVal = proto.Get("throw");
        Assert.True(throwVal.IsObject);
        Assert.IsType<JSFunction>(throwVal.AsObject());
    }

    #endregion

    #region Generator State Tests

    [Fact]
    public void GeneratorState_InitialValue()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.InitialYield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Equal(GeneratorState.SuspendedStart, gen.State);
    }

    [Fact]
    public void GeneratorFunctionState_StoresArgs()
    {
        var funcDef = new JSFunctionDef();
        var args = new[] { JSValue.FromInt32(1), JSValue.FromInt32(2) };
        var state = new GeneratorFunctionState(funcDef, JSValue.Undefined, args, null);

        Assert.Equal(2, state.Args.Length);
        Assert.Equal(1, state.Args[0].ToInt32());
        Assert.Equal(2, state.Args[1].ToInt32());
    }

    [Fact]
    public void GeneratorFunctionState_StoresThisValue()
    {
        var funcDef = new JSFunctionDef();
        var thisVal = JSValue.FromInt32(42);
        var state = new GeneratorFunctionState(funcDef, thisVal, Array.Empty<JSValue>(), null);

        Assert.Equal(42, state.ThisValue.ToInt32());
    }

    [Fact]
    public void GeneratorFunctionState_InitializesLocals()
    {
        var funcDef = new JSFunctionDef();
        funcDef.Vars.Add(new JSVarDef());
        funcDef.Vars.Add(new JSVarDef());

        var state = new GeneratorFunctionState(funcDef, JSValue.Undefined, Array.Empty<JSValue>(), null);

        Assert.Equal(2, state.Locals.Length);
        Assert.True(state.Locals[0].IsUndefined);
        Assert.True(state.Locals[1].IsUndefined);
    }

    #endregion

    #region Next Method Tests

    [Fact]
    public void Generator_Next_ReturnsIteratorResult()
    {
        // Generator that immediately returns
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var result = gen.Next(JSValue.Undefined);

        Assert.True(result.IsObject);
        var resultObj = result.AsObject();
        Assert.True(resultObj.HasProperty("value"));
        Assert.True(resultObj.HasProperty("done"));
    }

    [Fact]
    public void Generator_Next_CompletedGeneratorReturnsDoneTrue()
    {
        // Generator that immediately returns
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var result = gen.Next(JSValue.Undefined);
        var resultObj = result.AsObject();

        Assert.True(resultObj.Get("done").IsTrue);
    }

    [Fact]
    public void Generator_Next_YieldReturnsDoneFalse()
    {
        // Generator: yield 42;
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(42);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var result = gen.Next(JSValue.Undefined);
        var resultObj = result.AsObject();

        Assert.False(resultObj.Get("done").IsTrue);
        Assert.Equal(42, resultObj.Get("value").ToInt32());
    }

    [Fact]
    public void Generator_Next_MultipleYields()
    {
        // Generator: yield 1; yield 2; yield 3;
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };

        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(1);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.Drop);

        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(2);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.Drop);

        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(3);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.Drop);

        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        // First yield: 1
        var result1 = gen.Next(JSValue.Undefined);
        Assert.Equal(1, result1.AsObject().Get("value").ToInt32());
        Assert.False(result1.AsObject().Get("done").IsTrue);

        // Second yield: 2
        var result2 = gen.Next(JSValue.Undefined);
        Assert.Equal(2, result2.AsObject().Get("value").ToInt32());
        Assert.False(result2.AsObject().Get("done").IsTrue);

        // Third yield: 3
        var result3 = gen.Next(JSValue.Undefined);
        Assert.Equal(3, result3.AsObject().Get("value").ToInt32());
        Assert.False(result3.AsObject().Get("done").IsTrue);

        // Done
        var result4 = gen.Next(JSValue.Undefined);
        Assert.True(result4.AsObject().Get("done").IsTrue);
    }

    #endregion

    #region Return Method Tests

    [Fact]
    public void Generator_Return_CompletesGenerator()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(1);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var result = gen.Return(JSValue.FromInt32(100));
        var resultObj = result.AsObject();

        Assert.True(resultObj.Get("done").IsTrue);
        Assert.Equal(100, resultObj.Get("value").ToInt32());
        Assert.Equal(GeneratorState.Completed, gen.State);
    }

    [Fact]
    public void Generator_Return_OnSuspendedStart()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(1);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Equal(GeneratorState.SuspendedStart, gen.State);

        var result = gen.Return(JSValue.FromString("early"));
        var resultObj = result.AsObject();

        Assert.True(resultObj.Get("done").IsTrue);
        Assert.Equal("early", resultObj.Get("value").ToString());
    }

    #endregion

    #region Throw Method Tests

    [Fact]
    public void Generator_Throw_OnSuspendedStart_ThrowsError()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.PushI32);
        funcDef.ByteCode.EmitI32(1);
        funcDef.ByteCode.EmitOp(OpCode.Yield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        var result = gen.Throw(JSValue.FromString("error!"));

        Assert.True(result.IsException);
        Assert.Equal(GeneratorState.Completed, gen.State);
    }

    [Fact]
    public void Generator_Throw_OnCompletedGenerator()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        // Complete the generator
        gen.Next(JSValue.Undefined);
        Assert.Equal(GeneratorState.Completed, gen.State);

        // Throw on completed generator
        _context.ClearException();
        var result = gen.Throw(JSValue.FromString("error!"));

        Assert.True(result.IsException);
    }

    #endregion

    #region Completed Generator Tests

    [Fact]
    public void Generator_Next_OnCompletedGenerator_ReturnsUndefined()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var gen = new JSGenerator(_context, func, JSValue.Undefined, Array.Empty<JSValue>());

        // Complete the generator
        gen.Next(JSValue.Undefined);
        Assert.Equal(GeneratorState.Completed, gen.State);

        // Call next again
        var result = gen.Next(JSValue.Undefined);
        var resultObj = result.AsObject();

        Assert.True(resultObj.Get("done").IsTrue);
        Assert.True(resultObj.Get("value").IsUndefined);
    }

    #endregion

    #region Generator State Enum Tests

    [Fact]
    public void GeneratorState_Values()
    {
        Assert.Equal(0, (int)GeneratorState.SuspendedStart);
        Assert.Equal(1, (int)GeneratorState.SuspendedYield);
        Assert.Equal(2, (int)GeneratorState.SuspendedYieldStar);
        Assert.Equal(3, (int)GeneratorState.Executing);
        Assert.Equal(4, (int)GeneratorState.Completed);
    }

    #endregion

    #region Interpreter Generator Integration Tests

    [Fact]
    public void Interpreter_CallGeneratorFunction_ReturnsGenerator()
    {
        var funcDef = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };
        funcDef.ByteCode.EmitOp(OpCode.InitialYield);
        funcDef.ByteCode.EmitOp(OpCode.ReturnUndef);

        var func = new JSFunction(funcDef);
        var interpreter = new Interpreter(_context);

        // Push the function and call it
        var callerDef = new JSFunctionDef();
        int funcIdx = callerDef.Constants.Add(JSValue.FromObject(func));
        callerDef.ByteCode.EmitOp(OpCode.PushConst);
        callerDef.ByteCode.EmitU32((uint)funcIdx);
        callerDef.ByteCode.EmitOp(OpCode.Call0);
        callerDef.ByteCode.EmitOp(OpCode.Return);

        var result = interpreter.Execute(callerDef);

        Assert.False(_context.HasException);
        Assert.True(result.IsObject);
        Assert.IsType<JSGenerator>(result.AsObject());
    }

    #endregion

    #region InitializeGeneratorPrototype Tests

    [Fact]
    public void InitializeGeneratorPrototype_AddsAllMethods()
    {
        var proto = new JSObject(_context.GetClassPrototype(JSClassId.Object), JSClassId.Generator);
        JSGenerator.InitializeGeneratorPrototype(_context, proto);

        Assert.True(proto.HasProperty("next"));
        Assert.True(proto.HasProperty("return"));
        Assert.True(proto.HasProperty("throw"));
    }

    #endregion
}
