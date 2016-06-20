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

    Operations.c

Abstract:

    This file contains callbacks for IRP operations.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Communication.h"
#include "Configuration.h"
#include "Context.h"
#include "Fetch.h"
#include "FileLocks.h"
#include "LazyCopyDriver.h"
#include "ReparsePoints.h"
#include "Utilities.h"

// See the 'LazyCopyDriver.c' for details.
#include "LazyCopyEtw.h"

//------------------------------------------------------------------------
//  Struct definitions.
//------------------------------------------------------------------------

//
// This structure is passed from the pre- to post-operation callback.
//
typedef struct _CREATE_COMPLETION_CONTEXT
{
    // File name information.
    PFLT_FILE_NAME_INFORMATION NameInfo;

    // Current operation mode.
    DRIVER_OPERATION_MODE      OperationMode;

    // File access notification report rate for this file.
    ULONG                      ReportRate;
} CREATE_COMPLETION_CONTEXT, *PCREATE_COMPLETION_CONTEXT;

//------------------------------------------------------------------------
//  Local function prototype declarations.
//------------------------------------------------------------------------

static
VOID
LcEtwFileAccessed (
    _In_ ULONG            ReportRate,
    _In_ PCUNICODE_STRING Path,
    _In_ ULONG            CreateOptions
    );

static
_Check_return_
NTSTATUS
LcGetFileNameInformation (
    _Inout_  PFLT_CALLBACK_DATA          Data,
    _Outptr_ PFLT_FILE_NAME_INFORMATION* NameInformation
    );

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    // Functions that track operations on the volume.
    #pragma alloc_text(PAGE, PreCreateOperationCallback)
    #pragma alloc_text(PAGE, PostCreateOperationCallback)
    #pragma alloc_text(PAGE, PreReadWriteOperationCallback)
    #pragma alloc_text(PAGE, PreQueryInformationOperationCallback)
    #pragma alloc_text(PAGE, PostQueryInformationOperationCallback)
    #pragma alloc_text(PAGE, PostDirectoryControlOperationCallback)

    // Local functions.
    #pragma alloc_text(PAGE, LcEtwFileAccessed)
    #pragma alloc_text(PAGE, LcGetFileNameInformation)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Global variables.
//------------------------------------------------------------------------

// The current driver adds the following creation options to the file open operations,
// if the current file should be fetched.
static const ULONG  DefaultCreateOptions = FILE_OPEN_REPARSE_POINT | FILE_OPEN_FOR_BACKUP_INTENT | FILE_RANDOM_ACCESS | FILE_WRITE_THROUGH;
static const USHORT DefaultShareAccess   = FILE_SHARE_READ | FILE_SHARE_WRITE;

//------------------------------------------------------------------------
//  Functions that track operations on the volume.
//------------------------------------------------------------------------

FLT_PREOP_CALLBACK_STATUS
PreCreateOperationCallback (
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    )
/*++

Summary:

    This function is invoked before the 'IRP_MJ_CREATE' for this minifilter driver.

    In this method we check, whether the open operation can be possibly handled by
    the current driver. If so, the post-operation callback will be scheduled for
    execution.

Arguments:

    Data              - Pointer to the filter's callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The context for the completion function for this operation.

Return value:

    The return value is the status of the operation.

--*/
{
    FLT_PREOP_CALLBACK_STATUS  callbackStatus    = FLT_PREOP_SUCCESS_NO_CALLBACK;
    NTSTATUS                   status            = STATUS_SUCCESS;
    PCREATE_COMPLETION_CONTEXT completionContext = NULL;
    DRIVER_OPERATION_MODE      operationMode     = DriverDisabled;
    ULONG                      createOptions     = 0;
    ULONG                      createDisposition = 0;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(FltObjects);

    // We are only registered for the IRP_MJ_CREATE.
    FLT_ASSERT(Data                      != NULL);
    FLT_ASSERT(Data->Iopb                != NULL);
    FLT_ASSERT(Data->Iopb->MajorFunction == IRP_MJ_CREATE);

    FltAcquireResourceShared(Globals.Lock);

    __try
    {
        createOptions     = Data->Iopb->Parameters.Create.Options & 0xFFFFFF;
        createDisposition = Data->Iopb->Parameters.Create.Options >> 24;

        // Don't invoke callback for the non-file objects.
        if (Data->Iopb->TargetFileObject == NULL)
        {
            __leave;
        }

        // We don't want to affect:
        // - Directories;
        // - Open by ID operations (it is not possible to determine create path intent);
        // - Volume open operations;
        // - Paging I/O.
        if (FlagOn(createOptions,                    FILE_DIRECTORY_FILE)
            || FlagOn(createOptions,                 FILE_OPEN_BY_FILE_ID)
            || FlagOn(FltObjects->FileObject->Flags, FO_VOLUME_OPEN)
            || FlagOn(Data->Iopb->OperationFlags,    SL_OPEN_PAGING_FILE)
            || FlagOn(Data->Iopb->IrpFlags,          IRP_PAGING_IO)
            || FlagOn(Data->Iopb->IrpFlags,          IRP_SYNCHRONOUS_PAGING_IO))
        {
            __leave;
        }

        // Ignore minifilter-generated I/O.
        if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_GENERATED_IO)
            || FLT_IS_FS_FILTER_OPERATION(Data)
            || FLT_IS_REISSUED_IO(Data))
        {
            __leave;
        }

        // Check file create disposition.
        if (createDisposition == FILE_CREATE)
        {
            __leave;
        }

        operationMode = LcGetOperationMode();
        if (operationMode == DriverDisabled)
        {
            __leave;
        }

        // Let the trusted processes do whatever they want with the current file.
        if (LcIsProcessTrusted(PsGetThreadProcessId(Data->Thread)))
        {
            // If the trusted process is trying to access the current file, make sure it's opened
            // with the flags allowing it to read and write to the file.
            //
            // NOTE: You may want to disable this feature, if your scenario relies on files to be
            // blocked for access at some point.
            if ((Data->Iopb->Parameters.Create.Options & DefaultCreateOptions)      != DefaultCreateOptions
                || (Data->Iopb->Parameters.Create.ShareAccess & DefaultShareAccess) != DefaultShareAccess)
            {
                Data->Iopb->Parameters.Create.Options     |= DefaultCreateOptions;
                Data->Iopb->Parameters.Create.ShareAccess |= DefaultShareAccess;

                // Note: Don't reissue the I/O here.
                FltSetCallbackDataDirty(Data);
            }

            __leave;
        }

        // Allocate context to be passed to the post-operation callback.
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&completionContext, sizeof(CREATE_COMPLETION_CONTEXT)));

        // Fill in the completion context fields.
        NT_IF_FAIL_LEAVE(LcGetFileNameInformation(Data, &completionContext->NameInfo));
        completionContext->ReportRate    = FlagOn(operationMode, WatchEnabled) ? LcGetReportRateForPath(&completionContext->NameInfo->Name) : 0;
        completionContext->OperationMode = operationMode;

        *CompletionContext = completionContext;
        completionContext  = NULL;
        callbackStatus     = FLT_PREOP_SUCCESS_WITH_CALLBACK;
    }
    __finally
    {
        FltReleaseResource(Globals.Lock);

        if (completionContext != NULL)
        {
            if (completionContext->NameInfo != NULL)
            {
                FltReleaseFileNameInformation(completionContext->NameInfo);
            }

            LcFreeNonPagedBuffer(completionContext);
        }
    }

    return callbackStatus;
}

FLT_POSTOP_CALLBACK_STATUS
PostCreateOperationCallback (
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    )
/*++

Summary:

    This function is invoked after the 'IRP_MJ_CREATE' was processed by the low-level drivers.

    If the file create request is not failing and the reparse tag is valid, this function will
    read the reparse point data and set a new stream context on the file, if it is not already
    there.

    If there is a context set for a file, when read/write callbacks are executed, file will be
    fetched.

    If it is impossible to set the stream context, the I/O operation will be cancelled with the
    corresponding status code.

Parameters:

    Data              - Pointer to the filter callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The completion context set in the pre-operation function.

    Flags             - Denotes whether the completion is successful or is being drained.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                   status            = STATUS_SUCCESS;

    // Completion context received from the pre-operation callback.
    PCREATE_COMPLETION_CONTEXT completionContext = NULL;

    UNICODE_STRING             remotePath        = { 0 };
    LARGE_INTEGER              fileSize          = { 0 };
    BOOLEAN                    useCustomHandler  = FALSE;
    PLC_STREAM_CONTEXT         streamContext     = NULL;
    BOOLEAN                    contextCreated    = FALSE;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(Flags);

    FLT_ASSERT(Data                      != NULL);
    FLT_ASSERT(Data->Iopb                != NULL);
    FLT_ASSERT(Data->Iopb->MajorFunction == IRP_MJ_CREATE);

    FLT_ASSERT(FltObjects                != NULL);
    FLT_ASSERT(FltObjects->FileObject    != NULL);

    completionContext = (PCREATE_COMPLETION_CONTEXT)CompletionContext;
    IF_FALSE_RETURN_RESULT(completionContext           != NULL, FLT_POSTOP_FINISHED_PROCESSING);
    IF_FALSE_RETURN_RESULT(completionContext->NameInfo != NULL, FLT_POSTOP_FINISHED_PROCESSING);

    __try
    {
        // Leave, if the filter instance is being detached, or the file is opened for deletion.
        if (FlagOn(Flags, FLTFL_POST_OPERATION_DRAINING) || !NT_SUCCESS(Data->IoStatus.Status) || FltObjects->FileObject->DeletePending)
        {
            __leave;
        }

        // Report access operations for non-reparse files, which are accessed by non-trusted processes.
        if (Data->IoStatus.Status != STATUS_REPARSE && FlagOn(completionContext->OperationMode, WatchEnabled))
        {
            LcEtwFileAccessed(completionContext->ReportRate, &completionContext->NameInfo->Name, Data->Iopb->Parameters.Create.Options);
        }

        // Skip, if the file was opened by another driver or if the unsupported reparse point is found.
        if (Data->IoStatus.Status != STATUS_REPARSE || Data->TagData == NULL || Data->TagData->FileTag != LC_REPARSE_TAG)
        {
            __leave;
        }

        // Don't fetch the file, if we're not configured for it.
        if (!FlagOn(completionContext->OperationMode, FetchEnabled))
        {
            __leave;
        }

        // Don't fetch, if non-default stream is opened.
        if (completionContext->NameInfo->Stream.Length != 0)
        {
            UNICODE_STRING dataStreamName = CONSTANT_STRING(L"::$DATA");

            // It is possible to open the default data stream and yet
            // still have a stream name. This is done by appending
            // ::$DATA to the end of the file name. So if that is the
            // name of our stream, this is really the default stream.
            // Otherwise, it is an alternate stream.
            if (RtlCompareUnicodeString(&(completionContext->NameInfo->Stream), &dataStreamName, TRUE) != 0)
            {
                __leave;
            }
        }

        // Reopen the current file, so the open operation succeeds.
        //
        // NOTE: Sharing access is also overwritten.
        // We fetch in Read/Write operation callback, so it might be possible to two applications to open the same
        // LazyCopy file with no sharing access. In this case the first read/write will fetch the file, and those
        // apps will continue to work.
        // Without overwriting the sharing access, our user-mode client will not be able to easily work with these files.
        // This driver is designed to work as a fetcher for binary files that are not usually simultaneously written to.
        // If you feel that sharing access is important, consider disabling it and not accessing these files from the service.
        if ((Data->Iopb->Parameters.Create.Options & DefaultCreateOptions)      != DefaultCreateOptions
            || (Data->Iopb->Parameters.Create.ShareAccess & DefaultShareAccess) != DefaultShareAccess)
        {
            Data->Iopb->Parameters.Create.Options     |= DefaultCreateOptions;
            Data->Iopb->Parameters.Create.ShareAccess |= DefaultShareAccess;

            FltSetCallbackDataDirty(Data);
            FltReissueSynchronousIo(FltObjects->Instance, Data);
            NT_IF_FAIL_LEAVE(Data->IoStatus.Status);
        }

        // If the file have been created or overwritten, untag it, so it will not be fetched later.
        if (Data->IoStatus.Information    == FILE_CREATED
            || Data->IoStatus.Information == FILE_OVERWRITTEN
            || Data->IoStatus.Information == FILE_SUPERSEDED)
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] File overwritten, removing reparse tag: '%wZ'\n", completionContext->NameInfo->Name));
            NT_IF_FAIL_LEAVE(LcUntagFile(FltObjects, &completionContext->NameInfo->Name));

            __leave;
        }

        // Get data from the reparse point and set the proper context, so the file will be fetched on the first read/write operation.
        NT_IF_FAIL_LEAVE(LcGetReparsePointData(FltObjects, &fileSize, &remotePath, &useCustomHandler));

        NT_IF_FAIL_LEAVE(LcFindOrCreateStreamContext(Data, TRUE, &fileSize, &remotePath, useCustomHandler, &streamContext, &contextCreated));
        if (!contextCreated)
        {
            __leave;
        }

        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Stream context set: '%wZ'\n", completionContext->NameInfo->Name));
    }
    __finally
    {
        // Reparse point may be removed (file is already fetched).
        if (!NT_SUCCESS(status) && status != STATUS_NOT_A_REPARSE_POINT)
        {
            // Fail I/O request, because something went wrong with untagging or setting context.
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "[LazyCopy] Unable to untag or set context: '%wZ' 0x%X\n", completionContext->NameInfo->Name, status));

            FltCancelFileOpen(FltObjects->Instance, FltObjects->FileObject);
            Data->IoStatus.Status      = status;
            Data->IoStatus.Information = 0;
            FltSetCallbackDataDirty(Data);
        }

        FltReleaseFileNameInformation(completionContext->NameInfo);
        LcFreeNonPagedBuffer(completionContext);

        if (remotePath.Buffer != NULL)
        {
            LcFreeUnicodeString(&remotePath);
        }

        if (streamContext != NULL)
        {
            FltReleaseContext(streamContext);
        }
    }

    return FLT_POSTOP_FINISHED_PROCESSING;
}

FLT_PREOP_CALLBACK_STATUS
PreReadWriteOperationCallback (
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    )
/*++

Summary:

    This function is invoked before the 'IRP_MJ_READ', 'IRP_MJ_WRITE', and 'IRP_MJ_ACQUIRE_FOR_SECTION_SYNCHRONIZATION'
    are processed by the low-level drivers.

    The algorightm is simple:
    1. Check, whether the context is set for the stream. If it is not there, return;
    2. Try to exclusively lock the file for processing. If the obtained lock is not exclusive, return;
    3. Check for the reparse tag. If it's not there (file is fetched), return;
    4. Fetch file;
    5. Untag it and remove stream context, so it will not be fetched again.

Arguments:

    Data              - Pointer to the filter's callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The context for the completion function for this operation.

Return value:

    The return value is the status of the operation.

--*/
{
    FLT_PREOP_CALLBACK_STATUS      callbackStatus = FLT_PREOP_SUCCESS_NO_CALLBACK;
    NTSTATUS                       status         = STATUS_SUCCESS;
    PLC_STREAM_CONTEXT             context        = NULL;
    PFLT_FILE_NAME_INFORMATION     nameInfo       = NULL;
    FILE_ATTRIBUTE_TAG_INFORMATION attributeTag   = { 0 };
    LARGE_INTEGER                  bytesFetched   = { 0 };
    PKEVENT                        fileLockEvent  = NULL;
    LARGE_INTEGER                  zeroTimeout    = { 0 };

    // Whether I/O should be cancelled on unsuccessful error code.
    BOOLEAN cancelOnError = FALSE;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(CompletionContext);

    zeroTimeout = RtlConvertLongToLargeInteger(0);

    EventWriteFile_Fetch_Start(NULL);

    __try
    {
        // Skip, if the trusted process is accessing the file.
        if (LcIsProcessTrusted(PsGetThreadProcessId(Data->Thread)))
        {
            __leave;
        }

        // If context is not set for the stream, it should not be fetched.
        status = LcGetStreamContext(Data, &context);
        if (!NT_SUCCESS(status))
        {
            status = STATUS_SUCCESS;
            __leave;
        }

        // Get the file name details.
        NT_IF_FAIL_LEAVE(LcGetFileNameInformation(Data, &nameInfo));

        // Get the locking event to synchronize access to the same file.
        NT_IF_FAIL_LEAVE(LcGetFileLock(&nameInfo->Name, &fileLockEvent));

        // If the event is not in the signaled state, we don't need to fetch this file,
        // because another thread, which unset the event, is fetching it.
        if (KeWaitForSingleObject(fileLockEvent, Executive, KernelMode, FALSE, &zeroTimeout) != STATUS_SUCCESS)
        {
            // Wait for the file to be fetched.
            NT_IF_FAIL_LEAVE(KeWaitForSingleObject(fileLockEvent, Executive, KernelMode, FALSE, NULL));
            __leave;
        }

        // Skip, if the file is not tagged.
        NT_IF_FAIL_LEAVE(FltQueryInformationFile(FltObjects->Instance, FltObjects->FileObject, &attributeTag, sizeof(FILE_ATTRIBUTE_TAG_INFORMATION), FileAttributeTagInformation, NULL));
        if (attributeTag.ReparseTag != LC_REPARSE_TAG)
        {
            __leave;
        }

        // Set the I/O cancellation on error in the __finally block.
        cancelOnError = TRUE;

        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Fetching file: '%wZ'\n", nameInfo->Name));
        NT_IF_FAIL_LEAVE(LcFetchRemoteFile(FltObjects, &context->RemoteFilePath, &nameInfo->Name, context->UseCustomHandler, &bytesFetched));

        NT_IF_FAIL_LEAVE(LcUntagFile(FltObjects, &nameInfo->Name));
        NT_IF_FAIL_LEAVE(FltDeleteStreamContext(FltObjects->Instance, FltObjects->FileObject, NULL));

        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "[LazyCopy] File fetched: '%wZ' (%lld bytes)\n", nameInfo->Name, bytesFetched.QuadPart));
        EventWriteFileFetchedEvent(NULL, nameInfo->Name.Buffer, context->RemoteFilePath.Buffer, bytesFetched.QuadPart);
    }
    __finally
    {
        if (!NT_SUCCESS(status) && cancelOnError)
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "[LazyCopy] Unable to fetch file: '%wZ' 0x%X\n", nameInfo->Name, status));
            EventWriteFileNotFetchedEvent(NULL, nameInfo->Name.Buffer, context->RemoteFilePath.Buffer, status);

            // Fail I/O operation.
            Data->IoStatus.Status      = status;
            Data->IoStatus.Information = 0;
            FltSetCallbackDataDirty(Data);
            callbackStatus             = FLT_PREOP_COMPLETE;
        }

        if (nameInfo != NULL)
        {
            FltReleaseFileNameInformation(nameInfo);
        }

        if (context != NULL)
        {
            FltReleaseContext(context);
        }

        if (fileLockEvent != NULL)
        {
            LcReleaseFileLock(fileLockEvent);
        }
    }

    EventWriteFile_Fetch_Stop(NULL);

    return callbackStatus;
}

FLT_PREOP_CALLBACK_STATUS
PreQueryInformationOperationCallback (
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    )
/*++

Summary:

    This function is invoked before the 'IRP_MJ_QUERY_INFORMATION' for this minifilter driver.

    It is needed to fake the file size requests, so the applications that use memory mapping
    will read full file contents.
    This function does not affect Explorer behavior.

Arguments:

    Data              - Pointer to the filter's callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The context for the completion function for this operation.

Return value:

    The return value is the status of the operation.

--*/
{
    FLT_PREOP_CALLBACK_STATUS callbackStatus = FLT_PREOP_SUCCESS_NO_CALLBACK;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(FltObjects);
    UNREFERENCED_PARAMETER(CompletionContext);

    __try
    {
        // Ignore I/O that was generated by a minifilter.
        if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_GENERATED_IO)
            || FLT_IS_FS_FILTER_OPERATION(Data)
            || FLT_IS_REISSUED_IO(Data))
        {
            __leave;
        }

        switch (Data->Iopb->Parameters.QueryFileInformation.FileInformationClass)
        {
            case FileAllInformation:
            case FileStandardInformation:
            case FileEndOfFileInformation:
            case FileNetworkOpenInformation:
                callbackStatus = FLT_PREOP_SYNCHRONIZE;
                break;
        }
    }
    __finally
    {
        // Do nothing.
    }

    return callbackStatus;
}

FLT_POSTOP_CALLBACK_STATUS
PostQueryInformationOperationCallback (
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    )
/*++

Summary:

    This function is invoked after the 'IRP_MJ_QUERY_INFORMATION' was processed by the low-level drivers.

    It modifies file size in the structures returned by the underlying drivers with the data from the
    stream context, so the file will appear as non-empty for the callers.

Parameters:

    Data              - Pointer to the filter callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The completion context set in the pre-operation function.

    Flags             - Denotes whether the completion is successful or is being drained.

Return Value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS           status     = STATUS_SUCCESS;
    PVOID              userBuffer = NULL;
    PLC_STREAM_CONTEXT context    = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(FltObjects);
    UNREFERENCED_PARAMETER(CompletionContext);
    UNREFERENCED_PARAMETER(Flags);

    __try
    {
        if (FlagOn(Flags, FLTFL_POST_OPERATION_DRAINING))
        {
            __leave;
        }

        if (!NT_SUCCESS(Data->IoStatus.Status) && Data->IoStatus.Status != STATUS_BUFFER_OVERFLOW)
        {
            __leave;
        }

        // If the file is already fetched, it's size will be greater than zero and context will not be set.
        NT_IF_FAIL_LEAVE(LcGetStreamContext(Data, &context));
        userBuffer = Data->Iopb->Parameters.QueryFileInformation.InfoBuffer;

        // Strip offline attributes and fix the EOF data.
        switch (Data->Iopb->Parameters.QueryFileInformation.FileInformationClass)
        {
            case FileAllInformation:
                if (((PFILE_ALL_INFORMATION)userBuffer)->StandardInformation.EndOfFile.QuadPart == 0)
                {
                    ((PFILE_ALL_INFORMATION)userBuffer)->StandardInformation.EndOfFile = context->RemoteFileSize;
                }

                ((PFILE_ALL_INFORMATION)userBuffer)->BasicInformation.FileAttributes &= ~LC_FILE_ATTRIBUTES;
                break;
            case FileNetworkOpenInformation:
                if (((PFILE_NETWORK_OPEN_INFORMATION)userBuffer)->EndOfFile.QuadPart == 0)
                {
                    ((PFILE_NETWORK_OPEN_INFORMATION)userBuffer)->EndOfFile = context->RemoteFileSize;
                }

                ((PFILE_NETWORK_OPEN_INFORMATION)userBuffer)->FileAttributes &= ~LC_FILE_ATTRIBUTES;
                break;
            case FileBasicInformation:
                ((PFILE_BASIC_INFORMATION)userBuffer)->FileAttributes &= ~LC_FILE_ATTRIBUTES;
                break;
            case FileAttributeTagInformation:
                ((PFILE_ATTRIBUTE_TAG_INFORMATION)userBuffer)->FileAttributes &= ~LC_FILE_ATTRIBUTES;
                break;
            case FileStandardInformation:
                if (((PFILE_STANDARD_INFORMATION)userBuffer)->EndOfFile.QuadPart == 0)
                {
                    ((PFILE_STANDARD_INFORMATION)userBuffer)->EndOfFile = context->RemoteFileSize;
                }

                break;
            case FileEndOfFileInformation:
                if (((PFILE_END_OF_FILE_INFORMATION)userBuffer)->EndOfFile.QuadPart == 0)
                {
                    ((PFILE_END_OF_FILE_INFORMATION)userBuffer)->EndOfFile = context->RemoteFileSize;
                }

                break;
        }
    }
    __finally
    {
        if (context != NULL)
        {
            FltReleaseContext(context);
        }
    }

    return FLT_POSTOP_FINISHED_PROCESSING;
}

FLT_PREOP_CALLBACK_STATUS
PostDirectoryControlOperationCallback (
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    )
/*++

Routine Description:

    This callback is invoked after the user tries to enumerate a directory
    or get change notifications.

Parameters:

    Data              - Pointer to the filter callback data that is passed to us.

    FltObjects        - Pointer to the 'FLT_RELATED_OBJECTS' data structure containing
                        opaque handles to this filter, instance, its associated volume and
                        file object.

    CompletionContext - The completion context set in the pre-operation function.

    Flags             - Denotes whether the completion is successful or is being drained.

Return Value:

    The return value is the status of the operation.

--*/
{
    PVOID buffer = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(FltObjects);
    UNREFERENCED_PARAMETER(CompletionContext);

    __try
    {
        if (FlagOn(Flags, FLTFL_POST_OPERATION_DRAINING)
            || !NT_SUCCESS(Data->IoStatus.Status)
            || Data->Iopb->MinorFunction != IRP_MN_QUERY_DIRECTORY)
        {
            __leave;
        }

        if (NT_SUCCESS(FltLockUserBuffer(Data)))
        {
            PMDL address = Data->Iopb->Parameters.DirectoryControl.QueryDirectory.MdlAddress;
            if (address != NULL)
            {
                buffer = MmGetSystemAddressForMdlSafe(address, NormalPagePriority);
            }
        }

        if (buffer == NULL)
        {
            buffer = Data->Iopb->Parameters.DirectoryControl.QueryDirectory.DirectoryBuffer;

            __try
            {
                ULONG bufferSize = Data->Iopb->Parameters.DirectoryControl.QueryDirectory.Length;

                if (Data->RequestorMode != KernelMode)
                {
                    ProbeForWrite(buffer, bufferSize, sizeof(UCHAR));
                }
            }
            #pragma warning(suppress: 6320) // Handle all exceptions.
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                // Invalid user buffer.
                __leave;
            }
        }

        switch (Data->Iopb->Parameters.DirectoryControl.QueryDirectory.FileInformationClass)
        {
            case FileBothDirectoryInformation:
            {
                PFILE_BOTH_DIR_INFORMATION fileInfo = (PFILE_BOTH_DIR_INFORMATION)buffer;

                for (;;)
                {
                    if (!FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_DIRECTORY)
                        && !FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_SYSTEM)
                        && (fileInfo->FileAttributes & LC_FILE_ATTRIBUTES) == LC_FILE_ATTRIBUTES)
                    {
                        fileInfo->FileAttributes &= ~FILE_ATTRIBUTE_OFFLINE;
                    }

                    if (fileInfo->NextEntryOffset != 0)
                    {
                        fileInfo = (PFILE_BOTH_DIR_INFORMATION)((UCHAR*)fileInfo + fileInfo->NextEntryOffset);
                    }
                    else
                    {
                        break;
                    }
                }

                break;
            }
            case FileDirectoryInformation:
            {
                PFILE_DIRECTORY_INFORMATION fileInfo = (PFILE_DIRECTORY_INFORMATION)buffer;

                for (;;)
                {
                    if (!FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_DIRECTORY)
                        && !FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_SYSTEM)
                        && (fileInfo->FileAttributes & LC_FILE_ATTRIBUTES) == LC_FILE_ATTRIBUTES)
                    {
                        fileInfo->FileAttributes &= ~FILE_ATTRIBUTE_OFFLINE;
                    }

                    if (fileInfo->NextEntryOffset != 0)
                    {
                        fileInfo = (PFILE_DIRECTORY_INFORMATION)((UCHAR*)fileInfo + fileInfo->NextEntryOffset);
                    }
                    else
                    {
                        break;
                    }
                }

                break;
            }
            case FileFullDirectoryInformation:
            {
                PFILE_FULL_DIR_INFORMATION fileInfo = (PFILE_FULL_DIR_INFORMATION)buffer;

                for (;;)
                {
                    if (!FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_DIRECTORY)
                        && !FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_SYSTEM)
                        && (fileInfo->FileAttributes & LC_FILE_ATTRIBUTES) == LC_FILE_ATTRIBUTES)
                    {
                        fileInfo->FileAttributes &= ~FILE_ATTRIBUTE_OFFLINE;
                    }

                    if (fileInfo->NextEntryOffset != 0)
                    {
                        fileInfo = (PFILE_FULL_DIR_INFORMATION)((UCHAR*)fileInfo + fileInfo->NextEntryOffset);
                    }
                    else
                    {
                        break;
                    }
                }

                break;
            }
            case FileIdBothDirectoryInformation:
            {
                PFILE_ID_BOTH_DIR_INFORMATION fileInfo = (PFILE_ID_BOTH_DIR_INFORMATION)buffer;

                for (;;)
                {
                    if (!FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_DIRECTORY)
                        && !FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_SYSTEM)
                        && (fileInfo->FileAttributes & LC_FILE_ATTRIBUTES) == LC_FILE_ATTRIBUTES)
                    {
                        fileInfo->FileAttributes &= ~FILE_ATTRIBUTE_OFFLINE;
                    }

                    if (fileInfo->NextEntryOffset != 0)
                    {
                        fileInfo = (PFILE_ID_BOTH_DIR_INFORMATION)((UCHAR*)fileInfo + fileInfo->NextEntryOffset);
                    }
                    else
                    {
                        break;
                    }
                }

                break;
            }
            case FileIdFullDirectoryInformation:
            {
                PFILE_ID_FULL_DIR_INFORMATION fileInfo = (PFILE_ID_FULL_DIR_INFORMATION)buffer;

                for (;;)
                {
                    if (!FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_DIRECTORY)
                        && !FlagOn(fileInfo->FileAttributes, FILE_ATTRIBUTE_SYSTEM)
                        && (fileInfo->FileAttributes & LC_FILE_ATTRIBUTES) == LC_FILE_ATTRIBUTES)
                    {
                        fileInfo->FileAttributes &= ~FILE_ATTRIBUTE_OFFLINE;
                    }

                    if (fileInfo->NextEntryOffset != 0)
                    {
                        fileInfo = (PFILE_ID_FULL_DIR_INFORMATION)((UCHAR*)fileInfo + fileInfo->NextEntryOffset);
                    }
                    else
                    {
                        break;
                    }
                }

                break;
            }
        }
    }
    __finally
    {
        // Do nothing.
    }

    return FLT_POSTOP_FINISHED_PROCESSING;
}

//------------------------------------------------------------------------
//  Local functions.
//------------------------------------------------------------------------

static
VOID
LcEtwFileAccessed (
    _In_ ULONG            ReportRate,
    _In_ PCUNICODE_STRING Path,
    _In_ ULONG            CreateOptions
    )
/*++

Summary:

    Raises a new 'FileAccessed' ETW event based on the current report rate value.

Arguments:

    ReportRate    - Report rate for the path given.

    Path          - File path.

    CreateOptions - File create options.

Return value:

    None.

--*/
{
    // Factor to be used to normalize the report rate value to the MAXLONG interval.
    static const ULONG rateFactor = (MAXLONG - 1) / MAX_REPORT_RATE;

    PAGED_CODE();

    IF_FALSE_RETURN(Path != NULL);

    if (ReportRate == 0)
    {
        return;
    }

    // Determine whether the event should be sent.
    if (ReportRate >= MAX_REPORT_RATE
        || RtlRandomEx(&LC_RANDOM_SEED) < (ReportRate * rateFactor))
    {
        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] File accessed: '%wZ'\n", Path));
        EventWriteFileAccessedEvent(NULL, Path->Buffer, CreateOptions);
    }
}

static
_Check_return_
NTSTATUS
LcGetFileNameInformation (
    _Inout_  PFLT_CALLBACK_DATA          Data,
    _Outptr_ PFLT_FILE_NAME_INFORMATION* NameInformation
    )
/*++

Summary:

This function gets the name information used to open the current file.

Arguments:

Data            - A pointer to the callback data structure for the I/O operation.

NameInformation - A pointer to a caller-allocated variable that receives the
address of a system-allocated FLT_FILE_NAME_INFORMATION structure
containing the file name information.

Return value:

The return value is the status of the operation.

--*/
{
    NTSTATUS                   status   = STATUS_SUCCESS;
    PFLT_FILE_NAME_INFORMATION nameInfo = NULL;
    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Data            != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(NameInformation != NULL, STATUS_INVALID_PARAMETER_2);

    *NameInformation = NULL;

    __try
    {
        if (FlagOn(Data->Iopb->OperationFlags, SL_OPEN_TARGET_DIRECTORY))
        {
            // The SL_OPEN_TARGET_DIRECTORY flag indicates the caller is attempting
            // to open the target of a rename or hard link creation operation. We
            // must clear this flag when asking fltmgr for the name or the result
            // will not include the final component.
            ClearFlag(Data->Iopb->OperationFlags, SL_OPEN_TARGET_DIRECTORY);

            // Get the filename as it appears below this filter. Note that we use
            // FLT_FILE_NAME_QUERY_FILESYSTEM_ONLY when querying the filename
            // so that the filename as it appears below this filter does not end up
            // in filter manager's name cache.
            status = FltGetFileNameInformation(Data, FLT_FILE_NAME_OPENED | FLT_FILE_NAME_QUERY_FILESYSTEM_ONLY | FLT_FILE_NAME_ALLOW_QUERY_ON_REPARSE, &nameInfo);

            // Restore the SL_OPEN_TARGET_DIRECTORY flag so the create will proceed
            // for the target. The file systems depend on this flag being set in
            // the target create in order for the subsequent SET_INFORMATION
            // operation to proceed correctly.
            SetFlag(Data->Iopb->OperationFlags, SL_OPEN_TARGET_DIRECTORY);
        }
        else
        {
            // In some cases it is not safe for filter manager to generate a
            // file name, and FLT_FILE_NAME_QUERY_DEFAULT will detect those cases
            // and fail without looking in the cache.
            // FLT_FILE_NAME_QUERY_ALWAYS_ALLOW_CACHE_LOOKUP always checks the cache,
            // and then queries the file system if its safe.
            status = FltGetFileNameInformation(Data, FLT_FILE_NAME_OPENED | FLT_FILE_NAME_QUERY_ALWAYS_ALLOW_CACHE_LOOKUP | FLT_FILE_NAME_ALLOW_QUERY_ON_REPARSE, &nameInfo);
        }

        NT_IF_FAIL_LEAVE(status);

        // Make sure the stream name has been parsed.
        if (!FlagOn(nameInfo->NamesParsed, FLTFL_FILE_NAME_PARSED_STREAM))
        {
            NT_IF_FAIL_LEAVE(FltParseFileNameInformation(nameInfo));
        }

        *NameInformation = nameInfo;
    }
    __finally
    {
        if (!NT_SUCCESS(status))
        {
            if (nameInfo != NULL)
            {
                FltReleaseFileNameInformation(nameInfo);
                nameInfo = NULL;
            }
        }
    }

    return status;
}
