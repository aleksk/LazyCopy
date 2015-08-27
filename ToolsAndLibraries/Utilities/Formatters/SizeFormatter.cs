// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SizeFormatter.cs">
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

namespace LazyCopy.Utilities.Formatters
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Formatter for the sizes.
    /// </summary>
    /// <example>
    /// <code>
    /// string.Format(SizeFormatter.Instance, "{0:size}", 10000);    // 9.8 KB
    /// string.Format(SizeFormatter.Instance, "{0:size}", 10000000); // 9.5 MB
    /// </code>
    /// </example>
    public class SizeFormatter : IFormatProvider, ICustomFormatter
    {
        #region Fields

        /// <summary>
        /// Size format string.
        /// </summary>
        private const string SizeFormat = "size";

        /// <summary>
        /// Lazy class instance.
        /// </summary>
        private static readonly Lazy<SizeFormatter> LazyInstance = new Lazy<SizeFormatter>(() => new SizeFormatter());

        /// <summary>
        /// Collection of suffixes for sizes.
        /// </summary>
        private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "PB" };

        #endregion // Fields

        #region Properties

        /// <summary>
        /// Gets the static class instance.
        /// </summary>
        public static SizeFormatter Instance => SizeFormatter.LazyInstance.Value;

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Returns an object that provides formatting services for the specified type.
        /// </summary>
        /// <param name="formatType">An object that specifies the type of format object to return.</param>
        /// <returns>
        /// An instance of the object specified by <paramref name="formatType"/>, if the <see cref="T:System.IFormatProvider"/> implementation can supply that type of object; otherwise, <see langword="null"/>.
        /// </returns>
        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : null;
        }

        /// <summary>
        /// Converts the value of a specified object to an equivalent string representation using specified format and culture-specific formatting information.
        /// </summary>
        /// <param name="format">A format string containing formatting specifications.</param>
        /// <param name="arg">An object to format.</param>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that supplies format information about the current instance.</param>
        /// <returns>
        /// The string representation of the value of <paramref name="arg"/>, formatted as specified by <paramref name="format"/> and <paramref name="formatProvider"/>.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "General catch block is desired to return default format on failure.")]
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (format == null || !format.StartsWith(SizeFormatter.SizeFormat, StringComparison.CurrentCultureIgnoreCase))
            {
                return SizeFormatter.DefaultFormat(format, arg, formatProvider);
            }

            double size;
            double num = 0;
            int place  = 0;

            try
            {
                size = Convert.ToDouble(arg, CultureInfo.CurrentCulture);
            }
            catch
            {
                return SizeFormatter.DefaultFormat(format, arg, formatProvider);
            }

            if (size > 0)
            {
                place = (int)Math.Floor(Math.Log(size, 1024));
                if (place > SizeFormatter.Sizes.Length - 1)
                {
                    place = SizeFormatter.Sizes.Length - 1;
                }

                num = Math.Round(size / Math.Pow(1024, place), 1);
            }

            // Don't include fractional part, if it will be rounded to zero.
            string numStr = num % 1.0 >= 0.05
                            ? string.Format(formatProvider, "{0:N1}", num)
                            : string.Format(formatProvider, "{0:N0}", num);

            return string.Format(formatProvider, "{0} {1}", numStr, SizeFormatter.Sizes[place]);
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Applies the default format for the string given.
        /// </summary>
        /// <param name="format">A format string containing formatting specifications.</param>
        /// <param name="arg">An object to format.</param>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that supplies format information about the current instance.</param>
        /// <returns>
        /// The string representation of the value of <paramref name="arg"/>, formatted as specified by <paramref name="format"/> and <paramref name="formatProvider"/>.
        /// </returns>
        private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
        {
            IFormattable formattableArg = arg as IFormattable;
            return formattableArg?.ToString(format, formatProvider) ?? arg.ToString();
        }

        #endregion // Private methods
    }
}
