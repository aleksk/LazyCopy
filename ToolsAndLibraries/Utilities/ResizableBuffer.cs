// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ResizableBuffer.cs">
//   The MIT License (MIT)
//   Copyright (c) 2015 Aleksey Kabanov
// </copyright>
// <summary>
//   Permission is hereby granted, free of charge, to any person obtaining a copy
//   of this software and associated documentation files (the "Software"), to deal
//   in the Software without restriction, including without limitation the rights
//   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//   copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in
//   all copies or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//   THE SOFTWARE.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LazyCopy.Utilities
{
    using System;
    using System.Globalization;
    using System.Runtime.InteropServices;

    using LazyCopy.Utilities.Native;

    /// <summary>
    /// This class maintains its own internal buffer and extends it, if the it is too small to fit the desired data.
    /// </summary>
    /// <seealso cref="Resize"/>
    /// <remarks>
    /// The main reason of using this class is to avoid unnecessary memory allocations.<br/>
    /// For example, the same memory can be used for getting the driver notification and then storing the reply message into,
    /// so there's no need to allocate two separate memory blocks.
    /// </remarks>
    public class ResizableBuffer : IDisposable
    {
        #region Fields

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Maximum buffer size.
        /// </summary>
        /// <remarks>
        /// Current value is <c>5 Mb</c>.
        /// </remarks>
        private readonly int maxBufferSize = Math.Max(5 * 1024 * 1024, Environment.SystemPageSize);

        /// <summary>
        /// Pointer to the buffer allocated.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources", Justification = "This class manually manages this buffer.")]
        private IntPtr buffer = IntPtr.Zero;

        /// <summary>
        /// Size of the internal buffer, in bytes.
        /// </summary>
        private volatile int byteLength;

        /// <summary>
        /// Whether the current instance is already disposed.
        /// </summary>
        private volatile bool isDisposed;

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizableBuffer"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException">Buffer was not allocated.</exception>
        /// <remarks>
        /// The <see cref="Environment.SystemPageSize"/> is passed as an initial size.
        /// </remarks>
        public ResizableBuffer()
            : this(Environment.SystemPageSize)
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizableBuffer"/> class.
        /// </summary>
        /// <param name="initialSize">Initial buffer size.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialSize"/> is lesser or equal to zero or greater than the maximum size allowed.</exception>
        /// <exception cref="InvalidOperationException">Buffer was not allocated.</exception>
        public ResizableBuffer(int initialSize)
        {
            this.EnsureBufferIsOfTheRightSize(initialSize);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ResizableBuffer"/> class.
        /// </summary>
        ~ResizableBuffer()
        {
            this.Dispose(false);
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets the size of the allocated buffer.
        /// </summary>
        public int ByteLength
        {
            get
            {
                lock (this.syncRoot)
                {
                    if (this.isDisposed)
                    {
                        throw new ObjectDisposedException("Buffer is already disposed.");
                    }

                    return this.byteLength;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the buffer is already disposed.
        /// </summary>
        public bool Disposed
        {
            get
            {
                return this.isDisposed;
            }
        }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the pointer to the internal buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Buffer is already disposed.</exception>
        /// <returns>Pointer to the internal buffer.</returns>
        /// <remarks>
        /// Be careful with the pointer returned, because it may become invalid after the next
        /// <see cref="Resize"/> function call and after the <see cref="ResizableBuffer"/>
        /// instance is disposed.
        /// </remarks>
        public IntPtr DangerousGetPointer()
        {
            lock (this.syncRoot)
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException("Buffer is already disposed.");
                }

                return this.buffer;
            }
        }

        /// <summary>
        /// Resizes the buffer.
        /// </summary>
        /// <param name="newSize">The desired buffer size.</param>
        /// <remarks>
        /// Be careful with the pointer returned, because it may become invalid after the next <see cref="Resize"/> function call and after the
        /// <see cref="ResizableBuffer"/> instance is disposed.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Buffer is already disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newSize"/> is lesser or equal to zero or greater than the maximum size allowed.</exception>
        public void Resize(int newSize)
        {
            lock (this.syncRoot)
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException("Buffer is already disposed.");
                }

                this.EnsureBufferIsOfTheRightSize(newSize);
            }
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            lock (this.syncRoot)
            {
                if (this.buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(this.buffer);

                    this.buffer     = IntPtr.Zero;
                    this.byteLength = 0;
                }

                this.isDisposed = true;
            }
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Ensures that the current buffer can store the <paramref name="newSize"/> bytes.
        /// If the current buffer is not large enough, it's extended.
        /// </summary>
        /// <param name="newSize">The desired buffer size.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newSize"/> is lesser or equal to zero or greater than the maximum size allowed.</exception>
        /// <exception cref="InvalidOperationException">Buffer was not allocated or extended.</exception>
        private void EnsureBufferIsOfTheRightSize(int newSize)
        {
            if (newSize <= 0 || newSize > this.maxBufferSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newSize),
                    string.Format(CultureInfo.InvariantCulture, "Desired size should be greater than zero and lesser than {0}", this.maxBufferSize));
            }

            // Skip, if the buffer is already large enough.
            if (this.byteLength >= newSize)
            {
                return;
            }

            try
            {
                // Is it initial allocation or we need to extend the buffer?
                this.buffer = this.buffer == IntPtr.Zero
                              ? Marshal.AllocHGlobal(newSize)
                              : Marshal.ReAllocHGlobal(this.buffer, new IntPtr(newSize));

                this.byteLength = newSize;
                NativeMethods.ZeroMemory(this.buffer, (uint)this.byteLength);
            }
            catch (OutOfMemoryException oom)
            {
                this.buffer     = IntPtr.Zero;
                this.byteLength = 0;

                throw new InvalidOperationException("Unable to allocate or extend the buffer.", oom);
            }
        }

        #endregion // Private methods
    }
}
