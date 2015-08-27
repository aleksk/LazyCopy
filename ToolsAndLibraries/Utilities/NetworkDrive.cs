// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NetworkDrive.cs">
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
    using System.Globalization;

    /// <summary>
    /// Represents a <b>non-persistent</b> network drive that can be mapped to a remote share.
    /// </summary>
    /// <remarks>
    /// This class is basically a wrapper for the <see cref="DriveHelper"/> class methods.
    /// </remarks>
    public class NetworkDrive : IDisposable
    {
        #region Fields

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Whether the current instance is mapped.
        /// </summary>
        private volatile bool mapped;

        /// <summary>
        /// Whether the current instance is already disposed.
        /// </summary>
        private volatile bool disposed;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="NetworkDrive"/> class.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Failures in the abandoned drive deletion are not critical for the current class.")]
        static NetworkDrive()
        {
            try
            {
                DriveHelper.DeleteAbandonedDrives();
            }
            catch
            {
                // Ignore all exceptions, it is not critical for the current method.
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkDrive"/> class.
        /// </summary>
        public NetworkDrive()
            : this(DriveHelper.GetNextAvailableDriveLetter())
        {
            // Do nothing.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkDrive"/> class.
        /// </summary>
        /// <param name="letter">Drive letter to use.</param>
        /// <exception cref="ArgumentNullException"><paramref name="letter"/> is <see langword="null"/> or empty.</exception>
        public NetworkDrive(string letter)
        {
            if (string.IsNullOrEmpty(letter))
            {
                throw new ArgumentNullException(nameof(letter));
            }

            // Trim trailing separator characters.
            this.Letter = letter.TrimEnd('\\');
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="NetworkDrive"/> class.
        /// </summary>
        ~NetworkDrive()
        {
            this.Dispose(false);
        }

        #endregion // Constructors

        #region Properties

        /// <summary>
        /// Gets the current drive letter.
        /// </summary>
        public string Letter { get; }

        /// <summary>
        /// Gets the remote share this drive is currently mapped to.
        /// </summary>
        public string RemoteShare { get; private set; }

        #endregion // Properties

        #region Public methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Maps the specified remote share.
        /// </summary>
        /// <param name="remoteShare">The remote share.</param>
        /// <exception cref="ObjectDisposedException">Drive is already disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Drive is already mapped to another share.<br/>
        /// This exception is not thrown the <paramref name="remoteShare"/> is equal to the current <see cref="RemoteShare"/> value.
        /// </exception>
        /// <exception cref="InvalidOperationException">If it was impossible to map the drive.</exception>
        public void Map(string remoteShare)
        {
            this.Map(remoteShare, null, null);
        }

        /// <summary>
        /// Maps the specified remote share.
        /// </summary>
        /// <param name="remoteShare">The remote share.</param>
        /// <param name="userName">Username. May be <see langword="null"/>.</param>
        /// <param name="password">Password. May be <see langword="null"/>.</param>
        /// <exception cref="ObjectDisposedException">Drive is already disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Drive is already mapped to another share.<br/>
        /// This exception is not thrown the <paramref name="remoteShare"/> is equal to the current <see cref="RemoteShare"/> value.
        /// </exception>
        /// <exception cref="InvalidOperationException">If it was impossible to map the drive.</exception>
        public void Map(string remoteShare, string userName, string password)
        {
            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException("Drive is already disposed.");
                }

                if (this.mapped)
                {
                    if (string.Equals(this.RemoteShare, remoteShare, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Drive is already mapped to: {0} -> {1}", this.Letter, this.RemoteShare));
                }

                // Map a network drive.
                DriveHelper.AddNetworkDrive(this.Letter, remoteShare, userName, password);

                this.RemoteShare = remoteShare;
                this.mapped      = true;
            }
        }

        /// <summary>
        /// Deletes the current drive mapping.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Drive is already disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Drive is not mapped.
        ///     <para>-or-</para>
        /// Drive mapping in the Registry could not be removed.
        /// </exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Un", Justification = "The spelling is correct.")]
        public void UnMap()
        {
            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException("Drive is already disposed.");
                }

                if (!this.mapped)
                {
                    throw new InvalidOperationException("Drive is not mapped.");
                }

                DriveHelper.DeleteNetworkDrive(this.Letter);

                this.RemoteShare = null;
                this.mapped      = false;
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "NetworkDrive: {0} -> {1}", this.Letter, this.RemoteShare ?? "null");
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (this.syncRoot)
            {
                if (this.mapped)
                {
                    this.UnMap();
                }

                this.disposed = true;
            }
        }

        #endregion // Protected methods
    }
}
