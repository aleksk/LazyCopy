// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TaskHelper.cs">
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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains helper methods to work with tasks.
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>
        /// Waits for any of the <paramref name="tasks"/> given to successfully complete.
        /// </summary>
        /// <typeparam name="T">Task return value type.</typeparam>
        /// <param name="tasks">Collection of tasks to wait for.</param>
        /// <returns>Value returned by the first successfully completed task.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">All tasks faulted or cancelled.</exception>
        public static T GetFirstResult<T>(Task<T>[] tasks)
        {
            return TaskHelper.GetFirstResult(tasks, CancellationToken.None);
        }

        /// <summary>
        /// Waits for any of the <paramref name="tasks"/> given to successfully complete
        /// and returns its result.
        /// </summary>
        /// <typeparam name="T">Task return value type.</typeparam>
        /// <param name="tasks">Collection of tasks to wait for.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Value returned by the first successfully completed task.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tasks"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">All tasks faulted or cancelled.</exception>
        public static T GetFirstResult<T>(Task<T>[] tasks, CancellationToken token)
        {
            if (tasks == null || tasks.Length == 0)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            List<Task<T>> tasksLeft = tasks.ToList();
            T firstResult;

            while (true)
            {
                int index = Task.WaitAny(tasksLeft.ToArray(), cancellationToken: token);

                if (!tasksLeft[index].IsFaulted && !tasksLeft[index].IsCanceled)
                {
                    firstResult = tasksLeft[index].Result;
                    break;
                }

                tasksLeft.RemoveAt(index);
                if (!tasksLeft.Any())
                {
                    throw new InvalidOperationException("None of the tasks successfully completed.", tasks.Where(t => t.Exception != null).Select(t => t.Exception).FirstOrDefault());
                }
            }

            return firstResult;
        }
    }
}
