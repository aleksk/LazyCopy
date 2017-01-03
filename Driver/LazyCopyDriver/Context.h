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

    Context.h

Abstract:

    Contains context helper function declarations.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_CONTEXT_H__
#define __LAZY_COPY_CONTEXT_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Struct definitions.
//------------------------------------------------------------------------

//
// Stream context data structure.
//
typedef struct _STREAM_CONTEXT
{
    // Whether the file should be fetched by the user-mode client.
    BOOLEAN        UseCustomHandler;

    // Size of the remote file.
    LARGE_INTEGER  RemoteFileSize;

    // Path to the remote file to be fetched.
    UNICODE_STRING RemoteFilePath;

    // There is no resource to protect the context since the its fields are never modified.
} LC_STREAM_CONTEXT, *PLC_STREAM_CONTEXT;

//------------------------------------------------------------------------
//  Function prototypes.
//------------------------------------------------------------------------

VOID
LcContextCleanup(
    _In_ PFLT_CONTEXT     Context,
    _In_ FLT_CONTEXT_TYPE ContextType
    );

_Check_return_
NTSTATUS
LcFindOrCreateStreamContext(
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
    );

_Check_return_
NTSTATUS
LcCreateStreamContext(
    _In_     PLARGE_INTEGER      RemoteFileSize,
    _In_     PUNICODE_STRING     RemoteFilePath,
    _In_     BOOLEAN             UseCustomHandler,
    _Outptr_ PLC_STREAM_CONTEXT* StreamContext
    );

_Check_return_
NTSTATUS
LcGetStreamContext(
    _In_     PFLT_CALLBACK_DATA  Data,
    _Outptr_ PLC_STREAM_CONTEXT* StreamContext
    );

#endif // __LAZY_COPY_CONTEXT_H__
