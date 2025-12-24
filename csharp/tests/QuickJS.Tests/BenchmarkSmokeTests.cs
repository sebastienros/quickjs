// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

public class BenchmarkSmokeTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BenchmarkSmokeTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void QuickJS_Arithmetic()
    {
        AssertNumber("QuickJS_Arithmetic", _context.Evaluate("1 + 2 * 3"), 7);
    }

    [Fact]
    public void QuickJS_Variables()
    {
        AssertNumber("QuickJS_Variables", _context.Evaluate("var x = 42; x + 1"), 43);
    }

    [Fact]
    public void QuickJS_Function()
    {
        AssertNumber("QuickJS_Function", _context.Evaluate("(function(a, b) { return a + b; })(10, 20)"), 30);
    }

    [Fact]
    public void QuickJS_Loop()
    {
        AssertNumber("QuickJS_Loop", _context.Evaluate("var sum = 0; for (var i = 0; i < 100; i++) sum += i; sum"), 4950);
    }

    [Fact]
    public void QuickJS_String()
    {
        AssertString("QuickJS_String", _context.Evaluate("'hello' + ' ' + 'world'"), "hello world");
    }

    [Fact]
    public void QuickJS_Array()
    {
        AssertNumber("QuickJS_Array", _context.Evaluate("[1, 2, 3, 4, 5].map(x => x * 2).reduce((a, b) => a + b, 0)"), 30);
    }

    [Fact]
    public void QuickJS_Object()
    {
        AssertNumber("QuickJS_Object", _context.Evaluate("var obj = { a: 1, b: 2, c: 3 }; obj.a + obj.b + obj.c"), 6);
    }

    [Fact]
    public void QuickJS_Fibonacci()
    {
        var script = @"
            function fib(n) {
                if (n <= 1) return n;
                return fib(n - 1) + fib(n - 2);
            }
            fib(15)
        ";
        AssertNumber("QuickJS_Fibonacci", _context.Evaluate(script), 610);
    }

    [Fact]
    public void QuickJS_HexParsing()
    {
        AssertNumber("QuickJS_HexParsing", _context.Evaluate("0xFF + 0xABCD + 0x123456"), 1237282);
    }

    [Fact]
    public void QuickJS_BinaryOctalParsing()
    {
        AssertNumber("QuickJS_BinaryOctalParsing", _context.Evaluate("0b1010 + 0o777 + 0b11111111"), 776);
    }

    private static void AssertNumber(string name, JSValue value, double expected)
    {
        double actual = JSValueConversion.ToNumber(value);
        if (actual != expected)
        {
            throw new InvalidOperationException($"{name} expected {expected} but got {actual}");
        }
    }

    private static void AssertString(string name, JSValue value, string expected)
    {
        string actual = JSValueConversion.ToString(value);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{name} expected \"{expected}\" but got \"{actual}\"");
        }
    }
}
