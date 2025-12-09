// Copyright (c) the QuickJS.NET contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for ArrayBuffer, TypedArray, and DataView implementations.
/// </summary>
public class TypedArraysTests
{
    #region ArrayBuffer Tests

    [Fact]
    public void ArrayBuffer_Constructor_CreatesBuffer()
    {
        var buffer = new JSArrayBuffer(16);
        Assert.Equal(16, buffer.ByteLength);
        Assert.False(buffer.IsDetached);
        Assert.False(buffer.Resizable);
    }

    [Fact]
    public void ArrayBuffer_Constructor_ZeroLength()
    {
        var buffer = new JSArrayBuffer(0);
        Assert.Equal(0, buffer.ByteLength);
        Assert.False(buffer.IsDetached);
    }

    [Fact]
    public void ArrayBuffer_Slice_Basic()
    {
        var buffer = new JSArrayBuffer(10);
        // Set some data
        buffer.SetByte(0, 1);
        buffer.SetByte(1, 2);
        buffer.SetByte(2, 3);
        buffer.SetByte(3, 4);
        buffer.SetByte(4, 5);

        var sliced = buffer.Slice(1, 4);
        Assert.Equal(3, sliced.ByteLength);
        Assert.Equal(2, sliced.GetByte(0));
        Assert.Equal(3, sliced.GetByte(1));
        Assert.Equal(4, sliced.GetByte(2));
    }

    [Fact]
    public void ArrayBuffer_Slice_NegativeIndices()
    {
        var buffer = new JSArrayBuffer(10);
        buffer.SetByte(7, 42);
        buffer.SetByte(8, 43);
        buffer.SetByte(9, 44);

        var sliced = buffer.Slice(-3);
        Assert.Equal(3, sliced.ByteLength);
        Assert.Equal(42, sliced.GetByte(0));
        Assert.Equal(43, sliced.GetByte(1));
        Assert.Equal(44, sliced.GetByte(2));
    }

    [Fact]
    public void ArrayBuffer_Detach()
    {
        var buffer = new JSArrayBuffer(16);
        buffer.SetByte(0, 42);
        
        var data = buffer.Detach();
        Assert.True(buffer.IsDetached);
        Assert.Equal(0, buffer.ByteLength);
        Assert.Equal(16, data.Length);
        Assert.Equal(42, data[0]);
    }

    [Fact]
    public void ArrayBuffer_Transfer()
    {
        var original = new JSArrayBuffer(10);
        original.SetByte(0, 1);
        original.SetByte(1, 2);

        var transferred = original.Transfer();
        Assert.True(original.IsDetached);
        Assert.False(transferred.IsDetached);
        Assert.Equal(10, transferred.ByteLength);
        Assert.Equal(1, transferred.GetByte(0));
        Assert.Equal(2, transferred.GetByte(1));
    }

    [Fact]
    public void ArrayBuffer_Transfer_WithResize()
    {
        var original = new JSArrayBuffer(10);
        original.SetByte(0, 42);

        var transferred = original.Transfer(5);
        Assert.True(original.IsDetached);
        Assert.Equal(5, transferred.ByteLength);
        Assert.Equal(42, transferred.GetByte(0));
    }

    [Fact]
    public void ArrayBuffer_Resizable()
    {
        var buffer = new JSArrayBuffer(10, 100);
        Assert.True(buffer.Resizable);
        Assert.Equal(10, buffer.ByteLength);
        Assert.Equal(100, buffer.MaxByteLength);

        buffer.Resize(50);
        Assert.Equal(50, buffer.ByteLength);

        buffer.Resize(10);
        Assert.Equal(10, buffer.ByteLength);
    }

    #endregion

    #region TypedArray Tests

    [Fact]
    public void Int8Array_Constructor_WithLength()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int8, 10);
        Assert.Equal(10, arr.Length);
        Assert.Equal(1, arr.BytesPerElement);
        Assert.Equal(10, arr.ByteLength);
        Assert.Equal(0, arr.ByteOffset);
    }

    [Fact]
    public void Uint8Array_SetAndGet()
    {
        var arr = new JSTypedArray(TypedArrayKind.Uint8, 4);
        arr.SetElement(0, JSValue.FromInt32(255));
        arr.SetElement(1, JSValue.FromInt32(128));
        arr.SetElement(2, JSValue.FromInt32(0));
        arr.SetElement(3, JSValue.FromInt32(300)); // Truncated

        Assert.Equal(255, arr.GetElement(0).ToInt32());
        Assert.Equal(128, arr.GetElement(1).ToInt32());
        Assert.Equal(0, arr.GetElement(2).ToInt32());
        Assert.Equal(44, arr.GetElement(3).ToInt32()); // 300 & 0xFF = 44
    }

    [Fact]
    public void Uint8ClampedArray_Clamping()
    {
        var arr = new JSTypedArray(TypedArrayKind.Uint8Clamped, 4);
        arr.SetElement(0, JSValue.FromInt32(255));
        arr.SetElement(1, JSValue.FromInt32(300)); // Clamped to 255
        arr.SetElement(2, JSValue.FromInt32(-50)); // Clamped to 0
        arr.SetElement(3, JSValue.FromDouble(127.6)); // Rounded to 128

        Assert.Equal(255, arr.GetElement(0).ToInt32());
        Assert.Equal(255, arr.GetElement(1).ToInt32());
        Assert.Equal(0, arr.GetElement(2).ToInt32());
        Assert.Equal(128, arr.GetElement(3).ToInt32());
    }

    [Fact]
    public void Int16Array_ByteOrder()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int16, 2);
        arr.SetElement(0, JSValue.FromInt32(0x1234));
        arr.SetElement(1, JSValue.FromInt32(-1));

        Assert.Equal(0x1234, arr.GetElement(0).ToInt32());
        Assert.Equal(-1, arr.GetElement(1).ToInt32());
        Assert.Equal(2, arr.BytesPerElement);
        Assert.Equal(4, arr.ByteLength);
    }

    [Fact]
    public void Int32Array_Operations()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 3);
        arr.SetElement(0, JSValue.FromInt32(int.MaxValue));
        arr.SetElement(1, JSValue.FromInt32(int.MinValue));
        arr.SetElement(2, JSValue.FromInt32(0));

        Assert.Equal(int.MaxValue, arr.GetElement(0).ToInt32());
        Assert.Equal(int.MinValue, arr.GetElement(1).ToInt32());
        Assert.Equal(0, arr.GetElement(2).ToInt32());
    }

    [Fact]
    public void Float32Array_Operations()
    {
        var arr = new JSTypedArray(TypedArrayKind.Float32, 3);
        arr.SetElement(0, JSValue.FromDouble(3.14159f));
        arr.SetElement(1, JSValue.FromDouble(float.PositiveInfinity));
        arr.SetElement(2, JSValue.FromDouble(float.NaN));

        Assert.True(Math.Abs(3.14159 - arr.GetElement(0).ToDouble()) < 0.0001);
        Assert.True(double.IsPositiveInfinity(arr.GetElement(1).ToDouble()));
        Assert.True(double.IsNaN(arr.GetElement(2).ToDouble()));
    }

    [Fact]
    public void Float64Array_Operations()
    {
        var arr = new JSTypedArray(TypedArrayKind.Float64, 2);
        arr.SetElement(0, JSValue.FromDouble(Math.PI));
        arr.SetElement(1, JSValue.FromDouble(Math.E));

        Assert.Equal(Math.PI, arr.GetElement(0).ToDouble(), 10);
        Assert.Equal(Math.E, arr.GetElement(1).ToDouble(), 10);
    }

    [Fact]
    public void TypedArray_FromBuffer()
    {
        var buffer = new JSArrayBuffer(16);
        var view = new JSTypedArray(TypedArrayKind.Int32, buffer, 4, 2);

        Assert.Equal(2, view.Length);
        Assert.Equal(4, view.ByteOffset);
        Assert.Equal(8, view.ByteLength);
        Assert.Same(buffer, view.Buffer);
    }

    [Fact]
    public void TypedArray_SharedBuffer()
    {
        var buffer = new JSArrayBuffer(16);
        var view1 = new JSTypedArray(TypedArrayKind.Int32, buffer);
        var view2 = new JSTypedArray(TypedArrayKind.Uint8, buffer);

        view1.SetElement(0, JSValue.FromInt32(0x04030201));

        // Check that both views see the same data
        Assert.Equal(1, view2.GetElement(0).ToInt32());
        Assert.Equal(2, view2.GetElement(1).ToInt32());
        Assert.Equal(3, view2.GetElement(2).ToInt32());
        Assert.Equal(4, view2.GetElement(3).ToInt32());
    }

    [Fact]
    public void TypedArray_Slice()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        for (int i = 0; i < 5; i++)
            arr.SetElement(i, JSValue.FromInt32(i * 10));

        var sliced = arr.Slice(1, 4);
        Assert.Equal(3, sliced.Length);
        Assert.Equal(10, sliced.GetElement(0).ToInt32());
        Assert.Equal(20, sliced.GetElement(1).ToInt32());
        Assert.Equal(30, sliced.GetElement(2).ToInt32());
    }

    [Fact]
    public void TypedArray_Subarray()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        for (int i = 0; i < 5; i++)
            arr.SetElement(i, JSValue.FromInt32(i * 10));

        var sub = arr.Subarray(1, 4);
        Assert.Equal(3, sub.Length);
        Assert.Same(arr.Buffer, sub.Buffer);
        Assert.Equal(4, sub.ByteOffset); // 1 * 4 bytes per int32
    }

    [Fact]
    public void TypedArray_Fill()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        arr.Fill(JSValue.FromInt32(42));

        for (int i = 0; i < 5; i++)
            Assert.Equal(42, arr.GetElement(i).ToInt32());
    }

    [Fact]
    public void TypedArray_Fill_Partial()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        arr.Fill(JSValue.FromInt32(0));
        arr.Fill(JSValue.FromInt32(99), 2, 4);

        Assert.Equal(0, arr.GetElement(0).ToInt32());
        Assert.Equal(0, arr.GetElement(1).ToInt32());
        Assert.Equal(99, arr.GetElement(2).ToInt32());
        Assert.Equal(99, arr.GetElement(3).ToInt32());
        Assert.Equal(0, arr.GetElement(4).ToInt32());
    }

    [Fact]
    public void TypedArray_CopyWithin()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        for (int i = 0; i < 5; i++)
            arr.SetElement(i, JSValue.FromInt32(i));

        arr.CopyWithin(3, 0, 2);

        Assert.Equal(0, arr.GetElement(0).ToInt32());
        Assert.Equal(1, arr.GetElement(1).ToInt32());
        Assert.Equal(2, arr.GetElement(2).ToInt32());
        Assert.Equal(0, arr.GetElement(3).ToInt32()); // Copied from index 0
        Assert.Equal(1, arr.GetElement(4).ToInt32()); // Copied from index 1
    }

    [Fact]
    public void TypedArray_Reverse()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        for (int i = 0; i < 5; i++)
            arr.SetElement(i, JSValue.FromInt32(i));

        arr.Reverse();

        Assert.Equal(4, arr.GetElement(0).ToInt32());
        Assert.Equal(3, arr.GetElement(1).ToInt32());
        Assert.Equal(2, arr.GetElement(2).ToInt32());
        Assert.Equal(1, arr.GetElement(3).ToInt32());
        Assert.Equal(0, arr.GetElement(4).ToInt32());
    }

    [Fact]
    public void TypedArray_Sort()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        arr.SetElement(0, JSValue.FromInt32(30));
        arr.SetElement(1, JSValue.FromInt32(10));
        arr.SetElement(2, JSValue.FromInt32(50));
        arr.SetElement(3, JSValue.FromInt32(20));
        arr.SetElement(4, JSValue.FromInt32(40));

        arr.Sort();

        Assert.Equal(10, arr.GetElement(0).ToInt32());
        Assert.Equal(20, arr.GetElement(1).ToInt32());
        Assert.Equal(30, arr.GetElement(2).ToInt32());
        Assert.Equal(40, arr.GetElement(3).ToInt32());
        Assert.Equal(50, arr.GetElement(4).ToInt32());
    }

    [Fact]
    public void TypedArray_IndexOf()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        for (int i = 0; i < 5; i++)
            arr.SetElement(i, JSValue.FromInt32(i * 10));

        Assert.Equal(2, arr.IndexOf(JSValue.FromInt32(20)));
        Assert.Equal(-1, arr.IndexOf(JSValue.FromInt32(25)));
        Assert.Equal(-1, arr.IndexOf(JSValue.FromInt32(20), 3));
    }

    [Fact]
    public void TypedArray_LastIndexOf()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        arr.SetElement(0, JSValue.FromInt32(10));
        arr.SetElement(1, JSValue.FromInt32(20));
        arr.SetElement(2, JSValue.FromInt32(10));
        arr.SetElement(3, JSValue.FromInt32(20));
        arr.SetElement(4, JSValue.FromInt32(10));

        Assert.Equal(4, arr.LastIndexOf(JSValue.FromInt32(10)));
        Assert.Equal(2, arr.LastIndexOf(JSValue.FromInt32(10), 3));
    }

    [Fact]
    public void TypedArray_Includes()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 3);
        arr.SetElement(0, JSValue.FromInt32(1));
        arr.SetElement(1, JSValue.FromInt32(2));
        arr.SetElement(2, JSValue.FromInt32(3));

        Assert.True(arr.Includes(JSValue.FromInt32(2)));
        Assert.False(arr.Includes(JSValue.FromInt32(4)));
    }

    [Fact]
    public void TypedArray_Join()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 3);
        arr.SetElement(0, JSValue.FromInt32(1));
        arr.SetElement(1, JSValue.FromInt32(2));
        arr.SetElement(2, JSValue.FromInt32(3));

        Assert.Equal("1,2,3", arr.Join());
        Assert.Equal("1-2-3", arr.Join("-"));
    }

    [Fact]
    public void TypedArray_Set_FromArray()
    {
        var arr = new JSTypedArray(TypedArrayKind.Int32, 5);
        arr.Set(new[] { JSValue.FromInt32(10), JSValue.FromInt32(20), JSValue.FromInt32(30) }, 1);

        Assert.Equal(0, arr.GetElement(0).ToInt32());
        Assert.Equal(10, arr.GetElement(1).ToInt32());
        Assert.Equal(20, arr.GetElement(2).ToInt32());
        Assert.Equal(30, arr.GetElement(3).ToInt32());
        Assert.Equal(0, arr.GetElement(4).ToInt32());
    }

    [Fact]
    public void TypedArray_BytesPerElement()
    {
        Assert.Equal(1, JSTypedArray.GetBytesPerElement(TypedArrayKind.Int8));
        Assert.Equal(1, JSTypedArray.GetBytesPerElement(TypedArrayKind.Uint8));
        Assert.Equal(1, JSTypedArray.GetBytesPerElement(TypedArrayKind.Uint8Clamped));
        Assert.Equal(2, JSTypedArray.GetBytesPerElement(TypedArrayKind.Int16));
        Assert.Equal(2, JSTypedArray.GetBytesPerElement(TypedArrayKind.Uint16));
        Assert.Equal(4, JSTypedArray.GetBytesPerElement(TypedArrayKind.Int32));
        Assert.Equal(4, JSTypedArray.GetBytesPerElement(TypedArrayKind.Uint32));
        Assert.Equal(4, JSTypedArray.GetBytesPerElement(TypedArrayKind.Float32));
        Assert.Equal(8, JSTypedArray.GetBytesPerElement(TypedArrayKind.Float64));
        Assert.Equal(8, JSTypedArray.GetBytesPerElement(TypedArrayKind.BigInt64));
        Assert.Equal(8, JSTypedArray.GetBytesPerElement(TypedArrayKind.BigUint64));
    }

    #endregion

    #region DataView Tests

    [Fact]
    public void DataView_Constructor()
    {
        var buffer = new JSArrayBuffer(16);
        var view = new JSDataView(buffer);

        Assert.Same(buffer, view.Buffer);
        Assert.Equal(0, view.ByteOffset);
        Assert.Equal(16, view.ByteLength);
    }

    [Fact]
    public void DataView_Constructor_WithOffset()
    {
        var buffer = new JSArrayBuffer(16);
        var view = new JSDataView(buffer, 4);

        Assert.Equal(4, view.ByteOffset);
        Assert.Equal(12, view.ByteLength);
    }

    [Fact]
    public void DataView_Constructor_WithOffsetAndLength()
    {
        var buffer = new JSArrayBuffer(16);
        var view = new JSDataView(buffer, 4, 8);

        Assert.Equal(4, view.ByteOffset);
        Assert.Equal(8, view.ByteLength);
    }

    [Fact]
    public void DataView_Int8()
    {
        var buffer = new JSArrayBuffer(4);
        var view = new JSDataView(buffer);

        view.SetInt8(0, -128);
        view.SetInt8(1, 127);
        view.SetUint8(2, 255);
        view.SetUint8(3, 0);

        Assert.Equal(-128, view.GetInt8(0));
        Assert.Equal(127, view.GetInt8(1));
        Assert.Equal(255, view.GetUint8(2));
        Assert.Equal(0, view.GetUint8(3));
    }

    [Fact]
    public void DataView_Int16_LittleEndian()
    {
        var buffer = new JSArrayBuffer(4);
        var view = new JSDataView(buffer);

        view.SetInt16(0, 0x0102, littleEndian: true);
        view.SetInt16(2, 0x0102, littleEndian: false);

        // Little endian: low byte first
        Assert.Equal(2, view.GetUint8(0));
        Assert.Equal(1, view.GetUint8(1));

        // Big endian: high byte first
        Assert.Equal(1, view.GetUint8(2));
        Assert.Equal(2, view.GetUint8(3));

        Assert.Equal(0x0102, view.GetInt16(0, littleEndian: true));
        Assert.Equal(0x0102, view.GetInt16(2, littleEndian: false));
    }

    [Fact]
    public void DataView_Int32_Endianness()
    {
        var buffer = new JSArrayBuffer(8);
        var view = new JSDataView(buffer);

        view.SetInt32(0, 0x01020304, littleEndian: true);
        view.SetInt32(4, 0x01020304, littleEndian: false);

        // Little endian
        Assert.Equal(4, view.GetUint8(0));
        Assert.Equal(3, view.GetUint8(1));
        Assert.Equal(2, view.GetUint8(2));
        Assert.Equal(1, view.GetUint8(3));

        // Big endian
        Assert.Equal(1, view.GetUint8(4));
        Assert.Equal(2, view.GetUint8(5));
        Assert.Equal(3, view.GetUint8(6));
        Assert.Equal(4, view.GetUint8(7));
    }

    [Fact]
    public void DataView_Float32()
    {
        var buffer = new JSArrayBuffer(4);
        var view = new JSDataView(buffer);

        view.SetFloat32(0, 3.14159f, littleEndian: true);
        var result = view.GetFloat32(0, littleEndian: true);

        Assert.True(Math.Abs(3.14159f - result) < 0.00001);
    }

    [Fact]
    public void DataView_Float64()
    {
        var buffer = new JSArrayBuffer(8);
        var view = new JSDataView(buffer);

        view.SetFloat64(0, Math.PI, littleEndian: true);
        var result = view.GetFloat64(0, littleEndian: true);

        Assert.Equal(Math.PI, result, 10);
    }

    [Fact]
    public void DataView_BigInt64()
    {
        var buffer = new JSArrayBuffer(16);
        var view = new JSDataView(buffer);

        view.SetBigInt64(0, long.MaxValue, littleEndian: true);
        view.SetBigInt64(8, long.MinValue, littleEndian: false);

        Assert.Equal(long.MaxValue, view.GetBigInt64(0, littleEndian: true));
        Assert.Equal(long.MinValue, view.GetBigInt64(8, littleEndian: false));
    }

    [Fact]
    public void DataView_BigUint64()
    {
        var buffer = new JSArrayBuffer(8);
        var view = new JSDataView(buffer);

        view.SetBigUint64(0, ulong.MaxValue, littleEndian: true);
        Assert.Equal(ulong.MaxValue, view.GetBigUint64(0, littleEndian: true));
    }

    #endregion

    #region JSContext Integration Tests

    [Fact]
    public void Context_ArrayBuffer_Constructor()
    {
        using var rt = new JSRuntime();
        using var ctx = rt.CreateContext();

        var global = ctx.GlobalObject;
        var arrayBufferCtor = global.Get("ArrayBuffer");
        Assert.True(arrayBufferCtor.IsObject);
    }

    [Fact]
    public void Context_Int8Array_Constructor()
    {
        using var rt = new JSRuntime();
        using var ctx = rt.CreateContext();

        var global = ctx.GlobalObject;
        var int8ArrayCtor = global.Get("Int8Array");
        Assert.True(int8ArrayCtor.IsObject);
    }

    [Fact]
    public void Context_AllTypedArrays_Registered()
    {
        using var rt = new JSRuntime();
        using var ctx = rt.CreateContext();

        var global = ctx.GlobalObject;
        
        var typedArrayNames = new[]
        {
            "Int8Array", "Uint8Array", "Uint8ClampedArray",
            "Int16Array", "Uint16Array",
            "Int32Array", "Uint32Array",
            "Float32Array", "Float64Array",
            "BigInt64Array", "BigUint64Array",
            "DataView", "ArrayBuffer"
        };

        foreach (var name in typedArrayNames)
        {
            var ctor = global.Get(name);
            Assert.True(ctor.IsObject, $"{name} should be registered");
        }
    }

    [Fact]
    public void Context_TypedArray_BYTES_PER_ELEMENT()
    {
        using var rt = new JSRuntime();
        using var ctx = rt.CreateContext();

        var global = ctx.GlobalObject;
        
        var int32ArrayCtor = global.Get("Int32Array").AsObject();
        var bytesPerElement = int32ArrayCtor.Get("BYTES_PER_ELEMENT");
        Assert.Equal(4, bytesPerElement.ToInt32());

        var float64ArrayCtor = global.Get("Float64Array").AsObject();
        bytesPerElement = float64ArrayCtor.Get("BYTES_PER_ELEMENT");
        Assert.Equal(8, bytesPerElement.ToInt32());
    }

    [Fact]
    public void Context_ArrayBuffer_isView()
    {
        using var rt = new JSRuntime();
        using var ctx = rt.CreateContext();

        var global = ctx.GlobalObject;
        var arrayBufferCtor = global.Get("ArrayBuffer").AsObject();
        var isViewFn = arrayBufferCtor.Get("isView");
        Assert.True(isViewFn.IsObject);
    }

    #endregion
}
