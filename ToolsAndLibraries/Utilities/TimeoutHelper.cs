// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TimeoutHelper.cs">
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
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// A simple timeout helper that can tell whether a timeout has expired, how much time has elapsed,
    /// and how much time is remaining.
    /// </summary>
    public class TimeoutHelper
    {
        #region Fields

        /// <summary>
        /// The amount of time allocated before a timeout.
        /// </summary>
        private readonly TimeSpan timeout;

        /// <summary>
        /// The stopwatch used to keep track of elapsed time.
        /// </summary>
        private readonly Stopwatch stopWatch;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutHelper"/> class.
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Timeout is a negative number other than <c>-1</c> milliseconds, which represents an infinite time-out.</exception>
        public TimeoutHelper(int timeoutMilliseconds)
            : this(TimeSpan.FromMilliseconds(timeoutMilliseconds))
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutHelper"/> class.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="timeout"/> is a negative number other than <c>-1</c> milliseconds, which represents an infinite time-out.
        ///     <para>-or-</para>
        /// <paramref name="timeout"/> is greater than <see cref="int.MaxValue"/>.
        /// </exception>
        public TimeoutHelper(TimeSpan timeout)
        {
            if (timeout < System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout is a negative number other than -1 milliseconds, which represents an infinite time-out.");
            }

            if (timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout is greater than Int32.MaxValue.");
            }

            this.timeout   = timeout;
            this.stopWatch = Stopwatch.StartNew();
        }

        #endregion // Constructors

        #region Properties

        /// <summary>
        /// Gets a value indicating whether or not the timeout period has expired.
        /// </summary>
        public bool IsExpired
        {
            get
            {
                if (this.timeout == System.Threading.Timeout.InfiniteTimeSpan || this.timeout == TimeSpan.Zero)
                {
                    return false;
                }

                return this.timeout < this.stopWatch.Elapsed;
            }
        }

        /// <summary>
        /// Gets the time remaining before timeout.
        /// </summary>
        public TimeSpan Remaining
        {
            get
            {
                if (this.timeout == System.Threading.Timeout.InfiniteTimeSpan || this.timeout == TimeSpan.Zero)
                {
                    return this.timeout;
                }

                TimeSpan remaining = this.timeout - this.stopWatch.Elapsed;

                // Make sure that the remaining time is not equal to '-1' (infinity) after substraction. I bet no one wants to debug this issue.
                return remaining == System.Threading.Timeout.InfiniteTimeSpan ? TimeSpan.FromMilliseconds(-2) : remaining;
            }
        }

        /// <summary>
        /// Gets the elapsed time.
        /// </summary>
        public TimeSpan Elapsed => this.stopWatch.Elapsed;

        /// <summary>
        /// Gets the timeout.
        /// </summary>
        public TimeSpan Timeout => this.timeout;

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Waited {0:0.000} of {1:0.000} seconds.", this.Elapsed.TotalSeconds, this.timeout.TotalSeconds);
        }

        #endregion // Public methods
    }
}
