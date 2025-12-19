using System;
using BenchmarkDotNet.Attributes;

namespace QuickJS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CompileExecuteBenchmarks
{
    private const string ArithmeticSource = "1 + 2 * 3";

    private JSRuntime _runtime = null!;
    private JSContext _context = null!;
    private JSFunctionDef _compiledArithmetic = null!;

    [GlobalSetup]
    public void Setup()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();

        _compiledArithmetic = JSEval.Compile(_runtime, ArithmeticSource, "<benchmark>", isModule: false)
            ?? throw new InvalidOperationException("Failed to compile benchmark script.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
        _runtime?.Dispose();
    }

    [Benchmark]
    public JSFunctionDef QuickJS_Arithmetic_Compile()
    {
        return JSEval.Compile(_runtime, ArithmeticSource, "<benchmark>", isModule: false)
            ?? throw new InvalidOperationException("Unexpected compile failure.");
    }

    [Benchmark]
    public JSValue QuickJS_Arithmetic_Execute()
    {
        return _context.Execute(_compiledArithmetic);
    }
}
