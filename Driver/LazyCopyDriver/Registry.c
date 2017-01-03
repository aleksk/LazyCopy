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

    Registry.c

Abstract:

    Contains Registry helper functions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Registry.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcGetRegistryValue)
    #pragma alloc_text(PAGE, LcGetRegistryValueDWord)
    #pragma alloc_text(PAGE, LcGetRegistryValueString)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Registry helper functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcGetRegistryValue(
    _In_     PUNICODE_STRING                 RegistryPath,
    _In_     PUNICODE_STRING                 RegistryValueName,
    _Outptr_ PKEY_VALUE_PARTIAL_INFORMATION* ValueBuffer
    )
/*++

Summary:

    This function retrieves the registry value from the path given.

    Caller should free the 'ValueBuffer' received using the 'LcFreeNonPagedBuffer' function.

Arguments:

    RegistryPath      - Path from where the registry value should be read.

    RegistryValueName - Name of the registry value to read.

    ValueBuffer       - A pointer to a variable that receives the value content.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                       status            = STATUS_SUCCESS;

    // Registry key handle.
    HANDLE                         registryKeyHandle = NULL;
    OBJECT_ATTRIBUTES              attributes        = { 0 };

    // Buffer to read the value data into.
    PKEY_VALUE_PARTIAL_INFORMATION valueBuffer       = NULL;
    ULONG                          valueLength       = 0;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(RegistryPath)),      STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(RegistryPath->Buffer != NULL,                            STATUS_INVALID_PARAMETER_1);

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(RegistryValueName)), STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(RegistryValueName->Buffer != NULL,                       STATUS_INVALID_PARAMETER_2);

    IF_FALSE_RETURN_RESULT(ValueBuffer != NULL,                                     STATUS_INVALID_PARAMETER_3);

    __try
    {
        // Open the registry key given.
        InitializeObjectAttributes(&attributes, RegistryPath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
        NT_IF_FAIL_LEAVE(ZwOpenKey(&registryKeyHandle, KEY_READ, &attributes));

        // Get the length of the value for proper buffer allocation.
        status = ZwQueryValueKey(registryKeyHandle, RegistryValueName, KeyValuePartialInformation, NULL, 0, &valueLength);
        if (status != STATUS_BUFFER_TOO_SMALL && status != STATUS_BUFFER_OVERFLOW)
        {
            status = STATUS_INVALID_PARAMETER;
            __leave;
        }

        // Allocate buffer and read value into it.
        NT_IF_FAIL_LEAVE(LcAllocateNonPagedBuffer((PVOID*)&valueBuffer, valueLength));
        NT_IF_FAIL_LEAVE(ZwQueryValueKey(registryKeyHandle, RegistryValueName, KeyValuePartialInformation, valueBuffer, valueLength, &valueLength));

        // Set the output value and set the 'valueBuffer' to NULL, so it won't be freed in the __finally block.
        *ValueBuffer = valueBuffer;
        valueBuffer = NULL;
    }
    __finally
    {
        if (registryKeyHandle != NULL)
        {
            ZwClose(registryKeyHandle);
        }

        if (valueBuffer != NULL)
        {
            LcFreeNonPagedBuffer(valueBuffer);
        }
    }

    return status;
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcGetRegistryValueDWord(
    _In_  PUNICODE_STRING RegistryPath,
    _In_  PUNICODE_STRING RegistryValueName,
    _Out_ PULONG          Value
    )
/*++

Summary:

    This function reads a REG_DWORD value from the path given.

Arguments:

    RegistryPath      - Path to the registry value to be read.

    RegistryValueName - Registry value name, which value to read.

    Value             - A pointer to a ULONG variable that receives the DWORD value read.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                       status      = STATUS_SUCCESS;
    PKEY_VALUE_PARTIAL_INFORMATION valueBuffer = NULL;

    PAGED_CODE();

    // Other parameters will be validated in the 'LcGetRegistryValue' function.
    IF_FALSE_RETURN_RESULT(Value != NULL, STATUS_INVALID_PARAMETER_3);

    __try
    {
        NT_IF_FAIL_LEAVE(LcGetRegistryValue(RegistryPath, RegistryValueName, &valueBuffer));

        // We are expecting a DWORD value.
        NT_IF_FALSE_LEAVE(valueBuffer->Type == REG_DWORD, STATUS_INVALID_PARAMETER);

        *Value = *(PULONG)&valueBuffer->Data;
    }
    __finally
    {
        if (valueBuffer != NULL)
        {
            LcFreeNonPagedBuffer(valueBuffer);
        }
    }

    return status;
}

//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcGetRegistryValueString(
    _In_  PUNICODE_STRING RegistryPath,
    _In_  PUNICODE_STRING RegistryValueName,
    _Out_ PUNICODE_STRING Value
    )
/*++

Summary:

    This function reads REG_MULTI_SZ or REG_SZ value from the path given.

    Caller should manually free the string received using the 'LcFreeUnicodeString' function.

Arguments:

    RegistryPath      - Path to the registry value to be read.

    RegistryValueName - Registry value name, which value to read.

    Value             - A pointer to a unicode string that receives the value.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                       status      = STATUS_SUCCESS;
    PKEY_VALUE_PARTIAL_INFORMATION valueBuffer = NULL;
    UNICODE_STRING                 string      = { 0 };

    PAGED_CODE();

    // Other parameters will be validated in the 'LcGetRegistryValue' function.
    IF_FALSE_RETURN_RESULT(Value != NULL, STATUS_INVALID_PARAMETER_3);

    __try
    {
        NT_IF_FAIL_LEAVE(LcGetRegistryValue(RegistryPath, RegistryValueName, &valueBuffer));

        // We are expecting REG_MULTI_SZ or REG_SZ value.
        NT_IF_FALSE_LEAVE(valueBuffer->Type == REG_MULTI_SZ || valueBuffer->Type == REG_SZ, STATUS_INVALID_PARAMETER);

        // Allocate internal string buffer and copy the value there.
        NT_IF_FAIL_LEAVE(LcAllocateUnicodeString(&string, (USHORT)valueBuffer->DataLength));
        __analysis_assume(string.Buffer != NULL);
        RtlCopyMemory(string.Buffer, &valueBuffer->Data, valueBuffer->DataLength);
        string.Length = (USHORT)valueBuffer->DataLength - sizeof(WCHAR);

        // Set the output value and set the 'string.Buffer' to NULL, so it won't be freed in the __finally block.
        *Value        = string;
        string.Buffer = NULL;
    }
    __finally
    {
        if (valueBuffer != NULL)
        {
            LcFreeNonPagedBuffer(valueBuffer);
        }

        if (string.Buffer != NULL)
        {
            LcFreeUnicodeString(&string);
        }
    }

    return status;
}
