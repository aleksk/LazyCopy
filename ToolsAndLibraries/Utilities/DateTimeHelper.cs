// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeHelper.cs">
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

    /// <summary>
    /// Contains helper methods to work with the <see cref="DateTime"/> structure.
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>
        /// Converts the <c>UNIX</c> epoch <paramref name="time"/> to the <see cref="DateTime"/>.
        /// </summary>
        /// <param name="time"><c>UNIX</c> epoch time to convert.</param>
        /// <returns><see cref="DateTime"/> instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="time"/> is negative.</exception>
        public static DateTime FromUnixTime(long time)
        {
            if (time < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, "UNIX epoch time cannot be negative.");
            }

            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return dateTime.AddSeconds(time);
        }
    }
}
