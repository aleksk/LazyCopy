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

namespace LazyCopy.DriverClientLibrary.Native
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Contains P/Invoke method prototypes.
    /// </summary>
    /// <remarks>
    /// List of Win32 error codes: <c>http://msdn.microsoft.com/en-us/library/cc231199(v=prot.20).aspx</c>.
    /// </remarks>
    internal static class NativeMethods
    {
        #region Constants

        /// <summary>
        /// Windows return code for successful operations.
        /// </summary>
        public const uint Ok = 0;

        /// <summary>
        /// The I/O operation has been aborted because of either a thread exit or an application request.
        /// </summary>
        public const uint ErrorOperationAborted = 0x800703E3;

        /// <summary>
        /// Overlapped I/O operation is in progress.
        /// </summary>
        public const uint ErrorIoPending = 0x800703E5;

        /// <summary>
        /// The wait operation timed out.
        /// </summary>
        public const uint WaitTimeout = 0x80070102;

        /// <summary>
        /// Cannot create a file when that file already exists.
        /// </summary>
        public const uint ErrorAlreadyExists = 0x800700B7;

        /// <summary>
        /// The system cannot find the file specified.
        /// </summary>
        public const uint ErrorFileNotFound = 0x80070002;

        /// <summary>
        /// An instance of the service is already running.
        /// </summary>
        public const uint ErrorServiceAlreadyRunning = 0x80070420;

        /// <summary>
        /// Executable is not a valid Win32 application.
        /// </summary>
        public const uint ErrorBadExeFormat = 0x800700C1;

        /// <summary>
        /// The specified driver is invalid.
        /// </summary>
        public const uint ErrorBadDriver = 0x800707D1;

        /// <summary>
        /// The hash for the image cannot be found in the system catalogs.
        /// The image is likely corrupt or the victim of tampering.
        /// </summary>
        public const uint ErrorInvalidImageHash = 0x80070241;

        /// <summary>
        /// An instance already exists at this altitude on the volume specified.
        /// </summary>
        public const uint ErrorFltInstanceAltitudeCollision = 0x801F0011;

        /// <summary>
        /// An instance already exists with this name on the volume specified.
        /// </summary>
        public const uint ErrorFltInstanceNameCollision = 0x801F0012;

        /// <summary>
        /// The system could not find the filter specified.
        /// </summary>
        public const uint ErrorFltFilterNotFound = 0x801F0013;

        /// <summary>
        /// The system could not find the instance specified.
        /// </summary>
        public const uint ErrorFltInstanceNotFound = 0x801F0015;

        /// <summary>
        /// Element not found.
        /// </summary>
        public const uint ErrorNotFound = 0x80070490;

        /// <summary>
        /// No more data is available.
        /// </summary>
        public const uint ErrorNoMoreItems = 0x80070103;

        /// <summary>
        /// The data area passed to a system call is too small.
        /// </summary>
        public const uint ErrorInsufficientBuffer = 0x8007007A;

        #endregion //Constants

        #region Enums

        /// <summary>
        /// Type of filter driver information to be passed to the <see cref="NativeMethods.FilterFindFirst"/>
        /// and <see cref="NativeMethods.FilterFindNext"/> methods.
        /// </summary>
        internal enum FilterInformationClass
        {
            /// <summary>
            /// To return the FILTER_FULL_INFORMATION structure.
            /// </summary>
            FilterFullInformation = 0,

            /// <summary>
            /// To return the FILTER_AGGREGATE_BASIC_INFORMATION structure.
            /// </summary>
            FilterAggregateBasicInformation,

            /// <summary>
            /// To return the FILTER_AGGREGATE_STANDARD_INFORMATION structure.
            /// </summary>
            FilterAggregateStandardInformation
        }

        #endregion // Enums

        #region fltlib.dll

        /// <summary>
        /// Opens a new connection to a communication server port that is created by a file system MiniFilter.
        /// </summary>
        /// <param name="portName">
        /// NULL-terminated wide-character string containing the fully qualified name of the communication server port (for example, <c>"\\MyFilterPort"</c>).
        /// </param>
        /// <param name="options">
        /// Currently unused.
        /// Callers should set this parameter to zero.
        /// </param>
        /// <param name="context">
        /// Pointer to caller-supplied context information to be passed to the kernel-mode MiniFilter's connect notification routine.
        /// This parameter is optional and can be <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="sizeOfContext">
        /// Size, in bytes, of the structure that the <paramref name="context"/> parameter points to.
        /// If the value of <paramref name="context"/> is non-<see cref="IntPtr.Zero"/>, this parameter must be nonzero.
        /// If <paramref name="context"/> is <see cref="IntPtr.Zero"/>, this parameter must be zero.
        /// </param>
        /// <param name="securityAttributes">
        /// Pointer to a <c>SECURITY_ATTRIBUTES</c> structure that determines whether the returned handle can be inherited by child processes.
        /// This parameter is optional and can be <see cref="IntPtr.Zero"/>. If this parameter is <see cref="IntPtr.Zero"/>, the handle cannot be inherited.
        /// </param>
        /// <param name="portHandle">
        /// Pointer to a caller-allocated variable that receives a handle for the newly created connection port if the call to 
        /// <c>FilterConnectCommunicationPort</c> succeeds; otherwise, it receives <c>INVALID_HANDLE_VALUE</c>.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterConnectCommunicationPort(
            /* [in]  */ [MarshalAs(UnmanagedType.LPWStr)] string portName,
            /* [in]  */ uint options,
            /* [in]  */ IntPtr context,
            /* [in]  */ short sizeOfContext,
            /* [in]  */ IntPtr securityAttributes,
            /* [out] */ out SafeFileHandle portHandle);

        /// <summary>
        /// Gets a message from a kernel-mode MiniFilter.
        /// </summary>
        /// <param name="portHandle">
        /// Communication port handle returned by a previous call to <see cref="FilterConnectCommunicationPort"/>.
        /// This parameter is required and cannot be <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="messageBuffer">
        /// Pointer to a caller-allocated buffer that receives the message from the MiniFilter.
        /// </param>
        /// <param name="messageBufferSize">
        /// Size, in bytes, of the buffer that the <paramref name="messageBuffer"/> parameter points to.
        /// </param>
        /// <param name="overlapped">
        /// Pointer to an <see cref="NativeOverlapped"/> structure.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterGetMessage(
            /* [in]  */ SafeFileHandle portHandle,
            /* [out] */ IntPtr messageBuffer,
            /* [in]  */ int messageBufferSize,
            /* [out] */ ref NativeOverlapped overlapped);

        /// <summary>
        /// Sends a message to a kernel-mode MiniFilter.
        /// </summary>
        /// <param name="portHandle">
        /// Communication port handle returned by a previous call to <see cref="FilterConnectCommunicationPort"/>.
        /// This parameter is required and cannot be <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="inBuffer">
        /// Pointer to a caller-allocated buffer containing the message to be sent to the MiniFilter.
        /// The message format is caller-defined.
        /// This parameter is required and cannot be <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="inBufferSize">
        /// Size, in bytes, of the buffer pointed to by <paramref name="inBuffer"/>.
        /// </param>
        /// <param name="outBuffer">
        /// Pointer to a caller-allocated buffer that receives the reply (if any) from the MiniFilter.
        /// </param>
        /// <param name="outBufferSize">
        /// Size, in bytes, of the buffer pointed to by <paramref name="outBuffer"/>.
        /// This value is ignored if <paramref name="outBuffer"/> is <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="bytesReturned">
        /// Variable that receives the number of bytes returned in the buffer that
        /// <paramref name="outBuffer"/> points to if the call to <c>FilterSendMessage</c> succeeds.
        /// This parameter is required.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterSendMessage(
            /* [in]  */ SafeFileHandle portHandle,
            /* [in]  */ IntPtr inBuffer,
            /* [in]  */ uint inBufferSize,
            /* [out] */ IntPtr outBuffer,
            /* [in]  */ uint outBufferSize,
            /* [out] */ out uint bytesReturned);

        /// <summary>
        /// Replies to a message from a kernel-mode MiniFilter.
        /// </summary>
        /// <param name="portHandle">
        /// Communication port handle returned by a previous call to <see cref="FilterConnectCommunicationPort"/>.
        /// This parameter is required and cannot be <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="replyBuffer">
        /// A pointer to a caller-allocated buffer containing the reply to be sent to the MiniFilter.
        /// </param>
        /// <param name="replyBufferSize">
        /// Size, in bytes, of the buffer that the <paramref name="replyBuffer"/> parameter points to.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterReplyMessage(
            /* [in] */ SafeFileHandle portHandle,
            /* [in] */ IntPtr replyBuffer,
            /* [in] */ uint replyBufferSize);

        /// <summary>
        /// Dynamically loads a MiniFilter driver into the system.
        /// </summary>
        /// <param name="filterName">
        /// Pointer to a null-terminated wide-character string that specifies the service name of the MiniFilter driver.
        /// This parameter is required and cannot be <see langword="null"/> or an empty string.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful.<br/>
        /// Otherwise, it returns one of the following error values:<br/>
        /// <list type="table">
        /// <listheader>
        ///     <term>Return code</term>
        ///     <description>Description</description>
        /// </listheader>
        /// <item>
        ///     <term>ERROR_ALREADY_EXISTS</term>
        ///     <description>The MiniFilter driver is already running.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_FILE_NOT_FOUND</term>
        ///     <description>No matching MiniFilter driver was found.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_SERVICE_ALREADY_RUNNING</term>
        ///     <description>The MiniFilter driver is already running.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_BAD_EXE_FORMAT</term>
        ///     <description>The load image for the MiniFilter driver specified by <paramref name="filterName"/> is invalid.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_BAD_DRIVER</term>
        ///     <description>The load image for the MiniFilter driver specified by <paramref name="filterName"/> is invalid.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_INVALID_IMAGE_HASH</term>
        ///     <description>The MiniFilter driver has an invalid digital signature.</description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Callers of FilterLoad must have <c>SeLoadDriverPrivilege</c> (the LUID of <c>SE_LOAD_DRIVER_PRIVILEGE</c>) to load
        /// or unload a MiniFilter driver.
        /// </remarks>
        [DllImport("fltlib.dll")]
        public static extern uint FilterLoad(
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string filterName);

        /// <summary>
        /// An application that has loaded a supporting MiniFilter by calling <see cref="FilterLoad"/> can unload
        /// the MiniFilter by calling the <see cref="FilterUnload"/> function.
        /// </summary>
        /// <param name="filterName">
        /// Pointer to a null-terminated wide-character string containing the same MiniFilter name that was passed
        /// to <see cref="FilterLoad"/>.
        /// This parameter is required and cannot be <see langword="null"/> or an empty string.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        /// <remarks>
        /// Callers of <see cref="FilterUnload"/> must have <c>SeLoadDriverPrivilege</c> (the LUID of <c>SE_LOAD_DRIVER_PRIVILEGE</c>)
        /// to load or unload a MiniFilter driver.
        /// </remarks>
        [DllImport("fltlib.dll")]
        public static extern uint FilterUnload(
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string filterName);

        /// <summary>
        /// Attaches a new MiniFilter instance to the given volume.
        /// </summary>
        /// <param name="filterName">
        /// Pointer to a null-terminated wide-character string containing the name of the MiniFilter for which
        /// an instance is to be created.
        /// This parameter is required and cannot be <see langword="null"/>.
        /// </param>
        /// <param name="volumeName">
        /// Pointer to a null-terminated wide-character string containing the name of the volume
        /// to which the newly created instance is to be attached.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// This parameter is required and cannot be <see langword="null"/>.
        /// </param>
        /// <param name="instanceName">
        /// Pointer to a null-terminated wide-character string containing the instance name for the new instance.
        /// This parameter is optional and can be <see langword="null"/>.
        /// </param>
        /// <param name="createdInstanceNameLength">
        /// Length, in bytes, of the <paramref name="createdInstanceName"/> buffer.
        /// This parameter is optional and can be zero.
        /// </param>
        /// <param name="createdInstanceName">
        /// Pointer to a caller-allocated variable that receives the instance name for the new instance
        /// if the instance is successfully attached to the volume.
        /// This parameter is optional and can be <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful.<br/>
        /// Otherwise, it returns one of the following error values:<br/>
        /// <list type="table">
        /// <listheader>
        ///     <term>Return code</term>
        ///     <description>Description</description>
        /// </listheader>
        /// <item>
        ///     <term>ERROR_FLT_INSTANCE_ALTITUDE_COLLISION</term>
        ///     <description>An instance already exists at this altitude on the volume specified.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_FLT_INSTANCE_NAME_COLLISION</term>
        ///     <description>An instance already exists with this name on the volume specified.</description>
        /// </item>
        /// <item>
        ///     <term>ERROR_FILE_NOT_FOUND</term>
        ///     <description>If <paramref name="instanceName"/> is non-NULL, <paramref name="instanceName"/> does not match a registered filter instance name in the registry.</description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The instance name specified in <paramref name="instanceName"/> is required to be unique across the system.
        /// </remarks>
        [DllImport("fltlib.dll")]
        public static extern uint FilterAttach(
            /* [in]  */ [MarshalAs(UnmanagedType.LPWStr)] string filterName,
            /* [in]  */ [MarshalAs(UnmanagedType.LPWStr)] string volumeName,
            /* [in]  */ [MarshalAs(UnmanagedType.LPWStr)] string instanceName,
            /* [in]  */ uint createdInstanceNameLength,
            /* [out] */ [MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 3)] StringBuilder createdInstanceName);

        /// <summary>
        /// Detaches the given MiniFilter instance from the given volume.
        /// </summary>
        /// <param name="filterName">
        /// Pointer to a null-terminated wide-character string containing the name of the MiniFilter whose
        /// instance is to be detached from the stack.
        /// This parameter is required and cannot be <see langword="null"/>.
        /// </param>
        /// <param name="volumeName">
        /// Pointer to a null-terminated wide-character string containing the name of the volume
        /// to which the newly created instance is to be attached.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// This parameter is required and cannot be <see langword="null"/>.
        /// </param>
        /// <param name="instanceName">
        /// Pointer to a null-terminated wide-character string containing the instance name for
        /// the instance to be removed.
        /// This parameter is optional and can be <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        /// <remarks>
        /// <see cref="FilterDetach"/> detaches a MiniFilter instance from a volume and tears down the instance.
        /// </remarks>
        [DllImport("fltlib.dll")]
        public static extern uint FilterDetach(
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string filterName,
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string volumeName,
            /* [in] */ [MarshalAs(UnmanagedType.LPWStr)] string instanceName);

        /// <summary>
        /// Returns information about a filter driver (MiniFilter driver instance or legacy filter driver)
        /// and is used to begin scanning the filters in the global list of registered filters.
        /// </summary>
        /// <param name="informationClass">
        /// Type of filter driver information requested.
        /// </param>
        /// <param name="buffer">
        /// Pointer to a caller-allocated buffer that receives the requested information.
        /// The type of the information returned in the buffer is defined by the <paramref name="informationClass"/> parameter.
        /// </param>
        /// <param name="bufferSize">
        /// Size, in bytes, of the buffer that the <paramref name="buffer"/> parameter points to.
        /// The caller should set this parameter according to the given <paramref name="informationClass"/>.
        /// </param>
        /// <param name="bytesReturned">
        /// Pointer to a caller-allocated variable that receives the number of bytes returned in the
        /// buffer that <paramref name="buffer"/> points to if the call succeeds.
        /// </param>
        /// <param name="filterFind">
        /// Pointer to a caller-allocated variable that receives a search handle for the filter driver
        /// if the call to this method succeeds; otherwise, it receives <c>INVALID_HANDLE_VALUE</c>.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterFindFirst(
            /* [in]  */ [MarshalAs(UnmanagedType.I4)] FilterInformationClass informationClass,
            /* [in]  */ IntPtr buffer,
            /* [in]  */ uint bufferSize,
            /* [out] */ out uint bytesReturned,
            /* [out] */ out IntPtr filterFind);

        /// <summary>
        /// Continues a filter search started by a call to <see cref="FilterFindFirst"/>.
        /// </summary>
        /// <param name="filterFind">
        /// Filter search handle returned by a previous call to <see cref="FilterFindFirst"/>..
        /// </param>
        /// <param name="informationClass">
        /// Type of filter driver information requested.
        /// </param>
        /// <param name="buffer">
        /// Pointer to a caller-allocated buffer that receives the requested information.
        /// The type of the information returned in the buffer is defined by the <paramref name="informationClass"/> parameter.
        /// </param>
        /// <param name="bufferSize">
        /// Size, in bytes, of the buffer that the <paramref name="buffer"/> parameter points to.
        /// The caller should set this parameter according to the given <paramref name="informationClass"/>.
        /// </param>
        /// <param name="bytesReturned">
        /// Pointer to a caller-allocated variable that receives the number of bytes returned in the
        /// buffer that <paramref name="buffer"/> points to if the call succeeds.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterFindNext(
            /* [in]  */ IntPtr filterFind,
            /* [in]  */ [MarshalAs(UnmanagedType.I4)] FilterInformationClass informationClass,
            /* [in]  */ IntPtr buffer,
            /* [in]  */ uint bufferSize,
            /* [out] */ out uint bytesReturned);

        /// <summary>
        /// Closes the specified MiniFilter search handle.
        /// </summary>
        /// <param name="filterFind">
        /// MiniFilter search handle to close.
        /// </param>
        /// <returns>
        /// <see cref="Ok"/> if successful. Otherwise, it returns an error value.
        /// </returns>
        [DllImport("fltlib.dll")]
        public static extern uint FilterFindClose(
            /* [in] */ IntPtr filterFind);

        #endregion // fltlib.dll

        #region kernel32.dll

        /// <summary>
        /// Creates an input/output (I/O) completion port and associates it with a specified file handle,
        /// or creates an I/O completion port that is not yet associated with a file handle,
        /// allowing association at a later time.
        /// </summary>
        /// <param name="fileHandle">
        /// An open file handle or <c>INVALID_HANDLE_VALUE</c>.
        /// The handle must be to an object that supports overlapped I/O.
        /// </param>
        /// <param name="existingCompletionPort">
        /// A handle to an existing I/O completion port or <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <param name="completionKey">
        /// The per-handle user-defined completion key that is included in every I/O completion packet for the specified file handle.
        /// </param>
        /// <param name="numberOfConcurrentThreads">
        /// The maximum number of threads that the operating system can allow to concurrently process I/O completion packets for the I/O completion port.
        /// This parameter is ignored if the <paramref name="existingCompletionPort"/> parameter is not <see cref="IntPtr.Zero"/>.
        /// If this parameter is zero, the system allows as many concurrently running threads as there are processors in the system.
        /// </param>
        /// <returns>
        /// If the function succeeds, the return value is the handle to an I/O completion port.
        /// </returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateIoCompletionPort(
            /* [in] */ SafeFileHandle fileHandle,
            /* [in] */ IntPtr existingCompletionPort,
            /* [in] */ IntPtr completionKey,
            /* [in] */ uint numberOfConcurrentThreads);

        /// <summary>
        /// Attempts to dequeue an I/O completion packet from the specified I/O completion port.
        /// If there is no completion packet queued, the function waits for a pending I/O operation
        /// associated with the completion port to complete.
        /// </summary>
        /// <param name="completionPort">
        /// A handle to the completion port.
        /// </param>
        /// <param name="numberOfBytes">
        /// A pointer to a variable that receives the number of bytes transferred during
        /// an I/O operation that has completed.
        /// </param>
        /// <param name="completionKey">
        /// A pointer to a variable that receives the completion key value associated with
        /// the file handle whose I/O operation has completed.
        /// </param>
        /// <param name="overlapped">
        /// A pointer to a variable that receives the address of the <see cref="NativeOverlapped"/> structure
        /// that was specified when the completed I/O operation was started.
        /// </param>
        /// <param name="milliseconds">
        /// The number of milliseconds that the caller is willing to wait for a completion packet
        /// to appear at the completion port.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if successful or <see langword="false"/> otherwise.
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "'Queued' spelling is correct.")]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetQueuedCompletionStatus(
            /* [in]  */ IntPtr completionPort,
            /* [out] */ out uint numberOfBytes,
            /* [out] */ out IntPtr completionKey,
            /* [out] */ out NativeOverlapped overlapped,
            /* [in]  */ int milliseconds);

        /// <summary>
        /// Marks any outstanding I/O operations for the specified file handle.
        /// The function only cancels I/O operations in the current process, regardless of which thread created the I/O operation.
        /// </summary>
        /// <param name="portHandle">
        /// A handle to the port.
        /// </param>
        /// <param name="overlapped">
        /// A pointer to an OVERLAPPED data structure that contains the data used for asynchronous I/O.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if successful or <see langword="false"/> otherwise.
        /// </returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CancelIoEx(
            /* [in] */ SafeFileHandle portHandle,
            /* [in] */ ref NativeOverlapped overlapped);

        #endregion // kernel32.dll
    }
}
