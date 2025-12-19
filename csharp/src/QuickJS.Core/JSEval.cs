// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Provides JavaScript code evaluation capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the JavaScript <c>eval()</c> function and the
/// <c>Function</c> constructor, which both compile and execute JavaScript
/// code from strings at runtime.
/// </para>
/// <para>
/// From QuickJS (quickjs.c):
/// </para>
/// <code>
/// JSValue JS_EvalThis(JSContext *ctx, JSValueConst this_obj,
///                     const char *input, size_t input_len,
///                     const char *filename, int flags);
/// </code>
/// <para>
/// <b>Security Considerations:</b> The <c>eval()</c> function and <c>Function</c>
/// constructor can execute arbitrary code, which poses security risks when
/// evaluating untrusted input. Consider using safer alternatives when possible.
/// </para>
/// </remarks>
public static class JSEval
{
    #region Eval Flags

    /// <summary>
    /// Flags controlling JavaScript evaluation behavior.
    /// </summary>
    [Flags]
    public enum EvalFlags
    {
        /// <summary>Global code (script mode).</summary>
        Global = 0,

        /// <summary>Module code (ES module mode).</summary>
        Module = 1 << 0,

        /// <summary>Direct eval (has access to local scope).</summary>
        Direct = 1 << 1,

        /// <summary>Indirect eval (global scope only).</summary>
        Indirect = 1 << 2,

        /// <summary>Strict mode evaluation.</summary>
        Strict = 1 << 3,

        /// <summary>Strip debug information from compiled code.</summary>
        Strip = 1 << 4,

        /// <summary>Compile only (don't execute).</summary>
        CompileOnly = 1 << 5,

        /// <summary>Force strict mode even without "use strict".</summary>
        ForceStrict = 1 << 6,
    }

    #endregion

    #region Compilation

    /// <summary>
    /// Compiles JavaScript source code into a function definition.
    /// </summary>
    /// <param name="runtime">The JavaScript runtime.</param>
    /// <param name="source">The JavaScript source code.</param>
    /// <param name="fileName">The file name for error reporting.</param>
    /// <param name="isModule">True if parsing as ES module, false for script mode.</param>
    /// <param name="diagnostics">Parse diagnostics produced during compilation.</param>
    /// <returns>The compiled function definition, or null if compilation fails.</returns>
    /// <remarks>
    /// The returned function definition represents the top-level script or module.
    /// It can be executed using an interpreter.
    /// </remarks>
    public static JSFunctionDef? Compile(JSRuntime runtime, string source, string fileName, bool isModule, out DiagnosticBag diagnostics)
    {
        if (runtime == null)
            throw new ArgumentNullException(nameof(runtime));
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        try
        {
            var parser = new Parser(source, fileName, runtime.AtomTable, isModule);
            parser.ParseProgram();

            diagnostics = parser.Diagnostics;

            if (diagnostics.HasErrors)
                return null;

            return parser.CurrentFunction;
        }
        catch (Exception ex) when (ex is not JSException)
        {
            diagnostics = new DiagnosticBag();
            diagnostics.AddError("E0000", ex.Message, new SourceLocation(fileName, 1, 1), fileName);
            return null;
        }
    }

    /// <summary>
    /// Compiles JavaScript source code into a function definition.
    /// </summary>
    public static JSFunctionDef? Compile(JSRuntime runtime, string source, string fileName = "<eval>", bool isModule = false)
    {
        return Compile(runtime, source, fileName, isModule, out _);
    }

    /// <summary>
    /// Compiles JavaScript source code into a function definition.
    /// </summary>
    /// <remarks>
    /// This overload exists for backward compatibility. Prefer compiling against a <see cref="JSRuntime"/>
    /// (and using a <see cref="JSContext"/> only for execution).
    /// </remarks>
    [Obsolete("Compile should not depend on JSContext. Use JSEval.Compile(JSRuntime, ...) and execute with JSContext.Execute(...).")]
    public static JSFunctionDef? Compile(JSContext context, string source, string fileName = "<eval>", bool isModule = false)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var fn = Compile(context.Runtime, source, fileName, isModule, out var diagnostics);
        if (fn != null)
            return fn;

        ThrowSyntaxErrorFromDiagnostics(context, diagnostics);
        return null;
    }

    private static void ThrowSyntaxErrorFromDiagnostics(JSContext context, DiagnosticBag diagnostics)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (diagnostics != null)
        {
            foreach (var error in diagnostics.GetErrors())
            {
                context.ThrowError(JSErrorType.SyntaxError, error.Message);
                return;
            }
        }

        context.ThrowError(JSErrorType.SyntaxError, "Compilation failed");
    }

    /// <summary>
    /// Compiles and executes JavaScript source code.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="source">The JavaScript source code.</param>
    /// <param name="fileName">The file name for error reporting.</param>
    /// <param name="flags">Evaluation flags.</param>
    /// <returns>The result of evaluation, or JSValue.Exception on error.</returns>
    public static JSValue Evaluate(JSContext context, string source, string fileName = "<eval>", EvalFlags flags = EvalFlags.Global)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        // Empty source returns undefined
        if (string.IsNullOrWhiteSpace(source))
            return JSValue.Undefined;

        bool isModule = (flags & EvalFlags.Module) != 0;

        // Compile the source
        var functionDef = Compile(context.Runtime, source, fileName, isModule, out var diagnostics);
        if (functionDef == null)
        {
            ThrowSyntaxErrorFromDiagnostics(context, diagnostics);
            return JSValue.Exception;
        }

        // If compile-only flag is set, return the compiled function
        if ((flags & EvalFlags.CompileOnly) != 0)
        {
            var func = new JSFunction(functionDef);
            return JSValue.FromObject(func);
        }

        // Execute the compiled code
        return context.Execute(functionDef);
    }

    #endregion

    #region Global Eval Function

    /// <summary>
    /// Creates the global eval function.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <returns>The eval function.</returns>
    public static JSFunction CreateEvalFunction(JSContext context)
    {
        var functionProto = context.GetClassPrototype(JSClassId.CFunction);

        JSValue EvalImpl(JSValue thisArg, JSValue[] args)
        {
            // eval() with no arguments returns undefined
            if (args.Length == 0)
                return JSValue.Undefined;

            var arg = args[0];

            // If the argument is not a string, return it unchanged
            // This is per ECMA-262 spec: eval(x) returns x if x is not a string
            if (!arg.IsString)
                return arg;

            var source = arg.ToString() ?? "";
            return Evaluate(context, source, "<eval>", EvalFlags.Indirect);
        }

        return new JSFunction(EvalImpl, "eval", 1, functionProto);
    }

    #endregion

    #region Function Constructor

    /// <summary>
    /// Creates a function from string arguments (implements new Function(...)).
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <param name="args">The arguments: param names followed by body.</param>
    /// <returns>The created function, or JSValue.Exception on error.</returns>
    /// <remarks>
    /// <para>
    /// The Function constructor creates a new Function object. Calling the
    /// constructor directly can create functions dynamically.
    /// </para>
    /// <code>
    /// // Creates function: function(a, b) { return a + b; }
    /// new Function('a', 'b', 'return a + b');
    /// 
    /// // With no arguments: function() { }
    /// new Function();
    /// 
    /// // With only body: function() { return 42; }
    /// new Function('return 42');
    /// </code>
    /// <para>
    /// The created function does not have access to local scope - it's
    /// similar to an indirect eval in that it runs in global scope.
    /// </para>
    /// </remarks>
    public static JSValue CreateFunctionFromStrings(JSContext context, JSValue[] args)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        // Build the function source
        var paramList = new List<string>();
        string body = "";

        if (args.Length == 0)
        {
            // new Function() - empty function
            body = "";
        }
        else if (args.Length == 1)
        {
            // new Function(body) - no parameters
            body = JSValueConversion.ToString(args[0]);
        }
        else
        {
            // new Function(p1, p2, ..., body)
            // All but last argument are parameter names
            for (int i = 0; i < args.Length - 1; i++)
            {
                var paramStr = JSValueConversion.ToString(args[i]);
                // Parameters can be comma-separated in a single string
                var parts = paramStr.Split(',');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        // Validate parameter name (basic check)
                        if (!IsValidIdentifier(trimmed))
                        {
                            context.ThrowError(JSErrorType.SyntaxError, $"Invalid parameter name: {trimmed}");
                            return JSValue.Exception;
                        }
                        paramList.Add(trimmed);
                    }
                }
            }
            body = JSValueConversion.ToString(args[args.Length - 1]);
        }

        // Build the complete function source
        var paramString = string.Join(", ", paramList);
        var functionSource = $"(function anonymous({paramString}) {{\n{body}\n}})";

        // Compile the function
        var functionDef = Compile(context.Runtime, functionSource, "anonymous", isModule: false, out var diagnostics);
        if (functionDef == null)
        {
            ThrowSyntaxErrorFromDiagnostics(context, diagnostics);
            return JSValue.Exception;
        }

        // The compiled function def is the top-level script
        // We need to execute it to get the function value
        var result = context.Execute(functionDef);

        if (context.HasException)
            return JSValue.Exception;

        return result;
    }

    /// <summary>
    /// Creates the Function constructor function.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    /// <returns>The Function constructor.</returns>
    public static JSFunction CreateFunctionConstructor(JSContext context)
    {
        var functionProto = context.GetClassPrototype(JSClassId.CFunction)!;

        JSValue FunctionCtor(JSValue thisArg, JSValue[] args)
        {
            return CreateFunctionFromStrings(context, args);
        }

        var ctor = new JSFunction(FunctionCtor, "Function", 1, functionProto);

        // Function.prototype
        ctor.Set("prototype", JSValue.FromObject(functionProto));

        // Function.prototype.constructor = Function
        functionProto.Set("constructor", JSValue.FromObject(ctor));

        return ctor;
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Validates that a string is a valid JavaScript identifier.
    /// </summary>
    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // First character must be letter, underscore, or dollar sign
        var first = name[0];
        if (!char.IsLetter(first) && first != '_' && first != '$')
            return false;

        // Rest can include digits
        for (int i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '$')
                return false;
        }

        // Check for reserved words
        return !IsReservedWord(name);
    }

    /// <summary>
    /// Checks if a name is a JavaScript reserved word.
    /// </summary>
    private static bool IsReservedWord(string name)
    {
        return name switch
        {
            // Keywords
            "break" or "case" or "catch" or "continue" or "debugger" or
            "default" or "delete" or "do" or "else" or "finally" or
            "for" or "function" or "if" or "in" or "instanceof" or
            "new" or "return" or "switch" or "this" or "throw" or
            "try" or "typeof" or "var" or "void" or "while" or "with" => true,

            // Future reserved words
            "class" or "const" or "enum" or "export" or "extends" or
            "import" or "super" => true,

            // Strict mode reserved words
            "implements" or "interface" or "let" or "package" or
            "private" or "protected" or "public" or "static" or "yield" => true,

            // Literals
            "null" or "true" or "false" => true,

            _ => false
        };
    }

    #endregion
}
