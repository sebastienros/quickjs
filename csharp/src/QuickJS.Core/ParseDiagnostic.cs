// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace QuickJS;

/// <summary>
/// Represents the severity of a parse diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// An informational message that doesn't prevent parsing.
    /// </summary>
    Info,

    /// <summary>
    /// A warning that indicates potential issues but doesn't prevent parsing.
    /// </summary>
    Warning,

    /// <summary>
    /// An error that prevents successful parsing.
    /// </summary>
    Error
}

/// <summary>
/// Represents a diagnostic message from the parser.
/// </summary>
/// <remarks>
/// Diagnostics provide detailed information about parsing issues including:
/// - Error severity (error, warning, info)
/// - Location in source code (line, column)
/// - A unique error code for categorization
/// - A descriptive message
/// - Optional source context (code snippet)
/// </remarks>
public sealed class ParseDiagnostic
{
    /// <summary>
    /// Gets the severity of this diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the error code (e.g., "JS1001").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the source location where the diagnostic occurred.
    /// </summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// Gets the source file name, if available.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the source context (code snippet) around the error, if available.
    /// </summary>
    public string? SourceContext { get; }

    /// <summary>
    /// Gets the start position in the source (0-based character offset).
    /// </summary>
    public int StartPosition { get; }

    /// <summary>
    /// Gets the end position in the source (0-based character offset).
    /// </summary>
    public int EndPosition { get; }

    /// <summary>
    /// Creates a new parse diagnostic.
    /// </summary>
    public ParseDiagnostic(
        DiagnosticSeverity severity,
        string code,
        string message,
        SourceLocation location,
        string? fileName = null,
        string? sourceContext = null,
        int startPosition = 0,
        int endPosition = 0)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Location = location;
        FileName = fileName;
        SourceContext = sourceContext;
        StartPosition = startPosition;
        EndPosition = endPosition;
    }

    /// <summary>
    /// Creates an error diagnostic.
    /// </summary>
    public static ParseDiagnostic Error(string code, string message, SourceLocation location, string? fileName = null)
    {
        return new ParseDiagnostic(DiagnosticSeverity.Error, code, message, location, fileName);
    }

    /// <summary>
    /// Creates a warning diagnostic.
    /// </summary>
    public static ParseDiagnostic Warning(string code, string message, SourceLocation location, string? fileName = null)
    {
        return new ParseDiagnostic(DiagnosticSeverity.Warning, code, message, location, fileName);
    }

    /// <summary>
    /// Creates an info diagnostic.
    /// </summary>
    public static ParseDiagnostic Info(string code, string message, SourceLocation location, string? fileName = null)
    {
        return new ParseDiagnostic(DiagnosticSeverity.Info, code, message, location, fileName);
    }

    /// <summary>
    /// Returns a formatted string representation of this diagnostic.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        // Format: filename(line,col): severity code: message
        if (!string.IsNullOrEmpty(FileName))
        {
            sb.Append(FileName);
        }
        else
        {
            sb.Append("<source>");
        }

        sb.Append('(');
        sb.Append(Location.Line);
        sb.Append(',');
        sb.Append(Location.Column);
        sb.Append("): ");

        sb.Append(Severity.ToString().ToLowerInvariant());
        sb.Append(' ');
        sb.Append(Code);
        sb.Append(": ");
        sb.Append(Message);

        return sb.ToString();
    }

    /// <summary>
    /// Returns a detailed string representation including source context.
    /// </summary>
    public string ToDetailedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(ToString());

        if (!string.IsNullOrEmpty(SourceContext))
        {
            sb.AppendLine();
            sb.AppendLine(SourceContext);

            // Add caret pointing to error location
            if (Location.Column > 0)
            {
                sb.Append(new string(' ', Location.Column - 1));
                sb.Append('^');
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Known error codes for JavaScript parsing.
/// </summary>
public static class ParseErrorCode
{
    #region Lexer errors (1xxx)

    /// <summary>Unterminated string literal.</summary>
    public const string UnterminatedString = "JS1001";

    /// <summary>Unterminated multi-line comment.</summary>
    public const string UnterminatedComment = "JS1002";

    /// <summary>Unterminated regular expression literal.</summary>
    public const string UnterminatedRegExp = "JS1003";

    /// <summary>Unterminated template literal.</summary>
    public const string UnterminatedTemplateLiteral = "JS1004";

    /// <summary>Invalid numeric literal.</summary>
    public const string InvalidNumber = "JS1005";

    /// <summary>Invalid escape sequence in string.</summary>
    public const string InvalidEscapeSequence = "JS1006";

    /// <summary>Invalid Unicode escape sequence.</summary>
    public const string InvalidUnicodeEscape = "JS1007";

    /// <summary>Invalid or unexpected character.</summary>
    public const string InvalidCharacter = "JS1008";

    /// <summary>Octal escape sequences are not allowed in strict mode.</summary>
    public const string OctalEscapeInStrictMode = "JS1009";

    /// <summary>Octal literals are not allowed in strict mode.</summary>
    public const string OctalLiteralInStrictMode = "JS1010";

    #endregion

    #region Syntax errors (2xxx)

    /// <summary>Unexpected token encountered.</summary>
    public const string UnexpectedToken = "JS2001";

    /// <summary>Unexpected end of input.</summary>
    public const string UnexpectedEndOfInput = "JS2002";

    /// <summary>Expected a specific token.</summary>
    public const string ExpectedToken = "JS2003";

    /// <summary>Expected an identifier.</summary>
    public const string ExpectedIdentifier = "JS2004";

    /// <summary>Expected an expression.</summary>
    public const string ExpectedExpression = "JS2005";

    /// <summary>Expected a statement.</summary>
    public const string ExpectedStatement = "JS2006";

    /// <summary>Invalid left-hand side in assignment.</summary>
    public const string InvalidLeftHandSide = "JS2007";

    /// <summary>Duplicate parameter name.</summary>
    public const string DuplicateParameter = "JS2008";

    /// <summary>Duplicate property name in object literal.</summary>
    public const string DuplicateProperty = "JS2009";

    /// <summary>Use of reserved word in strict mode.</summary>
    public const string StrictModeReservedWord = "JS2010";

    /// <summary>Invalid assignment target.</summary>
    public const string InvalidAssignmentTarget = "JS2011";

    #endregion

    #region Declaration errors (3xxx)

    /// <summary>Cannot use 'let' as a name in lexical binding.</summary>
    public const string LetInLexicalBinding = "JS3001";

    /// <summary>Identifier has already been declared.</summary>
    public const string RedeclarationError = "JS3002";

    /// <summary>Const declarations require an initializer.</summary>
    public const string ConstRequiresInitializer = "JS3003";

    /// <summary>Rest parameter must be last in parameter list.</summary>
    public const string RestParameterNotLast = "JS3004";

    /// <summary>Rest element cannot have a default initializer.</summary>
    public const string DefaultAfterRest = "JS3005";

    #endregion

    #region Control flow errors (4xxx)

    /// <summary>Illegal break statement.</summary>
    public const string IllegalBreak = "JS4001";

    /// <summary>Illegal continue statement.</summary>
    public const string IllegalContinue = "JS4002";

    /// <summary>Illegal return statement.</summary>
    public const string IllegalReturn = "JS4003";

    /// <summary>Duplicate label declaration.</summary>
    public const string DuplicateLabel = "JS4004";

    /// <summary>Undefined label reference.</summary>
    public const string UndefinedLabel = "JS4005";

    #endregion

    #region Class/function errors (5xxx)

    /// <summary>Constructor cannot be a generator or async method.</summary>
    public const string ConstructorSpecialMethod = "JS5001";

    /// <summary>A class may only have one constructor.</summary>
    public const string DuplicateConstructor = "JS5002";

    /// <summary>Classes may not have a static property named 'prototype'.</summary>
    public const string StaticPrototype = "JS5003";

    /// <summary>Getter must not have any formal parameters.</summary>
    public const string GetterNoParams = "JS5004";

    /// <summary>Setter must have exactly one formal parameter.</summary>
    public const string SetterOneParam = "JS5005";

    /// <summary>Yield expression is not allowed outside a generator function.</summary>
    public const string YieldInNonGenerator = "JS5006";

    /// <summary>Await expression is not allowed outside an async function.</summary>
    public const string AwaitInNonAsync = "JS5007";

    /// <summary>'super' keyword is only allowed inside a method.</summary>
    public const string SuperOutsideMethod = "JS5008";

    /// <summary>'new.target' is only allowed inside a function.</summary>
    public const string NewTargetOutsideFunction = "JS5009";

    #endregion

    #region Module errors (6xxx)

    /// <summary>Import declaration must be at the top level of a module.</summary>
    public const string ImportNotAtTopLevel = "JS6001";

    /// <summary>Export declaration must be at the top level of a module.</summary>
    public const string ExportNotAtTopLevel = "JS6002";

    /// <summary>Import declarations are not allowed in script mode.</summary>
    public const string ImportInScript = "JS6003";

    /// <summary>Export declarations are not allowed in script mode.</summary>
    public const string ExportInScript = "JS6004";

    /// <summary>Duplicate export name.</summary>
    public const string DuplicateExport = "JS6005";

    #endregion

    #region Warning codes (8xxx)

    /// <summary>Statement has no effect.</summary>
    public const string UselessStatement = "JS8001";

    /// <summary>Unreachable code detected.</summary>
    public const string UnreachableCode = "JS8002";

    /// <summary>Deprecated octal literal syntax.</summary>
    public const string DeprecatedOctal = "JS8003";

    /// <summary>With statements are not recommended.</summary>
    public const string WithStatement = "JS8004";

    #endregion

    /// <summary>
    /// Gets a human-readable description for an error code.
    /// </summary>
    public static string GetDescription(string code)
    {
        return code switch
        {
            UnterminatedString => "Unterminated string literal",
            UnterminatedComment => "Unterminated comment",
            UnterminatedRegExp => "Unterminated regular expression",
            UnterminatedTemplateLiteral => "Unterminated template literal",
            InvalidNumber => "Invalid number",
            InvalidEscapeSequence => "Invalid escape sequence",
            InvalidUnicodeEscape => "Invalid Unicode escape sequence",
            InvalidCharacter => "Invalid character",
            OctalEscapeInStrictMode => "Octal escape sequences are not allowed in strict mode",
            OctalLiteralInStrictMode => "Octal literals are not allowed in strict mode",

            UnexpectedToken => "Unexpected token",
            UnexpectedEndOfInput => "Unexpected end of input",
            ExpectedToken => "Expected token",
            ExpectedIdentifier => "Expected identifier",
            ExpectedExpression => "Expected expression",
            ExpectedStatement => "Expected statement",
            InvalidLeftHandSide => "Invalid left-hand side in assignment",
            DuplicateParameter => "Duplicate parameter name",
            DuplicateProperty => "Duplicate property name",
            StrictModeReservedWord => "Reserved word in strict mode",
            InvalidAssignmentTarget => "Invalid assignment target",

            LetInLexicalBinding => "'let' is not allowed as a lexically bound name",
            RedeclarationError => "Identifier has already been declared",
            ConstRequiresInitializer => "'const' declarations require an initializer",
            RestParameterNotLast => "Rest parameter must be last",
            DefaultAfterRest => "Default parameter not allowed after rest parameter",

            IllegalBreak => "'break' not inside loop or switch",
            IllegalContinue => "'continue' not inside loop",
            IllegalReturn => "'return' not inside function",
            DuplicateLabel => "Duplicate label",
            UndefinedLabel => "Undefined label",

            ConstructorSpecialMethod => "Constructor can't be a generator or async",
            DuplicateConstructor => "A class may only have one constructor",
            StaticPrototype => "Classes may not have a static property named 'prototype'",
            GetterNoParams => "Getter must have no parameters",
            SetterOneParam => "Setter must have exactly one parameter",
            YieldInNonGenerator => "'yield' expression is only allowed in generator functions",
            AwaitInNonAsync => "'await' expression is only allowed in async functions",
            SuperOutsideMethod => "'super' keyword only allowed in methods",
            NewTargetOutsideFunction => "'new.target' only allowed in functions",

            ImportNotAtTopLevel => "'import' declarations may only appear at top level",
            ExportNotAtTopLevel => "'export' declarations may only appear at top level",
            ImportInScript => "Cannot use 'import' in script mode",
            ExportInScript => "Cannot use 'export' in script mode",
            DuplicateExport => "Duplicate export",

            UselessStatement => "Useless statement",
            UnreachableCode => "Unreachable code",
            DeprecatedOctal => "Octal literals are deprecated",
            WithStatement => "'with' statement is not allowed in strict mode",

            _ => "Unknown error"
        };
    }
}

/// <summary>
/// Collection of parse diagnostics.
/// </summary>
public sealed class DiagnosticBag
{
    private readonly List<ParseDiagnostic> _diagnostics = new List<ParseDiagnostic>();

    /// <summary>
    /// Gets whether this bag contains any errors.
    /// </summary>
    public bool HasErrors
    {
        get
        {
            foreach (var d in _diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets whether this bag contains any diagnostics.
    /// </summary>
    public bool HasDiagnostics => _diagnostics.Count > 0;

    /// <summary>
    /// Gets the number of diagnostics.
    /// </summary>
    public int Count => _diagnostics.Count;

    /// <summary>
    /// Gets the diagnostics in this bag.
    /// </summary>
    public IReadOnlyList<ParseDiagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Adds a diagnostic to this bag.
    /// </summary>
    public void Add(ParseDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Adds an error diagnostic.
    /// </summary>
    public void AddError(string code, string message, SourceLocation location, string? fileName = null)
    {
        _diagnostics.Add(ParseDiagnostic.Error(code, message, location, fileName));
    }

    /// <summary>
    /// Adds a warning diagnostic.
    /// </summary>
    public void AddWarning(string code, string message, SourceLocation location, string? fileName = null)
    {
        _diagnostics.Add(ParseDiagnostic.Warning(code, message, location, fileName));
    }

    /// <summary>
    /// Clears all diagnostics.
    /// </summary>
    public void Clear()
    {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Gets errors only.
    /// </summary>
    public IEnumerable<ParseDiagnostic> GetErrors()
    {
        foreach (var d in _diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error)
                yield return d;
        }
    }

    /// <summary>
    /// Gets warnings only.
    /// </summary>
    public IEnumerable<ParseDiagnostic> GetWarnings()
    {
        foreach (var d in _diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Warning)
                yield return d;
        }
    }

    /// <summary>
    /// Returns a formatted string with all diagnostics.
    /// </summary>
    public override string ToString()
    {
        if (_diagnostics.Count == 0)
            return "No diagnostics";

        var sb = new StringBuilder();
        foreach (var d in _diagnostics)
        {
            sb.AppendLine(d.ToString());
        }
        return sb.ToString();
    }
}
