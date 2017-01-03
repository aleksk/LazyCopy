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

    Fetch.h

Abstract:

    Contains helper function declarations for file fetch operations.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_FETCH_H__
#define __LAZY_COPY_FETCH_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Fetch function prototypes.
//------------------------------------------------------------------------

_Check_return_
_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS
LcFetchRemoteFile(
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _In_  PUNICODE_STRING       SourceFile,
    _In_  PUNICODE_STRING       TargetFile,
    _In_  BOOLEAN               UseCustomHandler,
    _Out_ PLARGE_INTEGER        BytesCopied
    );

#endif // __LAZY_COPY_FETCH_H__
