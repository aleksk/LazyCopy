// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LazyCopyService.cs">
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
    using System;
    using System.IO;
    using System.ServiceProcess;

    using LazyCopy.Service.Properties;
    using NLog;
    using NLog.Config;
    using NLog.Targets;

    /// <summary>
    /// This service manages the LazyCopy FileSystem MiniFilter driver.
    /// </summary>
    public class LazyCopyService : ServiceBase
    {
        #region Constructors

        /// <summary>
        /// Initializes static members of the <see cref="LazyCopyService"/> class.
        /// </summary>
        static LazyCopyService()
        {
            LazyCopyService.ConfigureLogger();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyCopyService"/> class.
        /// </summary>
        public LazyCopyService()
        {
            this.ServiceName = "LazyCopySvc";
        }

        #endregion // Constructors

        #region Public methods

        /// <summary>
        /// Entry point for this service.
        /// </summary>
        public static void Main()
        {
            ServiceBase.Run(new LazyCopyService());
        }

        #endregion // Public methods

        #region Protected methods

        /// <summary>
        /// Service startup handler.
        /// </summary>
        /// <param name="args">Service startup parameters.</param>
        protected override void OnStart(string[] args)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif

            try
            {
                // Load driver and update its settings.
                LazyCopyDriver.Instance.ConfigureDriver();
            }
            catch (Exception e)
            {
                LogManager.GetCurrentClassLogger().Fatal(e, "Unable to start service: {0}");
                throw;
            }
        }

        #endregion // Protected methods

        #region Private methods

        /// <summary>
        /// Configures the NLog module.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Targets will be disposed by the logger.")]
        private static void ConfigureLogger()
        {
            LoggingConfiguration config = new LoggingConfiguration();

            EventLogTarget eventLogTarget = new EventLogTarget
            {
                Name   = "eventLog",
                Layout = @"${longdate} [${level:uppercase=true}] ${message}",
                Source = "LazyCopySvc",
                Log    = "Application"
            };

            FileTarget traceFileTarget = new FileTarget
            {
                Name             = "traceFileLog",
                Layout           = @"${longdate} [${level:uppercase=true}] ${logger}: ${message}",
                FileName         = Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogPath), "service.trace"),
                ArchiveFileName  = Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogPath), "service.{#####}.trace"),
                ArchiveAboveSize = 5 * 1024 * 1024,
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles  = 5
            };

            FileTarget errorFileTarget = new FileTarget
            {
                Name             = "errorFileLog",
                Layout           = @"${longdate} [${level:uppercase=true}] ${logger}: ${message}",
                FileName         = Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogPath), "service.err"),
                ArchiveFileName  = Path.Combine(Environment.ExpandEnvironmentVariables(Settings.Default.LogPath), "service.{#####}.err"),
                ArchiveAboveSize = 1 * 1024 * 1024,
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles  = 5
            };

            config.AddTarget(eventLogTarget.Name,  eventLogTarget);
            config.AddTarget(traceFileTarget.Name, traceFileTarget);
            config.AddTarget(errorFileTarget.Name, errorFileTarget);

            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info,  eventLogTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, traceFileTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, errorFileTarget));

            LogManager.Configuration = config;
        }

        #endregion // Private methods
    }
}
