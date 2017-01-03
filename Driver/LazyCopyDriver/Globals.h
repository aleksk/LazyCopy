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

    Globals.h

Abstract:

    Contains global structures, type definitions, constants and
    variables that can be shared across different driver modules.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_GLOBALS_H__
#define __LAZY_COPY_GLOBALS_H__

//------------------------------------------------------------------------
//  Disabled warnings.
//------------------------------------------------------------------------

#pragma prefast(disable:__WARNING_ENCODE_MEMBER_FUNCTION_POINTER, "Not valid for kernel mode drivers")

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#pragma warning(push)
#pragma warning(disable:4995)  // <function>: name was marked as #pragma deprecated.

#include <fltKernel.h>
#include <dontuse.h>
#include <suppress.h>
#include <Ntstrsafe.h>

#pragma warning(pop) // Enable C4995.

// Globally disable these compiler warnings.
#pragma warning(disable:6102) // Using value from failed function call.
#pragma warning(disable:6103) // Returning value from failed function call.

#include "Macro.h"

//------------------------------------------------------------------------
//  Defines.
//------------------------------------------------------------------------

// Win8 supports NPFS/MSFS.
// Win7 supports new ECPs.
// Vista supports transactions, older ECPs.

#define PLATFORM_WIN10     (NTDDI_VERSIOn >= NTDDI_WIN10)
#define PLATFORM_WIN8      (NTDDI_VERSION >= NTDDI_WIN8)
#define PLATFORM_WIN7      (NTDDI_VERSION >= NTDDI_WIN7)
#define PLATFORM_VISTA     (NTDDI_VERSION >= NTDDI_VISTA)
#define PLATFORM_NOT_W2K   (OSVER(NTDDI_VERSION) > NTDDI_WIN2K)

// Current driver version.
#define LC_MAJOR_VERSION  1
#define LC_MINOR_VERSION  1

//
//  Pool tags for memory allocation.
//

#define LC_BUFFER_PAGED_POOL_TAG             ('lcbP')
#define LC_BUFFER_NON_PAGED_POOL_TAG         ('lcbN')
#define LC_STRING_PAGED_POOL_TAG             ('lcsP')
#define LC_STRING_NON_PAGED_POOL_TAG         ('lcsN')
#define LC_ERESOURCE_PAGED_POOL_TAG          ('lceP')
#define LC_ERESOURCE_NON_PAGED_POOL_TAG      ('lceN')
#define LC_CONTEXT_PAGED_POOL_TAG            ('lccP')
#define LC_CONTEXT_NON_PAGED_POOL_TAG        ('lccN')
#define LC_COMMUNICATION_PAGED_POOL_TAG      ('lcmP')
#define LC_COMMUNICATION_NON_PAGED_POOL_TAG  ('lcmN')

//
// Reparse point data.
//

// TODO: Before releasing your driver, make sure to contact Microsoft to register this value.
#define LC_REPARSE_TAG  (0x00000340)

extern GUID LC_REPARSE_GUID;

//
// File attributes.
//

#define LC_FILE_ATTRIBUTES (FILE_ATTRIBUTE_OFFLINE | FILE_ATTRIBUTE_REPARSE_POINT)

//
// Other.
//

// Seed to be used for randomization.
extern ULONG LC_RANDOM_SEED;

//------------------------------------------------------------------------
//  Global structures and variables.
//------------------------------------------------------------------------

//
// Global driver data.
//
typedef struct _DRIVER_GLOBAL_DATA
{
    // The object that identifies this driver.
    PDRIVER_OBJECT DriverObject;

    // The filter that results from a call to the 'FltRegisterFilter' function.
    PFLT_FILTER    Filter;

    // Global driver lock.
    PERESOURCE     Lock;
} DRIVER_GLOBAL_DATA, *PDRIVER_GLOBAL_DATA;

extern DRIVER_GLOBAL_DATA Globals;

#endif // __LAZY_COPY_GLOBALS_H__
