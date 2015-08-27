// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeData.cs">
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

namespace LazyCopy.DriverClientLibrary.Native
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Contains message header information.
    /// </summary>
    /// <remarks>
    /// To receive messages from a kernel-mode MiniFilter, a user-mode application typically defines
    /// a custom message structure.<br/>
    /// This structure typically consists of this header structure, followed by an application-defined
    /// structure to hold the actual message data.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DriverNotificationHeader
    {
        /// <summary>
        /// The length, in bytes, of the expected reply.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public int ReplyLength;

        /// <summary>
        /// The unique identifier (ID) for the message sent by the kernel-mode driver.
        /// </summary>
        [MarshalAs(UnmanagedType.U8)]
        public long MessageId;

        /// <summary>
        /// Notification type.
        /// </summary>
        [MarshalAs(UnmanagedType.I4)]
        public int Type;

        /// <summary>
        /// Actual notification data length.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public int DataLength;
    }

    /// <summary>
    /// Contains message reply header information.
    /// </summary>
    /// <remarks>
    /// This structure is allocated by a user-mode application.<br/>
    /// It is a container for a reply that the application sends in response to a message
    /// received from a kernel-mode MiniFilter or MiniFilter instance.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DriverReplyHeader
    {
        /// <summary>
        /// Status value to be returned for the original message.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public int Status;

        /// <summary>
        /// Unique ID received in the <c>MessageId</c> field of the original message.
        /// </summary>
        [MarshalAs(UnmanagedType.U8)]
        public long MessageId;
    }

    /// <summary>
    /// Contains information about a MiniFilter or legacy filter driver.
    /// </summary>
    /// <remarks>
    /// This structure is returned from the <see cref="NativeMethods.FilterFindFirst"/>
    /// and <see cref="NativeMethods.FilterFindNext"/> methods if the
    /// <see cref="NativeMethods.FilterInformationClass.FilterAggregateStandardInformation"/>
    /// is requested.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FilterAggregateStandardInformation
    {
        /// <summary>
        /// The <see cref="Flags"/> value, if this structure contains the
        /// <see cref="FilterAggregateStandardMiniFilterInformation"/> structure.
        /// </summary>
        public const uint FltflAsiIsMinifilter = 0x00000001;

        /// <summary>
        /// The <see cref="Flags"/> value, if this structure contains the
        /// <see cref="FilterAggregateStandardLegacyFilterInformation"/> structure.
        /// </summary>
        public const uint FltflAsiIsLegacyfilter = 0x00000002;

        /// <summary>
        /// Byte offset of the next <see cref="FilterAggregateStandardInformation"/> entry,
        /// if multiple entries are present in a buffer.
        /// </summary>
        /// <remarks>
        /// This member is zero if no other entries follow this one.
        /// </remarks>
        [MarshalAs(UnmanagedType.U4)]
        public uint NextEntryOffset;

        /// <summary>
        /// Indicates whether the filter driver is a legacy filter or a MiniFilter.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint Flags;

        /// <summary>
        /// ULONG field to get the union offset from.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint StructureOffset;

        /// <summary>
        /// Gets the offset of the structure with the detailed filter information.
        /// </summary>
        /// <returns>Information structure offset within the current structure.</returns>
        public static int GetStructureOffset()
        {
            return Marshal.OffsetOf(typeof(FilterAggregateStandardInformation), nameof(StructureOffset)).ToInt32();
        }
    }

    /// <summary>
    /// Nested structure in the <see cref="FilterAggregateStandardInformation"/> for MiniFilters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FilterAggregateStandardMiniFilterInformation
    {
        /// <summary>
        /// Reserved field.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint Flags;

        /// <summary>
        /// Zero-based index used to identify the filter manager frame that the MiniFilter is in.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint FrameId;

        /// <summary>
        /// Number of instances that currently exist for the MiniFilter.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint NumberOfInstances;

        /// <summary>
        /// Length, in bytes, of the MiniFilter name string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterNameLength;

        /// <summary>
        /// Byte offset (relative to the beginning of the structure) of the first character
        /// of the Unicode MiniFilter name string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterNameBufferOffset;

        /// <summary>
        /// Length, in bytes, of the MiniFilter altitude string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterAltitudeLength;

        /// <summary>
        /// Byte offset (relative to the beginning of the structure) of the first character
        /// of the Unicode MiniFilter altitude string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterAltitudeBufferOffset;
    }

    /// <summary>
    /// Nested structure in the <see cref="FilterAggregateStandardInformation"/> for the legacy filters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FilterAggregateStandardLegacyFilterInformation
    {
        /// <summary>
        /// Reserved field.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public uint Flags;

        /// <summary>
        /// Length, in bytes, of the legacy filter name string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterNameLength;

        /// <summary>
        /// Byte offset (relative to the beginning of the structure) of the first character
        /// of the Unicode legacy filter name string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterNameBufferOffset;

        /// <summary>
        /// Length, in bytes, of the legacy filter altitude string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterAltitudeLength;

        /// <summary>
        /// Byte offset (relative to the beginning of the structure) of the first character
        /// of the Unicode legacy filter altitude string.
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public ushort FilterAltitudeBufferOffset;
    }
}
