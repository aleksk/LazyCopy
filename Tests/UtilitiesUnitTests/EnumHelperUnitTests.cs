// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnumHelperUnitTests.cs">
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

    using NUnit.Framework;

    /// <summary>
    /// Contains unit tests for the <see cref="EnumHelper"/> class.
    /// </summary>
    [TestFixture]
    public class EnumHelperUnitTests
    {
        /// <summary>
        /// Enumeration with the <see cref="FlagsAttribute"/> to be used in the tests.
        /// </summary>
        [Flags]
        private enum FlagsEnum
        {
        }

        /// <summary>
        /// Enumeration without the <see cref="FlagsAttribute"/> to be used in the tests.
        /// </summary>
        private enum NonFlagsEnum
        {
        }

        /// <summary>
        /// Validates that the <see cref="EnumHelper.IsFlagsEnum"/> throws correct exceptions,
        /// if invalid parameters are specified.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags", Justification = "'Flags' is preferred.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidateIsFlagsEnumArguments()
        {
            Assert.Throws<ArgumentNullException>(() => EnumHelper.IsFlagsEnum(null));
            Assert.Throws<ArgumentException>(() => EnumHelper.IsFlagsEnum(typeof(object)));
            Assert.Throws<ArgumentException>(() => EnumHelper.IsFlagsEnum(typeof(string)));
            Assert.Throws<ArgumentException>(() => EnumHelper.IsFlagsEnum(typeof(int)));
        }

        /// <summary>
        /// Validates that the <see cref="EnumHelper.IsFlagsEnum"/> returns <see langword="true"/> for enumerations
        /// that have the <see cref="FlagsAttribute"/> set.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags", Justification = "'Flags' is preferred.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void EnumHasFlagsAttribute()
        {
            Assert.IsTrue(EnumHelper.IsFlagsEnum(typeof(FlagsEnum)));
        }

        /// <summary>
        /// Validates that the <see cref="EnumHelper.IsFlagsEnum"/> returns <see langword="false"/> for enumerations
        /// that do not have the <see cref="FlagsAttribute"/> set.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags", Justification = "'Flags' is preferred.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void EnumDoesNotHaveFlagsAttribute()
        {
            Assert.IsFalse(EnumHelper.IsFlagsEnum(typeof(NonFlagsEnum)));
        }
    }
}
