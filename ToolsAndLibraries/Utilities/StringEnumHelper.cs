// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StringEnumHelper.cs">
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
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;

    /// <summary>
    /// Contains helper methods for the <see cref="Enum"/> class that contains values marked
    /// with the <see cref="StringValueAttribute"/> or <see cref="EnumMemberAttribute"/> attributes.
    /// </summary>
    public static class StringEnumHelper
    {
        #region Fields

        /// <summary>
        /// Cached string attribute values.
        /// </summary>
        private static readonly IDictionary<Enum, string> CachedStringValues = new Dictionary<Enum, string>();

        /// <summary>
        /// Cached parsed enumeration values.
        /// </summary>
        private static readonly IDictionary<Type, IDictionary<string, object>> CachedParsedValues = new Dictionary<Type, IDictionary<string, object>>();

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Converts the <paramref name="value"/> enumeration value given to a string.
        /// </summary>
        /// <param name="value">Enumeration value.</param>
        /// <returns>Enumeration field name or the value of the <see cref="StringValueAttribute"/> or <see cref="EnumMemberAttribute"/> attribute, if the enumeration field has it.</returns>
        /// <remarks>If the flags enumeration is passed to the method, all its flags will be comma-separated.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        public static string GetStringValue(Enum value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            // Return cached value, if any.
            if (StringEnumHelper.CachedStringValues.ContainsKey(value))
            {
                return StringEnumHelper.CachedStringValues[value];
            }

            Type enumType = value.GetType();
            string result;

            if (!EnumHelper.IsFlagsEnum(enumType))
            {
                result = StringEnumHelper.GetStringAttributeValue(enumType.GetField(value.ToString()));
            }
            else
            {
                StringBuilder builder = new StringBuilder();

                int intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);

                bool isFirstItem = true;
                foreach (FieldInfo fieldInfo in enumType.GetFields().Where(field => field.IsLiteral))
                {
                    Enum fieldValue   = (Enum)fieldInfo.GetValue(enumType);
                    int intFieldValue = Convert.ToInt32(fieldValue, CultureInfo.InvariantCulture);

                    // Check for the default enum value.
                    if (intFieldValue == 0)
                    {
                        // If we've found the default enum value and it's the one we're looking for.
                        if (intValue == 0)
                        {
                            builder.Append(StringEnumHelper.GetStringAttributeValue(fieldInfo));
                            break;
                        }
                    }
                    else if (value.HasFlag(fieldValue))
                    {
                        if (!isFirstItem)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(StringEnumHelper.GetStringAttributeValue(fieldInfo));
                        isFirstItem = false;
                    }
                }

                result = builder.Length > 0 ? builder.ToString() : string.Empty;
            }

            // Cache the value found.
            StringEnumHelper.CachedStringValues.Add(value, result);

            return result;
        }

        /// <summary>
        /// Converts the string representation of the name to an equivalent enumerated object.
        /// </summary>
        /// <param name="enumType">An enumeration type.</param>
        /// <param name="value">A string containing the name or value to convert.</param>
        /// <param name="ignoreCase"><see langword="true"/> to ignore case; <see langword="false"/> to regard case.</param>
        /// <returns>An object of type <paramref name="enumType"/> whose value is represented by <paramref name="value"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="enumType"/> is not a flags enumeration and the <paramref name="value"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="enumType"/> is a flags enumeration and a <paramref name="value"/> part cannot be parsed.</exception>
        public static object ParseStringValue(Type enumType, string value, bool ignoreCase)
        {
            if (enumType == null)
            {
                throw new ArgumentNullException(nameof(enumType));
            }

            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Parameter is not an Enum.", nameof(enumType));
            }

            bool isFlags = EnumHelper.IsFlagsEnum(enumType);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (isFlags)
                {
                    return 0;
                }

                throw new ArgumentNullException(nameof(value));
            }

            // Return cached value, if any.
            if (StringEnumHelper.CachedParsedValues.ContainsKey(enumType) && StringEnumHelper.CachedParsedValues[enumType].ContainsKey(value))
            {
                return StringEnumHelper.CachedParsedValues[enumType][value];
            }

            object result;

            if (!isFlags)
            {
                result = StringEnumHelper.GetEnumValueForString(enumType, value, ignoreCase);
            }
            else 
            {
                int flagsValue = 0;

                // Split the source string, parse each fragment and add the parsed enumeration value to the 'flagsValue'.
                foreach (string stringValuePart in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    object enumValue = StringEnumHelper.GetEnumValueForString(enumType, stringValuePart.Trim(), ignoreCase);
                    if (enumValue == null)
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The value '{0}' cannot be found within the {1}", stringValuePart, enumType), value);
                    }

                    flagsValue |= (int)enumValue;
                }

                result = flagsValue;
            }

            // Cache the result value.
            if (result != null)
            {
                if (!StringEnumHelper.CachedParsedValues.ContainsKey(enumType))
                {
                    StringEnumHelper.CachedParsedValues.Add(enumType, new Dictionary<string, object>());
                }

                StringEnumHelper.CachedParsedValues[enumType].Add(value, result);
            }

            return result;
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Converts the string representation of the name to an equivalent enumerated object.
        /// </summary>
        /// <param name="enumType">An enumeration type.</param>
        /// <param name="stringValue">A string containing the name or value to convert.</param>
        /// <param name="ignoreCase"><see langword="true"/> to ignore case; <see langword="false"/> to regard case.</param>
        /// <returns>An object of type <paramref name="enumType"/> whose value is represented by <paramref name="stringValue"/>.</returns>
        private static object GetEnumValueForString(Type enumType, string stringValue, bool ignoreCase)
        {
            StringComparison stringComparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            foreach (FieldInfo fieldInfo in enumType.GetFields().Where(field => field.IsLiteral))
            {
                string fieldValue = StringEnumHelper.GetStringAttributeValue(fieldInfo);

                if (string.Equals(fieldValue, stringValue, stringComparison) || string.Equals(fieldInfo.Name, stringValue, stringComparison))
                {
                    return Enum.Parse(enumType, fieldInfo.Name);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the <see cref="StringValueAttribute.Value"/> or the <see cref="EnumMemberAttribute"/> attribute property from the <paramref name="fieldInfo"/> given.
        /// </summary>
        /// <param name="fieldInfo">Field to get the attribute value from.</param>
        /// <returns>
        /// The <see cref="StringValueAttribute.Value"/>, if the field contains the <see cref="StringValueAttribute"/>.
        /// The <see cref="EnumMemberAttribute.Value"/>, if the field contains the <see cref="EnumMemberAttribute"/>.
        /// Otherwise, field name.
        /// </returns>
        private static string GetStringAttributeValue(FieldInfo fieldInfo)
        {
            // StringValueAttribute has a higher priority.
            StringValueAttribute[] stringValueAttributes = (StringValueAttribute[])fieldInfo.GetCustomAttributes(typeof(StringValueAttribute), false);
            if (stringValueAttributes.Length > 0)
            {
                return stringValueAttributes[0].Value;
            }

            EnumMemberAttribute[] enumMemberAttributes = (EnumMemberAttribute[])fieldInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false);
            if (enumMemberAttributes.Length > 0)
            {
                return enumMemberAttributes[0].Value;
            }

            return fieldInfo.Name;
        }

        #endregion // Private methods
    }
}
