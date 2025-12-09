// Copyright (c) the QuickJS.NET contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace QuickJS
{
    /// <summary>
    /// Defines the element types for typed arrays.
    /// </summary>
    public enum TypedArrayKind
    {
        /// <summary>Int8Array - 8-bit signed integer</summary>
        Int8,
        /// <summary>Uint8Array - 8-bit unsigned integer</summary>
        Uint8,
        /// <summary>Uint8ClampedArray - 8-bit unsigned integer (clamped)</summary>
        Uint8Clamped,
        /// <summary>Int16Array - 16-bit signed integer</summary>
        Int16,
        /// <summary>Uint16Array - 16-bit unsigned integer</summary>
        Uint16,
        /// <summary>Int32Array - 32-bit signed integer</summary>
        Int32,
        /// <summary>Uint32Array - 32-bit unsigned integer</summary>
        Uint32,
        /// <summary>Float32Array - 32-bit IEEE floating point</summary>
        Float32,
        /// <summary>Float64Array - 64-bit IEEE floating point</summary>
        Float64,
        /// <summary>BigInt64Array - 64-bit signed integer (BigInt)</summary>
        BigInt64,
        /// <summary>BigUint64Array - 64-bit unsigned integer (BigInt)</summary>
        BigUint64,
    }

    /// <summary>
    /// Represents a JavaScript TypedArray - a view into an ArrayBuffer providing typed access.
    /// </summary>
    /// <remarks>
    /// TypedArray objects provide a mechanism for reading and writing raw binary data in memory buffers.
    /// Each typed array type represents a different numeric data type (Int8, Uint8, etc.).
    /// </remarks>
    public class JSTypedArray : JSObject
    {
        private readonly JSArrayBuffer _buffer;
        private readonly int _byteOffset;
        private readonly int _length; // Element count
        private readonly TypedArrayKind _kind;
        private readonly int _bytesPerElement;
        private readonly bool _trackLength; // Track resizable ArrayBuffer length

        /// <summary>
        /// Creates a new TypedArray with a new backing ArrayBuffer.
        /// </summary>
        /// <param name="kind">The kind of typed array.</param>
        /// <param name="length">The number of elements.</param>
        public JSTypedArray(TypedArrayKind kind, int length)
            : base(null, GetClassId(kind))
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            _kind = kind;
            _bytesPerElement = GetBytesPerElement(kind);
            _length = length;
            _byteOffset = 0;
            _buffer = new JSArrayBuffer(length * _bytesPerElement);
            _trackLength = false;
        }

        /// <summary>
        /// Creates a new TypedArray as a view into an existing ArrayBuffer.
        /// </summary>
        /// <param name="kind">The kind of typed array.</param>
        /// <param name="buffer">The ArrayBuffer to use.</param>
        /// <param name="byteOffset">The byte offset into the buffer.</param>
        /// <param name="length">The number of elements, or null to use the rest of the buffer.</param>
        public JSTypedArray(TypedArrayKind kind, JSArrayBuffer buffer, int byteOffset = 0, int? length = null)
            : base(null, GetClassId(kind))
        {
            _kind = kind;
            _bytesPerElement = GetBytesPerElement(kind);
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _byteOffset = byteOffset;

            if (buffer.IsDetached)
            {
                throw new InvalidOperationException("Cannot create TypedArray from detached ArrayBuffer");
            }
            if (byteOffset < 0 || byteOffset > buffer.ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset));
            }
            if ((byteOffset % _bytesPerElement) != 0)
            {
                throw new ArgumentException($"byteOffset must be a multiple of {_bytesPerElement}", nameof(byteOffset));
            }

            if (length.HasValue)
            {
                if (length.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }
                if (byteOffset + length.Value * _bytesPerElement > buffer.ByteLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }
                _length = length.Value;
                _trackLength = false;
            }
            else
            {
                // Auto-calculate length based on remaining buffer
                int remainingBytes = buffer.ByteLength - byteOffset;
                if ((remainingBytes % _bytesPerElement) != 0)
                {
                    throw new ArgumentException("Buffer length minus offset must be a multiple of element size");
                }
                _length = remainingBytes / _bytesPerElement;
                _trackLength = buffer.Resizable;
            }
        }

        /// <summary>
        /// Gets the kind of typed array.
        /// </summary>
        public TypedArrayKind Kind => _kind;

        /// <summary>
        /// Gets the number of bytes per element.
        /// </summary>
        public int BytesPerElement => _bytesPerElement;

        /// <summary>
        /// Gets the underlying ArrayBuffer.
        /// </summary>
        public JSArrayBuffer Buffer => _buffer;

        /// <summary>
        /// Gets the byte offset into the buffer.
        /// </summary>
        public int ByteOffset => _byteOffset;

        /// <summary>
        /// Gets the byte length of this view.
        /// </summary>
        public int ByteLength
        {
            get
            {
                if (_buffer.IsDetached)
                {
                    return 0;
                }
                return Length * _bytesPerElement;
            }
        }

        /// <summary>
        /// Gets the number of elements.
        /// </summary>
        public int Length
        {
            get
            {
                if (_buffer.IsDetached)
                {
                    return 0;
                }
                if (_trackLength)
                {
                    // For RAB-tracking arrays, recalculate based on current buffer size
                    int remainingBytes = Math.Max(0, _buffer.ByteLength - _byteOffset);
                    return remainingBytes / _bytesPerElement;
                }
                return _length;
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        public JSValue this[int index]
        {
            get => GetElement(index);
            set => SetElement(index, value);
        }

        /// <summary>
        /// Gets the element at the specified index as a JSValue.
        /// </summary>
        public JSValue GetElement(int index)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }
            if (index < 0 || index >= Length)
            {
                return JSValue.Undefined;
            }

            int byteIndex = _byteOffset + index * _bytesPerElement;
            var data = _buffer.Data!;

            return _kind switch
            {
                TypedArrayKind.Int8 => JSValue.FromInt32((sbyte)data[byteIndex]),
                TypedArrayKind.Uint8 => JSValue.FromInt32(data[byteIndex]),
                TypedArrayKind.Uint8Clamped => JSValue.FromInt32(data[byteIndex]),
                TypedArrayKind.Int16 => JSValue.FromInt32(BitConverter.ToInt16(data, byteIndex)),
                TypedArrayKind.Uint16 => JSValue.FromInt32(BitConverter.ToUInt16(data, byteIndex)),
                TypedArrayKind.Int32 => JSValue.FromInt32(BitConverter.ToInt32(data, byteIndex)),
                TypedArrayKind.Uint32 => JSValue.FromDouble(BitConverter.ToUInt32(data, byteIndex)),
                TypedArrayKind.Float32 => JSValue.FromDouble(BitConverter.ToSingle(data, byteIndex)),
                TypedArrayKind.Float64 => JSValue.FromDouble(BitConverter.ToDouble(data, byteIndex)),
                TypedArrayKind.BigInt64 => JSValue.FromDouble(BitConverter.ToInt64(data, byteIndex)),
                TypedArrayKind.BigUint64 => JSValue.FromDouble((double)BitConverter.ToUInt64(data, byteIndex)),
                _ => throw new InvalidOperationException("Unknown TypedArray kind"),
            };
        }

        /// <summary>
        /// Sets the element at the specified index.
        /// </summary>
        public void SetElement(int index, JSValue value)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }
            if (index < 0 || index >= Length)
            {
                return; // Silent ignore for out-of-bounds
            }

            int byteIndex = _byteOffset + index * _bytesPerElement;
            var data = _buffer.Data!;

            switch (_kind)
            {
                case TypedArrayKind.Int8:
                    data[byteIndex] = (byte)(sbyte)JSValueConversion.ToInt32(value);
                    break;
                case TypedArrayKind.Uint8:
                    data[byteIndex] = (byte)JSValueConversion.ToInt32(value);
                    break;
                case TypedArrayKind.Uint8Clamped:
                    {
                        double d = JSValueConversion.ToNumber(value);
                        if (double.IsNaN(d)) data[byteIndex] = 0;
                        else if (d <= 0) data[byteIndex] = 0;
                        else if (d >= 255) data[byteIndex] = 255;
                        else data[byteIndex] = (byte)Math.Round(d);
                    }
                    break;
                case TypedArrayKind.Int16:
                    WriteInt16(data, byteIndex, (short)JSValueConversion.ToInt32(value));
                    break;
                case TypedArrayKind.Uint16:
                    WriteUInt16(data, byteIndex, (ushort)JSValueConversion.ToInt32(value));
                    break;
                case TypedArrayKind.Int32:
                    WriteInt32(data, byteIndex, JSValueConversion.ToInt32(value));
                    break;
                case TypedArrayKind.Uint32:
                    WriteUInt32(data, byteIndex, (uint)JSValueConversion.ToInt32(value));
                    break;
                case TypedArrayKind.Float32:
                    WriteSingle(data, byteIndex, (float)JSValueConversion.ToNumber(value));
                    break;
                case TypedArrayKind.Float64:
                    WriteDouble(data, byteIndex, JSValueConversion.ToNumber(value));
                    break;
                case TypedArrayKind.BigInt64:
                    WriteInt64(data, byteIndex, (long)JSValueConversion.ToNumber(value));
                    break;
                case TypedArrayKind.BigUint64:
                    WriteUInt64(data, byteIndex, (ulong)JSValueConversion.ToNumber(value));
                    break;
            }
        }

        /// <summary>
        /// Creates a new TypedArray with a subset of elements.
        /// </summary>
        public JSTypedArray Slice(int start = 0, int? end = null)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            int actualEnd = end ?? len;

            // Handle negative indices
            if (start < 0) start = Math.Max(len + start, 0);
            if (actualEnd < 0) actualEnd = Math.Max(len + actualEnd, 0);

            // Clamp to bounds
            start = Math.Min(start, len);
            actualEnd = Math.Min(actualEnd, len);

            int count = Math.Max(actualEnd - start, 0);
            var result = new JSTypedArray(_kind, count);

            for (int i = 0; i < count; i++)
            {
                result.SetElement(i, GetElement(start + i));
            }

            return result;
        }

        /// <summary>
        /// Creates a new TypedArray that is a copy of this one.
        /// </summary>
        public JSTypedArray Subarray(int start = 0, int? end = null)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            int actualEnd = end ?? len;

            // Handle negative indices
            if (start < 0) start = Math.Max(len + start, 0);
            if (actualEnd < 0) actualEnd = Math.Max(len + actualEnd, 0);

            // Clamp to bounds
            start = Math.Min(start, len);
            actualEnd = Math.Min(actualEnd, len);

            int count = Math.Max(actualEnd - start, 0);
            int newByteOffset = _byteOffset + start * _bytesPerElement;

            return new JSTypedArray(_kind, _buffer, newByteOffset, count);
        }

        /// <summary>
        /// Sets multiple values starting at the given offset.
        /// </summary>
        public void Set(JSValue[] source, int offset = 0)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (offset + source.Length > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Source array is too large");
            }

            for (int i = 0; i < source.Length; i++)
            {
                SetElement(offset + i, source[i]);
            }
        }

        /// <summary>
        /// Copies elements within the array.
        /// </summary>
        public JSTypedArray CopyWithin(int target, int start, int? end = null)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            int actualEnd = end ?? len;

            // Handle negative indices
            if (target < 0) target = Math.Max(len + target, 0);
            if (start < 0) start = Math.Max(len + start, 0);
            if (actualEnd < 0) actualEnd = Math.Max(len + actualEnd, 0);

            // Clamp
            target = Math.Min(target, len);
            start = Math.Min(start, len);
            actualEnd = Math.Min(actualEnd, len);

            int count = Math.Min(actualEnd - start, len - target);
            if (count <= 0) return this;

            // Use a temp array to handle overlapping
            var temp = new JSValue[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = GetElement(start + i);
            }
            for (int i = 0; i < count; i++)
            {
                SetElement(target + i, temp[i]);
            }

            return this;
        }

        /// <summary>
        /// Fills all elements with a value.
        /// </summary>
        public JSTypedArray Fill(JSValue value, int start = 0, int? end = null)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            int actualEnd = end ?? len;

            // Handle negative indices
            if (start < 0) start = Math.Max(len + start, 0);
            if (actualEnd < 0) actualEnd = Math.Max(len + actualEnd, 0);

            // Clamp
            start = Math.Min(start, len);
            actualEnd = Math.Min(actualEnd, len);

            for (int i = start; i < actualEnd; i++)
            {
                SetElement(i, value);
            }

            return this;
        }

        /// <summary>
        /// Reverses the array in place.
        /// </summary>
        public JSTypedArray Reverse()
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            for (int i = 0; i < len / 2; i++)
            {
                var temp = GetElement(i);
                SetElement(i, GetElement(len - 1 - i));
                SetElement(len - 1 - i, temp);
            }

            return this;
        }

        /// <summary>
        /// Sorts the array in place.
        /// </summary>
        public JSTypedArray Sort(Func<JSValue, JSValue, int>? compareFn = null)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("TypedArray buffer is detached");
            }

            int len = Length;
            var array = new JSValue[len];
            for (int i = 0; i < len; i++)
            {
                array[i] = GetElement(i);
            }

            if (compareFn != null)
            {
                Array.Sort(array, (a, b) => compareFn(a, b));
            }
            else
            {
                // Default numeric sort for typed arrays
                Array.Sort(array, (a, b) =>
                {
                    double da = JSValueConversion.ToNumber(a);
                    double db = JSValueConversion.ToNumber(b);
                    if (double.IsNaN(da) && double.IsNaN(db)) return 0;
                    if (double.IsNaN(da)) return 1;
                    if (double.IsNaN(db)) return -1;
                    return da.CompareTo(db);
                });
            }

            for (int i = 0; i < len; i++)
            {
                SetElement(i, array[i]);
            }

            return this;
        }

        /// <summary>
        /// Returns the index of the first matching element, or -1.
        /// </summary>
        public int IndexOf(JSValue searchElement, int fromIndex = 0)
        {
            if (_buffer.IsDetached)
            {
                return -1;
            }

            int len = Length;
            if (fromIndex < 0) fromIndex = Math.Max(len + fromIndex, 0);

            for (int i = fromIndex; i < len; i++)
            {
                var element = GetElement(i);
                if (JSValueConversion.StrictEquals(element, searchElement))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the last matching element, or -1.
        /// </summary>
        public int LastIndexOf(JSValue searchElement, int? fromIndex = null)
        {
            if (_buffer.IsDetached)
            {
                return -1;
            }

            int len = Length;
            int start = fromIndex ?? len - 1;
            if (start < 0) start = len + start;
            start = Math.Min(start, len - 1);

            for (int i = start; i >= 0; i--)
            {
                var element = GetElement(i);
                if (JSValueConversion.StrictEquals(element, searchElement))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns whether any element matches the predicate.
        /// </summary>
        public bool Includes(JSValue searchElement, int fromIndex = 0)
        {
            return IndexOf(searchElement, fromIndex) >= 0;
        }

        /// <summary>
        /// Joins all elements into a string.
        /// </summary>
        public string Join(string separator = ",")
        {
            if (_buffer.IsDetached)
            {
                return "";
            }

            int len = Length;
            var parts = new string[len];
            for (int i = 0; i < len; i++)
            {
                var val = GetElement(i);
                parts[i] = JSValueConversion.ToString(val);
            }
            return string.Join(separator, parts);
        }

        // Helper methods for writing values
        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            var bytes = BitConverter.GetBytes(value);
            buffer[offset] = bytes[0];
            buffer[offset + 1] = bytes[1];
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            buffer[offset] = bytes[0];
            buffer[offset + 1] = bytes[1];
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++)
                buffer[offset + i] = bytes[i];
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++)
                buffer[offset + i] = bytes[i];
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++)
                buffer[offset + i] = bytes[i];
        }

        private static void WriteDouble(byte[] buffer, int offset, double value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 8; i++)
                buffer[offset + i] = bytes[i];
        }

        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 8; i++)
                buffer[offset + i] = bytes[i];
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 8; i++)
                buffer[offset + i] = bytes[i];
        }

        /// <summary>
        /// Gets the bytes per element for a typed array kind.
        /// </summary>
        public static int GetBytesPerElement(TypedArrayKind kind)
        {
            return kind switch
            {
                TypedArrayKind.Int8 => 1,
                TypedArrayKind.Uint8 => 1,
                TypedArrayKind.Uint8Clamped => 1,
                TypedArrayKind.Int16 => 2,
                TypedArrayKind.Uint16 => 2,
                TypedArrayKind.Int32 => 4,
                TypedArrayKind.Uint32 => 4,
                TypedArrayKind.Float32 => 4,
                TypedArrayKind.Float64 => 8,
                TypedArrayKind.BigInt64 => 8,
                TypedArrayKind.BigUint64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        /// <summary>
        /// Gets the JSClassId for a typed array kind.
        /// </summary>
        public static JSClassId GetClassId(TypedArrayKind kind)
        {
            return kind switch
            {
                TypedArrayKind.Int8 => JSClassId.Int8Array,
                TypedArrayKind.Uint8 => JSClassId.Uint8Array,
                TypedArrayKind.Uint8Clamped => JSClassId.Uint8ClampedArray,
                TypedArrayKind.Int16 => JSClassId.Int16Array,
                TypedArrayKind.Uint16 => JSClassId.Uint16Array,
                TypedArrayKind.Int32 => JSClassId.Int32Array,
                TypedArrayKind.Uint32 => JSClassId.Uint32Array,
                TypedArrayKind.Float32 => JSClassId.Float32Array,
                TypedArrayKind.Float64 => JSClassId.Float64Array,
                TypedArrayKind.BigInt64 => JSClassId.BigInt64Array,
                TypedArrayKind.BigUint64 => JSClassId.BigUint64Array,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        /// <summary>
        /// Gets the TypedArrayKind from a JSClassId.
        /// </summary>
        public static TypedArrayKind GetKind(JSClassId classId)
        {
            return classId switch
            {
                JSClassId.Int8Array => TypedArrayKind.Int8,
                JSClassId.Uint8Array => TypedArrayKind.Uint8,
                JSClassId.Uint8ClampedArray => TypedArrayKind.Uint8Clamped,
                JSClassId.Int16Array => TypedArrayKind.Int16,
                JSClassId.Uint16Array => TypedArrayKind.Uint16,
                JSClassId.Int32Array => TypedArrayKind.Int32,
                JSClassId.Uint32Array => TypedArrayKind.Uint32,
                JSClassId.Float32Array => TypedArrayKind.Float32,
                JSClassId.Float64Array => TypedArrayKind.Float64,
                JSClassId.BigInt64Array => TypedArrayKind.BigInt64,
                JSClassId.BigUint64Array => TypedArrayKind.BigUint64,
                _ => throw new ArgumentOutOfRangeException(nameof(classId)),
            };
        }
    }
}
