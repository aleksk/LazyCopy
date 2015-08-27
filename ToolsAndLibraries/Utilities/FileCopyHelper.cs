// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileCopyHelper.cs">
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
    using System.IO;

    using LazyCopy.Utilities.Extensions;
    using LongPath;

    /// <summary>
    /// This class contains helper methods for copying files.
    /// </summary>
    public static class FileCopyHelper
    {
        /// <summary>
        /// Copies the <paramref name="sourceFile"/> to the <paramref name="targetFile"/>.
        /// </summary>
        /// <param name="sourceFile">Source file to be copied.</param>
        /// <param name="targetFile">Target file.</param>
        /// <returns><see langword="true"/>, if the file was created or overwritten; <see langword="false"/>, if the file was skipped.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sourceFile"/> or <paramref name="sourceFile"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="sourceFile"/> does not exist.</exception>
        public static bool CopyFile(string sourceFile, string targetFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
            {
                throw new ArgumentNullException(nameof(sourceFile));
            }

            if (string.IsNullOrEmpty(targetFile))
            {
                throw new ArgumentNullException(nameof(targetFile));
            }

            return FileCopyHelper.CopyFile(new LongPathFileInfo(sourceFile), new LongPathFileInfo(targetFile));
        }

        /// <summary>
        /// Copies the <paramref name="sourceFileInfo"/> to the <paramref name="targetFileInfo"/>.
        /// </summary>
        /// <param name="sourceFileInfo">Source file to be copied.</param>
        /// <param name="targetFileInfo">Target file.</param>
        /// <returns><see langword="true"/>, if the file was created or overwritten; <see langword="false"/>, if the file was skipped.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sourceFileInfo"/> or <paramref name="targetFileInfo"/> is <see langword="null"/>.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="sourceFileInfo"/> does not exist.</exception>
        public static bool CopyFile(LongPathFileInfo sourceFileInfo, LongPathFileInfo targetFileInfo)
        {
            if (sourceFileInfo == null)
            {
                throw new ArgumentNullException(nameof(sourceFileInfo));
            }

            if (targetFileInfo == null)
            {
                throw new ArgumentNullException(nameof(targetFileInfo));
            }

            // Will throw an exception, if the source file does not exist.
            if (sourceFileInfo.Match(targetFileInfo))
            {
                return false;
            }

            LongPathDirectory.CreateDirectory(targetFileInfo.DirectoryName);
            sourceFileInfo.CopyTo(targetFileInfo.FullName, true);

            LongPathCommon.SetAttributes(targetFileInfo.FullName, FileAttributes.Normal);
            LongPathCommon.SetTimestamps(targetFileInfo.FullName, sourceFileInfo.CreationTime, sourceFileInfo.LastAccessTime, sourceFileInfo.LastWriteTime);

            return true;
        }
    }
}
