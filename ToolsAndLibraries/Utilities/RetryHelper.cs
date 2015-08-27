// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RetryHelper.cs">
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
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Contains helper methods for methods re-execution on failure.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Executes the <paramref name="action"/> given with retry logic.
        /// </summary>
        /// <param name="action">Action to be executed.</param>
        /// <param name="retryCount">Maximum amount of retries.</param>
        /// <param name="retryDelay">Interval between retry attempts.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryCount"/> or <paramref name="retryDelay"/> is negative.</exception>
        /// <seealso cref="Retry{T}(Func{T},int,TimeSpan)"/>
        public static void Retry(Action action, int retryCount, TimeSpan retryDelay)
        {
            RetryHelper.Retry(action, retryCount, retryDelay, new[] { typeof(Exception) });
        }

        /// <summary>
        /// Executes the <paramref name="action"/> given with retry logic.
        /// </summary>
        /// <param name="action">Action to be executed.</param>
        /// <param name="retryCount">Maximum amount of retries.</param>
        /// <param name="retryDelay">Interval between retry attempts.</param>
        /// <param name="retryOnExceptions">List of exceptions on which to retry.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="action"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="retryOnExceptions"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryCount"/> or <paramref name="retryDelay"/> is negative.</exception>
        /// <exception cref="ArgumentException">Any of <paramref name="retryOnExceptions"/> elements are not an exception types.</exception>
        /// <seealso cref="Retry{T}(Func{T},int,TimeSpan)"/>
        public static void Retry(Action action, int retryCount, TimeSpan retryDelay, Type[] retryOnExceptions)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // Just call the overloaded method.
            RetryHelper.Retry(
                () =>
                {
                    action();
                    return true;
                },
                retryCount,
                retryDelay,
                retryOnExceptions);
        }

        /// <summary>
        /// Executes the <paramref name="func"/> given with retry logic.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="func">Function to be executed.</param>
        /// <param name="retryCount">Maximum amount of retries.</param>
        /// <param name="retryDelay">Interval between retry attempts.</param>
        /// <returns><paramref name="func"/> return value, if no exception are thrown.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryCount"/> or <paramref name="retryDelay"/> is negative.</exception>
        /// <remarks>
        /// Retry attempt is considered successful, if the <paramref name="func"/> doesn't throw an exception.<br/>
        /// If the exception is thrown, this method wait for the <paramref name="retryDelay"/> interval, and then
        /// re-executes the <paramref name="func"/>.<br/>
        /// If it fails for <paramref name="retryCount"/> times in a row, this method re-throws the last exception occurred.
        /// </remarks>
        public static T Retry<T>(Func<T> func, int retryCount, TimeSpan retryDelay)
        {
            return RetryHelper.Retry(func, retryCount, retryDelay, new[] { typeof(Exception) });
        }

        /// <summary>
        /// Executes the <paramref name="func"/> given with retry logic.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="func">Function to be executed.</param>
        /// <param name="retryCount">Maximum amount of retries.</param>
        /// <param name="retryDelay">Interval between retry attempts.</param>
        /// <param name="retryOnExceptions">List of exceptions on which to retry.</param>
        /// <returns><paramref name="func"/> return value, if no exception are thrown.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="func"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="retryOnExceptions"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryCount"/> or <paramref name="retryDelay"/> is negative.</exception>
        /// <exception cref="ArgumentException">Any of <paramref name="retryOnExceptions"/> elements are not an exception types.</exception>
        /// <remarks>
        /// Retry attempt is considered successful, if the <paramref name="func"/> doesn't throw an exception.<br/>
        /// If the exception is thrown, this method wait for the <paramref name="retryDelay"/> interval, and then
        /// re-executes the <paramref name="func"/>.<br/>
        /// If it fails for <paramref name="retryCount"/> times in a row, this method re-throws the last exception occurred.
        /// </remarks>
        public static T Retry<T>(Func<T> func, int retryCount, TimeSpan retryDelay, Type[] retryOnExceptions)
        {
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "Retry count is negative.");
            }

            if (retryDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(retryDelay), retryDelay, "Retry delay is negative.");
            }

            if (retryOnExceptions == null || retryOnExceptions.Length == 0)
            {
                throw new ArgumentNullException(nameof(retryOnExceptions));
            }

            if (retryOnExceptions.Any(re => !typeof(Exception).IsAssignableFrom(re)))
            {
                throw new ArgumentException("Retriable exceptions list contains element(s) that are not exception types.");
            }

            T result = default(T);

            for (uint i = 0; i <= retryCount; i++)
            {
                try
                {
                    result = func();
                    break;
                }
                catch (Exception e)
                {
                    if (i >= retryCount)
                    {
                        throw;
                    }

                    if (!retryOnExceptions.Any(re => re.IsInstanceOfType(e)))
                    {
                        // For aggregate exceptions we need to also check inner exceptions.
                        AggregateException aggregateException = e as AggregateException;
                        if (aggregateException == null || !aggregateException.InnerExceptions.Any(ie => retryOnExceptions.Any(re => re.IsInstanceOfType(ie))))
                        {
                            throw;
                        }
                    }

                    Thread.Sleep(retryDelay);
                }
            }

            return result;
        }
    }
}
