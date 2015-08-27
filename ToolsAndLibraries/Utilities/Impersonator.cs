// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Impersonator.cs">
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
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Principal;

    using LazyCopy.Utilities.Native;

    /// <summary>
    /// Contains helper methods for user impersonation.
    /// </summary>
    public static class Impersonator
    {
        #region Fields

        /// <summary>
        /// Default amount of retries.
        /// </summary>
        private const int DefaultRetryCount = 10;

        /// <summary>
        /// Default interval between retries.
        /// </summary>
        private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(100);

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Impersonates the specified user within the <c>Explorer</c> process context.<br/>
        /// <c>Explorer</c> process is vital for the system and should always be there, if the user is logged in.
        /// </summary>
        /// <param name="domainUser">The domain user.</param>
        /// <returns>
        /// Impersonated context.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="domainUser"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="domainUser"/> is not in the <c>DOMAIN\username</c> format.</exception>
        /// <exception cref="InvalidOperationException">
        /// No processes are running for the <paramref name="domainUser"/>.
        ///     <para>-or-</para>
        /// Process handle cannot be duplicated.
        /// </exception>
        /// <seealso cref="Impersonate(string,Predicate{Process})"/>
        public static WindowsImpersonationContext Impersonate(string domainUser)
        {
            return Impersonator.Impersonate(domainUser, p => p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Impersonates the specified user.
        /// </summary>
        /// <param name="domainUser">The domain user.</param>
        /// <param name="processFilter">Predicate to find the process suitable for impersonation. If it's <see langword="null"/>, the first <paramref name="domainUser"/>'s process will be used.</param>
        /// <returns>
        /// Impersonated context.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="domainUser"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="domainUser"/> is not in the <c>DOMAIN\username</c> format.</exception>
        /// <exception cref="InvalidOperationException">
        /// No processes are running for the <paramref name="domainUser"/>.
        ///     <para>-or-</para>
        /// Process handle cannot be duplicated.
        /// </exception>
        /// <remarks>
        /// This method looks for the existing <paramref name="domainUser"/> process running in the system, obtains and duplicates
        /// its token, and uses it to impersonate the caller. So it won't work, if the user is logged out or has no processes running.
        /// </remarks>
        public static WindowsImpersonationContext Impersonate(string domainUser, Predicate<Process> processFilter)
        {
            if (string.IsNullOrEmpty(domainUser))
            {
                throw new ArgumentNullException(nameof(domainUser));
            }

            // Process returned by the ProcessHelper.FindUserProcesses may already be closed, so we want to retry.
            return RetryHelper.Retry(
                () =>
                {
                    using (Process process = ProcessHelper.FindUserProcesses(domainUser).FirstOrDefault(p => processFilter == null || processFilter(p)))
                    {
                        if (process == null)
                        {
                            throw new InvalidOperationException("No suitable user processes found.");
                        }

                        return WindowsIdentity.Impersonate(Impersonator.DuplicateProcessHandle(process));
                    }
                },
                Impersonator.DefaultRetryCount,
                Impersonator.DefaultRetryDelay,
                new[] { typeof(InvalidOperationException) });
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Duplicates handle for the <paramref name="process"/> given.
        /// </summary>
        /// <param name="process">Process to duplicate handle for.</param>
        /// <returns>Duplicated process handle.</returns>
        /// <exception cref="InvalidOperationException">Process handle cannot be duplicated.</exception>
        private static IntPtr DuplicateProcessHandle(Process process)
        {
            SafeTokenHandle tokenHandle;
            if (!NativeMethods.OpenProcessToken(process.Handle, TokenAccessRights.TokenAllAccess, out tokenHandle))
            {
                int hr = Marshal.GetHRForLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to open process token: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
            }

            using (tokenHandle)
            {
                IntPtr duplicatedTokenHandle;

                if (!NativeMethods.DuplicateToken(tokenHandle, SecurityImpersonationLevel.SecurityDelegation, out duplicatedTokenHandle))
                {
                    int hr = Marshal.GetHRForLastWin32Error();
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to duplicate token handle: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
                }

                return duplicatedTokenHandle;
            }
        }

        #endregion // Private methods
    }
}
