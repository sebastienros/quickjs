// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace QuickJS;

/// <summary>
/// Specifies the kind of variable declaration.
/// </summary>
/// <remarks>
/// JavaScript has different types of variable declarations with different semantics:
/// <list type="bullet">
/// <item><c>var</c> - function-scoped, hoisted</item>
/// <item><c>let</c> - block-scoped, not hoisted (temporal dead zone)</item>
/// <item><c>const</c> - block-scoped, not hoisted, cannot be reassigned</item>
/// <item>function declarations - hoisted with their definition</item>
/// </list>
/// Based on JSVarKindEnum from QuickJS.
/// </remarks>
public enum JSVarKind : byte
{
    /// <summary>Normal variable (var, let, or const).</summary>
    Normal = 0,

    /// <summary>Function declaration (hoisted with its definition).</summary>
    FunctionDecl,

    /// <summary>Async or generator function declaration.</summary>
    NewFunctionDecl,

    /// <summary>Catch clause variable (e.g., <c>catch(e)</c>).</summary>
    Catch,

    /// <summary>Function expression name (only visible inside the function).</summary>
    FunctionName,

    /// <summary>Private class field.</summary>
    PrivateField,

    /// <summary>Private class method.</summary>
    PrivateMethod,

    /// <summary>Private getter.</summary>
    PrivateGetter,

    /// <summary>Private setter.</summary>
    PrivateSetter,

    /// <summary>Private getter and setter pair.</summary>
    PrivateGetterSetter,

    /// <summary>Global function declaration (for global scope only).</summary>
    GlobalFunctionDecl,
}

/// <summary>
/// Specifies how a closure variable is accessed.
/// </summary>
/// <remarks>
/// <para>
/// When a function accesses a variable from an outer scope, that variable
/// becomes a "closure variable". JavaScript closures capture variables by
/// reference, not by value.
/// </para>
/// <para>
/// Example:
/// <code>
/// function outer() {
///     let x = 10;           // This becomes a closure var
///     return function() {
///         return x + 1;     // Accesses x from outer scope
///     };
/// }
/// </code>
/// </para>
/// Based on JSClosureTypeEnum from QuickJS.
/// </remarks>
public enum JSClosureType : byte
{
    /// <summary>Local variable from parent function.</summary>
    Local = 0,

    /// <summary>Closure variable from parent function's closure.</summary>
    Parent,

    /// <summary>Variable reference to parent (used during argument scope).</summary>
    Ref,

    /// <summary>Module-level variable declaration.</summary>
    ModuleDecl,

    /// <summary>Module import binding.</summary>
    ModuleImport,
}

/// <summary>
/// Represents a variable definition during compilation.
/// </summary>
/// <remarks>
/// <para>
/// JavaScript variables have complex scoping rules:
/// </para>
/// <list type="bullet">
/// <item><c>var</c> is function-scoped and hoisted to the top</item>
/// <item><c>let</c>/<c>const</c> are block-scoped with a temporal dead zone</item>
/// <item>Function declarations are hoisted with their definitions</item>
/// </list>
/// <para>
/// Example of scoping:
/// <code>
/// function example() {
///     console.log(a);    // undefined (var is hoisted)
///     console.log(b);    // ReferenceError (temporal dead zone)
///     var a = 1;
///     let b = 2;
///     {
///         let c = 3;     // Block-scoped, not visible outside
///     }
/// }
/// </code>
/// </para>
/// Based on JSVarDef from QuickJS.
/// </remarks>
public sealed class JSVarDef
{
    /// <summary>
    /// Gets or sets the variable name atom.
    /// </summary>
    public JSAtom Name { get; set; }

    /// <summary>
    /// Gets or sets the scope level where this variable is defined.
    /// 0 means function scope (var), other values indicate lexical scope.
    /// </summary>
    public int ScopeLevel { get; set; }

    /// <summary>
    /// Gets or sets the index of the next variable in the same or enclosing scope.
    /// Used for scope chain traversal.
    /// </summary>
    public int ScopeNext { get; set; } = -1;

    /// <summary>
    /// Gets or sets whether this is a const variable.
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// Gets or sets whether this is a lexically-scoped variable (let/const).
    /// </summary>
    public bool IsLexical { get; set; }

    /// <summary>
    /// Gets or sets whether this variable is captured by a closure.
    /// </summary>
    public bool IsCaptured { get; set; }

    /// <summary>
    /// Gets or sets the kind of variable.
    /// </summary>
    public JSVarKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the index of the corresponding variable reference.
    /// Used when IsCaptured is true.
    /// </summary>
    public int VarRefIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the function pool index for function declarations.
    /// </summary>
    public int FuncPoolIndex { get; set; } = -1;

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString()
    {
        var flags = new System.Text.StringBuilder();
        if (IsConst) flags.Append("const ");
        if (IsLexical) flags.Append("lexical ");
        if (IsCaptured) flags.Append("captured ");
        return $"Var({Name}, scope={ScopeLevel}, kind={Kind}, {flags.ToString().Trim()})";
    }
}

/// <summary>
/// Represents a closure variable - a variable captured from an outer scope.
/// </summary>
/// <remarks>
/// <para>
/// Closures are one of JavaScript's most powerful features. When a function
/// references a variable from an outer scope, that variable must remain
/// accessible even after the outer function returns.
/// </para>
/// <para>
/// Example:
/// <code>
/// function createCounter() {
///     let count = 0;              // Captured by inner function
///     return function() {
///         return ++count;         // count is a closure variable
///     };
/// }
/// const counter = createCounter();
/// counter(); // 1
/// counter(); // 2 (count persists!)
/// </code>
/// </para>
/// Based on JSClosureVar from QuickJS.
/// </remarks>
public sealed class JSClosureVar
{
    /// <summary>
    /// Gets or sets the closure type (local, parent, ref, etc.).
    /// </summary>
    public JSClosureType ClosureType { get; set; }

    /// <summary>
    /// Gets or sets whether this is a lexically-scoped variable.
    /// </summary>
    public bool IsLexical { get; set; }

    /// <summary>
    /// Gets or sets whether this is a const variable.
    /// </summary>
    public bool IsConst { get; set; }

    /// <summary>
    /// Gets or sets the variable kind.
    /// </summary>
    public JSVarKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the index in the parent function's variables or closure.
    /// </summary>
    public int VarIndex { get; set; }

    /// <summary>
    /// Gets or sets the variable name atom.
    /// </summary>
    public JSAtom Name { get; set; }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"ClosureVar({Name}, type={ClosureType}, idx={VarIndex})";
}

/// <summary>
/// Represents a lexical scope during compilation.
/// </summary>
/// <remarks>
/// <para>
/// JavaScript has block-level scoping for <c>let</c> and <c>const</c>.
/// Each block <c>{ }</c> creates a new lexical scope.
/// </para>
/// <para>
/// Example:
/// <code>
/// function example() {           // Scope 0
///     let a = 1;
///     {                          // Scope 1 (parent = 0)
///         let b = 2;
///         {                      // Scope 2 (parent = 1)
///             let c = 3;
///             console.log(a+b+c); // Can access all three
///         }
///     }
///     // b and c are not accessible here
/// }
/// </code>
/// </para>
/// Based on JSVarScope from QuickJS.
/// </remarks>
public struct JSVarScope
{
    /// <summary>
    /// Gets or sets the index of the parent scope, or -1 for the root scope.
    /// </summary>
    public int Parent { get; set; }

    /// <summary>
    /// Gets or sets the index of the first variable in this scope.
    /// </summary>
    public int First { get; set; }

    /// <summary>
    /// Initializes a new scope.
    /// </summary>
    public JSVarScope(int parent, int first)
    {
        Parent = parent;
        First = first;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() => $"Scope(parent={Parent}, first={First})";
}
