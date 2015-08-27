// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PathHelper.cs">
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
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;

    using LazyCopy.Utilities.Native;
    using LongPath;

    /// <summary>
    /// Contains helper functions to work with file system.
    /// </summary>
    public static class PathHelper
    {
        #region Fields

        /// <summary>
        /// Current directory separator as a string.
        /// </summary>
        private static readonly string DirectorySeparator = new string(new[] { Path.DirectorySeparatorChar });

        /// <summary>
        /// Mapping between root paths and DOS device names.
        /// </summary>
        private static readonly IDictionary<string, string> DriveLetterToDeviceName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Generates unique file name in the <paramref name="parentDirectory"/>.
        /// </summary>
        /// <param name="parentDirectory">Directory, where the file will be stored into.</param>
        /// <param name="prefix">File prefix. May be <see langword="null"/>.</param>
        /// <param name="extension">File extension. May be <see langword="null"/>.</param>
        /// <returns>Path to the file.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="parentDirectory"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The result file name will look like the following: <c>&lt;prefix&gt;&lt;GUID&gt;.&lt;extension&gt;</c><br/>
        /// For example:<br/>
        /// <code>
        ///     PathHelper.GenerateUniqueFileName(@"C:\Temp\", "a_", "bin");  // C:\Temp\a_8F3C680522E0457896837ADF38ED8E6A.bin
        ///     PathHelper.GenerateUniqueFileName(@"C:\Temp\", "b_", ".txt"); // C:\Temp\b_BF3C086BE0C444B4906E4A36CBC16551.txt
        /// </code>
        /// </remarks>
        public static string GenerateUniqueFileName(string parentDirectory, string prefix, string extension)
        {
            if (string.IsNullOrEmpty(parentDirectory))
            {
                throw new ArgumentNullException(nameof(parentDirectory));
            }

            prefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.Trim();

            extension = extension?.Trim().TrimStart('.') ?? string.Empty;
            extension = string.IsNullOrEmpty(extension) ? string.Empty : "." + extension;

            string result;
            do
            {
                result = Path.Combine(parentDirectory, prefix + Guid.NewGuid().ToString("N") + extension);
            }
            while (LongPathFile.Exists(result));

            return result;
        }

        /// <summary>
        /// Returns the file name of the specified <paramref name="path"/> string without the <paramref name="extension"/>.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="extension">Extension to be removed (with or without leading dot), if the <paramref name="path"/> has it.</param>
        /// <returns>File name without extension.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="extension"/> is <see langword="null"/> or empty.</exception>
        /// <remarks>
        /// If the <paramref name="path"/> does not end with the <paramref name="extension"/>, its original value is returned.
        /// </remarks>
        public static string GetFileNameWithoutExtension(string path, string extension)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (!extension.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                extension = "." + extension;
            }

            return path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);
        }

        /// <summary>
        /// Makes sure that the <paramref name="path"/> ends with the directory separator.
        /// </summary>
        /// <param name="path">The path information to modify.</param>
        /// <returns>Path ending with the directory separator.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        public static string EndWithDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + PathHelper.DirectorySeparator;
        }

        /// <summary>
        /// Truncates a path to fit within a certain number of characters by replacing path components with ellipses.
        /// </summary>
        /// <param name="longPathName">Path to be altered.</param>
        /// <param name="newLength">The maximum number of characters to be contained in the new string.</param>
        /// <returns>Altered path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="longPathName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is negative or equal to zero.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="longPathName"/> cannot be compacted.</exception>
        /// <remarks>
        /// The <c>'/'</c> separator will be used instead of <c>'\'</c> if the original string used it.<br/>
        /// If <paramref name="longPathName"/> points to a file name that is too long, instead of a path,
        /// the file name will be truncated to <paramref name="newLength"/> characters, including the ellipsis
        /// and the terminating <see langword="null"/> character.<br/>
        /// For example, if the input file name is <c>"My Filename"</c> and <paramref name="newLength"/> is <c>10</c>,
        /// this method will return <c>"My Fil..."</c>.
        /// </remarks>
        public static string CompactPath(string longPathName, int newLength)
        {
            if (string.IsNullOrEmpty(longPathName))
            {
                throw new ArgumentNullException(nameof(longPathName));
            }

            if (newLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newLength), "Wanted length is negative or equal to zero.");
            }

            StringBuilder sb = new StringBuilder(newLength + 1);

            if (!NativeMethods.PathCompactPathEx(sb, longPathName, newLength + 1, 0))
            {
                Exception nativeException = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to compact path {0}.", longPathName), nativeException);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Changes the parent of a <paramref name="path"/> string.
        /// </summary>
        /// <param name="path">The path information to modify.</param>
        /// <param name="oldParent">The old parent.</param>
        /// <param name="newParent">The new parent.</param>
        /// <returns>The modified path information.</returns>
        /// <exception cref="ArgumentNullException">Any of the parameters is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> string does not start with the <paramref name="oldParent"/>.</exception>
        /// <remarks>
        /// This method is useful, when you need not just replace the path root, but one of its parent.<br/>
        /// For example, to transform <c>C:\Source\SubDir\File.txt</c> into <c>D:\Target\SubDir\File.txt</c> the following code may be used:
        /// <code>
        /// string targetFile = PathHelper.ChangeParent(@"C:\Source\File.txt", @"C:\Source", @"D:\Target");
        /// </code>
        /// </remarks>
        public static string ChangeParent(string path, string oldParent, string newParent)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrEmpty(oldParent))
            {
                throw new ArgumentNullException(nameof(oldParent));
            }

            if (newParent == null)
            {
                throw new ArgumentNullException(nameof(newParent));
            }

            oldParent = PathHelper.EndWithDirectorySeparator(oldParent);
            newParent = string.IsNullOrEmpty(newParent) ? string.Empty : PathHelper.EndWithDirectorySeparator(newParent);

            if (!path.StartsWith(oldParent, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Path '{0}' does not start with '{1}'", path, oldParent));
            }

            return newParent + path.Substring(oldParent.Length);
        }

        /// <summary>
        /// Changes the drive letter for the <paramref name="path"/> given to the according DOS device name.
        /// </summary>
        /// <param name="path">Path to replace the drive letter for.</param>
        /// <returns>New path string.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> is too short.</exception>
        /// <exception cref="InvalidOperationException">The according DOS device name could not be found.</exception>
        /// <remarks>
        /// This method also supports UNC paths.
        /// </remarks>
        public static string ChangeDriveLetterToDeviceName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.Length < 2)
            {
                throw new ArgumentException("Path is too short.", nameof(path));
            }

            // If the path is already converted, skip.
            if (path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            path = LongPathCommon.RemoveLongPathPrefix(path);

            // Convert UNC path to a Network Redirector path.
            if (path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"//", StringComparison.OrdinalIgnoreCase))
            {
                return @"\Device\Mup\" + path.Substring(2);
            }

            // C:\, for example. Or will contain invalid string and will fail later.
            string driveLetter = path.Substring(0, 2) + "\\";
            string deviceName;

            lock (PathHelper.DriveLetterToDeviceName)
            {
                // Update the device-root caches, if the root path is not there.
                if (!PathHelper.DriveLetterToDeviceName.ContainsKey(driveLetter))
                {
                    PathHelper.UpdateDeviceMappings();

                    if (!PathHelper.DriveLetterToDeviceName.ContainsKey(driveLetter))
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to find DOS device name for path: {0}", path));
                    }
                }

                deviceName = PathHelper.DriveLetterToDeviceName[driveLetter];
            }

            // Change parent.
            return deviceName + path.Substring(driveLetter.Length);
        }

        /// <summary>
        /// Changes the DOS device name for the <paramref name="path"/> given to the according drive letter.
        /// </summary>
        /// <param name="path">Path to replace DOS device name for.</param>
        /// <returns>New path string.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> does not start with a DOS device name.</exception>
        /// <exception cref="InvalidOperationException">Drive letter could not be found for the <paramref name="path"/> given.</exception>
        /// <remarks>
        /// This method also supports UNC paths.
        /// </remarks>
        public static string ChangeDeviceNameToDriveLetter(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            path = LongPathCommon.RemoveLongPathPrefix(path);

            // Path is already converted.
            if (Regex.IsMatch(path, @"^(?:\\\\|[a-z]:)", RegexOptions.IgnoreCase))
            {
                return path;
            }

            if (!path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Path given does not start with a device name: {0}", path), nameof(path));
            }

            // Convert Network Redirector path to UNC.
            if (path.StartsWith(@"\Device\Mup\", StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + path.Substring(12);
            }

            string driveLetter;
            string deviceName = null;

            // Find the proper device name.
            lock (PathHelper.DriveLetterToDeviceName)
            {
                Func<string> findDeviceName = () => deviceName = PathHelper.DriveLetterToDeviceName.Values.FirstOrDefault(dn => path.StartsWith(dn, StringComparison.OrdinalIgnoreCase));
                if (findDeviceName() == null)
                {
                    PathHelper.UpdateDeviceMappings();

                    if (findDeviceName() == null)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to find root path for {0}", path));
                    }
                }

                // Find the root path for the device name given.
                driveLetter = PathHelper.DriveLetterToDeviceName.Keys.First(rp => string.Equals(PathHelper.DriveLetterToDeviceName[rp], deviceName));
            }

            // Change parent.
            return driveLetter + path.Substring(deviceName.Length);
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Updates the internal maps between the volume names (<c>D:\</c>) and device names (<c>\Device\HarddiskVolume2\</c>).
        /// </summary>
        private static void UpdateDeviceMappings()
        {
            List<string> driveLetters = DriveInfo.GetDrives().Select(d => d.RootDirectory.Name).Select(PathHelper.EndWithDirectorySeparator).ToList();

            foreach (string driveLetter in driveLetters)
            {
                string deviceName = PathHelper.EndWithDirectorySeparator(PathHelper.GetDeviceDosName(driveLetter));
                PathHelper.DriveLetterToDeviceName[driveLetter] = deviceName;
            }
        }

        /// <summary>
        /// Queries OS for the device name.
        /// </summary>
        /// <param name="rootPath">Root path to to get device name for.</param>
        /// <returns>Device name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rootPath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Unable to get device name for the <paramref name="rootPath"/> specified.</exception>
        private static string GetDeviceDosName(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentNullException(nameof(rootPath));
            }

            // Remove the trailing backslash, as the 'GetDeviceDosName' method requires.
            string deviceName = rootPath.Trim().TrimEnd(Path.DirectorySeparatorChar);

            StringBuilder builder = new StringBuilder(NativeMethods.MaxPath);

            uint numBytes = NativeMethods.QueryDosDevice(deviceName, builder, builder.Capacity);
            if (numBytes == 0)
            {
                int error = Marshal.GetHRForLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get DOS name for the device '{0}': 0x{1:X8}", deviceName, error));
            }

            return builder.ToString();
        }

        #endregion // Private methods
    }
}
