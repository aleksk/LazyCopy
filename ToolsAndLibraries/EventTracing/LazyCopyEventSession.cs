// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DriverEventCollector.cs">
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

namespace LazyCopy.EventTracing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using LazyCopy.Utilities.Extensions;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Session;

    /// <summary>
    /// This class is a wrapper for the <see cref="LazyCopyEventParser"/> that creates
    /// a real-time ETW session and invokes event handlers, when a certain messages are
    /// found in the trace.
    /// </summary>
    public class LazyCopyEventSession : IDisposable
    {
        #region Fields

        /// <summary>
        /// Default ETW session buffer size in MB.
        /// </summary>
        private const int DefaultBufferSizeMB = 32;

        /// <summary>
        /// ETW session name used by this listener.
        /// </summary>
        private readonly string name;

        /// <summary>
        /// ETW session buffer size in MB.
        /// </summary>
        private readonly int bufferSizeMB;

        /// <summary>
        /// Native ETW trace session.
        /// </summary>
        private TraceEventSession eventSession;

        /// <summary>
        /// Dispatcher for the events found in the <see cref="eventSession"/>.
        /// </summary>
        private ETWTraceEventSource eventSource;

        /// <summary>
        /// Whether the current event session is started.
        /// </summary>
        private bool isStarted;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyEventSession"/> class.
        /// </summary>
        /// <param name="name">ETW session name to use.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        public LazyCopyEventSession(string name)
            : this(name, LazyCopyEventSession.DefaultBufferSizeMB)
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyEventSession"/> class.
        /// </summary>
        /// <param name="name">ETW session name to use.</param>
        /// <param name="bufferSizeMB">ETW session buffer size in megabytes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSizeMB"/> is too small.</exception>
        public LazyCopyEventSession(string name, int bufferSizeMB)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (bufferSizeMB < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSizeMB), bufferSizeMB, "ETW session buffer size should be at least 1Mb.");
            }

            this.name         = name;
            this.bufferSizeMB = bufferSizeMB;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="LazyCopyEventSession"/> class.
        /// </summary>
        ~LazyCopyEventSession()
        {
            this.Dispose(false);
        }

        #endregion // Constructors

        #region Events

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileAccessed"/> event is logged.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileAccessedEventData> FileAccessed;

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileFetched"/> event is logged.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileFetchedEventData> FileFetched;

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileNotFetched"/> event is logged.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileNotFetchedEventData> FileNotFetched;

        #endregion // Events

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
        /// Starts a new ETW session, if it's not yet started.
        /// </summary>
        /// <exception cref="InvalidOperationException">The session is already started.</exception>
        public void Start()
        {
            if (this.isStarted)
            {
                throw new InvalidOperationException("ETW session is already started.");
            }

            // Stop the native ETW session, because it can be started by the previous application instance.
            this.Stop();

            ManualResetEvent startedEvent = new ManualResetEvent(false);

            Task.Factory.StartNew(
                () =>
                {
                    // Create a new real-time session.
                    this.eventSession = new TraceEventSession(this.name, fileName: null);
                    this.eventSession.StopOnDispose = true;
                    this.eventSession.BufferSizeMB  = this.bufferSizeMB;
                    this.eventSession.EnableProvider(LazyCopyEventParser.ProviderGuid);

                    using (this.eventSource = new ETWTraceEventSource(this.name, TraceEventSourceType.Session))
                    {
                        LazyCopyEventParser parser = new LazyCopyEventParser(this.eventSource);
                        parser.FileAccessed   += this.NotifyFileAccessed;
                        parser.FileFetched    += this.NotifyFileFetched;
                        parser.FileNotFetched += this.NotifyFileNotFetched;

                        try
                        {
                            startedEvent.Set();
                            this.eventSource.Process();
                        }
                        finally
                        {
                            parser.FileAccessed   -= this.NotifyFileAccessed;
                            parser.FileFetched    -= this.NotifyFileFetched;
                            parser.FileNotFetched -= this.NotifyFileNotFetched;
                        }
                    }
                },
                TaskCreationOptions.LongRunning);

            startedEvent.WaitOne();
            this.isStarted = true;
        }

        /// <summary>
        /// Stops the current ETW session.
        /// </summary>
        public void Stop()
        {
            if (this.eventSource != null)
            {
                this.eventSource.StopProcessing();
                this.eventSource.Dispose();
                this.eventSource = null;
            }

            if (this.eventSession != null)
            {
                this.eventSession.Stop(true);
                this.eventSession.Dispose();
                this.eventSession = null;
            }

            if (TraceEventSession.GetActiveSessionNames().Contains(this.name, StringComparer.OrdinalIgnoreCase))
            {
                using (TraceEventSession session = new TraceEventSession(this.name))
                {
                    session.Stop(true);
                }
            }

            this.isStarted = false;
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            this.Stop();
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// <see cref="LazyCopyEventParser.FileNotFetched"/> event handler.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">The <see cref="FileNotFetchedEventData"/> instance containing the event data.</param>
        private void NotifyFileNotFetched(object sender, FileNotFetchedEventData e)
        {
            this.FileNotFetched.Notify(this, e);
        }

        /// <summary>
        /// <see cref="LazyCopyEventParser.FileFetched"/> event handler.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">The <see cref="FileFetchedEventData"/> instance containing the event data.</param>
        private void NotifyFileFetched(object sender, FileFetchedEventData e)
        {
            this.FileFetched.Notify(this, e);
        }

        /// <summary>
        /// <see cref="LazyCopyEventParser.FileAccessed"/> event handler.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">The <see cref="FileAccessedEventData"/> instance containing the event data.</param>
        private void NotifyFileAccessed(object sender, FileAccessedEventData e)
        {
            this.FileAccessed.Notify(this, e);
        }

        #endregion // Private methods
    }
}
