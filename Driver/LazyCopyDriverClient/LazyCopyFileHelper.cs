// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyFileHelper.cs">
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

namespace LazyCopy.DriverClient
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    using LazyCopy.Utilities;
    using LongPath;

    /// <summary>
    /// Contains helper methods to work with the <c>LazyCopy</c> reparse files.
    /// </summary>
    public static class LazyCopyFileHelper
    {
        #region Fields

        /// <summary>
        /// LazyCopy reparse point tag.
        /// </summary>
        /// <remarks>
        /// Defined in the <c>Globals.h</c> file.
        /// </remarks>
        private const int LazyCopyReparseTag = 0x340;

        /// <summary>
        /// Reparse point GUID.
        /// </summary>
        /// <remarks>
        /// Defined in the <c>LazyCopyDriver.c</c> file.
        /// </remarks>
        private static readonly Guid LazyCopyReparseGuid = new Guid("{611F0D07-698B-49F4-9DDB-8446662D3325}");

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Creates a new <c>LazyCopy</c> file.
        /// </summary>
        /// <param name="path">Path to the reparse point to get data from.</param>
        /// <param name="fileData">Reparse file data to be set for the <paramref name="path"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="path"/> is <see langword="null"/> or empty.
        ///     <para>-or-</para>
        /// <paramref name="fileData"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="fileData"/> contains <see langword="null"/> or empty file path.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="fileData"/> contains negative file size.</exception>
        /// <exception cref="IOException">File cannot be created.</exception>
        /// <exception cref="InvalidOperationException">Reparse point data cannot be set.</exception>
        public static void CreateLazyCopyFile(string path, LazyCopyFileData fileData)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (fileData == null)
            {
                throw new ArgumentNullException(nameof(fileData));
            }

            if (string.IsNullOrEmpty(fileData.RemotePath))
            {
                throw new ArgumentNullException(nameof(fileData));
            }

            if (fileData.FileSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fileData), fileData.FileSize, "File size is negative.");
            }

            string normalizedPath     = LongPathCommon.NormalizePath(path);
            LongPathFileInfo fileInfo = new LongPathFileInfo(normalizedPath);

            bool shouldCreateFile = false;
            if (!fileInfo.Exists || fileInfo.Length > 0)
            {
                LongPathDirectory.CreateDirectory(fileInfo.DirectoryName);
                shouldCreateFile = true;
            }
            else if (!fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                shouldCreateFile = true;
            }
            else if (fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                fileInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            if (shouldCreateFile)
            {
                using (fileInfo.Create())
                {
                    // Do nothing.
                }
            }

            // If the original file is empty, we don't need to set a reparse point for it.
            if (fileData.FileSize == 0)
            {
                return;
            }

            ReparsePointHelper.SetReparsePointData(
                path,
                new object[] // Custom serialization layout for the LazyCopyFileData object.
                {
                    (long)(fileData.UseCustomHandler ? 1L : 0L),
                    (long)fileData.FileSize,
                    // Add the prefix, if the custom handling is needed for the file.
                    Encoding.Unicode.GetBytes(
                        (fileData.UseCustomHandler ? fileData.RemotePath : PathHelper.ChangeDriveLetterToDeviceName(fileData.RemotePath))
                        + '\0')
                },
                LazyCopyFileHelper.LazyCopyReparseTag,
                LazyCopyFileHelper.LazyCopyReparseGuid);

            // Set the proper file attributes.
            LongPathCommon.SetAttributes(path, FileAttributes.ReparsePoint | FileAttributes.NotContentIndexed | FileAttributes.Offline);
        }

        /// <summary>
        /// Gets the LazyCopy reparse data from the <paramref name="path"/> given.
        /// </summary>
        /// <param name="path">Path to the file to get the reparse data from.</param>
        /// <returns>
        /// Reparse data found or <see langword="null"/>, if it's not set.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="IOException">File cannot be opened.</exception>
        /// <exception cref="InvalidOperationException">Reparse point data cannot be retrieved.</exception>
        public static LazyCopyFileData GetReparseData(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            string normalizedPath     = LongPathCommon.NormalizePath(path);
            LongPathFileInfo fileInfo = new LongPathFileInfo(normalizedPath);
            if (!fileInfo.Exists || !fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return null;
            }

            try
            {
                LazyCopyReparseData data = ReparsePointHelper.GetReparsePointData<LazyCopyReparseData>(path, LazyCopyFileHelper.LazyCopyReparseTag, LazyCopyFileHelper.LazyCopyReparseGuid);
                bool useCustomHandler    = data.UseCustomHandler > 0;

                return new LazyCopyFileData
                {
                    UseCustomHandler = useCustomHandler,
                    FileSize         = data.FileSize,
                    RemotePath       = useCustomHandler ? data.RemotePath : PathHelper.ChangeDeviceNameToDriveLetter(data.RemotePath)
                };
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        #endregion // Public methods

        #region Nested type: LazyCopyReparseData

        /// <summary>
        /// Contains reparse point data for the LazyCopy files.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LazyCopyReparseData
        {
            /// <summary>
            /// Whether the file should be fetched by the user-mode service.
            /// </summary>
            public long UseCustomHandler;

            /// <summary>
            /// Original file size.
            /// </summary>
            public long FileSize;

            /// <summary>
            /// Path the current file should be downloaded from.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8 * 1024)]
            public string RemotePath;
        }

        #endregion // Nested type: LazyCopyReparseData
    }
}
