using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace QuickJS.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EngineComparisonBenchmarks
{
    private JSRuntime _quickJsRuntime = null!;
    private JSContext _quickJsContext = null!;
    private Jint.Engine _jintEngine = null!;

    [GlobalSetup]
    public void Setup()
    {
        _quickJsRuntime = new JSRuntime();
        _quickJsContext = _quickJsRuntime.CreateContext();
        _jintEngine = new Jint.Engine();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _quickJsContext?.Dispose();
        _quickJsRuntime?.Dispose();
    }

    // Simple arithmetic
    [Benchmark]
    public object QuickJS_Arithmetic() => _quickJsContext.Evaluate("1 + 2 * 3");

    [Benchmark]
    public object Jint_Arithmetic() => _jintEngine.Evaluate("1 + 2 * 3");

    // Variable assignment and retrieval
    [Benchmark]
    public object QuickJS_Variables()
    {
        return _quickJsContext.Evaluate("var x = 42; x + 1");
    }

    [Benchmark]
    public object Jint_Variables()
    {
        return _jintEngine.Evaluate("var x = 42; x + 1");
    }

    // Function call
    [Benchmark]
    public object QuickJS_Function()
    {
        return _quickJsContext.Evaluate("(function(a, b) { return a + b; })(10, 20)");
    }

    [Benchmark]
    public object Jint_Function()
    {
        return _jintEngine.Evaluate("(function(a, b) { return a + b; })(10, 20)");
    }

    // Loop
    [Benchmark]
    public object QuickJS_Loop()
    {
        return _quickJsContext.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) sum += i; sum");
    }

    [Benchmark]
    public object Jint_Loop()
    {
        return _jintEngine.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) sum += i; sum");
    }

    // String operations
    [Benchmark]
    public object QuickJS_String()
    {
        return _quickJsContext.Evaluate("'hello' + ' ' + 'world'");
    }

    [Benchmark]
    public object Jint_String()
    {
        return _jintEngine.Evaluate("'hello' + ' ' + 'world'");
    }

    // Array operations
    [Benchmark]
    public object QuickJS_Array()
    {
        return _quickJsContext.Evaluate("[1, 2, 3, 4, 5].map(x => x * 2).reduce((a, b) => a + b, 0)");
    }

    [Benchmark]
    public object Jint_Array()
    {
        return _jintEngine.Evaluate("[1, 2, 3, 4, 5].map(x => x * 2).reduce((a, b) => a + b, 0)");
    }

    // Object creation
    [Benchmark]
    public object QuickJS_Object()
    {
        return _quickJsContext.Evaluate("var obj = { a: 1, b: 2, c: 3 }; obj.a + obj.b + obj.c");
    }

    [Benchmark]
    public object Jint_Object()
    {
        return _jintEngine.Evaluate("var obj = { a: 1, b: 2, c: 3 }; obj.a + obj.b + obj.c");
    }

    // Fibonacci (recursive)
    [Benchmark]
    public object QuickJS_Fibonacci()
    {
        return _quickJsContext.Evaluate(@"
            function fib(n) {
                if (n <= 1) return n;
                return fib(n - 1) + fib(n - 2);
            }
            fib(15)
        ");
    }

    [Benchmark]
    public object Jint_Fibonacci()
    {
        return _jintEngine.Evaluate(@"
            function fib(n) {
                if (n <= 1) return n;
                return fib(n - 1) + fib(n - 2);
            }
            fib(15)
        ");
    }

    // Number parsing (hex)
    [Benchmark]
    public object QuickJS_HexParsing()
    {
        return _quickJsContext.Evaluate("0xFF + 0xABCD + 0x123456");
    }

    [Benchmark]
    public object Jint_HexParsing()
    {
        return _jintEngine.Evaluate("0xFF + 0xABCD + 0x123456");
    }

    // Number parsing (binary/octal)
    [Benchmark]
    public object QuickJS_BinaryOctalParsing()
    {
        return _quickJsContext.Evaluate("0b1010 + 0o777 + 0b11111111");
    }

    [Benchmark]
    public object Jint_BinaryOctalParsing()
    {
        return _jintEngine.Evaluate("0b1010 + 0o777 + 0b11111111");
    }
}
