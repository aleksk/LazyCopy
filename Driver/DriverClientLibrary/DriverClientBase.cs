// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DriverClientBase.cs">
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

namespace LazyCopy.DriverClientLibrary
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using LazyCopy.DriverClientLibrary.Native;
    using LazyCopy.Utilities;
    using Microsoft.Win32.SafeHandles;
    using NLog;

    /// <summary>
    /// This abstract class contains methods for communication with the filter driver.
    /// </summary>
    public abstract class DriverClientBase : IDisposable
    {
        #region Fields

        /// <summary>
        /// Logger instance.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The default amount of concurrent connections to the driver's communication port.
        /// </summary>
        private static readonly Lazy<int> DefaultThreadCount = new Lazy<int>(() =>
        {
            int coreCount;
            if (!int.TryParse(Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out coreCount))
            {
                coreCount = 0;
            }

            return Math.Min(TaskScheduler.Current.MaximumConcurrencyLevel, coreCount > 0 ? coreCount : Environment.ProcessorCount);
        });

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The communication port name for this client.
        /// </summary>
        private readonly string portName;

        /// <summary>
        /// Current amount of concurrent connections supported by the communication port.
        /// </summary>
        private readonly int threadCount;

        /// <summary>
        /// Maximum notification size expected.
        /// </summary>
        private readonly int maxNotificationSize;

        /// <summary>
        /// List of wait handles to be used to wait until the notification monitor tasks finish.
        /// </summary>
        private readonly List<WaitHandle> tasksCompletionWaitHandles = new List<WaitHandle>();

        /// <summary>
        /// Cancellation token source.
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// MiniFilter driver communication port handle.
        /// </summary>
        private SafeFileHandle filterPortHandle;

        /// <summary>
        /// MiniFilter driver I/O completion port handle.
        /// </summary>
        private SafeFileHandle completionPortHandle;

        /// <summary>
        /// Current connection state.
        /// </summary>
        private volatile ConnectionState state;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DriverClientBase"/> class.
        /// </summary>
        /// <param name="portName">Driver communication port name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="portName"/> is <see langword="null"/> or empty.</exception>
        /// <remarks>
        /// When instantiated, the <see cref="Connect"/> method is called.
        /// </remarks>
        protected DriverClientBase(string portName)
            : this(portName, DriverClientBase.DefaultThreadCount.Value, Environment.SystemPageSize)
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DriverClientBase"/> class.
        /// </summary>
        /// <param name="portName">Driver communication port name.</param>
        /// <param name="threadCount">Amount of background threads to be created.</param>
        /// <param name="maxNotificationSize">Maximum notification size.</param>
        /// <exception cref="ArgumentNullException"><paramref name="portName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="threadCount"/> or <paramref name="maxNotificationSize"/> is lesser than zero.</exception>
        protected DriverClientBase(string portName, int threadCount, int maxNotificationSize)
        {
            if (string.IsNullOrEmpty(portName))
            {
                throw new ArgumentNullException(nameof(portName));
            }

            if (threadCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(threadCount), threadCount, "Thread count should be more or equal to zero.");
            }

            if (maxNotificationSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNotificationSize), maxNotificationSize, "Notification size should be more than zero.");
            }

            this.portName            = portName;
            this.threadCount         = threadCount;
            this.maxNotificationSize = maxNotificationSize;

            this.state = ConnectionState.Created;

            this.Connect();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="DriverClientBase"/> class.
        /// </summary>
        ~DriverClientBase()
        {
            this.Dispose(false);
        }

        #endregion // Constructors

        #region Properties

        /// <summary>
        /// Gets the current connection state.
        /// </summary>
        protected ConnectionState State
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.state;
                }
            }
        }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Connects to the driver's communication port.
        /// </summary>
        /// <exception cref="InvalidOperationException">Client was unable to connect to the driver or the client is already connected.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "General exception catch block is required here.")]
        public void Connect()
        {
            lock (this.syncRoot)
            {
                // Should not be connected.
                if (this.state == ConnectionState.Connected)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Client is already connected to the port '{0}'", this.portName));
                }

                if (this.state == ConnectionState.Faulted)
                {
                    throw new InvalidOperationException("Client encountered an unrecoverable error and cannot connect to the port.");
                }

                try
                {
                    this.state = ConnectionState.Connecting;
                    DriverClientBase.Logger.Debug(CultureInfo.InvariantCulture, "Connecting to port: {0}", this.portName);

                    // Open communication ports.
                    uint hr = NativeMethods.FilterConnectCommunicationPort(this.portName, 0, IntPtr.Zero, 0, IntPtr.Zero, out this.filterPortHandle);
                    if (hr != NativeMethods.Ok || this.filterPortHandle == null || this.filterPortHandle.IsInvalid)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to connect to driver via the '{0}' port: 0x{1:X8}", this.portName, hr);
                        Exception innerException = Marshal.GetExceptionForHR(unchecked((int)hr));

                        DriverClientBase.Logger.Error(innerException, message);
                        throw new InvalidOperationException(message, innerException);
                    }

                    this.completionPortHandle = NativeMethods.CreateIoCompletionPort(this.filterPortHandle, IntPtr.Zero, IntPtr.Zero, (uint)this.threadCount);
                    if (this.completionPortHandle == null || this.completionPortHandle.IsInvalid)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to create I/O completion port: 0x{0:X8}", Marshal.GetHRForLastWin32Error());
                        DriverClientBase.Logger.Error(message);

                        throw new InvalidOperationException(message);
                    }

                    // Start monitoring threads, so we will start getting notifications from the driver.
                    this.StartMonitoringThreads();

                    this.state = ConnectionState.Connected;
                    DriverClientBase.Logger.Info(CultureInfo.InvariantCulture, "Client is connected to port: {0}", this.portName);
                }
                catch
                {
                    this.state = ConnectionState.Faulted;
                }
            }
        }

        /// <summary>
        /// Disconnects from the MiniFilter driver.
        /// </summary>
        /// <exception cref="InvalidOperationException">Client is not connected to a driver.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "General exception catch block is required here.")]
        public void Disconnect()
        {
            lock (this.syncRoot)
            {
                // Should be connected or faulted.
                if (this.state != ConnectionState.Connected)
                {
                    throw new InvalidOperationException("Client is not connected.");
                }

                try
                {
                    this.state = ConnectionState.Closing;

                    this.StopMonitoringThreads();

                    if (this.completionPortHandle != null && !this.completionPortHandle.IsInvalid)
                    {
                        this.completionPortHandle.Dispose();
                        this.completionPortHandle = null;
                    }

                    if (this.filterPortHandle != null && !this.filterPortHandle.IsInvalid)
                    {
                        this.filterPortHandle.Dispose();
                        this.filterPortHandle = null;
                    }

                    this.state = ConnectionState.Closed;
                    DriverClientBase.Logger.Info(CultureInfo.InvariantCulture, "Disconnected from port: {0}", this.portName);
                }
                catch
                {
                    this.state = ConnectionState.Faulted;
                }
            }
        }

        /// <summary>
        /// Sends the command to the driver.
        /// </summary>
        /// <param name="command">Command to be sent to the driver.</param>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Memory for the command could not be allocated.
        ///     <para>-or-</para>
        /// Memory for the response could not be allocated.
        ///     <para>-or-</para>
        /// Message was not sent to the driver.
        /// </exception>
        /// <remarks>
        /// This method should be called from a synchronized context.
        /// </remarks>
        public void ExecuteCommand(IDriverCommand command)
        {
            this.ExecuteCommand(command, null);
        }

        /// <summary>
        /// Sends the command to the driver and gets the response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response.</typeparam>
        /// <param name="command">Command to be sent to the driver.</param>
        /// <returns>Response received from the driver.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><typeparamref name="TResponse"/> type is not a structure type.</exception>
        /// <exception cref="InvalidOperationException">
        /// Memory for the command could not be allocated.
        ///     <para>-or-</para>
        /// Memory for the response could not be allocated.
        ///     <para>-or-</para>
        /// Message was not sent to the driver.
        /// </exception>
        /// <remarks>
        /// This method should be called from a synchronized context.
        /// </remarks>
        public TResponse ExecuteCommand<TResponse>(IDriverCommand command)
            where TResponse : struct
        {
            return (TResponse)this.ExecuteCommand(command, typeof(TResponse));
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (this.syncRoot)
            {
                if (this.state == ConnectionState.Connected)
                {
                    this.Disconnect();
                }

                if (disposing)
                {
                    if (this.cancellationTokenSource != null)
                    {
                        this.cancellationTokenSource.Dispose();
                        this.cancellationTokenSource = null;
                    }
                }
            }
        }

        /// <summary>
        /// Handles notifications received from the driver.
        /// </summary>
        /// <param name="driverNotification">Driver notification.</param>
        /// <returns>Reply to be sent back to the driver. Must be marshallable. Can be <see langword="null"/>, if an empty response should be sent.</returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "The spelling is correct.")]
        protected abstract object NotificationsHandler(IDriverNotification driverNotification);

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Starts listening for the notifications from the driver.
        /// </summary>
        /// <remarks>
        /// This method is invoked from the <see cref="Connect"/> and will be executed in the synchronization context, so no lock is needed.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle",
            Justification = "Handle won't be garbage collected.")]
        private void StartMonitoringThreads()
        {
            this.cancellationTokenSource = new CancellationTokenSource();

            for (int i = 0; i < this.threadCount; i++)
            {
                using (ManualResetEvent taskStartedEvent = new ManualResetEvent(false))
                {
                    ManualResetEvent taskFinishedEvent = new ManualResetEvent(false);
                    this.tasksCompletionWaitHandles.Add(taskFinishedEvent);

                    // Create new long-running background task.
                    NotificationsMonitor monitor = new NotificationsMonitor(
                        this.cancellationTokenSource.Token,
                        this.maxNotificationSize,
                        this.NotificationsHandler,
                        this.filterPortHandle.DangerousGetHandle(),
                        this.completionPortHandle.DangerousGetHandle());

                    Task.Factory.StartNew(
                        () =>
                        {
                            taskStartedEvent.Set();
                            monitor.DoWork();
                        },
                        this.cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Current)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            DriverClientBase.Logger.Error(t.Exception, "Monitor thread failed.");
                        }

                        // Set the event, so the 'StopMonitoringThreads' method will be unblocked, if it's waiting for us.
                        taskFinishedEvent.Set();

                        // If the monitoring task has failed, we need to put the client into the faulted state.
                        ConnectionState currentState = this.state;
                        if (t.IsFaulted && currentState != ConnectionState.Closed && currentState != ConnectionState.Faulted)
                        {
                            // After the lock is acquired, any pending 'Disconnect()' should've already been finished.
                            lock (this.syncRoot)
                            {
                                if (this.state != ConnectionState.Closed && this.state != ConnectionState.Faulted)
                                {
                                    this.Disconnect();
                                    this.state = ConnectionState.Faulted;
                                }
                            }
                        }
                    });

                    // Wait for this monitor task to start.
                    taskStartedEvent.WaitOne();
                }
            }
        }

        /// <summary>
        /// Stops listening for driver notifications.
        /// </summary>
        /// <remarks>
        /// This method is invoked from the <see cref="Disconnect"/> and will be executed in the synchronization context, so no lock is needed.
        /// </remarks>
        private void StopMonitoringThreads()
        {
            this.cancellationTokenSource.Cancel();

            // Wait for all monitor tasks to complete.
            if (this.tasksCompletionWaitHandles.Any())
            {
                WaitHandle.WaitAll(this.tasksCompletionWaitHandles.ToArray());
                this.tasksCompletionWaitHandles.ForEach(wh => wh.Dispose());
                this.tasksCompletionWaitHandles.Clear();
            }

            this.cancellationTokenSource.Dispose();
            this.cancellationTokenSource = null;
        }

        /// <summary>
        /// Sends the command to the driver.
        /// </summary>
        /// <param name="command">Command to be sent to the driver.</param>
        /// <param name="responseType">Type of the response. This parameter may be <see langword="null"/>.</param>
        /// <returns>Response received from the driver, or <see langword="null"/>, if the <paramref name="responseType"/> is <see langword="null"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="responseType"/> is not a structure type.</exception>
        /// <exception cref="InvalidOperationException">
        /// Client is not connected.
        ///     <para>-or-</para>
        /// Memory for the command or response was not allocated.
        ///     <para>-or-</para>
        /// Message was not sent to the driver.
        /// </exception>
        private object ExecuteCommand(IDriverCommand command, Type responseType)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (responseType != null && (!responseType.IsValueType || responseType.IsPrimitive))
            {
                throw new ArgumentException("Response type is not a structure type.", nameof(responseType));
            }

            lock (this.syncRoot)
            {
                if (this.state != ConnectionState.Connected)
                {
                    throw new InvalidOperationException("Client is not connected.");
                }

                // We want to have two separate buffers, because the driver can update the response buffer
                // while parsing the command.
                IntPtr commandBuffer  = IntPtr.Zero;
                IntPtr responseBuffer = IntPtr.Zero;

                try
                {
                    // Allocate buffer for the command.
                    // Command header contains 'Type' and 'DataLength' (Int32) values.
                    // See the 'DRIVER_COMMAND' structure in the 'CommunicationData.h' for details.
                    int commandHeaderSize = Marshal.SizeOf(command.Type) + Marshal.SizeOf(typeof(int));
                    int commandDataSize   = command.Data?.Length ?? 0;
                    int commandSize       = commandHeaderSize + commandDataSize;

                    try
                    {
                        commandBuffer = Marshal.AllocHGlobal(commandSize);
                    }
                    catch (OutOfMemoryException oom)
                    {
                        string message = "Unable to allocate memory for the command.";

                        DriverClientBase.Logger.Error(message, oom);
                        throw new InvalidOperationException(message, oom);
                    }

                    // Marshal command to the buffer allocated, if the command.Data is NULL, it'll be ignored.
                    MarshalingHelper.MarshalObjectsToPointer(commandBuffer, commandSize, command.Type, commandDataSize, command.Data);

                    //
                    // Allocate the response buffer, if needed.
                    //

                    int responseSize = 0;

                    if (responseType != null)
                    {
                        responseSize = Marshal.SizeOf(responseType);

                        try
                        {
                            responseBuffer = Marshal.AllocHGlobal(responseSize);
                        }
                        catch (OutOfMemoryException oom)
                        {
                            string message = "Unable to allocate memory for the response.";

                            DriverClientBase.Logger.Error(message, oom);
                            throw new InvalidOperationException(message, oom);
                        }
                    }

                    //
                    // Send command to the driver.
                    //

                    uint bytesReceived;
                    uint hr = NativeMethods.FilterSendMessage(this.filterPortHandle, commandBuffer, (uint)commandSize, responseBuffer, (uint)responseSize, out bytesReceived);
                    if (hr != NativeMethods.Ok)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to send message to the driver: 0x{0:X8}", hr);
                        Exception innerException = Marshal.GetExceptionForHR((int)hr);

                        DriverClientBase.Logger.Error(innerException, message);
                        throw new InvalidOperationException(message, innerException);
                    }

                    // Return NULL, if response type is not specified.
                    return responseType == null ? null : Marshal.PtrToStructure(responseBuffer, responseType);
                }
                catch
                {
                    this.Disconnect();
                    this.state = ConnectionState.Faulted;

                    throw;
                }
                finally
                {
                    if (commandBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(commandBuffer);
                    }

                    if (responseBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(responseBuffer);
                    }
                }
            }
        }

        #endregion // Private methods
    }
}
