// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProjectInstaller.cs">
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

namespace LazyCopy.Service
{
    using System.ComponentModel;
    using System.Configuration.Install;
    using System.ServiceProcess;

    /// <summary>
    /// This class installs the current application as a Windows service.
    /// </summary>
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        /// <summary>
        /// Service process installer.
        /// </summary>
        private ServiceProcessInstaller serviceProcessInstaller;

        /// <summary>
        /// Service installer.
        /// </summary>
        private ServiceInstaller serviceInstaller;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectInstaller"/> class.
        /// </summary>
        public ProjectInstaller()
        {
            this.serviceProcessInstaller = new ServiceProcessInstaller();
            this.serviceInstaller = new ServiceInstaller();

            this.serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            this.serviceInstaller.Description = "Performs additional tasks for the LazyCopy driver.";
            this.serviceInstaller.DisplayName = "LazyCopy";
            this.serviceInstaller.ServiceName = "LazyCopySvc";
            this.serviceInstaller.StartType   = ServiceStartMode.Automatic;

            this.Installers.AddRange(new Installer[] { this.serviceProcessInstaller, this.serviceInstaller });
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.ComponentModel.Component"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.serviceInstaller != null)
                {
                    this.serviceInstaller.Dispose();
                    this.serviceInstaller = null;
                }

                if (this.serviceProcessInstaller != null)
                {
                    this.serviceProcessInstaller.Dispose();
                    this.serviceProcessInstaller = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
