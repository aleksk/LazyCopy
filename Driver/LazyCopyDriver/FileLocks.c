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

    FileLocks.c

Abstract:

    Contains file locking helper function definitions for maintaining
    custom file locks to handle situations, when multiple threads are
    trying to access file that is being fetched from a remote source.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "FileLocks.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Structures.
//------------------------------------------------------------------------

//
// List entry containing information about the locked file.
//
typedef struct _FILE_LOCK_ENTRY
{
    // Path to the locked file.
    UNICODE_STRING  FileName;

    // Event object to synchronize on.
    KEVENT          Event;

    // Reference count for this entry.
    __volatile LONG RefCount;

    LIST_ENTRY      ListEntry;
} FILE_LOCK_ENTRY, *PFILE_LOCK_ENTRY;

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcInitializeFileLocks)
    #pragma alloc_text(PAGE, LcFreeFileLocks)
    #pragma alloc_text(PAGE, LcGetFileLock)
    #pragma alloc_text(PAGE, LcReleaseFileLock)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Global variables.
//------------------------------------------------------------------------

// Used to synchronize access to the 'FileLocksList'.
static PERESOURCE FileLocksResource = { 0 };

// List to store the 'FILE_LOCK_ENTRY' items.
static LIST_ENTRY FileLocksList     = { 0 };

//------------------------------------------------------------------------
//  File locking functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcInitializeFileLocks (
    )
/*++

Summary:

    This function initializes objects used by the current module.

Arguments:

    None.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    InitializeListHead(&FileLocksList);
    NT_IF_FAIL_RETURN(LcAllocateResource(&FileLocksResource));

    return status;
}

VOID
LcFreeFileLocks (
    )
/*++

Summary:

    This function releases objects used by the current module.

    It is not thread-safe, so it should only be called when the driver is about to be unloaded.

Arguments:

    None.

Return value:

    None.

--*/
{
    PLIST_ENTRY      listEntry     = NULL;
    PFILE_LOCK_ENTRY fileLockEntry = NULL;

    PAGED_CODE();

    if (FileLocksList.Flink != NULL)
    {
        // Remove the last element from the list while it's not empty.
        while ((listEntry = RemoveTailList(&FileLocksList)) != &FileLocksList)
        {
            fileLockEntry = CONTAINING_RECORD(listEntry, FILE_LOCK_ENTRY, ListEntry);

            LcFreeUnicodeString(&fileLockEntry->FileName);
            LcFreeNonPagedBuffer(fileLockEntry);
        }
    }

    if (FileLocksResource != NULL)
    {
        LcFreeResource(FileLocksResource);
        FileLocksResource = NULL;
    }
}

_Check_return_
NTSTATUS
LcGetFileLock (
    _In_     PCUNICODE_STRING FileName,
    _Outptr_ PKEVENT*         Event
    )
/*++

Summary:

    This function tries to get a locking event for the file given.

    In this minifilter we don't want to affect the actual filesystem
    behavior and the file object obtained, so we don't use the existing
    file locking mechanisms: oplocks and FILE_LOCK.

    Instead, we maintain a simple list of events that can be used
    for access synchronization.

Arguments:

    FileName - File path to get the locking event for.

    Event    - Pointer to a PKEVENT variable that receives the event created.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS         status        = STATUS_SUCCESS;
    PLIST_ENTRY      listEntry     = NULL;
    PFILE_LOCK_ENTRY fileLockEntry = NULL;
    BOOLEAN          entryFound    = FALSE;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(FileName != NULL, STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Event    != NULL, STATUS_INVALID_PARAMETER_2);

    FltAcquireResourceExclusive(FileLocksResource);

    __try
    {
        // First, look for the entry with the same file name.
        listEntry = FileLocksList.Flink;
        while (listEntry != &FileLocksList)
        {
            fileLockEntry = CONTAINING_RECORD(listEntry, FILE_LOCK_ENTRY, ListEntry);
            if (RtlCompareUnicodeString(FileName, &fileLockEntry->FileName, TRUE) == 0)
            {
                entryFound = TRUE;
                break;
            }

            // Move to the next element.
            listEntry = listEntry->Flink;
        }

        // If the lock entry wasn't found, create a new one.
        if (!entryFound)
        {
            NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&fileLockEntry, sizeof(FILE_LOCK_ENTRY)));
            NT_IF_FAIL_LEAVE(LcCopyUnicodeString(&fileLockEntry->FileName, FileName));
            KeInitializeEvent(&fileLockEntry->Event, SynchronizationEvent, TRUE);
            fileLockEntry->RefCount = 0;

            // Add the new record to the list.
            InsertHeadList(&FileLocksList, &fileLockEntry->ListEntry);
        }

        fileLockEntry->RefCount++;

        *Event = &fileLockEntry->Event;
    }
    __finally
    {
        FltReleaseResource(FileLocksResource);

        if (!NT_SUCCESS(status) && fileLockEntry != NULL)
        {
            if (fileLockEntry->FileName.Buffer != NULL)
            {
                LcFreeUnicodeString(&fileLockEntry->FileName);
            }

            LcFreeNonPagedBuffer(fileLockEntry);
        }
    }

    return status;
}

VOID
LcReleaseFileLock (
    _In_ PKEVENT Event
    )
/*++

Summary:

    This function releases the custom file lock based on the event given.

    The actual entry in the locks list may not be freed after this method
    completes, because we maintain reference counter. When it reaches zero,
    entry will be removed.

    Event is set to the signaled state in this function.

Arguments:

    Event - Event to be released.

Return value:

    None.

--*/
{
    PLIST_ENTRY      listEntry     = NULL;
    PFILE_LOCK_ENTRY fileLockEntry = NULL;

    PAGED_CODE();

    FltAcquireResourceExclusive(FileLocksResource);

    __try
    {
        // Look for the entry with the same event.
        listEntry = FileLocksList.Flink;
        while (listEntry != &FileLocksList)
        {
            fileLockEntry = CONTAINING_RECORD(listEntry, FILE_LOCK_ENTRY, ListEntry);
            if (&fileLockEntry->Event == Event)
            {
                // Decrease the reference count and remove the list entry, if it reaches zero.
                fileLockEntry->RefCount--;
                FLT_ASSERT(fileLockEntry->RefCount >= 0);

                if (fileLockEntry->RefCount <= 0)
                {
                    RemoveEntryList(listEntry);

                    LcFreeUnicodeString(&fileLockEntry->FileName);
                    LcFreeNonPagedBuffer(fileLockEntry);
                }
                else
                {
                    KeSetEvent(&fileLockEntry->Event, IO_NO_INCREMENT, FALSE);
                }

                break;
            }

            // Move to the next element.
            listEntry = listEntry->Flink;
        }
    }
    __finally
    {
        FltReleaseResource(FileLocksResource);
    }
}
