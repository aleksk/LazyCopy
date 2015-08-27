// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FilterInfo.cs">
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
    /// Contains basic information about the filter driver.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Equality comparison is not needed for the current structure.")]
    public struct FilterInfo
    {
        /// <summary>
        /// Gets the filter name.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the filter altitude.
        /// </summary>
        public int Altitude { get; internal set; }

        /// <summary>
        /// Gets the amount of instances for non-legacy filter.
        /// </summary>
        public int? Instances { get; internal set; }

        /// <summary>
        /// Gets the frame ID for non-legacy filter.
        /// </summary>
        public int? FrameId { get; internal set; }
    }
}
