// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CompressionHelper.cs">
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
    using System.IO.Compression;

    using LongPath;

    /// <summary>
    /// Contains helper methods to work with the compressed files.
    /// </summary>
    public static class CompressionHelper
    {
        /// <summary>
        /// Compresses the <paramref name="fileToCompress"/> given and saves the result into the <paramref name="targetFile"/>.
        /// </summary>
        /// <param name="fileToCompress">File to compress.</param>
        /// <param name="targetFile">File to save the compressed data into.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="fileToCompress"/> is <see langword="null"/> or empty.
        ///     <para>-or-</para>
        /// <paramref name="targetFile"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="IOException">
        /// <paramref name="fileToCompress"/> cannot be opened.
        ///     <para>-or-</para>
        /// <paramref name="targetFile"/> cannot be created.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Multiple stream disposal won't throw an exception.")]
        public static void Compress(string fileToCompress, string targetFile)
        {
            if (string.IsNullOrEmpty(fileToCompress))
            {
                throw new ArgumentNullException(nameof(fileToCompress));
            }

            if (string.IsNullOrEmpty(targetFile))
            {
                throw new ArgumentNullException(nameof(targetFile));
            }

            using (FileStream originalFileStream   = LongPathFile.OpenRead(fileToCompress))
            using (FileStream compressedFileStream = LongPathFile.Create(targetFile))
            using (GZipStream compressedStream     = new GZipStream(compressedFileStream, CompressionLevel.Optimal))
            {
                originalFileStream.CopyTo(compressedStream);
            }
        }

        /// <summary>
        /// Decompresses the <paramref name="fileToDecompress"/> given and saves the result into the <paramref name="targetFile"/>.
        /// </summary>
        /// <param name="fileToDecompress">File to decompress.</param>
        /// <param name="targetFile">File to save the compressed data into.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="fileToDecompress"/> is <see langword="null"/> or empty.
        ///     <para>-or-</para>
        /// <paramref name="targetFile"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="IOException">
        /// <paramref name="fileToDecompress"/> cannot be opened.
        ///     <para>-or-</para>
        /// <paramref name="targetFile"/> cannot be created.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Multiple stream disposal won't throw an exception.")]
        public static void Decompress(string fileToDecompress, string targetFile)
        {
            if (string.IsNullOrEmpty(fileToDecompress))
            {
                throw new ArgumentNullException(nameof(fileToDecompress));
            }

            if (string.IsNullOrEmpty(targetFile))
            {
                throw new ArgumentNullException(nameof(targetFile));
            }

            using (FileStream sourceStream        = LongPathFile.OpenRead(fileToDecompress))
            using (GZipStream decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
            using (FileStream decompressedStream  = LongPathFile.Create(targetFile))
            {
                decompressionStream.CopyTo(decompressedStream);
            }
        }
    }
}
