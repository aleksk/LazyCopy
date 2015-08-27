// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConsoleState.cs">
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

namespace LazyCopy.Utilities
{
    using System;

    /// <summary>
    /// Contains information about the console state.
    /// </summary>
    /// <seealso cref="ConsoleHelper.SetConsoleState"/>
    public class ConsoleState
    {
        /// <summary>
        /// Gets or sets the top cursor position.
        /// </summary>
        public int CursorTop { get; set; }

        /// <summary>
        /// Gets or sets the left cursor position.
        /// </summary>
        public int CursorLeft { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the cursor is visible.
        /// </summary>
        public bool CursorVisible { get; set; }

        /// <summary>
        /// Gets or sets the text background color.
        /// </summary>
        public ConsoleColor BackgroundColor { get; set; }

        /// <summary>
        /// Gets or sets the text foreground color.
        /// </summary>
        public ConsoleColor ForegroundColor { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="ConsoleState"/> based on the current <see cref="Console"/> parameters.
        /// </summary>
        /// <returns>Current <see cref="Console"/> state.</returns>
        public static ConsoleState CreateFromCurrent()
        {
            return new ConsoleState
            {
                BackgroundColor = Console.BackgroundColor,
                ForegroundColor = Console.ForegroundColor,
                CursorLeft      = Console.CursorLeft,
                CursorTop       = Console.CursorTop,
                CursorVisible   = Console.CursorVisible
            };
        }
    }
}
