// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RetryHelperUnitTests.cs">
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
    using System.Diagnostics;

    using NUnit.Framework;

    /// <summary>
    /// Contains unit tests for the <see cref="RetryHelper"/> class.
    /// </summary>
    [TestFixture]
    public class RetryHelperUnitTests
    {
        /// <summary>
        /// Validates that the <see cref="RetryHelper.Retry(Action,int,TimeSpan)"/> throws correct exceptions,
        /// if it's invoked with invalid parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidateRetryActionParameters()
        {
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry(null, 0, TimeSpan.FromMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetryHelper.Retry(() => { }, -1, TimeSpan.FromMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetryHelper.Retry(() => { }, 0, TimeSpan.FromMilliseconds(-1)));
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry(() => { }, 0, TimeSpan.Zero, null));
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry(() => { }, 0, TimeSpan.Zero, new Type[0]));
            Assert.Throws<ArgumentException>(() => RetryHelper.Retry(() => { }, 0, TimeSpan.Zero, new[] { typeof(string) }));
        }

        /// <summary>
        /// Validates that the <see cref="RetryHelper.Retry{T}(Func{T},int,TimeSpan)"/> throws correct exceptions,
        /// if it's invoked with invalid parameters.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidateRetryFuncParameters()
        {
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry((Func<bool>)null, 0, TimeSpan.FromMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetryHelper.Retry(() => true, -1, TimeSpan.FromMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetryHelper.Retry(() => true, 0, TimeSpan.FromMilliseconds(-1)));
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry(() => true, 0, TimeSpan.Zero, null));
            Assert.Throws<ArgumentNullException>(() => RetryHelper.Retry(() => true, 0, TimeSpan.Zero, new Type[0]));
            Assert.Throws<ArgumentException>(() => RetryHelper.Retry(() => true, 0, TimeSpan.Zero, new[] { typeof(object) }));
        }

        /// <summary>
        /// Validates that the retry action is executed only once, if it doesn't throw any exceptions.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ExecutedOnlyOnce()
        {
            int executed = 0;
            RetryHelper.Retry(() => { executed++; }, 5, TimeSpan.Zero);
            Assert.That(executed, Is.EqualTo(1), "Method wasn't executed once.");
        }

        /// <summary>
        /// Validates that the retry action is executed even if the retry count is zero.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ExecutedIfRetryCountIsZero()
        {
            int executed = 0;
            RetryHelper.Retry(() => { executed++; }, 0, TimeSpan.Zero);
            Assert.That(executed, Is.EqualTo(1), "Action was not executed.");
        }

        /// <summary>
        /// Validates that the <see cref="RetryHelper.Retry{T}(Func{T},int,TimeSpan)"/> returns the valid method result.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidValueReturned()
        {
            Assert.That(RetryHelper.Retry(() => true, 5, TimeSpan.Zero), Is.EqualTo(true), "Invalid value returned.");
        }

        /// <summary>
        /// Validates that the valid value is returned even if the function throws an exception first time it's executed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "Desired UT behavior.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ValidValueReturnedEvenIfExceptionIsThrownOnce()
        {
            int executed = 0;
            bool exceptionThrown = false;

            bool result = RetryHelper.Retry(
                () =>
                {
                    executed++;

                    if (!exceptionThrown)
                    {
                        exceptionThrown = true;
                        throw new Exception();
                    }

                    return true;
                },
                1,
                TimeSpan.Zero);

            Assert.IsTrue(result, "Valid value returned.");
            Assert.That(executed, Is.EqualTo(2), "Method wasn't executed twice.");
        }

        /// <summary>
        /// Validates that if the method constantly fails, it's executed valid amount of times.
        /// </summary>
        /// <param name="retryCount">Retry count.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Desired UT behavior.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ExecutionCountEqualToRetryCountPlusOne([Random(0, 5, 2)] int retryCount)
        {
            int executed = 0;

            Assert.Throws<InvalidOperationException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        executed++;
                        throw new InvalidOperationException();
                    },
                    retryCount,
                    TimeSpan.Zero));

            Assert.AreEqual(retryCount + 1, executed, "Executed not the same amount of times as requested.");
        }

        /// <summary>
        /// Validates that the <see cref="RetryHelper"/> waits for the specified delay between attempts.
        /// </summary>
        /// <param name="retryMilliseconds">Retry delay in milliseconds.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void WaitBeforeNextAttempt([Random(100, 1000, 3)] int retryMilliseconds)
        {
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(retryMilliseconds);

            Stopwatch stopwatch = new Stopwatch();

            RetryHelper.Retry(
                () =>
                {
                    if (!stopwatch.IsRunning)
                    {
                        stopwatch.Start();
                        throw new InvalidOperationException();
                    }
                },
                2,
                retryDelay);

            Assert.That(stopwatch.Elapsed, Is.GreaterThan(retryDelay));
        }

        /// <summary>
        /// Validates that there is no wait after the failing method is executed for the last time.
        /// </summary>
        /// <param name="retryMilliseconds">Retry delay in milliseconds.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void NoWaitAfterLastAttempt([Random(100, 1000, 3)] int retryMilliseconds)
        {
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(retryMilliseconds);
            Stopwatch stopwatch = new Stopwatch();

            RetryHelper.Retry(stopwatch.Start, 0, retryDelay);

            Assert.That(stopwatch.Elapsed, Is.LessThan(retryDelay));
        }

        /// <summary>
        /// Validates that retry occurs, if the retriable exception is thrown.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ThrowRetriableException()
        {
            int executed = 0;

            Assert.Throws<InvalidOperationException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        executed++;
                        throw new InvalidOperationException();
                    },
                    1,
                    TimeSpan.Zero));

            Assert.AreEqual(2, executed);
            executed = 0;

            Assert.Throws<ArgumentNullException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        executed++;
                        throw new ArgumentNullException(string.Empty);
                    },
                    1,
                    TimeSpan.Zero,
                    new[] { typeof(ArgumentException) }));

            Assert.AreEqual(2, executed);
            executed = 0;

            Assert.Throws<AggregateException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        executed++;
                        throw new AggregateException();
                    },
                    1,
                    TimeSpan.Zero,
                    new[] { typeof(AggregateException) }));

            Assert.AreEqual(2, executed);
        }

        /// <summary>
        /// Validates that retry logic also checks inner exception for the <see cref="AggregateException"/>.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ThrowAggregateException()
        {
            Assert.Throws<AggregateException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        throw new AggregateException("message", new ArgumentNullException(string.Empty));
                    },
                    1,
                    TimeSpan.Zero,
                    new[] { typeof(ArgumentNullException) }));
        }

        /// <summary>
        /// Validates that there is no retries, if non-retriable exception is thrown.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "No need for UT.")]
        [Test]
        public void ThrowNonRetriableException()
        {
            int executed = 0;

            Assert.Throws<InvalidOperationException>(
                () => RetryHelper.Retry(
                    () =>
                    {
                        executed++;
                        throw new InvalidOperationException();
                    },
                    1,
                    TimeSpan.Zero,
                    new[] { typeof(ArgumentNullException) }));

            Assert.AreEqual(1, executed);
        }
    }
}
