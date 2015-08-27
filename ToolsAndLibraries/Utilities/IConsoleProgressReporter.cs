// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConsoleProgressReporter.cs">
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
    /// Defines methods to show the progress bar in a console window.
    /// </summary>
    public interface IConsoleProgressReporter : IDisposable
    {
        /// <summary>
        /// Advances progress to the next step.
        /// </summary>
        /// <param name="stepName">Step name. If it's not equal to the previously used name, progress will move to the next line keeping the previous step visible.</param>
        void StartNewStep(string stepName);

        /// <summary>
        /// Moves the current progress to the end of the current step.
        /// </summary>
        void FinishCurrentStep();

        /// <summary>
        /// Advances the current progress in accordance with the <paramref name="entries"/> amount given.
        /// </summary>
        /// <param name="entries">Amount of entries finished.</param>
        void AddCompletedEntries(int entries);
    }
}
