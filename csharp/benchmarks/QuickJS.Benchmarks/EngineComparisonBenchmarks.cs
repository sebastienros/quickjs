using BenchmarkDotNet.Attributes;
using Jint.Native;
using Microsoft.VSDiagnostics;

namespace QuickJS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
//[CPUUsageDiagnoser]
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
    public JSValue QuickJS_Arithmetic() => _quickJsContext.Evaluate("1 + 2 * 3");

    [Benchmark]
    public JsValue Jint_Arithmetic() => _jintEngine.Evaluate("1 + 2 * 3");

    // Variable assignment and retrieval
    [Benchmark]
    public object QuickJS_Variables()
    {
        return _quickJsContext.Evaluate("var x = 42; x + 1");
    }

    [Benchmark]
    public JsValue Jint_Variables()
    {
        return _jintEngine.Evaluate("var x = 42; x + 1");
    }

    // Function call
    [Benchmark]
    public JSValue QuickJS_Function()
    {
        return _quickJsContext.Evaluate("(function(a, b) { return a + b; })(10, 20)");
    }

    [Benchmark]
    public JsValue Jint_Function()
    {
        return _jintEngine.Evaluate("(function(a, b) { return a + b; })(10, 20)");
    }

    // Loop
    [Benchmark]
    public JSValue QuickJS_Loop()
    {
        return _quickJsContext.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) sum += i; sum");
    }

    [Benchmark]
    public JsValue Jint_Loop()
    {
        return _jintEngine.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) sum += i; sum");
    }

    // String operations
    [Benchmark]
    public JSValue QuickJS_String()
    {
        return _quickJsContext.Evaluate("'hello' + ' ' + 'world'");
    }

    [Benchmark]
    public JsValue Jint_String()
    {
        return _jintEngine.Evaluate("'hello' + ' ' + 'world'");
    }

    // Array operations
    [Benchmark]
    public JSValue QuickJS_Array()
    {
        return _quickJsContext.Evaluate("[1, 2, 3, 4, 5].map(x => x * 2).reduce((a, b) => a + b, 0)");
    }

    [Benchmark]
    public JsValue Jint_Array()
    {
        return _jintEngine.Evaluate("[1, 2, 3, 4, 5].map(x => x * 2).reduce((a, b) => a + b, 0)");
    }

    // Object creation
    [Benchmark]
    public JSValue QuickJS_Object()
    {
        return _quickJsContext.Evaluate("var obj = { a: 1, b: 2, c: 3 }; obj.a + obj.b + obj.c");
    }

    [Benchmark]
    public JsValue Jint_Object()
    {
        return _jintEngine.Evaluate("var obj = { a: 1, b: 2, c: 3 }; obj.a + obj.b + obj.c");
    }

    // Fibonacci (recursive)
    [Benchmark]
    public JSValue QuickJS_Fibonacci()
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
    public JsValue Jint_Fibonacci()
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
    public JSValue QuickJS_HexParsing()
    {
        return _quickJsContext.Evaluate("0xFF + 0xABCD + 0x123456");
    }

    [Benchmark]
    public JsValue Jint_HexParsing()
    {
        return _jintEngine.Evaluate("0xFF + 0xABCD + 0x123456");
    }

    // Number parsing (binary/octal)
    [Benchmark]
    public JSValue QuickJS_BinaryOctalParsing()
    {
        return _quickJsContext.Evaluate("0b1010 + 0o777 + 0b11111111");
    }

    [Benchmark]
    public JsValue Jint_BinaryOctalParsing()
    {
        return _jintEngine.Evaluate("0b1010 + 0o777 + 0b11111111");
    }
}
