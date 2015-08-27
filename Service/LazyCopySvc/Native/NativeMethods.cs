// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs">
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

namespace LazyCopy.Service.Native
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Contains P/Invoke method prototypes.
    /// </summary>
    internal static class NativeMethods
    {
        #region kernel32.dll

        /// <summary>
        /// Creates or opens a file or I/O device.
        /// </summary>
        /// <param name="fileName">The name of the file or device to be created or opened.</param>
        /// <param name="desiredAccess">The requested access to the file or device, which can be summarized as read, write, both or neither zero.</param>
        /// <param name="shareMode">The requested sharing mode of the file or device, which can be read, write, both, delete, all of these, or none.</param>
        /// <param name="securityAttributes">A pointer to a <c>SECURITY_ATTRIBUTES</c> structure.</param>
        /// <param name="creationDisposition">An action to take on a file or device that exists or does not exist.</param>
        /// <param name="flagsAndAttributes">The file or device attributes and flags.</param>
        /// <param name="templateFile">A valid handle to a template file with the <c>GENERIC_READ</c> access right.</param>
        /// <returns>If the function succeeds, the return value is an open handle to the specified file, device, named pipe, or mail slot.</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileAccess desiredAccess,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileShare shareMode,
            /* [in] */ IntPtr securityAttributes,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            /* [in] */ IntPtr templateFile);

        /// <summary>
        /// Closes an open object handle.
        /// </summary>
        /// <param name="handle">A valid handle to an open object.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(
            /* [in] */ IntPtr handle);

        #endregion // kernel32.dll
    }
}
