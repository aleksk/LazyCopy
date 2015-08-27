// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConsoleHelper.cs">
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
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Contains helper functions to work with <see cref="Console"/>.
    /// </summary>
    public static class ConsoleHelper
    {
        #region Fields

        /// <summary>
        /// Whether the current application has the console window available.
        /// </summary>
        public static readonly bool ConsoleWindowAvailable;

        /// <summary>
        /// Width of the current console window.
        /// </summary>
        public static readonly int WindowWidth;

        /// <summary>
        /// String to be used to print the horizontal line to the console.
        /// </summary>
        public static readonly string HorizontalLine;

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Initializes the <see cref="ConsoleHelper"/> class.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Field initialization may throw an exception, so doing it here.")]
        static ConsoleHelper()
        {
            ConsoleHelper.WindowWidth = 80;

            try
            {
                ConsoleHelper.WindowWidth            = Console.WindowWidth;
                ConsoleHelper.ConsoleWindowAvailable = true;
            }
            catch (IOException)
            {
                // We might now have the console window, so ignore any exceptions.
                ConsoleHelper.ConsoleWindowAvailable = false;
            }

            ConsoleHelper.HorizontalLine = new string('\u2500', ConsoleHelper.WindowWidth - 1);
        }

        #endregion // Constructor

        #region Public methods

        /// <summary>
        /// Gets the user's choice from the console (not the standard input stream).
        /// This method works like the <c>choice</c> command in the Windows batch files:
        /// <c>http://en.wikipedia.org/wiki/Choice_(command)</c>.
        /// </summary>
        /// <param name="message">Choice message.</param>
        /// <param name="options">List of options.</param>
        /// <param name="ignoreCase">If set to <see langword="true"/>, options case will be ignored.</param>
        /// <returns>User's choice.</returns>
        /// <remarks>
        /// The difference between this and the <see cref="Choice(string,string[])"/> method is that this method uses <see cref="Console.ReadKey()"/>
        /// to get the immediate user input, while <see cref="Choice(string,string[])"/> uses <see cref="Console.In"/> to get it.<br/>
        /// So, don't use this method, if the <see cref="ConsoleWindowAvailable"/> is <see langword="false"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or the <paramref name="options"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static char Choice(string message, char[] options, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (options == null || options.Length == 0)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            IList<string> optionsStr = options.Select(o => o.ToString(CultureInfo.InvariantCulture)).ToList();

            Console.Out.Write("{0} [{1}] ", message, string.Join(",", optionsStr));
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (optionsStr.Contains(key.KeyChar.ToString(CultureInfo.InvariantCulture), ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                {
                    Console.Out.Write("{0}\n", key.KeyChar);
                    return key.KeyChar;
                }

                Console.Beep();
            }
        }

        /// <summary>
        /// Gets the user's choice from the standard input stream based on the selection list provided.
        /// User can cancel the selection using the <c>X</c> option.
        /// </summary>
        /// <param name="message">Choice message.</param>
        /// <param name="options">List of available options.</param>
        /// <returns>
        /// Index of the option selected from the <paramref name="options"/> list.
        /// If nothing is selected, returns <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> or the <paramref name="options"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static int Choice(string message, string[] options)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (options == null || options.Length == 0)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            int selectedIndex = -1;

            // Print out the message and selection list.
            Console.Out.WriteLine(message);

            for (int i = 0; i < options.Length; i++)
            {
                Console.Out.WriteLine("   {0}  {1}", i + 1, options[i]);
            }

            Console.Out.WriteLine("\n   X  Exit");
            Console.Out.WriteLine(string.Empty);

            while (true)
            {
                Console.Out.Write(" Make a selection: ");

                string selectionStr = Console.ReadLine();
                if (string.Equals("X", selectionStr, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (int.TryParse(selectionStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out selectedIndex)
                    && selectedIndex > 0 && selectedIndex <= options.Length)
                {
                    // We show items starting from '1'.
                    selectedIndex--;
                    break;
                }

                Console.Beep();

                Console.CursorTop--;
                ConsoleHelper.ClearCurrentLine();
            }

            return selectedIndex;
        }

        /// <summary>
        /// Draws a progress bar in a console window using the default character set.
        /// </summary>
        /// <param name="percentage">Progress bar percentage to be displayed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentage"/> is negative or greater than <c>100</c>.</exception>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        /// <seealso cref="RenderConsoleProgress(int,char,char,string)"/>
        public static void RenderConsoleProgress(int percentage)
        {
            if (percentage < 0 || percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage should be greater or equal to zero and lesser or equal to 100.");
            }

            ConsoleHelper.RenderConsoleProgress(percentage, '\u2588', '\u2591', string.Format(CultureInfo.InvariantCulture, "{0, 4}%", percentage));
        }

        /// <summary>
        /// Draws a progress bar in a console window using the <paramref name="filledCharacter"/> and <paramref name="emptyCharacter"/>
        /// to make up the progress bar elements.
        /// A <paramref name="message"/> can be displayed to the right of the progress bar at the same time.
        /// </summary>
        /// <param name="percentage">Progress bar percentage to be displayed.</param>
        /// <param name="filledCharacter">Character to be used to build filled (completed) part of the progress bar.</param>
        /// <param name="emptyCharacter">Character to be used to build empty (not completed) part of the progress bar.</param>
        /// <param name="message">Message to be displayed to the right of the progress bar. May be <see langword="null"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentage"/> is negative or greater than <c>100</c>.</exception>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static void RenderConsoleProgress(int percentage, char filledCharacter, char emptyCharacter, string message)
        {
            if (percentage < 0 || percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage should be greater or equal to zero and lesser or equal to 100.");
            }

            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            message = message ?? string.Empty;

            Console.CursorVisible = false;

            int lineWidth         = ConsoleHelper.WindowWidth - 1;
            int progressBarWidth  = lineWidth - message.Length - 3;
            int filledWidth       = (int)((progressBarWidth * percentage) / 100.0);

            string filledProgress = new string(filledCharacter, filledWidth);
            string emptyProgress  = new string(emptyCharacter, progressBarWidth - filledWidth);

            ConsoleHelper.OverwriteConsoleMessage(string.Format(CultureInfo.InvariantCulture, " [{0}{1}]{2}", filledProgress, emptyProgress, message));

            Console.CursorVisible = true;
        }

        /// <summary>
        /// Writes the message to the current line in the console overwriting the existing text.
        /// This method truncates the <paramref name="message"/>, if it doesn't fit in the single line.
        /// </summary>
        /// <param name="message">Message to be displayed in the console.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static void OverwriteConsoleMessage(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            message = ConsoleHelper.TruncateToLineWidth(message);

            // Move cursor to the beginning of the line.
            Console.CursorLeft = 0;
            Console.Write(message);
        }

        /// <summary>
        /// Clears the current line in the console.
        /// </summary>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static void ClearCurrentLine()
        {
            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            Console.CursorLeft = 0;
            Console.Write(new string(' ', ConsoleHelper.WindowWidth));
            Console.CursorLeft = 0;
            Console.CursorTop--;
        }

        /// <summary>
        /// Truncates the <paramref name="value"/> given to fit in the current console line.
        /// </summary>
        /// <param name="value">String to be truncated.</param>
        /// <returns>
        /// If the <paramref name="value"/> does not fit in the console window, this method returns truncated string ending with the '<c>...</c>' suffix.<br/>
        /// Otherwise, it returns the original <paramref name="value"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        public static string TruncateToLineWidth(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int lineWidth = ConsoleHelper.WindowWidth - 1;
            value = value.Length > lineWidth ? value.Substring(0, lineWidth - 3) + "..." : value;

            // Append spaces to the message, so the entire line will be overwritten.
            return value + new string(' ', lineWidth - value.Length);
        }

        /// <summary>
        /// Sets the new console parameters, like cursor position and text color.
        /// </summary>
        /// <param name="newState">The desired console state.</param>
        /// <returns><see cref="IDisposable"/> instance that will restore the console to its original state, when disposed.</returns>
        /// <exception cref="InvalidOperationException">Console window is not available.</exception>
        public static IDisposable SetConsoleState(ConsoleState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            if (!ConsoleHelper.ConsoleWindowAvailable)
            {
                throw new InvalidOperationException("Console window is not available.");
            }

            return new ConsoleStateManager(newState);
        }

        #endregion // Public methods

        #region Private classes

        /// <summary>
        /// This class manages and restores the console state set by the <see cref="ConsoleHelper.SetConsoleState"/> method.
        /// </summary>
        private class ConsoleStateManager : IDisposable
        {
            #region Fields

            /// <summary>
            /// State of the console.
            /// </summary>
            private ConsoleState consoleState;

            #endregion // Fields

            #region Constructor

            /// <summary>
            /// Initializes a new instance of the <see cref="ConsoleStateManager"/> class.
            /// </summary>
            /// <param name="newState">New console state.</param>
            public ConsoleStateManager(ConsoleState newState)
            {
                this.SaveCurrentState();
                this.ApplyState(newState);
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="ConsoleStateManager"/> class.
            /// </summary>
            ~ConsoleStateManager()
            {
                this.Dispose(false);
            }

            #endregion // Constructor

            #region Public methods

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            #endregion // Public methods

            #region Private methods

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources.
            /// </summary>
            /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.RestoreState();
                }
            }

            /// <summary>
            /// Saves the current <see cref="Console"/> state.
            /// </summary>
            private void SaveCurrentState()
            {
                this.RestoreState();
                this.consoleState = ConsoleState.CreateFromCurrent();
            }

            /// <summary>
            /// Applies the <paramref name="state"/> to the console.
            /// </summary>
            /// <param name="state">Console state.</param>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Having this method as an instance one looks better.")]
            private void ApplyState(ConsoleState state)
            {
                Console.BackgroundColor = state.BackgroundColor;
                Console.ForegroundColor = state.ForegroundColor;
                Console.CursorLeft      = state.CursorLeft;
                Console.CursorTop       = state.CursorTop;
                Console.CursorVisible   = state.CursorVisible;
            }

            /// <summary>
            /// Restores the <see cref="Console"/> to the saved state, if any.
            /// </summary>
            private void RestoreState()
            {
                ConsoleState stateToRestore = Interlocked.Exchange(ref this.consoleState, null);
                if (stateToRestore == null)
                {
                    return;
                }

                this.ApplyState(stateToRestore);
            }

            #endregion // Private methods
        }

        #endregion // Private classes
    }
}
