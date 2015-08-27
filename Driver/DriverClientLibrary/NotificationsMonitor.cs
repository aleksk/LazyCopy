// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NotificationsMonitor.cs">
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
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using LazyCopy.DriverClientLibrary.Native;
    using LazyCopy.Utilities;
    using Microsoft.Win32.SafeHandles;
    using NLog;

    /// <summary>
    /// This class is a wrapper for a <see cref="Task"/> delegate that is waiting for the
    /// driver notifications, marshals them, invokes the user-defined handler, and sends the
    /// reply back to the driver.
    /// </summary>
    /// <seealso cref="DoWork"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Safe handles wrappers don't own the pointers")]
    internal class NotificationsMonitor
    {
        #region Fields

        /// <summary>
        /// Timeout to wait on the <see cref="Native.NativeMethods.GetQueuedCompletionStatus"/> method.
        /// </summary>
        /// <remarks>
        /// The current value is <c>300 milliseconds</c>.
        /// </remarks>
        private const int QueueTimeout = 300;

        /// <summary>
        /// Logger instance.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Size of the <see cref="DriverNotificationHeader"/> structure.
        /// </summary>
        private readonly int notificationHeaderSize = Marshal.SizeOf(typeof(DriverNotificationHeader));

        /// <summary>
        /// Size of the <see cref="DriverReplyHeader"/> structure.
        /// </summary>
        private readonly int replyHeaderSize = Marshal.SizeOf(typeof(DriverReplyHeader));

        /// <summary>
        /// User-defined notification handler.
        /// </summary>
        private readonly Func<IDriverNotification, object> handler;

        /// <summary>
        /// Driver port handle.
        /// </summary>
        private readonly SafeFileHandle filterPortHandle;

        /// <summary>
        /// Driver I/O completion port handle.
        /// </summary>
        private readonly SafeFileHandle completionPortHandle;

        /// <summary>
        /// Task cancellation token.
        /// </summary>
        private readonly CancellationToken token;

        /// <summary>
        /// Default notification buffer size.
        /// </summary>
        private readonly int bufferSize;

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationsMonitor"/> class.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <param name="bufferSize">The desired size of the buffer used to store notification structures received from the driver into.</param>
        /// <param name="handler">User-defined notification handler.</param>
        /// <param name="filterPortHandle">Driver port handle.</param>
        /// <param name="completionPortHandle">Driver I/O completion port handle.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is invalid.</exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="handler"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="filterPortHandle"/> or <paramref name="completionPortHandle"/> are invalid pointers.
        /// </exception>
        /// <remarks>
        /// The real buffer allocated will be bigger, than the <paramref name="bufferSize"/> specified, because every notification
        /// should also contain <see cref="DriverNotificationHeader"/> structure. So we add it to the buffer to make sure it'll be large enough to store both
        /// header and data.
        /// </remarks>
        public NotificationsMonitor(CancellationToken token, int bufferSize, Func<IDriverNotification, object> handler, IntPtr filterPortHandle, IntPtr completionPortHandle)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "The desired buffer size is negative or equal to zero.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (filterPortHandle == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(filterPortHandle));
            }

            if (completionPortHandle == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(completionPortHandle));
            }

            this.token                = token;
            this.bufferSize           = bufferSize;
            this.handler              = handler;
            this.filterPortHandle     = new SafeFileHandle(filterPortHandle, false);
            this.completionPortHandle = new SafeFileHandle(completionPortHandle, false);
        }

        #endregion // Constructor

        #region Public methods

        /// <summary>
        /// This method is passed as an action delegate to the <see cref="Task.Factory"/> and invoked when the according <see cref="Task"/> is started.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Message request was not sent to the driver.
        ///     <para>-or-</para>
        /// I/O completion status was not retrieved.
        ///     <para>-or-</para>
        /// Reply was not sent to the driver.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle",
            Justification = "The I/O will be cancelled before leaving this method, so no AVs should occur.")]
        public void DoWork()
        {
            // Notification buffer must also include a message header. This also validates the 'this.bufferSize' parameter.
            // The 'resetEvent' will be set by the I/O completion port via the OVERLAPPED structure when notification is available.
            using (ResizableBuffer resizableBuffer = new ResizableBuffer(this.notificationHeaderSize + this.bufferSize))
            using (ManualResetEvent resetEvent     = new ManualResetEvent(false))
            {
                // This OVERLAPPED structure will be passed to the 'FilterGetMessage' method, so it'll operate in the asynchronous mode.
                NativeOverlapped overlapped = new NativeOverlapped { EventHandle = resetEvent.SafeWaitHandle.DangerousGetHandle() };

                try
                {
                    // Get the next notification and store it into the 'resizableBuffer'.
                    while (this.GetNextNotification(resizableBuffer, resetEvent, overlapped))
                    {
                        // Get the reply and send it back to the driver.
                        this.ProcessNotification(resizableBuffer);
                    }
                }
                finally
                {
                    this.CancelIo(overlapped);
                }
            }
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Gets the next notification.
        /// </summary>
        /// <param name="resizableBuffer">Resizable buffer to store notification into.</param>
        /// <param name="resetEvent">Reset event used in the <paramref name="overlapped"/> structure.</param>
        /// <param name="overlapped">Native structure to be used by the driver for notifications.</param>
        /// <returns>
        /// <see langword="true"/>, if the notification was successfully received and stored in the <paramref name="resizableBuffer"/> variable;
        /// <see langword="false"/>, if the operation or the current task was cancelled.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Message request was not sent to the driver.
        ///     <para>-or-</para>
        /// I/O completion status was not retrieved.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle",
            Justification = "The I/O will be cancelled in the 'DoWork()' method, so no AVs should occur.")]
        private bool GetNextNotification(ResizableBuffer resizableBuffer, ManualResetEvent resetEvent, NativeOverlapped overlapped)
        {
            uint numberOfBytesTransferred;
            IntPtr completionKey;
            NativeOverlapped nativeOverlapped;

            // Buffer to marshal the notification into.
            IntPtr notificationBuffer = resizableBuffer.DangerousGetPointer();
            resetEvent.Reset();

            //
            // 'Asynchronously' request and get a message from the driver.
            //

            // FilterGetMessage returns ERROR_IO_PENDING, if it's set to operate in the asynchronous mode.
            uint hr = NativeMethods.FilterGetMessage(this.filterPortHandle, notificationBuffer, this.bufferSize, ref overlapped);
            if (hr != NativeMethods.Ok && hr != NativeMethods.ErrorIoPending)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to request for a message: 0x{0:X8}", hr), Marshal.GetExceptionForHR(unchecked((int)hr)));
            }

            // If we don't specify timeout, this method will wait forever, but we want to be able to react on the task cancellation.
            while (!NativeMethods.GetQueuedCompletionStatus(this.completionPortHandle.DangerousGetHandle(), out numberOfBytesTransferred, out completionKey, out nativeOverlapped, NotificationsMonitor.QueueTimeout))
            {
                hr = unchecked((uint)Marshal.GetHRForLastWin32Error());

                // Break on the task cancellation.
                if (this.token.IsCancellationRequested)
                {
                    break;
                }

                // If the WAIT_TIMEOUT is returned, there was no notification available and we want to wait again.
                if (hr == NativeMethods.WaitTimeout)
                {
                    continue;
                }

                // If the I/O was cancelled on the completion port, or the completion port was closed.
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Invalid completion status: 0x{0:X8}", hr), Marshal.GetExceptionForHR(unchecked((int)hr)));
            }

            // If the current task was cancelled, the token wait handle will be set.
            // The 'resetEvent' will be set via the overlapped' structure, when the driver finishes writing notification to the buffer.
            return WaitHandle.WaitAny(new[] { this.token.WaitHandle, resetEvent }) == 1;
        }

        /// <summary>
        /// Gets the reply by calling the notification handler and sends the reply back to the driver.
        /// </summary>
        /// <param name="resizableBuffer">Resizable buffer that contains the notification received from the driver.</param>
        /// <exception cref="InvalidOperationException">
        /// The reply cannot be marshaled.
        ///     <para>-or-</para>
        /// The reply size is bigger, than the one expected by the driver.
        ///     <para>-or-</para>
        /// Reply was not sent to the driver.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "General exception is required here.")]
        private void ProcessNotification(ResizableBuffer resizableBuffer)
        {
            IntPtr bufferPointer = resizableBuffer.DangerousGetPointer();

            // Marshal the notification received.
            DriverNotificationHeader header = (DriverNotificationHeader)Marshal.PtrToStructure(bufferPointer, typeof(DriverNotificationHeader));
            DriverNotification notification = new DriverNotification { Type = header.Type, DataLength = header.DataLength, Data = bufferPointer + this.notificationHeaderSize };

            // Get the reply object from the user-defined handler.
            object reply = null;
            int handlerResult = (int)NativeMethods.Ok;

            try
            {
                reply = this.handler(notification);
            }
            catch (Exception e)
            {
                handlerResult = Marshal.GetHRForException(e);
                NotificationsMonitor.Logger.Error(e, "Notification handler threw an exception.");
            }

            // Driver is not expecting any reply.
            if (header.ReplyLength == 0)
            {
                if (reply != null)
                {
                    NotificationsMonitor.Logger.Warn(CultureInfo.InvariantCulture, "Driver is not expecting any reply, but reply object is returned from handler: {0}", reply.GetType());
                }

                return;
            }

            int replySize = this.replyHeaderSize + MarshalingHelper.GetObjectSize(reply);
            if (replySize > header.ReplyLength)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Reply ({0} bytes) is bigger than the one expected by the driver ({1} bytes).", replySize, header.ReplyLength));
            }

            DriverReplyHeader replyHeader = new DriverReplyHeader
            {
                MessageId = header.MessageId,

                // Notify driver about the exception thrown, if any.
                Status    = handlerResult
            };

            // Adjust the buffer to fit the reply.
            resizableBuffer.Resize(replySize);
            bufferPointer = resizableBuffer.DangerousGetPointer();

            // Marshal reply to the output buffer.
            MarshalingHelper.MarshalObjectsToPointer(bufferPointer, replySize, replyHeader, reply);

            // And send it to the driver.
            uint hr = NativeMethods.FilterReplyMessage(this.filterPortHandle, bufferPointer, (uint)replySize);
            if (hr != NativeMethods.Ok)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to send reply: 0x{0:X8}", hr), Marshal.GetExceptionForHR(unchecked((int)hr)));
            }
        }

        /// <summary>
        /// Cancels the pending I/O on the filter port.
        /// </summary>
        /// <param name="overlapped">OVERLAPPED structure.</param>
        private void CancelIo(NativeOverlapped overlapped)
        {
            // We need to cancel I/O before leaving, so the driver won't try to read the OVERLAPPED memory (and no AVs will occur).
            if (!NativeMethods.CancelIoEx(this.filterPortHandle, ref overlapped))
            {
                uint hr = unchecked((uint)Marshal.GetHRForLastWin32Error());
                if (hr != NativeMethods.ErrorNotFound)
                {
                    NotificationsMonitor.Logger.Warn(CultureInfo.InvariantCulture, "Unable to cancel I/O for the current task: 0x{0:X8}", hr);
                }
            }
        }

        #endregion // Private methods
    }
}
