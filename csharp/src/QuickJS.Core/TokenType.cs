// Licensed under the MIT License.

namespace QuickJS;

/// <summary>
/// Represents the type of a lexical token in JavaScript source code.
/// </summary>
/// <remarks>
/// <para>
/// Tokens are the basic building blocks produced by the lexer. Each token
/// represents a meaningful unit of JavaScript syntax: keywords, identifiers,
/// literals, operators, and punctuation.
/// </para>
/// <para>
/// The token types are organized into categories:
/// - Literals: Number, String, Template, RegExp
/// - Identifiers: Ident, PrivateName
/// - Keywords: if, else, for, while, etc.
/// - Operators: +, -, *, /, ==, ===, etc.
/// - Punctuation: {, }, (, ), [, ], etc.
/// - Special: EOF, Error
/// </para>
/// </remarks>
public enum TokenType
{
    // ========================================
    // Special tokens
    // ========================================

    /// <summary>End of file/input.</summary>
    EOF = 0,

    /// <summary>Lexer error token.</summary>
    Error,

    // ========================================
    // Literals
    // ========================================

    /// <summary>Numeric literal (integer or floating point).</summary>
    Number,

    /// <summary>String literal (single or double quoted).</summary>
    String,

    /// <summary>Template literal (backtick quoted).</summary>
    Template,

    /// <summary>Regular expression literal.</summary>
    RegExp,

    // ========================================
    // Identifiers
    // ========================================

    /// <summary>Identifier (variable name, property name, etc.).</summary>
    Identifier,

    /// <summary>Private name (#identifier).</summary>
    PrivateName,

    // ========================================
    // Keywords - Literals
    // ========================================

    /// <summary>null keyword.</summary>
    Null,

    /// <summary>false keyword.</summary>
    False,

    /// <summary>true keyword.</summary>
    True,

    // ========================================
    // Keywords - Control Flow
    // ========================================

    /// <summary>if keyword.</summary>
    If,

    /// <summary>else keyword.</summary>
    Else,

    /// <summary>do keyword.</summary>
    Do,

    /// <summary>while keyword.</summary>
    While,

    /// <summary>for keyword.</summary>
    For,

    /// <summary>break keyword.</summary>
    Break,

    /// <summary>continue keyword.</summary>
    Continue,

    /// <summary>switch keyword.</summary>
    Switch,

    /// <summary>case keyword.</summary>
    Case,

    /// <summary>default keyword.</summary>
    Default,

    // ========================================
    // Keywords - Functions
    // ========================================

    /// <summary>return keyword.</summary>
    Return,

    /// <summary>function keyword.</summary>
    Function,

    /// <summary>yield keyword.</summary>
    Yield,

    /// <summary>await keyword.</summary>
    Await,

    // ========================================
    // Keywords - Exception Handling
    // ========================================

    /// <summary>throw keyword.</summary>
    Throw,

    /// <summary>try keyword.</summary>
    Try,

    /// <summary>catch keyword.</summary>
    Catch,

    /// <summary>finally keyword.</summary>
    Finally,

    // ========================================
    // Keywords - Declarations
    // ========================================

    /// <summary>var keyword.</summary>
    Var,

    /// <summary>let keyword.</summary>
    Let,

    /// <summary>const keyword.</summary>
    Const,

    // ========================================
    // Keywords - Classes
    // ========================================

    /// <summary>class keyword.</summary>
    Class,

    /// <summary>extends keyword.</summary>
    Extends,

    /// <summary>super keyword.</summary>
    Super,

    /// <summary>static keyword.</summary>
    Static,

    // ========================================
    // Keywords - Operators
    // ========================================

    /// <summary>this keyword.</summary>
    This,

    /// <summary>new keyword.</summary>
    New,

    /// <summary>delete keyword.</summary>
    Delete,

    /// <summary>void keyword.</summary>
    Void,

    /// <summary>typeof keyword.</summary>
    TypeOf,

    /// <summary>in keyword.</summary>
    In,

    /// <summary>of keyword.</summary>
    Of,

    /// <summary>instanceof keyword.</summary>
    InstanceOf,

    // ========================================
    // Keywords - Modules
    // ========================================

    /// <summary>import keyword.</summary>
    Import,

    /// <summary>export keyword.</summary>
    Export,

    // ========================================
    // Keywords - Other
    // ========================================

    /// <summary>debugger keyword.</summary>
    Debugger,

    /// <summary>with keyword (deprecated).</summary>
    With,

    // ========================================
    // Keywords - Future Reserved (Strict Mode)
    // ========================================

    /// <summary>enum keyword (reserved).</summary>
    Enum,

    /// <summary>implements keyword (reserved in strict mode).</summary>
    Implements,

    /// <summary>interface keyword (reserved in strict mode).</summary>
    Interface,

    /// <summary>package keyword (reserved in strict mode).</summary>
    Package,

    /// <summary>private keyword (reserved in strict mode).</summary>
    Private,

    /// <summary>protected keyword (reserved in strict mode).</summary>
    Protected,

    /// <summary>public keyword (reserved in strict mode).</summary>
    Public,

    // ========================================
    // Punctuation - Grouping
    // ========================================

    /// <summary>Left parenthesis '('.</summary>
    LeftParen,

    /// <summary>Right parenthesis ')'.</summary>
    RightParen,

    /// <summary>Left brace '{'.</summary>
    LeftBrace,

    /// <summary>Right brace '}'.</summary>
    RightBrace,

    /// <summary>Left bracket '['.</summary>
    LeftBracket,

    /// <summary>Right bracket ']'.</summary>
    RightBracket,

    // ========================================
    // Punctuation - Separators
    // ========================================

    /// <summary>Comma ','.</summary>
    Comma,

    /// <summary>Semicolon ';'.</summary>
    Semicolon,

    /// <summary>Colon ':'.</summary>
    Colon,

    /// <summary>Dot '.'.</summary>
    Dot,

    /// <summary>Ellipsis '...'.</summary>
    Ellipsis,

    // ========================================
    // Operators - Arithmetic
    // ========================================

    /// <summary>Plus '+'.</summary>
    Plus,

    /// <summary>Minus '-'.</summary>
    Minus,

    /// <summary>Asterisk '*'.</summary>
    Asterisk,

    /// <summary>Slash '/'.</summary>
    Slash,

    /// <summary>Percent '%'.</summary>
    Percent,

    /// <summary>Exponentiation '**'.</summary>
    Power,

    /// <summary>Increment '++'.</summary>
    Increment,

    /// <summary>Decrement '--'.</summary>
    Decrement,

    // ========================================
    // Operators - Bitwise
    // ========================================

    /// <summary>Bitwise AND '&amp;'.</summary>
    Ampersand,

    /// <summary>Bitwise OR '|'.</summary>
    Pipe,

    /// <summary>Bitwise XOR '^'.</summary>
    Caret,

    /// <summary>Bitwise NOT '~'.</summary>
    Tilde,

    /// <summary>Left shift '&lt;&lt;'.</summary>
    LeftShift,

    /// <summary>Signed right shift '&gt;&gt;'.</summary>
    RightShift,

    /// <summary>Unsigned right shift '&gt;&gt;&gt;'.</summary>
    UnsignedRightShift,

    // ========================================
    // Operators - Comparison
    // ========================================

    /// <summary>Less than '&lt;'.</summary>
    LessThan,

    /// <summary>Less than or equal '&lt;='.</summary>
    LessThanOrEqual,

    /// <summary>Greater than '&gt;'.</summary>
    GreaterThan,

    /// <summary>Greater than or equal '&gt;='.</summary>
    GreaterThanOrEqual,

    /// <summary>Equality '=='.</summary>
    Equal,

    /// <summary>Inequality '!='.</summary>
    NotEqual,

    /// <summary>Strict equality '==='.</summary>
    StrictEqual,

    /// <summary>Strict inequality '!=='.</summary>
    StrictNotEqual,

    // ========================================
    // Operators - Logical
    // ========================================

    /// <summary>Logical AND '&amp;&amp;'.</summary>
    LogicalAnd,

    /// <summary>Logical OR '||'.</summary>
    LogicalOr,

    /// <summary>Logical NOT '!'.</summary>
    LogicalNot,

    /// <summary>Nullish coalescing '??'.</summary>
    NullishCoalescing,

    // ========================================
    // Operators - Assignment
    // ========================================

    /// <summary>Assignment '='.</summary>
    Assign,

    /// <summary>Addition assignment '+='.</summary>
    PlusAssign,

    /// <summary>Subtraction assignment '-='.</summary>
    MinusAssign,

    /// <summary>Multiplication assignment '*='.</summary>
    AsteriskAssign,

    /// <summary>Division assignment '/='.</summary>
    SlashAssign,

    /// <summary>Remainder assignment '%='.</summary>
    PercentAssign,

    /// <summary>Exponentiation assignment '**='.</summary>
    PowerAssign,

    /// <summary>Bitwise AND assignment '&amp;='.</summary>
    AmpersandAssign,

    /// <summary>Bitwise OR assignment '|='.</summary>
    PipeAssign,

    /// <summary>Bitwise XOR assignment '^='.</summary>
    CaretAssign,

    /// <summary>Left shift assignment '&lt;&lt;='.</summary>
    LeftShiftAssign,

    /// <summary>Signed right shift assignment '&gt;&gt;='.</summary>
    RightShiftAssign,

    /// <summary>Unsigned right shift assignment '&gt;&gt;&gt;='.</summary>
    UnsignedRightShiftAssign,

    /// <summary>Logical AND assignment '&amp;&amp;='.</summary>
    LogicalAndAssign,

    /// <summary>Logical OR assignment '||='.</summary>
    LogicalOrAssign,

    /// <summary>Nullish coalescing assignment '??='.</summary>
    NullishCoalescingAssign,

    // ========================================
    // Operators - Misc
    // ========================================

    /// <summary>Conditional/ternary '?'.</summary>
    Question,

    /// <summary>Arrow function '=>'.</summary>
    Arrow,

    /// <summary>Optional chaining '?.'.</summary>
    OptionalChaining,
}
