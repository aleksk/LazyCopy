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

    Macro.h

Abstract:

    Contains custom macroses used by the minifilter.

Environment:

    Kernel/User modes.

--*/

#pragma once
#ifndef __LAZY_COPY_MACRO_H__
#define __LAZY_COPY_MACRO_H__

//------------------------------------------------------------------------
//  Debug macroses.
//------------------------------------------------------------------------

#if DBG
    #define LOG(Data) DbgPrintEx Data;
#else  // DBG
    #define LOG(Data) { NOTHING; }
#endif // DBG

//------------------------------------------------------------------------
//  Parameter validation macroses.
//------------------------------------------------------------------------

//
// Calls the 'FLT_ASSERTMSG' and returns from the current function, if the '_exp' expression is FALSE.
//
#define IF_FALSE_RETURN(_exp)         \
    if (!(_exp))                      \
    {                                 \
        FLT_ASSERTMSG(#_exp, FALSE);  \
        return;                       \
    }

//
// Calls the 'FLT_ASSERTMSG' and returns from the current function, if the '_exp' expression is TRUE.
//
#define IF_TRUE_RETURN(_exp)          \
    if ((_exp))                       \
    {                                 \
        FLT_ASSERTMSG(#_exp, FALSE);  \
        return;                       \
    }

//
// Calls the 'FLT_ASSERTMSG' and returns the 'result', if the '_exp' expression is FALSE.
//
#define IF_FALSE_RETURN_RESULT(_exp, result)  \
    if (!(_exp))                              \
    {                                         \
        FLT_ASSERTMSG(#_exp, FALSE);          \
        return result;                        \
    }

//
// Calls the 'FLT_ASSERTMSG' and returns the 'result', if the '_exp' expression is TRUE.
//
#define IF_TRUE_RETURN_RESULT(_exp, result)  \
    if ((_exp))                              \
    {                                        \
        FLT_ASSERTMSG(#_exp, FALSE);         \
        return result;                       \
    }

//
// Leaves the current '__try' block, if the '_exp' expression returns TRUE.
// Requires the 'NTSTATUS status' local variable to be defined.
//
#define NT_IF_TRUE_LEAVE(_exp, result)       \
    if ((_exp))                              \
    {                                        \
        status = result;                     \
        __leave;                             \
    }

//
// Leaves the current '__try' block, if the '_exp' expression returns FALSE.
// Requires the 'NTSTATUS status' local variable to be defined.
//
#define NT_IF_FALSE_LEAVE(_exp, result)      \
    if (!(_exp))                             \
    {                                        \
        status = result;                     \
        __leave;                             \
    }

//------------------------------------------------------------------------
//  Return value validation macroses.
//------------------------------------------------------------------------

//
// Leaves the current '__try' block, if the '_exp' expression returns a failing NTSTATUS.
// Requires the 'NTSTATUS status' local variable to be defined.
//
#define NT_IF_FAIL_LEAVE(_exp)         \
    status = (_exp);                   \
    if (!NT_SUCCESS(status))           \
    {                                  \
        __leave;                       \
    }

//
// Returns from the current function, if the '_exp' expression returns a failing NTSTATUS.
// Requires the 'NTSTATUS status' local variable to be defined.
//
#define NT_IF_FAIL_RETURN(_exp)       \
    status = (_exp);                  \
    if (!NT_SUCCESS(status))          \
    {                                 \
        return status;                \
    }

//
// Leaves the current '__try' block, if the '_exp' expression returns a failing HRESULT.
// Requires the 'HRESULT hr' local variable to be defined.
//
#define HR_IF_FAIL_LEAVE(_exp)        \
    hr = (_exp);                      \
    if (FAILED(hr))                   \
    {                                 \
        __leave;                      \
    }

//
// Returns from the current function, if the '_exp' expression returns a failing HRESULT.
// Requires the 'HRESULT hr' local variable to be defined.
//
#define HR_IF_FAIL_RETURN(_exp)       \
    hr = (_exp);                      \
    if (FAILED(hr))                   \
    {                                 \
        return hr;                    \
    }

//------------------------------------------------------------------------
//  String manipulation macroses.
//------------------------------------------------------------------------

//
// Creates a new UNICODE_STRING.
//
#define CONSTANT_STRING(x) { sizeof((x)) - sizeof((x)[0]), sizeof((x)), (x) }

#endif // __LAZY_COPY_MACRO_H__
