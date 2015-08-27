// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EnumHelper.cs">
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
    /// Contains helper methods for the <see cref="Enum"/> class.
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// Determines whether the <paramref name="enumType"/> has a <see cref="FlagsAttribute"/> attribute.
        /// </summary>
        /// <param name="enumType">An enumeration type.</param>
        /// <returns>
        /// <see langword="true"/> if the <paramref name="enumType"/> contains the <see cref="FlagsAttribute"/> attribute; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="enumType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="enumType"/> is not an <see cref="Enum"/>.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags", Justification = "'Flags' term is desired.")]
        public static bool IsFlagsEnum(Type enumType)
        {
            if (enumType == null)
            {
                throw new ArgumentNullException(nameof(enumType));
            }

            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Parameter is not an Enum.", nameof(enumType));
            }

            return enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
        }
    }
}
