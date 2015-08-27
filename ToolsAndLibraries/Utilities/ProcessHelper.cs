// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProcessHelper.cs">
//   The MIT License (MIT)
//   Copyright (c) 2015 Aleksey Kabanov
// </copyright>
// <summary>
//   Permission is hereby granted, free of charge, to any person obtaining a copy
//   of this software and associated documentation files (the "Software"), to deal
//   in the Software without restriction, including without limitation the rights
//   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//   copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in
//   all copies or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//   THE SOFTWARE.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LazyCopy.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;

    using LazyCopy.Utilities.Native;

    /// <summary>
    /// Contains helper methods to work with the system processes.
    /// </summary>
    public static class ProcessHelper
    {
        #region Fields

        /// <summary>
        /// Default buffer size for token information retrieval.
        /// </summary>
        private const int DefaultBufferSize = 256;

        #endregion // Fields

        #region Public methods

        /// <summary>
        /// Starts a new <paramref name="fileName"/> application with no console window created, and
        /// waits for its to finish, optionally redirecting its output to the <paramref name="outputCallback"/>
        /// specified.
        /// </summary>
        /// <param name="fileName">Path to the application executable.</param>
        /// <param name="arguments">Application arguments. Might be <see langword="null"/>.</param>
        /// <param name="outputCallback">Callback that receives process output. Might be <see langword="null"/>.</param>
        /// <returns>Process exit code.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
        public static int RunProcess(string fileName, string arguments, Action<string> outputCallback)
        {
            Func<string, string[]> outputFunc = outputCallback == null
                ? (Func<string, string[]>)null
                : s =>
                  {
                      outputCallback(s);
                      return null;
                  };

            return ProcessHelper.RunProcess(fileName, arguments, outputFunc);
        }

        /// <summary>
        /// Starts a new <paramref name="fileName"/> application with no console window created, and
        /// waits for its to finish, optionally redirecting its output to the <paramref name="outputFunc"/>
        /// specified.
        /// </summary>
        /// <param name="fileName">Path to the application executable.</param>
        /// <param name="arguments">Application arguments. Might be <see langword="null"/>.</param>
        /// <param name="outputFunc">
        /// Function that receives process output, and its output is redirected to the process input stream.
        /// Might be <see langword="null"/>.
        /// </param>
        /// <returns>Process exit code.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
        public static int RunProcess(string fileName, string arguments, Func<string, string[]> outputFunc)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName               = fileName,
                    Arguments              = arguments,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput  = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                if (outputFunc != null)
                {
                    Action<string> callback = s =>
                    {
                        string[] inputLines = outputFunc(s);
                        if (inputLines != null)
                        {
                            foreach (string line in inputLines)
                            {
                                process.StandardInput.WriteLine(line);
                            }
                        }
                    };

                    process.OutputDataReceived += (sender, args) => callback(args.Data);
                    process.ErrorDataReceived  += (sender, args) => callback(args.Data);
                }

                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                return process.ExitCode;
            }
        }

        /// <summary>
        /// Gets the process name based on its <paramref name="processId"/>.
        /// </summary>
        /// <param name="processId">Process Id.</param>
        /// <returns>Process name or <see cref="string.Empty"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="processId"/> is lesser than <c>-1</c>.</exception>
        public static string GetProcessName(int processId)
        {
            if (processId < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process Id should be greater than zero or equal to -1.");
            }

            return processId == -1 ? string.Empty : Process.GetProcessById(processId).ProcessName;
        }

        /// <summary>
        /// Gets the list of processes for the <paramref name="domainUser"/> given.
        /// </summary>
        /// <param name="domainUser">Username in the <c>DOMAIN\\username</c> format.</param>
        /// <returns>Collection of processes found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="domainUser"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentException"><paramref name="domainUser"/> is not in the <c>DOMAIN\username</c> format.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "General catch block is desired here.")]
        public static IEnumerable<Process> FindUserProcesses(string domainUser)
        {
            if (string.IsNullOrEmpty(domainUser))
            {
                throw new ArgumentNullException(nameof(domainUser));
            }

            domainUser = domainUser.Trim();

            if (!Regex.IsMatch(domainUser, @"^[^\\]+\\[^\\]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                throw new ArgumentException("Domain user should be in the DOMAIN\\username format.", nameof(domainUser));
            }

            foreach (Process process in Process.GetProcesses())
            {
                string userForProcess;

                try
                {
                    userForProcess = ProcessHelper.GetUserNameForProcess(process);
                }
                catch
                {
                    continue;
                }

                if (string.Equals(userForProcess, domainUser, StringComparison.OrdinalIgnoreCase))
                {
                    yield return process;
                }
            }
        }

        /// <summary>
        /// Gets the name of the user for the <paramref name="process"/> given.
        /// </summary>
        /// <param name="process">Process to get username for.</param>
        /// <returns>Name of the user for the <paramref name="process"/> in the <c>DOMAIN\username</c> format.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Process token cannot be obtained.
        ///     <para>-or-</para>
        /// Process token information cannot be retrieved.
        ///     <para>-or-</para>
        /// Account information cannot be found.
        /// </exception>
        public static string GetUserNameForProcess(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            SafeTokenHandle tokenHandle;
            if (!NativeMethods.OpenProcessToken(process.Handle, TokenAccessRights.TokenQuery, out tokenHandle))
            {
                int hr = Marshal.GetHRForLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to open process token: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
            }

            using (tokenHandle)
            using (ResizableBuffer buffer = new ResizableBuffer(ProcessHelper.DefaultBufferSize))
            {
                int actualSize;
                if (!NativeMethods.GetTokenInformation(tokenHandle, TokenInformationClass.TokenUser, buffer.DangerousGetPointer(), buffer.ByteLength, out actualSize))
                {
                    int hr = Marshal.GetHRForLastWin32Error();
                    if (hr != NativeMethods.ErrorInsufficientBuffer)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get token information: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
                    }

                    // Resize and retry.
                    buffer.Resize(actualSize);

                    if (!NativeMethods.GetTokenInformation(tokenHandle, TokenInformationClass.TokenUser, buffer.DangerousGetPointer(), buffer.ByteLength, out actualSize))
                    {
                        hr = Marshal.GetHRForLastWin32Error();
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get token information: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
                    }
                }

                TokenUser tokenUser = (TokenUser)Marshal.PtrToStructure(buffer.DangerousGetPointer(), typeof(TokenUser));

                return ProcessHelper.GetUserNameForSid(tokenUser.User.Sid);
            }
        }

        #endregion // Public methods

        #region Private methods

        /// <summary>
        /// Gets the username for the <paramref name="sid"/> given.
        /// </summary>
        /// <param name="sid">Account SID.</param>
        /// <returns>Name of the user for the <paramref name="sid"/> in the <c>DOMAIN\username</c> format.</returns>
        /// <exception cref="InvalidOperationException">Account information cannot be found.</exception>
        private static string GetUserNameForSid(IntPtr sid)
        {
            int accountStringLength = 0;
            int domainStringLength  = 0;
            SidNameUse sidNameUse;

            // Get the actual account and domain names first.
            if (NativeMethods.LookupAccountSid(null, sid, null, ref accountStringLength, null, ref domainStringLength, out sidNameUse))
            {
                int hr = Marshal.GetHRForLastWin32Error();
                if (hr == NativeMethods.ErrorNoMoreItems)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to lookup account SID. No more items (0x{0:X8}).", hr), Marshal.GetExceptionForHR(hr));
                }
            }

            // Allocate memory and get the actual account and domain values.
            StringBuilder account = new StringBuilder(accountStringLength);
            StringBuilder domain  = new StringBuilder(domainStringLength);

            if (!NativeMethods.LookupAccountSid(null, sid, account, ref accountStringLength, domain, ref domainStringLength, out sidNameUse))
            {
                int hr = Marshal.GetHRForLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to lookup account SID: 0x{0:X8}", hr), Marshal.GetExceptionForHR(hr));
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", domain, account);
        }

        #endregion Private methods
    }
}
