using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage;
using LocalCloudStorage.Contracts;
using LocalCloudStorage.ViewModel;

namespace LocalCloudStorage
{
    public class CloudStorageInstance
    {
        private IRemoteFileStoreInterface _remoteInterface;
        private ILocalFileStore _local;

        private FileStoreBridge _bridge;

        private CancellationTokenSource _instanceCts = new CancellationTokenSource();

        #region Background Loop
        private Task _remoteSyncLoop;
        private CancellationTokenSource _remoteSyncLoopCTS;
        #endregion

        public CloudStorageInstance(IRemoteFileStoreInterface remoteInterface, ILocalFileStore local)
        {
            _remoteInterface = remoteInterface;
            _local = local;
            _bridge = new FileStoreBridge(new List<string>(), new LocalFileStoreInterface(local), remoteInterface);

            //make sure we cancel when the app is closing
            LocalCloudStorage.Instance.AppClosingCancellationToken.Register(() => _instanceCts.Cancel());
        }

        #region Private Methods
        private async Task SyncLoopMethod(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                //TODO: how to support pausing?  some sort of a "PausableCancellationToken"?
                try
                {
                    await _bridge.ApplyLocalChangesAsync(ct);
                    await _bridge.ApplyLocalChangesAsync(ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        #endregion

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
            await _bridge.ForceLocalChangesAsync(_instanceCts.Token);
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
