// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileFetchedEventData.cs">
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

    using LazyCopy.Utilities;

    /// <summary>
    /// Event data for the <see cref="LazyCopyEventType.FileFetched"/> event.
    /// </summary>
    public class FileFetchedEventData : LazyCopyDriverEventData<FileFetchedEventData>
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFetchedEventData"/> class.
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
        public FileFetchedEventData(Action<FileFetchedEventData> callback, int eventId, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(callback, eventId, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            // Do nothing.
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets the path of the local file.
        /// </summary>
        public string LocalPath => PathHelper.ChangeDeviceNameToDriveLetter(this.GetUnicodeStringAt(0));

        /// <summary>
        /// Gets the path to the remote file, where the current <see cref="LocalPath"/> was fetched from.
        /// </summary>
        public string RemotePath => PathHelper.ChangeDeviceNameToDriveLetter(this.GetUnicodeStringAt(this.SkipUnicodeString(0)));

        /// <summary>
        /// Gets the size of the fetched file.
        /// </summary>
        public long Size => this.GetInt64At(this.SkipUnicodeString(0, 2));

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Gets names of all the field names for the event.
        /// </summary>
        public override string[] PayloadNames => this.payloadNames ?? (this.payloadNames = new[]
        {
            nameof(this.LocalPath),
            nameof(this.RemotePath),
            nameof(this.Size)
        });

        /// <summary>
        /// Returns the event payload value based on the <paramref name="index"/> given.
        /// </summary>
        /// <param name="index">Payload value index.</param>
        /// <returns>Payload value.</returns>
        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return this.LocalPath;
                case 1:
                    return this.RemotePath;
                case 2:
                    return this.Size;
                default:
                    return null;
            }
        }

        #endregion // Public methods
    }
}
