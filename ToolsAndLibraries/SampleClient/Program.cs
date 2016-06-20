// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs">
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

namespace SampleClient
{
    using System;

    using LazyCopy.DriverClient;
    using LongPath;

    class Program
    {
        /// <summary>
        /// This sample application accepts two input parameters:
        /// * source file - file with actual data which content should be copied to the target file, when it's opened.
        /// * target file - empty file to be created. When this file is opened, its contents are downloaded from the source file.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Length != 2 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]))
            {
                Console.Out.WriteLine("sampleclient.exe \"<source_file_with_data>\" \"<local_file>\"");
                return;
            }

            string sourceFileName = args[0].Trim();
            string targetFileName = args[1].Trim();
            var targetFile        = new LongPathFileInfo(targetFileName);

            if (sourceFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourceFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                LazyCopyFileHelper.CreateLazyCopyFile(targetFile.FullName, new LazyCopyFileData { RemotePath = sourceFileName, FileSize = 404, UseCustomHandler = true });
            }
            else
            {
                var sourceFile = new LongPathFileInfo(sourceFileName);
                if (!sourceFile.Exists)
                {
                    Console.Out.WriteLine("Source file doesn't exist: " + sourceFile);
                    return;
                }

                LazyCopyFileHelper.CreateLazyCopyFile(targetFile.FullName, new LazyCopyFileData { RemotePath = sourceFile.FullName, FileSize = sourceFile.Length });
            }
        }
    }
}
