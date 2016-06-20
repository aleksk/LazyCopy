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

    CommunicationData.h

Abstract:

    Contains structur definitions used in the driver-to-client communication.

Environment:

    Kernel mode.

--*/

#pragma once
#ifndef __LAZY_COPY_COMMUNICATION_DATA_H__
#define __LAZY_COPY_COMMUNICATION_DATA_H__

//------------------------------------------------------------------------
//  Includes.
//------------------------------------------------------------------------

#include "Globals.h"

//------------------------------------------------------------------------
//  Enums.
//------------------------------------------------------------------------

//
// Type of the command sent by the user-mode client to this driver for execution.
//
typedef enum _DRIVER_COMMAND_TYPE
{
    // Driver environment commands.
    GetDriverVersion       = 1,

    // Driver configuration commands.
    ReadRegistryParameters = 100,
    SetOperationMode       = 101,
    SetWatchPaths          = 102,
    SetReportRate          = 103
} DRIVER_COMMAND_TYPE, *PDRIVER_COMMAND_TYPE;

//
// Notification type sent by this driver to the user-mode client(s).
//
typedef enum _DRIVER_NOTIFICATION_TYPE
{
    // Asks the user-mode client to open the file given.
    OpenFileInUserMode  = 1,

    // Tells the user-mode client that the handle is not needed anymore and can be closed.
    CloseFileHandle     = 2,

    // Asks the user-mode client to fetch the file given for us.
    FetchFileInUserMode = 3
} DRIVER_NOTIFICATION_TYPE, *PDRIVER_NOTIFICATION_TYPE;

//------------------------------------------------------------------------
//  Structures.
//------------------------------------------------------------------------

#pragma warning(push)
#pragma warning(disable:4200)  // Disable warnings for structures with zero length arrays.

//
// Commands are sent from the user-mode client(s) to the minifilter driver.
//
typedef struct _DRIVER_COMMAND
{
    // Command type.
    DRIVER_COMMAND_TYPE Type;

    // Size of the 'Data' buffer.
    ULONG               DataLength;

    // Buffer containing the actual command data.
    PVOID               Data;
} DRIVER_COMMAND, *PDRIVER_COMMAND;

//
// Notifications are sent from the minifilter driver to the user-mode client(s).
//
typedef struct _DRIVER_NOTIFICATION
{
    // Notification type.
    DRIVER_NOTIFICATION_TYPE Type;

    // Size of the 'Data' buffer.
    ULONG                    DataLength;

    // Buffer containing the actual notification data.
    PVOID                    Data;
} DRIVER_NOTIFICATION, *PDRIVER_NOTIFICATION;

//------------------------------------------------------------------------
//  'GetDriverVersion' command.
//------------------------------------------------------------------------

//
// Contains version data to be sent to the user-mode client(s).
//
typedef struct _DRIVER_VERSION
{
    USHORT Major;
    USHORT Minor;
} DRIVER_VERSION, *PDRIVER_VERSION;

//------------------------------------------------------------------------
//  'SetOperationMode' command.
//------------------------------------------------------------------------

//
// Contains new value for the driver's operation mode.
//
typedef struct _OPERATION_MODE
{
    ULONG Mode;
} OPERATION_MODE, *POPERATION_MODE;

//------------------------------------------------------------------------
//  'SetWatchPaths' command.
//------------------------------------------------------------------------

//
// Contains list of paths to be watched for file access operations.
//
typedef struct _WATCH_PATHS
{
    // Probability of sending the file access notification for the 'Path'.
    ULONG PathCount;

    // Buffer containing the list of paths.
    WCHAR Data[];
} WATCH_PATHS, *PWATCH_PATHS;

//------------------------------------------------------------------------
//  'SetReportRate' command.
//------------------------------------------------------------------------

//
// Contains new value for the driver's file access report rate.
//
typedef struct _REPORT_RATE
{
    // Probability of sending the file access notification for the 'Path'.
    ULONG ReportRate;
} REPORT_RATE, *PREPORT_RATE;

//------------------------------------------------------------------------
//  'OpenFileInUserMode' notification.
//------------------------------------------------------------------------

//
// Contains notification data to be sent to the user-mode client,
// when the driver needs a file to be opened by it.
//
typedef struct _FILE_OPEN_NOTIFICATION_DATA
{
    // Paths to the source and target files.
    // Strings are divided by the null-terminator.
    WCHAR Data[];
} FILE_OPEN_NOTIFICATION_DATA, *PFILE_OPEN_NOTIFICATION_DATA;

//
// Reply received from the user-mode client for the 'OpenFileInUserMode' notification.
//
typedef struct _FILE_OPEN_NOTIFICATION_REPLY
{
    // Handle to the opened target file.
    HANDLE FileHandle;
} FILE_OPEN_NOTIFICATION_REPLY, *PFILE_OPEN_NOTIFICATION_REPLY;

//------------------------------------------------------------------------
//  'CloseFileHandle' notification.
//------------------------------------------------------------------------

//
// Contains notification data to be sent to the user-mode client,
// when the driver does not need the file handle previously
// opened by the client.
//
typedef struct _FILE_CLOSE_NOTIFICATION_DATA
{
    // Handle to be closed.
    HANDLE FileHandle;
} FILE_CLOSE_NOTIFICATION_DATA, *PFILE_CLOSE_NOTIFICATION_DATA;

//------------------------------------------------------------------------
//  'FetchFileInUserMode' notification.
//------------------------------------------------------------------------

//
// Contains notification data to be sent to the user-mode client,
// when the driver needs a file to be fetched by it.
//
typedef struct _FILE_FETCH_NOTIFICATION_DATA
{
    // Paths to the source and target files.
    // Strings are divided by the null-terminator.
    WCHAR Data[];
} FILE_FETCH_NOTIFICATION_DATA, *PFILE_FETCH_NOTIFICATION_DATA;

//
// Reply received from the user-mode client for the 'FetchFileInUserMode' notification.
//
typedef struct _FILE_FETCH_NOTIFICATION_REPLY
{
    // The amount of bytes copied from the source file.
    LONGLONG BytesCopied;
} FILE_FETCH_NOTIFICATION_REPLY, *PFILE_FETCH_NOTIFICATION_REPLY;

#pragma warning(pop)
#endif // __LAZY_COPY_COMMUNICATION_DATA_H__
