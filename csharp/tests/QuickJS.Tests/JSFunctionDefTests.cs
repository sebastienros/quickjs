// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSVarDef"/>, <see cref="JSClosureVar"/>, and <see cref="JSVarScope"/>.
/// </summary>
public class JSVarDefTests
{
    #region JSVarDef

    [Fact]
    public void JSVarDef_DefaultValues()
    {
        var varDef = new JSVarDef();

        Assert.Equal(default, varDef.Name);
        Assert.Equal(0, varDef.ScopeLevel);
        Assert.Equal(-1, varDef.ScopeNext);
        Assert.False(varDef.IsConst);
        Assert.False(varDef.IsLexical);
        Assert.False(varDef.IsCaptured);
        Assert.Equal(JSVarKind.Normal, varDef.Kind);
        Assert.Equal(-1, varDef.VarRefIndex);
        Assert.Equal(-1, varDef.FuncPoolIndex);
    }

    [Fact]
    public void JSVarDef_VarDeclaration()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(100),
            Kind = JSVarKind.Normal,
            IsLexical = false, // var is not lexical
            ScopeLevel = 0,    // function-scoped
        };

        Assert.False(varDef.IsLexical);
        Assert.Equal(0, varDef.ScopeLevel);
    }

    [Fact]
    public void JSVarDef_LetDeclaration()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(101),
            Kind = JSVarKind.Normal,
            IsLexical = true,
            ScopeLevel = 1,    // block-scoped
        };

        Assert.True(varDef.IsLexical);
        Assert.Equal(1, varDef.ScopeLevel);
    }

    [Fact]
    public void JSVarDef_ConstDeclaration()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(102),
            Kind = JSVarKind.Normal,
            IsConst = true,
            IsLexical = true,  // const is always lexical
            ScopeLevel = 2,
        };

        Assert.True(varDef.IsConst);
        Assert.True(varDef.IsLexical);
    }

    [Fact]
    public void JSVarDef_FunctionDeclaration()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(103),
            Kind = JSVarKind.FunctionDecl,
            FuncPoolIndex = 5,
        };

        Assert.Equal(JSVarKind.FunctionDecl, varDef.Kind);
        Assert.Equal(5, varDef.FuncPoolIndex);
    }

    [Fact]
    public void JSVarDef_CapturedVariable()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(104),
            IsCaptured = true,
            VarRefIndex = 3,
        };

        Assert.True(varDef.IsCaptured);
        Assert.Equal(3, varDef.VarRefIndex);
    }

    [Fact]
    public void JSVarDef_ToString()
    {
        var varDef = new JSVarDef
        {
            Name = new JSAtom(100),
            Kind = JSVarKind.Normal,
            IsConst = true,
            IsLexical = true,
            IsCaptured = true,
            ScopeLevel = 2,
        };

        var str = varDef.ToString();

        Assert.Contains("Var", str);
        Assert.Contains("scope=2", str);
        Assert.Contains("const", str);
        Assert.Contains("lexical", str);
        Assert.Contains("captured", str);
    }

    #endregion

    #region JSClosureVar

    [Fact]
    public void JSClosureVar_DefaultValues()
    {
        var closureVar = new JSClosureVar();

        Assert.Equal(JSClosureType.Local, closureVar.ClosureType);
        Assert.False(closureVar.IsLexical);
        Assert.False(closureVar.IsConst);
        Assert.Equal(JSVarKind.Normal, closureVar.Kind);
        Assert.Equal(0, closureVar.VarIndex);
    }

    [Fact]
    public void JSClosureVar_LocalCapture()
    {
        // Capturing a local variable from parent function
        var closureVar = new JSClosureVar
        {
            ClosureType = JSClosureType.Local,
            Name = new JSAtom(200),
            VarIndex = 5,
            IsLexical = true,
        };

        Assert.Equal(JSClosureType.Local, closureVar.ClosureType);
        Assert.Equal(5, closureVar.VarIndex);
    }

    [Fact]
    public void JSClosureVar_ParentCapture()
    {
        // Capturing a variable from grandparent via parent's closure
        var closureVar = new JSClosureVar
        {
            ClosureType = JSClosureType.Parent,
            Name = new JSAtom(201),
            VarIndex = 2,
        };

        Assert.Equal(JSClosureType.Parent, closureVar.ClosureType);
    }

    [Fact]
    public void JSClosureVar_ToString()
    {
        var closureVar = new JSClosureVar
        {
            Name = new JSAtom(200),
            ClosureType = JSClosureType.Local,
            VarIndex = 5,
        };

        var str = closureVar.ToString();

        Assert.Contains("ClosureVar", str);
        Assert.Contains("Local", str);
        Assert.Contains("idx=5", str);
    }

    #endregion

    #region JSVarScope

    [Fact]
    public void JSVarScope_DefaultConstructor()
    {
        var scope = new JSVarScope();

        Assert.Equal(0, scope.Parent);
        Assert.Equal(0, scope.First);
    }

    [Fact]
    public void JSVarScope_ParameterizedConstructor()
    {
        var scope = new JSVarScope(parent: 2, first: 5);

        Assert.Equal(2, scope.Parent);
        Assert.Equal(5, scope.First);
    }

    [Fact]
    public void JSVarScope_ToString()
    {
        var scope = new JSVarScope(parent: 1, first: 3);

        var str = scope.ToString();

        Assert.Contains("parent=1", str);
        Assert.Contains("first=3", str);
    }

    #endregion

    #region JSVarKind

    [Theory]
    [InlineData(JSVarKind.Normal, 0)]
    [InlineData(JSVarKind.FunctionDecl, 1)]
    [InlineData(JSVarKind.Catch, 3)]
    [InlineData(JSVarKind.PrivateField, 5)]
    public void JSVarKind_Values(JSVarKind kind, byte expected)
    {
        Assert.Equal(expected, (byte)kind);
    }

    #endregion

    #region JSClosureType

    [Theory]
    [InlineData(JSClosureType.Local, 0)]
    [InlineData(JSClosureType.Parent, 1)]
    [InlineData(JSClosureType.Ref, 2)]
    [InlineData(JSClosureType.ModuleDecl, 3)]
    [InlineData(JSClosureType.ModuleImport, 4)]
    public void JSClosureType_Values(JSClosureType type, byte expected)
    {
        Assert.Equal(expected, (byte)type);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="JSFunctionDef"/>.
/// </summary>
public class JSFunctionDefTests
{
    #region Construction

    [Fact]
    public void Constructor_DefaultValues()
    {
        var fd = new JSFunctionDef();

        Assert.Null(fd.Parent);
        Assert.Empty(fd.Children);
        Assert.Equal(-1, fd.ParentCPoolIndex);
        Assert.Equal(0, fd.ParentScopeLevel);

        Assert.Equal(default, fd.FuncName);
        Assert.Equal(JSFunctionKind.Normal, fd.FuncKind);
        Assert.Equal(JSParseFunctionType.Statement, fd.FuncType);

        Assert.NotNull(fd.ByteCode);
        Assert.NotNull(fd.Constants);
        Assert.NotNull(fd.Vars);
        Assert.NotNull(fd.Args);
        Assert.NotNull(fd.ClosureVars);
        Assert.NotNull(fd.Scopes);
        Assert.NotNull(fd.Labels);
        Assert.NotNull(fd.LineNumbers);

        Assert.Equal(-1, fd.ArgumentsVarIndex);
        Assert.Equal(-1, fd.FuncVarIndex);
        Assert.Equal(-1, fd.ThisVarIndex);
        Assert.Equal(-1, fd.NewTargetVarIndex);
    }

    #endregion

    #region Function Properties

    [Fact]
    public void FunctionKind_Generator()
    {
        var fd = new JSFunctionDef { FuncKind = JSFunctionKind.Generator };

        Assert.Equal(JSFunctionKind.Generator, fd.FuncKind);
    }

    [Fact]
    public void FunctionKind_Async()
    {
        var fd = new JSFunctionDef { FuncKind = JSFunctionKind.Async };

        Assert.Equal(JSFunctionKind.Async, fd.FuncKind);
    }

    [Fact]
    public void FunctionType_Arrow()
    {
        var fd = new JSFunctionDef
        {
            FuncType = JSParseFunctionType.Arrow,
            HasThisBinding = false, // Arrow functions don't have their own this
        };

        Assert.Equal(JSParseFunctionType.Arrow, fd.FuncType);
        Assert.False(fd.HasThisBinding);
    }

    [Fact]
    public void StrictMode()
    {
        var fd = new JSFunctionDef { IsStrict = true };

        Assert.True(fd.IsStrict);
    }

    #endregion

    #region Variable Management

    [Fact]
    public void AddVar_ReturnsSequentialIndices()
    {
        var fd = new JSFunctionDef();

        int idx0 = fd.AddVar(new JSAtom(100));
        int idx1 = fd.AddVar(new JSAtom(101));
        int idx2 = fd.AddVar(new JSAtom(102));

        Assert.Equal(0, idx0);
        Assert.Equal(1, idx1);
        Assert.Equal(2, idx2);
        Assert.Equal(3, fd.Vars.Count);
    }

    [Fact]
    public void AddVar_WithKind()
    {
        var fd = new JSFunctionDef();

        fd.AddVar(new JSAtom(100), JSVarKind.FunctionDecl);

        Assert.Equal(JSVarKind.FunctionDecl, fd.Vars[0].Kind);
    }

    [Fact]
    public void AddVar_Const()
    {
        var fd = new JSFunctionDef();

        fd.AddVar(new JSAtom(100), isConst: true, isLexical: true);

        Assert.True(fd.Vars[0].IsConst);
        Assert.True(fd.Vars[0].IsLexical);
    }

    [Fact]
    public void AddArg_ReturnsSequentialIndices()
    {
        var fd = new JSFunctionDef();

        int idx0 = fd.AddArg(new JSAtom(100));
        int idx1 = fd.AddArg(new JSAtom(101));

        Assert.Equal(0, idx0);
        Assert.Equal(1, idx1);
        Assert.Equal(2, fd.Args.Count);
    }

    [Fact]
    public void FindVar_ExistingVar_ReturnsIndex()
    {
        var fd = new JSFunctionDef();
        var name = new JSAtom(100);
        fd.AddVar(new JSAtom(99));
        fd.AddVar(name);
        fd.AddVar(new JSAtom(101));

        int idx = fd.FindVar(name);

        Assert.Equal(1, idx);
    }

    [Fact]
    public void FindVar_NonExisting_ReturnsMinusOne()
    {
        var fd = new JSFunctionDef();
        fd.AddVar(new JSAtom(100));

        int idx = fd.FindVar(new JSAtom(999));

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void FindArg_ExistingArg_ReturnsIndex()
    {
        var fd = new JSFunctionDef();
        var name = new JSAtom(100);
        fd.AddArg(new JSAtom(99));
        fd.AddArg(name);

        int idx = fd.FindArg(name);

        Assert.Equal(1, idx);
    }

    [Fact]
    public void FindArg_NonExisting_ReturnsMinusOne()
    {
        var fd = new JSFunctionDef();
        fd.AddArg(new JSAtom(100));

        int idx = fd.FindArg(new JSAtom(999));

        Assert.Equal(-1, idx);
    }

    #endregion

    #region Scope Management

    [Fact]
    public void PushScope_CreatesNewScope()
    {
        var fd = new JSFunctionDef();

        int scopeIdx = fd.PushScope();

        Assert.Equal(0, scopeIdx);
        Assert.Equal(1, fd.Scopes.Count);
        Assert.Equal(0, fd.ScopeLevel);
    }

    [Fact]
    public void PushScope_MultipleLevels()
    {
        var fd = new JSFunctionDef();

        int scope0 = fd.PushScope();
        int scope1 = fd.PushScope();
        int scope2 = fd.PushScope();

        Assert.Equal(0, scope0);
        Assert.Equal(1, scope1);
        Assert.Equal(2, scope2);
        Assert.Equal(3, fd.Scopes.Count);
        Assert.Equal(2, fd.ScopeLevel);
    }

    [Fact]
    public void PopScope_RestoresParentScope()
    {
        var fd = new JSFunctionDef();
        fd.PushScope(); // scope 0
        fd.PushScope(); // scope 1

        fd.PopScope();

        Assert.Equal(0, fd.ScopeLevel);
    }

    [Fact]
    public void ScopeChain_LexicalVariables()
    {
        var fd = new JSFunctionDef();

        // Function scope - add a var (non-lexical)
        fd.AddVar(new JSAtom(100)); // var x - goes to scope 0

        // Enter first block scope
        fd.PushScope(); // Now at scope index 0
        
        // Enter nested block
        fd.PushScope(); // Now at scope index 1

        // Add lexical variable in nested block
        fd.AddVar(new JSAtom(101), isLexical: true); // let y

        // Verify the lexical variable has correct scope level (matches current scope)
        Assert.True(fd.Vars[1].IsLexical);
        Assert.Equal(1, fd.Vars[1].ScopeLevel); // At nested scope level

        fd.PopScope();
        fd.PopScope();
    }

    #endregion

    #region Label Management

    [Fact]
    public void NewLabel_ReturnsSequentialIndices()
    {
        var fd = new JSFunctionDef();

        int label0 = fd.NewLabel();
        int label1 = fd.NewLabel();
        int label2 = fd.NewLabel();

        Assert.Equal(0, label0);
        Assert.Equal(1, label1);
        Assert.Equal(2, label2);
        Assert.Equal(3, fd.Labels.Count);
    }

    #endregion

    #region Child Functions

    [Fact]
    public void CreateChild_SetsParent()
    {
        var parent = new JSFunctionDef();

        var child = parent.CreateChild();

        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public void CreateChild_CapturesParentScopeLevel()
    {
        var parent = new JSFunctionDef();
        parent.PushScope();
        parent.PushScope();

        var child = parent.CreateChild();

        Assert.Equal(parent.ScopeLevel, child.ParentScopeLevel);
    }

    [Fact]
    public void NestedFunctions_MultiLevel()
    {
        // Simulate: function outer() { function middle() { function inner() {} } }
        var outer = new JSFunctionDef { FuncName = new JSAtom(1) };
        var middle = outer.CreateChild();
        middle.FuncName = new JSAtom(2);
        var inner = middle.CreateChild();
        inner.FuncName = new JSAtom(3);

        Assert.Same(outer, middle.Parent);
        Assert.Same(middle, inner.Parent);
        Assert.Null(outer.Parent);
    }

    #endregion

    #region Integration

    [Fact]
    public void CompleteFunction_AllParts()
    {
        // Simulate compiling: function add(a, b) { let result = a + b; return result; }
        var fd = new JSFunctionDef
        {
            FuncName = new JSAtom(100), // "add"
            FuncKind = JSFunctionKind.Normal,
            FuncType = JSParseFunctionType.Statement,
            HasThisBinding = true,
        };

        // Arguments
        fd.AddArg(new JSAtom(101)); // a
        fd.AddArg(new JSAtom(102)); // b

        // Enter function body scope
        fd.PushScope();
        fd.BodyScope = fd.ScopeLevel;

        // Local variable
        int resultIdx = fd.AddVar(new JSAtom(103), isLexical: true); // let result

        // Emit bytecode (simplified)
        fd.ByteCode.EmitOp(OpCode.GetArg);
        fd.ByteCode.EmitArg(0); // a
        fd.ByteCode.EmitOp(OpCode.GetArg);
        fd.ByteCode.EmitArg(1); // b
        fd.ByteCode.EmitOp(OpCode.Add);
        fd.ByteCode.EmitOp(OpCode.PutLocCheck);
        fd.ByteCode.EmitLoc((ushort)resultIdx);
        fd.ByteCode.EmitOp(OpCode.GetLoc);
        fd.ByteCode.EmitLoc((ushort)resultIdx);
        fd.ByteCode.EmitOp(OpCode.Return);

        fd.PopScope();

        // Verify
        Assert.Equal(2, fd.Args.Count);
        Assert.Equal(1, fd.Vars.Count);
        Assert.True(fd.ByteCode.Size > 0);
    }

    [Fact]
    public void Closure_VariableCapture()
    {
        // Simulate: function outer() { let x = 1; return function() { return x; }; }
        var outer = new JSFunctionDef { FuncName = new JSAtom(100) };

        // Add variable x to outer function
        int xIdx = outer.AddVar(new JSAtom(101), isLexical: true);
        outer.Vars[xIdx].IsCaptured = true;
        outer.VarRefCount = 1;

        // Create inner function
        var inner = outer.CreateChild();
        inner.FuncType = JSParseFunctionType.Expression;

        // Inner function captures x
        inner.ClosureVars.Add(new JSClosureVar
        {
            Name = new JSAtom(101),
            ClosureType = JSClosureType.Local,
            VarIndex = xIdx,
            IsLexical = true,
        });

        // Verify
        Assert.True(outer.Vars[0].IsCaptured);
        Assert.Equal(1, inner.ClosureVars.Count);
        Assert.Equal(JSClosureType.Local, inner.ClosureVars[0].ClosureType);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShowsInfo()
    {
        var fd = new JSFunctionDef
        {
            FuncName = new JSAtom(100),
        };
        fd.AddArg(new JSAtom(1));
        fd.AddArg(new JSAtom(2));
        fd.AddVar(new JSAtom(3));
        fd.ByteCode.EmitOp(OpCode.Return);

        var str = fd.ToString();

        Assert.Contains("args=2", str);
        Assert.Contains("vars=1", str);
        Assert.Contains("bytecode=", str);
    }

    [Fact]
    public void ToString_AnonymousFunction()
    {
        var fd = new JSFunctionDef();

        var str = fd.ToString();

        Assert.Contains("anonymous", str);
    }

    #endregion
}
