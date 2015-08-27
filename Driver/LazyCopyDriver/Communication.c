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

    Communication.c

Abstract:

    Contains communication function definitions.

Environment:

    Kernel mode.

--*/

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Communication.h"
#include "CommunicationData.h"
#include "Configuration.h"
#include "Utilities.h"

//------------------------------------------------------------------------
//  Defines.
//------------------------------------------------------------------------

// Communication port name.
#define DEFAULT_PORT_NAME  L"\\LazyCopyDriverPort"

//------------------------------------------------------------------------
//  Local type definitions.
//------------------------------------------------------------------------

//
// Handler for the commands that are received from the user-mode client.
//
typedef
_Check_return_
NTSTATUS
(*CommandHandler) (
    PVOID  InputBuffer,
    ULONG  InputBufferSize,
    PVOID  OutputBuffer,
    ULONG  OutputBufferSize,
    PULONG ReturnOutputBufferLength
    );

//------------------------------------------------------------------------
//  Local variables.
//------------------------------------------------------------------------

// User-mode client process Id.
static __volatile HANDLE    ClientProcessId     = NULL;

// User-mode client process handle.
static __volatile HANDLE    ClientProcessHandle = NULL;

// Handle to the system process.
static __volatile HANDLE    SystemProcessHandle = NULL;

// Server port listens for incoming connections.
static __volatile PFLT_PORT ServerPort          = NULL;

// Used to send notifications to the user-mode client.
static __volatile PFLT_PORT ClientPort          = NULL;

//------------------------------------------------------------------------
//  Local function prototype declarations.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcCommunicationPortConnect (
    _In_                                PFLT_PORT Port,
    _In_opt_                            PVOID     ServerPortCookie,
    _In_reads_bytes_opt_(SizeOfContext) PVOID     ConnectionContext,
    _In_                                ULONG     SizeOfContext,
    _Outptr_result_maybenull_           PVOID*    ConnectionCookie
    );

static
VOID
LcCommunicationPortDisconnect (
    _In_opt_ PVOID ConnectionCookie
    );

static
VOID
LcResetConnectionVariables (
    );

static
_Check_return_
NTSTATUS
LcClientMessageReceived (
    _In_                                                                   PVOID  ConnectionCookie,
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

static
_Check_return_
NTSTATUS
LcSendMessageToClient (
    _In_                                             DRIVER_NOTIFICATION_TYPE NotificationType,
    _In_reads_bytes_(DataLength)                     PVOID                    Data,
    _In_                                             ULONG                    DataLength,
    _Inout_updates_bytes_all_opt_(ReplyBufferLength) PVOID                    ReplyBuffer,
    _In_opt_                                         ULONG                    ReplyBufferLength
    );

static
LONG
LcDriverExceptionFilter (
    _In_ PEXCEPTION_POINTERS ExceptionPointer,
    _In_ BOOLEAN             AccessingUserBuffer
    );

//------------------------------------------------------------------------
//  Command handlers.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcGetDriverVersionHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

static
_Check_return_
NTSTATUS
LcReadRegistryParametersHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

static
_Check_return_
NTSTATUS
LcSetOperationModeHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

static
_Check_return_
NTSTATUS
LcSetWatchPathsHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

static
_Check_return_
NTSTATUS
LcSetReportRateHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    );

//------------------------------------------------------------------------
//  Additional validation functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcValidateBufferAlignment (
    _In_ PVOID Buffer
    );

//------------------------------------------------------------------------
//  Text sections.
//------------------------------------------------------------------------

#ifdef ALLOC_PRAGMA
    #pragma alloc_text(PAGE, LcCreateCommunicationPort)
    #pragma alloc_text(PAGE, LcCloseCommunicationPort)

    // Notifications.
    #pragma alloc_text(PAGE, LcOpenFileInUserMode)
    #pragma alloc_text(PAGE, LcCloseFileHandle)

    // Local functions.
    #pragma alloc_text(PAGE, LcCommunicationPortConnect)
    #pragma alloc_text(PAGE, LcCommunicationPortDisconnect)
    #pragma alloc_text(PAGE, LcResetConnectionVariables)
    #pragma alloc_text(PAGE, LcClientMessageReceived)
    #pragma alloc_text(PAGE, LcSendMessageToClient)
    #pragma alloc_text(PAGE, LcDriverExceptionFilter)

    // Command handlers.
    #pragma alloc_text(PAGE, LcGetDriverVersionHandler)
    #pragma alloc_text(PAGE, LcReadRegistryParametersHandler)
    #pragma alloc_text(PAGE, LcSetOperationModeHandler)
    #pragma alloc_text(PAGE, LcSetWatchPathsHandler)
    #pragma alloc_text(PAGE, LcSetReportRateHandler)

    // Additional validation functions.
    #pragma alloc_text(PAGE, LcValidateBufferAlignment)
#endif // ALLOC_PRAGMA

//------------------------------------------------------------------------
//  Functions.
//------------------------------------------------------------------------

_Check_return_
NTSTATUS
LcCreateCommunicationPort (
    )
/*++

Summary:

    This function creates a communication port, which can be used by a
    user-mode client.

    In order to use the port created, client must be running with the
    SYSTEM or ADMIN privileges.

    Only one simultaneous client is allowed.

Arguments:

    None.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status                         = STATUS_SUCCESS;
    UNICODE_STRING portName                 = { 0 };
    OBJECT_ATTRIBUTES objectAttributes      = { 0 };
    PSECURITY_DESCRIPTOR securityDescriptor = NULL;

    PAGED_CODE();

    __try
    {
        // Initialize the port name string.
        NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&portName, DEFAULT_PORT_NAME));

        // We secure the port so only ADMINs & SYSTEM can acecss it.
        NT_IF_FAIL_LEAVE(FltBuildDefaultSecurityDescriptor(&securityDescriptor, FLT_PORT_ALL_ACCESS));

        InitializeObjectAttributes(
            &objectAttributes,                         // [out] Structure to be initialized.
            &portName,                                 // [in]  Name of the object for which a handle is to be opened.
            OBJ_CASE_INSENSITIVE                       // [in]  Case-insensitive comparison is used when matching the 'uniString' value.
                | OBJ_KERNEL_HANDLE,                   //       The handle can only be accessed in kernel mode.
            NULL,                                      // [in]  A handle to the root object directory for the path name specified.
            securityDescriptor);                       // [in]  Security descriptor to apply to an object when it is created.

        NT_IF_FAIL_LEAVE(FltCreateCommunicationPort(
            Globals.Filter,                            // [in] Filter pointer for the caller.
            &ServerPort,                               // [in] Port handle for the communication server port.
            &objectAttributes,                         // [in] Attributes of the communication server port.
            NULL,                                      // [in] Context information.
            (PFLT_CONNECT_NOTIFY)                      // [in] Connection callback function.
                LcCommunicationPortConnect,
            (PFLT_DISCONNECT_NOTIFY)                   // [in] Connection termination callback function.
                LcCommunicationPortDisconnect,
            (PFLT_MESSAGE_NOTIFY)                      // [in] Message notification callback function.
                LcClientMessageReceived,
            1));                                       // [in] Maximum number of simultaneous client connections allowed.

        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Communication port created.\n"));
    }
    __finally
    {
        // Free the security descriptor in all cases.
        // It is not needed once the call to 'FltCreateCommunicationPort()' is made.
        if (securityDescriptor != NULL)
        {
            FltFreeSecurityDescriptor(securityDescriptor);
        }

        if (!NT_SUCCESS(status))
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "[LazyCopy] Unable to create communication port: 0x%X\n", status));
            LcCloseCommunicationPort();
        }
    }

    return status;
}

VOID
LcCloseCommunicationPort (
    )
/*++

Summary:

    This function closes the previously opened communication port.

Arguments:

    None.

Return value:

    None.

--*/
{
    PAGED_CODE();

    if (ServerPort != NULL)
    {
        FltCloseCommunicationPort(ServerPort);
        ServerPort = NULL;
    }

    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Communication port closed.\n"));
}

_Check_return_
NTSTATUS
LcOpenFileInUserMode (
    _In_  PCUNICODE_STRING FilePath,
    _Out_ PHANDLE          Handle
    )
/*++

Summary:

    This function asks user-mode client to open file.

Arguments:

    FilePath - Path to the file to be opened.

    Handle   - Handle received from the client.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                      status = STATUS_SUCCESS;
    PFILE_OPEN_NOTIFICATION_DATA  data   = NULL;
    PFILE_OPEN_NOTIFICATION_REPLY reply  = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(NT_SUCCESS(RtlUnicodeStringValidate(FilePath)), STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(Handle != NULL,                                 STATUS_INVALID_PARAMETER_2);

    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Sending open notification for file: '%wZ'\n", FilePath));

    __try
    {
        // Don't forget to reserve space for a null-termination character.
        const        ULONG dataSize  = sizeof(FILE_OPEN_NOTIFICATION_DATA) + FilePath->Length + sizeof(WCHAR);
        static const ULONG replySize = sizeof(FILTER_REPLY_HEADER) + sizeof(FILE_OPEN_NOTIFICATION_REPLY);

        NT_IF_FAIL_LEAVE(LcAllocateBuffer((PVOID*)&data,  NonPagedPool, dataSize,  LC_COMMUNICATION_NON_PAGED_POOL_TAG));
        NT_IF_FAIL_LEAVE(LcAllocateBuffer((PVOID*)&reply, NonPagedPool, replySize, LC_COMMUNICATION_NON_PAGED_POOL_TAG));

        RtlCopyMemory(data->FilePath, FilePath->Buffer, FilePath->Length);

        // Send message to the user-mode client.
        NT_IF_FAIL_LEAVE(LcSendMessageToClient(OpenFileInUserMode, data, dataSize, reply, replySize));

        //
        // Duplicate the file handle received.
        //

        NT_IF_FAIL_LEAVE(ZwDuplicateObject(
            ClientProcessHandle,
            reply->FileHandle,
            SystemProcessHandle,
            Handle,
            FILE_READ_DATA | FILE_WRITE_DATA,
            OBJ_KERNEL_HANDLE,
            DUPLICATE_SAME_ATTRIBUTES | DUPLICATE_SAME_ACCESS));
    }
    __finally
    {
        if (data != NULL)
        {
            LcFreeBuffer(data, LC_COMMUNICATION_NON_PAGED_POOL_TAG);
        }

        if (reply != NULL)
        {
            if (reply->FileHandle != NULL)
            {
                #pragma warning(suppress: 6031) // Ignore the return value here.
                LcCloseFileHandle(reply->FileHandle);
            }

            LcFreeBuffer(reply, LC_COMMUNICATION_NON_PAGED_POOL_TAG);
        }
    }

    return status;
}

_Check_return_
NTSTATUS
LcCloseFileHandle (
    _In_  HANDLE FileHandle
    )
/*++

Summary:

    This function notifies the user-mode client that the handle
    can be closed.

Arguments:

    FileHandle - Handle to be closed.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS                      status = STATUS_SUCCESS;
    PFILE_CLOSE_NOTIFICATION_DATA data   = NULL;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(FileHandle != NULL, STATUS_INVALID_PARAMETER_1);

    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Sending close notification for handle: 0x%p\n", FileHandle));

    __try
    {
        static const ULONG dataSize  = sizeof(FILE_CLOSE_NOTIFICATION_DATA);
        NT_IF_FAIL_LEAVE(LcAllocateBuffer((PVOID*)&data, NonPagedPool, dataSize, LC_COMMUNICATION_NON_PAGED_POOL_TAG));
        data->FileHandle = FileHandle;

        // Send message to the user-mode client.
        NT_IF_FAIL_LEAVE(LcSendMessageToClient(CloseFileHandle, data, dataSize, NULL, 0));
    }
    __finally
    {
        if (data != NULL)
        {
            LcFreeBuffer(data, LC_COMMUNICATION_NON_PAGED_POOL_TAG);
        }
    }

    return status;
}

//------------------------------------------------------------------------
//  Local functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcCommunicationPortConnect (
    _In_                                PFLT_PORT Port,
    _In_opt_                            PVOID     ServerPortCookie,
    _In_reads_bytes_opt_(SizeOfContext) PVOID     ConnectionContext,
    _In_                                ULONG     SizeOfContext,
    _Outptr_result_maybenull_           PVOID*    ConnectionCookie
    )
/*++

Summary:

    This function is called when user-mode connects to the server port to establish a
    connection.

Arguments:

    Port              - Client connection port that will be used to send messages
                        from the filter.

    ServerPortCookie  - The context associated with this port when the minifilter
                        created this port.

    ConnectionContext - Context from entity connecting to this port (most likely
                        your user mode service).

    SizeOfContext     - Size of 'ConnectionContext', in bytes.

    ConnectionCookie  - Context to be passed to the port disconnect function.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS          status           = STATUS_SUCCESS;
    OBJECT_ATTRIBUTES objectAttributes = { 0 };
    CLIENT_ID         clientId         = { 0 };

    PAGED_CODE();

    UNREFERENCED_PARAMETER(ServerPortCookie);
    UNREFERENCED_PARAMETER(ConnectionContext);
    UNREFERENCED_PARAMETER(SizeOfContext);

    FLT_ASSERT(Port             != NULL);
    FLT_ASSERT(ConnectionCookie != NULL);

    FLT_ASSERT(ClientPort       == NULL);
    FLT_ASSERT(ClientProcessId  == NULL);

    // Set the cookie value.
    *ConnectionCookie = NULL;
    ClientPort        = Port;
    ClientProcessId   = PsGetCurrentProcessId();

    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "[LazyCopy] Client connected to port 0x%p\n", ClientPort));

    __try
    {
        // Obtain handles to the client and system processes.
        InitializeObjectAttributes(&objectAttributes, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);
        clientId.UniqueProcess = ClientProcessId;
        clientId.UniqueThread  = NULL;

        NT_IF_FAIL_LEAVE(ZwOpenProcess(
            &ClientProcessHandle,
            GENERIC_READ,
            &objectAttributes,
            &clientId));

        NT_IF_FAIL_LEAVE(ObOpenObjectByPointer(
            PsInitialSystemProcess,
            OBJ_KERNEL_HANDLE,
            NULL,
            STANDARD_RIGHTS_READ,
            NULL,
            KernelMode,
            &SystemProcessHandle));

        // We trust the client process.
        NT_IF_FAIL_LEAVE(LcAddTrustedProcess(ClientProcessId));
    }
    __finally
    {
        if (!NT_SUCCESS(status))
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "[LazyCopy] Unable to accept client connection: 0x%X\n", status));

            LcResetConnectionVariables();
        }
    }

    return status;
}

static
VOID
LcCommunicationPortDisconnect (
    _In_opt_ PVOID ConnectionCookie
    )
/*++

Summary:

    This function is called when the connection is torn-down.
    We use it to close our connection handle.

Arguments:

    ConnectionCookie - Context from the port connect function.

Return value:

    None.

--*/
{
    PAGED_CODE();

    UNREFERENCED_PARAMETER(ConnectionCookie);

    // Close the connection handle. This will set the 'ClientPort' to NULL.
    // NOTE: Since we limited max connections to 1, another connect request
    //       won't won't be allowed until we return from the disconnect function.
    FltCloseClientPort(Globals.Filter, &ClientPort);

    LcResetConnectionVariables();

    LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "[LazyCopy] Client disconnected.\n"));
}

static
VOID
LcResetConnectionVariables (
    )
/*++

Summary:

    This function resets the connection-related variables.

Arguments:

    None.

Return value:

    None.

--*/
{
    PAGED_CODE();

    if (ClientProcessHandle != NULL)
    {
        ZwClose(ClientProcessHandle);
        ClientProcessHandle = NULL;
    }

    if (SystemProcessHandle != NULL)
    {
        ZwClose(SystemProcessHandle);
        SystemProcessHandle = NULL;
    }

    if (ClientProcessId != NULL)
    {
        LcRemoveTrustedProcess(ClientProcessId);
        ClientProcessId = NULL;
    }
}

static
_Check_return_
NTSTATUS
LcClientMessageReceived (
    _In_                                                                   PVOID  ConnectionCookie,
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function is called whenever the user mode client wishes to communicate
    with this driver.

Arguments:

    ConnectionCookie         - Unused.

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size in bytes of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size in bytes of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size in bytes of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS            status         = STATUS_SUCCESS;
    DRIVER_COMMAND_TYPE command        = 0;
    ULONG               dataLength     = 0;
    CommandHandler      commandHandler = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(ConnectionCookie);

    IF_FALSE_RETURN_RESULT(InputBuffer != NULL, STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(InputBufferSize > 0, STATUS_INVALID_PARAMETER_3);
    IF_FALSE_RETURN_RESULT((OutputBuffer != NULL) == (OutputBufferSize > 0), STATUS_INVALID_PARAMETER_4);
    IF_FALSE_RETURN_RESULT(ReturnOutputBufferLength != NULL, STATUS_INVALID_PARAMETER_6);

    // Validate the output buffer alignment.
    if (OutputBuffer != NULL)
    {
        #pragma warning(suppress: 6001) // Using uninitialized memory '*OutputBuffer'.
        NT_IF_FAIL_RETURN(LcValidateBufferAlignment(OutputBuffer));
    }

    // Probe and capture input message: the message is raw user-mode buffer, so we need to protect it with the exception handler.
    __try
    {
        command    = ((PDRIVER_COMMAND)InputBuffer)->Type;
        dataLength = ((PDRIVER_COMMAND)InputBuffer)->DataLength;

        // Make sure that the buffer is large enough to contain the DRIVER_COMMAND structure.
        IF_FALSE_RETURN_RESULT(InputBufferSize >= (FIELD_OFFSET(DRIVER_COMMAND, Data) + dataLength), STATUS_BUFFER_TOO_SMALL);
    }
    __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
    {
        return GetExceptionCode();
    }

    // Get the command handler based on the command type received.
    switch (command)
    {
        // Driver environment commands.
        case GetDriverVersion:
            commandHandler = &LcGetDriverVersionHandler;
            break;

        // Driver configuration commands.
        case ReadRegistryParameters:
            commandHandler = &LcReadRegistryParametersHandler;
            break;
        case SetOperationMode:
            commandHandler = &LcSetOperationModeHandler;
            break;
        case SetWatchPaths:
            commandHandler = &LcSetWatchPathsHandler;
            break;
        case SetReportRate:
            commandHandler = &LcSetReportRateHandler;
            break;

        default:
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "[LazyCopy] Command not supported: %d\n", command));
            return STATUS_NOT_SUPPORTED;
    }

    __try
    {
        // Set the length to zero in case it's not set in the command handler.
        *ReturnOutputBufferLength = 0;

        // Execute the command handler found passing the actual 'Data' and 'DataLength' parameters, so the command handler will not
        // need to access these fields again.
        status = commandHandler(
            dataLength > 0 ? &((PDRIVER_COMMAND)InputBuffer)->Data : NULL,
            dataLength,
            OutputBuffer,
            OutputBufferSize,
            ReturnOutputBufferLength);

        if (!NT_SUCCESS(status))
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_ERROR_LEVEL, "[LazyCopy] Command handler failed: 0x%X\n", status));
            FLT_ASSERTMSG("Command handler failed.", FALSE);
        }
    }
    __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
    {
        return GetExceptionCode();
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcSendMessageToClient (
    _In_                                             DRIVER_NOTIFICATION_TYPE NotificationType,
    _In_reads_bytes_(DataLength)                     PVOID                    Data,
    _In_                                             ULONG                    DataLength,
    _Inout_updates_bytes_all_opt_(ReplyBufferLength) PVOID                    ReplyBuffer,
    _In_opt_                                         ULONG                    ReplyBufferLength
    )
/*++

Summary:

    This function sends the notification message to the connected user-mode client.

Arguments:

    NotificationType  - Notification message type.

    Data              - A buffer containing input data. Cannot be NULL.

    DataLength        - Length of the 'Data' buffer.

    ReplyBuffer       - A buffer where the user-mode client reply should be written to.
                        May be NULL, if no client reply is expected.

    ReplyBufferLength - Length of the 'ReplyBuffer' buffer.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS             status           = STATUS_SUCCESS;
    PDRIVER_NOTIFICATION notification     = NULL;
    const ULONG          notificationSize = sizeof(DRIVER_NOTIFICATION) + DataLength;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Data != NULL,   STATUS_INVALID_PARAMETER_2);
    IF_FALSE_RETURN_RESULT(DataLength > 0, STATUS_INVALID_PARAMETER_3);

    IF_FALSE_RETURN_RESULT(ReplyBuffer != NULL ? ReplyBufferLength >= sizeof(FILTER_REPLY_HEADER) : ReplyBufferLength == 0, STATUS_INVALID_PARAMETER_5);

    __try
    {
        // If the client is not connected, don't send the notification.
        NT_IF_FALSE_LEAVE(ClientPort != NULL, STATUS_PORT_DISCONNECTED);

        // Allocate enough memory for the notification.
        NT_IF_FAIL_LEAVE(LcAllocateBuffer((PVOID*)&notification, NonPagedPool, notificationSize, LC_COMMUNICATION_NON_PAGED_POOL_TAG));
        notification->Type       = NotificationType;
        notification->DataLength = DataLength;

        // Copy the data buffer to the notification message.
        RtlCopyMemory(&notification->Data, Data, DataLength);

        // Send message to the user-mode client.
        NT_IF_FAIL_LEAVE(FltSendMessage(Globals.Filter, &ClientPort, notification, notificationSize, ReplyBuffer, (PULONG)&ReplyBufferLength, NULL));
    }
    __finally
    {
        if (notification != NULL)
        {
            LcFreeBuffer(notification, LC_COMMUNICATION_NON_PAGED_POOL_TAG);
        }

        if (!NT_SUCCESS(status))
        {
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_WARNING_LEVEL, "[LazyCopy] Unable to send message to the client: 0x%X\n", status));
        }
    }

    return status;
}

static
LONG
LcDriverExceptionFilter (
    _In_ PEXCEPTION_POINTERS ExceptionPointer,
    _In_ BOOLEAN             AccessingUserBuffer
    )
/*++

Summary:

    Exception filter to catch errors while working with the user-mode buffers.

Arguments:

    ExceptionPointer    - The exception record.

    AccessingUserBuffer - If TRUE, overrides 'FsRtlIsNtStatusExpected' to allow
                          the caller to munge the error to a desired status.

Return value:

    EXCEPTION_EXECUTE_HANDLER - If the exception handler should be executed.

    EXCEPTION_CONTINUE_SEARCH - If a higher exception handler should take care of
                                this exception.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    FLT_ASSERT(ExceptionPointer                  != NULL);
    FLT_ASSERT(ExceptionPointer->ExceptionRecord != NULL);

    status = ExceptionPointer->ExceptionRecord->ExceptionCode;

    // Certain exceptions shouldn't be dismissed within the minifilter
    // unless we're touching user memory.
    if (!FsRtlIsNtstatusExpected(status) && !AccessingUserBuffer)
    {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    return EXCEPTION_EXECUTE_HANDLER;
}

//------------------------------------------------------------------------
//  Command handlers.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcGetDriverVersionHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function handles the 'GetDriverVersion' command received from a user-mode client.

    It sends back the response with the current driver version information based on the
    LC_MAJOR_VERSION and LC_MINOR_VERSION defines.

Arguments:

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size, in bytes, of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size, in bytes, of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size, in bytes, of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    // The 'GetDriverVersion' command does not contain any data.
    UNREFERENCED_PARAMETER(InputBuffer);
    UNREFERENCED_PARAMETER(InputBufferSize);

    // Verify we have a valid output buffer.
    IF_FALSE_RETURN_RESULT(OutputBuffer != NULL,                       STATUS_INVALID_PARAMETER_3);
    IF_FALSE_RETURN_RESULT(OutputBufferSize >= sizeof(DRIVER_VERSION), STATUS_INVALID_PARAMETER_4);

    // Protect access to the raw user-mode output buffer with an exception handler.
    __try
    {
        LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "[LazyCopy] Returning driver version %d.%d\n", LC_MAJOR_VERSION, LC_MINOR_VERSION));

        ((PDRIVER_VERSION)OutputBuffer)->Major = LC_MAJOR_VERSION;
        ((PDRIVER_VERSION)OutputBuffer)->Minor = LC_MINOR_VERSION;

        *ReturnOutputBufferLength = sizeof(DRIVER_VERSION);
    }
    __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
    {
        status = GetExceptionCode();
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcReadRegistryParametersHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function handles the 'ReadRegistryParameters' command received from a user-mode client.

Arguments:

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size, in bytes, of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size, in bytes, of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size, in bytes, of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(InputBuffer);
    UNREFERENCED_PARAMETER(InputBufferSize);
    UNREFERENCED_PARAMETER(OutputBuffer);
    UNREFERENCED_PARAMETER(OutputBufferSize);

    *ReturnOutputBufferLength = 0;

    FltAcquireResourceExclusive(Globals.Lock);

    __try
    {
        status = LcReadConfigurationFromRegistry();
    }
    __finally
    {
        FltReleaseResource(Globals.Lock);
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcSetOperationModeHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function handles the 'SetOperationMode' command received from a user-mode client.

Arguments:

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size, in bytes, of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size, in bytes, of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size, in bytes, of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS        status        = STATUS_SUCCESS;
    POPERATION_MODE operationMode = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(OutputBuffer);
    UNREFERENCED_PARAMETER(OutputBufferSize);

    // Input buffer should at least contain the 'PathCount' value.
    IF_FALSE_RETURN_RESULT(InputBuffer != NULL,                       STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(InputBufferSize >= sizeof(OPERATION_MODE), STATUS_INVALID_PARAMETER_2);

    *ReturnOutputBufferLength = 0;

    FltAcquireResourceExclusive(Globals.Lock);

    __try
    {
        __try
        {
            operationMode = (POPERATION_MODE)InputBuffer;
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Setting operation mode to: %u\n", operationMode->Mode));

            LcSetOperationMode((DRIVER_OPERATION_MODE)operationMode->Mode);
        }
        __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
        {
            status = GetExceptionCode();
        }
    }
    __finally
    {
        FltReleaseResource(Globals.Lock);
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcSetWatchPathsHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function handles the 'SetWatchPaths' command received from a user-mode client.

Arguments:

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size, in bytes, of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size, in bytes, of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size, in bytes, of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS     status     = STATUS_SUCCESS;
    PWATCH_PATHS watchPaths = NULL;
    PWCHAR       buffer     = NULL;
    ULONG        idx        = 0;

    const ULONG_PTR bufferEnd = (ULONG_PTR)InputBuffer + InputBufferSize;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(OutputBuffer);
    UNREFERENCED_PARAMETER(OutputBufferSize);

    // Input buffer should at least contain the 'PathCount' value.
    IF_FALSE_RETURN_RESULT(InputBuffer != NULL,                                       STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(InputBufferSize >= (ULONG)FIELD_OFFSET(WATCH_PATHS, Data), STATUS_INVALID_PARAMETER_2);

    *ReturnOutputBufferLength = 0;

    FltAcquireResourceExclusive(Globals.Lock);

    __try
    {
        __try
        {
            // Free the previous list before populating it again.
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Clearing previous paths to watch.\n"));
            LcClearPathsToWatch();

            watchPaths = (PWATCH_PATHS)InputBuffer;
            buffer = watchPaths->Data;

            for (idx = 0; idx < watchPaths->PathCount; idx++)
            {
                UNICODE_STRING currentString       = { 0 };
                SIZE_T         currentStringLength = wcslen(buffer);

                NT_IF_FALSE_LEAVE(bufferEnd >= (ULONG_PTR)buffer + (currentStringLength + 1) * sizeof(WCHAR), STATUS_INVALID_BUFFER_SIZE);

                NT_IF_FAIL_LEAVE(RtlInitUnicodeStringEx(&currentString, buffer));
                LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Adding path to watch: '%wZ'\n", currentString));

                NT_IF_FAIL_LEAVE(LcAddPathToWatch(&currentString));

                // Move to the next string in the buffer.
                buffer += currentStringLength + 1;
            }
        }
        __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
        {
            status = GetExceptionCode();
        }
    }
    __finally
    {
        FltReleaseResource(Globals.Lock);
    }

    return status;
}

static
_Check_return_
NTSTATUS
LcSetReportRateHandler (
    _In_reads_bytes_opt_(InputBufferSize)                                  PVOID  InputBuffer,
    _In_                                                                   ULONG  InputBufferSize,
    _Out_writes_bytes_to_opt_(OutputBufferSize, *ReturnOutputBufferLength) PVOID  OutputBuffer,
    _In_                                                                   ULONG  OutputBufferSize,
    _Out_                                                                  PULONG ReturnOutputBufferLength
    )
/*++

Summary:

    This function handles the 'SetReportRate' command received from a user-mode client.

Arguments:

    InputBuffer              - A buffer containing input data, can be NULL if there
                               is no input data.

    InputBufferSize          - The size, in bytes, of the 'InputBuffer'.

    OutputBuffer             - A buffer provided by the application that originated the
                               communication in which to store data to be returned to this
                               application.

    OutputBufferSize         - The size, in bytes, of the 'OutputBuffer'.

    ReturnOutputBufferLength - The size, in bytes, of meaningful data returned in the 'OutputBuffer'.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS     status     = STATUS_SUCCESS;
    PREPORT_RATE reportRate = NULL;

    PAGED_CODE();

    UNREFERENCED_PARAMETER(OutputBuffer);
    UNREFERENCED_PARAMETER(OutputBufferSize);

    // Input buffer should at least contain the 'PathCount' value.
    IF_FALSE_RETURN_RESULT(InputBuffer != NULL,                    STATUS_INVALID_PARAMETER_1);
    IF_FALSE_RETURN_RESULT(InputBufferSize >= sizeof(REPORT_RATE), STATUS_INVALID_PARAMETER_2);

    *ReturnOutputBufferLength = 0;

    FltAcquireResourceExclusive(Globals.Lock);

    __try
    {
        __try
        {
            reportRate = (PREPORT_RATE)InputBuffer;
            LOG((DPFLTR_IHVDRIVER_ID, DPFLTR_TRACE_LEVEL, "[LazyCopy] Setting report rate to: %u\n", reportRate->ReportRate));

            LcSetReportRate(reportRate->ReportRate);
        }
        __except (LcDriverExceptionFilter(GetExceptionInformation(), TRUE))
        {
            status = GetExceptionCode();
        }
    }
    __finally
    {
        FltReleaseResource(Globals.Lock);
    }

    return status;
}

//------------------------------------------------------------------------
//  Additional validation functions.
//------------------------------------------------------------------------

static
_Check_return_
NTSTATUS
LcValidateBufferAlignment (
    _In_ PVOID Buffer
    )
/*++

Summary:

    This function validates the 'Buffer' alignment.
    It supports both 32 and 64-bit platforms.

Arguments:

    Buffer - Buffer, which alignment should be validated.

Return value:

    The return value is the status of the operation.

--*/
{
    NTSTATUS status = STATUS_SUCCESS;

    PAGED_CODE();

    IF_FALSE_RETURN_RESULT(Buffer != NULL, STATUS_INVALID_PARAMETER_1);

    #if defined(_WIN64)
    if (IoIs32bitProcess(NULL))
    {
        // Validate alignment for the 32bit process on a 64bit system.
        if (!IS_ALIGNED(Buffer, sizeof(ULONG)))
        {
            status = STATUS_DATATYPE_MISALIGNMENT;
        }
    }
    else
    {
    #endif // defined(_WIN64)

        if (!IS_ALIGNED(Buffer, sizeof(PVOID)))
        {
            status = STATUS_DATATYPE_MISALIGNMENT;
        }

    #if defined(_WIN64)
    }
    #endif // defined(_WIN64)

    return status;
}
