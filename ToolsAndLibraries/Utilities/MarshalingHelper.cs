// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MarshalingHelper.cs">
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
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    using LazyCopy.Utilities.Native;

    /// <summary>
    /// Contains helper methods for objects marshaling.
    /// </summary>
    public static class MarshalingHelper
    {
        #region Public methods

        /// <summary>
        /// Marshals the <paramref name="data"/> byte array into a structure of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of the structure expected.</typeparam>
        /// <param name="data">Byte array to be marshaled.</param>
        /// <returns>Result structure, if <paramref name="data"/> is not <see langword="null"/> or empty. Otherwise, the default <typeparamref name="T"/> value.</returns>
        /// <exception cref="ArgumentException"><typeparamref name="T"/> type is not a structure.</exception>
        public static T ByteArrayToStructure<T>(byte[] data)
            where T : struct
        {
            Type type = typeof(T);

            if (type.IsPrimitive)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Type {0} is not a structure.", type));
            }

            if (data == null || !data.Any())
            {
                return default(T);
            }

            // Get handle for accessing the pointer to the data array.
            // In this case we don't need to allocate memory with the 'Marshal.AllocHGlobal'
            // and copy the data there.
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Marshals the <paramref name="values"/> given (<i>ignoring the <see langword="null"/> objects</i>) to the <paramref name="destination"/>.
        /// </summary>
        /// <param name="destination">The pointer to marshal the <paramref name="values"/> array to.</param>
        /// <param name="destinationSize">Size of the buffer the <paramref name="destination"/> points to.</param>
        /// <param name="values">The values to be marshaled.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="destination"/> is <see cref="IntPtr.Zero"/>.
        ///     <para>-or-</para>
        /// <paramref name="values"/> array is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentException">Total size of the <paramref name="values"/> objects is zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationSize"/> is not large enough to store all <paramref name="values"/>.</exception>
        public static void MarshalObjectsToPointer(IntPtr destination, int destinationSize, params object[] values)
        {
            if (destination == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (values == null || !values.Any())
            {
                throw new ArgumentNullException(nameof(values));
            }

            // Calculate the object sizes and save it for future use.
            // This will also validate the 'values' array elements.
            List<int> valueSizes = values.Select(MarshalingHelper.GetObjectSize).ToList();

            // Calculate the total size of the values given.
            int totalSize = valueSizes.Sum();
            if (totalSize <= 0)
            {
                throw new ArgumentException("The total size of the values given is zero.");
            }

            if (destinationSize < totalSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destinationSize),
                    string.Format(CultureInfo.InvariantCulture, "Buffer is too small ({0} bytes) to hold the values given, it should be at least {1} bytes.", destinationSize, totalSize));
            }

            NativeMethods.ZeroMemory(destination, (uint)destinationSize);

            // Marshal each value (skipping 'null') to the destination pointer.
            int currentValueIndex = 0;
            IntPtr currentPointer = destination;

            foreach (object value in values)
            {
                if (value != null)
                {
                    MarshalingHelper.MarshalObjectToPointer(value, currentPointer);
                    currentPointer += valueSizes[currentValueIndex];
                }

                currentValueIndex++;
            }
        }

        /// <summary>
        /// This method marshals the <paramref name="value"/> given to the pointer depending on its type.<br/>
        /// The arrays of value type objects are also supported by this method.
        /// </summary>
        /// <param name="value">Object to be marshaled. May be an array of value type objects.</param>
        /// <param name="destination">The pointer to marshal the <paramref name="value"/> object to.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="destination"/> is <see cref="IntPtr.Zero"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException"><paramref name="value"/> cannot be marshaled.</exception>
        /// <exception cref="NotSupportedException"><paramref name="value"/> (or one of its elements, if it's a collection) is not supported.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily", Justification = "Fixing this warning will make code less readable.")]
        public static void MarshalObjectToPointer(object value, IntPtr destination)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (destination == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            Type valueType = value.GetType();

            // If a structure or a primitive was passed, marshal it and exit.
            if (valueType.IsValueType)
            {
                if (valueType.IsPrimitive)
                {
                    MarshalingHelper.MarshalPrimitiveToPointer(value, destination);
                }
                else
                {
                    Marshal.StructureToPtr(value, destination, true);
                }

                return;
            }

            // If the value passed is not a structure and a primitive type,
            // check, whether it's an enumerable collection we can marshal.
            if (!(value is IEnumerable))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type {0} cannot be marshaled.", value.GetType()));
            }

            //
            // We've found out that the value passed is an enumerable collection.
            //

            if (value is Array && ((Array)value).Length == 0)
            {
                // We don't want to do anything with empty arrays, so just exit.
                return;
            }

            // First, we want to use the pre-defined marshaling methods for arrays of primitives.
            if (value is byte[])
            {
                Marshal.Copy((byte[])value, 0, destination, ((byte[])value).Length);
            }
            else if (value is char[])
            {
                Marshal.Copy((char[])value, 0, destination, ((char[])value).Length);
            }
            else if (value is short[])
            {
                Marshal.Copy((short[])value, 0, destination, ((short[])value).Length);
            }
            else if (value is int[])
            {
                Marshal.Copy((int[])value, 0, destination, ((int[])value).Length);
            }
            else if (value is long[])
            {
                Marshal.Copy((long[])value, 0, destination, ((long[])value).Length);
            }
            else if (value is float[])
            {
                Marshal.Copy((float[])value, 0, destination, ((float[])value).Length);
            }
            else if (value is double[])
            {
                Marshal.Copy((double[])value, 0, destination, ((double[])value).Length);
            }
            else if (value is string)
            {
                byte[] stringAsByteArray = Encoding.Unicode.GetBytes((string)value + '\0');
                Marshal.Copy(stringAsByteArray, 0, destination, stringAsByteArray.Length);
            }
            else
            {
                // It wasn't collection of primitives that we received, iterate over it and try to marshal each element.
                IntPtr currentPointer = destination;
                foreach (object element in (IEnumerable)value)
                {
                    // Recursively call this method to marshal the element in the collection.
                    MarshalingHelper.MarshalObjectToPointer(element, currentPointer);

                    // And don't forget to move the current pointer to a new location,
                    // so the next element won't overwrite the marshaled data.
                    currentPointer += MarshalingHelper.GetObjectSize(element);
                }
            }
        }

        /// <summary>
        /// Gets the size of the <paramref name="value"/> object, in bytes.
        /// </summary>
        /// <param name="value">Object to get the size of.</param>
        /// <returns>The <paramref name="value"/> size, if it's not <see langword="null"/>; otherwise, <c>0</c>.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> type is not supported.</exception>
        /// <remarks>
        /// This method is called recursively, if the <paramref name="value"/> is a collection.
        /// </remarks>
        public static int GetObjectSize(object value)
        {
            if (value == null)
            {
                return 0;
            }

            Type type = value.GetType();
            if (type.IsValueType)
            {
                return Marshal.SizeOf(value);
            }

            string strValue = value as string;
            if (strValue != null)
            {
                return Encoding.Unicode.GetByteCount(strValue + '\0');
            }

            // If the value given is an enumerable collection, call this method recursively.
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                return enumerable.Cast<object>().Sum(element => MarshalingHelper.GetObjectSize(element));
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Type {0} is not a collection or a value type.", type));
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Marshals the <paramref name="primitiveValue"/> primitive to the <paramref name="pointer"/>.
        /// </summary>
        /// <param name="primitiveValue">Primitive value to be marshaled.</param>
        /// <param name="pointer">Pointer to marshal the <paramref name="primitiveValue"/> to.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="primitiveValue"/> is <see langword="null"/>.
        ///     <para>-or-</para>
        /// <paramref name="pointer"/> is <see cref="IntPtr.Zero"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="primitiveValue"/> is not a primitive value.</exception>
        /// <exception cref="NotSupportedException"><paramref name="primitiveValue"/>'s type is not supported.</exception>
        private static void MarshalPrimitiveToPointer(object primitiveValue, IntPtr pointer)
        {
            if (primitiveValue == null)
            {
                throw new ArgumentNullException(nameof(primitiveValue));
            }

            if (pointer == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(pointer));
            }

            Type valueType = primitiveValue.GetType();
            if (!valueType.IsPrimitive)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Value type is not a primitive: {0}", valueType));
            }

            if (primitiveValue is byte)
            {
                Marshal.WriteByte(pointer, (byte)primitiveValue);
            }
            else if (primitiveValue is char)
            {
                Marshal.WriteInt16(pointer, (char)primitiveValue);
            }
            else if (primitiveValue is short)
            {
                Marshal.WriteInt16(pointer, (short)primitiveValue);
            }
            else if (primitiveValue is int)
            {
                Marshal.WriteInt32(pointer, (int)primitiveValue);
            }
            else if (primitiveValue is long)
            {
                Marshal.WriteInt64(pointer, (long)primitiveValue);
            }
            else if (primitiveValue is float)
            {
                Marshal.Copy(new[] { (float)primitiveValue }, 0, pointer, 1);
            }
            else if (primitiveValue is double)
            {
                Marshal.Copy(new[] { (double)primitiveValue }, 0, pointer, 1);
            }
            else
            {
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Value type {0} is not supported.", valueType));
            }
        }

        #endregion // Private methods
    }
}
