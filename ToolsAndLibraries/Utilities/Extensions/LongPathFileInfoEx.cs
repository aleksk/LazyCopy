// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LongPathFileInfoEx.cs">
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
    using System.Globalization;
    using System.IO;

    using LongPath;

    /// <summary>
    /// Contains extension methods for the <see cref="LongPathFileInfo"/> objects.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "This suffix is desired here.")]
    public static class LongPathFileInfoEx
    {
        /// <summary>
        /// Checks whether two files have the same size and timestamps.
        /// </summary>
        /// <param name="file1">First file info.</param>
        /// <param name="file2">Second file info.</param>
        /// <returns>Whether or not the files are likely the same.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="file1"/> or <paramref name="file2"/> is <see langword="null"/>.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="file1"/> file does not exist.</exception>
        public static bool Match(this LongPathFileInfo file1, LongPathFileInfo file2)
        {
            if (file1 == null)
            {
                throw new ArgumentNullException(nameof(file1));
            }

            if (!file1.Exists)
            {
                throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, "File file does not exist: {0}", file1.FullName));
            }

            if (file2 == null)
            {
                throw new ArgumentNullException(nameof(file2));
            }

            return file2.Exists
                && file1.Length           == file2.Length
                && file1.CreationTimeUtc  == file2.CreationTimeUtc
                && file1.LastWriteTimeUtc == file2.LastWriteTimeUtc;
        }
    }
}
