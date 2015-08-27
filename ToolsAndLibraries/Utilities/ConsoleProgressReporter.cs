// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConsoleProgressReporter.cs">
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
    using System.Globalization;
    using System.Timers;

    using LongPath;

    /// <summary>
    /// This class shows the progress bar in the system console window.
    /// </summary>
    /// <remarks>
    /// This class does not update the progress every time the <see cref="AddCompletedEntries"/>
    /// method is called.<br/>
    /// This behavior in controlled by the <see cref="UpdateFrequency"/> property.
    /// </remarks>
    /// <see cref="Create"/>
    public sealed class ConsoleProgressReporter : IConsoleProgressReporter
    {
        #region Fields

        /// <summary>
        /// Minimum progress update frequency value.
        /// </summary>
        private static readonly TimeSpan MinimumUpdateFrequency = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Total amount of entries to report.
        /// </summary>
        private readonly long totalEntries;

        /// <summary>
        /// Current step number.
        /// </summary>
        private volatile int currentStep;

        /// <summary>
        /// Name of the current step.
        /// </summary>
        private volatile string currentStepName;

        /// <summary>
        /// Current amount of entries reported.
        /// </summary>
        private long currentEntries;

        /// <summary>
        /// Whether the current progress bar is visible.
        /// </summary>
        private volatile bool visible;

        /// <summary>
        /// Timer used for progress updates.
        /// </summary>
        private Timer progressUpdateTimer;

        /// <summary>
        /// Console state with information about the cursor position to update the progress bar.
        /// </summary>
        private ConsoleState consoleState;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleProgressReporter"/> class.
        /// </summary>
        /// <param name="totalEntries">Total amount of entries to report.</param>
        /// <param name="updateFrequency">Time interval after which the progress should be reported.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="totalEntries"/> is negative or equal to zero.
        ///     <para>-or-</para>
        /// <paramref name="updateFrequency"/> is lesser than the minimal allowed value.
        /// </exception>
        private ConsoleProgressReporter(long totalEntries, TimeSpan updateFrequency)
        {
            if (totalEntries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalEntries), totalEntries, "Total entries cannot be negative or equal to zero.");
            }

            if (updateFrequency < ConsoleProgressReporter.MinimumUpdateFrequency)
            {
                throw new ArgumentOutOfRangeException(nameof(updateFrequency), updateFrequency, string.Format(CultureInfo.InvariantCulture, "Update frequency should be at least {0}", ConsoleProgressReporter.MinimumUpdateFrequency));
            }

            this.totalEntries    = totalEntries;
            this.UpdateFrequency = updateFrequency;

            this.progressUpdateTimer           = new Timer(this.UpdateFrequency.TotalMilliseconds);
            this.progressUpdateTimer.AutoReset = true;
            this.progressUpdateTimer.Elapsed  += (sender, args) => this.UpdateProgress();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ConsoleProgressReporter"/> class.
        /// </summary>
        ~ConsoleProgressReporter()
        {
            this.Dispose(false);
        }

        #endregion // Constructors

        #region Properties

        /// <summary>
        /// Gets the current progress update frequency.
        /// </summary>
        /// <value>
        /// Indicates how often the progress bar will be updated.
        /// </value>
        public TimeSpan UpdateFrequency { get; }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Creates the <see cref="IConsoleProgressReporter"/> instance based on the current console capabilities.<br/>
        /// For example, if the current console window is not available, a dummy reporter instance is returned.
        /// </summary>
        /// <param name="totalEntries">Total amount of entries to report.</param>
        /// <param name="updateFrequency">Progress update frequency.</param>
        /// <returns>The suitable <see cref="IConsoleProgressReporter"/> implementation.</returns>
        public static IConsoleProgressReporter Create(long totalEntries, TimeSpan updateFrequency)
        {
            return ConsoleHelper.ConsoleWindowAvailable ? (IConsoleProgressReporter)new ConsoleProgressReporter(totalEntries, updateFrequency) : new DummyProgressReporter();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Advances progress to the next step.
        /// </summary>
        /// <param name="stepName">Step name. If it's not equal to the previously used name, progress will move to the next line keeping the previous step visible.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stepName"/> is <see langword="null"/> or empty.</exception>
        public void StartNewStep(string stepName)
        {
            if (string.IsNullOrEmpty(stepName))
            {
                throw new ArgumentNullException(nameof(stepName));
            }

            lock (this.syncRoot)
            {
                if (this.currentStep > 0)
                {
                    // We want to keep the previous step name, so move progress bar with title on the next line.
                    if (!string.Equals(stepName, this.currentStepName))
                    {
                        // Make sure that the buffer have enough space to move on the next line.
                        if (Console.CursorTop == Console.BufferHeight - 1)
                        {
                            Console.Out.WriteLine(string.Empty);
                        }
                        else
                        {
                            Console.CursorTop++;
                            this.consoleState.CursorTop++;
                        }
                    }
                }
                else
                {
                    this.ShowProgressBar();
                    this.progressUpdateTimer.Start();
                }

                this.currentStep++;
                this.currentStepName = stepName;

                this.UpdateTitle();
                this.UpdateProgress();
            }
        }

        /// <summary>
        /// Hides the progress bar, if the current amount of entries is equal to the total
        /// amount of entries to be reported.
        /// </summary>
        /// <see cref="AddCompletedEntries"/>
        public void FinishCurrentStep()
        {
            lock (this.syncRoot)
            {
                // Hide progress bar if we're finished.
                if (this.currentEntries >= this.totalEntries)
                {
                    this.HideProgressBar();
                }
                else
                {
                    this.UpdateProgress();
                }
            }
        }

        /// <summary>
        /// Advances the current progress in accordance with the <paramref name="entries"/> amount given.
        /// </summary>
        /// <param name="entries">Amount of entries finished.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="entries"/> is negative or equal to zero.
        ///     <para>-or-</para>
        /// More entries are reported than the total amount of entries specified during the class instantiation.
        /// </exception>
        /// <see cref="Create"/>
        public void AddCompletedEntries(int entries)
        {
            if (entries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), entries, "Entries amount cannot be negative or equal to zero.");
            }

            lock (this.syncRoot)
            {
                if (this.currentEntries + entries > this.totalEntries)
                {
                    throw new ArgumentOutOfRangeException(nameof(entries), entries, string.Format(CultureInfo.InvariantCulture, "More entries reported ({0} than requested ({1}).", this.currentEntries + entries, this.totalEntries));
                }

                this.currentEntries += entries;
            }
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.progressUpdateTimer != null)
                {
                    this.progressUpdateTimer.Stop();
                    this.progressUpdateTimer.Dispose();
                    this.progressUpdateTimer = null;
                }

                this.HideProgressBar();
            }
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Displays the progress bar with an empty title.
        /// </summary>
        private void ShowProgressBar()
        {
            lock (this.syncRoot)
            {
                if (this.visible)
                {
                    return;
                }

                Console.Out.WriteLine(string.Empty);

                this.consoleState = ConsoleState.CreateFromCurrent();
                this.consoleState.CursorTop--;
                this.consoleState.CursorLeft = 0;
                this.consoleState.CursorVisible = false;

                using (ConsoleHelper.SetConsoleState(this.consoleState))
                {
                    // Set an empty title and set the progress to zero.
                    Console.Out.WriteLine(ConsoleHelper.TruncateToLineWidth(string.Empty));
                    ConsoleHelper.RenderConsoleProgress(0);
                }

                this.visible = true;
            }
        }

        /// <summary>
        /// Updates the progress bar.
        /// </summary>
        private void UpdateProgress()
        {
            lock (this.syncRoot)
            {
                if (!this.visible)
                {
                    return;
                }

                int total = (int)((double)this.currentEntries / this.totalEntries * 100.0);

                // Move cursor to the end of the line with progress bar.
                Console.CursorLeft = ConsoleHelper.WindowWidth - 1;

                using (ConsoleHelper.SetConsoleState(this.consoleState))
                {
                    Console.CursorTop = this.consoleState.CursorTop + 1;
                    ConsoleHelper.RenderConsoleProgress(total > 100 ? 100 : total);
                }
            }
        }

        /// <summary>
        /// Sets the new title for the progress bar.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "The exact exception is unknown in this case.")]
        private void UpdateTitle()
        {
            lock (this.syncRoot)
            {
                if (!this.visible)
                {
                    return;
                }

                using (ConsoleHelper.SetConsoleState(this.consoleState))
                {
                    string compactName = this.currentStepName;

                    try
                    {
                        // We want to compact path, if it's given as a step name.
                        if (LongPathCommon.Exists(this.currentStepName))
                        {
                            compactName = PathHelper.CompactPath(LongPathCommon.RemoveLongPathPrefix(LongPathCommon.NormalizePath(this.currentStepName)), ConsoleHelper.WindowWidth - 2);
                        }
                    }
                    catch
                    {
                        // Ignore.
                    }

                    Console.Out.WriteLine(ConsoleHelper.TruncateToLineWidth(" " + compactName));
                }
            }
        }

        /// <summary>
        /// Hides the progress bar.
        /// </summary>
        private void HideProgressBar()
        {
            lock (this.syncRoot)
            {
                if (!this.visible)
                {
                    return;
                }

                using (ConsoleHelper.SetConsoleState(this.consoleState))
                {
                    // Kepp title and remove the progress bar.
                    Console.Out.WriteLine(string.Empty);
                    Console.Out.Write(ConsoleHelper.TruncateToLineWidth(string.Empty));
                }

                // This will set cursor to a position where the progress was originally displayed.
                // Yes, this will overwrite any text that was added after the progress,
                // but it's a bad idea to do that.
                Console.CursorTop  = this.consoleState.CursorTop + 1;
                Console.CursorLeft = 0;

                this.visible = false;
            }
        }

        #endregion // Private methods

        #region Private classes

        /// <summary>
        /// Dummy implementation of the <see cref="IConsoleProgressReporter"/> that is used, if the
        /// console window is not available.
        /// </summary>
        private sealed class DummyProgressReporter : IConsoleProgressReporter
        {
            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                // Do nothing.
            }

            /// <summary>
            /// Advances progress to the next step.
            /// </summary>
            /// <param name="stepName">Step name. If it's not equal to the previously used name, progress will move to the next line keeping the previous step visible.</param>
            public void StartNewStep(string stepName)
            {
                // Do nothing.
            }

            /// <summary>
            /// Moves the current progress to the end of the current step.
            /// </summary>
            public void FinishCurrentStep()
            {
                // Do nothing.
            }

            /// <summary>
            /// Advances the current progress in accordance with the <paramref name="entries"/> amount given.
            /// </summary>
            /// <param name="entries">Amount of entries finished.</param>
            public void AddCompletedEntries(int entries)
            {
                // Do nothing.
            }
        }

        #endregion // Private classes
    }
}
