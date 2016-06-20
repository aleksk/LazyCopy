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

    Context.c

Abstract:

    Contains context function definitions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Context.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcContextCleanup)

    #pragma alloc_text(PAGE, LcFindOrCreateStreamContext)
    #pragma alloc_text(PAGE, LcCreateStreamContext)
    #pragma alloc_text(PAGE, LcGetStreamContext)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  FilterManager callbacks.
//------------------------------------------------------------------------

VOID
LcContextCleanup (
    _In_ PFLT_CONTEXT     Context,
    _In_ FLT_CONTEXT_TYPE ContextType
    )
/*++

Summary:

    This function is called by the FilterManager when the context
    reference counter reaches zero and the context should be freed.

Arguments:

    Context     - Context to cleanup.

    ContextType - Type of the context to be cleaned up.

Return value:

    None.

--*/
{
    PLC_STREAM_CONTEXT context = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(ContextType);

    FLT_ASSERT(Context     != NULL_CONTEXT);
    FLT_ASSERT(ContextType == FLT_STREAM_CONTEXT);

    context = (PLC_STREAM_CONTEXT)Context;

    if (context->RemoteFilePath.Buffer != NULL)
    {
        LcFreeUnicodeString(&context->RemoteFilePath);
    }
}

//------------------------------------------------------------------------
//  Stream context functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcFindOrCreateStreamContext (
    _In_      PFLT_CALLBACK_DATA  Data,
    _In_      BOOLEAN             CreateIfNotFound,
    _When_(CreateIfNotFound,  _In_)
    _When_(!CreateIfNotFound, _In_opt_)
              PLARGE_INTEGER      RemoteFileSize,
    _When_(CreateIfNotFound,  _In_)
    _When_(!CreateIfNotFound, _In_opt_)
              PUNICODE_STRING     RemoteFilePath,
    _In_      BOOLEAN             UseCustomHandler,
    _Outptr_  PLC_STREAM_CONTEXT* StreamContext,
    _Out_opt_ PBOOLEAN            ContextCreated
    )
/*++

Summary:

    This function finds the stream context for the target file object.
    Optionally, if the context does not exist, this function creates
    a new one and attaches it to the file.

Arguments:

    Data             - Callback data, which declares the requested operation.

    CreateIfNotFound - Whether the stream context must be created, if missing.

    RemoteFileSize   - Size of the remote file.

    RemoteFilePath   - Path to the remote file to be fetched.

    UseCustomHandler - Whether the file should be fetched by the user-mode client.

    StreamContext    - Returns the stream context.

    ContextCreated   - Returns TRUE, if the context was created as a result of this function;
                       otherwise, FALSE.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS           status         = STATUS_SUCCESS;
    PLC_STREAM_CONTEXT context        = NULL;
    PLC_STREAM_CONTEXT oldContext     = NULL;
    BOOLEAN            contextCreated = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Data          != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(StreamContext != NULL, STATUS_INVALID_PARAMETER_6);

    if (CreateIfNotFound)
    {
        IF_FALSE_RETURN_RESULT(RemoteFileSize != NULL, STATUS_INVALID_PARAMETER_3);

        IF_FALSE_RETURN_RESULT(RemoteFilePath         != NULL, STATUS_INVALID_PARAMETER_4);
        IF_FALSE_RETURN_RESULT(RemoteFilePath->Buffer != NULL, STATUS_INVALID_PARAMETER_4);
        IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(RemoteFilePath)), STATUS_INVALID_PARAMETER_4);
    }

    FLT_ASSERT(Data->Iopb                   != NULL);
    FLT_ASSERT(Data->Iopb->TargetInstance   != NULL);
    FLT_ASSERT(Data->Iopb->TargetFileObject != NULL);

    __try
    {
        // Try to get the existing stream context first.
        status = FltGetStreamContext(Data->Iopb->TargetInstance, Data->Iopb->TargetFileObject, (PFLT_CONTEXT*)&context);

        if (!NT_SUCCESS(status))
        {
            // If context does not exist and the user wants to create a new one, do that.
            if (status != STATUS_NOT_FOUND || !CreateIfNotFound)
            {
                __leave;
            }

            NT_IF_FAIL_LEAVE(LcCreateStreamContext(RemoteFileSize, RemoteFilePath, UseCustomHandler, &context));

            // Set the allocated context, if it's not already set by another caller.
            status = FltSetStreamContext(Data->Iopb->TargetInstance, Data->Iopb->TargetFileObject, FLT_SET_CONTEXT_KEEP_IF_EXISTS, context, (PFLT_CONTEXT*)&oldContext);

            if (NT_SUCCESS(status))
            {
                contextCreated = TRUE;
            }
            else if (status != STATUS_FLT_CONTEXT_ALREADY_DEFINED)
            {
                __leave;
            }
            else
            {
                // If the context is already defined, release the allocated context and use the defined one.
                FltReleaseContext(context);
                context        = oldContext;
                contextCreated = FALSE;
                status         = STATUS_SUCCESS;
            }
        }

        *StreamContext = context;

        if (ContextCreated != NULL)
        {
            *ContextCreated = contextCreated;
        }
    }
    __finally
    {
        if (!NT_SUCCESS(status) && context != NULL)
        {
            FltReleaseContext(context);
        }
    }

    return status;
}

_Check_return_
NTSTATUS
LcCreateStreamContext (
    _In_     PLARGE_INTEGER      RemoteFileSize,
    _In_     PUNICODE_STRING     RemoteFilePath,
    _In_     BOOLEAN             UseCustomHandler,
    _Outptr_ PLC_STREAM_CONTEXT* StreamContext
    )
/*++

Summary:

    This function allocates a new stream context (LC_STREAM_CONTEXT) from the paged pool.

Arguments:

    RemoteFileSize   - Size of the remote file.

    RemoteFilePath   - Path to the remote file.

    UseCustomHandler - Whether the file should be fetched by the user-mode client.

    StreamContext    - Returns the context allocated.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS           status  = STATUS_SUCCESS;
    PLC_STREAM_CONTEXT context = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(RemoteFileSize != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(RemoteFilePath != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(StreamContext  != NULL, STATUS_INVALID_PARAMETER_4);

    __try
    {
        // Allocate stream context from the Paged pool and populate it with the parameters given.
        NT_IF_FAIL_LEAVE(FltAllocateContext(Globals.Filter, FLT_STREAM_CONTEXT, sizeof(LC_STREAM_CONTEXT), PagedPool, (PFLT_CONTEXT*)&context));
        RtlZeroMemory(context, sizeof(LC_STREAM_CONTEXT));

        // Copy the remote path and size given to the context allocated.
        NT_IF_FAIL_LEAVE(LcCopyUnicodeString(&context->RemoteFilePath, RemoteFilePath));

        context->RemoteFileSize   = *RemoteFileSize;
        context->UseCustomHandler = UseCustomHandler;

        *StreamContext = context;
    }
    __finally
    {
        if (!NT_SUCCESS(status) && context != NULL)
        {
            if (context->RemoteFilePath.Buffer != NULL)
            {
                LcFreeUnicodeString(&context->RemoteFilePath);
            }

            FltDeleteContext(context);
        }
    }

    return status;
}

_Check_return_
NTSTATUS
LcGetStreamContext (
    _In_     PFLT_CALLBACK_DATA  Data,
    _Outptr_ PLC_STREAM_CONTEXT* StreamContext
    )
/*++

Summary:

    This function gets the stream context from the target file object.

Arguments:

    Data          - Pointer to the callback data which declares the requested operation.

    StreamContext - A pointer to the variable that receives the context found.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS           status  = STATUS_SUCCESS;
    PLC_STREAM_CONTEXT context = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Data          != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Data->Iopb    != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(StreamContext != NULL, STATUS_INVALID_PARAMETER_2);

    NT_IF_FAIL_RETURN(FltGetStreamContext(Data->Iopb->TargetInstance, Data->Iopb->TargetFileObject, (PFLT_CONTEXT*)&context));

    *StreamContext = context;

    return status;
}
