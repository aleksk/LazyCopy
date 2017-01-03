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

    Configuration.h

Abstract:

    Contains driver configuration management function declarations.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_CONFIGURATION_H__
#define __LAZY_COPY_CONFIGURATION_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Defines.
//------------------------------------------------------------------------

#define MAX_REPORT_RATE      10000L
#define DEFAULT_REPORT_RATE  600L

//------------------------------------------------------------------------
//  Enums.
//------------------------------------------------------------------------

//
// Defines operations driver can perform.
//
typedef enum _DRIVER_OPERATION_MODE
{
    // All driver operations are disabled.
    DriverDisabled = 0,

    // Fetch operations are enabled.
    FetchEnabled   = 1 << 0,

    // Watch paths are enabled.
    WatchEnabled   = 1 << 1
} DRIVER_OPERATION_MODE, *PDRIVER_OPERATION_MODE;

//------------------------------------------------------------------------
//  Function prototypes.
//------------------------------------------------------------------------

//
//  Configuration lifecycle management functions.
//

_Check_return_
NTSTATUS
LcInitializeConfiguration(
    _In_ PCUNICODE_STRING RegistryPath
    );

VOID
LcFreeConfiguration();

//
//  Registry access functions.
//

_Check_return_
NTSTATUS
LcReadConfigurationFromRegistry();

//
//  Trusted processes management functions.
//

_Check_return_
NTSTATUS
LcAddTrustedProcess(
    _In_ HANDLE ProcessId
    );

VOID
LcRemoveTrustedProcess(
    _In_ HANDLE ProcessId
    );

_Check_return_
BOOLEAN
LcIsProcessTrusted(
    _In_ HANDLE ProcessId
    );

VOID
LcClearTrustedProcesses();

//
//  Paths to watch management functions.
//

_Check_return_
NTSTATUS
LcAddPathToWatch(
    _In_ PCUNICODE_STRING Path
    );

_Check_return_
BOOLEAN
LcIsPathWatched(
    _In_ PCUNICODE_STRING Path
    );

VOID
LcClearPathsToWatch();

//
//  Operation mode management functions.
//

VOID
LcSetOperationMode(
    _In_ DRIVER_OPERATION_MODE Value
    );

_Check_return_
DRIVER_OPERATION_MODE
LcGetOperationMode();

//
//  Report rate management functions.
//

VOID
LcSetReportRate(
    _In_ ULONG Value
    );

_Check_return_
ULONG
LcGetReportRateForPath(
    _In_ PCUNICODE_STRING Path
    );

#endif // __LAZY_COPY_CONFIGURATION_H__
