// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TimeoutHelperUnitTests.cs">
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
    using System.Threading;

    using NUnit.Framework;

    /// <summary>
    /// Contains unit tests for the <see cref="TimeoutHelper"/> class.
    /// </summary>
    [TestFixture]
    public class TimeoutHelperUnitTests
    {
        /// <summary>
        /// Validates that the <see cref="System.Threading.TimeoutHelper"/> throws correct exceptions, if it's created with invalid parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "LazyCopy.Utilities.TimeoutHelper", Justification = "Desired UT behavior.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidateConstructorArguments()
        {
            Assert.DoesNotThrow(() => new TimeoutHelper(int.MaxValue));
            Assert.DoesNotThrow(() => new TimeoutHelper(0));
            Assert.DoesNotThrow(() => new TimeoutHelper(-1));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutHelper(-2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutHelper(int.MinValue));

            Assert.DoesNotThrow(() => new TimeoutHelper(TimeSpan.Zero));
            Assert.DoesNotThrow(() => new TimeoutHelper(Timeout.InfiniteTimeSpan));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutHelper(TimeSpan.MaxValue));
        }

        /// <summary>
        /// Verifies that the timeout helper properly updates its properties, if it expires.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void TimespanExpiration()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(1);

            TimeoutHelper helper = new TimeoutHelper(timeout);
            Assert.IsFalse(helper.IsExpired);
            Assert.IsTrue(helper.Elapsed > TimeSpan.Zero);
            Assert.AreEqual(helper.Timeout, timeout);
            Assert.IsNotNull(helper.ToString());

            Thread.Sleep(timeout);

            Assert.IsTrue(helper.IsExpired);
            Assert.IsTrue(helper.Elapsed > timeout);
            Assert.AreEqual(helper.Timeout, timeout);
            Assert.IsNotNull(helper.ToString());
        }

        /// <summary>
        /// Verifies that the correct values are returned for helper created with <see cref="TimeSpan.Zero"/> timeout.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void UseZeroTimespans()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.Zero);
            Assert.AreEqual(helper.IsExpired, false);
            Assert.IsTrue(helper.Elapsed > TimeSpan.Zero);
            Assert.AreEqual(helper.Timeout, TimeSpan.Zero);
            Assert.IsNotNull(helper.ToString());
        }

        /// <summary>
        /// Verifies that the correct values are returned for helper created with <see cref="Timeout.InfiniteTimeSpan"/> timeout.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void UseInfiniteTimespans()
        {
            TimeoutHelper helper = new TimeoutHelper(Timeout.InfiniteTimeSpan);
            Assert.AreEqual(helper.IsExpired, false);
            Assert.IsTrue(helper.Elapsed > TimeSpan.Zero);
            Assert.AreEqual(helper.Timeout, Timeout.InfiniteTimeSpan);
            Assert.IsNotNull(helper.ToString());
        }
    }
}
