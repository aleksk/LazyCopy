/*++

    The MIT License (MIT)

    Copyright (c) 2015 Aleksey Kabanov

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

Module Name:

    ReparsePoints.c

Abstract:

    Contains helper function definitions for managing custom reparse
    points used by the current minifilter.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "ReparsePoints.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcGetReparsePointData)
    #pragma alloc_text(PAGE, LcUntagFile)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Structures.
//------------------------------------------------------------------------

//
// Contains reparse point data used by this driver.
//
typedef struct _LC_REPARSE_DATA
{
    // Reparse point tag that uniquely identifies the owner of the reparse point.
    ULONG  ReparseTag;

    // Size, in bytes, of the reparse data in the 'ReparseBuffer' member.
    USHORT ReparseDataLength;

    // Reserved.
    USHORT Reserved;

    // GUID that uniquely identifies the owner of the reparse point.
    GUID   ReparseGuid;

    // User-defined data for the reparse point.
    struct {
        // Size of the remote file.
        LONGLONG RemoteFileSize;

        // Buffer containing remote file path string.
        WCHAR    RemoteFilePath[1];
    } ReparseBuffer;
} LC_REPARSE_DATA, *PLC_REPARSE_DATA;

//------------------------------------------------------------------------
//  Reparse points management functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcGetReparsePointData (
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _Out_ PLARGE_INTEGER        RemoteFileSize,
    _Out_ PUNICODE_STRING       RemoteFilePath
    )
/*++

Summary:

    This function gets the reparse point data from the file given.

Arguments:

    FltObjects     - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                     opaque handles to this filter, instance, its associated volume and
                     file object.

    RemoteFileSize - Size of the remote file to be fetched.

    RemoteFilePath - Path of the file to be fetched.

Return Value:

    The return value is the status of the operation.
    Returns STATUS_NOT_A_REPARSE_POINT, if reparse point data was not found.

--*/
{
    NTSTATUS                 status               = STATUS_SUCCESS;
    REPARSE_GUID_DATA_BUFFER dataBuffer           = { 0 };
    PLC_REPARSE_DATA         reparseData          = NULL;
    ULONG                    reparseDataLength    = 0;
    SIZE_T                   remoteFilePathLength = 0;
    LARGE_INTEGER            remoteFileSize       = { 0 };
    UNICODE_STRING           remoteFilePath       = { 0 };

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(FltObjects             != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(FltObjects->Instance   != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(FltObjects->FileObject != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(RemoteFileSize         != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(RemoteFilePath         != NULL, STATUS_INVALID_PARAMETER_3);

    __try
    {
        // Get the reparse data size.
        status = FltFsControlFile(FltObjects->Instance, FltObjects->FileObject, FSCTL_GET_REPARSE_POINT, NULL, 0, &dataBuffer, sizeof(REPARSE_GUID_DATA_BUFFER), NULL);
        if (status != STATUS_BUFFER_OVERFLOW)
        {
            status = STATUS_NOT_A_REPARSE_POINT;
            __leave;
        }

        reparseDataLength = REPARSE_GUID_DATA_BUFFER_HEADER_SIZE + dataBuffer.ReparseDataLength;

        // Get the reparse point buffer.
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&reparseData, reparseDataLength));
        NT_IF_FAIL_LEAVE(FltFsControlFile(FltObjects->Instance, FltObjects->FileObject, FSCTL_GET_REPARSE_POINT, NULL, 0, reparseData, reparseDataLength, NULL));

        // Get remote file path.
        remoteFilePathLength = (wcslen(reparseData->ReparseBuffer.RemoteFilePath) + 1) * sizeof(WCHAR);
        NT_IF_FALSE_LEAVE(reparseData->ReparseDataLength < sizeof(reparseData->ReparseBuffer) + remoteFilePathLength, STATUS_IO_REPARSE_DATA_INVALID);
        NT_IF_FAIL_LEAVE(LcAllocateUnicodeString(&remoteFilePath, (USHORT)remoteFilePathLength));

        __analysis_assume(remoteFilePath.Buffer != NULL);
        RtlCopyMemory(remoteFilePath.Buffer, &reparseData->ReparseBuffer.RemoteFilePath, remoteFilePathLength);
        remoteFilePath.Length = (USHORT)remoteFilePathLength - sizeof(WCHAR);

        // And size.
        remoteFileSize.QuadPart = reparseData->ReparseBuffer.RemoteFileSize;

        *RemoteFileSize       = remoteFileSize;
        *RemoteFilePath       = remoteFilePath;
        remoteFilePath.Buffer = NULL;
    }
    __finally
    {
        if (reparseData != NULL)
        {
            LcFreeNonPagedBuffer(reparseData);
        }

        if (remoteFilePath.Buffer != NULL)
        {
            LcFreeUnicodeString(&remoteFilePath);
        }
    }

    return status;
}

_Check_return_
_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS
LcUntagFile (
    _In_ PCFLT_RELATED_OBJECTS FltObjects,
    _In_ PUNICODE_STRING       FileName
    )
/*++

Summary:

    This function removes the LazyCopy reparse tag and proper attributes from the file given.

Arguments:

    FltObjects - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                 opaque handles to this filter, instance, its associated volume and
                 file object.

    FileName   - Full file name to be opened.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS               status           = STATUS_SUCCESS;
    HANDLE                 fileHandle       = NULL;
    PFILE_OBJECT           fileObject       = NULL;
    OBJECT_ATTRIBUTES      objectAttributes = { 0 };
    IO_STATUS_BLOCK        statusBlock      = { 0 };
    FILE_BASIC_INFORMATION basicInformation = { 0 };
    BOOLEAN                readOnlyFile     = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(FltObjects             != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(FltObjects->Instance   != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(FltObjects->FileObject != NULL, STATUS_INVALID_PARAMETER_1);

    IF_FALSE_RETURN_RESULT(FileName               != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(FileName->Buffer       != NULL, STATUS_INVALID_PARAMETER_2);

    __try
    {
        // Remove read-only flag, so we'll be able to modify file properties.
        NT_IF_FAIL_LEAVE(FltQueryInformationFile(FltObjects->Instance, FltObjects->FileObject, &basicInformation, sizeof(FILE_BASIC_INFORMATION), FileBasicInformation, NULL));
        if (FlagOn(basicInformation.FileAttributes, FILE_ATTRIBUTE_READONLY))
        {
            readOnlyFile = TRUE;

            ClearFlag(basicInformation.FileAttributes, FILE_ATTRIBUTE_READONLY);
            NT_IF_FAIL_LEAVE(FltSetInformationFile(FltObjects->Instance, FltObjects->FileObject, &basicInformation, sizeof(FILE_BASIC_INFORMATION), FileBasicInformation));
        }

        // In order to remove the reparse tag, file should be opened for write.
        InitializeObjectAttributes(&objectAttributes, FileName, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);

        // Create file breaking OpLock, if any.
        NT_IF_FAIL_LEAVE(FltCreateFileEx(
            FltObjects->Filter,
            FltObjects->Instance,
            &fileHandle,
            &fileObject,
            FILE_WRITE_ATTRIBUTES | FILE_WRITE_EA,
            &objectAttributes,
            &statusBlock,
            0,
            FILE_ATTRIBUTE_NORMAL,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            FILE_OPEN,
            FILE_OPEN_REPARSE_POINT | FILE_OPEN_FOR_BACKUP_INTENT | FILE_NON_DIRECTORY_FILE | FILE_COMPLETE_IF_OPLOCKED,
            NULL,
            0,
            IO_IGNORE_SHARE_ACCESS_CHECK));

        // Remove reparse tag.
        status = FltUntagFile(FltObjects->Instance, fileObject, LC_REPARSE_TAG, &LC_REPARSE_GUID);
        if (!NT_SUCCESS(status) && status != STATUS_NOT_A_REPARSE_POINT)
        {
            __leave;
        }

        // Remove additional attributes.
        ClearFlag(basicInformation.FileAttributes, FILE_ATTRIBUTE_REPARSE_POINT);
        ClearFlag(basicInformation.FileAttributes, FILE_ATTRIBUTE_OFFLINE);
        ClearFlag(basicInformation.FileAttributes, FILE_ATTRIBUTE_NOT_CONTENT_INDEXED);

        // Restore the read-only attribute value.
        if (readOnlyFile)
        {
            SetFlag(basicInformation.FileAttributes, FILE_ATTRIBUTE_READONLY);
        }

        NT_IF_FAIL_LEAVE(FltSetInformationFile(FltObjects->Instance, fileObject, &basicInformation, sizeof(FILE_BASIC_INFORMATION), FileBasicInformation));
    }
    __finally
    {
        if (fileHandle != NULL)
        {
            FltClose(fileHandle);
            ObfDereferenceObject(fileObject);
        }
    }

    return status;
}
