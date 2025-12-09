// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSVarRef"/> - closure variable references.
/// </summary>
public class JSVarRefTests
{
    #region Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesDetachedUndefined()
    {
        var varRef = new JSVarRef();

        Assert.True(varRef.IsDetached);
        Assert.False(varRef.IsLexical);
        Assert.False(varRef.IsConst);
        Assert.True(varRef.Value.IsUndefined);
        Assert.Equal(-1, varRef.VarIndex);
    }

    [Fact]
    public void StackConstructor_CreatesNonDetachedReference()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(42), JSValue.FromString("test") };
        var varRef = new JSVarRef(stackVars, 0);

        Assert.False(varRef.IsDetached);
        Assert.False(varRef.IsLexical);
        Assert.False(varRef.IsConst);
        Assert.Equal(0, varRef.VarIndex);
        Assert.Equal(42, varRef.Value.ToInt32());
    }

    [Fact]
    public void StackConstructor_WithLexicalFlag()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(10) };
        var varRef = new JSVarRef(stackVars, 0, isLexical: true);

        Assert.True(varRef.IsLexical);
        Assert.False(varRef.IsConst);
    }

    [Fact]
    public void StackConstructor_WithConstFlag()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(10) };
        var varRef = new JSVarRef(stackVars, 0, isLexical: true, isConst: true);

        Assert.True(varRef.IsLexical);
        Assert.True(varRef.IsConst);
    }

    [Fact]
    public void ValueConstructor_CreatesDetachedWithValue()
    {
        var varRef = new JSVarRef(JSValue.FromDouble(3.14));

        Assert.True(varRef.IsDetached);
        Assert.Equal(3.14, varRef.Value.ToDouble());
    }

    [Fact]
    public void ValueConstructor_WithLexicalAndConstFlags()
    {
        var varRef = new JSVarRef(JSValue.FromString("constant"), isLexical: true, isConst: true);

        Assert.True(varRef.IsDetached);
        Assert.True(varRef.IsLexical);
        Assert.True(varRef.IsConst);
        Assert.Equal("constant", varRef.Value.ToString());
    }

    #endregion

    #region Stack Variable Access Tests

    [Fact]
    public void Value_ReadsFromStack_WhenNotDetached()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(100), JSValue.FromInt32(200) };
        var varRef = new JSVarRef(stackVars, 1);

        Assert.Equal(200, varRef.Value.ToInt32());
    }

    [Fact]
    public void Value_WritesToStack_WhenNotDetached()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(100), JSValue.FromInt32(200) };
        var varRef = new JSVarRef(stackVars, 0);

        varRef.Value = JSValue.FromInt32(999);

        Assert.Equal(999, stackVars[0].ToInt32());
        Assert.Equal(999, varRef.Value.ToInt32());
    }

    [Fact]
    public void Value_ReflectsExternalStackChanges()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(1) };
        var varRef = new JSVarRef(stackVars, 0);

        // External code modifies the stack
        stackVars[0] = JSValue.FromInt32(2);

        // VarRef should see the change
        Assert.Equal(2, varRef.Value.ToInt32());
    }

    [Fact]
    public void Value_ReturnsUndefined_WhenStackIndexInvalid()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(1) };
        var varRef = new JSVarRef(stackVars, 5); // Invalid index

        Assert.True(varRef.Value.IsUndefined);
    }

    #endregion

    #region Detach Tests

    [Fact]
    public void Detach_CopiesValueFromStack()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(42) };
        var varRef = new JSVarRef(stackVars, 0);

        Assert.False(varRef.IsDetached);

        varRef.Detach();

        Assert.True(varRef.IsDetached);
        Assert.Equal(42, varRef.Value.ToInt32());
        Assert.Equal(-1, varRef.VarIndex);
    }

    [Fact]
    public void Detach_IsolatesFromStackChanges()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(100) };
        var varRef = new JSVarRef(stackVars, 0);

        varRef.Detach();

        // Change the stack after detaching
        stackVars[0] = JSValue.FromInt32(999);

        // VarRef should still have the old value
        Assert.Equal(100, varRef.Value.ToInt32());
    }

    [Fact]
    public void Detach_IsIdempotent()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(50) };
        var varRef = new JSVarRef(stackVars, 0);

        varRef.Detach();
        varRef.Value = JSValue.FromInt32(60);
        varRef.Detach(); // Should not reset the value

        Assert.Equal(60, varRef.Value.ToInt32());
    }

    [Fact]
    public void Detach_HandlesInvalidIndex()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(1) };
        var varRef = new JSVarRef(stackVars, 10); // Invalid index

        varRef.Detach();

        Assert.True(varRef.IsDetached);
        Assert.True(varRef.Value.IsUndefined);
    }

    #endregion

    #region Const Variable Tests

    [Fact]
    public void Value_IgnoresSetOnConstVariable()
    {
        var varRef = new JSVarRef(JSValue.FromInt32(10), isConst: true);

        varRef.Value = JSValue.FromInt32(20);

        // Should still be 10 because it's const
        Assert.Equal(10, varRef.Value.ToInt32());
    }

    [Fact]
    public void Value_IgnoresSetOnConstStackVariable()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(10) };
        var varRef = new JSVarRef(stackVars, 0, isConst: true);

        varRef.Value = JSValue.FromInt32(20);

        // Should still be 10 because it's const
        Assert.Equal(10, varRef.Value.ToInt32());
        Assert.Equal(10, stackVars[0].ToInt32());
    }

    [Fact]
    public void TrySetValue_ReturnsFalseForConst()
    {
        var varRef = new JSVarRef(JSValue.FromInt32(10), isConst: true);

        bool result = varRef.TrySetValue(JSValue.FromInt32(20));

        Assert.False(result);
        Assert.Equal(10, varRef.Value.ToInt32());
    }

    [Fact]
    public void TrySetValue_ReturnsTrueForNonConst()
    {
        var varRef = new JSVarRef(JSValue.FromInt32(10));

        bool result = varRef.TrySetValue(JSValue.FromInt32(20));

        Assert.True(result);
        Assert.Equal(20, varRef.Value.ToInt32());
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_CreatesDetachedCopy()
    {
        var stackVars = new JSValue[] { JSValue.FromInt32(42) };
        var varRef = new JSVarRef(stackVars, 0);

        var clone = varRef.Clone();

        Assert.True(clone.IsDetached);
        Assert.Equal(42, clone.Value.ToInt32());
    }

    [Fact]
    public void Clone_PreservesFlags()
    {
        var varRef = new JSVarRef(JSValue.FromInt32(10), isLexical: true, isConst: true);

        var clone = varRef.Clone();

        Assert.True(clone.IsLexical);
        Assert.True(clone.IsConst);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var varRef = new JSVarRef(JSValue.FromInt32(10));

        var clone = varRef.Clone();
        varRef.Value = JSValue.FromInt32(20);

        Assert.Equal(20, varRef.Value.ToInt32());
        Assert.Equal(10, clone.Value.ToInt32());
    }

    #endregion

    #region Closure Scenario Tests

    [Fact]
    public void ClosureScenario_InnerFunctionAccessesOuterVariable()
    {
        // Simulates:
        // function outer() {
        //     let x = 10;
        //     return function inner() { return x; };
        // }
        // let f = outer();
        // f(); // Should return 10

        // Outer function's stack
        var outerStack = new JSValue[] { JSValue.FromInt32(10) };

        // Create var ref while outer is executing
        var xRef = new JSVarRef(outerStack, 0, isLexical: true);

        // Value is accessible
        Assert.Equal(10, xRef.Value.ToInt32());

        // Outer function returns - detach the var ref
        xRef.Detach();

        // Clear the stack (simulating stack pop)
        outerStack[0] = JSValue.Undefined;

        // Inner function can still access the value
        Assert.Equal(10, xRef.Value.ToInt32());
    }

    [Fact]
    public void ClosureScenario_MultipleClosuresShareVariable()
    {
        // Simulates:
        // function outer() {
        //     let counter = 0;
        //     return {
        //         inc: function() { counter++; },
        //         get: function() { return counter; }
        //     };
        // }

        var outerStack = new JSValue[] { JSValue.FromInt32(0) };

        // Both closures share the same var ref
        var counterRef = new JSVarRef(outerStack, 0, isLexical: true);

        // Simulate incrementing from one closure
        counterRef.Value = JSValue.FromInt32(counterRef.Value.ToInt32() + 1);

        // Both see the new value
        Assert.Equal(1, counterRef.Value.ToInt32());

        // After outer returns
        counterRef.Detach();

        // Still works and is shared
        counterRef.Value = JSValue.FromInt32(counterRef.Value.ToInt32() + 1);
        Assert.Equal(2, counterRef.Value.ToInt32());
    }

    [Fact]
    public void ClosureScenario_NestedClosures()
    {
        // Simulates:
        // function a() {
        //     let x = 1;
        //     return function b() {
        //         let y = 2;
        //         return function c() {
        //             return x + y;
        //         };
        //     };
        // }

        // Stack for function a
        var aStack = new JSValue[] { JSValue.FromInt32(1) };
        var xRef = new JSVarRef(aStack, 0, isLexical: true);

        // Stack for function b
        var bStack = new JSValue[] { JSValue.FromInt32(2) };
        var yRef = new JSVarRef(bStack, 0, isLexical: true);

        // Function a returns - detach x
        xRef.Detach();
        aStack[0] = JSValue.Undefined;

        // Function c can still access x
        Assert.Equal(1, xRef.Value.ToInt32());

        // Function b returns - detach y
        yRef.Detach();
        bStack[0] = JSValue.Undefined;

        // Function c can access both
        Assert.Equal(1, xRef.Value.ToInt32());
        Assert.Equal(2, yRef.Value.ToInt32());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void VarRef_HandlesNullStackArray()
    {
        // This tests the null-safety of the internal implementation
        var varRef = new JSVarRef();

        // Should not throw
        var value = varRef.Value;
        varRef.Value = JSValue.FromInt32(10);

        Assert.True(varRef.IsDetached);
    }

    [Fact]
    public void VarRef_HandlesAllJSValueTypes()
    {
        // Test with different value types
        var values = new[]
        {
            JSValue.Undefined,
            JSValue.Null,
            JSValue.True,
            JSValue.False,
            JSValue.FromInt32(42),
            JSValue.FromDouble(3.14),
            JSValue.FromString("hello"),
        };

        foreach (var value in values)
        {
            var varRef = new JSVarRef(value);
            Assert.Equal(value.Tag, varRef.Value.Tag);
        }
    }

    #endregion
}
