// Copyright (c) the QuickJS.NET contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace QuickJS
{
    /// <summary>
    /// Represents a JavaScript ArrayBuffer object - a fixed-length raw binary data buffer.
    /// </summary>
    /// <remarks>
    /// ArrayBuffer is used to represent a generic, fixed-length raw binary data buffer.
    /// You cannot directly manipulate the contents of an ArrayBuffer; instead, you create
    /// one of the typed array objects or a DataView object which represents the buffer
    /// in a specific format.
    /// </remarks>
    public class JSArrayBuffer : JSObject
    {
        private byte[] _data;
        private bool _detached;
        private bool _shared;
        private int _maxByteLength;

        /// <summary>
        /// Creates a new ArrayBuffer with the specified byte length.
        /// </summary>
        /// <param name="byteLength">The size, in bytes, of the array buffer to create.</param>
        public JSArrayBuffer(int byteLength)
            : base(null, JSClassId.ArrayBuffer)
        {
            if (byteLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteLength), "ArrayBuffer size cannot be negative");
            }
            _data = new byte[byteLength];
            _maxByteLength = -1; // Not resizable
            ByteLength = byteLength;
        }

        /// <summary>
        /// Creates a new ArrayBuffer with the specified byte length and optional max length for resizable buffers.
        /// </summary>
        /// <param name="byteLength">The initial size, in bytes.</param>
        /// <param name="maxByteLength">The maximum size, in bytes, for resizable buffers. -1 for fixed-size.</param>
        /// <param name="shared">Whether this is a SharedArrayBuffer.</param>
        public JSArrayBuffer(int byteLength, int maxByteLength, bool shared = false)
            : base(null, shared ? JSClassId.SharedArrayBuffer : JSClassId.ArrayBuffer)
        {
            if (byteLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteLength), "ArrayBuffer size cannot be negative");
            }
            if (maxByteLength >= 0 && maxByteLength < byteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxByteLength), "maxByteLength cannot be less than byteLength");
            }
            // For resizable buffers, allocate the max size upfront
            _data = new byte[maxByteLength >= 0 ? maxByteLength : byteLength];
            _maxByteLength = maxByteLength;
            _shared = shared;
            
            // ByteLength property returns the current length, not max
            // We track this separately since _data.Length is the max
            ByteLength = byteLength;
        }

        /// <summary>
        /// Creates a new ArrayBuffer wrapping existing data.
        /// </summary>
        /// <param name="data">The data to wrap.</param>
        /// <param name="shared">Whether this is a SharedArrayBuffer.</param>
        internal JSArrayBuffer(byte[] data, bool shared = false)
            : base(null, shared ? JSClassId.SharedArrayBuffer : JSClassId.ArrayBuffer)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _maxByteLength = -1;
            _shared = shared;
            ByteLength = data.Length;
        }

        /// <summary>
        /// Gets the size, in bytes, of the ArrayBuffer.
        /// </summary>
        public int ByteLength { get; private set; }

        /// <summary>
        /// Gets the maximum byte length for resizable ArrayBuffers, or -1 if not resizable.
        /// </summary>
        public int MaxByteLength => _maxByteLength;

        /// <summary>
        /// Gets whether this ArrayBuffer is resizable.
        /// </summary>
        public bool Resizable => _maxByteLength >= 0;

        /// <summary>
        /// Gets whether this ArrayBuffer has been detached.
        /// </summary>
        public bool IsDetached => _detached;

        /// <summary>
        /// Gets whether this is a SharedArrayBuffer.
        /// </summary>
        public bool IsShared => _shared;

        /// <summary>
        /// Gets the underlying data array. Returns null if detached.
        /// </summary>
        internal byte[]? Data => _detached ? null : _data;

        /// <summary>
        /// Detaches this ArrayBuffer, making it unusable.
        /// </summary>
        /// <returns>The data that was in the buffer before detaching.</returns>
        public byte[] Detach()
        {
            if (_shared)
            {
                throw new InvalidOperationException("Cannot detach a SharedArrayBuffer");
            }
            if (_detached)
            {
                throw new InvalidOperationException("ArrayBuffer is already detached");
            }
            _detached = true;
            var data = _data;
            _data = Array.Empty<byte>();
            ByteLength = 0;
            return data;
        }

        /// <summary>
        /// Resizes a resizable ArrayBuffer to a new byte length.
        /// </summary>
        /// <param name="newByteLength">The new byte length.</param>
        public void Resize(int newByteLength)
        {
            if (_detached)
            {
                throw new InvalidOperationException("Cannot resize a detached ArrayBuffer");
            }
            if (!Resizable)
            {
                throw new InvalidOperationException("ArrayBuffer is not resizable");
            }
            if (newByteLength < 0 || newByteLength > _maxByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(newByteLength));
            }
            // Since we allocated max size upfront, just update the logical length
            // Zero out any newly exposed bytes
            if (newByteLength > ByteLength)
            {
                Array.Clear(_data, ByteLength, newByteLength - ByteLength);
            }
            ByteLength = newByteLength;
        }

        /// <summary>
        /// Creates a new ArrayBuffer with a copy of this buffer's data.
        /// </summary>
        /// <param name="begin">Start offset (optional).</param>
        /// <param name="end">End offset (optional).</param>
        /// <returns>A new ArrayBuffer containing the sliced data.</returns>
        public JSArrayBuffer Slice(int begin = 0, int? end = null)
        {
            if (_detached)
            {
                throw new InvalidOperationException("Cannot slice a detached ArrayBuffer");
            }

            int actualEnd = end ?? ByteLength;
            
            // Handle negative indices
            if (begin < 0) begin = Math.Max(ByteLength + begin, 0);
            if (actualEnd < 0) actualEnd = Math.Max(ByteLength + actualEnd, 0);
            
            // Clamp to bounds
            begin = Math.Min(begin, ByteLength);
            actualEnd = Math.Min(actualEnd, ByteLength);
            
            int newLength = Math.Max(actualEnd - begin, 0);
            var newBuffer = new JSArrayBuffer(newLength);
            
            if (newLength > 0)
            {
                Array.Copy(_data, begin, newBuffer._data, 0, newLength);
            }
            
            return newBuffer;
        }

        /// <summary>
        /// Transfers ownership to a new ArrayBuffer with optional resize.
        /// </summary>
        /// <param name="newByteLength">Optional new byte length.</param>
        /// <returns>A new ArrayBuffer with the transferred data.</returns>
        public JSArrayBuffer Transfer(int? newByteLength = null)
        {
            if (_detached)
            {
                throw new InvalidOperationException("Cannot transfer a detached ArrayBuffer");
            }
            if (_shared)
            {
                throw new InvalidOperationException("Cannot transfer a SharedArrayBuffer");
            }

            int newLength = newByteLength ?? ByteLength;
            if (newLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newByteLength));
            }

            var newBuffer = new JSArrayBuffer(newLength);
            int copyLength = Math.Min(ByteLength, newLength);
            if (copyLength > 0)
            {
                Array.Copy(_data, 0, newBuffer._data, 0, copyLength);
            }

            // Detach the original
            Detach();

            return newBuffer;
        }

        /// <summary>
        /// Gets a byte at the specified index.
        /// </summary>
        internal byte GetByte(int index)
        {
            if (_detached)
            {
                throw new InvalidOperationException("ArrayBuffer is detached");
            }
            if (index < 0 || index >= ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _data[index];
        }

        /// <summary>
        /// Sets a byte at the specified index.
        /// </summary>
        internal void SetByte(int index, byte value)
        {
            if (_detached)
            {
                throw new InvalidOperationException("ArrayBuffer is detached");
            }
            if (index < 0 || index >= ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            _data[index] = value;
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Gets a span over the buffer data.
        /// </summary>
        public Span<byte> AsSpan()
        {
            if (_detached)
            {
                throw new InvalidOperationException("ArrayBuffer is detached");
            }
            return _data.AsSpan(0, ByteLength);
        }

        /// <summary>
        /// Gets a span over a portion of the buffer data.
        /// </summary>
        public Span<byte> AsSpan(int start, int length)
        {
            if (_detached)
            {
                throw new InvalidOperationException("ArrayBuffer is detached");
            }
            if (start < 0 || start > ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            if (length < 0 || start + length > ByteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            return _data.AsSpan(start, length);
        }
#endif
    }
}
