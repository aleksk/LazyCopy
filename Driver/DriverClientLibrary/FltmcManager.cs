// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FltmcManager.cs">
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

namespace LazyCopy.DriverClientLibrary
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;

    using LazyCopy.DriverClientLibrary.Native;
    using LazyCopy.Utilities;
    using NLog;
    using ProcessPrivileges;

    /// <summary>
    /// This singleton class provides methods to manage the MiniFilter driver lifecycle: load/unload, start/stop, etc.<br/>
    /// Basically, it duplicates parts of the
    /// <a href="http://msdn.microsoft.com/en-us/library/windows/hardware/ff549684(v=vs.85).aspx"><c>fltmc</c></a> (Filter Manager Control)
    /// tool functionality.
    /// </summary>
    /// <remarks>
    /// This class uses the <a href="http://processprivileges.codeplex.com/">Process Privileges</a> library to
    /// enable the current process to be able to load/unload drivers.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Fltmc", Justification = "'Fltmc' spelling is correct.")]
    public sealed class FltmcManager
    {
        #region Fields

        /// <summary>
        /// Logger instance.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Private singleton instance.
        /// </summary>
        private static readonly Lazy<FltmcManager> LazyInstance = new Lazy<FltmcManager>(() => new FltmcManager());

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Prevents a default instance of the <see cref="FltmcManager"/> class from being created.
        /// </summary>
        private FltmcManager()
        {
            // Do nothing.
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// Gets the singleton class instance.
        /// </summary>
        public static FltmcManager Instance => FltmcManager.LazyInstance.Value;

        #endregion // Properties

        #region Load/Unload

        /// <summary>
        /// Loads the MiniFilter driver.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc load %filterName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not loaded.</exception>
        /// <remarks>
        /// In order for the process to be able to load the MiniFilter driver, it should be able to obtain the
        /// <c>SE_LOAD_DRIVER_NAME</c> privilege.<br/>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already loaded.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This is a singleton class.")]
        public void LoadFilter(string filterName)
        {
            if (string.IsNullOrEmpty(filterName))
            {
                throw new ArgumentNullException(nameof(filterName));
            }

            lock (this.syncRoot)
            {
                using (Process process = Process.GetCurrentProcess())
                using (new PrivilegeEnabler(process, Privilege.LoadDriver))
                {
                    uint hr = NativeMethods.FilterLoad(filterName);
                    if (hr != NativeMethods.Ok && hr != NativeMethods.ErrorAlreadyExists && hr != NativeMethods.ErrorServiceAlreadyRunning)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to load filter driver '{0}': 0x{1:X8}", filterName, hr);
                        FltmcManager.Logger.Error(message);

                        throw new InvalidOperationException(message, Marshal.GetExceptionForHR(unchecked((int)hr)));
                    }
                }
            }
        }

        /// <summary>
        /// Unloads the MiniFilter driver.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc unload %filterName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not unloaded.</exception>
        /// <remarks>
        /// In order for the process to be able to unload the MiniFilter driver, it should be able to obtain the
        /// <c>SE_LOAD_DRIVER_NAME</c> privilege.<br/>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already unloaded.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This is a singleton class.")]
        public void UnloadFilter(string filterName)
        {
            if (string.IsNullOrEmpty(filterName))
            {
                throw new ArgumentNullException(nameof(filterName));
            }

            lock (this.syncRoot)
            {
                using (Process process = Process.GetCurrentProcess())
                using (new PrivilegeEnabler(process, Privilege.LoadDriver))
                {
                    uint hr = NativeMethods.FilterUnload(filterName);
                    if (hr != NativeMethods.Ok && hr != NativeMethods.ErrorFltFilterNotFound)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to unload filter driver '{0}': 0x{1:X8}", filterName, hr);
                        FltmcManager.Logger.Error(message);

                        throw new InvalidOperationException(message, Marshal.GetExceptionForHR(unchecked((int)hr)));
                    }
                }
            }
        }

        #endregion // Load/Unload

        #region Attach

        /// <summary>
        /// Attaches a new MiniFilter instance to the given volume.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc attach %filterName% %volumeName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <param name="volumeName">
        /// Volume name to attach the driver to.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// </param>
        /// <returns>
        /// Attached instance name.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> or <paramref name="volumeName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not attached.</exception>
        /// <remarks>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already attached.
        /// </remarks>
        public string AttachFilter(string filterName, string volumeName)
        {
            return this.AttachFilter(filterName, volumeName, null);
        }

        /// <summary>
        /// Attaches a new MiniFilter instance to the given volume.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc attach %filterName% %volumeName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <param name="volumeName">
        /// Volume name to attach the driver to.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// </param>
        /// <param name="instanceName">The desired instance name.</param>
        /// <returns>
        /// Attached instance name.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> or <paramref name="volumeName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not attached.</exception>
        /// <remarks>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already attached.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This is a singleton class.")]
        public string AttachFilter(string filterName, string volumeName, string instanceName)
        {
            if (string.IsNullOrEmpty(filterName))
            {
                throw new ArgumentNullException(nameof(filterName));
            }

            if (string.IsNullOrEmpty(volumeName))
            {
                throw new ArgumentNullException(nameof(volumeName));
            }

            lock (this.syncRoot)
            {
                StringBuilder createdInstanceName = new StringBuilder(256);

                uint hr = NativeMethods.FilterAttach(filterName, volumeName, instanceName, (uint)createdInstanceName.Capacity - 1, createdInstanceName);
                if (hr != NativeMethods.Ok)
                {
                    // We don't want to throw an exception if the driver is already attached.
                    if (hr == NativeMethods.ErrorFltInstanceNameCollision || hr == NativeMethods.ErrorFltInstanceAltitudeCollision)
                    {
                        return instanceName;
                    }

                    string message = string.Format(CultureInfo.InvariantCulture, "Unable to attach filter driver '{0}' to volume '{1}': 0x{2:X8}", filterName, volumeName, hr);
                    FltmcManager.Logger.Error(message);

                    throw new InvalidOperationException(message, Marshal.GetExceptionForHR(unchecked((int)hr)));
                }

                return createdInstanceName.ToString();
            }
        }

        #endregion // Attach

        #region Detach

        /// <summary>
        /// Detaches the given MiniFilter instance from the given volume.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc detach %filterName% %volumeName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <param name="volumeName">
        /// Volume name to detach the driver from.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> or <paramref name="volumeName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not detached.</exception>
        /// <remarks>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already detached.
        /// </remarks>
        public void DetachFilter(string filterName, string volumeName)
        {
            this.DetachFilter(filterName, volumeName, null);
        }

        /// <summary>
        /// Detaches the given MiniFilter instance from the given volume.<br/>
        /// The same operation may be performed from the command line (administrative privileges are needed):
        /// <code>
        /// fltmc detach %filterName% %volumeName%
        /// </code>
        /// </summary>
        /// <param name="filterName">Name of the MiniFilter driver.</param>
        /// <param name="volumeName">
        /// Volume name to detach the driver from.<br/>
        /// The <paramref name="volumeName"/> input string can be any of the following. The trailing backslash (<c>\</c>) is optional.
        /// <list type="bullet">
        ///     <description>
        ///         A drive letter, such as <c>"D:\"</c>
        ///     </description>
        ///     <description>
        ///         A path to a volume mount point, such as <c>"c:\mnt\edrive\"</c>
        ///     </description>
        ///     <description>
        ///         A unique volume identifier (also called a volume GUID name),
        ///         such as <c>"\??\Volume{7603f260-142a-11d4-ac67-806d6172696f}\"</c>
        ///     </description>
        ///     <description>
        ///         A non-persistent device name (also called a target name or an NT device name),
        ///         such as <c>"\Device\HarddiskVolume1\"</c>
        ///     </description>
        /// </list>
        /// </param>
        /// <param name="instanceName">The MiniFilter instance name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="filterName"/> or <paramref name="volumeName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Driver was not detached.</exception>
        /// <remarks>
        /// NOTE: This method doesn't throw an exception if the MiniFilter driver is already detached.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This is a singleton class.")]
        public void DetachFilter(string filterName, string volumeName, string instanceName)
        {
            if (string.IsNullOrEmpty(filterName))
            {
                throw new ArgumentNullException(nameof(filterName));
            }

            if (string.IsNullOrEmpty(volumeName))
            {
                throw new ArgumentNullException(nameof(volumeName));
            }

            lock (this.syncRoot)
            {
                uint hr = NativeMethods.FilterDetach(filterName, volumeName, instanceName);
                if (hr != NativeMethods.Ok && hr != NativeMethods.ErrorFltInstanceNotFound)
                {
                    string message = string.Format(CultureInfo.InvariantCulture, "Unable to detach filter driver '{0}' from volume '{1}': 0x{2:X8}", filterName, volumeName, hr);
                    FltmcManager.Logger.Error(message);

                    throw new InvalidOperationException(message, Marshal.GetExceptionForHR(unchecked((int)hr)));
                }
            }
        }

        #endregion // Detach

        #region Get information

        /// <summary>
        /// Get the information about the filters loaded.
        /// </summary>
        /// <returns>List of filters loaded.</returns>
        /// <exception cref="InvalidOperationException">Filter information was not retrieved.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This is a singleton class.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method is not a property.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "LazyCopy.DriverClientLibrary.Native.NativeMethods.FilterFindClose(System.IntPtr)", Justification = "We don't care whether this method succeeds or not.")]
        public IEnumerable<FilterInfo> GetFiltersInformation()
        {
            List<FilterInfo> result = new List<FilterInfo>();

            lock (this.syncRoot)
            {
                using (ResizableBuffer buffer = new ResizableBuffer(1024))
                {
                    IntPtr filterFindHandle = IntPtr.Zero;
                    uint hr = 0;

                    try
                    {
                        uint bytesReturned;

                        hr = NativeMethods.FilterFindFirst(NativeMethods.FilterInformationClass.FilterAggregateStandardInformation, buffer.DangerousGetPointer(), (uint)buffer.ByteLength, out bytesReturned, out filterFindHandle);

                        // If the buffer allocated is not large enough to hold all data returned, resize it and try again.
                        if (hr == NativeMethods.ErrorInsufficientBuffer)
                        {
                            buffer.Resize(unchecked((int)bytesReturned));
                            hr = NativeMethods.FilterFindFirst(NativeMethods.FilterInformationClass.FilterAggregateStandardInformation, buffer.DangerousGetPointer(), (uint)buffer.ByteLength, out bytesReturned, out filterFindHandle);
                        }

                        if (hr != NativeMethods.Ok)
                        {
                            // There are no filters available.
                            if (hr == NativeMethods.ErrorNoMoreItems)
                            {
                                return result;
                            }

                            throw Marshal.GetExceptionForHR(unchecked((int)hr));
                        }

                        result.AddRange(FltmcManager.MarshalFilterInfo(buffer.DangerousGetPointer()));

                        while (true)
                        {
                            hr = NativeMethods.FilterFindNext(filterFindHandle, NativeMethods.FilterInformationClass.FilterAggregateStandardInformation, buffer.DangerousGetPointer(), (uint)buffer.ByteLength, out bytesReturned);
                            if (hr == NativeMethods.ErrorInsufficientBuffer)
                            {
                                buffer.Resize(unchecked((int)bytesReturned));
                                hr = NativeMethods.FilterFindNext(filterFindHandle, NativeMethods.FilterInformationClass.FilterAggregateStandardInformation, buffer.DangerousGetPointer(), (uint)buffer.ByteLength, out bytesReturned);
                            }

                            if (hr != NativeMethods.Ok)
                            {
                                if (hr == NativeMethods.ErrorNoMoreItems)
                                {
                                    break;
                                }

                                throw Marshal.GetExceptionForHR(unchecked((int)hr));
                            }

                            result.AddRange(FltmcManager.MarshalFilterInfo(buffer.DangerousGetPointer()));
                        }
                    }
                    catch (Exception e)
                    {
                        string message = string.Format(CultureInfo.InvariantCulture, "Unable to get the filter driver information: 0x{0:X8}", hr);

                        FltmcManager.Logger.Error(e, message);
                        throw new InvalidOperationException(message, e);
                    }
                    finally
                    {
                        if (filterFindHandle != IntPtr.Zero)
                        {
                            NativeMethods.FilterFindClose(filterFindHandle);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Marshals the filter info objects from the <paramref name="ptr"/> specified.
        /// </summary>
        /// <param name="ptr">Pointer to the buffer with the filter information structures to marshal.</param>
        /// <returns>List of filter information structures marshaled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is equal to the <see cref="IntPtr.Zero"/>.</exception>
        /// <exception cref="InvalidOperationException">Filter information structure contains invalid data.</exception>
        private static IEnumerable<FilterInfo> MarshalFilterInfo(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(ptr));
            }

            List<FilterInfo> result = new List<FilterInfo>();
            IntPtr curPtr = ptr;

            while (true)
            {
                // Get the structure offset from the aggregate information and marshal it.
                FilterAggregateStandardInformation aggregateInfo = (FilterAggregateStandardInformation)Marshal.PtrToStructure(curPtr, typeof(FilterAggregateStandardInformation));
                IntPtr infoPtr = curPtr + FilterAggregateStandardInformation.GetStructureOffset();

                FilterInfo filterInfo = new FilterInfo();

                //// The following code is not very 'clear', but adding a separate method for parsing Name and Altitude fields is redundant.

                // Whether the structure contains legacy or minifilter information.
                if (aggregateInfo.Flags == FilterAggregateStandardInformation.FltflAsiIsMinifilter)
                {
                    FilterAggregateStandardMiniFilterInformation info = (FilterAggregateStandardMiniFilterInformation)Marshal.PtrToStructure(infoPtr, typeof(FilterAggregateStandardMiniFilterInformation));
                    filterInfo.FrameId = unchecked((int)info.FrameId);
                    filterInfo.Instances = unchecked((int)info.NumberOfInstances);

                    filterInfo.Name = Marshal.PtrToStringUni(curPtr + info.FilterNameBufferOffset, info.FilterNameLength / UnicodeEncoding.CharSize);
                    filterInfo.Altitude = int.Parse(Marshal.PtrToStringUni(curPtr + info.FilterAltitudeBufferOffset, info.FilterAltitudeLength / UnicodeEncoding.CharSize), NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                else if (aggregateInfo.Flags == FilterAggregateStandardInformation.FltflAsiIsLegacyfilter)
                {
                    FilterAggregateStandardLegacyFilterInformation info = (FilterAggregateStandardLegacyFilterInformation)Marshal.PtrToStructure(infoPtr, typeof(FilterAggregateStandardLegacyFilterInformation));
                    filterInfo.Name = Marshal.PtrToStringUni(curPtr + info.FilterNameBufferOffset, info.FilterNameLength / UnicodeEncoding.CharSize);
                    filterInfo.Altitude = int.Parse(Marshal.PtrToStringUni(curPtr + info.FilterAltitudeBufferOffset, info.FilterAltitudeLength / UnicodeEncoding.CharSize), NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Invalid information type received: {0:X8}", aggregateInfo.Flags));
                }

                result.Add(filterInfo);

                // If there're several entries in the buffer, proceed to the next one.
                if (aggregateInfo.NextEntryOffset == 0)
                {
                    break;
                }

                curPtr += unchecked((int)aggregateInfo.NextEntryOffset);
            }

            return result;
        }

        #endregion // Get information
    }
}
