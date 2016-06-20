// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyDriver.cs">
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

namespace LazyCopy.Service
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Threading;

    using LazyCopy.DriverClient;
    using LazyCopy.DriverClientLibrary;
    using LazyCopy.Service.Properties;
    using LazyCopy.Utilities;
    using NLog;

    /// <summary>
    /// This class is a wrapper for the <see cref="LazyCopyDriverClient"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "This is a singleton class and IDisposable is not needed here.")]
    public sealed class LazyCopyDriver
    {
        #region Fields

        /// <summary>
        /// Logger instance.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Lazily created class instance.
        /// </summary>
        private static readonly Lazy<LazyCopyDriver> LazyInstance = new Lazy<LazyCopyDriver>(() => new LazyCopyDriver());

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// MiniFilter driver client.
        /// </summary>
        private readonly LazyCopyDriverClient driverClient;

        /// <summary>
        /// Current impersonation context.
        /// </summary>
        private readonly ThreadLocal<WindowsImpersonationContext> impersonationContext = new ThreadLocal<WindowsImpersonationContext>();

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Prevents a default instance of the <see cref="LazyCopyDriver"/> class from being created.
        /// </summary>
        private LazyCopyDriver()
        {
            // First, load the driver.
            FltmcManager.Instance.LoadFilter(Settings.Default.DriverName);

            // And connect to it.
            this.driverClient = new LazyCopyDriverClient();
            this.driverClient.OpenFileInUserModeHandler  += this.OpenFileInUserModeHandler;
            this.driverClient.CloseFileHandleHandler     += this.CloseFileHandleHandler;
            this.driverClient.FetchFileInUserModeHandler += this.FetchFileInUserModeHandler;
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets the singleton class instance.
        /// </summary>
        public static LazyCopyDriver Instance => LazyCopyDriver.LazyInstance.Value;

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Updates the driver configuration using the current settings.
        /// </summary>
        public void ConfigureDriver()
        {
            lock (this.syncRoot)
            {
                LazyCopyDriver.Logger.Debug("Configuring driver...");
                DriverConfiguration configuration = LazyCopyDriver.GetConfiguration();

                LazyCopyDriver.Logger.Debug("Updating driver settings...");

                this.driverClient.SetWatchPaths(configuration.WatchPaths.ToArray());
                this.driverClient.SetReportRate(configuration.ReportRate);
                this.driverClient.SetOperationStatus(configuration.OperationMode);

                LazyCopyDriver.Logger.Debug("Finished configuring driver.");
            }
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Creates driver configuration based on the current settings.
        /// </summary>
        /// <returns>Driver configuration.</returns>
        private static DriverConfiguration GetConfiguration()
        {
            DriverConfiguration configuration = new DriverConfiguration
            {
                ReportRate    = Settings.Default.ReportRate,
                OperationMode = OperationMode.FetchEnabled
            };

            return configuration;
        }

        /// <summary>
        /// Opens the file given.
        /// </summary>
        /// <param name="notification">Driver notification.</param>
        /// <returns>Structure containing the reply data.</returns>
        private OpenFileInUserModeNotificationReply OpenFileInUserModeHandler(OpenFileInUserModeNotification notification)
        {
            this.ImpersonateCurrentThread();

            //
            // NOTE: You may want to open a different source file depending on where the local file is located.
            // string targetFile = notification.TargetFile;
            //

            string sourceFile = PathHelper.ChangeDeviceNameToDriveLetter(notification.SourceFile);

            IntPtr handle = Native.NativeMethods.CreateFile(sourceFile, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            return new OpenFileInUserModeNotificationReply { Handle = handle };
        }

        /// <summary>
        /// Closes the file handle given.
        /// </summary>
        /// <param name="notification">Driver notification.</param>
        private void CloseFileHandleHandler(CloseFileHandleNotification notification)
        {
            if (notification.Handle != IntPtr.Zero)
            {
                Native.NativeMethods.CloseHandle(notification.Handle);
            }
        }

        /// <summary>
        /// Downloads the remote file given.
        /// </summary>
        /// <param name="notification">Driver notification.</param>
        private FetchFileInUserModeNotificationReply FetchFileInUserModeHandler(FetchFileInUserModeNotification notification)
        {
            this.ImpersonateCurrentThread();

            string sourceFile = notification.SourceFile;
            string targetFile = PathHelper.ChangeDeviceNameToDriveLetter(notification.TargetFile);

            if (sourceFile.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourceFile.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(sourceFile, targetFile);
                }
            }

            return new FetchFileInUserModeNotificationReply { BytesCopied = 404 };
        }

        /// <summary>
        /// Used the currently logged in user for thread impersonation, if it's not yet impersonated.
        /// </summary>
        private void ImpersonateCurrentThread()
        {
            if (!this.impersonationContext.IsValueCreated)
            {
                this.impersonationContext.Value = UserHelper.ImpersonateCurrentUser();
            }
        }

        #endregion // Private methods
    }
}
