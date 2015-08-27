// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DriverConfiguration.cs">
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

namespace LazyCopy.Service
{
    using System;
    using System.Collections.Generic;

    using LazyCopy.DriverClient;
    using LazyCopy.Service.Properties;

    /// <summary>
    /// Contains configuration parameters for the <c>LazyCopyDriver</c>.
    /// </summary>
    public class DriverConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DriverConfiguration"/> class.
        /// </summary>
        public DriverConfiguration()
        {
            this.WatchPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.ReportRate = Settings.Default.ReportRate;
        }

        /// <summary>
        /// Gets the collection of path this driver should watch.
        /// </summary>
        public HashSet<string> WatchPaths { get; private set; }

        /// <summary>
        /// Gets or sets the watch report rate.
        /// </summary>
        public int ReportRate { get; set; }

        /// <summary>
        /// Gets or sets the driver operation mode.
        /// </summary>
        public OperationMode OperationMode { get; set; }
    }
}
