// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Base class for all JavaScript exceptions.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when JavaScript code encounters an error during
/// parsing or execution. It corresponds to the JavaScript Error object and
/// its derived types (TypeError, SyntaxError, etc.).
/// </para>
/// <para>
/// The exception includes:
/// - A message describing the error
/// - The JavaScript error type (Error, TypeError, etc.)
/// - An optional source location where the error occurred
/// - An optional JavaScript stack trace
/// </para>
/// </remarks>
#if NET8_0_OR_GREATER
[Serializable]
#endif
public class JSException : Exception
{
    /// <summary>
    /// Gets the JavaScript error type.
    /// </summary>
    public JSErrorType ErrorType { get; }

    /// <summary>
    /// Gets the source location where the error occurred.
    /// </summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// Gets the JavaScript stack trace, if available.
    /// </summary>
    public JSStackTrace? JSStackTrace { get; }

    /// <summary>
    /// Creates a new JavaScript exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSException(string message)
        : base(message)
    {
        ErrorType = JSErrorType.Error;
        Location = SourceLocation.Empty;
    }

    /// <summary>
    /// Creates a new JavaScript exception with an error type.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The JavaScript error type.</param>
    public JSException(string message, JSErrorType errorType)
        : base(message)
    {
        ErrorType = errorType;
        Location = SourceLocation.Empty;
    }

    /// <summary>
    /// Creates a new JavaScript exception with error type and location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The JavaScript error type.</param>
    /// <param name="location">The source location where the error occurred.</param>
    public JSException(string message, JSErrorType errorType, SourceLocation location)
        : base(message)
    {
        ErrorType = errorType;
        Location = location;
    }

    /// <summary>
    /// Creates a new JavaScript exception with full details.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The JavaScript error type.</param>
    /// <param name="location">The source location where the error occurred.</param>
    /// <param name="stackTrace">The JavaScript stack trace.</param>
    public JSException(string message, JSErrorType errorType, SourceLocation location, JSStackTrace? stackTrace)
        : base(message)
    {
        ErrorType = errorType;
        Location = location;
        JSStackTrace = stackTrace;
    }

    /// <summary>
    /// Creates a new JavaScript exception with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public JSException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = JSErrorType.Error;
        Location = SourceLocation.Empty;
    }

    /// <summary>
    /// Creates a new JavaScript exception with error type and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The JavaScript error type.</param>
    /// <param name="innerException">The inner exception.</param>
    public JSException(string message, JSErrorType errorType, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = errorType;
        Location = SourceLocation.Empty;
    }

    /// <summary>
    /// Gets the JavaScript error name (e.g., "TypeError", "SyntaxError").
    /// </summary>
    public string ErrorName => ErrorType switch
    {
        JSErrorType.Error => "Error",
        JSErrorType.EvalError => "EvalError",
        JSErrorType.RangeError => "RangeError",
        JSErrorType.ReferenceError => "ReferenceError",
        JSErrorType.SyntaxError => "SyntaxError",
        JSErrorType.TypeError => "TypeError",
        JSErrorType.URIError => "URIError",
        JSErrorType.InternalError => "InternalError",
        JSErrorType.AggregateError => "AggregateError",
        _ => "Error",
    };

    /// <summary>
    /// Gets a formatted message in JavaScript style: "ErrorName: message".
    /// </summary>
    public string FormattedMessage => $"{ErrorName}: {Message}";

    /// <inheritdoc />
    public override string ToString()
    {
        var result = FormattedMessage;

        if (!Location.IsEmpty)
        {
            result += $"\n    at {Location}";
        }

        if (JSStackTrace != null && JSStackTrace.Count > 0)
        {
            result += "\n" + JSStackTrace;
        }

        return result;
    }
}

/// <summary>
/// Represents a JavaScript SyntaxError.
/// </summary>
/// <remarks>
/// Thrown when the parser encounters syntactically invalid code.
/// Examples: unterminated string, unexpected token, invalid regular expression.
/// </remarks>
public class JSSyntaxError : JSException
{
    /// <summary>
    /// Creates a new syntax error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSSyntaxError(string message)
        : base(message, JSErrorType.SyntaxError)
    {
    }

    /// <summary>
    /// Creates a new syntax error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSSyntaxError(string message, SourceLocation location)
        : base(message, JSErrorType.SyntaxError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript TypeError.
/// </summary>
/// <remarks>
/// Thrown when a value is not of the expected type.
/// Examples: calling a non-function, accessing property of null/undefined.
/// </remarks>
public class JSTypeError : JSException
{
    /// <summary>
    /// Creates a new type error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSTypeError(string message)
        : base(message, JSErrorType.TypeError)
    {
    }

    /// <summary>
    /// Creates a new type error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSTypeError(string message, SourceLocation location)
        : base(message, JSErrorType.TypeError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript ReferenceError.
/// </summary>
/// <remarks>
/// Thrown when referencing a variable that doesn't exist.
/// Examples: using an undeclared variable, accessing before declaration (TDZ).
/// </remarks>
public class JSReferenceError : JSException
{
    /// <summary>
    /// Creates a new reference error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSReferenceError(string message)
        : base(message, JSErrorType.ReferenceError)
    {
    }

    /// <summary>
    /// Creates a new reference error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSReferenceError(string message, SourceLocation location)
        : base(message, JSErrorType.ReferenceError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript RangeError.
/// </summary>
/// <remarks>
/// Thrown when a numeric value is outside its allowed range.
/// Examples: invalid array length, toFixed() with invalid precision.
/// </remarks>
public class JSRangeError : JSException
{
    /// <summary>
    /// Creates a new range error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSRangeError(string message)
        : base(message, JSErrorType.RangeError)
    {
    }

    /// <summary>
    /// Creates a new range error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSRangeError(string message, SourceLocation location)
        : base(message, JSErrorType.RangeError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript URIError.
/// </summary>
/// <remarks>
/// Thrown when a URI handling function is used incorrectly.
/// Examples: malformed URI in decodeURI(), decodeURIComponent().
/// </remarks>
public class JSURIError : JSException
{
    /// <summary>
    /// Creates a new URI error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSURIError(string message)
        : base(message, JSErrorType.URIError)
    {
    }

    /// <summary>
    /// Creates a new URI error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSURIError(string message, SourceLocation location)
        : base(message, JSErrorType.URIError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript EvalError.
/// </summary>
/// <remarks>
/// Indicates an error related to the eval() function.
/// This error is rarely used in modern JavaScript and is mostly retained
/// for backward compatibility.
/// </remarks>
public class JSEvalError : JSException
{
    /// <summary>
    /// Creates a new eval error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSEvalError(string message)
        : base(message, JSErrorType.EvalError)
    {
    }

    /// <summary>
    /// Creates a new eval error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSEvalError(string message, SourceLocation location)
        : base(message, JSErrorType.EvalError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript InternalError.
/// </summary>
/// <remarks>
/// This is a non-standard error type used by QuickJS for internal engine errors.
/// Examples: too much recursion, out of memory (though OOM may also throw a generic Error).
/// </remarks>
public class JSInternalError : JSException
{
    /// <summary>
    /// Creates a new internal error.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JSInternalError(string message)
        : base(message, JSErrorType.InternalError)
    {
    }

    /// <summary>
    /// Creates a new internal error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    public JSInternalError(string message, SourceLocation location)
        : base(message, JSErrorType.InternalError, location)
    {
    }
}

/// <summary>
/// Represents a JavaScript AggregateError (ES2021).
/// </summary>
/// <remarks>
/// Wraps multiple errors into a single error. Typically used by Promise.any()
/// when all promises reject.
/// </remarks>
public class JSAggregateError : JSException
{
    private readonly JSException[] _errors;

    /// <summary>
    /// Gets the aggregated errors.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<JSException> Errors => _errors;

    /// <summary>
    /// Creates a new aggregate error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errors">The aggregated errors.</param>
    public JSAggregateError(string message, params JSException[] errors)
        : base(message, JSErrorType.AggregateError)
    {
        _errors = errors ?? Array.Empty<JSException>();
    }

    /// <summary>
    /// Creates a new aggregate error with location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="location">The source location.</param>
    /// <param name="errors">The aggregated errors.</param>
    public JSAggregateError(string message, SourceLocation location, params JSException[] errors)
        : base(message, JSErrorType.AggregateError, location)
    {
        _errors = errors ?? Array.Empty<JSException>();
    }
}
