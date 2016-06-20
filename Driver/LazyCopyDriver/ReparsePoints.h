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

    ReparsePoints.h

Abstract:

    Contains helper function declarations for managing custom reparse
    points used by the current minifilter.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_REPARSE_POINTS_H__
#define __LAZY_COPY_REPARSE_POINTS_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Reparse points management function prototype declarations.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcGetReparsePointData (
    _In_  PCFLT_RELATED_OBJECTS FltObjects,
    _Out_ PLARGE_INTEGER        RemoteFileSize,
    _Out_ PUNICODE_STRING       RemoteFilePath,
    _Out_ PBOOLEAN              UseCustomHandler
    );

_Check_return_
_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS
LcUntagFile (
    _In_ PCFLT_RELATED_OBJECTS FltObjects,
    _In_ PUNICODE_STRING       FileName
    );

#endif // __LAZY_COPY_REPARSE_POINTS_H__
