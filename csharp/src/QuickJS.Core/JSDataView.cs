// Copyright (c) the QuickJS.NET contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace QuickJS
{
    /// <summary>
    /// Represents a JavaScript DataView - a low-level interface for reading and writing
    /// multiple number types in an ArrayBuffer.
    /// </summary>
    /// <remarks>
    /// DataView provides a low-level interface for reading and writing multiple number types
    /// in a binary ArrayBuffer, without having to care about the platform's endianness.
    /// </remarks>
    public class JSDataView : JSObject
    {
        private readonly JSArrayBuffer _buffer;
        private readonly int _byteOffset;
        private readonly int _byteLength;
        private readonly bool _trackLength; // Track resizable ArrayBuffer length

        /// <summary>
        /// Creates a new DataView as a view into an ArrayBuffer.
        /// </summary>
        /// <param name="buffer">The ArrayBuffer to use.</param>
        /// <param name="byteOffset">The byte offset into the buffer.</param>
        /// <param name="byteLength">The byte length, or null to use the rest of the buffer.</param>
        public JSDataView(JSArrayBuffer buffer, int byteOffset = 0, int? byteLength = null)
            : base(null, JSClassId.DataView)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _byteOffset = byteOffset;

            if (buffer.IsDetached)
            {
                throw new InvalidOperationException("Cannot create DataView from detached ArrayBuffer");
            }
            if (byteOffset < 0 || byteOffset > buffer.ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset));
            }

            if (byteLength.HasValue)
            {
                if (byteLength.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(byteLength));
                }
                if (byteOffset + byteLength.Value > buffer.ByteLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(byteLength));
                }
                _byteLength = byteLength.Value;
                _trackLength = false;
            }
            else
            {
                _byteLength = buffer.ByteLength - byteOffset;
                _trackLength = buffer.Resizable;
            }
        }

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
                if (_trackLength)
                {
                    // For RAB-tracking views, recalculate based on current buffer size
                    return Math.Max(0, _buffer.ByteLength - _byteOffset);
                }
                return _byteLength;
            }
        }

        /// <summary>
        /// Checks if the buffer is detached or bounds are exceeded.
        /// </summary>
        private void CheckBounds(int offset, int size)
        {
            if (_buffer.IsDetached)
            {
                throw new InvalidOperationException("DataView buffer is detached");
            }
            if (offset < 0 || offset + size > ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside the bounds of the DataView");
            }
        }

        /// <summary>
        /// Gets a signed 8-bit integer.
        /// </summary>
        public sbyte GetInt8(int byteOffset)
        {
            CheckBounds(byteOffset, 1);
            return (sbyte)_buffer.Data![_byteOffset + byteOffset];
        }

        /// <summary>
        /// Gets an unsigned 8-bit integer.
        /// </summary>
        public byte GetUint8(int byteOffset)
        {
            CheckBounds(byteOffset, 1);
            return _buffer.Data![_byteOffset + byteOffset];
        }

        /// <summary>
        /// Gets a signed 16-bit integer.
        /// </summary>
        public short GetInt16(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 2);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                return (short)(data[idx] | (data[idx + 1] << 8));
            }
            else
            {
                return (short)((data[idx] << 8) | data[idx + 1]);
            }
        }

        /// <summary>
        /// Gets an unsigned 16-bit integer.
        /// </summary>
        public ushort GetUint16(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 2);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                return (ushort)(data[idx] | (data[idx + 1] << 8));
            }
            else
            {
                return (ushort)((data[idx] << 8) | data[idx + 1]);
            }
        }

        /// <summary>
        /// Gets a signed 32-bit integer.
        /// </summary>
        public int GetInt32(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 4);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                return data[idx] | (data[idx + 1] << 8) | (data[idx + 2] << 16) | (data[idx + 3] << 24);
            }
            else
            {
                return (data[idx] << 24) | (data[idx + 1] << 16) | (data[idx + 2] << 8) | data[idx + 3];
            }
        }

        /// <summary>
        /// Gets an unsigned 32-bit integer.
        /// </summary>
        public uint GetUint32(int byteOffset, bool littleEndian = false)
        {
            return (uint)GetInt32(byteOffset, littleEndian);
        }

        /// <summary>
        /// Gets a 32-bit floating point number.
        /// </summary>
        public float GetFloat32(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 4);
            var bytes = GetBytes(byteOffset, 4, littleEndian);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Gets a 64-bit floating point number.
        /// </summary>
        public double GetFloat64(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 8);
            var bytes = GetBytes(byteOffset, 8, littleEndian);
            return BitConverter.ToDouble(bytes, 0);
        }

        /// <summary>
        /// Gets a signed 64-bit integer.
        /// </summary>
        public long GetBigInt64(int byteOffset, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 8);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                return (long)data[idx] | ((long)data[idx + 1] << 8) | ((long)data[idx + 2] << 16) |
                       ((long)data[idx + 3] << 24) | ((long)data[idx + 4] << 32) | ((long)data[idx + 5] << 40) |
                       ((long)data[idx + 6] << 48) | ((long)data[idx + 7] << 56);
            }
            else
            {
                return ((long)data[idx] << 56) | ((long)data[idx + 1] << 48) | ((long)data[idx + 2] << 40) |
                       ((long)data[idx + 3] << 32) | ((long)data[idx + 4] << 24) | ((long)data[idx + 5] << 16) |
                       ((long)data[idx + 6] << 8) | (long)data[idx + 7];
            }
        }

        /// <summary>
        /// Gets an unsigned 64-bit integer.
        /// </summary>
        public ulong GetBigUint64(int byteOffset, bool littleEndian = false)
        {
            return (ulong)GetBigInt64(byteOffset, littleEndian);
        }

        /// <summary>
        /// Sets a signed 8-bit integer.
        /// </summary>
        public void SetInt8(int byteOffset, sbyte value)
        {
            CheckBounds(byteOffset, 1);
            _buffer.Data![_byteOffset + byteOffset] = (byte)value;
        }

        /// <summary>
        /// Sets an unsigned 8-bit integer.
        /// </summary>
        public void SetUint8(int byteOffset, byte value)
        {
            CheckBounds(byteOffset, 1);
            _buffer.Data![_byteOffset + byteOffset] = value;
        }

        /// <summary>
        /// Sets a signed 16-bit integer.
        /// </summary>
        public void SetInt16(int byteOffset, short value, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 2);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                data[idx] = (byte)value;
                data[idx + 1] = (byte)(value >> 8);
            }
            else
            {
                data[idx] = (byte)(value >> 8);
                data[idx + 1] = (byte)value;
            }
        }

        /// <summary>
        /// Sets an unsigned 16-bit integer.
        /// </summary>
        public void SetUint16(int byteOffset, ushort value, bool littleEndian = false)
        {
            SetInt16(byteOffset, (short)value, littleEndian);
        }

        /// <summary>
        /// Sets a signed 32-bit integer.
        /// </summary>
        public void SetInt32(int byteOffset, int value, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 4);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                data[idx] = (byte)value;
                data[idx + 1] = (byte)(value >> 8);
                data[idx + 2] = (byte)(value >> 16);
                data[idx + 3] = (byte)(value >> 24);
            }
            else
            {
                data[idx] = (byte)(value >> 24);
                data[idx + 1] = (byte)(value >> 16);
                data[idx + 2] = (byte)(value >> 8);
                data[idx + 3] = (byte)value;
            }
        }

        /// <summary>
        /// Sets an unsigned 32-bit integer.
        /// </summary>
        public void SetUint32(int byteOffset, uint value, bool littleEndian = false)
        {
            SetInt32(byteOffset, (int)value, littleEndian);
        }

        /// <summary>
        /// Sets a 32-bit floating point number.
        /// </summary>
        public void SetFloat32(int byteOffset, float value, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 4);
            var bytes = BitConverter.GetBytes(value);
            SetBytes(byteOffset, bytes, littleEndian);
        }

        /// <summary>
        /// Sets a 64-bit floating point number.
        /// </summary>
        public void SetFloat64(int byteOffset, double value, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 8);
            var bytes = BitConverter.GetBytes(value);
            SetBytes(byteOffset, bytes, littleEndian);
        }

        /// <summary>
        /// Sets a signed 64-bit integer.
        /// </summary>
        public void SetBigInt64(int byteOffset, long value, bool littleEndian = false)
        {
            CheckBounds(byteOffset, 8);
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;
            
            if (littleEndian)
            {
                data[idx] = (byte)value;
                data[idx + 1] = (byte)(value >> 8);
                data[idx + 2] = (byte)(value >> 16);
                data[idx + 3] = (byte)(value >> 24);
                data[idx + 4] = (byte)(value >> 32);
                data[idx + 5] = (byte)(value >> 40);
                data[idx + 6] = (byte)(value >> 48);
                data[idx + 7] = (byte)(value >> 56);
            }
            else
            {
                data[idx] = (byte)(value >> 56);
                data[idx + 1] = (byte)(value >> 48);
                data[idx + 2] = (byte)(value >> 40);
                data[idx + 3] = (byte)(value >> 32);
                data[idx + 4] = (byte)(value >> 24);
                data[idx + 5] = (byte)(value >> 16);
                data[idx + 6] = (byte)(value >> 8);
                data[idx + 7] = (byte)value;
            }
        }

        /// <summary>
        /// Sets an unsigned 64-bit integer.
        /// </summary>
        public void SetBigUint64(int byteOffset, ulong value, bool littleEndian = false)
        {
            SetBigInt64(byteOffset, (long)value, littleEndian);
        }

        /// <summary>
        /// Helper to get bytes with endianness handling.
        /// </summary>
        private byte[] GetBytes(int byteOffset, int length, bool littleEndian)
        {
            var data = _buffer.Data!;
            var bytes = new byte[length];
            int idx = _byteOffset + byteOffset;

            if (BitConverter.IsLittleEndian == littleEndian)
            {
                // Native endianness matches requested
                for (int i = 0; i < length; i++)
                {
                    bytes[i] = data[idx + i];
                }
            }
            else
            {
                // Need to reverse
                for (int i = 0; i < length; i++)
                {
                    bytes[i] = data[idx + length - 1 - i];
                }
            }

            return bytes;
        }

        /// <summary>
        /// Helper to set bytes with endianness handling.
        /// </summary>
        private void SetBytes(int byteOffset, byte[] bytes, bool littleEndian)
        {
            var data = _buffer.Data!;
            int idx = _byteOffset + byteOffset;

            if (BitConverter.IsLittleEndian == littleEndian)
            {
                // Native endianness matches requested
                for (int i = 0; i < bytes.Length; i++)
                {
                    data[idx + i] = bytes[i];
                }
            }
            else
            {
                // Need to reverse
                for (int i = 0; i < bytes.Length; i++)
                {
                    data[idx + i] = bytes[bytes.Length - 1 - i];
                }
            }
        }
    }
}
