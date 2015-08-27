// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectionState.cs">
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
    /// <summary>
    /// Represents the current connection state of a <see cref="DriverClientBase"/> instance.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Indicates that the client has been instantiated and is configurable,
        /// but not yet open or ready for use.
        /// </summary>
        Created = 0,

        /// <summary>
        /// Indicates that the client is being transitioned from the <see cref="Created"/>
        /// state to the <see cref="Connected"/> state.
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// Indicates that the client is now connected and ready to be used.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Indicates that the client is transitioning to the <see cref="Closed"/> state.
        /// </summary>
        Closing = 3,

        /// <summary>
        /// Indicates that the client has been closed and is no longer usable.
        /// </summary>
        Closed = 4,

        /// <summary>
        /// Indicates that the client has encountered an error or fault from which it cannot
        /// recover and from which it is no longer usable.
        /// </summary>
        Faulted = 5
    }
}
