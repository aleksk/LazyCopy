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

    Fetch.c

Abstract:

    Contains file fetching function definitions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Communication.h"
#include "Fetch.h"
#include "LazyCopyEtw.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Struct definitions.
//------------------------------------------------------------------------

typedef struct _FILE_CHUNK
{
    // Pointer to the preallocated buffer to store data in.
    PVOID            Buffer;

    // Size of the buffer.
    ULONG            BufferSize;

    // Total amount of bytes that buffer contains.
    __volatile ULONG BytesInBuffer;

    // List entry for the current chunk.
    LIST_ENTRY       ListEntry;
} FILE_CHUNK, *PFILE_CHUNK;

//
// Contains custom data passed to the asynchronous write I/O callback routine.
//
typedef struct _WRITE_CALLBACK_CONTEXT
{
    // Event to set after callback finishes.
    PKEVENT             Event;

    // Status of the current I/O operation.
    __volatile NTSTATUS Status;

    // Amount of bytes written to the file.
    __volatile ULONG    BytesWritten;
} WRITE_CALLBACK_CONTEXT, *PWRITE_CALLBACK_CONTEXT;

//------------------------------------------------------------------------
//  Local function prototype declarations.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcOpenFile (
    _In_  PUNICODE_STRING FilePath,
    _Out_ PHANDLE         Handle
    );

static
_Check_return_
NTSTATUS
LcFetchFileByChunks (
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _In_  HANDLE                SourceFileHandle,
    _In_  PLARGE_INTEGER        SourceFileSize,
    _Out_ PLARGE_INTEGER        BytesCopied
    );

static
VOID
LcWriteCallback (
    _In_ PFLT_CALLBACK_DATA CallbackData,
    _In_ PFLT_CONTEXT       Context
    );

static
_Check_return_
NTSTATUS
LcGetNextAvailableChunk (
    _In_     PFLT_INSTANCE  Instance,
    _In_     PLIST_ENTRY    ListHead,
    _Inout_  PFILE_CHUNK*   CurrentChunk,
    _Inout_  PULONG         CurrentListLength,
    _In_     BOOLEAN        ReadOperation,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PLARGE_INTEGER RemainingBytes,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PKEVENT        WriteOperationEvent,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PLARGE_INTEGER WaitTimeout
    );

static
_Check_return_
NTSTATUS
LcInitializeChunksList (
    _In_    PFLT_INSTANCE Instance,
    _In_    PLIST_ENTRY   ListHead,
    _In_    LARGE_INTEGER FileSize,
    _Inout_ PULONG        ListLength
    );

static
VOID
LcClearChunksList (
    _In_ PFLT_INSTANCE Instance,
    _In_ PLIST_ENTRY   ListHead
    );

static
_Check_return_
NTSTATUS
LcAddNewChunk (
    _In_     PFLT_INSTANCE  Instance,
    _In_     PLIST_ENTRY    Entry,
    _In_     PLARGE_INTEGER RemainingBytes,
    _Outptr_ PFILE_CHUNK*   AllocatedChunk,
    _Inout_  PULONG         ListLength
    );

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcFetchRemoteFile)

    // Local functions.
    #pragma alloc_text(PAGE, LcOpenFile)
    #pragma alloc_text(PAGE, LcFetchFileByChunks)
    #pragma alloc_text(PAGE, LcWriteCallback)
    #pragma alloc_text(PAGE, LcGetNextAvailableChunk)
    #pragma alloc_text(PAGE, LcInitializeChunksList)
    #pragma alloc_text(PAGE, LcClearChunksList)
    #pragma alloc_text(PAGE, LcAddNewChunk)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Global variables.
//------------------------------------------------------------------------

// Size of the temporary buffer to read data into.
static const ULONG ChunkSize           = 128 * 1024;

// Maximum amount of chunks to use to copy file.
static const ULONG MaxChunks           = 4;

// I/O operation timeout in milliseconds.
static const ULONG TimeoutMilliseconds = 15000;

//------------------------------------------------------------------------
//  File fetch functions.
//------------------------------------------------------------------------

_Check_return_
_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS
LcFetchRemoteFile (
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _In_  PUNICODE_STRING       SourceFile,
    _Out_ PLARGE_INTEGER        BytesCopied
    )
/*++

Summary:

    This function copies the remote file content to the current file object.

    In order for the remote file to be fetched, make sure that the network redirector
    device is used, i.e. the 'SourceFile' root points to the '\Device\Mup\<path>'.

Arguments:

    FltObjects  - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                  opaque handles to this filter, instance, its associated volume and
                  file object.

    SourceFile  - Path to the file to fetch content from.

    BytesCopied - Pointer to the LARGE_INTEGER structure that receives the amount of bytes copied.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                     status           = STATUS_SUCCESS;
    HANDLE                       sourceFileHandle = NULL;
    IO_STATUS_BLOCK              statusBlock      = { 0 };
    FILE_STANDARD_INFORMATION    standardInfo     = { 0 };
    FILE_END_OF_FILE_INFORMATION eofInfo          = { 0 };

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(FltObjects  != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(SourceFile  != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(BytesCopied != NULL, STATUS_INVALID_PARAMETER_3);

    FLT_ASSERT(KeGetCurrentIrql() == PASSIVE_LEVEL);

    *BytesCopied = RtlConvertLongToLargeInteger(0);

    __try
    {
        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Fetching content from: '%wZ'\n", SourceFile));

        //
        // Open source file and make sure it's not empty.
        //

        NT_IF_FAIL_LEAVE(LcOpenFile(SourceFile, &sourceFileHandle));

        NT_IF_FAIL_LEAVE(ZwQueryInformationFile(sourceFileHandle, &statusBlock, &standardInfo, sizeof(FILE_STANDARD_INFORMATION), FileStandardInformation));
        if (standardInfo.EndOfFile.QuadPart == 0)
        {
            // No need to copy an empty file.
            __leave;
        }

        // Extend the target file, so all readers that wait for the content to be copied will get the actual file size information.
        // Remote file system may return incorrect information, but we are doing it only for the cases, when multiple threads
        // try to access the same file, while we are fetching it.
        eofInfo.EndOfFile.QuadPart = standardInfo.EndOfFile.QuadPart;
        NT_IF_FAIL_LEAVE(FltSetInformationFile(FltObjects->Instance, FltObjects->FileObject, &eofInfo, sizeof(eofInfo), FileEndOfFileInformation));

        //
        // Copy source file contents into the local (target) file.
        //

        NT_IF_FAIL_LEAVE(LcFetchFileByChunks(
            FltObjects,
            sourceFileHandle,
            &standardInfo.EndOfFile,
            BytesCopied));
    }
    __finally
    {
        if (sourceFileHandle != NULL)
        {
            ZwClose(sourceFileHandle);
        }
    }

    return status;
}

//------------------------------------------------------------------------
//  Local functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcOpenFile (
    _In_  PUNICODE_STRING FilePath,
    _Out_ PHANDLE         Handle
    )
/*++

Summary:

    This function tries to open the 'FilePath' given.

    If open operation fails in the current context, it asks for the user-mode service
    to open that file.

Arguments:

    FilePath - Path to the file to open.

    Handle   - Receives handle to the opened file.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS          status           = STATUS_SUCCESS;
    IO_STATUS_BLOCK   statusBlock      = { 0 };
    OBJECT_ATTRIBUTES objectAttributes = { 0 };
    HANDLE            fileHandle       = NULL;

    PAGED_CODE();

    FLT_ASSERT(FilePath != NULL);
    FLT_ASSERT(Handle   != NULL);

    EventWriteFile_Open_Start(NULL, FilePath->Buffer);

    __try
    {
        // The current minifilter instance may not be attached to the target volume,
        // so the ZwOpenFile/ZwReadFile functions should be used here instead of the FltCreateFile/FltReadFile.
        InitializeObjectAttributes(&objectAttributes, FilePath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);

        // Open file for asynchronous reads.
        status = ZwOpenFile(
            &fileHandle,
            GENERIC_READ,
            &objectAttributes,
            &statusBlock,
            FILE_SHARE_READ,
            FILE_NON_DIRECTORY_FILE | FILE_SEQUENTIAL_ONLY);

        // Open operation may fail, if a remote file is opened by a system, which does not have access to the remote share.
        // If this happens, we want the user-mode client to open it for us.
        if (!NT_SUCCESS(status) && status == STATUS_ACCESS_DENIED)
        {
            NTSTATUS notificationStatus = STATUS_SUCCESS;

            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_WARNING_LEVEL, "[LazyCopy] '%wZ' cannot be accessed by the system, trying to use user-mode service instead.\n", FilePath));

            notificationStatus = LcOpenFileInUserMode(FilePath, &fileHandle);

            // Return original status, if error occurred while sending notification to the user-mode client.
            status = notificationStatus == STATUS_PORT_DISCONNECTED || notificationStatus == STATUS_TIMEOUT
                     ? status
                     : notificationStatus;
        }
    }
    __finally
    {
        if (NT_SUCCESS(status))
        {
            *Handle = fileHandle;
            fileHandle = NULL;
        }

        if (fileHandle != NULL)
        {
            ZwClose(fileHandle);
        }
    }

    EventWriteFile_Open_Stop(NULL);

    return status;
}

static
_Check_return_
NTSTATUS
LcFetchFileByChunks (
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _In_  HANDLE                SourceFileHandle,
    _In_  PLARGE_INTEGER        SourceFileSize,
    _Out_ PLARGE_INTEGER        BytesCopied
    )
/*++

Summary:

    This function copies the original file from the 'SourceFileHandle' to the currently opened
    file ('FltObjects->FileObject') by chunks.

    It maintains its own list of chunks, and extends it, if there are no chunks available to read into.
    Write operation goes from one chunk to another in a sequential order. If the next chunk is empty,
    write waits for the read to be completed, and proceeds.

    There are simple rules for chunks allocation:
    1. Up to two chunks are initially allocated:
       a) If the file is smaller than the 'ChunkSize', only one chunk is allocated with buffer
          that is equal to the file size.
       b) If the file is larger than the 'ChunkSize', two chunks are allocated, taking the
          file size into account for the second chunk size.
    2. If all chunks currently allocated are full and awaiting to be written to a disk, and
       the current amount of chunks is lesser than 'MaxChunks', an additional chunk is allocated.

    There is a corner case, when the actual file size differs from the reported one. In this case
    one of the chunks in the list will be smaller than the 'ChunkSize'.
    For example, 'MaxChunks' is 3, ChunkSize is '10', file size reported is '12', and actual file
    size is '25'.
    Two chunks will be initially allocated: [1] 10b; [2] (12-10)=2b.
    Later on, when all of them will be filled in with the data, and EOF will not be received,
    because the actual size is 25b, another chunk [3] of size 10b (ChunkSize) will be allocated.
    In total, there will be three chunks: 10b, 2b, and 10b.
    We don't reallocate the 2nd chunk, because this driver is supposed to be used with a proper
    filesystems, but making this modification might be a valuable TODO item.

    Let's look at how chunks work.
    All chunks are stored in the doubly-linked list. [Head] node doesn't contain any buffer to
    store data in. Refer to the 'FILE_CHUNK' structure for details.
    MSDN about lists: http://msdn.microsoft.com/en-us/library/windows/hardware/ff563802(v=vs.85).aspx

    For large files we will have two chunks from the beginning.
    [Head] <-> [1] <-> [2] <-> [Head]

    There are pointers for [R]ead and [W]rite.

    When the first chunk is being read, the list will look like the following:
    [Head] <-> [1] <-> [2] <-> [Head]
      [W]      [R]

    [W] is awaiting for the [1] to be filled in with the data before writing it to the disk.

    When the [1] chunk is filled with the data:
    [Head] <-> [1*] <-> [2] <-> [Head]
                [W]     [R]

    [1] is full and is being written to a disk, and we're reading into chunk [2].
    Let's also assume that the reads are faster then writes. When the [2] chunk is
    full, there are no free chunks available:
    [Head] <-> [1*] <-> [2*] <-> [Head]
                [W]      [R]

    If the current amount of chunks is lesser than the 'MaxChunks' value, a new chunk will be
    allocated before the next [R] node. In this case it will be added before [Head] to the end
    of the list, and read will continue:
    [Head] <-> [1*] <-> [2*] <-> [3] <-> [Head]
                [W]              [R]

    Then [W] and [R] finish, and [W] moves to the [2] chunk.
    [Head] <-> [1] <-> [2*] <-> [3*] <-> [Head]
                        [W]      [R]

    [R] sees that [1] chunk is available, and reads into it:
    [Head] <-> [1] <-> [2*] <-> [3*] <-> [Head]
               [R]      [W]

    After [R] finishes reading, there are no free chunks again:
    [Head] <-> [1*] <-> [2*] <-> [3*] <-> [Head]
               [R]       [W]

    A new chunk can be allocated again before the [2]:
    [Head] <-> [1*] <-> [4] <-> [2*] <-> [3*] <-> [Head]
                        [R]      [W]

    With this approach, [R] will fill chunks [1]->[2]->[3]->[1]->[4], and write will
    write them in the same order.
    I.e. allocating a new chunk before the next filled chunk (if the amount of chunks
    is lesser than the 'MaxChunks') makes sure that the data is written sequentially,
    and there is no need to constantly seek in the file.

Arguments:

    FltObjects       - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                       opaque handles to this filter, instance, its associated volume and
                       file object.

    SourceFileHandle - Handle to the source file to copy content from.

    SourceFileSize   - Size of the source file.

    BytesCopied      - Pointer to the LARGE_INTEGER structure that receives the amount
                       of bytes copied.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS               status                = STATUS_SUCCESS;

    LIST_ENTRY             chunksListHead        = { 0 };
    ULONG                  chunkListLength       = 0;

    // State of the R/W operations.
    BOOLEAN                readComplete          = FALSE;
    BOOLEAN                writeComplete         = FALSE;
    PFILE_CHUNK            readChunk             = NULL;
    PFILE_CHUNK            writeChunk            = NULL;
    BOOLEAN                eof                   = FALSE;
    BOOLEAN                waitingForRead        = FALSE;
    KEVENT                 writeEvent            = { 0 };

    WRITE_CALLBACK_CONTEXT writeCallbackContext  = { 0 };

    LARGE_INTEGER          waitTimeout           = { 0 };
    LARGE_INTEGER          zeroTimeout           = { 0 };

    IO_STATUS_BLOCK        statusBlock           = { 0 };

    LARGE_INTEGER          remainingBytes        = { 0 };
    LARGE_INTEGER          totalBytesRead        = { 0 };
    LARGE_INTEGER          totalBytesWritten     = { 0 };
    LARGE_INTEGER          sourceFileOffset      = { 0 };
    LARGE_INTEGER          destinationFileOffset = { 0 };

    PAGED_CODE();

    FLT_ASSERT(FltObjects          != NULL);
    FLT_ASSERT(SourceFileHandle    != NULL);
    FLT_ASSERT(SourceFileSize      != NULL);
    FLT_ASSERT(SourceFileSize->QuadPart > 0);
    FLT_ASSERT(BytesCopied         != NULL);
    FLT_ASSERT(KeGetCurrentIrql()  == PASSIVE_LEVEL);

    *BytesCopied = RtlConvertLongToLargeInteger(0);

    __try
    {
        // Set the relative timeout (1 stands for 100 nanoseconds).
        waitTimeout           = RtlConvertLongToLargeInteger(-10000);
        waitTimeout.QuadPart *= TimeoutMilliseconds;

        KeInitializeEvent(&writeEvent, NotificationEvent, TRUE);
        writeCallbackContext.Event = &writeEvent;

        remainingBytes.QuadPart = SourceFileSize->QuadPart;

        NT_IF_FAIL_LEAVE(LcInitializeChunksList(FltObjects->Instance, &chunksListHead, remainingBytes, &chunkListLength));

        for (;;)
        {
            if (waitingForRead)
            {
                // Wait for the read operation to finish.
                NT_IF_FAIL_LEAVE(ZwWaitForSingleObject(SourceFileHandle, FALSE, &waitTimeout));
                readComplete = TRUE;
            }
            else
            {
                readComplete = ZwWaitForSingleObject(SourceFileHandle, FALSE, &zeroTimeout) == STATUS_SUCCESS;
            }

            writeComplete = KeReadStateEvent(&writeEvent) != 0;

            if (!eof && readComplete)
            {
                // If it's not the first read, update status of the current chunk.
                if (readChunk != NULL)
                {
                    status = statusBlock.Status;

                    if (NT_SUCCESS(status) || status == STATUS_END_OF_FILE)
                    {
                        ULONG bytesRead = (ULONG)statusBlock.Information;

                        readChunk->BytesInBuffer   = bytesRead;

                        remainingBytes.QuadPart   -= bytesRead;
                        totalBytesRead.QuadPart   += bytesRead;
                        sourceFileOffset.QuadPart += bytesRead;

                        if (status == STATUS_END_OF_FILE || bytesRead < readChunk->BufferSize)
                        {
                            eof    = TRUE;
                            status = STATUS_SUCCESS;

                            // Will not be used later in this case, only to have the proper data here.
                            remainingBytes.QuadPart = 0;
                        }
                    }

                    NT_IF_FAIL_LEAVE(status);
                }

                // Move to the next available chunk and schedule read.
                if (!eof)
                {
                    // If the remote file system returned an invalid file size, when we started reading it,
                    // this value might be negative. Set it to the default, so the newly allocated chunk
                    // will have the maximum allowed size.
                    if (remainingBytes.QuadPart <= 0)
                    {
                        remainingBytes.QuadPart = ChunkSize;
                    }

                    NT_IF_FAIL_LEAVE(LcGetNextAvailableChunk(
                        FltObjects->Instance,
                        &chunksListHead,
                        &readChunk,
                        &chunkListLength,
                        TRUE,              // Read operation.
                        &remainingBytes,
                        &writeEvent,
                        &waitTimeout));

                    // Schedule read operation for the current chunk.
                    status = ZwReadFile(
                        SourceFileHandle,
                        NULL,
                        NULL,
                        NULL,
                        &statusBlock,
                        readChunk->Buffer,
                        readChunk->BufferSize,
                        &sourceFileOffset,
                        NULL);

                    NT_IF_FALSE_LEAVE(status == STATUS_PENDING || status == STATUS_SUCCESS, status);
                }
            }

            if (writeComplete)
            {
                if (!waitingForRead)
                {
                    // If it's not the first write, update status of the current chunk.
                    if (writeChunk != NULL)
                    {
                        NT_IF_FAIL_LEAVE(writeCallbackContext.Status);

                        writeChunk->BytesInBuffer       = 0;
                        totalBytesWritten.QuadPart     += writeCallbackContext.BytesWritten;
                        destinationFileOffset.QuadPart += writeCallbackContext.BytesWritten;
                    }

                    NT_IF_FAIL_LEAVE(LcGetNextAvailableChunk(
                        FltObjects->Instance,
                        &chunksListHead,
                        &writeChunk,
                        &chunkListLength,
                        FALSE,             // Write operation.
                        NULL,
                        NULL,
                        NULL));
                }

                waitingForRead = FALSE;

                // If we don't have any data in the current chunk, restart from the beginning of the loop.
                if (writeChunk->BytesInBuffer == 0)
                {
                    if (eof)
                    {
                        // We're done!
                        break;
                    }
                    else
                    {
                        // Since we're waiting for the read to complete for the current chunk,
                        // don't change the chunk position on next iteration.
                        waitingForRead = TRUE;

                        continue;
                    }
                }

                KeClearEvent(&writeEvent);

                NT_IF_FAIL_LEAVE(FltWriteFile(
                    FltObjects->Instance,
                    FltObjects->FileObject,
                    &destinationFileOffset,
                    writeChunk->BytesInBuffer,
                    writeChunk->Buffer,
                    FLTFL_IO_OPERATION_DO_NOT_UPDATE_BYTE_OFFSET,
                    NULL,
                    (PFLT_COMPLETED_ASYNC_IO_CALLBACK)&LcWriteCallback,
                    &writeCallbackContext));
            }
        }

        *BytesCopied = totalBytesWritten;
    }
    __finally
    {
        LcClearChunksList(FltObjects->Instance, &chunksListHead);
    }

    return status;
}

static
VOID
LcWriteCallback (
    _In_ PFLT_CALLBACK_DATA CallbackData,
    _In_ PFLT_CONTEXT       Context
    )
/*++

Summary:

    This function is a completion callback for the asynchronous I/O write operation.

Arguments:

    CallbackData - Pointer to the callback data structure for the I/O operation.

    Context      - Custom context pointer.

Return Value:

    None.

--*/
{
    PWRITE_CALLBACK_CONTEXT context = (PWRITE_CALLBACK_CONTEXT)Context;

    FLT_ASSERT(CallbackData != NULL);
    FLT_ASSERT(Context      != NULL);

    PAGED_CODE();

    context->Status       = CallbackData->IoStatus.Status;
    context->BytesWritten = NT_SUCCESS(context->Status)
                            ? (ULONG)CallbackData->IoStatus.Information
                            : 0;

    // Notify listeners that write operation has completed for the current chunk.
    KeSetEvent(context->Event, IO_NO_INCREMENT, FALSE);
}

static
_Check_return_
NTSTATUS
LcGetNextAvailableChunk (
    _In_     PFLT_INSTANCE  Instance,
    _In_     PLIST_ENTRY    ListHead,
    _Inout_  PFILE_CHUNK*   CurrentChunk,
    _Inout_  PULONG         CurrentListLength,
    _In_     BOOLEAN        ReadOperation,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PLARGE_INTEGER RemainingBytes,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PKEVENT        WriteOperationEvent,
    _When_(ReadOperation,  _In_)
    _When_(!ReadOperation, _In_opt_)
             PLARGE_INTEGER WaitTimeout
    )
/*++

Summary:

    This function gets the next chunk (after the 'CurrentChunk') from the 'ListHead' list.

    If the current operation is read ('ReadOperation' is set to TRUE), and the next chunk
    in the list is not empty, it checks, whether a new chunk can be allocated.
    If true, a new chunk is allocated and returned; otherwise, this function waits
    for the write operation to complete before returning.

Arguments:

    Instance            - Opaque instance pointer for a caller-owned minifilter driver
                          instance that is attached to the volume.

    ListHead            - Pointer to the list head element.

    CurrentChunk        - Pointer to a pointer to the current chunk. May be NULL.
                          Its updated with the new chunk value.

    CurrentListLength   - If the new chunk is allocated, its value is incremented.

    ReadOperation       - Whether the next chunk will be used for read.

    RemainingBytes      - Amount of bytes remaining to read from the file.

    WriteOperationEvent - Synchronization event to wait for in case the next chunk is
                          not yet written, and the 'ReadOperation' is set to TRUE.

    WaitTimeout         - Timeout to wait for the 'WriteOperationEvent'.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS    status = STATUS_SUCCESS;
    PFILE_CHUNK chunk  = NULL;

    PAGED_CODE();

    FLT_ASSERT(Instance           != NULL);
    FLT_ASSERT(ListHead           != NULL);
    FLT_ASSERT(CurrentChunk       != NULL);
    FLT_ASSERT(CurrentListLength  != NULL);
    FLT_ASSERT(*CurrentListLength <= MaxChunks);

    if (ReadOperation)
    {
        FLT_ASSERT(RemainingBytes      != NULL);
        FLT_ASSERT(WriteOperationEvent != NULL);
        FLT_ASSERT(WaitTimeout         != NULL);
    }

    // Chunks list should have already been initialized.
    FLT_ASSERT(!IsListEmpty(ListHead));

    __try
    {
        if (*CurrentChunk == NULL)
        {
            chunk = CONTAINING_RECORD(ListHead->Flink, FILE_CHUNK, ListEntry);
        }
        else
        {
            // Go to the next chunk in the list.
            // This will move to the first element, if the current element is the last one in the list.
            chunk = (*CurrentChunk)->ListEntry.Flink == ListHead
                    ? CONTAINING_RECORD(ListHead->Flink, FILE_CHUNK, ListEntry)
                    : CONTAINING_RECORD((*CurrentChunk)->ListEntry.Flink, FILE_CHUNK, ListEntry);
        }

        if (ReadOperation)
        {
            // If data from the current chunk is not yet written to the output file.
            if (chunk->BytesInBuffer != 0)
            {
                // Check, whether we can extend the list.
                if (*CurrentListLength < MaxChunks)
                {
                    NT_IF_FAIL_LEAVE(LcAddNewChunk(Instance, &chunk->ListEntry, RemainingBytes, &chunk, CurrentListLength));
                }
                else
                {
                    // If we cannot extend the current list, wait for the pending write operation to complete.
                    NT_IF_FAIL_LEAVE(KeWaitForSingleObject(WriteOperationEvent, Executive, KernelMode, FALSE, WaitTimeout));
                }
            }
        }

        *CurrentChunk = chunk;
    }
    __finally
    {
        // Do nothing.
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcInitializeChunksList (
    _In_    PFLT_INSTANCE Instance,
    _In_    PLIST_ENTRY   ListHead,
    _In_    LARGE_INTEGER FileSize,
    _Inout_ PULONG        ListLength
    )
/*++

Summary:

    This function initializes the 'ListHead' list given and preallocates one or more
    chunks, depending on the 'FileSize' given.

    If the 'FileSize' is lesser than the global 'ChunkSize', one buffer is enough
    to fit the file contents in, and no other chunks are to be allocated.

Arguments:

    Instance   - Opaque instance pointer for a caller-owned minifilter driver
                 instance that is attached to the volume.

    ListHead   - Pointer to a head of the list that should be initialized.

    FileSize   - Size of the file to be copied.

    ListLength - Receives the amount of chunks allocated.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS    status = STATUS_SUCCESS;
    ULONG       idx    = 0;
    PFILE_CHUNK chunk  = NULL;

    PAGED_CODE();

    FLT_ASSERT(Instance   != NULL);
    FLT_ASSERT(ListHead   != NULL);
    FLT_ASSERT(ListLength != NULL);
    FLT_ASSERT(FileSize.QuadPart > 0);

    *ListLength = 0;

    InitializeListHead(ListHead);

    __try
    {
        // Up to two chunks should be allocated.
        for (idx = 0; idx < 2; idx++)
        {
            // This will add a new chunk before the ListHead entry, which means that the
            // chunk will be added to the end of the list.
            NT_IF_FAIL_LEAVE(LcAddNewChunk(Instance, ListHead, &FileSize, &chunk, ListLength));

            // Skip, if one chunk is enough to fit the current file contents in.
            FileSize.QuadPart -= chunk->BufferSize;
            if (FileSize.QuadPart <= 0)
            {
                break;
            }
        }
    }
    __finally
    {
        if (!NT_SUCCESS(status))
        {
            LcClearChunksList(Instance, ListHead);
            *ListLength = 0;
        }
    }

    return status;
}

static
VOID
LcClearChunksList (
    _In_ PFLT_INSTANCE Instance,
    _In_ PLIST_ENTRY   ListHead
    )
/*++

Summary:

    This function clears memory allocated for file chunks list.

Arguments:

    Instance - Opaque instance pointer for a caller-owned minifilter driver
               instance that is attached to the volume.

    ListHead - Pointer to the list head.

Return Value:

    None.

--*/
{
    PLIST_ENTRY listEntry = NULL;
    PFILE_CHUNK chunk     = NULL;

    PAGED_CODE();

    FLT_ASSERT(Instance != NULL);
    FLT_ASSERT(ListHead != NULL);

    // Remove the last element from the list, while it's not empty.
    while ((listEntry = RemoveTailList(ListHead)) != ListHead)
    {
        chunk = CONTAINING_RECORD(listEntry, FILE_CHUNK, ListEntry);

        if (chunk->Buffer != NULL)
        {
            LcFreeNonPagedAlignedBuffer(Instance, chunk->Buffer);
        }

        LcFreeNonPagedBuffer(chunk);
    }

    return;
}

static
_Check_return_
NTSTATUS
LcAddNewChunk (
    _In_     PFLT_INSTANCE  Instance,
    _In_     PLIST_ENTRY    Entry,
    _In_     PLARGE_INTEGER RemainingBytes,
    _Outptr_ PFILE_CHUNK*   AllocatedChunk,
    _Inout_  PULONG         ListLength
    )
/*++

Summary:

    This function allocates memory for a new FILE_CHUNK structure and adds it
    before the 'Entry'.

    If the 'RemainingBytes' is smaller than the global 'ChunkSize', the
    buffer of the 'RemaningBytes' size will be allocated for the chunk.

Arguments:

    Instance       - Opaque instance pointer for a caller-owned minifilter driver
                     instance that is attached to the volume.

    Entry          - The newly allocated chunk will be added before this list entry.

    RemainingBytes - Amount of bytes remaining to be copied.

    AllocatedChunk - Receives the pointer to the allocated chunk.

    ListLength     - If the chunk was successfully allocated, this method increases
                     the value of the variable this pointer points to.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS    status     = STATUS_SUCCESS;
    PFILE_CHUNK newChunk   = NULL;
    PLIST_ENTRY prevEntry  = NULL;
    ULONG       bufferSize = 0;

    PAGED_CODE();

    FLT_ASSERT(Entry          != NULL);
    FLT_ASSERT(AllocatedChunk != NULL);
    FLT_ASSERT(ListLength     != NULL);
    FLT_ASSERT(RemainingBytes != NULL);
    FLT_ASSERT(RemainingBytes->QuadPart > 0);

    *AllocatedChunk = NULL;

    __try
    {
        // We don't want to allocate more memory, than needed.
        bufferSize = (ULONG)min(ChunkSize, (ULONG)RemainingBytes->QuadPart);

        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&newChunk, sizeof(FILE_CHUNK)));
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedAlignedBuffer(Instance, (PVOID*)&newChunk->Buffer, bufferSize));
        newChunk->BufferSize = bufferSize;

        // Add the chunk to the list.
        prevEntry = Entry->Blink;

        newChunk->ListEntry.Flink = Entry;
        newChunk->ListEntry.Blink = prevEntry;

        Entry->Blink     = &newChunk->ListEntry;
        prevEntry->Flink = &newChunk->ListEntry;

        *AllocatedChunk = newChunk;
        (*ListLength)++;

        // Don't cleanup the allocated chunk.
        newChunk = NULL;
    }
    __finally
    {
        if (newChunk != NULL)
        {
            if (newChunk->Buffer)
            {
                LcFreeNonPagedAlignedBuffer(Instance, newChunk->Buffer);
            }

            LcFreeNonPagedBuffer(newChunk);
        }
    }

    return status;
}
