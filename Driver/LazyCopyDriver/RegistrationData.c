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

    RegistrationData.c

Abstract:

    Contains the registration information required to register this
    minifilter within the Filter Manager.

    This is in a unique file, so it could be set into the 'INIT' section.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Context.h"
#include "LazyCopyDriver.h"

//------------------------------------------------------------------------
//  Registration information required by the Filter Manager.
//------------------------------------------------------------------------

//
// This defines the set of context supported by this minifilter.
//
static const FLT_CONTEXT_REGISTRATION Contexts[] =
{
     {
         FLT_STREAM_CONTEXT,             // Context type
         0,                              // Flags
         (PFLT_CONTEXT_CLEANUP_CALLBACK)
            LcContextCleanup,            // Cleanup callback
         sizeof(LC_STREAM_CONTEXT),      // Context size
         LC_CONTEXT_NON_PAGED_POOL_TAG,  // Pool tag
         NULL,                           // Allocate callback
         NULL,                           // Free callback
         NULL                            // Reserved
     },

     { FLT_CONTEXT_END }
};

//
// This defines what the set of Filter Manager events we want to subscribe to.
//
static const FLT_OPERATION_REGISTRATION Callbacks[] =
{
    {
        IRP_MJ_CREATE,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        (PFLT_PRE_OPERATION_CALLBACK)PreCreateOperationCallback,
        (PFLT_POST_OPERATION_CALLBACK)PostCreateOperationCallback
    },

    {
        IRP_MJ_READ,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        (PFLT_PRE_OPERATION_CALLBACK)PreReadWriteOperationCallback,
        NULL
    },

    {
        IRP_MJ_WRITE,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        (PFLT_PRE_OPERATION_CALLBACK)PreReadWriteOperationCallback,
        NULL
    },

    {
        IRP_MJ_ACQUIRE_FOR_SECTION_SYNCHRONIZATION,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        (PFLT_PRE_OPERATION_CALLBACK)PreReadWriteOperationCallback,
        NULL
    },

    {
        IRP_MJ_QUERY_INFORMATION,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        (PFLT_PRE_OPERATION_CALLBACK)PreQueryInformationOperationCallback,
        (PFLT_POST_OPERATION_CALLBACK)PostQueryInformationOperationCallback
    },

    {
        IRP_MJ_DIRECTORY_CONTROL,
        FLTFL_OPERATION_REGISTRATION_SKIP_PAGING_IO,
        NULL,
        (PFLT_POST_OPERATION_CALLBACK)PostDirectoryControlOperationCallback
    },

    { IRP_MJ_OPERATION_END }
};

//
// This defines what we want to filter with the Filter Manager.
//
const FLT_REGISTRATION FilterRegistration =
{
    sizeof(FLT_REGISTRATION),                // Size
    FLT_REGISTRATION_VERSION,                // Version

#if PLATFORM_WIN8
    FLTFL_REGISTRATION_SUPPORT_NPFS_MSFS,    // Flags
#else
    0,                                       // Flags
#endif // PLATFORM_WIN8

    Contexts,                                // Supported contexts
    Callbacks,                               // Operation callbacks
    (PFLT_FILTER_UNLOAD_CALLBACK)            // Filters unload function
        DriverUnload,
    (PFLT_INSTANCE_SETUP_CALLBACK)           // InstanceSetup function
        DriverInstanceSetup,
    (PFLT_INSTANCE_QUERY_TEARDOWN_CALLBACK)  // InstanceQueryTeardown function
        DriverInstanceQueryTeardown,
    NULL,                                    // InstanceTeardownStart function
    NULL,                                    // InstanceTeardownComplete function
    NULL,                                    // Filename generation support callback
    NULL,                                    // Filename normalization support callback
    NULL,                                    // Normalize name component cleanup callback

#if PLATFORM_VISTA
    NULL,                                    // Transaction notification callback
    NULL                                     // Filename normalization support callback
#endif // PLATFORM_VISTA
};
