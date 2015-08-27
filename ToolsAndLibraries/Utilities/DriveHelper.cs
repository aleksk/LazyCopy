// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DriveHelper.cs">
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
    using System.IO;
    using System.Linq;
    using System.Security.Principal;
    using System.Threading;

    using LazyCopy.Utilities.Extensions;
    using LazyCopy.Utilities.Native;
    using Microsoft.Win32;

    /// <summary>
    /// Contains helper methods to work with disk drives.
    /// </summary>
    public static class DriveHelper
    {
        #region Fields

        /// <summary>
        /// Name of the mutex used to notify others that the drive is being mapped and registry is to be updated
        /// with the new mapping values.
        /// </summary>
        private const string DriveGlobalMutexName = "__ND_Map__";

        /// <summary>
        /// Drive mutex name format.
        /// </summary>
        private const string DriveMutexNameFormat = "__ND_{0}_{1}_{2}__";

        /// <summary>
        /// Path to the registry key where mutex name to drive letter mappings are stored.
        /// </summary>
        private const string RegistryKeyName = @"Software\Microsoft\NetworkDrives";

        /// <summary>
        /// List of drive mutexes.
        /// These mutexes, when abandoned or closed, will allow other application instances
        /// to determnine whether the drive 'owned' by the mutex, is abandoned and should be removed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Mutexes spelling is correct.")]
        private static readonly IDictionary<string, Mutex> DriveMutexes = new Dictionary<string, Mutex>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Default timeout for mutex acquisition operations.
        /// </summary>
        private static readonly TimeSpan DefaultMutexTimeout = TimeSpan.FromSeconds(10);

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Gets the next available drive letter.
        /// </summary>
        /// <returns>The next available drive letter. <c>Z:</c>, for example.</returns>
        /// <exception cref="InvalidOperationException">There are no drive letters available.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "A static method is desired here.")]
        public static string GetNextAvailableDriveLetter()
        {
            // Create and acquire mutex, so no one else using the same class will be able to map/unmap this drive.
            using (Mutex globalMutex = new Mutex(false, DriveHelper.DriveGlobalMutexName))
            {
                globalMutex.Acquire(DriveHelper.DefaultMutexTimeout);

                try
                {
                    // Will contain root paths like 'C:\', 'D:\', 'E:\', and etc.
                    string[] driveLetters = Directory.GetLogicalDrives();

                    for (char i = 'Z'; i >= 'A'; i--)
                    {
                        string currentDriveLetter = i.ToString(CultureInfo.InvariantCulture);

                        if (!driveLetters.Any(d => d.StartsWith(currentDriveLetter, StringComparison.OrdinalIgnoreCase)))
                        {
                            return string.Format(CultureInfo.InvariantCulture, @"{0}:", currentDriveLetter);
                        }
                    }
                }
                finally
                {
                    globalMutex.ReleaseMutex();
                }
            }

            throw new InvalidOperationException("There are no drive letters available.");
        }

        /// <summary>
        /// Adds the new network drive.
        /// </summary>
        /// <param name="driveLetter">Drive letter to use.</param>
        /// <param name="remoteShare">Remote share to map.</param>
        /// <param name="userName">Username. May be <see langword="null"/>.</param>
        /// <param name="password">Password. May be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="driveLetter"/> or the <paramref name="remoteShare"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Unable to map a network drive.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Drive mutex is stored in the internal map and will be disposed on application exit or in the DeleteNetworkDrive method.")]
        public static void AddNetworkDrive(string driveLetter, string remoteShare, string userName, string password)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                throw new ArgumentNullException(nameof(driveLetter));
            }

            if (string.IsNullOrEmpty(remoteShare))
            {
                throw new ArgumentNullException(nameof(remoteShare));
            }

            // Trim the trailing directory separator characters.
            driveLetter = driveLetter.TrimEnd('\\');

            NetResource netResource = new NetResource
            {
                Scope       = ResourceScope.GlobalNetwork,
                Type        = ResourceType.Disk,
                DisplayType = ResourceDisplayType.Share,
                LocalName   = driveLetter,
                RemoteName  = remoteShare,
            };

            using (Mutex globalMutex = new Mutex(false, DriveHelper.DriveGlobalMutexName))
            {
                globalMutex.Acquire(DriveHelper.DefaultMutexTimeout);

                try
                {
                    // Map network drive.
                    int hr = NativeMethods.WNetAddConnection2(ref netResource, password, userName, 0);
                    if (hr != NativeMethods.Ok)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to map network drive: '{0}' -> '{1}'. Error: 0x{2:X8}", driveLetter, remoteShare, hr));
                    }

                    try
                    {
                        // Create a new mutex that will mark this drive operational and save its name into the Registry.
                        string mutexName = DriveHelper.GetMutexNameForDrive(driveLetter);

                        Mutex driveMutex = new Mutex(false, mutexName);
                        driveMutex.Acquire(DriveHelper.DefaultMutexTimeout);

                        DriveHelper.SaveDriveMapping(mutexName, driveLetter, driveMutex);
                    }
                    catch (Exception)
                    {
                        // In case of failure, don't forget to delete the mapped drive.
                        DriveHelper.DeleteNetworkDrive(driveLetter);
                        throw;
                    }
                }
                finally
                {
                    globalMutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Deletes the the network drive given.
        /// </summary>
        /// <param name="driveLetter">Drive letter to delete.</param>
        /// <exception cref="ArgumentNullException"><paramref name="driveLetter"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Unable to delete network drive.</exception>
        public static void DeleteNetworkDrive(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                throw new ArgumentNullException(nameof(driveLetter));
            }

            // Trim the trailing directory separator characters.
            driveLetter = driveLetter.TrimEnd('\\');

            using (Mutex globalMutex = new Mutex(false, DriveHelper.DriveGlobalMutexName))
            {
                globalMutex.Acquire(DriveHelper.DefaultMutexTimeout);

                try
                {
                    // Delete network drive.
                    int hr = NativeMethods.WNetCancelConnection2(driveLetter, 0, true);
                    if (hr != NativeMethods.Ok)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to delete network drive: '{0}'. Error: 0x{1:X8}", driveLetter, hr));
                    }

                    // Delete mutex mapping for the current drive and release mutex, if any.
                    string mutexName = DriveHelper.GetMutexNameForDrive(driveLetter);
                    DriveHelper.DeleteDriveMapping(mutexName);
                }
                finally
                {
                    globalMutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Disconnects the abandoned drives previously mapped with this class.
        /// </summary>
        /// <remarks>
        /// This method uses the Registry to get the list of drives mapped by this class, which is maintained by the <see cref="AddNetworkDrive"/>
        /// and <see cref="DeleteNetworkDrive"/> methods.
        /// </remarks>
        public static void DeleteAbandonedDrives()
        {
            for (char i = 'Z'; i >= 'A'; i--)
            {
                string driveLetter = string.Format(CultureInfo.InvariantCulture, "{0}:\\", i);
                string mutexName   = DriveHelper.GetMutexNameForDrive(driveLetter);

                // Check whether this drive has the mutex registered for it.
                string driveMapping = DriveHelper.GetDriveMapping(mutexName);
                if (string.IsNullOrEmpty(driveMapping))
                {
                    continue;
                }

                if (new DriveInfo(i.ToString(CultureInfo.InvariantCulture)).DriveType != DriveType.Network)
                {
                    // Delete registry mapping for the missing drives.
                    DriveHelper.DeleteDriveMapping(mutexName);
                }
                else
                {
                    using (Mutex driveMutex = new Mutex(false, mutexName))
                    {
                        // If drive mutex can be acquired, drive is abandoned and should be deleted.
                        if (driveMutex.TryToAcquire())
                        {
                            DriveHelper.DeleteNetworkDrive(driveLetter);
                        }
                    }
                }
            }
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Saves the drive mutex name to letter mapping into the Registry, so it'll possible for the other applications to
        /// determine whether this drive was mapped with this class.
        /// </summary>
        /// <param name="mutexName">Drive mutex name.</param>
        /// <param name="driveLetter">Drive letter.</param>
        /// <param name="driveMutex">Drive mutex.</param>
        /// <exception cref="InvalidOperationException">Registry with drive mappings cannot be accessed.</exception>
        /// <remarks>
        /// The <paramref name="driveMutex"/> given is added to the <see cref="DriveMutexes"/> map, so the mutex will not
        /// be garbage collected.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "NetworkDrives", Justification = "NetworkDrives spelling is correct.")]
        private static void SaveDriveMapping(string mutexName, string driveLetter, Mutex driveMutex)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(DriveHelper.RegistryKeyName))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to create or open registry key {0}", DriveHelper.RegistryKeyName));
                }

                key.SetValue(mutexName, driveLetter, RegistryValueKind.String);
            }

            lock (DriveHelper.DriveMutexes)
            {
                // Add the mutex to the list, so it won't be abandoned.
                if (DriveHelper.DriveMutexes.ContainsKey(mutexName))
                {
                    try
                    {
                        DriveHelper.DriveMutexes[mutexName].ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Just in case this mutex has been already released.
                    }

                    DriveHelper.DriveMutexes[mutexName].Dispose();
                    DriveHelper.DriveMutexes.Remove(mutexName);
                }

                DriveHelper.DriveMutexes[mutexName] = driveMutex;
            }
        }

        /// <summary>
        /// Deletes the drive mutex name to letter mapping from the Registry, so other applications will not attempt to delete this drive.
        /// </summary>
        /// <param name="mutexName">Drive mutex name.</param>
        /// <exception cref="InvalidOperationException">Registry with drive mappings cannot be accessed.</exception>
        /// <remarks>
        /// The <paramref name="mutexName"/> given is removed from the <see cref="DriveMutexes"/> map, and the according mutex is released.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "NetworkDrives", Justification = "NetworkDrives spelling is correct.")]
        private static void DeleteDriveMapping(string mutexName)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(DriveHelper.RegistryKeyName))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to create or open registry key {0}", DriveHelper.RegistryKeyName));
                }

                key.DeleteValue(mutexName, false);
            }

            lock (DriveHelper.DriveMutexes)
            {
                if (DriveHelper.DriveMutexes.ContainsKey(mutexName))
                {
                    try
                    {
                        DriveHelper.DriveMutexes[mutexName].ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // This mutex may have already been disposed.
                    }

                    DriveHelper.DriveMutexes[mutexName].Dispose();
                    DriveHelper.DriveMutexes.Remove(mutexName);
                }
            }
        }

        /// <summary>
        /// Gets the drive mapping from the Registry for the <paramref name="mutexName"/> given.
        /// </summary>
        /// <param name="mutexName">Drive mutex name to get mapping for.</param>
        /// <returns>Drive letter for the <paramref name="mutexName"/> or <see langword="null"/>, if no mapping exists for it in the Registry.</returns>
        /// <exception cref="InvalidOperationException">Registry with drive mappings cannot be accessed.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "NetworkDrives", Justification = "NetworkDrives spelling is correct.")]
        private static string GetDriveMapping(string mutexName)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(DriveHelper.RegistryKeyName))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to create or open registry key {0}", DriveHelper.RegistryKeyName));
                }

                return key.GetValue(mutexName) as string;
            }
        }

        /// <summary>
        /// Gets the mutex name for the <paramref name="driveLetter"/> given.
        /// </summary>
        /// <param name="driveLetter">Drive letter to get mutex name for.</param>
        /// <returns>Mutex name.</returns>
        private static string GetMutexNameForDrive(string driveLetter)
        {
            return string.Format(CultureInfo.InvariantCulture, DriveHelper.DriveMutexNameFormat, Environment.UserName, driveLetter[0], DriveHelper.IsRunningElevated() ? "1" : "0");
        }

        /// <summary>
        /// Determines whether the application is running with the elevated privileges.
        /// </summary>
        /// <returns><see langword="true"/> if the current application is running with the elevated privileges; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">Unable to get current user identity.</exception>
        /// <remarks>
        /// By default, users won't see network drives mapped by the administrator (and vice versa),
        /// unless the <c>EnableLinkedConnections</c> registry value is set to <c>1</c>.
        /// </remarks>
        private static bool IsRunningElevated()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity == null)
            {
                throw new InvalidOperationException("Unable to get current user identity.");
            }

            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        #endregion // Private methods
    }
}
