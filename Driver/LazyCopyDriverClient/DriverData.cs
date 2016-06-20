// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DriverData.cs">
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
    using System.Globalization;
    using System.Runtime.InteropServices;

    using LazyCopy.DriverClientLibrary;

    #region Enumerations

    /// <summary>
    /// Driver operation mode.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames", Justification = "The current spelling is desired.")]
    [Flags]
    public enum OperationMode
    {
        /// <summary>
        /// Driver is disabled.
        /// </summary>
        None = 0,

        /// <summary>
        /// Fetch is enabled.
        /// </summary>
        FetchEnabled = 1,

        /// <summary>
        /// Watch is enabled.
        /// </summary>
        WatchEnabled = 2
    }

    /// <summary>
    /// Command type to be sent to the driver.
    /// </summary>
    internal enum DriverCommandType
    {
        /// <summary>
        /// Invalid command type.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Driver should return its version.
        /// </summary>
        GetDriverVersion = 1,

        /// <summary>
        /// Read driver configuration from the registry.
        /// </summary>
        ReadRegistryParameters = 100,

        /// <summary>
        /// Sets the driver's operation mode.
        /// </summary>
        SetOperationMode = 101,

        /// <summary>
        /// Set the list of paths to watch.
        /// </summary>
        SetWatchPaths = 102,

        /// <summary>
        /// Sets the driver's report rate.
        /// </summary>
        SetReportRate = 103
    }

    /// <summary>
    /// Notification type driver sends to the user-mode client.
    /// </summary>
    internal enum DriverNotificationType
    {
        /// <summary>
        /// Invalid notification type.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// File should be opened in the user-mode.
        /// </summary>
        OpenFileInUserMode = 1,

        /// <summary>
        /// File handle is no longer needed by the driver.
        /// </summary>
        CloseFileHandle = 2,

        /// <summary>
        /// Driver wants us to download the file.
        /// </summary>
        FetchFileInUserMode = 3
    }

    #endregion // Enumerations

    #region Structures

    /// <summary>
    /// Response for the the <see cref="DriverCommandType.GetDriverVersion"/> command.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    [StructLayout(LayoutKind.Sequential)]
    public struct DriverVersion
    {
        /// <summary>
        /// Major driver version.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        [MarshalAs(UnmanagedType.U2)]
        public short Major;

        /// <summary>
        /// Minor driver version.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        [MarshalAs(UnmanagedType.U2)]
        public short Minor;

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.Major, this.Minor);
        }
    }

    /// <summary>
    /// Contains data for the <see cref="DriverNotificationType.OpenFileInUserMode"/> notification.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    public struct OpenFileInUserModeNotification
    {
        /// <summary>
        /// Path to the file to open.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public string SourceFile;

        /// <summary>
        /// Path to the file the content should be stored to.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public string TargetFile;
    }

    /// <summary>
    /// Reply for the <see cref="DriverNotificationType.OpenFileInUserMode"/> notification.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    [StructLayout(LayoutKind.Sequential)]
    public struct OpenFileInUserModeNotificationReply
    {
        /// <summary>
        /// Opened file handle.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",      Justification = "Declared as public to simplify access and assignment.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public IntPtr Handle;
    }

    /// <summary>
    /// Contains data for the <see cref="DriverNotificationType.CloseFileHandle"/> notification.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable", Justification = "Handle will be closed by notification handler.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    public struct CloseFileHandleNotification
    {
        /// <summary>
        /// Handle to be closed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources", Justification = "Will be closed by the notification handler.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",                   Justification = "Declared as public to simplify access and assignment.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields",              Justification = "Declared as public to simplify access and assignment.")]
        public IntPtr Handle;
    }

    /// <summary>
    /// Contains data for the <see cref="DriverNotificationType.FetchFileInUserMode"/> notification.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    public struct FetchFileInUserModeNotification
    {
        /// <summary>
        /// Path to the file to fetch content from.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public string SourceFile;

        /// <summary>
        /// Path to the file to store content to.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public string TargetFile;
    }

    /// <summary>
    /// Reply for the <see cref="DriverNotificationType.FetchFileInUserMode"/> notification.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes",
        Justification = "This structure is a part of the communication protocol and the equality methods are not needed.")]
    [StructLayout(LayoutKind.Sequential)]
    public struct FetchFileInUserModeNotificationReply
    {
        /// <summary>
        /// Opened file handle.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",      Justification = "Declared as public to simplify access and assignment.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Declared as public to simplify access and assignment.")]
        public long BytesCopied;
    }

    #endregion // Structures

    #region Classes

    /// <summary>
    /// The <see cref="IDriverCommand"/> implementation for the LazyCopy driver.
    /// </summary>
    internal class DriverCommand : IDriverCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DriverCommand"/> class.
        /// </summary>
        /// <param name="type">Command type.</param>
        public DriverCommand(DriverCommandType type)
            : this(type, null)
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DriverCommand"/> class.
        /// </summary>
        /// <param name="type">Command type.</param>
        /// <param name="data">Command data.</param>
        public DriverCommand(DriverCommandType type, byte[] data)
        {
            this.Type = (int)type;
            this.Data = data;
        }

        /// <summary>
        /// Gets the command header.
        /// </summary>
        public int Type { get; }

        /// <summary>
        /// Gets the command data.
        /// </summary>
        public byte[] Data { get; }
    }

    #endregion // Classes
}
