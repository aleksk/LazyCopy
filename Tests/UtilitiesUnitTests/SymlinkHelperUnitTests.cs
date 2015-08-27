// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SymlinkHelperUnitTests.cs">
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
    using System.IO;

    using LongPath;
    using NUnit.Framework;

    /// <summary>
    /// Contains unit tests for the <see cref="SymlinkHelper"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Symlink", Justification = "Symlink spelling is desired here.")]
    [TestFixture]
    public class SymlinkHelperUnitTests
    {
        /// <summary>
        /// Source file content.
        /// </summary>
        private const string SourceFileContent = "sample content";

        /// <summary>
        /// Temporary directory.
        /// </summary>
        private string tempPath;

        /// <summary>
        /// Sample file.
        /// </summary>
        private FileInfo sourceFile;

        /// <summary>
        /// Creates temporary directory.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            this.tempPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "SymlinkHelperUnitTests");
            Directory.CreateDirectory(this.tempPath);

            this.sourceFile = new FileInfo(Path.Combine(this.tempPath, "source.txt"));
            File.WriteAllText(this.sourceFile.FullName, SymlinkHelperUnitTests.SourceFileContent);
        }

        /// <summary>
        /// Removes temporary directory.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            Directory.Delete(this.tempPath, true);
        }

        /// <summary>
        /// Validates that the <see cref="SymlinkHelper.CreateFileLink"/> throws correct exceptions,
        /// if it's invoked with invalid parameters.
        /// </summary>
        [Test]
        public void ValidateCreateFileLinkParameters()
        {
            Console.Out.WriteLine(this.tempPath);

            string fileTxt = Path.Combine(this.tempPath, "File.txt");

            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateFileLink(null, fileTxt));
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateFileLink(string.Empty, fileTxt));
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateFileLink(fileTxt, string.Empty));

            Assert.Throws<ArgumentException>(() => SymlinkHelper.CreateFileLink(fileTxt, fileTxt));
        }

        /// <summary>
        /// Validates that the <see cref="SymlinkHelper.CreateDirectoryLink"/> throws correct exceptions,
        /// if it's invoked with invalid parameters.
        /// </summary>
        [Test]
        public void ValidateCreateDirectoryLinkParameters()
        {
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateDirectoryLink(null, this.tempPath));
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateDirectoryLink(string.Empty, this.tempPath));
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.CreateDirectoryLink(this.tempPath, string.Empty));

            Assert.Throws<ArgumentException>(() => SymlinkHelper.CreateDirectoryLink(this.tempPath, this.tempPath));
        }

        /// <summary>
        /// Validates that the <see cref="SymlinkHelper.GetLinkTarget"/> throws correct exceptions,
        /// if it's invoked with invalid parameters.
        /// </summary>
        [Test]
        public void ValidateGetLinkTargetParameters()
        {
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.GetLinkTarget(null));
            Assert.Throws<ArgumentNullException>(() => SymlinkHelper.GetLinkTarget(string.Empty));

            Assert.Throws<InvalidOperationException>(() => SymlinkHelper.GetLinkTarget(@"C:\Does_not_exist\file.txt"));
            Assert.Throws<InvalidOperationException>(() => SymlinkHelper.GetLinkTarget(this.sourceFile.FullName));
            Assert.Throws<InvalidOperationException>(() => SymlinkHelper.GetLinkTarget(this.tempPath));
        }

        /// <summary>
        /// Validates that the proper exception is thrown, if the source file/directory does not exist.
        /// </summary>
        [Test]
        public void SourceDoesNotExist()
        {
            Assert.Throws<FileNotFoundException>(() => SymlinkHelper.CreateFileLink(Path.Combine(this.tempPath, "File.txt"), @"C:\Does_Not_Exist_123\File.txt"));

            Assert.Throws<DirectoryNotFoundException>(() => SymlinkHelper.CreateDirectoryLink(Path.Combine(this.tempPath, "dir"), @"C:\Does_Not_Exist_123\"));
        }

        /// <summary>
        /// Verifies that the file symbolic link can be created and it's operational.
        /// </summary>
        [Test]
        public void FileSymbolicLinkCreated()
        {
            string targetFile = Path.Combine(this.tempPath, "target.txt");

            SymlinkHelper.CreateFileLink(targetFile, this.sourceFile.FullName);

            Assert.IsTrue(File.Exists(targetFile), "Target file was not created.");

            Assert.That(new FileInfo(targetFile).Length, Is.EqualTo(0));
            Assert.That(File.ReadAllText(targetFile), Is.EqualTo(SymlinkHelperUnitTests.SourceFileContent));
        }

        /// <summary>
        /// Verifies that the directory symbolic link can be created and it's operational.
        /// </summary>
        [Test]
        public void DirectorySymbolicLinkCreated()
        {
            string sourceDir = Path.Combine(this.tempPath, "src");
            string targetDir = Path.Combine(this.tempPath, "tgt");
            string fileInSource = Path.Combine(sourceDir, "a.txt");
            string fileInTarget = Path.Combine(targetDir, "a.txt");

            // Create sample file in the source directory.
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(fileInSource, string.Empty);

            SymlinkHelper.CreateDirectoryLink(targetDir, sourceDir);

            Assert.IsTrue(Directory.Exists(targetDir), "Directory symbolic link was not created.");
            Assert.IsTrue(File.Exists(fileInTarget), "File was not found in the directory symbolic link.");
        }

        /// <summary>
        /// Verifies that the <see cref="SymlinkHelper.GetLinkTarget"/> returns valid results for file symbolic links.
        /// </summary>
        [Test]
        public void GetFileLinkTarget()
        {
            string targetFile = Path.Combine(this.tempPath, "target.txt");
            SymlinkHelper.CreateFileLink(targetFile, this.sourceFile.FullName);

            Assert.That(LongPathCommon.RemoveLongPathPrefix(SymlinkHelper.GetLinkTarget(targetFile)), Is.EqualTo(this.sourceFile.FullName).IgnoreCase);
        }

        /// <summary>
        /// Verifies that the <see cref="SymlinkHelper.GetLinkTarget"/> returns valid results for directory symbolic links.
        /// </summary>
        [Test]
        public void GetDirectoryLinkTarget()
        {
            string sourceDir = Path.Combine(this.tempPath, "src");
            string targetDir = Path.Combine(this.tempPath, "tgt");

            Directory.CreateDirectory(sourceDir);
            SymlinkHelper.CreateDirectoryLink(targetDir, sourceDir);

            Assert.That(LongPathCommon.RemoveLongPathPrefix(SymlinkHelper.GetLinkTarget(targetDir)), Is.EqualTo(sourceDir).IgnoreCase);
        }
    }
}
