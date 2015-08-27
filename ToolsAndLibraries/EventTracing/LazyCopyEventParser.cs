// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyEventParser.cs">
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

    using LazyCopy.Utilities.Extensions;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Event parser for the LazyCopy ETW events.
    /// </summary>
    public class LazyCopyEventParser : TraceEventParser
    {
        #region Fields

        /// <summary>
        /// Provider name.
        /// </summary>
        public const string ProviderName = "LazyCopyDriver";

        /// <summary>
        /// Provider GUID.
        /// </summary>
        public static readonly Guid ProviderGuid = new Guid("{0FE08EE4-B08F-4D27-8CBB-C816308AE235}");

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyEventParser"/> class.
        /// </summary>
        /// <param name="source">Event source.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
        public LazyCopyEventParser(TraceEventSource source)
            : base(source)
        {
            if (this.source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Register callbacks for known event types.
            this.source.RegisterEventTemplate(new FileAccessedEventData(this.InvokeFileAccessed,     (int)LazyCopyEventType.FileAccessed,   0, null, Guid.Empty, 0, null, LazyCopyEventParser.ProviderGuid, LazyCopyEventParser.ProviderName));
            this.source.RegisterEventTemplate(new FileFetchedEventData(this.InvokeFileFetched,       (int)LazyCopyEventType.FileFetched,    0, null, Guid.Empty, 0, null, LazyCopyEventParser.ProviderGuid, LazyCopyEventParser.ProviderName));
            this.source.RegisterEventTemplate(new FileNotFetchedEventData(this.InvokeFileNotFetched, (int)LazyCopyEventType.FileNotFetched, 0, null, Guid.Empty, 0, null, LazyCopyEventParser.ProviderGuid, LazyCopyEventParser.ProviderName));
        }

        #endregion // Constructor

        #region Events

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileAccessed"/> event is found in the source.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileAccessedEventData> FileAccessed;

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileFetched"/> event is found in the source.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileFetchedEventData> FileFetched;

        /// <summary>
        /// Occurs when a <see cref="LazyCopyEventType.FileNotFetched"/> event is found in the source.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly",      Justification = "The current declaration is desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "The current name is desired.")]
        public event EventHandler<FileNotFetchedEventData> FileNotFetched;

        #endregion // Events

        #region Protected methods

        /// <summary>
        /// Returns the name of the provider this parser can handle.
        /// </summary>
        /// <returns>Provider name.</returns>
        protected override string GetProviderName()
        {
            return LazyCopyEventParser.ProviderName;
        }

        /// <summary>
        /// Returns a list of all templates currently existing.
        /// </summary>
        /// <param name="eventsToObserve">Template enumerator.</param>
        /// <param name="callback">Callback to be invoked per event.</param>
        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            throw new NotImplementedException();
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Invokes the <see cref="FileAccessed"/> handles, if any.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        private void InvokeFileAccessed(FileAccessedEventData eventData)
        {
            this.FileAccessed.Notify(this, eventData);
        }

        /// <summary>
        /// Invokes the <see cref="FileFetched"/> handles, if any.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        private void InvokeFileFetched(FileFetchedEventData eventData)
        {
            this.FileFetched.Notify(this, eventData);
        }

        /// <summary>
        /// Invokes the <see cref="FileNotFetched"/> handles, if any.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        private void InvokeFileNotFetched(FileNotFetchedEventData eventData)
        {
            this.FileNotFetched.Notify(this, eventData);
        }

        #endregion // Private methods
    }
}
