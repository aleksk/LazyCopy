// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WaitHandleEx.cs">
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

namespace LazyCopy.Utilities.Extensions
{
    using System;
    using System.Threading;

    /// <summary>
    /// Contains extension methods for the <see cref="WaitHandle"/> objects.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "This suffix is desired here.")]
    public static class WaitHandleEx
    {
        /// <summary>
        /// Tries to acquire <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeoutHelper">A <see cref="TimeoutHelper"/> that provides the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException"><paramref name="handle"/> wasn't acquired withing the timeout, or the timeout has expired.</exception>
        public static void Acquire(this WaitHandle handle, TimeoutHelper timeoutHelper)
        {
            WaitHandleHelper.Acquire(handle, timeoutHelper);
        }

        /// <summary>
        /// Tries to acquire <paramref name="handle"/> within the <paramref name="timeout"/> interval.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException"><paramref name="handle"/> wasn't acquired within the <paramref name="timeout"/> interval.</exception>
        public static void Acquire(this WaitHandle handle, TimeSpan timeout)
        {
            WaitHandleHelper.Acquire(handle, timeout);
        }

        /// <summary>
        /// Tries to acquire handle without blocking the current thread.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <returns><see langword="true"/>, if the handle is acquired; otherwise, <see langword="false"/>.</returns>
        public static bool TryToAcquire(this WaitHandle handle)
        {
            return WaitHandleHelper.TryToAcquire(handle);
        }
    }
}
