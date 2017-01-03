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

    FileLocks.h

Abstract:

    Contains file locking helper functions for maintaining custom file
    locks to handle situations, when multiple threads are trying to access
    file that is being fetched from a remote source.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_FILE_LOCKS_H__
#define __LAZY_COPY_FILE_LOCKS_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  File locking function prototypes.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcInitializeFileLocks();

VOID
LcFreeFileLocks();

_Check_return_
NTSTATUS
LcGetFileLock(
    _In_     PCUNICODE_STRING FileName,
    _Outptr_ PKEVENT*         Event
    );

VOID
LcReleaseFileLock(
    _In_ PKEVENT Event
    );

#endif // __LAZY_COPY_FILE_LOCKS_H__
