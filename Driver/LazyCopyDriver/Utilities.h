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

    Utilities.h

Abstract:

    Contains common helper function prototype declarations.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_UTILITIES_H__
#define __LAZY_COPY_UTILITIES_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Memory allocation/freeing function prototype declarations.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAllocateBuffer (
    _Outptr_result_buffer_(Size) PVOID*    Buffer,
    _In_                         POOL_TYPE PoolType,
    _In_                         SIZE_T    Size,
    _In_                         ULONG     Tag
    );

_Check_return_
NTSTATUS
LcAllocateNonPagedBuffer (
    _Outptr_result_buffer_(Size) PVOID* Buffer,
    _In_                         SIZE_T Size
    );

_Check_return_
NTSTATUS
LcAllocateNonPagedAlignedBuffer (
    _In_                         PFLT_INSTANCE Instance,
    _Outptr_result_buffer_(Size) PVOID*        Buffer,
    _In_                         SIZE_T        Size
    );

VOID
LcFreeBuffer (
    _Inout_ PVOID Buffer,
    _In_    ULONG Tag
    );

VOID
LcFreeNonPagedBuffer (
    _Inout_ PVOID Buffer
    );

VOID
LcFreeNonPagedAlignedBuffer (
    _In_    PFLT_INSTANCE Instance,
    _Inout_ PVOID         Buffer
    );

_Check_return_
NTSTATUS
LcAllocateResource (
    _Outptr_ PERESOURCE* Resource
    );

VOID
LcFreeResource (
    _In_ PERESOURCE Resource
    );

_Check_return_
NTSTATUS
LcAllocateUnicodeString (
    _Inout_ PUNICODE_STRING String,
    _In_    USHORT          Size
    );

_Check_return_
NTSTATUS
LcCopyUnicodeString (
    _Inout_ PUNICODE_STRING  DestinationString,
    _In_    PCUNICODE_STRING SourceString
    );

VOID
LcFreeUnicodeString (
    _Inout_ PUNICODE_STRING String
    );

#endif // __LAZY_COPY_UTILITIES_H__
