// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyDriverClient.cs">
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
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    using LazyCopy.DriverClientLibrary;
    using LazyCopy.Utilities;

    /// <summary>
    /// Client for the LazyCopy kernel-mode driver.
    /// </summary>
    public class LazyCopyDriverClient : DriverClientBase
    {
        #region Fields

        /// <summary>
        /// Maximum value for the driver's 'ReportRate' variable.
        /// </summary>
        public const int MaxReportRate = 10000;

        /// <summary>
        /// Default communication port name.
        /// </summary>
        public const string DefaultPortName = "\\LazyCopyDriverPort";

        /// <summary>
        /// Default notification size value.
        /// </summary>
        private const int DefaultNotificationSize = 4 * 1024;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyDriverClient"/> class.
        /// </summary>
        public LazyCopyDriverClient()
            : this(LazyCopyDriverClient.DefaultPortName)
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyDriverClient"/> class.
        /// </summary>
        /// <param name="portName">Name of the port.</param>
        public LazyCopyDriverClient(string portName)
            : base(portName, 1, LazyCopyDriverClient.DefaultNotificationSize)
        {
            // Do nothing.
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets or sets the <c>OpenFileInUserMode</c> notifications handler.
        /// </summary>
        public Func<OpenFileInUserModeNotification, OpenFileInUserModeNotificationReply> OpenFileInUserModeHandler { get; set; }

        /// <summary>
        /// Gets or sets the <c>CloseFileHandle</c> notification handler.
        /// </summary>
        public Action<CloseFileHandleNotification> CloseFileHandleHandler { get; set; }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Gets the driver version this client is currently connected to.
        /// </summary>
        /// <returns>Driver version structure.</returns>
        /// <exception cref="InvalidOperationException">Client is not connected to the driver.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Should be a method, because it retrieves data from a driver.")]
        public DriverVersion GetDriverVersion()
        {
            return this.ExecuteCommand<DriverVersion>(new DriverCommand(DriverCommandType.GetDriverVersion));
        }

        /// <summary>
        /// Tells driver to re-read the configuration parameters from the Registry.
        /// </summary>
        /// <exception cref="InvalidOperationException">Client is not connected to the driver.</exception>
        public void ReadConfigurationFromRegistry()
        {
            this.ExecuteCommand(new DriverCommand(DriverCommandType.ReadRegistryParameters));
        }

        /// <summary>
        /// Sets the list of paths the driver should watch.
        /// </summary>
        /// <param name="paths">List of paths to watch and their report rates. Should be in DOS name format, for example: <c>\Device\HarddiskVolume1\Folder\</c></param>
        /// <exception cref="InvalidOperationException">Client is not connected to the driver.</exception>
        public void SetWatchPaths(string[] paths)
        {
            byte[] data = LazyCopyDriverClient.GetWatchPathsData(paths);
            this.ExecuteCommand(new DriverCommand(DriverCommandType.SetWatchPaths, data));
        }

        /// <summary>
        /// Clears the list of paths to watch.
        /// </summary>
        /// <exception cref="InvalidOperationException">Client is not connected to the driver.</exception>
        public void ClearWatchPaths()
        {
            this.ExecuteCommand(new DriverCommand(DriverCommandType.SetWatchPaths, LazyCopyDriverClient.GetWatchPathsData(null)));
        }

        /// <summary>
        /// Changes the driver's operation mode.
        /// </summary>
        /// <param name="mode">New value to be set.</param>
        /// <exception cref="InvalidOperationException">Client is not connected to the driver.</exception>
        public void SetOperationStatus(OperationMode mode)
        {
            this.ExecuteCommand(new DriverCommand(DriverCommandType.SetOperationMode, BitConverter.GetBytes((int)mode)));
        }

        /// <summary>
        /// Sets the new report rate value.
        /// </summary>
        /// <param name="reportRate">The report rate.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="reportRate"/> is invalid.</exception>
        public void SetReportRate(int reportRate)
        {
            if (reportRate < 0 || reportRate > LazyCopyDriverClient.MaxReportRate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reportRate),
                    reportRate,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Report rate should be within {0} - {1} range",
                        0,
                        LazyCopyDriverClient.MaxReportRate));
            }

            this.ExecuteCommand(new DriverCommand(DriverCommandType.SetReportRate, BitConverter.GetBytes(reportRate)));
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Handles notifications received from the driver.
        /// </summary>
        /// <param name="driverNotification">Driver notification.</param>
        /// <returns>Reply to be sent back to the driver.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="driverNotification"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">No handler is found for the <paramref name="driverNotification"/> given.</exception>
        protected override object NotificationsHandler(IDriverNotification driverNotification)
        {
            if (driverNotification == null)
            {
                throw new ArgumentNullException(nameof(driverNotification));
            }

            switch (driverNotification.Type)
            {
                case (int)DriverNotificationType.OpenFileInUserMode:
                    Func<OpenFileInUserModeNotification, OpenFileInUserModeNotificationReply> fetchHandler = this.OpenFileInUserModeHandler;
                    if (fetchHandler != null)
                    {
                        OpenFileInUserModeNotification notification = new OpenFileInUserModeNotification { FilePath = Marshal.PtrToStringUni(driverNotification.Data) };
                        return fetchHandler(notification);
                    }

                    break;

                case (int)DriverNotificationType.CloseFileHandle:
                    Action<CloseFileHandleNotification> handler = this.CloseFileHandleHandler;
                    if (handler != null)
                    {
                        CloseFileHandleNotification notification = new CloseFileHandleNotification { Handle = Marshal.ReadIntPtr(driverNotification.Data) };
                        handler(notification);

                        return null;
                    }

                    break;
            }

            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No handler found for the notification of type {0}", driverNotification.Type));
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Converts the <paramref name="paths"/> list into a byte array containing the amount of paths in the
        /// <paramref name="paths"/> as a first <c>int</c>, and the unicode-converted list of paths after it.
        /// </summary>
        /// <param name="paths">List of paths to convert.</param>
        /// <returns>
        /// Byte array suitable to be used as a <c>FETCH_PATHS</c> data.
        /// </returns>
        private static byte[] GetWatchPathsData(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                paths = new string[0];
            }

            // This will replace the Windows path roots with the device names.
            string[] pathsArray = paths.ToArray();

            // See the 'FETCH_PATHS' structure for more details.
            List<byte> data = new List<byte>(BitConverter.GetBytes(pathsArray.Length));

            // Convert strings to byte array.
            foreach (string path in pathsArray)
            {
                data.AddRange(LazyCopyDriverClient.ConvertToDevicePath(path));
            }

            return data.ToArray();
        }

        /// <summary>
        /// Replaces the <paramref name="path"/> root with the according device name and converts it to the
        /// Unicode byte array.
        /// </summary>
        /// <param name="path">Path to convert.</param>
        /// <returns>Byte array containing the converted path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
        private static byte[] ConvertToDevicePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            string formattedPath = PathHelper.EndWithDirectorySeparator(PathHelper.ChangeDriveLetterToDeviceName(path));

            // Make sure the path is null-terminated.
            return Encoding.Unicode.GetBytes(formattedPath + '\0');
        }

        #endregion // Private methods
    }
}
