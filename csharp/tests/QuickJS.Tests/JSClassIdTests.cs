// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for <see cref="JSClassId"/> and related extensions.
/// </summary>
public class JSClassIdTests
{
    #region Basic Values Tests

    [Fact]
    public void JSClassId_None_IsZero()
    {
        Assert.Equal(0, (int)JSClassId.None);
    }

    [Fact]
    public void JSClassId_Object_IsOne()
    {
        Assert.Equal(1, (int)JSClassId.Object);
    }

    [Fact]
    public void JSClassId_Array_IsTwo()
    {
        Assert.Equal(2, (int)JSClassId.Array);
    }

    [Fact]
    public void JSClassId_FirstUserClass_IsHighEnough()
    {
        // User classes should start after all built-in classes
        Assert.True((int)JSClassId.FirstUserClass >= 60);
    }

    #endregion

    #region IsFunction Tests

    [Theory]
    [InlineData(JSClassId.CFunction, true)]
    [InlineData(JSClassId.BytecodeFunction, true)]
    [InlineData(JSClassId.BoundFunction, true)]
    [InlineData(JSClassId.CFunctionData, true)]
    [InlineData(JSClassId.GeneratorFunction, true)]
    [InlineData(JSClassId.AsyncFunction, true)]
    [InlineData(JSClassId.AsyncGeneratorFunction, true)]
    [InlineData(JSClassId.Object, false)]
    [InlineData(JSClassId.Array, false)]
    [InlineData(JSClassId.Generator, false)]
    public void IsFunction_ReturnsCorrectly(JSClassId classId, bool expected)
    {
        Assert.Equal(expected, classId.IsFunction());
    }

    #endregion

    #region IsTypedArray Tests

    [Theory]
    [InlineData(JSClassId.Uint8ClampedArray, true)]
    [InlineData(JSClassId.Int8Array, true)]
    [InlineData(JSClassId.Uint8Array, true)]
    [InlineData(JSClassId.Int16Array, true)]
    [InlineData(JSClassId.Uint16Array, true)]
    [InlineData(JSClassId.Int32Array, true)]
    [InlineData(JSClassId.Uint32Array, true)]
    [InlineData(JSClassId.BigInt64Array, true)]
    [InlineData(JSClassId.BigUint64Array, true)]
    [InlineData(JSClassId.Float16Array, true)]
    [InlineData(JSClassId.Float32Array, true)]
    [InlineData(JSClassId.Float64Array, true)]
    [InlineData(JSClassId.Array, false)]
    [InlineData(JSClassId.ArrayBuffer, false)]
    [InlineData(JSClassId.DataView, false)]
    public void IsTypedArray_ReturnsCorrectly(JSClassId classId, bool expected)
    {
        Assert.Equal(expected, classId.IsTypedArray());
    }

    #endregion

    #region IsArrayLike Tests

    [Theory]
    [InlineData(JSClassId.Array, true)]
    [InlineData(JSClassId.Arguments, true)]
    [InlineData(JSClassId.MappedArguments, true)]
    [InlineData(JSClassId.Uint8Array, true)]
    [InlineData(JSClassId.Float64Array, true)]
    [InlineData(JSClassId.Object, false)]
    [InlineData(JSClassId.String, false)]
    public void IsArrayLike_ReturnsCorrectly(JSClassId classId, bool expected)
    {
        Assert.Equal(expected, classId.IsArrayLike());
    }

    #endregion

    #region IsPrimitiveWrapper Tests

    [Theory]
    [InlineData(JSClassId.Number, true)]
    [InlineData(JSClassId.String, true)]
    [InlineData(JSClassId.Boolean, true)]
    [InlineData(JSClassId.Symbol, true)]
    [InlineData(JSClassId.BigInt, true)]
    [InlineData(JSClassId.Object, false)]
    [InlineData(JSClassId.Array, false)]
    [InlineData(JSClassId.Date, false)]
    public void IsPrimitiveWrapper_ReturnsCorrectly(JSClassId classId, bool expected)
    {
        Assert.Equal(expected, classId.IsPrimitiveWrapper());
    }

    #endregion
}
