using Xunit;

namespace QuickJS.Tests;

public class JSValueTypeTests
{
    [Theory]
    [InlineData(JSValueType.BigInt, -9)]
    [InlineData(JSValueType.Symbol, -8)]
    [InlineData(JSValueType.String, -7)]
    [InlineData(JSValueType.StringRope, -6)]
    [InlineData(JSValueType.Module, -3)]
    [InlineData(JSValueType.FunctionBytecode, -2)]
    [InlineData(JSValueType.Object, -1)]
    [InlineData(JSValueType.Int, 0)]
    [InlineData(JSValueType.Bool, 1)]
    [InlineData(JSValueType.Null, 2)]
    [InlineData(JSValueType.Undefined, 3)]
    [InlineData(JSValueType.Uninitialized, 4)]
    [InlineData(JSValueType.CatchOffset, 5)]
    [InlineData(JSValueType.Exception, 6)]
    [InlineData(JSValueType.ShortBigInt, 7)]
    [InlineData(JSValueType.Float64, 8)]
    public void EnumValues_MatchQuickJSTags(JSValueType type, int expectedValue)
    {
        // The enum values should match the JS_TAG_* constants from quickjs.h
        Assert.Equal(expectedValue, (int)type);
    }

    [Theory]
    [InlineData(JSValueType.BigInt, true)]
    [InlineData(JSValueType.Symbol, true)]
    [InlineData(JSValueType.String, true)]
    [InlineData(JSValueType.StringRope, true)]
    [InlineData(JSValueType.Module, true)]
    [InlineData(JSValueType.FunctionBytecode, true)]
    [InlineData(JSValueType.Object, true)]
    [InlineData(JSValueType.Int, false)]
    [InlineData(JSValueType.Bool, false)]
    [InlineData(JSValueType.Null, false)]
    [InlineData(JSValueType.Undefined, false)]
    [InlineData(JSValueType.Float64, false)]
    public void IsHeapAllocated_ReturnsCorrectValue(JSValueType type, bool expected)
    {
        Assert.Equal(expected, type.IsHeapAllocated());
    }

    [Theory]
    [InlineData(JSValueType.Int, true)]
    [InlineData(JSValueType.Bool, true)]
    [InlineData(JSValueType.Null, true)]
    [InlineData(JSValueType.Undefined, true)]
    [InlineData(JSValueType.Float64, true)]
    [InlineData(JSValueType.Object, false)]
    [InlineData(JSValueType.String, false)]
    [InlineData(JSValueType.BigInt, false)]
    public void IsImmediate_ReturnsCorrectValue(JSValueType type, bool expected)
    {
        Assert.Equal(expected, type.IsImmediate());
    }

    [Theory]
    [InlineData(JSValueType.Int, true)]
    [InlineData(JSValueType.Float64, true)]
    [InlineData(JSValueType.BigInt, true)]
    [InlineData(JSValueType.ShortBigInt, true)]
    [InlineData(JSValueType.String, false)]
    [InlineData(JSValueType.Bool, false)]
    [InlineData(JSValueType.Object, false)]
    [InlineData(JSValueType.Null, false)]
    [InlineData(JSValueType.Undefined, false)]
    public void IsNumber_ReturnsCorrectValue(JSValueType type, bool expected)
    {
        Assert.Equal(expected, type.IsNumber());
    }

    [Theory]
    [InlineData(JSValueType.Module, true)]
    [InlineData(JSValueType.FunctionBytecode, true)]
    [InlineData(JSValueType.CatchOffset, true)]
    [InlineData(JSValueType.Uninitialized, true)]
    [InlineData(JSValueType.StringRope, true)]
    [InlineData(JSValueType.Object, false)]
    [InlineData(JSValueType.String, false)]
    [InlineData(JSValueType.Int, false)]
    public void IsInternal_ReturnsCorrectValue(JSValueType type, bool expected)
    {
        Assert.Equal(expected, type.IsInternal());
    }

    [Fact]
    public void IsHeapAllocated_And_IsImmediate_AreMutuallyExclusive()
    {
        foreach (JSValueType type in Enum.GetValues<JSValueType>())
        {
            // A type should never be both heap-allocated and immediate
            Assert.NotEqual(type.IsHeapAllocated(), type.IsImmediate());
        }
    }
}
