// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyDriverEventData.cs">
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

    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Abstract class for all LazyCopy ETW events.
    /// </summary>
    /// <typeparam name="T">Callback type.</typeparam>
    public abstract class LazyCopyDriverEventData<T> : TraceEvent
        where T : LazyCopyDriverEventData<T>
    {
        #region Fields

        /// <summary>
        /// Action to be invoked when this event is found.
        /// </summary>
        private readonly Action<T> callback;

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyDriverEventData{T}"/> class.
        /// </summary>
        /// <param name="callback">Action to be invoked when this event is found.</param>
        /// <param name="eventId">Event ID.</param>
        /// <param name="task">Event task.</param>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="taskGuid">Task GUID.</param>
        /// <param name="opcode">Event OpCode.</param>
        /// <param name="opcodeName">Name of the OpCode.</param>
        /// <param name="providerGuid">Event provider GUID.</param>
        /// <param name="providerName">Name of the event provider.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",  MessageId = "opcode", Justification = "Opcode case is correct.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "guid",   Justification = "Naming is taken from the parent class.")]
        protected LazyCopyDriverEventData(Action<T> callback, int eventId, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventId, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            this.callback = callback;
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        public LazyCopyEventType EventType => (LazyCopyEventType)this.ID;

        /// <summary>
        /// Returns (or sets) the delegate associated with this event.
        /// </summary>
        protected override Delegate Target { get; set; }

        #endregion // Properties

        #region Protected methods

        /// <summary>
        /// Invokes the <see cref="callback"/>.
        /// </summary>
        protected override void Dispatch()
        {
            this.callback((T)this);
        }

        #endregion // Protected methods
    }
}
