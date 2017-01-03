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

    Utilities.c

Abstract:

    Contains common helper functions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Utilities.h"

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcAllocateBuffer)
    #pragma alloc_text(PAGE, LcAllocateNonPagedBuffer)
    #pragma alloc_text(PAGE, LcAllocateNonPagedAlignedBuffer)
    #pragma alloc_text(PAGE, LcFreeBuffer)
    #pragma alloc_text(PAGE, LcFreeNonPagedBuffer)
    #pragma alloc_text(PAGE, LcFreeNonPagedAlignedBuffer)
    #pragma alloc_text(PAGE, LcAllocateResource)
    #pragma alloc_text(PAGE, LcFreeResource)
    #pragma alloc_text(PAGE, LcAllocateUnicodeString)
    #pragma alloc_text(PAGE, LcCopyUnicodeString)
    #pragma alloc_text(PAGE, LcFreeUnicodeString)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Memory allocation/freeing functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateBuffer(
    _Outptr_result_buffer_(Size) PVOID*    Buffer,
    _In_                         POOL_TYPE PoolType,
    _In_                         SIZE_T    Size,
    _In_                         ULONG     Tag
    )
/*++

Summary:

    This function allocates a buffer of a given size from a pool specified.

Arguments:

    Buffer   - A pointer to a variable that receives the buffer allocated.

    PoolType - Type of the pool to allocate memory from.
               Must be 'PagedPool' or 'NonPagedPoolNx'.

    Size     - Supplies the size of the buffer to be allocated.

    Tag      - Pool tag for the allocated memory.

Return value:

    STATUS_SUCCESS                - Success.
    STATUS_INSUFFICIENT_RESOURCES - Failure. Unable to allocate memory.
    STATUS_INVALID_PARAMETER_1    - Failure. The 'Buffer' parameter is NULL.
    STATUS_INVALID_PARAMETER_2    - Failure. The 'PoolType' parameter is not 'PagedPool' or 'NonPagedPoolNx'.
    STATUS_INVALID_PARAMETER_3    - Failure. The 'Size' parameter is equal to zero.

--*/
{
    PVOID allocatedBuffer = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Buffer != NULL,                                      STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(PoolType == PagedPool || PoolType == NonPagedPoolNx, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(Size != 0,                                           STATUS_INVALID_PARAMETER_3);

    allocatedBuffer = ExAllocatePoolWithTag(PoolType, Size, Tag);
    IF_FALSE_RETURN_RESULT(allocatedBuffer != NULL, STATUS_INSUFFICIENT_RESOURCES);

    // Zero buffer and set the result value.
    RtlZeroMemory(allocatedBuffer, Size);
    *Buffer = allocatedBuffer;

    return STATUS_SUCCESS;
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateNonPagedBuffer(
    _Outptr_result_buffer_(Size) PVOID* Buffer,
    _In_                         SIZE_T Size
    )
/*++

Summary:

    This function allocates a buffer from the non-paged pool.

Arguments:

    Buffer - A pointer to a variable that receives the buffer allocated.

    Size   - Supplies the size of the buffer to be allocated.

Return value:

    STATUS_SUCCESS                - Success.
    STATUS_INSUFFICIENT_RESOURCES - Failure. Unable to allocate memory.
    STATUS_INVALID_PARAMETER_1    - Failure. The 'Buffer' parameter is NULL.
    STATUS_INVALID_PARAMETER_2    - Failure. The 'Size' parameter is equal to zero.

--*/
{
    PVOID allocatedBuffer = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Buffer != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Size   != 0,    STATUS_INVALID_PARAMETER_2);

    allocatedBuffer = ExAllocatePoolWithTag(NonPagedPoolNx, Size, LC_BUFFER_NON_PAGED_POOL_TAG);
    IF_FALSE_RETURN_RESULT(allocatedBuffer != NULL, STATUS_INSUFFICIENT_RESOURCES);

    // Zero buffer and set the result value.
    RtlZeroMemory(allocatedBuffer, Size);
    *Buffer = allocatedBuffer;

    return STATUS_SUCCESS;
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateNonPagedAlignedBuffer(
    _In_                         PFLT_INSTANCE Instance,
    _Outptr_result_buffer_(Size) PVOID*        Buffer,
    _In_                         SIZE_T        Size
    )
/*++

Summary:

    This function allocates a device-aligned buffer from non-paged pool
    for use in a noncached I/O operations.

Arguments:

    Instance - Opaque instance pointer for a caller-owned minifilter driver
               instance that is attached to the volume.

    Buffer   - A pointer to a variable that receives the buffer allocated.

    Size     - Supplies the size of the buffer to be allocated.

Return value:

    STATUS_SUCCESS                - Success.
    STATUS_INSUFFICIENT_RESOURCES - Failure. Unable to allocate memory.
    STATUS_INVALID_PARAMETER_1    - Failure. The 'Instance' parameter is NULL.
    STATUS_INVALID_PARAMETER_2    - Failure. The 'Buffer' parameter is NULL.
    STATUS_INVALID_PARAMETER_3    - Failure. The 'Size' parameter is equal to zero.

--*/
{
    PVOID allocatedBuffer = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Instance != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Buffer   != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(Size     != 0,    STATUS_INVALID_PARAMETER_3);

    allocatedBuffer = FltAllocatePoolAlignedWithTag(Instance, NonPagedPoolNx, Size, LC_BUFFER_NON_PAGED_POOL_TAG);
    IF_FALSE_RETURN_RESULT(allocatedBuffer != NULL, STATUS_INSUFFICIENT_RESOURCES);

    // Zero buffer and set the result value.
    RtlZeroMemory(allocatedBuffer, Size);
    *Buffer = allocatedBuffer;

    return STATUS_SUCCESS;
}

//------------------------------------------------------------------------

VOID
LcFreeBuffer(
    _Inout_ PVOID Buffer,
    _In_    ULONG Tag
    )
/*++

Summary:

    This function frees the buffer previously allocated using the 'LcAllocateBuffer' function.

Arguments:

    Buffer - Supplies the buffer to be freed. If it's NULL, this function will do nothing.

    Tag    - Pool tag.

Return value:

    None.

--*/
{
    PAGED_CODE();

    IF_FALSE_RETURN(Buffer != NULL);

    ExFreePoolWithTag(Buffer, Tag);
}

//------------------------------------------------------------------------

VOID
LcFreeNonPagedBuffer(
    _Inout_ PVOID Buffer
    )
/*++

Summary:

    This function frees the buffer in the non-paged pool that previously allocated using
    'LcAllocateNonPagedBuffer' function.

Arguments:

    Buffer - Supplies the buffer to be freed. If it's NULL, this function will do nothing.
             NOTE:  In order for this function to work properly, the buffer given must be
                    allocated using the 'LcAllocateNonPagedBuffer' function.

Return value:

    None.

--*/
{
    PAGED_CODE();

    IF_FALSE_RETURN(Buffer != NULL);

    ExFreePoolWithTag(Buffer, LC_BUFFER_NON_PAGED_POOL_TAG);
}

//------------------------------------------------------------------------

VOID
LcFreeNonPagedAlignedBuffer(
    _In_    PFLT_INSTANCE Instance,
    _Inout_ PVOID         Buffer
    )
/*++

Summary:

    This function frees the cache-aligned buffer in the non-paged pool that previously allocated using
    'LcAllocateNonPagedAlignedBuffer' function.

Arguments:

    Instance - Opaque instance pointer for a caller-owned minifilter driver instance that
               is attached to the volume.

    Buffer   - Supplies the buffer to be freed. If it's NULL, this function will do nothing.
               NOTE:  In order for this function to work properly, the buffer given must be
                      allocated using the 'LcAllocateNonPagedAlignedBuffer' function.

Return value:

    None.

--*/
{
    PAGED_CODE();

    IF_FALSE_RETURN(Instance != NULL);
    IF_FALSE_RETURN(Buffer   != NULL);

    FltFreePoolAlignedWithTag(Instance, Buffer, LC_BUFFER_NON_PAGED_POOL_TAG);
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateResource(
    _Outptr_ PERESOURCE* Resource
    )
/*++

Summary:

    This function allocates a new ERESOURCE from the non-paged pool and initializes it.

Arguments:

    Resource - Supplies pointer to the PERESOURCE to be created.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS   status         = STATUS_SUCCESS;
    PERESOURCE resource       = NULL;
    BOOLEAN    deleteResource = FALSE;
    BOOLEAN    freeBuffer     = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Resource != NULL, STATUS_INVALID_PARAMETER_1);

    __try
    {
        status = LcAllocateBuffer((PVOID*)&resource, NonPagedPoolNx, sizeof(ERESOURCE), LC_ERESOURCE_NON_PAGED_POOL_TAG);
        if (!NT_SUCCESS(status))
        {
            freeBuffer = TRUE;
            __leave;
        }

        status = ExInitializeResourceLite(resource);
        if (!NT_SUCCESS(status))
        {
            deleteResource = TRUE;
            __leave;
        }

        *Resource = resource;
    }
    __finally
    {
        if (!NT_SUCCESS(status))
        {
            if (deleteResource)
            {
                ExDeleteResourceLite(resource);
            }

            if (freeBuffer)
            {
                LcFreeBuffer(resource, LC_ERESOURCE_NON_PAGED_POOL_TAG);
            }
        }
    }

    return status;
}

//------------------------------------------------------------------------

VOID
LcFreeResource(
    _In_ PERESOURCE Resource
    )
/*++

Summary:

    This function frees the PERESOURCE previously allocated using the 'LcAllocateResource' function.

Arguments:

    Resource - Supplies pointer to the ERESOURCE to be freed.
               If it's NULL, this function will do nothing.
               NOTE:  In order for this function to work properly, the resource given must be
                      allocated using the 'LcAllocateResource' function.

Return value:

    None.

--*/
{
    PAGED_CODE();

    IF_FALSE_RETURN(Resource != NULL);

    ExDeleteResourceLite(Resource);
    LcFreeBuffer(Resource, LC_ERESOURCE_NON_PAGED_POOL_TAG);
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateUnicodeString(
    _Inout_ PUNICODE_STRING String,
    _In_    USHORT          Size
    )
/*++

Summary:

    This function allocates a unicode string from the non-paged pool.

    The allocated memory is zeroed and assigned to the 'String->Buffer', 'String->Length' is set to zero,
    and the 'String->MaximumLength' is set to 'Size' given.

Arguments:

    String - Pointer to unicode string to allocate buffer for.

    Size   - Size, in bytes, of the buffer to be allocated.

Return value:

    STATUS_SUCCESS                - Success.
    STATUS_INSUFFICIENT_RESOURCES - Failure. Unable to allocate memory.
    STATUS_INVALID_PARAMETER_1    - Failure. If the 'String' parameter is NULL or the 'String->Buffer' is NOT NULL.
    STATUS_INVALID_PARAMETER_2    - Failure. If the 'Size' value is equal to zero or the 'Size % sizeof(WCHAR)' is not equal to zero.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(String != NULL,            STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(String->Buffer == NULL,    STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Size != 0,                 STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(Size % sizeof(WCHAR) == 0, STATUS_INVALID_PARAMETER_2);

    NT_IF_FAIL_RETURN(LcAllocateBuffer((PVOID*)&String->Buffer, NonPagedPoolNx, Size, LC_STRING_NON_PAGED_POOL_TAG));

    String->Length        = 0;
    String->MaximumLength = Size;

    // The buffer allocated should be freed by the caller.

    return status;
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcCopyUnicodeString(
    _Inout_ PUNICODE_STRING  DestinationString,
    _In_    PCUNICODE_STRING SourceString
    )
/*++

Summary:

    This function copies a source string to a destination string.
    Before copying, buffer is allocated for the 'DestinationString' to store
    the source string data.

Arguments:

    DestinationString - Pointer to unicode string to copy source string into.
                        It must NOT have the allocated buffer, because this
                        function allocates a new buffer for it.

    SourceString      - Source string, which contents should be copied.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(DestinationString != NULL,                          STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(DestinationString->Buffer == NULL,                  STATUS_INVALID_PARAMETER_1);

    IF_FALSE_RETURN_RESULT(SourceString->Buffer != NULL,                       STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(SourceString)), STATUS_INVALID_PARAMETER_2);

    NT_IF_FAIL_RETURN(LcAllocateUnicodeString(DestinationString, min(SourceString->Length, SourceString->MaximumLength) + sizeof(WCHAR)));

    #pragma warning(suppress: __WARNING_INVALID_PARAM_VALUE_1) // '_Param_(1)->Buffer' could be '0'.
    RtlCopyUnicodeString(DestinationString, SourceString);

    return status;
}

//------------------------------------------------------------------------

VOID
LcFreeUnicodeString(
    _Inout_ PUNICODE_STRING UniString
    )
/*++

Summary:

    This function frees the unicode string previously allocated using the 'LcAllocateUnicodeString' function.

Arguments:

    UniString - Supplies the string to be freed.
                If it's NULL, this function will do nothing.
                NOTE:  In order for this function to work properly, the string given must be
                       allocated using the 'LcAllocateUnicodeString' function.

Return value:

    None.

--*/
{
    PAGED_CODE();

    IF_FALSE_RETURN(UniString != NULL);

    LcFreeBuffer(UniString->Buffer, LC_STRING_NON_PAGED_POOL_TAG);

    UniString->Buffer        = NULL;
    UniString->Length        = 0;
    UniString->MaximumLength = 0;
}
