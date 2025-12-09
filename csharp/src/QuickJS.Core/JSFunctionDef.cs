// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Specifies the type of function being compiled.
/// </summary>
/// <remarks>
/// JavaScript has multiple ways to define functions, each with different semantics:
/// <list type="bullet">
/// <item>Function declarations are hoisted</item>
/// <item>Function expressions are not hoisted</item>
/// <item>Arrow functions don't have their own <c>this</c> or <c>arguments</c></item>
/// <item>Generators can pause and resume (yield)</item>
/// <item>Async functions return Promises</item>
/// </list>
/// </remarks>
public enum JSFunctionKind : byte
{
    /// <summary>Normal function.</summary>
    Normal = 0,

    /// <summary>Generator function (can yield).</summary>
    Generator = 1,

    /// <summary>Async function (returns Promise).</summary>
    Async = 2,

    /// <summary>Async generator function.</summary>
    AsyncGenerator = 3,
}

/// <summary>
/// Specifies how a function was parsed/defined.
/// </summary>
public enum JSParseFunctionType : byte
{
    /// <summary>Function statement: <c>function foo() {}</c></summary>
    Statement = 0,

    /// <summary>Variable-bound function.</summary>
    Var,

    /// <summary>Function expression: <c>var f = function() {}</c></summary>
    Expression,

    /// <summary>Arrow function: <c>() => {}</c></summary>
    Arrow,

    /// <summary>Getter: <c>get prop() {}</c></summary>
    Getter,

    /// <summary>Setter: <c>set prop(v) {}</c></summary>
    Setter,

    /// <summary>Method: <c>method() {}</c></summary>
    Method,

    /// <summary>Class static initializer block.</summary>
    ClassStaticInit,

    /// <summary>Class constructor.</summary>
    ClassConstructor,

    /// <summary>Derived class constructor (must call super).</summary>
    DerivedClassConstructor,
}

/// <summary>
/// The compilation context for a JavaScript function.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JSFunctionDef"/> is the central data structure during JavaScript compilation.
/// When the parser encounters a function, it creates a JSFunctionDef to track:
/// </para>
/// <list type="bullet">
/// <item><b>Bytecode</b>: The compiled instructions (<see cref="ByteCode"/>)</item>
/// <item><b>Constants</b>: String/number literals, nested functions (<see cref="Constants"/>)</item>
/// <item><b>Variables</b>: Local variables, arguments, closure captures</item>
/// <item><b>Scopes</b>: Lexical scopes for let/const block scoping</item>
/// <item><b>Labels</b>: Jump targets for control flow</item>
/// <item><b>Debug info</b>: Source positions for error messages</item>
/// </list>
/// <para>
/// JavaScript functions form a tree structure - each function can contain nested
/// functions, which can themselves contain more functions. This is essential for
/// closures to work correctly.
/// </para>
/// <para>
/// Example:
/// <code>
/// function outer(x) {           // JSFunctionDef for outer
///     function inner(y) {       // JSFunctionDef for inner (parent = outer)
///         return x + y;         // x is captured from outer
///     }
///     return inner;
/// }
/// </code>
/// </para>
/// Based on JSFunctionDef from QuickJS.
/// </remarks>
public sealed class JSFunctionDef
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="JSFunctionDef"/> class.
    /// </summary>
    public JSFunctionDef()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JSFunctionDef"/> class with the specified name.
    /// </summary>
    /// <param name="funcName">The function name atom.</param>
    public JSFunctionDef(JSAtom funcName)
    {
        FuncName = funcName;
    }

    #endregion

    #region Compilation Context

    /// <summary>
    /// Gets or sets the parent function, or null for top-level code.
    /// </summary>
    /// <remarks>
    /// JavaScript functions nest - inner functions can access outer function variables.
    /// This is how closures work.
    /// </remarks>
    public JSFunctionDef? Parent { get; set; }

    /// <summary>
    /// Gets the list of child functions defined within this function.
    /// </summary>
    public List<JSFunctionDef> Children { get; } = new List<JSFunctionDef>();

    /// <summary>
    /// Adds a child function and returns its index.
    /// </summary>
    /// <param name="child">The child function to add.</param>
    /// <returns>The index of the child function in the Children list.</returns>
    public int AddChildFunction(JSFunctionDef child)
    {
        int idx = Children.Count;
        Children.Add(child);
        child.Parent = this;
        child.ParentCPoolIndex = Constants.AddFunction(idx);
        child.ParentScopeLevel = ScopeLevel;
        return idx;
    }

    /// <summary>
    /// Gets or sets the index of this function in the parent's constant pool.
    /// -1 if this is the top-level function or not yet added.
    /// </summary>
    public int ParentCPoolIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the scope level in the parent at the point of definition.
    /// </summary>
    public int ParentScopeLevel { get; set; }

    #endregion

    #region Function Properties

    /// <summary>
    /// Gets or sets the function name atom, or null for anonymous functions.
    /// </summary>
    public JSAtom FuncName { get; set; }

    /// <summary>
    /// Gets or sets the kind of function (normal, generator, async, etc.).
    /// </summary>
    public JSFunctionKind FuncKind { get; set; }

    /// <summary>
    /// Gets or sets how the function was defined (statement, expression, arrow, etc.).
    /// </summary>
    public JSParseFunctionType FuncType { get; set; }

    /// <summary>
    /// Gets or sets whether this is eval code.
    /// </summary>
    public bool IsEval { get; set; }

    /// <summary>
    /// Gets or sets whether this is a function expression.
    /// </summary>
    public bool IsFuncExpr { get; set; }

    /// <summary>
    /// Gets or sets whether strict mode is enabled.
    /// </summary>
    /// <remarks>
    /// Strict mode changes several JavaScript behaviors:
    /// - No implicit globals
    /// - <c>this</c> is undefined (not window) in function calls
    /// - Errors on assigning to read-only properties
    /// </remarks>
    public bool IsStrict { get; set; }

    /// <summary>
    /// Gets or sets whether the function has a simple parameter list.
    /// </summary>
    /// <remarks>
    /// A function has a simple parameter list if it has no default values,
    /// rest parameters, or destructuring patterns. Non-simple parameters
    /// affect strict mode handling and arguments object behavior.
    /// </remarks>
    public bool HasSimpleParameterList { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the function has parameter expressions.
    /// </summary>
    public bool HasParameterExpressions { get; set; }

    /// <summary>
    /// Gets or sets whether the function uses <c>arguments</c>.
    /// </summary>
    public bool HasArgumentsBinding { get; set; }

    /// <summary>
    /// Gets or sets whether the function has its own <c>this</c> binding.
    /// </summary>
    /// <remarks>
    /// Arrow functions do not have their own <c>this</c> - they inherit it
    /// from the enclosing scope. Regular functions get their own <c>this</c>.
    /// </remarks>
    public bool HasThisBinding { get; set; }

    /// <summary>
    /// Gets or sets whether <c>new.target</c> is allowed in this function.
    /// </summary>
    public bool NewTargetAllowed { get; set; }

    /// <summary>
    /// Gets or sets whether <c>super()</c> is allowed (derived class constructor).
    /// </summary>
    public bool SuperCallAllowed { get; set; }

    /// <summary>
    /// Gets or sets whether <c>super.x</c> or <c>super[x]</c> is allowed.
    /// </summary>
    public bool SuperAllowed { get; set; }

    /// <summary>
    /// Gets or sets whether the <c>arguments</c> identifier is allowed in this function.
    /// </summary>
    public bool ArgumentsAllowed { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the function contains a call to <c>eval()</c>.
    /// </summary>
    /// <remarks>
    /// Presence of <c>eval()</c> affects optimization - all local variables
    /// must be accessible because eval can reference them dynamically.
    /// </remarks>
    public bool HasEvalCall { get; set; }

    /// <summary>
    /// Gets or sets whether this is a derived class constructor.
    /// </summary>
    public bool IsDerivedClassConstructor { get; set; }

    /// <summary>
    /// Gets or sets whether this function has a prototype property.
    /// </summary>
    /// <remarks>
    /// Arrow functions and methods don't have a prototype property.
    /// Only regular function declarations and expressions have one.
    /// </remarks>
    public bool HasPrototype { get; set; }

    /// <summary>
    /// Gets or sets whether this function has a home object for super references.
    /// </summary>
    public bool HasHomeObject { get; set; }

    #endregion

    #region Bytecode

    /// <summary>
    /// Gets the bytecode buffer for this function.
    /// </summary>
    public ByteCodeBuffer ByteCode { get; } = new ByteCodeBuffer();

    /// <summary>
    /// Gets or sets whether to use short opcodes for optimization.
    /// </summary>
    public bool UseShortOpcodes { get; set; } = true;

    #endregion

    #region Constant Pool

    /// <summary>
    /// Gets the constant pool for this function.
    /// </summary>
    public ConstantPool Constants { get; } = new ConstantPool();

    #endregion

    #region Variables

    /// <summary>
    /// Gets the list of local variable definitions.
    /// </summary>
    public List<JSVarDef> Vars { get; } = new List<JSVarDef>();

    /// <summary>
    /// Gets the list of argument definitions.
    /// </summary>
    public List<JSVarDef> Args { get; } = new List<JSVarDef>();
    /// <summary>
    /// Finds the index of a variable by name atom.
    /// </summary>
    public int FindVarIndex(string name)
    {
        for (int i = 0; i < Vars.Count; i++)
        {
            if (Vars[i].Name.ToString() == name)
                return i;
        }
        return -1;
    }


    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int ArgCount => Args.Count;

    /// <summary>
    /// Gets or sets the number of required arguments (before any with defaults).
    /// </summary>
    public int DefinedArgCount { get; set; }

    /// <summary>
    /// Gets the list of closure variables (captured from outer scopes).
    /// </summary>
    public List<JSClosureVar> ClosureVars { get; } = new List<JSClosureVar>();

    /// <summary>
    /// Gets the number of variable references (for creating VarRef array at runtime).
    /// </summary>
    public int VarRefCount { get; set; }

    /// <summary>
    /// Gets or sets the index of the arguments variable, or -1 if none.
    /// </summary>
    public int ArgumentsVarIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the function expression name variable, or -1 if none.
    /// </summary>
    public int FuncVarIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the 'this' variable, or -1 if none.
    /// </summary>
    public int ThisVarIndex { get; set; } = -1;

    /// <summary>
    /// Exception handlers for try/catch/finally.
    /// </summary>
    public List<JSExceptionHandler> ExceptionHandlers { get; } = new List<JSExceptionHandler>();

    /// <summary>
    /// Adds an exception handler entry.
    /// </summary>
    public void AddExceptionHandler(JSExceptionHandler handler) => ExceptionHandlers.Add(handler);

    /// <summary>
    /// Gets or sets the index of the 'new.target' variable, or -1 if none.
    /// </summary>
    public int NewTargetVarIndex { get; set; } = -1;

    #endregion

    #region Scopes

    /// <summary>
    /// Gets the list of lexical scopes.
    /// </summary>
    public List<JSVarScope> Scopes { get; } = new List<JSVarScope>();

    /// <summary>
    /// Gets or sets the current scope level during compilation.
    /// </summary>
    public int ScopeLevel { get; set; }

    /// <summary>
    /// Gets or sets the index of the first lexically-scoped variable.
    /// </summary>
    public int ScopeFirst { get; set; } = -1;

    /// <summary>
    /// Gets or sets the scope index of the function body.
    /// </summary>
    public int BodyScope { get; set; }

    #endregion

    #region Labels

    /// <summary>
    /// Gets the list of labels for jump targets.
    /// </summary>
    public List<LabelInfo> Labels { get; } = new List<LabelInfo>();

    #endregion

    #region Debug Info

    /// <summary>
    /// Gets or sets the filename atom for this function.
    /// </summary>
    public JSAtom Filename { get; set; }

    /// <summary>
    /// Gets the line number table for source mapping.
    /// </summary>
    public LineNumberTable LineNumbers { get; } = new LineNumberTable();

    /// <summary>
    /// Gets or sets the source code (if debug info is preserved).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets whether to strip debug information.
    /// </summary>
    public bool StripDebug { get; set; }

    #endregion

    #region Variable Management

    /// <summary>
    /// Adds a local variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="kind">The variable kind.</param>
    /// <param name="isConst">Whether the variable is const.</param>
    /// <param name="isLexical">Whether the variable is lexically scoped (let/const).</param>
    /// <returns>The index of the new variable.</returns>
    public int AddVar(JSAtom name, JSVarKind kind = JSVarKind.Normal,
                      bool isConst = false, bool isLexical = false)
    {
        int index = Vars.Count;
        var varDef = new JSVarDef
        {
            Name = name,
            Kind = kind,
            IsConst = isConst,
            IsLexical = isLexical,
            ScopeLevel = isLexical ? ScopeLevel : 0,
            ScopeNext = isLexical ? ScopeFirst : -1,
        };
        Vars.Add(varDef);

        if (isLexical)
        {
            ScopeFirst = index;
        }

        return index;
    }

    /// <summary>
    /// Adds an argument definition.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <returns>The index of the new argument.</returns>
    public int AddArg(JSAtom name)
    {
        int index = Args.Count;
        Args.Add(new JSVarDef { Name = name });
        return index;
    }

    /// <summary>
    /// Finds a local variable by name.
    /// </summary>
    /// <param name="name">The variable name to find.</param>
    /// <returns>The variable index, or -1 if not found.</returns>
    public int FindVar(JSAtom name)
    {
        for (int i = Vars.Count - 1; i >= 0; i--)
        {
            if (Vars[i].Name.Equals(name) && Vars[i].ScopeLevel == 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Finds an argument by name.
    /// </summary>
    /// <param name="name">The argument name to find.</param>
    /// <returns>The argument index, or -1 if not found.</returns>
    public int FindArg(JSAtom name)
    {
        for (int i = Args.Count - 1; i >= 0; i--)
        {
            if (Args[i].Name.Equals(name))
                return i;
        }
        return -1;
    }

    #endregion

    #region Scope Management

    /// <summary>
    /// Pushes a new lexical scope.
    /// </summary>
    /// <returns>The scope index.</returns>
    public int PushScope()
    {
        int scopeIdx = Scopes.Count;
        Scopes.Add(new JSVarScope(ScopeLevel, ScopeFirst));
        ScopeLevel = scopeIdx;
        ScopeFirst = -1;
        return scopeIdx;
    }

    /// <summary>
    /// Pops the current lexical scope.
    /// </summary>
    public void PopScope()
    {
        if (ScopeLevel >= 0 && ScopeLevel < Scopes.Count)
        {
            var scope = Scopes[ScopeLevel];
            ScopeLevel = scope.Parent;
            ScopeFirst = scope.First;
        }
    }

    #endregion

    #region Label Management

    /// <summary>
    /// Creates a new label.
    /// </summary>
    /// <returns>The label index.</returns>
    public int NewLabel()
    {
        int label = Labels.Count;
        Labels.Add(new LabelInfo());
        return label;
    }

    #endregion

    #region Child Functions

    /// <summary>
    /// Creates a new child function definition.
    /// </summary>
    /// <returns>The new child function definition.</returns>
    public JSFunctionDef CreateChild()
    {
        var child = new JSFunctionDef
        {
            Parent = this,
            ParentScopeLevel = ScopeLevel,
        };
        Children.Add(child);
        return child;
    }

    #endregion

    #region Module Support

    /// <summary>
    /// Gets the list of module requests (imports from other modules).
    /// </summary>
    public List<JSAtom> ModuleRequests { get; } = new List<JSAtom>();

    /// <summary>
    /// Gets the list of import bindings.
    /// Each entry is (local name, import name) where import name is the exported name from the module.
    /// </summary>
    public List<(JSAtom LocalName, JSAtom ImportName)> ImportBindings { get; } = new List<(JSAtom, JSAtom)>();

    /// <summary>
    /// Gets the list of export entries.
    /// Each entry is (local name, export name) where export name is the name exposed to importers.
    /// </summary>
    public List<(JSAtom LocalName, JSAtom ExportName)> ExportEntries { get; } = new List<(JSAtom, JSAtom)>();

    /// <summary>
    /// Adds a module request for the given module specifier.
    /// </summary>
    /// <param name="moduleAtom">The module specifier atom.</param>
    public void AddModuleRequest(JSAtom moduleAtom)
    {
        if (!ModuleRequests.Contains(moduleAtom))
        {
            ModuleRequests.Add(moduleAtom);
        }
    }

    /// <summary>
    /// Adds an import binding.
    /// </summary>
    /// <param name="localName">The local variable name to bind.</param>
    /// <param name="importName">The name exported from the module.</param>
    public void AddImportBinding(JSAtom localName, JSAtom importName)
    {
        ImportBindings.Add((localName, importName));
    }

    /// <summary>
    /// Adds an export entry.
    /// </summary>
    /// <param name="localName">The local name being exported.</param>
    /// <param name="exportName">The exported name (may differ via 'as').</param>
    public void AddExportEntry(JSAtom localName, JSAtom exportName)
    {
        ExportEntries.Add((localName, exportName));
    }

    #endregion

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString()
    {
        var name = FuncName.Value != 0 ? FuncName.ToString() : "(anonymous)";
        return $"JSFunctionDef({name}, args={Args.Count}, vars={Vars.Count}, bytecode={ByteCode.Size})";
    }
}
