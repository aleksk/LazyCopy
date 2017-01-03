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

    LazyCopyDriver.h

Abstract:

    Contains type definitions and function prototypes that are used
    for minifilter registration and operation.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_DRIVER_H__
#define __LAZY_COPY_DRIVER_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Registration structure.
//------------------------------------------------------------------------

extern const FLT_REGISTRATION FilterRegistration;

//------------------------------------------------------------------------
//  Function prototypes.
//------------------------------------------------------------------------

//
//  Functions that handle driver load/unload and instance setup/cleanup.
//

DRIVER_INITIALIZE DriverEntry;

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT  DriverObject,
    _In_ PUNICODE_STRING RegistryPath
    );

NTSTATUS
DriverInstanceSetup(
    _In_ PCFLT_RELATED_OBJECTS    FltObjects,
    _In_ FLT_INSTANCE_SETUP_FLAGS Flags,
    _In_ DEVICE_TYPE              VolumeDeviceType,
    _In_ FLT_FILESYSTEM_TYPE      VolumeFilesystemType
    );

NTSTATUS
DriverInstanceQueryTeardown(
    _In_ PCFLT_RELATED_OBJECTS             FltObjects,
    _In_ FLT_INSTANCE_QUERY_TEARDOWN_FLAGS Flags
    );

NTSTATUS
DriverUnload(
    _In_ FLT_FILTER_UNLOAD_FLAGS Flags
    );

//
//  Functions that track operations on the volume.
//

FLT_PREOP_CALLBACK_STATUS
PreCreateOperationCallback(
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    );

FLT_POSTOP_CALLBACK_STATUS
PostCreateOperationCallback(
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    );

FLT_PREOP_CALLBACK_STATUS
PreReadWriteOperationCallback(
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    );

FLT_PREOP_CALLBACK_STATUS
PreQueryInformationOperationCallback(
    _Inout_                        PFLT_CALLBACK_DATA    Data,
    _In_                           PCFLT_RELATED_OBJECTS FltObjects,
    _Flt_CompletionContext_Outptr_ PVOID*                CompletionContext
    );

FLT_POSTOP_CALLBACK_STATUS
PostQueryInformationOperationCallback(
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    );

FLT_PREOP_CALLBACK_STATUS
PostDirectoryControlOperationCallback(
    _Inout_  PFLT_CALLBACK_DATA       Data,
    _In_     PCFLT_RELATED_OBJECTS    FltObjects,
    _In_opt_ PVOID                    CompletionContext,
    _In_     FLT_POST_OPERATION_FLAGS Flags
    );

#endif // __LAZY_COPY_DRIVER_H__
