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

    Configuration.c

Abstract:

    Contains driver configuration management function definitions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Configuration.h"
#include "LazyCopyEtw.h"
#include "Registry.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Structures.
//------------------------------------------------------------------------

//
// Contains driver configuration parameters.
//
typedef struct _DRIVER_CONFIGURATION_DATA
{
    // We don't want to rely on the 'Globals.Lock' lock in the configuraiton methods.
    PERESOURCE                       Lock;

    // Current operation mode.
    __volatile DRIVER_OPERATION_MODE OperationMode;

    // Probability of raising the FileAccessed ETW event.
    __volatile ULONG                 ReportRate;

    // Registry path to read the driver configuration parameters from,
    // when the 'LcReadConfigurationFromRegistry' function is called.
    UNICODE_STRING                   RegistryPath;

    // List of 'trusted' processes that will not be monitored by this driver.
    LIST_ENTRY                       TrustedProccessList;

    // List of path roots that should be monitored for file access operations.
    LIST_ENTRY                       PathsToWatch;

} DRIVER_CONFIGURATION_DATA, *PDRIVER_CONFIGURATION_DATA;

//
// The 'Configuration.TrustedProccessList' list entry.
//
typedef struct _TRUSTED_PROCESS_ENTRY
{
    HANDLE     ProcessId;
    LIST_ENTRY ListEntry;
} TRUSTED_PROCESS_ENTRY, *PTRUSTED_PROCESS_ENTRY;

//
// The 'Configuration.PathsToWatch' list entry.
//
typedef struct _PATH_TO_WATCH_ENTRY
{
    UNICODE_STRING Path;
    LIST_ENTRY     ListEntry;
} PATH_TO_WATCH_ENTRY, *PPATH_TO_WATCH_ENTRY;

//------------------------------------------------------------------------
//  Local functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcValidatePath(
    _In_ PCUNICODE_STRING Path
    );

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    // Configuration lifecycle management functions.
    #pragma alloc_text(PAGE, LcInitializeConfiguration)
    #pragma alloc_text(PAGE, LcFreeConfiguration)

    // Registry access functions.
    #pragma alloc_text(PAGE, LcReadConfigurationFromRegistry)

    // Trusted processes management functions.
    #pragma alloc_text(PAGE, LcAddTrustedProcess)
    #pragma alloc_text(PAGE, LcRemoveTrustedProcess)
    #pragma alloc_text(PAGE, LcIsProcessTrusted)
    #pragma alloc_text(PAGE, LcClearTrustedProcesses)

    // Paths to watch management functions.
    #pragma alloc_text(PAGE, LcAddPathToWatch)
    #pragma alloc_text(PAGE, LcIsPathWatched)
    #pragma alloc_text(PAGE, LcClearPathsToWatch)

    // Operation mode management functions.
    #pragma alloc_text(PAGE, LcSetOperationMode)
    #pragma alloc_text(PAGE, LcGetOperationMode)

    // Report rate management functions.
    #pragma alloc_text(PAGE, LcSetReportRate)
    #pragma alloc_text(PAGE, LcGetReportRateForPath)

    // Local functions.
    #pragma alloc_text(PAGE, LcValidatePath)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Global variables.
//------------------------------------------------------------------------

// Local instance of the configuration structure.
static DRIVER_CONFIGURATION_DATA Configuration = { 0 };

//------------------------------------------------------------------------
//  Configuration lifecycle management functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcInitializeConfiguration(
    _In_ PCUNICODE_STRING RegistryPath
    )
/*++

Summary:

    This function initializes configuration objects.

Arguments:

    RegistryPath - Unicode string identifying where the parameters for this
                   driver are located in the registry.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(RegistryPath != NULL, STATUS_INVALID_PARAMETER_1);

    EventWriteConfiguration_Load_Start(NULL);

    __try
    {
        // Initialize lock.
        NT_IF_FAIL_LEAVE(LcAllocateResource(&Configuration.Lock));

        // Initalize lists.
        InitializeListHead(&Configuration.TrustedProccessList);
        InitializeListHead(&Configuration.PathsToWatch);

        Configuration.ReportRate    = 0;
        Configuration.OperationMode = DriverDisabled;

        // Read parameters from the Registry.
        NT_IF_FAIL_LEAVE(LcCopyUnicodeString(&Configuration.RegistryPath, RegistryPath));
        NT_IF_FAIL_LEAVE(LcReadConfigurationFromRegistry());
    }
    __finally
    {
        // Do nothing.
    }

    EventWriteConfiguration_Load_Stop(NULL);

    return status;
}

//------------------------------------------------------------------------

VOID
LcFreeConfiguration()
/*++

Summary:

    This function frees memory used by the configuration objects.

    It is not thread-safe, so it should only be called when the driver is about to be unloaded.

Arguments:

    None.

Return value:

    None.

--*/
{
    PAGED_CODE();

    // We initialize lock first in the 'LcInitializeConfiguration' method.
    // If it's not initialized, everything else is not initialized also.
    if (Configuration.Lock == NULL)
    {
        return;
    }

    if (Configuration.TrustedProccessList.Flink != NULL)
    {
        LcClearTrustedProcesses();
    }

    if (Configuration.PathsToWatch.Flink != NULL)
    {
        LcClearPathsToWatch();
    }

    if (Configuration.RegistryPath.Buffer != NULL)
    {
        LcFreeUnicodeString(&Configuration.RegistryPath);
    }

    LcFreeResource(Configuration.Lock);
    Configuration.Lock = NULL;
}

//------------------------------------------------------------------------
//  Registry access functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcReadConfigurationFromRegistry()
/*++

Summary:

    This function reads the driver configuration parameters from the registry.

    All necessary registry keys are created on driver install and are defined
    in the '[MiniFilter.Registry]' section in the INF file.

    This minifilter reads the following values:
    * OperationMode - see the 'LcSetOperationMode';
    * ReportRate    - see the 'LcSetReportRate';
    * WatchPaths    - see the 'LcAddPathToWatch'.

Arguments:

    None.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS       status      = STATUS_SUCCESS;
    UNICODE_STRING valueName   = { 0 };
    UNICODE_STRING stringValue = { 0 };
    ULONG          dwordValue  = 0;

    // Temporary variables for parsing the 'WatchPaths' values.
    PWCHAR         buffer      = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(&Configuration.RegistryPath)), STATUS_INVALID_PARAMETER);
    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Reading configuration from the registry key: '%wZ'\n", Configuration.RegistryPath));

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        //
        // Read the 'ReportRate' value.
        //

        NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&valueName, L"ReportRate"));
        status = LcGetRegistryValueDWord(&Configuration.RegistryPath, &valueName, &dwordValue);
        if (!NT_SUCCESS(status))
        {
            if (status == STATUS_INVALID_PARAMETER)
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] ReportRate value not found\n"));

                LcSetReportRate(0);
                status = STATUS_SUCCESS;
            }
            else
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Unable to get ReportRate value: %08X\n", status));
                __leave;
            }
        }
        else
        {
            LcSetReportRate(dwordValue);
        }

        //
        // Read the 'OperationMode' value.
        //

        NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&valueName, L"OperationMode"));
        status = LcGetRegistryValueDWord(&Configuration.RegistryPath, &valueName, &dwordValue);
        if (!NT_SUCCESS(status))
        {
            if (status == STATUS_INVALID_PARAMETER)
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] OperationMode value not found\n"));

                LcSetOperationMode(DriverDisabled);
                status = STATUS_SUCCESS;
            }
            else
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Unable to get OperationMode value: %08X\n", status));
                __leave;
            }
        }
        else
        {
            LcSetOperationMode((DRIVER_OPERATION_MODE)dwordValue);
        }

        //
        // Read the 'WatchPaths' value.
        //

        LcClearPathsToWatch();

        NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&valueName, L"WatchPaths"));
        status = LcGetRegistryValueString(&Configuration.RegistryPath, &valueName, &stringValue);
        if (!NT_SUCCESS(status))
        {
            if (status == STATUS_INVALID_PARAMETER)
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] WatchPaths value not found\n"));
                status = STATUS_SUCCESS;
            }
            else
            {
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Unable to get WatchPaths value: %08X\n", status));
                __leave;
            }
        }
        else
        {
            __analysis_assume(stringValue.Buffer != NULL);
            buffer = stringValue.Buffer;

            for (;;)
            {
                UNICODE_STRING currentString       = { 0 };
                SIZE_T         currentStringLength = wcslen(buffer);
                if (currentStringLength == 0)
                {
                    break;
                }

                NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&currentString, buffer));
                NT_IF_FAIL_LEAVE(LcAddPathToWatch(&currentString));

                buffer += currentStringLength + 1;
            }

            // Don't forget to free the string before reusing it.
            LcFreeUnicodeString(&stringValue);
        }
    }
    __finally
    {
        if (stringValue.Buffer != NULL)
        {
            LcFreeUnicodeString(&stringValue);
        }

        // At this point driver can be left in a bad state, because of the wrong configuration parameters.
        // Currently we don't support restoring driver configuration to the last known good state.
        if (!NT_SUCCESS(status))
        {
            LcSetOperationMode(DriverDisabled);
            LcSetReportRate(0);
            LcClearPathsToWatch();
        }

        FltReleaseResource(Configuration.Lock);
    }

    return status;
}

//------------------------------------------------------------------------
//  Trusted processes management functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAddTrustedProcess(
    _In_ HANDLE ProcessId
    )
/*++

Summary:

    This function adds the process handle given to the list of trusted processes.

    Trusted processes will not be monitored by this driver.
    It means that if a trusted process is accessing a file that is suitable for fetching,
    driver will skip it the file will not be fetched.

    This is helpful, when we need to allow some application to perform some file operations,
    like rename, without fetching the file.

Arguments:

    ProcessId - Process handle.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS               status              = STATUS_SUCCESS;
    PTRUSTED_PROCESS_ENTRY trustedProcessEntry = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(ProcessId != NULL, STATUS_INVALID_PARAMETER_1);

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        if (LcIsProcessTrusted(ProcessId))
        {
            __leave;
        }

        // Allocate a new list entry.
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&trustedProcessEntry, sizeof(TRUSTED_PROCESS_ENTRY)));
        trustedProcessEntry->ProcessId = ProcessId;

        // Add the new record to the list.
        InsertHeadList(&Configuration.TrustedProccessList, &trustedProcessEntry->ListEntry);

        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Process added to trusted: %p\n", ProcessId));
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }

    return status;
}

//------------------------------------------------------------------------

VOID
LcRemoveTrustedProcess(
    _In_ HANDLE ProcessId
    )
/*++

Summary:

    This function removes the process handle given from the list of trusted processes.

Arguments:

    ProcessId - Process handle.

Return value:

    None.

--*/
{
    PLIST_ENTRY            listEntry           = NULL;
    PTRUSTED_PROCESS_ENTRY trustedProcessEntry = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN(ProcessId != NULL);

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        if (IsListEmpty(&Configuration.TrustedProccessList))
        {
            __leave;
        }

        listEntry = Configuration.TrustedProccessList.Flink;
        while (listEntry != &Configuration.TrustedProccessList)
        {
            trustedProcessEntry = CONTAINING_RECORD(listEntry, TRUSTED_PROCESS_ENTRY, ListEntry);
            if (trustedProcessEntry->ProcessId == ProcessId)
            {
                RemoveEntryList(listEntry);
                LcFreeNonPagedBuffer(trustedProcessEntry);

                break;
            }

            // Move to the next element.
            listEntry = listEntry->Flink;
        }
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }
}

//------------------------------------------------------------------------

_Check_return_
BOOLEAN
LcIsProcessTrusted(
    _In_ HANDLE ProcessId
    )
/*++

Summary:

    This function checks whether the process handle given is in the list of trusted processes.

Arguments:

    ProcessId - Process handle.

Return value:

    Whether the 'ProcessId' is in the list of trusted processes.

--*/
{
    PLIST_ENTRY            listEntry           = NULL;
    PTRUSTED_PROCESS_ENTRY trustedProcessEntry = NULL;
    BOOLEAN                result              = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(ProcessId != NULL, FALSE);

    FltAcquireResourceShared(Configuration.Lock);

    __try
    {
        if (IsListEmpty(&Configuration.TrustedProccessList))
        {
            __leave;
        }

        listEntry = Configuration.TrustedProccessList.Flink;
        while (listEntry != &Configuration.TrustedProccessList)
        {
            trustedProcessEntry = CONTAINING_RECORD(listEntry, TRUSTED_PROCESS_ENTRY, ListEntry);
            if (trustedProcessEntry->ProcessId == ProcessId)
            {
                result = TRUE;
                break;
            }

            // Move to the next element.
            listEntry = listEntry->Flink;
        }
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }

    return result;
}

//------------------------------------------------------------------------

VOID
LcClearTrustedProcesses()
/*++

Summary:

    This function clears the list of trusted processes.

Arguments:

    None.

Return value:

    None.

--*/
{
    PLIST_ENTRY            listEntry           = NULL;
    PTRUSTED_PROCESS_ENTRY trustedProcessEntry = NULL;

    PAGED_CODE();

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        // Remove the last element from the list, while it's not empty.
        while ((listEntry = RemoveTailList(&Configuration.TrustedProccessList)) != &Configuration.TrustedProccessList)
        {
            trustedProcessEntry = CONTAINING_RECORD(listEntry, TRUSTED_PROCESS_ENTRY, ListEntry);
            LcFreeNonPagedBuffer(trustedProcessEntry);
        }
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }
}

//------------------------------------------------------------------------
//  Paths to watch management functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcAddPathToWatch(
    _In_ PCUNICODE_STRING Path
    )
/*++

Summary:

    This function adds the path given to the list of paths to watch.

    When a managed file is accessed such path, the FileAccessed ETW event is raised.

Arguments:

    Path - Pointer to the preallocated unicode string containing the path to be added to the watch list.
           Path must end with the directory separator character.
           The pointer content is copied.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS             status    = STATUS_SUCCESS;
    PPATH_TO_WATCH_ENTRY pathEntry = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(LcValidatePath(Path)), STATUS_INVALID_PARAMETER_1);

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        // If the path is already added to the list, leave.
        if (LcIsPathWatched(Path))
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_WARNING_LEVEL, "[LazyCopy] Path is already in the watch list: '%wZ'\n", Path));
            __leave;
        }

        // Allocate memory for a new list entry.
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&pathEntry, sizeof(PATH_TO_WATCH_ENTRY)));

        // Copy the Path given.
        NT_IF_FAIL_LEAVE(LcCopyUnicodeString(&pathEntry->Path, Path));

        // Add a new record to the list.
        InsertHeadList(&Configuration.PathsToWatch, &pathEntry->ListEntry);
        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Path added to the watch list: '%wZ'\n", pathEntry->Path));
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);

        // Don't forget to free memory on failure.
        if (!NT_SUCCESS(status) && pathEntry != NULL)
        {
            if (pathEntry->Path.Buffer != NULL)
            {
                LcFreeUnicodeString(&pathEntry->Path);
            }

            LcFreeNonPagedBuffer(pathEntry);
        }
    }

    return status;
}

//------------------------------------------------------------------------

_Check_return_
BOOLEAN
LcIsPathWatched(
    _In_ PCUNICODE_STRING Path
    )
/*++

Summary:

    This function checks whether the 'Path' given (or one of its parents) is in the list of paths to watch.

Arguments:

    Path - Pointer to the preallocated unicode string containing the path to be checked.

Return value:

    Whether the 'Path' is in the list of paths to watch.

--*/
{
    PLIST_ENTRY          listEntry = NULL;
    PPATH_TO_WATCH_ENTRY pathEntry = NULL;
    BOOLEAN              result    = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(Path)), FALSE);

    FltAcquireResourceShared(Configuration.Lock);

    __try
    {
        if (IsListEmpty(&Configuration.PathsToWatch))
        {
            __leave;
        }

        listEntry = Configuration.PathsToWatch.Flink;
        while (listEntry != &Configuration.PathsToWatch)
        {
            pathEntry = CONTAINING_RECORD(listEntry, PATH_TO_WATCH_ENTRY, ListEntry);
            if (RtlPrefixUnicodeString(&pathEntry->Path, Path, TRUE))
            {
                result = TRUE;
                break;
            }

            // Move to the next element.
            listEntry = listEntry->Flink;
        }
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }

    return result;
}

//------------------------------------------------------------------------

VOID
LcClearPathsToWatch()
/*++

Summary:

    This function clears the list of paths to watch.

Arguments:

    None.

Return value:

    None.

--*/
{
    PLIST_ENTRY          listEntry = NULL;
    PPATH_TO_WATCH_ENTRY pathEntry = NULL;

    PAGED_CODE();

    FltAcquireResourceExclusive(Configuration.Lock);

    __try
    {
        // Remove the last element from the list while it's not empty.
        while ((listEntry = RemoveTailList(&Configuration.PathsToWatch)) != &Configuration.PathsToWatch)
        {
            pathEntry = CONTAINING_RECORD(listEntry, PATH_TO_WATCH_ENTRY, ListEntry);

            // Free the unicode string and the list entry.
            LcFreeUnicodeString(&pathEntry->Path);
            LcFreeNonPagedBuffer(pathEntry);
        }
    }
    __finally
    {
        FltReleaseResource(Configuration.Lock);
    }
}

//------------------------------------------------------------------------
//  Operation mode management functions.
//------------------------------------------------------------------------

VOID
LcSetOperationMode(
    _In_ DRIVER_OPERATION_MODE Value
    )
/*++

Summary:

    This function sets the new value for the 'Configuration.OperationMode' variable.

Arguments:

    Value - The new operation mode value.

Return value:

    None.

--*/
{
    PAGED_CODE();

    Configuration.OperationMode = Value;
    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Configuration.OperationMode is set to: %08X\n", (ULONG)Value));
}

//------------------------------------------------------------------------

_Check_return_
DRIVER_OPERATION_MODE
LcGetOperationMode()
/*++

Summary:

    This function gets the value of the 'Configuration.OperationMode' variable.

Arguments:

    None.

Return value:

    The current 'Configuration.OperationMode' value.

--*/
{
    PAGED_CODE();

    return Configuration.OperationMode;
}

//------------------------------------------------------------------------
//  Report rate management functions.
//------------------------------------------------------------------------

VOID
LcSetReportRate(
    _In_ ULONG Value
    )
/*++

Summary:

    This function sets the new value for the 'Configuration.ReportRate' variable.

Arguments:

    Value - The new report rate value.
            This value belongs to the [0; 10000] interval and represents the number
            of chances in 10,000 that the current driver will report ETW for a file
            access operation.

Return value:

    None.

--*/
{
    PAGED_CODE();

    if (Value > MAX_REPORT_RATE)
    {
        Value = MAX_REPORT_RATE;
    }

    Configuration.ReportRate = Value;
    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Configuration.ReportRate is set to: %u\n", Value));
}

//------------------------------------------------------------------------

_Check_return_
ULONG
LcGetReportRateForPath(
    _In_ PCUNICODE_STRING Path
    )
/*++

Summary:

    This function checks, whether the 'Path' given (or one of its parents) is in the list
    of paths to watch, and returns the proper report rate for it.

Arguments:

    Path - Pointer to a unicode string containing the path to be checked.

Return value:

    Report rate for the 'Path' given.

    If the 'Path' is not in the list of paths to watch, this function returns zero.

--*/
{
    PAGED_CODE();

    return LcIsPathWatched(Path) ? Configuration.ReportRate : 0;
}

//------------------------------------------------------------------------
//  Local functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcValidatePath(
    _In_ PCUNICODE_STRING Path
    )
/*++

Summary:

    This local function validates the 'Path' value given.

    The valid path should not be empty and end with the directory separator character.

Arguments:

    Path - Path string to validate.

Return value:

    STATUS_SUCCESS - If the 'Path' is a valid UNICODE_STRING and ends with directory
                     separator.

    Anything else  - Otherwise.

--*/
{
    PWCHAR lastPathSeparator = NULL;
    PWCHAR endOfString       = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(Path)), STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Path->Buffer != NULL,                       STATUS_INVALID_PARAMETER_1);

    // Path should at least be large enough to contain the '\\' character.
    if (Path->Length < sizeof(WCHAR))
    {
        return STATUS_INVALID_PARAMETER_1;
    }

    // Check that the path ends with the directory separator character.
    lastPathSeparator = wcsrchr(Path->Buffer, L'\\');
    endOfString = &Path->Buffer[Path->Length / sizeof(WCHAR) - 1];

    // UNICODE_STRING may not be null-terminated.
    // If the last path separator found is not the last character in the string, check that the null-terminator follows it.
    if (lastPathSeparator == NULL || (lastPathSeparator < endOfString && *(lastPathSeparator + 1) != UNICODE_NULL))
    {
        return STATUS_INVALID_PARAMETER_1;
    }

    return STATUS_SUCCESS;
}
