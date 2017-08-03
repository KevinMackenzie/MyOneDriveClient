using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;
using LocalCloudStorage.ViewModel;

namespace LocalCloudStorage
{
    public class CloudStorageInstance
    {
        private IRemoteFileStoreConnection _remote;
        private ILocalFileStore _local;

        public CloudStorageInstance(IRemoteFileStoreConnection remote, ILocalFileStore local)
        {
            _remote = remote;
            _local = local;
        }

        #region Public Properties
        /// <summary>
        /// The time until the syncing resumes
        /// </summary>
        public TimeSpan TimeUntilResume { get; }
        /// <summary>
        /// The view model to the active requests
        /// </summary>
        public RequestsViewModel ViewModel { get; }
        #endregion

        #region Settings
        /// <summary>
        /// Whether data uploaded to remote should be encrypted
        /// </summary>
        public bool Encrypted { get; set; }
        /// <summary>
        /// Whether to create file links for all blacklisted files
        /// </summary>
        public bool EnableFileLinks { get; set; }
        /// <summary>
        /// How frequently to check for remote deltas
        /// </summary>
        public TimeSpan RemoteDeltaFrequency { get; set; } = TimeSpan.FromMinutes(1);
        #endregion

        #region Public Methods
        /// <summary>
        /// Looks past the existing knowledge of the local file store and 
        /// looks for new/changed/deleted files
        /// </summary>
        /// <returns></returns>
        public async Task ForceUpdateLocalAsync()
        {
            
        }
        public void PauseSync(TimeSpan howLong)
        {
            
        }
        public void ResumeSync()
        {
            
        }
        #endregion
    }
}
