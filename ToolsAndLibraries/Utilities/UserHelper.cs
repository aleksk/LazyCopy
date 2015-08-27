// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UserHelper.cs">
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
    using System.Globalization;
    using System.Linq;
    using System.Management;
    using System.Runtime.InteropServices;
    using System.Security.Principal;

    using LazyCopy.Utilities.Native;

    /// <summary>
    /// Contains helper functions to work with the system users.
    /// </summary>
    public static class UserHelper
    {
        #region Fields

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private static readonly object syncRoot = new object();

        /// <summary>
        /// Contains the name of the currently logged on user.
        /// </summary>
        private static string loggedOnUser;

        #endregion // Fields

        #region Properties

        /// <summary>
        /// Gets the currently logged on user.
        /// </summary>
        public static string LoggedOnUser
        {
            get
            {
                if (UserHelper.loggedOnUser == null)
                {
                    lock (UserHelper.syncRoot)
                    {
                        if (UserHelper.loggedOnUser == null)
                        {
                            UserHelper.loggedOnUser = UserHelper.GetCurrentUser();
                        }
                    }
                }

                return UserHelper.loggedOnUser;
            }

            private set
            {
                lock (UserHelper.syncRoot)
                {
                    UserHelper.loggedOnUser = value;
                }
            }
        }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Impersonates the currently logged on user in the current thread context.
        /// </summary>
        /// <returns>Impersonation context.</returns>
        public static WindowsImpersonationContext ImpersonateCurrentUser()
        {
            try
            {
                return Impersonator.Impersonate(UserHelper.LoggedOnUser);
            }
            catch
            {
                // Exception might occur, if we have outdated username information.
                // Maybe, that user has logged out. So, try to find a new username.
                string previousUserName = UserHelper.LoggedOnUser;
                string newUserName      = UserHelper.GetCurrentUser();

                // If username is the same, don't retry.
                if (string.Equals(previousUserName, newUserName, StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }

                UserHelper.LoggedOnUser = newUserName;
            }

            return Impersonator.Impersonate(UserHelper.LoggedOnUser);
        }

        /// <summary>
        /// Gets the amount of time the user is idle.
        /// </summary>
        /// <returns>User idle time.</returns>
        /// <exception cref="InvalidOperationException">Unable to get the last input time.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This is a helper method, property is not needed here.")]
        public static TimeSpan GetIdleTime()
        {
            return DateTime.UtcNow - UserHelper.GetLastInputTime();
        }

        /// <summary>
        /// Gets the time of the last user input event.
        /// </summary>
        /// <returns>Time of the last user input in the UTC format.</returns>
        /// <exception cref="InvalidOperationException">Unable to get the last input time.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This is a helper method, property is not needed here.")]
        public static DateTime GetLastInputTime()
        {
            LastInputInfo inputInfo = new LastInputInfo { Size = (uint)Marshal.SizeOf(typeof(LastInputInfo)) };
            if (!NativeMethods.GetLastInputInfo(ref inputInfo))
            {
                int error = Marshal.GetHRForLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get the last input time: 0x{0:X8}", error));
            }

            return DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount - inputInfo.Time);
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Gets the name of the currently logged on user.
        /// </summary>
        /// <returns>Name of the currently logged on user.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This is a helper method, property is not needed here.")]
        private static string GetCurrentUser()
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem"))
            {
                ManagementObjectCollection collection = searcher.Get();
                return (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
            }
        }

        #endregion // Private methods
    }
}
