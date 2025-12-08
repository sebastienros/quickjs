// Licensed under the MIT License.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Unit tests for JavaScript exception classes.
/// </summary>
public class JSExceptionTests
{
    #region SourceLocation Tests

    [Fact]
    public void SourceLocation_Default_IsEmpty()
    {
        var location = SourceLocation.Empty;
        Assert.True(location.IsEmpty);
        Assert.Equal("<unknown>", location.ToString());
    }

    [Fact]
    public void SourceLocation_WithAllValues_FormatsCorrectly()
    {
        var location = new SourceLocation("test.js", 10, 5);
        Assert.Equal("test.js", location.FileName);
        Assert.Equal(10, location.Line);
        Assert.Equal(5, location.Column);
        Assert.False(location.IsEmpty);
        Assert.Equal("test.js:10:5", location.ToString());
    }

    [Fact]
    public void SourceLocation_WithLineOnly_FormatsWithoutColumn()
    {
        var location = new SourceLocation("test.js", 10, 0);
        Assert.Equal("test.js:10", location.ToString());
    }

    [Fact]
    public void SourceLocation_WithFileOnly_FormatsFileOnly()
    {
        var location = new SourceLocation("test.js", 0, 0);
        Assert.Equal("test.js", location.ToString());
    }

    [Fact]
    public void SourceLocation_Equality_SameValues_AreEqual()
    {
        var a = new SourceLocation("test.js", 10, 5);
        var b = new SourceLocation("test.js", 10, 5);
        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void SourceLocation_Equality_DifferentValues_AreNotEqual()
    {
        var a = new SourceLocation("test.js", 10, 5);
        var b = new SourceLocation("test.js", 11, 5);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void SourceLocation_NullFileName_TreatedAsEmpty()
    {
        var location = new SourceLocation(null!, 10, 5);
        Assert.Equal(string.Empty, location.FileName);
    }

    #endregion

    #region JSStackFrame Tests

    [Fact]
    public void JSStackFrame_WithFunctionName_FormatsCorrectly()
    {
        var frame = new JSStackFrame("myFunction", new SourceLocation("test.js", 10, 5));
        Assert.Equal("myFunction", frame.FunctionName);
        Assert.Contains("myFunction", frame.ToString());
        Assert.Contains("test.js:10:5", frame.ToString());
    }

    [Fact]
    public void JSStackFrame_Anonymous_ShowsAnonymous()
    {
        var frame = new JSStackFrame(null, new SourceLocation("test.js", 10, 5));
        Assert.Contains("<anonymous>", frame.ToString());
    }

    [Fact]
    public void JSStackFrame_Native_ShowsNativeCode()
    {
        var frame = new JSStackFrame("Array.prototype.map", SourceLocation.Empty, isNative: true);
        Assert.Contains("[native code]", frame.ToString());
    }

    #endregion

    #region JSStackTrace Tests

    [Fact]
    public void JSStackTrace_Empty_HasNoFrames()
    {
        var trace = JSStackTrace.Empty;
        Assert.Equal(0, trace.Count);
        Assert.Equal(string.Empty, trace.ToString());
    }

    [Fact]
    public void JSStackTrace_WithFrames_FormatsCorrectly()
    {
        var trace = new JSStackTrace(new[]
        {
            new JSStackFrame("inner", new SourceLocation("test.js", 5, 10)),
            new JSStackFrame("outer", new SourceLocation("test.js", 10, 5)),
        });

        Assert.Equal(2, trace.Count);
        var output = trace.ToString();
        Assert.Contains("inner", output);
        Assert.Contains("outer", output);
    }

    #endregion

    #region JSErrorType Tests

    [Theory]
    [InlineData(JSErrorType.Error, "Error")]
    [InlineData(JSErrorType.TypeError, "TypeError")]
    [InlineData(JSErrorType.SyntaxError, "SyntaxError")]
    [InlineData(JSErrorType.ReferenceError, "ReferenceError")]
    [InlineData(JSErrorType.RangeError, "RangeError")]
    [InlineData(JSErrorType.URIError, "URIError")]
    [InlineData(JSErrorType.EvalError, "EvalError")]
    [InlineData(JSErrorType.InternalError, "InternalError")]
    [InlineData(JSErrorType.AggregateError, "AggregateError")]
    public void JSException_ErrorName_MatchesType(JSErrorType errorType, string expectedName)
    {
        var ex = new JSException("test", errorType);
        Assert.Equal(expectedName, ex.ErrorName);
    }

    #endregion

    #region JSException Base Class Tests

    [Fact]
    public void JSException_BasicConstructor_SetsMessage()
    {
        var ex = new JSException("Something went wrong");
        Assert.Equal("Something went wrong", ex.Message);
        Assert.Equal(JSErrorType.Error, ex.ErrorType);
        Assert.True(ex.Location.IsEmpty);
    }

    [Fact]
    public void JSException_WithErrorType_SetsType()
    {
        var ex = new JSException("Type mismatch", JSErrorType.TypeError);
        Assert.Equal(JSErrorType.TypeError, ex.ErrorType);
        Assert.Equal("TypeError: Type mismatch", ex.FormattedMessage);
    }

    [Fact]
    public void JSException_WithLocation_IncludesInToString()
    {
        var location = new SourceLocation("script.js", 42, 10);
        var ex = new JSException("Error occurred", JSErrorType.Error, location);
        
        var result = ex.ToString();
        Assert.Contains("Error: Error occurred", result);
        Assert.Contains("script.js:42:10", result);
    }

    [Fact]
    public void JSException_WithStackTrace_IncludesInToString()
    {
        var trace = new JSStackTrace(new[]
        {
            new JSStackFrame("foo", new SourceLocation("test.js", 5, 1)),
        });
        var ex = new JSException("Oops", JSErrorType.Error, SourceLocation.Empty, trace);
        
        var result = ex.ToString();
        Assert.Contains("foo", result);
        Assert.Contains("test.js:5:1", result);
    }

    [Fact]
    public void JSException_WithInnerException_PreservesChain()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new JSException("Outer error", inner);
        
        Assert.Same(inner, ex.InnerException);
    }

    #endregion

    #region Derived Exception Tests

    [Fact]
    public void JSSyntaxError_HasCorrectType()
    {
        var ex = new JSSyntaxError("Unexpected token");
        Assert.Equal(JSErrorType.SyntaxError, ex.ErrorType);
        Assert.Equal("SyntaxError", ex.ErrorName);
        Assert.Equal("SyntaxError: Unexpected token", ex.FormattedMessage);
    }

    [Fact]
    public void JSSyntaxError_WithLocation_IncludesLocation()
    {
        var location = new SourceLocation("module.js", 100, 15);
        var ex = new JSSyntaxError("Unterminated string literal", location);
        
        Assert.Equal(location, ex.Location);
        Assert.Contains("module.js:100:15", ex.ToString());
    }

    [Fact]
    public void JSTypeError_HasCorrectType()
    {
        var ex = new JSTypeError("undefined is not a function");
        Assert.Equal(JSErrorType.TypeError, ex.ErrorType);
        Assert.Equal("TypeError: undefined is not a function", ex.FormattedMessage);
    }

    [Fact]
    public void JSReferenceError_HasCorrectType()
    {
        var ex = new JSReferenceError("x is not defined");
        Assert.Equal(JSErrorType.ReferenceError, ex.ErrorType);
        Assert.Equal("ReferenceError: x is not defined", ex.FormattedMessage);
    }

    [Fact]
    public void JSRangeError_HasCorrectType()
    {
        var ex = new JSRangeError("Invalid array length");
        Assert.Equal(JSErrorType.RangeError, ex.ErrorType);
        Assert.Equal("RangeError: Invalid array length", ex.FormattedMessage);
    }

    [Fact]
    public void JSURIError_HasCorrectType()
    {
        var ex = new JSURIError("malformed URI");
        Assert.Equal(JSErrorType.URIError, ex.ErrorType);
        Assert.Equal("URIError: malformed URI", ex.FormattedMessage);
    }

    [Fact]
    public void JSEvalError_HasCorrectType()
    {
        var ex = new JSEvalError("eval error");
        Assert.Equal(JSErrorType.EvalError, ex.ErrorType);
        Assert.Equal("EvalError: eval error", ex.FormattedMessage);
    }

    [Fact]
    public void JSInternalError_HasCorrectType()
    {
        var ex = new JSInternalError("too much recursion");
        Assert.Equal(JSErrorType.InternalError, ex.ErrorType);
        Assert.Equal("InternalError: too much recursion", ex.FormattedMessage);
    }

    [Fact]
    public void JSAggregateError_HasCorrectType()
    {
        var ex = new JSAggregateError("All promises rejected");
        Assert.Equal(JSErrorType.AggregateError, ex.ErrorType);
        Assert.Equal("AggregateError: All promises rejected", ex.FormattedMessage);
    }

    [Fact]
    public void JSAggregateError_ContainsNestedErrors()
    {
        var error1 = new JSTypeError("Error 1");
        var error2 = new JSReferenceError("Error 2");
        var aggregate = new JSAggregateError("Multiple errors", error1, error2);
        
        Assert.Equal(2, aggregate.Errors.Count);
        Assert.Same(error1, aggregate.Errors[0]);
        Assert.Same(error2, aggregate.Errors[1]);
    }

    [Fact]
    public void JSAggregateError_WithNoErrors_HasEmptyCollection()
    {
        var aggregate = new JSAggregateError("No errors");
        Assert.Empty(aggregate.Errors);
    }

    #endregion

    #region Exception Inheritance Tests

    [Fact]
    public void AllJSExceptions_AreDerivedException()
    {
        Assert.IsAssignableFrom<Exception>(new JSException("test"));
        Assert.IsAssignableFrom<Exception>(new JSSyntaxError("test"));
        Assert.IsAssignableFrom<Exception>(new JSTypeError("test"));
        Assert.IsAssignableFrom<Exception>(new JSReferenceError("test"));
        Assert.IsAssignableFrom<Exception>(new JSRangeError("test"));
        Assert.IsAssignableFrom<Exception>(new JSURIError("test"));
        Assert.IsAssignableFrom<Exception>(new JSEvalError("test"));
        Assert.IsAssignableFrom<Exception>(new JSInternalError("test"));
        Assert.IsAssignableFrom<Exception>(new JSAggregateError("test"));
    }

    [Fact]
    public void AllDerivedExceptions_AreJSException()
    {
        Assert.IsAssignableFrom<JSException>(new JSSyntaxError("test"));
        Assert.IsAssignableFrom<JSException>(new JSTypeError("test"));
        Assert.IsAssignableFrom<JSException>(new JSReferenceError("test"));
        Assert.IsAssignableFrom<JSException>(new JSRangeError("test"));
        Assert.IsAssignableFrom<JSException>(new JSURIError("test"));
        Assert.IsAssignableFrom<JSException>(new JSEvalError("test"));
        Assert.IsAssignableFrom<JSException>(new JSInternalError("test"));
        Assert.IsAssignableFrom<JSException>(new JSAggregateError("test"));
    }

    [Fact]
    public void JSException_CanBeCaughtAsException()
    {
        try
        {
            throw new JSTypeError("test error");
        }
        catch (Exception ex)
        {
            Assert.IsType<JSTypeError>(ex);
            Assert.Equal("test error", ex.Message);
        }
    }

    [Fact]
    public void JSException_CanBeCaughtAsJSException()
    {
        try
        {
            throw new JSSyntaxError("syntax error");
        }
        catch (JSException ex)
        {
            Assert.Equal(JSErrorType.SyntaxError, ex.ErrorType);
        }
    }

    #endregion

    #region Realistic Usage Scenarios

    [Fact]
    public void JSException_RealisticSyntaxError()
    {
        // Simulating a parser error
        var location = new SourceLocation("app.js", 42, 15);
        var ex = new JSSyntaxError("Unexpected token ')'", location);
        
        var output = ex.ToString();
        Assert.Contains("SyntaxError: Unexpected token ')'", output);
        Assert.Contains("app.js:42:15", output);
    }

    [Fact]
    public void JSException_RealisticRuntimeError()
    {
        // Simulating a runtime TypeError with stack trace
        var trace = new JSStackTrace(new[]
        {
            new JSStackFrame("processData", new SourceLocation("utils.js", 25, 10)),
            new JSStackFrame("handleClick", new SourceLocation("app.js", 100, 5)),
            new JSStackFrame(null, new SourceLocation("app.js", 150, 1)), // anonymous
        });
        
        var ex = new JSException(
            "Cannot read property 'length' of undefined",
            JSErrorType.TypeError,
            new SourceLocation("utils.js", 25, 10),
            trace
        );
        
        var output = ex.ToString();
        Assert.Contains("TypeError:", output);
        Assert.Contains("processData", output);
        Assert.Contains("handleClick", output);
        Assert.Contains("<anonymous>", output);
    }

    [Fact]
    public void JSException_PromiseAllRejectionScenario()
    {
        // Simulating Promise.any() with all rejections
        var errors = new[]
        {
            new JSTypeError("Network error"),
            new JSReferenceError("API not available"),
            new JSException("Timeout", JSErrorType.Error),
        };
        
        var aggregate = new JSAggregateError("All promises were rejected", errors);
        
        Assert.Equal(3, aggregate.Errors.Count);
        Assert.Contains("All promises were rejected", aggregate.FormattedMessage);
    }

    #endregion
}
