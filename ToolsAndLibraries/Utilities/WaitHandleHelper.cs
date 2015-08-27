// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WaitHandleHelper.cs">
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
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Contains helper methods for the <see cref="WaitHandle"/> class.
    /// </summary>
    public static class WaitHandleHelper
    {
        #region Acquire

        /// <summary>
        /// Acquires the <paramref name="handle"/> given.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        public static void Acquire(WaitHandle handle)
        {
            WaitHandleHelper.Acquire(handle, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Tries to acquire <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeoutHelper">A <see cref="TimeoutHelper"/> that provides the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> or <paramref name="timeoutHelper"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException"><paramref name="handle"/> wasn't acquired within the timeout specified.</exception>
        public static void Acquire(WaitHandle handle, TimeoutHelper timeoutHelper)
        {
            if (timeoutHelper == null)
            {
                throw new ArgumentNullException(nameof(timeoutHelper));
            }

            TimeSpan remaining = timeoutHelper.Remaining;
            if (timeoutHelper.IsExpired)
            {
                throw new TimeoutException(string.Format(CultureInfo.InvariantCulture, "Unable to acquire handle within {0}", timeoutHelper.Timeout));
            }

            WaitHandleHelper.Acquire(handle, remaining);
        }

        /// <summary>
        /// Tries to acquire <paramref name="handle"/> within the <paramref name="timeout"/> interval.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException"><paramref name="handle"/> wasn't acquired within the <paramref name="timeout"/> interval.</exception>
        public static void Acquire(WaitHandle handle, TimeSpan timeout)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            try
            {
                if (!handle.WaitOne(timeout))
                {
                    throw new TimeoutException(string.Format(CultureInfo.InvariantCulture, "Unable to acquire handle within {0}", timeout));
                }
            }
            catch (AbandonedMutexException)
            {
                // Handle is acquired by us.
            }
        }

        #endregion // Acquire

        #region TryToAcquire

        /// <summary>
        /// Tries to acquire the <paramref name="handle"/> given without blocking the current thread.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <returns><see langword="true"/>, if the <paramref name="handle"/> is acquired; otherwise, <see langword="false"/>.</returns>
        public static bool TryToAcquire(WaitHandle handle)
        {
            return WaitHandleHelper.TryToAcquire(handle, TimeSpan.Zero);
        }

        /// <summary>
        /// Tries to acquire the <paramref name="handle"/> given.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeoutHelper">A <see cref="TimeoutHelper"/> that provides the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> or <paramref name="timeoutHelper"/> is <see langword="null"/>.</exception>
        /// <returns><see langword="true"/>, if the <paramref name="handle"/> is acquired; otherwise, <see langword="false"/>.</returns>
        public static bool TryToAcquire(WaitHandle handle, TimeoutHelper timeoutHelper)
        {
            if (timeoutHelper == null)
            {
                throw new ArgumentNullException(nameof(timeoutHelper));
            }

            TimeSpan remaining = timeoutHelper.Remaining;
            if (timeoutHelper.IsExpired)
            {
                return false;
            }

            return WaitHandleHelper.TryToAcquire(handle, remaining);
        }

        /// <summary>
        /// Tries to acquire the <paramref name="handle"/> given within the <paramref name="timeout"/> interval.
        /// </summary>
        /// <param name="handle">Handle to be acquired.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <returns><see langword="true"/>, if the <paramref name="handle"/> is acquired; otherwise, <see langword="false"/>.</returns>
        public static bool TryToAcquire(WaitHandle handle, TimeSpan timeout)
        {
            try
            {
                WaitHandleHelper.Acquire(handle, timeout);
            }
            catch (TimeoutException)
            {
                return false;
            }

            return true;
        }

        #endregion // TryToAcquire

        #region AcquireAll

        /// <summary>
        /// Acquires all handles in the <paramref name="handles"/> list given.
        /// </summary>
        /// <param name="handles">Handles to acquire.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handles"/> list is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="handles"/> list contains same elements.</exception>
        /// <remarks>
        /// If exception is thrown within this method, all handles acquired are automatically released.
        /// </remarks>
        public static void AcquireAll(WaitHandle[] handles)
        {
            WaitHandleHelper.AcquireAll(handles, new TimeoutHelper(Timeout.InfiniteTimeSpan));
        }

        /// <summary>
        /// Acquires all handles in the <paramref name="handles"/> list given.
        /// </summary>
        /// <param name="handles">Handles to acquire.</param>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handles"/> list is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="handles"/> list contains same elements.</exception>
        /// <exception cref="TimeoutException">If all handles weren't acquired within the timeout specified.</exception>
        /// <remarks>
        /// If exception is thrown within this method, all handles acquired are automatically released.
        /// </remarks>
        public static void AcquireAll(WaitHandle[] handles, TimeSpan timeout)
        {
            WaitHandleHelper.AcquireAll(handles, new TimeoutHelper(timeout));
        }

        /// <summary>
        /// Acquires all handles in the <paramref name="handles"/> list given.
        /// </summary>
        /// <param name="handles">Handles to acquire.</param>
        /// <param name="timeoutHelper">A <see cref="TimeoutHelper"/> that provides the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="handles"/> list is <see langword="null"/> or empty.
        ///     <para>-or-</para>
        /// <paramref name="timeoutHelper"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="handles"/> list contains same elements.</exception>
        /// <exception cref="TimeoutException">If all handles weren't acquired within the timeout specified.</exception>
        /// <remarks>
        /// If exception is thrown within this method, all handles acquired are automatically released.
        /// </remarks>
        public static void AcquireAll(WaitHandle[] handles, TimeoutHelper timeoutHelper)
        {
            if (handles == null || handles.Length == 0)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (timeoutHelper == null)
            {
                throw new ArgumentNullException(nameof(timeoutHelper));
            }

            List<WaitHandle> acquiredHandles = new List<WaitHandle>();
            List<WaitHandle> pendingHandles  = new List<WaitHandle>(handles);

            // Check if there are equal handles in the list.
            for (int i = 0; i < pendingHandles.Count; i++)
            {
                for (int j = i + 1; j < pendingHandles.Count; j++)
                {
                    if (pendingHandles[i] == pendingHandles[j])
                    {
                        throw new ArgumentException("List contains equal handles.");
                    }
                }
            }

            try
            {
                // Wait for all handles.
                while (pendingHandles.Any())
                {
                    for (int i = 0; i < pendingHandles.Count; i++)
                    {
                        if (!WaitHandleHelper.TryToAcquire(pendingHandles[i]))
                        {
                            continue;
                        }

                        acquiredHandles.Add(pendingHandles[i]);
                        pendingHandles.RemoveAt(i);
                        i--;
                    }

                    if (timeoutHelper.IsExpired)
                    {
                        throw new TimeoutException(string.Format(CultureInfo.InvariantCulture, "Unable to acquire all handles within {0}", timeoutHelper.Timeout));
                    }
                }
            }
            catch
            {
                foreach (WaitHandle handle in acquiredHandles)
                {
                    try
                    {
                        Mutex mutex = handle as Mutex;
                        if (mutex != null)
                        {
                            mutex.ReleaseMutex();
                            continue;
                        }

                        Semaphore semaphore = handle as Semaphore;
                        semaphore?.Release();

                        // Do nothing for the EventWaitHandle.
                    }
                    catch (ApplicationException)
                    {
                        // We may not own this handle.
                    }
                }

                throw;
            }
        }

        #endregion // AcquireAll
    }
}
