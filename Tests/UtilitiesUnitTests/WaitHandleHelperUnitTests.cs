// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WaitHandleHelperUnitTests.cs">
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
    using System.Threading.Tasks;

    using NUnit.Framework;

    /// <summary>
    /// Contains unit tests for the <see cref="WaitHandleHelper"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "No need for UTs.")]
    [TestFixture]
    public class WaitHandleHelperUnitTests
    {
        /// <summary>
        /// Mutex to be used in the tests.
        /// </summary>
        private Mutex mutex;

        /// <summary>
        /// Creates a new mutex to be used in the test.
        /// </summary>
        [SetUp]
        public void Initialize()
        {
            this.mutex = new Mutex(false, "UT_mutex_" + new Random().Next(0, 1000000));
        }

        /// <summary>
        /// Tries to release mutex.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            try
            {
                this.mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Do nothing.
            }

            this.mutex.Dispose();
        }

        /// <summary>
        /// Validates that the acquire methods throw correct exceptions, if they're invoked with invalid parameters.
        /// </summary>
        [Test]
        public void ValidateAcquireArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.Acquire(null, TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.Acquire(null, new TimeoutHelper(TimeSpan.Zero)));
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.Acquire(this.mutex, null));

            Assert.Throws<ArgumentOutOfRangeException>(() => WaitHandleHelper.Acquire(this.mutex, TimeSpan.FromMilliseconds(-2)));
        }

        /// <summary>
        /// Validates that the acquire methods throw correct exceptions, if the timeout expires.
        /// </summary>
        [Test]
        public void TimeoutExpires()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.FromMilliseconds(1));
            Thread.Sleep(10);
            Assert.IsTrue(helper.IsExpired);

            Assert.Throws<TimeoutException>(() => WaitHandleHelper.Acquire(this.mutex, helper));

            // Mutex should not be acquired in the previous step.
            Assert.Throws<ApplicationException>(() => this.mutex.ReleaseMutex());

            ManualResetEvent taskStarted = new ManualResetEvent(false);
            Task.Run(() =>
            {
                Assert.IsTrue(this.mutex.WaitOne(0));
                taskStarted.Set();
            });

            Assert.IsTrue(taskStarted.WaitOne(TimeSpan.FromSeconds(1)));

            Assert.Throws<TimeoutException>(() => WaitHandleHelper.Acquire(this.mutex, TimeSpan.FromMilliseconds(100)));
            Assert.Throws<TimeoutException>(() => WaitHandleHelper.Acquire(this.mutex, new TimeoutHelper(100)));
        }

        /// <summary>
        /// Verifies that the acquire method can acquire the released mutex.
        /// </summary>
        [Test]
        public void ReleaseMutex()
        {
            ManualResetEvent taskStarted  = new ManualResetEvent(false);
            ManualResetEvent taskFinished = new ManualResetEvent(false);

            Task.Run(() =>
            {
                this.mutex.WaitOne(0);
                taskStarted.Set();
                Thread.Sleep(TimeSpan.FromSeconds(1));
                this.mutex.ReleaseMutex();
            }).ContinueWith(task => taskFinished.Set());

            Assert.IsTrue(taskStarted.WaitOne(TimeSpan.FromSeconds(5)));

            WaitHandleHelper.Acquire(this.mutex, TimeSpan.FromSeconds(5));
            Assert.IsTrue(taskFinished.WaitOne(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// Validates that the <see cref="WaitHandleHelper.TryToAcquire(System.Threading.WaitHandle)"/> method throw correct exceptions, if they're invoked with invalid parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UTs.")]
        [Test]
        public void ValidateTryToAcquireArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.TryToAcquire(null));
        }

        /// <summary>
        /// Validates that it's possible to acquire mutex using the <see cref="WaitHandleHelper.TryToAcquire(System.Threading.WaitHandle)"/> method, if mutex is not held.
        /// </summary>
        [Test]
        public void TryToAcquireWithoutLockHeld()
        {
            Assert.IsTrue(WaitHandleHelper.TryToAcquire(this.mutex));
            this.mutex.ReleaseMutex();
        }

        /// <summary>
        /// Validates that the <see cref="WaitHandleHelper.TryToAcquire(System.Threading.WaitHandle)"/> returns <c>false</c>, if mutex is held.
        /// </summary>
        [Test]
        public void TryToAcquireWithLockHeld()
        {
            WaitHandleHelper.Acquire(this.mutex, TimeSpan.FromSeconds(1));
            ManualResetEvent notAcquired = new ManualResetEvent(false);

            Task.Run(() =>
            {
                Assert.IsFalse(WaitHandleHelper.TryToAcquire(this.mutex));
                notAcquired.Set();
            });

            Assert.IsTrue(notAcquired.WaitOne(TimeSpan.FromSeconds(1)));
        }

        /// <summary>
        /// Validates that the <see cref="WaitHandleHelper.AcquireAll(System.Threading.WaitHandle[],TimeoutHelper)"/> method throws correct exceptions,
        /// it it's invoked with invalid arguments.
        /// </summary>
        [Test]
        public void ValidateAcquireAllArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.AcquireAll(null, TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.AcquireAll(new WaitHandle[0], TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.AcquireAll(new WaitHandle[] { this.mutex }, null));
            Assert.Throws<ArgumentNullException>(() => WaitHandleHelper.AcquireAll(new WaitHandle[] { this.mutex, this.mutex }, null));
        }

        /// <summary>
        /// Verifies that the <see cref="WaitHandleHelper.AcquireAll(WaitHandle[],TimeoutHelper)"/> can acquire all handles.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UTs.")]
        [Test]
        public void AcquireAllSucceeds()
        {
            using (Mutex firstMutex  = new Mutex(false, "UT_firstMutex_1"))
            using (Mutex secondMutex = new Mutex(true,  "UT_secondMutex_1"))
            {
                WaitHandleHelper.AcquireAll(new WaitHandle[] { firstMutex, secondMutex }, TimeSpan.FromSeconds(5));

                firstMutex.ReleaseMutex();
                secondMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Verifies that the <see cref="WaitHandleHelper.AcquireAll(WaitHandle[],TimeoutHelper)"/> releases the acquired handles, if it fails.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UTs.")]
        [Test]
        public void AcquireAllReleasesAcquiredOnFailure()
        {
            using (Mutex firstMutex  = new Mutex(false, "UT_firstMutex_2"))
            using (Mutex secondMutex = new Mutex(false, "UT_secondMutex_2"))
            using (Mutex thirdMutex  = new Mutex(true,  "UT_thirdMutex_2"))
            {
                ManualResetEvent taskStarted = new ManualResetEvent(false);
                Task.Run(() =>
                {
                    firstMutex.WaitOne(0);

                    taskStarted.Set();
                    Thread.Sleep(1000);
                });

                Assert.IsTrue(taskStarted.WaitOne(TimeSpan.FromSeconds(10)));

                Assert.Throws<TimeoutException>(
                    () => WaitHandleHelper.AcquireAll(new WaitHandle[] { secondMutex, thirdMutex, firstMutex }, TimeSpan.FromSeconds(3)),
                    "All mutexes acquired.");

                Assert.Throws<ApplicationException>(secondMutex.ReleaseMutex, "Second mutex is still acquired.");
                Assert.DoesNotThrow(thirdMutex.ReleaseMutex, "Third mutex is not acquired.");
            }
        }
    }
}
