// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReparsePointHelper.cs">
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
    using System.Collections;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;

    using LazyCopy.Utilities.Native;
    using LongPath;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Contains helper methods to work with the reparse points.
    /// </summary>
    /// <remarks>
    /// Reparse points: <c>https://msdn.microsoft.com/en-us/library/windows/desktop/aa365503(v=vs.85).aspx</c>
    /// </remarks>
    public static class ReparsePointHelper
    {
        #region Fields

        /// <summary>
        /// <c>FSCTL_GET_REPARSE_POINT</c> control code value.
        /// </summary>
        private const int GetReparsePointControlCode = 0x000900A8;

        /// <summary>
        /// <c>FSCTL_SET_REPARSE_POINT</c> control code value.
        /// </summary>
        private const int SetReparsePointControlCode = 0x000900A4;

        /// <summary>
        /// Default buffer size to work with the reparse points.
        /// </summary>
        private const int BufferSize = 1024;

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Sets the reparse point data for the <paramref name="path"/> given.
        /// </summary>
        /// <param name="path">File or directory to set the reparse point data for.</param>
        /// <param name="data">
        /// Reparse point data to be set.
        /// It should be a value type, or an <see cref="IEnumerable"/>, and it should not contain
        /// reparse point data header information, because this function handles it separately.
        /// </param>
        /// <param name="reparseTag">Reparse point tag.</param>
        /// <param name="reparseGuid">Reparse point <see cref="Guid"/>. Must be specified, if the <paramref name="reparseTag"/> is a non-Microsoft tag.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="data"/> is not a collection or a value type.
        ///     <para>-or-</para>
        /// <paramref name="reparseTag"/> is a Microsoft tag, but the <paramref name="reparseGuid"/> is not <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="reparseTag"/> is a non-Microsoft tag, but the <paramref name="reparseGuid"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Reparse point <paramref name="data"/> cannot be set for the <paramref name="path"/>.
        /// </exception>
        /// <exception cref="IOException">
        /// <paramref name="path"/> cannot be accessed.
        /// </exception>
        /// <remarks>
        /// This method will <i>NOT</i> update file attributes.<br/>
        /// For example, the <see cref="FileAttributes.ReparsePoint"/> will not be set.
        /// </remarks>
        public static void SetReparsePointData(string path, object data, int reparseTag, Guid? reparseGuid)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            ReparsePointHelper.ValidateTagAndGuid(reparseTag, reparseGuid);

            bool isMicrosoftTag   = ReparsePointHelper.IsMicrosoftTag(reparseTag);
            string normalizedPath = LongPathCommon.NormalizePath(path);

            if (!LongPathCommon.Exists(normalizedPath))
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "{0} cannot be found.", path));
            }

            using (SafeFileHandle handle = NativeMethods.CreateFile(
                normalizedPath,
                AccessRights.GenericWrite,
                FileShare.None,
                IntPtr.Zero,
                FileMode.Open,
                EFileAttributes.OpenReparsePoint | EFileAttributes.BackupSemantics,
                IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Exception nativeException = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                    throw new IOException(string.Format(CultureInfo.InvariantCulture, "Unable to open: {0}", path), nativeException);
                }

                int dataSize = MarshalingHelper.GetObjectSize(data);

                object header = isMicrosoftTag
                                ? (object)new ReparseDataBufferHeader { ReparseDataLength = unchecked((ushort)dataSize), ReparseTag = reparseTag }
                                : new ReparseGuidDataBufferHeader     { ReparseDataLength = unchecked((ushort)dataSize), ReparseTag = reparseTag, ReparseGuid = reparseGuid.Value };

                int headerSize    = Marshal.SizeOf(header);
                int tagDataLength = headerSize + dataSize;

                using (ResizableBuffer buffer = new ResizableBuffer(Math.Max(ReparsePointHelper.BufferSize, tagDataLength)))
                {
                    MarshalingHelper.MarshalObjectToPointer(new[] { header, data }, buffer.DangerousGetPointer());

                    // Set the reparse point data.
                    int bytesReturned;
                    bool success = NativeMethods.DeviceIoControl(
                        handle,
                        ReparsePointHelper.SetReparsePointControlCode,
                        buffer.DangerousGetPointer(),
                        tagDataLength,
                        IntPtr.Zero,
                        0,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!success)
                    {
                        Exception nativeException = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to set the reparse point data: {0}", path), nativeException);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the reparse point data from the <paramref name="path"/> given.
        /// </summary>
        /// <typeparam name="T">
        /// Reparse point buffer data type.
        /// It should not contain reparse point data header information, because this function handles it separately.
        /// </typeparam>
        /// <param name="path">Path to the reparse point to get data from.</param>
        /// <param name="reparseTag">Reparse point tag.</param>
        /// <param name="reparseGuid">Reparse point <see cref="Guid"/>. Must be specified, if the <paramref name="reparseTag"/> is a non-Microsoft tag.</param>
        /// <returns>Reparse point data found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="reparseTag"/> is a Microsoft tag, but the <paramref name="reparseGuid"/> is not <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="reparseTag"/> is a non-Microsoft tag, but the <paramref name="reparseGuid"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="path"/> cannot be found.
        ///     <para>-or-</para>
        /// <paramref name="path"/> is not a reparse point.
        ///     <para>-or-</para>
        /// <paramref name="path"/> reparse point cannot be opened.
        ///     <para>-or-</para>
        /// <paramref name="path"/> reparse point data cannot be retrieved.
        ///     <para>-or-</para>
        /// <paramref name="path"/> reparse point tag or GUID is invalid.
        /// </exception>
        /// <remarks>
        /// See <c>http://msdn.microsoft.com/en-us/library/windows/desktop/aa365511(v=vs.85).aspx</c> for more details about reparse point tags.
        /// </remarks>
        public static T GetReparsePointData<T>(string path, int reparseTag, Guid? reparseGuid)
            where T : struct
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            ReparsePointHelper.ValidateTagAndGuid(reparseTag, reparseGuid);

            bool isMicrosoftTag   = ReparsePointHelper.IsMicrosoftTag(reparseTag);
            string normalizedPath = LongPathCommon.NormalizePath(path);

            bool isDirectory;
            if (!LongPathCommon.Exists(normalizedPath, out isDirectory))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Path does not exist: {0}", path));
            }

            // Check, whether the path given is a reparse point.
            FileAttributes attributes = isDirectory ? new LongPathDirectoryInfo(normalizedPath).Attributes : LongPathFile.GetAttributes(normalizedPath);
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Path given is not a reparse point: {0}", path));
            }

            using (SafeFileHandle handle = NativeMethods.CreateFile(
                normalizedPath,
                AccessRights.GenericRead,
                FileShare.Read,
                IntPtr.Zero,
                FileMode.Open,
                EFileAttributes.OpenReparsePoint | EFileAttributes.BackupSemantics,
                IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Exception nativeException = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to open reparse point: {0}", path), nativeException);
                }

                // Make sure that the buffer will be large enough to hold reparse point data.
                int initialBufferSize = Math.Max(ReparsePointHelper.BufferSize, Marshal.SizeOf(typeof(ReparseGuidDataBufferHeader)) + Marshal.SizeOf(typeof(T)));

                using (ResizableBuffer buffer = new ResizableBuffer(initialBufferSize))
                {
                    // Query the reparse point data.
                    int bytesReturned;
                    bool success = NativeMethods.DeviceIoControl(
                        handle,
                        ReparsePointHelper.GetReparsePointControlCode,
                        IntPtr.Zero,
                        0,
                        buffer.DangerousGetPointer(),
                        buffer.ByteLength,
                        out bytesReturned,
                        IntPtr.Zero);

                    int headerSize = isMicrosoftTag ? Marshal.SizeOf(typeof(ReparseDataBufferHeader)) : Marshal.SizeOf(typeof(ReparseGuidDataBufferHeader));

                    if (!success)
                    {
                        int hr = Marshal.GetHRForLastWin32Error();
                        if (hr != NativeMethods.ErrorMoreData)
                        {
                            Exception nativeException = Marshal.GetExceptionForHR(hr);
                            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get the reparse point data: {0}", path), nativeException);
                        }

                        // Read the ReparseDataLength value, and resize buffer to fit the data.
                        int dataSize = headerSize + Marshal.ReadInt16(buffer.DangerousGetPointer(), 4 /* sizeof(uint) */);

                        buffer.Resize(dataSize);

                        success = NativeMethods.DeviceIoControl(
                            handle,
                            ReparsePointHelper.GetReparsePointControlCode,
                            IntPtr.Zero,
                            0,
                            buffer.DangerousGetPointer(),
                            buffer.ByteLength,
                            out bytesReturned,
                            IntPtr.Zero);

                        if (!success)
                        {
                            Exception nativeException = Marshal.GetExceptionForHR(hr);
                            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get the reparse point data: {0}", path), nativeException);
                        }
                    }

                    // Make sure that the reparse tag is correct.
                    uint tag = unchecked((uint)Marshal.ReadInt32(buffer.DangerousGetPointer()));
                    if (tag != unchecked((uint)reparseTag))
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Reparse point tag is invalid. Path has 0x{0:X8}, but 0x{1:X8} is specified.", tag, reparseTag));
                    }

                    // Make sure that the reparse point GUID is correct, if needed.
                    if (!isMicrosoftTag)
                    {
                        ReparseGuidDataBufferHeader header = (ReparseGuidDataBufferHeader)Marshal.PtrToStructure(buffer.DangerousGetPointer(), typeof(ReparseGuidDataBufferHeader));
                        if (header.ReparseGuid != reparseGuid)
                        {
                            throw new InvalidOperationException(string.Format(
                                CultureInfo.InvariantCulture,
                                "Reparse point GUID is invalid. Path has {0}, but {1} is specified.",
                                header.ReparseGuid.ToString("N"),
                                reparseGuid.Value.ToString("N")));
                        }
                    }

                    return (T)Marshal.PtrToStructure(buffer.DangerousGetPointer() + headerSize, typeof(T));
                }
            }
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Determines whether a reparse point tag indicates a Microsoft reparse point.
        /// </summary>
        /// <param name="reparseTag">The reparse point tag to be tested.</param>
        /// <returns><see langword="true"/>, if the <paramref name="reparseTag"/> is a Microsoft tag; otherwise, <see langword="false"/>.</returns>
        private static bool IsMicrosoftTag(int reparseTag)
        {
            return reparseTag < 0;
        }

        /// <summary>
        /// Validates the reparse point tag and GUID given.
        /// </summary>
        /// <param name="reparseTag">Reparse point tag.</param>
        /// <param name="reparseGuid">Reparse point GUID.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="reparseTag"/> is a Microsoft tag, but the <paramref name="reparseGuid"/> is not <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="reparseTag"/> is a non-Microsoft tag, but the <paramref name="reparseGuid"/> is <see langword="null"/>.
        /// </exception>
        private static void ValidateTagAndGuid(int reparseTag, Guid? reparseGuid)
        {
            bool isMicrosoftTag = ReparsePointHelper.IsMicrosoftTag(reparseTag);

            if (isMicrosoftTag && reparseGuid.HasValue)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "0x{0:X8} is a Microsoft tag, but the reparse GUID is specified.", reparseTag), nameof(reparseGuid));
            }

            if (!isMicrosoftTag && !reparseGuid.HasValue)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "0x{0:X8} is a non-Microsoft tag, but the reparse GUID is not specified.", reparseTag), nameof(reparseGuid));
            }
        }

        #endregion // Private methods
    }
}
