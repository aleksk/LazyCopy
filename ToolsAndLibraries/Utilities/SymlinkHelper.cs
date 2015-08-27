// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SymlinkHelper.cs">
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
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    using LazyCopy.Utilities.Native;
    using LongPath;

    /// <summary>
    /// Contains helper methods for symbolic links management.
    /// </summary>
    /// <remarks>
    /// Symbolic links: <c>https://msdn.microsoft.com/en-us/library/windows/desktop/aa365680(v=vs.85).aspx</c>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Symlink", Justification = "The spelling is correct.")]
    public static class SymlinkHelper
    {
        #region Fields

        /// <summary>
        /// Reparse tag used for the symbolic links.
        /// </summary>
        private const int SymbolicLinkTag = unchecked((int)0xA000000C);

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Creates the file symbolic link.
        /// </summary>
        /// <param name="linkPath">Link path.</param>
        /// <param name="targetPath">Target path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetPath"/> or <paramref name="linkPath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetPath"/> is equal to the <paramref name="linkPath"/>.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="targetPath"/> was not found.</exception>
        /// <exception cref="InvalidOperationException">Symbolic link was not created.</exception>
        /// <remarks>
        /// The <c>SE_CREATE_SYMBOLIC_LINK_NAME</c> privilege is required to create symbolic links.
        /// </remarks>
        public static void CreateFileLink(string linkPath, string targetPath)
        {
            SymlinkHelper.CreateSymbolicLink(linkPath, targetPath, SymbolicLinkFlag.File);
        }

        /// <summary>
        /// Creates the directory symbolic link.
        /// </summary>
        /// <param name="linkPath">Link path.</param>
        /// <param name="targetPath">Target path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetPath"/> or <paramref name="linkPath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetPath"/> is equal to the <paramref name="linkPath"/>.</exception>
        /// <exception cref="DirectoryNotFoundException"><paramref name="targetPath"/> was not found.</exception>
        /// <exception cref="InvalidOperationException">Symbolic link was not created.</exception>
        /// <remarks>
        /// The <c>SE_CREATE_SYMBOLIC_LINK_NAME</c> privilege is required to create symbolic links.
        /// </remarks>
        public static void CreateDirectoryLink(string linkPath, string targetPath)
        {
            SymlinkHelper.CreateSymbolicLink(linkPath, targetPath, SymbolicLinkFlag.Directory);
        }

        /// <summary>
        /// Gets the target for the symbolic link given.
        /// </summary>
        /// <param name="linkPath">Path to the symbolic link to get target for.</param>
        /// <returns>Path to the link target.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="linkPath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Target information could not be retrieved from the symbolic link.</exception>
        public static string GetLinkTarget(string linkPath)
        {
            SymbolicLinkReparsePointData reparseData = ReparsePointHelper.GetReparsePointData<SymbolicLinkReparsePointData>(linkPath, SymlinkHelper.SymbolicLinkTag, null);
            return Encoding.Unicode.GetString(reparseData.PathBuffer, reparseData.PrintNameOffset, reparseData.PrintNameLength);
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Creates the symbolic link.
        /// </summary>
        /// <param name="linkPath">Link path.</param>
        /// <param name="targetPath">Target path.</param>
        /// <param name="linkFlag">Type of the symbolic link.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetPath"/> or <paramref name="linkPath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetPath"/> is equal to the <paramref name="linkPath"/>.</exception>
        /// <exception cref="FileNotFoundException"><paramref name="targetPath"/> was not found and <paramref name="linkFlag"/> is <see cref="SymbolicLinkFlag.File"/>.</exception>
        /// <exception cref="DirectoryNotFoundException"><paramref name="targetPath"/> was not found and <paramref name="linkFlag"/> is <see cref="SymbolicLinkFlag.Directory"/>.</exception>
        /// <exception cref="InvalidOperationException">Symbolic link was not created.</exception>
        /// <remarks>
        /// The <c>SE_CREATE_SYMBOLIC_LINK_NAME</c> privilege is required to create symbolic links.
        /// </remarks>
        private static void CreateSymbolicLink(string linkPath, string targetPath, SymbolicLinkFlag linkFlag)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                throw new ArgumentNullException(nameof(targetPath));
            }

            if (string.IsNullOrEmpty(linkPath))
            {
                throw new ArgumentNullException(nameof(linkPath));
            }

            if (string.Equals(targetPath, linkPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Target path is equal to the link path: {0}", targetPath));
            }

            string normalizedLinkPath   = LongPathCommon.NormalizePath(linkPath);
            string normalizedTargetPath = LongPathCommon.NormalizePath(targetPath);

            if (!LongPathCommon.Exists(targetPath))
            {
                if (linkFlag == SymbolicLinkFlag.Directory)
                {
                    throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture, "Target directory not found: {0}", targetPath));
                }

                throw new FileNotFoundException("Target file not found.", targetPath);
            }

            if (!NativeMethods.CreateSymbolicLink(normalizedLinkPath, normalizedTargetPath, linkFlag))
            {
                Exception nativeException = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to create symbolic link: {0} -> {1}", linkPath, targetPath), nativeException);
            }
        }

        #endregion // Private methods

        #region Structs

        /// <summary>
        /// Contains information about the symbolic link reparse point data.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SymbolicLinkReparsePointData
        {
            /// <summary>
            /// Offset, in bytes, of the substitute name string in the PathBuffer array.
            /// </summary>
            public ushort SubstituteNameOffset;

            /// <summary>
            /// Length, in bytes, of the substitute name string.
            /// </summary>
            public ushort SubstituteNameLength;

            /// <summary>
            /// Offset, in bytes, of the print name string in the PathBuffer array.
            /// </summary>
            public ushort PrintNameOffset;

            /// <summary>
            /// Length, in bytes, of the print name string.
            /// </summary>
            public ushort PrintNameLength;

            /// <summary>
            /// Used to indicate if the given symbolic link is an absolute or relative symbolic link.
            /// </summary>
            public uint Flags;

            /// <summary>
            /// First character of the path string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15 * 1024)]
            public byte[] PathBuffer;
        }

        #endregion // Structs
    }
}
