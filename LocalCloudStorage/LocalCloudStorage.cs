using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;

namespace LocalCloudStorage
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class LocalCloudStorage
    {
        #region Singleton Implementation
        private static LocalCloudStorage _instance;
        public static LocalCloudStorage Instance => _instance ?? (_instance = new LocalCloudStorage());
        #endregion

        #region Application-Wide Settings
        /// <summary>
        /// The maximum upload rate.  If 0, then no limit
        /// </summary>
        public int MaxUploadRate { get; set; }
        /// <summary>
        /// The maximum download rate.  If 0, then no limit
        /// </summary>
        public int MaxDownloadRate { get; set; }
        /// <summary>
        /// Whether to start the application on startup
        /// </summary>
        public bool StartOnBoot { get; set; }
        #endregion

        #region Internal variables
        public CancellationToken AppClosingCancellationToken { get; }
        #endregion

        //TODO: implement dependency-injection for cloud storage services
        [ImportMany()]
        private IEnumerable<Lazy<IRemoteFileStoreConnectionFactory, RemoteFileStoreConnectionFactoryMetadataAttribute>> RemoteConnectionFactories { get; set; }
    }
}
