// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CollectionHelper.cs">
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
    using System.Linq;

    /// <summary>
    /// Contains helper methods for collections.
    /// </summary>
    public static class CollectionHelper
    {
        /// <summary>
        /// Shuffles the source collection given.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">Source collection to shuffle.</param>
        /// <param name="rand">Random generator.</param>
        /// <returns>Shuffled collection.</returns>
        public static IEnumerable<T> Shuffle<T>(IEnumerable<T> source, Random rand)
        {
            T[] elements = source.ToArray();

            for (int i = elements.Length - 1; i >= 0; i--)
            {
                int swapIndex = rand.Next(i + 1);
                yield return elements[swapIndex];

                elements[swapIndex] = elements[i];
            }
        }
    }
}
