// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EventHandlerEx.cs">
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

namespace LazyCopy.Utilities.Extensions
{
    using System;

    /// <summary>
    /// Contains extension methods for the <see cref="EventHandler"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "This suffix is desired here.")]
    public static class EventHandlerEx
    {
        /// <summary>
        /// Invokes event handlers, if any, with empty event data.
        /// </summary>
        /// <param name="handler">Event handlers.</param>
        /// <param name="sender">Event sender.</param>
        public static void Notify(this EventHandler handler, object sender)
        {
            handler?.Invoke(sender, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes event handlers, if any.
        /// </summary>
        /// <param name="handler">Event handlers.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        public static void Notify(this EventHandler handler, object sender, EventArgs args)
        {
            handler?.Invoke(sender, args);
        }

        /// <summary>
        /// Invokes event handlers, if any, with empty event data.
        /// </summary>
        /// <typeparam name="T">Event data type.</typeparam>
        /// <param name="handler">Event handlers.</param>
        /// <param name="sender">Event sender.</param>
        public static void Notify<T>(this EventHandler<T> handler, object sender)
            where T : new()
        {
            handler?.Invoke(sender, new T());
        }

        /// <summary>
        /// Invokes event handlers, if any.
        /// </summary>
        /// <typeparam name="T">Event data type.</typeparam>
        /// <param name="handler">Event handlers.</param>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">The <typeparamref name="T"/> instance containing the event data.</param>
        public static void Notify<T>(this EventHandler<T> handler, object sender, T args)
        {
            handler?.Invoke(sender, args);
        }
    }
}
