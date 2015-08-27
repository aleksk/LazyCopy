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

namespace LazyCopy.Utilities.Native
{
    using System;
    using System.Runtime.InteropServices;

    #region Enums

    /// <summary>
    /// Enumeration used by the <see cref="NativeMethods.CreateSymbolicLink"/> function.
    /// </summary>
    internal enum SymbolicLinkFlag
    {
        /// <summary>
        /// The link target is a file.
        /// </summary>
        File = 0,

        /// <summary>
        /// The link target is a directory.
        /// </summary>
        Directory = 1
    }

    /// <summary>
    /// The scope of the resource.
    /// </summary>
    internal enum ResourceScope
    {
        /// <summary>
        /// Enumerate currently connected resources.
        /// </summary>
        Connected = 1,

        /// <summary>
        /// Enumerate all resources on the network.
        /// </summary>
        GlobalNetwork,

        /// <summary>
        /// Enumerate remembered (persistent) connections.
        /// </summary>
        Remembered
    }

    /// <summary>
    /// The type of the resource.
    /// </summary>
    internal enum ResourceType
    {
        /// <summary>
        /// All resources.
        /// </summary>
        Any = 0,

        /// <summary>
        /// Disk resources.
        /// </summary>
        Disk = 1,

        /// <summary>
        /// Print resources.
        /// </summary>
        Print = 2
    }

    /// <summary>
    /// The display options for the network object in a network browsing user interface.
    /// </summary>
    internal enum ResourceDisplayType
    {
        /// <summary>
        /// The method used to display the object does not matter.
        /// </summary>
        Generic = 0x0,

        /// <summary>
        /// The object should be displayed as a domain.
        /// </summary>
        Domain = 0x1,

        /// <summary>
        /// The object should be displayed as a server.
        /// </summary>
        Server = 0x2,

        /// <summary>
        /// The object should be displayed as a share.
        /// </summary>
        Share = 0x3,

        /// <summary>
        /// The object should be displayed as a file.
        /// </summary>
        File = 0x4,

        /// <summary>
        /// The object should be displayed as a group.
        /// </summary>
        Group = 0x5,

        /// <summary>
        /// The object should be displayed as a network.
        /// </summary>
        Network = 0x6,

        /// <summary>
        /// The object should be displayed as a logical root for the entire network.
        /// </summary>
        Root = 0x7,

        /// <summary>
        /// The object should be displayed as a administrative share.
        /// </summary>
        ShareAdmin = 0x8,

        /// <summary>
        /// The object should be displayed as a directory.
        /// </summary>
        Directory = 0x9,

        /// <summary>
        /// The object should be displayed as a tree.
        /// </summary>
        Tree = 0xA,

        /// <summary>
        /// The object should be displayed as a Netware Directory Service container.
        /// </summary>
        NdsContainer = 0xA
    }

    /// <summary>
    /// A set of bit flags describing how the resource can be used.
    /// </summary>
    [Flags]
    internal enum ResourceUsage
    {
        /// <summary>
        /// The resource is a connectable resource.
        /// </summary>
        Connectable = 0x1,

        /// <summary>
        /// The resource is a container resource.
        /// </summary>
        Container = 0x2,

        /// <summary>
        /// The resource is not a local device.
        /// </summary>
        NoLocalDevice = 0x4,

        /// <summary>
        /// The resource is a sibling.
        /// </summary>
        Sibling = 0x8,

        /// <summary>
        /// The resource must be attached.
        /// </summary>
        Attached = 0x10
    }

    /// <summary>
    /// A set of flags describing file access rights.
    /// </summary>
    [Flags]
    internal enum AccessRights : uint
    {
        #region Standard section

        /// <summary>
        /// Controls the ability to get or set the SACL in an object's security descriptor.
        /// </summary>
        AccessSystemSecurity = 0x1000000,

        /// <summary>
        /// Maximum allowed access mask.
        /// </summary>
        MaximumAllowed = 0x2000000,

        /// <summary>
        /// The right to delete the object.
        /// </summary>
        Delete = 0x10000,

        /// <summary>
        /// The right to read the information in the object's security descriptor, not including the information in the system access control list (SACL).
        /// </summary>
        ReadControl = 0x20000,

        /// <summary>
        /// The right to modify the discretionary access control list (DACL) in the object's security descriptor.
        /// </summary>
        WriteDac = 0x40000,

        /// <summary>
        /// The right to change the owner in the object's security descriptor.
        /// </summary>
        WriteOwner = 0x80000,

        /// <summary>
        /// The right to use the object for synchronization.
        /// </summary>
        Synchronize = 0x100000,

        /// <summary>
        /// Combines <see cref="Delete"/>, <see cref="ReadControl"/>, <see cref="WriteDac"/>, and <see cref="WriteOwner"/> access.
        /// </summary>
        StandardRightsRequired = 0xF0000,

        /// <summary>
        /// Currently defined to equal <see cref="ReadControl"/>.
        /// </summary>
        StandardRightsRead = ReadControl,

        /// <summary>
        /// Currently defined to equal <see cref="ReadControl"/>.
        /// </summary>
        StandardRightsWrite = ReadControl,

        /// <summary>
        /// Currently defined to equal <see cref="ReadControl"/>.
        /// </summary>
        StandardRightsExecute = ReadControl,

        /// <summary>
        /// Combines <see cref="Delete"/>, <see cref="ReadControl"/>, <see cref="WriteDac"/>, <see cref="WriteOwner"/>, and <see cref="Synchronize"/> access.
        /// </summary>
        StandardRightsAll = 0x1F0000,

        /// <summary>
        /// Contains specific rights.
        /// </summary>
        SpecificRightsAll = 0xFFFF,

        /// <summary>
        /// For a file object, the right to read the corresponding file data.
        /// For a directory object, the right to read the corresponding directory data.
        /// </summary>
        FileReadData = 0x0001,

        /// <summary>
        /// For a directory, the right to list the contents of the directory.
        /// </summary>
        FileListDirectory = 0x0001,

        /// <summary>
        /// For a file object, the right to write data to the file.
        /// For a directory object, the right to create a file in the directory.
        /// </summary>
        FileWriteData = 0x0002,

        /// <summary>
        /// For a directory, the right to create a file in the directory.
        /// </summary>
        FileAddFile = 0x0002,

        /// <summary>
        /// For a file object, the right to append data to the file.
        /// For a directory object, the right to create a subdirectory.
        /// </summary>
        FileAppendData = 0x0004,

        /// <summary>
        /// For a directory, the right to create a subdirectory.
        /// </summary>
        FileAddSubdirectory = 0x0004,

        /// <summary>
        /// For a named pipe, the right to create a pipe.
        /// </summary>
        FileCreatePipeInstance = 0x0004,

        /// <summary>
        /// The right to read extended file attributes.
        /// </summary>
        FileReadEa = 0x0008,

        /// <summary>
        /// The right to write extended file attributes.
        /// </summary>
        FileWriteEa = 0x0010,

        /// <summary>
        /// The right to execute the file.
        /// </summary>
        FileExecute = 0x0020,

        /// <summary>
        /// For a directory, the right to traverse the directory.
        /// </summary>
        FileTraverse = 0x0020,

        /// <summary>
        /// For a directory, the right to delete a directory and all the files it contains, including read-only files.
        /// </summary>
        FileDeleteChild = 0x0040,

        /// <summary>
        /// The right to read file attributes.
        /// </summary>
        FileReadAttributes = 0x0080,

        /// <summary>
        /// The right to write file attributes.
        /// </summary>
        FileWriteAttributes = 0x0100,

        #endregion // Standard section

        #region Generic section

        /// <summary>
        /// Read access.
        /// </summary>
        GenericRead = 0x80000000,

        /// <summary>
        /// Write access.
        /// </summary>
        GenericWrite = 0x40000000,

        /// <summary>
        /// Execute access.
        /// </summary>
        GenericExecute = 0x20000000,

        /// <summary>
        /// All possible access rights.
        /// </summary>
        GenericAll = 0x10000000,

        /// <summary>
        /// All access rights.
        /// </summary>
        FileAllAccess =
            StandardRightsRequired |
            Synchronize |
            0x1FF,

        /// <summary>
        /// Generic read access rights.
        /// </summary>
        FileGenericRead =
            StandardRightsRead |
            FileReadData |
            FileReadAttributes |
            FileReadEa |
            Synchronize,

        /// <summary>
        /// Generic write access rights.
        /// </summary>
        FileGenericWrite =
            StandardRightsWrite |
            FileWriteData |
            FileWriteAttributes |
            FileWriteEa |
            FileAppendData |
            Synchronize,

        /// <summary>
        /// Generic execution access rights.
        /// </summary>
        FileGenericExecute =
            StandardRightsExecute |
            FileReadAttributes |
            FileExecute |
            Synchronize

        #endregion // Generic section
    }

    /// <summary>
    /// A set of flags describing file or device attributes and flags.
    /// </summary>
    [Flags]
    internal enum EFileAttributes : uint
    {
        /// <summary>
        /// A file that is read-only.
        /// Applications can read the file, but cannot write to it or delete it.
        /// </summary>
        ReadOnly = 0x00000001,

        /// <summary>
        /// The file or directory is hidden.
        /// It is not included in an ordinary directory listing.
        /// </summary>
        Hidden = 0x00000002,

        /// <summary>
        /// A file or directory that the operating system uses a part of, or uses exclusively.
        /// </summary>
        System = 0x00000004,

        /// <summary>
        /// The handle that identifies a directory.
        /// </summary>
        Directory = 0x00000010,

        /// <summary>
        /// A file or directory that is an archive file or directory. 
        /// </summary>
        Archive = 0x00000020,

        /// <summary>
        /// This value is reserved for system use.
        /// </summary>
        Device = 0x00000040,

        /// <summary>
        /// A file that does not have other attributes set.
        /// This attribute is valid only when used alone.
        /// </summary>
        Normal = 0x00000080,

        /// <summary>
        /// A file that is being used for temporary storage.
        /// </summary>
        Temporary = 0x00000100,

        /// <summary>
        /// A file that is a sparse file.
        /// </summary>
        SparseFile = 0x00000200,

        /// <summary>
        /// A file or directory that has an associated reparse point, or a file that is a symbolic link.
        /// </summary>
        ReparsePoint = 0x00000400,

        /// <summary>
        /// A file or directory that is compressed.
        /// </summary>
        Compressed = 0x00000800,

        /// <summary>
        /// The data of a file is not available immediately.
        /// </summary>
        Offline = 0x00001000,

        /// <summary>
        /// The file or directory is not to be indexed by the content indexing service.
        /// </summary>
        NotContentIndexed = 0x00002000,

        /// <summary>
        /// A file or directory that is encrypted.
        /// </summary>
        Encrypted = 0x00004000,

        /// <summary>
        /// Write operations will not go through any intermediate cache, they will go directly to disk.
        /// </summary>
        WriteThrough = 0x80000000,

        /// <summary>
        /// The file or device is being opened or created for asynchronous I/O.
        /// </summary>
        Overlapped = 0x40000000,

        /// <summary>
        /// The file or device is being opened with no system caching for data reads and writes.
        /// </summary>
        NoBuffering = 0x20000000,

        /// <summary>
        /// Access is intended to be random.
        /// </summary>
        RandomAccess = 0x10000000,

        /// <summary>
        /// Access is intended to be sequential from beginning to end. 
        /// </summary>
        SequentialScan = 0x08000000,

        /// <summary>
        /// The file is to be deleted immediately after all of its handles are closed,
        /// which includes the specified handle and any other open or duplicated handles.
        /// </summary>
        DeleteOnClose = 0x04000000,

        /// <summary>
        /// The file is being opened or created for a backup or restore operation.
        /// </summary>
        BackupSemantics = 0x02000000,

        /// <summary>
        /// Access will occur according to POSIX rules.
        /// </summary>
        PosixSemantics = 0x01000000,

        /// <summary>
        /// Normal reparse point processing will not occur.
        /// </summary>
        OpenReparsePoint = 0x00200000,

        /// <summary>
        /// The file data is requested, but it should continue to be located in remote storage.
        /// </summary>
        OpenNoRecall = 0x00100000,

        /// <summary>
        /// Invalid file attributes.
        /// </summary>
        Invalid = 0xFFFFFFFF
    }

    /// <summary>
    /// Access rights for an access token object.
    /// </summary>
    [Flags]
    internal enum TokenAccessRights : uint
    {
        /// <summary>
        /// Required to attach a primary token to a process.
        /// </summary>
        TokenAssignPrimary = 0x0001,

        /// <summary>
        /// Required to duplicate an access token.
        /// </summary>
        TokenDuplicate = 0x0002,

        /// <summary>
        /// Required to attach an impersonation access token to a process.
        /// </summary>
        TokenImpersonate = 0x0004,

        /// <summary>
        /// Required to query an access token.
        /// </summary>
        TokenQuery = 0x0008,

        /// <summary>
        /// Required to query the source of an access token.
        /// </summary>
        TokenQuerySource = 0x0010,

        /// <summary>
        /// Required to enable or disable the privileges in an access token.
        /// </summary>
        TokenAdjustPrivileges = 0x0020,

        /// <summary>
        /// Required to adjust the attributes of the groups in an access token.
        /// </summary>
        TokenAdjustGroups = 0x0040,

        /// <summary>
        /// Required to change the default owner, primary group, or DACL of an access token.
        /// </summary>
        TokenAdjustDefault = 0x0080,

        /// <summary>
        /// Required to adjust the session ID of an access token. The SE_TCB_NAME privilege is required.
        /// </summary>
        TokenAdjustSessionId = 0x0100,

        /// <summary>
        /// Combines <see cref="AccessRights.StandardRightsExecute"/> and <see cref="TokenImpersonate"/>.
        /// </summary>
        TokenExecute =
            AccessRights.StandardRightsExecute |
            TokenImpersonate,

        /// <summary>
        /// Combines <see cref="AccessRights.StandardRightsRead"/> and <see cref="TokenQuery"/>.
        /// </summary>
        TokenRead =
            AccessRights.StandardRightsRead |
            TokenQuery,

        /// <summary>
        /// Combines all possible access rights for a token.
        /// </summary>
        TokenAllAccess =
            AccessRights.StandardRightsRequired |
            TokenAssignPrimary |
            TokenDuplicate |
            TokenImpersonate |
            TokenQuery |
            TokenQuerySource |
            TokenAdjustPrivileges |
            TokenAdjustGroups |
            TokenAdjustDefault |
            TokenAdjustSessionId
    }

    /// <summary>
    /// Supplies the impersonation level of the new token.
    /// </summary>
    internal enum SecurityImpersonationLevel
    {
        /// <summary>
        /// The server process cannot obtain identification information about the client,
        /// and it cannot impersonate the client. It is defined with no value given, and thus,
        /// by ANSI C rules, defaults to a value of zero.
        /// </summary>
        SecurityAnonymous = 0,

        /// <summary>
        /// The server process can obtain information about the client, such as security identifiers and privileges,
        /// but it cannot impersonate the client. This is useful for servers that export their own objects,
        /// for example, database products that export tables and views.
        /// Using the retrieved client-security information, the server can make access-validation decisions without
        /// being able to use other services that are using the client's security context.
        /// </summary>
        SecurityIdentification = 1,

        /// <summary>
        /// The server process can impersonate the client's security context on its local system.
        /// The server cannot impersonate the client on remote systems.
        /// </summary>
        SecurityImpersonation = 2,

        /// <summary>
        /// The server process can impersonate the client's security context on remote systems.
        /// NOTE: Windows NT: This impersonation level is not supported.
        /// </summary>
        SecurityDelegation = 3
    }

    /// <summary>
    /// Specifies the type of information being assigned to or retrieved from an access token.
    /// </summary>
    internal enum TokenInformationClass
    {
        /// <summary>
        /// The buffer receives a <see cref="TokenUser"/> structure that contains the user account of the token.
        /// </summary>
        TokenUser = 1,

        /// <summary>
        /// The buffer receives a TOKEN_GROUPS structure that contains the group accounts associated with the token.
        /// </summary>
        TokenGroups,

        /// <summary>
        /// The buffer receives a TOKEN_PRIVILEGES structure that contains the privileges of the token.
        /// </summary>
        TokenPrivileges,

        /// <summary>
        /// The buffer receives a TOKEN_OWNER structure that contains the default owner security identifier (SID) for newly created objects.
        /// </summary>
        TokenOwner,

        /// <summary>
        /// The buffer receives a TOKEN_PRIMARY_GROUP structure that contains the default primary group SID for newly created objects.
        /// </summary>
        TokenPrimaryGroup,

        /// <summary>
        /// The buffer receives a TOKEN_DEFAULT_DACL structure that contains the default DACL for newly created objects.
        /// </summary>
        TokenDefaultDacl,

        /// <summary>
        /// The buffer receives a TOKEN_SOURCE structure that contains the source of the token. TOKEN_QUERY_SOURCE access is needed to retrieve this information.
        /// </summary>
        TokenSource,

        /// <summary>
        /// The buffer receives a TOKEN_TYPE value that indicates whether the token is a primary or impersonation token.
        /// </summary>
        TokenType,

        /// <summary>
        /// The buffer receives a SECURITY_IMPERSONATION_LEVEL value that indicates the impersonation level of the token. If the access token is not an impersonation token, the function fails.
        /// </summary>
        TokenImpersonationLevel,

        /// <summary>
        /// The buffer receives a TOKEN_STATISTICS structure that contains various token statistics.
        /// </summary>
        TokenStatistics,

        /// <summary>
        /// The buffer receives a TOKEN_GROUPS structure that contains the list of restricting SIDs in a restricted token.
        /// </summary>
        TokenRestrictedSids,

        /// <summary>
        /// The buffer receives a DWORD value that indicates the Terminal Services session identifier that is associated with the token. 
        /// </summary>
        TokenSessionId,

        /// <summary>
        /// The buffer receives a TOKEN_GROUPS_AND_PRIVILEGES structure that contains the user SID, the group accounts, the restricted SIDs, and the authentication ID associated with the token.
        /// </summary>
        TokenGroupsAndPrivileges,

        /// <summary>
        /// Reserved value.
        /// </summary>
        TokenSessionReference,

        /// <summary>
        /// The buffer receives a DWORD value that is nonzero if the token includes the SANDBOX_INERT flag.
        /// </summary>
        TokenSandBoxInert,

        /// <summary>
        /// Reserved value.
        /// </summary>
        TokenAuditPolicy,

        /// <summary>
        /// The buffer receives a TOKEN_ORIGIN value. 
        /// </summary>
        TokenOrigin,

        /// <summary>
        /// The buffer receives a TOKEN_ELEVATION_TYPE value that specifies the elevation level of the token.
        /// </summary>
        TokenElevationType,

        /// <summary>
        /// The buffer receives a TOKEN_LINKED_TOKEN structure that contains a handle to another token that is linked to this token.
        /// </summary>
        TokenLinkedToken,

        /// <summary>
        /// The buffer receives a TOKEN_ELEVATION structure that specifies whether the token is elevated.
        /// </summary>
        TokenElevation,

        /// <summary>
        /// The buffer receives a DWORD value that is nonzero if the token has ever been filtered.
        /// </summary>
        TokenHasRestrictions,

        /// <summary>
        /// The buffer receives a TOKEN_ACCESS_INFORMATION structure that specifies security information contained in the token.
        /// </summary>
        TokenAccessInformation,

        /// <summary>
        /// The buffer receives a DWORD value that is nonzero if virtualization is allowed for the token.
        /// </summary>
        TokenVirtualizationAllowed,

        /// <summary>
        /// The buffer receives a DWORD value that is nonzero if virtualization is enabled for the token.
        /// </summary>
        TokenVirtualizationEnabled,

        /// <summary>
        /// The buffer receives a TOKEN_MANDATORY_LABEL structure that specifies the token's integrity level. 
        /// </summary>
        TokenIntegrityLevel,

        /// <summary>
        /// The buffer receives a DWORD value that is nonzero if the token has the UIAccess flag set.
        /// </summary>
        TokenUIAccess,

        /// <summary>
        /// The buffer receives a TOKEN_MANDATORY_POLICY structure that specifies the token's mandatory integrity policy.
        /// </summary>
        TokenMandatoryPolicy,

        /// <summary>
        /// The buffer receives the token's logon security identifier (SID).
        /// </summary>
        TokenLogonSid,

        /// <summary>
        /// The maximum value for this enumeration.
        /// </summary>
        MaxTokenInfoClass
    }

    /// <summary>
    /// Contains values that specify the type of a security identifier (SID).
    /// </summary>
    internal enum SidNameUse
    {
        /// <summary>
        /// A user SID.
        /// </summary>
        SidTypeUser = 1,

        /// <summary>
        /// A group SID.
        /// </summary>
        SidTypeGroup,

        /// <summary>
        /// A domain SID.
        /// </summary>
        SidTypeDomain,

        /// <summary>
        /// An alias SID.
        /// </summary>
        SidTypeAlias,

        /// <summary>
        /// A SID for a well-known group.
        /// </summary>
        SidTypeWellKnownGroup,

        /// <summary>
        /// A SID for a deleted account.
        /// </summary>
        SidTypeDeletedAccount,

        /// <summary>
        /// A SID that is not valid.
        /// </summary>
        SidTypeInvalid,

        /// <summary>
        /// A SID of unknown type.
        /// </summary>
        SidTypeUnknown,

        /// <summary>
        /// A SID for a computer.
        /// </summary>
        SidTypeComputer,

        /// <summary>
        /// A mandatory integrity label SID.
        /// </summary>
        SidTypeLabel
    }

    #endregion // Enums

    #region Structures

    /// <summary>
    /// Contains reparse point data header for a non-Microsoft reparse point.
    /// </summary>
    /// <remarks>
    /// Refer to <c>http://msdn.microsoft.com/en-us/library/windows/hardware/ff552014(v=vs.85).aspx</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ReparseGuidDataBufferHeader
    {
        /// <summary>
        /// Reparse point tag.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public int ReparseTag;

        /// <summary>
        /// Size, in bytes, of the reparse data in the DataBuffer member.
        /// </summary>
        public ushort ReparseDataLength;

        /// <summary>
        /// Length, in bytes, of the unparsed portion of the file name pointed to by the FileName member of the associated file object.
        /// </summary>
        public ushort Reserved;

        /// <summary>
        /// Reparse point GUID.
        /// </summary>
        public Guid ReparseGuid;
    }

    /// <summary>
    /// Contains reparse point data header for a Microsoft reparse point.
    /// </summary>
    /// <remarks>
    /// Refer to <c>http://msdn.microsoft.com/en-us/library/windows/hardware/ff552012%28v=vs.85%29.aspx</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ReparseDataBufferHeader
    {
        /// <summary>
        /// Reparse point tag.
        /// </summary>
        [MarshalAs(UnmanagedType.U4)]
        public int ReparseTag;

        /// <summary>
        /// Size, in bytes, of the reparse data in the DataBuffer member.
        /// </summary>
        public ushort ReparseDataLength;

        /// <summary>
        /// Length, in bytes, of the unparsed portion of the file name pointed to by the FileName member of the associated file object.
        /// </summary>
        public ushort Reserved;
    }

    /// <summary>
    /// Contains information about a network resource.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct NetResource
    {
        /// <summary>
        /// The scope of the enumeration.
        /// </summary>
        public ResourceScope Scope;

        /// <summary>
        /// The type of the resource.
        /// </summary>
        public ResourceType Type;

        /// <summary>
        /// The display options for the network object in a network browsing user interface.
        /// </summary>
        public ResourceDisplayType DisplayType;

        /// <summary>
        /// A set of bit flags describing how the resource can be used.
        /// </summary>
        public ResourceUsage Usage;

        /// <summary>
        /// Specifies the name of a local device.
        /// </summary>
        public string LocalName;

        /// <summary>
        /// If the entry is a network resource, this member specifies the remote network name.<br/>
        /// If the entry is a current or persistent connection, this member points to the network name associated with the name pointed to by the <see cref="LocalName"/> member.
        /// </summary>
        public string RemoteName;

        /// <summary>
        /// Comment supplied by the network provider.
        /// </summary>
        public string Comment;

        /// <summary>
        /// The name of the provider that owns the resource.
        /// </summary>
        public string Provider;
    }

    /// <summary>
    /// Contains the time of the last input.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        /// <summary>
        /// The size of the structure, in bytes.
        /// </summary>
        public uint Size;

        /// <summary>
        /// The tick count when the last input event was received.
        /// </summary>
        public uint Time;
    }

    /// <summary>
    /// Contains the user account of the token.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenUser
    {
        /// <summary>
        /// User account.
        /// </summary>
        public SidAndAttributes User;
    }

    /// <summary>
    /// User account information.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SidAndAttributes
    {
        /// <summary>
        /// User SID.
        /// </summary>
        public IntPtr Sid;

        /// <summary>
        /// User attributes.
        /// </summary>
        public int Attributes;
    }

    #endregion // Structures
}
