// Licensed under the MIT License.

using System;

namespace QuickJS;

/// <summary>
/// Represents the type of a JavaScript error.
/// </summary>
/// <remarks>
/// These correspond to the native error constructors in JavaScript:
/// - Error (base type)
/// - EvalError
/// - RangeError
/// - ReferenceError
/// - SyntaxError
/// - TypeError
/// - URIError
/// - AggregateError (ES2021)
/// - InternalError (non-standard, used by QuickJS)
/// </remarks>
public enum JSErrorType
{
    /// <summary>
    /// Generic Error - base type for all JavaScript errors.
    /// </summary>
    Error = 0,

    /// <summary>
    /// EvalError - error related to the eval() function.
    /// Rarely used in modern JavaScript.
    /// </summary>
    EvalError = 1,

    /// <summary>
    /// RangeError - a numeric variable or parameter is outside its valid range.
    /// Examples: Array index out of bounds, invalid array length.
    /// </summary>
    RangeError = 2,

    /// <summary>
    /// ReferenceError - an invalid reference is used.
    /// Examples: Using an undeclared variable, accessing a variable before it's declared.
    /// </summary>
    ReferenceError = 3,

    /// <summary>
    /// SyntaxError - a syntax error in parsed code.
    /// Examples: Unterminated string literal, unexpected token.
    /// </summary>
    SyntaxError = 4,

    /// <summary>
    /// TypeError - a variable or parameter is not of a valid type.
    /// Examples: Calling a non-function, accessing property of null/undefined.
    /// </summary>
    TypeError = 5,

    /// <summary>
    /// URIError - a URI handling function was used incorrectly.
    /// Examples: malformed URI passed to decodeURI().
    /// </summary>
    URIError = 6,

    /// <summary>
    /// InternalError - an internal error in the JavaScript engine.
    /// This is non-standard but used by QuickJS for internal errors
    /// like too much recursion.
    /// </summary>
    InternalError = 7,

    /// <summary>
    /// AggregateError - wraps multiple errors into one (ES2021).
    /// Used by Promise.any() when all promises reject.
    /// </summary>
    AggregateError = 8,
}
