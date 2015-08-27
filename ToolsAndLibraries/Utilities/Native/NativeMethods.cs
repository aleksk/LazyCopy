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

namespace LazyCopy.Utilities.Native
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Contains P/Invoke method prototypes.
    /// </summary>
    /// <remarks>
    /// List of Win32 error codes:
    /// <c>http://msdn.microsoft.com/en-us/library/cc231199(v=prot.20).aspx</c>
    /// </remarks>
    internal static class NativeMethods
    {
        #region Constants

        /// <summary>
        /// Windows return code for successful operations.
        /// </summary>
        public const int Ok = 0;

        /// <summary>
        /// Maximum path length.
        /// </summary>
        public const int MaxPath = 248;

        /// <summary>
        /// No more data is available.
        /// </summary>
        public const int ErrorNoMoreItems = unchecked((int)0x80070103);

        /// <summary>
        /// The data area passed to a system call is too small.
        /// </summary>
        public const int ErrorInsufficientBuffer = unchecked((int)0x8007007A);

        /// <summary>
        /// More data is available.
        /// </summary>
        public const int ErrorMoreData = unchecked((int)0x800700EA);

        #endregion // Constants

        #region advapi32.dll

        /// <summary>
        /// The <see cref="OpenProcessToken"/> function opens the access token associated with a process.
        /// </summary>
        /// <param name="handle">A handle to the process whose access token is opened.</param>
        /// <param name="desiredAccess">Specifies an access mask that specifies the requested types of access to the access token.</param>
        /// <param name="tokenHandle">A pointer to a handle that identifies the newly opened access token when the function returns.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(
            /* [in]  */ IntPtr handle,
            /* [in]  */ [MarshalAs(UnmanagedType.U4)] TokenAccessRights desiredAccess,
            /* [out] */ out SafeTokenHandle tokenHandle);

        /// <summary>
        /// The <see cref="DuplicateToken"/> function creates a new access token that duplicates one already in existence.
        /// </summary>
        /// <param name="tokenHandle">A handle to an access token opened with <see cref="TokenAccessRights.TokenDuplicate"/> access.</param>
        /// <param name="impersonationLevel">Specifies a <see cref="SecurityImpersonationLevel"/> enumerated type that supplies the impersonation level of the new token.</param>
        /// <param name="duplicatedTokenHandle">A pointer to a variable that receives a handle to the duplicate token.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DuplicateToken(
            /* [in]  */ SafeTokenHandle tokenHandle,
            /* [in]  */ [MarshalAs(UnmanagedType.I4)] SecurityImpersonationLevel impersonationLevel,
            /* [out] */ out IntPtr duplicatedTokenHandle);

        /// <summary>
        /// Retrieves a specified type of information about an access token.
        /// </summary>
        /// <param name="token">A handle to an access token from which information is retrieved.</param>
        /// <param name="tokenInfoClass">Specifies a value from the <see cref="TokenInformationClass"/> enumerated type to identify the type of information the function retrieves.</param>
        /// <param name="tokenInformation">A pointer to a buffer the function fills with the requested information.</param>
        /// <param name="tokeInfoLength">Specifies the size, in bytes, of the buffer pointed to by the <paramref name="tokenInformation"/>.</param>
        /// <param name="actualSize">A pointer to a variable that receives the number of bytes needed for the buffer pointed to by the <paramref name="tokenInformation"/>.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetTokenInformation(
            /* [in]  */ SafeTokenHandle token,
            /* [in]  */ TokenInformationClass tokenInfoClass,
            /* [in]  */ IntPtr tokenInformation,
            /* [in]  */ int tokeInfoLength,
            /* [out] */ out int actualSize);

        /// <summary>
        /// Retrieves the name of the account for this SID and the name of the first domain on which this SID is found.
        /// </summary>
        /// <param name="systemName">Specifies the target computer.</param>
        /// <param name="sid">A pointer to the SID to look up.</param>
        /// <param name="name">Account name that corresponds to the <paramref name="sid"/>.</param>
        /// <param name="nameSize">Specifies the size, in TCHARs, of the <paramref name="name"/>.</param>
        /// <param name="domain">Name of the domain where the account name was found.</param>
        /// <param name="domainSize">Specifies the size, in TCHARs, of the <paramref name="domain"/>.</param>
        /// <param name="use">A pointer to a variable that receives a SID_NAME_USE value that indicates the type of the account.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true, ThrowOnUnmappableChar = true, BestFitMapping = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupAccountSid(
            /* [in]  */ [MarshalAs(UnmanagedType.LPTStr)] string systemName,
            /* [in]  */ IntPtr sid,
            /* [out] */ [MarshalAs(UnmanagedType.LPTStr)] StringBuilder name,
            /* [in]  */ ref int nameSize,
            /* [out] */ [MarshalAs(UnmanagedType.LPTStr)] StringBuilder domain,
            /* [in]  */ ref int domainSize,
            /* [out] */ out SidNameUse use);

        #endregion // advapi32.dll

        #region kernel32.dll

        /// <summary>
        /// Closes an open object handle.
        /// </summary>
        /// <param name="handle">A valid handle to an open object.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(
            /* [in] */ IntPtr handle);

        /// <summary>
        /// Creates a symbolic link.
        /// </summary>
        /// <param name="linkFileName">The symbolic link to be created.</param>
        /// <param name="targetFileName">The name of the target for the symbolic link to be created.</param>
        /// <param name="flags">Indicates whether the link target, <paramref name="targetFileName"/>, is a directory.</param>
        /// <returns>If the function succeeds, the return value is <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", MessageId = "return", Justification = "Declaration is correct.")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CreateSymbolicLink(
            /* [in] */ string linkFileName,
            /* [in] */ string targetFileName,
            /* [in] */ SymbolicLinkFlag flags);

        /// <summary>
        /// Sends a control code directly to a specified device driver, causing the corresponding device to perform the corresponding operation.
        /// </summary>
        /// <param name="deviceHandle">A handle to the device on which the operation is to be performed.</param>
        /// <param name="controlCode">The control code for the operation.</param>
        /// <param name="inBuffer">A pointer to the input buffer that contains the data required to perform the operation.</param>
        /// <param name="inBufferSize">The size of the input buffer, in bytes.</param>
        /// <param name="outBuffer">A pointer to the output buffer that is to receive the data returned by the operation.</param>
        /// <param name="outBufferSize">The size of the output buffer, in bytes.</param>
        /// <param name="bytesReturned">A pointer to a variable that receives the size of the data stored in the output buffer, in bytes.</param>
        /// <param name="overlapped">A pointer to an <c>OVERLAPPED</c> structure.</param>
        /// <returns>If the function succeeds, the return value is <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", MessageId = "return", Justification = "Declaration is correct.")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool DeviceIoControl(
            /* [in]  */ SafeFileHandle deviceHandle,
            /* [in]  */ uint controlCode,
            /* [in]  */ IntPtr inBuffer,
            /* [in]  */ int inBufferSize,
            /* [out] */ IntPtr outBuffer,
            /* [in]  */ int outBufferSize,
            /* [out] */ out int bytesReturned,
            /* [in]  */ IntPtr overlapped);

        /// <summary>
        /// Creates or opens a file or I/O device.
        /// </summary>
        /// <param name="fileName">The name of the file or device to be created or opened.</param>
        /// <param name="desiredAccess">The requested access to the file or device, which can be summarized as read, write, both or neither zero.</param>
        /// <param name="shareMode">The requested sharing mode of the file or device, which can be read, write, both, delete, all of these, or none.</param>
        /// <param name="securityAttributes">A pointer to a <c>SECURITY_ATTRIBUTES</c> structure.</param>
        /// <param name="createMode">An action to take on a file or device that exists or does not exist.</param>
        /// <param name="flagsAndAttributes">The file or device attributes and flags.</param>
        /// <param name="templateFile">A valid handle to a template file with the <c>GENERIC_READ</c> access right.</param>
        /// <returns>If the function succeeds, the return value is an open handle to the specified file, device, named pipe, or mail slot.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] AccessRights desiredAccess,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileShare shareMode,
            /* [in] */ IntPtr securityAttributes,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] FileMode createMode,
            /* [in] */ [MarshalAs(UnmanagedType.U4)] EFileAttributes flagsAndAttributes,
            /* [in] */ IntPtr templateFile);

        /// <summary>
        /// Retrieves information about MS-DOS device names.
        /// </summary>
        /// <param name="deviceName">
        /// An MS-DOS device name string specifying the target of the query.
        /// The device name cannot have a trailing backslash; for example, use <c>"C:"</c>, not <c>"C:\"</c>.
        /// </param>
        /// <param name="targetPath">
        /// A pointer to a data that will receive the result of the query.
        /// </param>
        /// <param name="maxPathChars">
        /// The maximum number of TCHARs that can be stored into the data pointed to by <paramref name="targetPath"/>.
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is the number of TCHARs stored into the data pointed to by <paramref name="targetPath"/>.
        /// If the function fails, the return value is zero.
        /// </returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern uint QueryDosDevice(
            /* [in] */ [MarshalAs(UnmanagedType.LPTStr)] string deviceName,
            /* [in] */ [MarshalAs(UnmanagedType.LPTStr)] StringBuilder targetPath,
            /* [in] */ int maxPathChars);

        /// <summary>
        /// Fills a block of memory with zeros.
        /// </summary>
        /// <param name="handle">
        /// A pointer to the starting address of the block of memory to fill with zeros.
        /// </param>
        /// <param name="length">
        /// The size of the block of memory to fill with zeros, in bytes.
        /// </param>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void ZeroMemory(
            /* [in] */ IntPtr handle,
            /* [in] */ uint length);

        #endregion // kernel32.dll

        #region mpr.dll

        /// <summary>
        /// Makes a connection to a network resource and can redirect a local device to the network resource.
        /// </summary>
        /// <param name="netResource">A pointer to a NETRESOURCE structure that specifies details of the proposed connection.</param>
        /// <param name="password">
        /// Password to be used in making the network connection.<br/>
        /// If <paramref name="password"/> is <see langword="null"/>, the function uses the current default password
        /// associated with the user specified by the <paramref name="username"/> parameter.<br/>
        /// If <paramref name="password"/> points to an empty string, the function does not use a password.
        /// </param>
        /// <param name="username">
        /// User name for making the connection.
        /// If <paramref name="username"/> is <see langword="null"/>, the function uses the default user name.<br/>
        /// The <paramref name="username"/> parameter is specified when users want to connect to a network resource for which
        /// they have been assigned a user name or account other than the default user name or account.
        /// </param>
        /// <param name="flags">A set of connection options.</param>
        /// <returns>If the function succeeds, the return value is <see cref="Ok"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "1", Justification = "This method only supports ANSI.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "2", Justification = "This method only supports ANSI.")]
        [DllImport("mpr.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern int WNetAddConnection2(
            /* [in] */ ref NetResource netResource,
            /* [in] */ [MarshalAs(UnmanagedType.LPStr)] string password,
            /* [in] */ [MarshalAs(UnmanagedType.LPStr)] string username,
            /* [in] */ uint flags);

        /// <summary>
        /// Cancels an existing network connection.
        /// </summary>
        /// <param name="name">The name of either the redirected local device or the remote network resource to disconnect from.</param>
        /// <param name="flags">Connection type.</param>
        /// <param name="force">Specifies whether the disconnection should occur if there are open files or jobs on the connection.</param>
        /// <returns>If the function succeeds, the return value is <see cref="Ok"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "0", Justification = "This method only supports ANSI.")]
        [DllImport("mpr.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern int WNetCancelConnection2(
            /* [in] */ [MarshalAs(UnmanagedType.LPStr)] string name,
            /* [in] */ int flags,
            /* [in] */ [MarshalAs(UnmanagedType.Bool)] bool force);

        #endregion // mpr.dll

        #region user32.dll

        /// <summary>
        /// Retrieves the time of the last input event.
        /// </summary>
        /// <param name="lastInputInfo">Receives the time of the last input event.</param>
        /// <returns><see langword="true"/> if function succeeded; otherwise, <see langword="false"/>.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(
            /* [out] */ ref LastInputInfo lastInputInfo);

        #endregion // user32.dll

        #region shlwapi.dll

        /// <summary>
        /// Truncates a path to fit within a certain number of characters by replacing path components with ellipses.
        /// </summary>
        /// <param name="resultPath">The address of the string that has been altered.</param>
        /// <param name="pathToCompact">Path to be altered.</param>
        /// <param name="wantedLength">The maximum number of characters to be contained in the new string, including the terminating null character.</param>
        /// <param name="flags">Additional flags.</param>
        /// <returns>Returns <see langword="true"/> if successful, or <see langword="false"/> otherwise.</returns>
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PathCompactPathEx(
            /* [out] */ [MarshalAs(UnmanagedType.LPWStr)] StringBuilder resultPath,
            /* [in]  */ [MarshalAs(UnmanagedType.LPWStr)] string pathToCompact,
            /* [in]  */ int wantedLength,
            /* [in]  */ int flags);

        #endregion // shlwapi.dll
    }
}
