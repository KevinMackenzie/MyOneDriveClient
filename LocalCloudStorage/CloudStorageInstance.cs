using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage
{
    public class CloudStorageInstance : IDisposable
    {
        private IRemoteFileStoreInterface _remoteInterface;
        private ILocalFileStore _local;

        private FileStoreBridge _bridge;

        private PauseTokenSource _instancePts = new PauseTokenSource();


        #region Private Fields
        #region Background Loop
        private Task _syncLoopTask;
        #endregion

        private SingleTimer _pauseTimer = new SingleTimer();
        #endregion

        public CloudStorageInstance(IRemoteFileStoreInterface remoteInterface, ILocalFileStore local, CancellationToken appClosingToken)
        {
            _remoteInterface = remoteInterface;
            _local = local;
            _bridge = new FileStoreBridge(new List<string>(), new LocalFileStoreInterface(local), remoteInterface);

            //make sure we cancel when the app is closing
            appClosingToken.Register(() => _instancePts.Cancel());

            //start the background loop
            _syncLoopTask = SyncLoopMethod(_instancePts.Token);
        }

        #region Private Methods
        private async Task SyncLoopMethod(PauseToken pt)
        {
            var ct = pt.CancellationToken;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _bridge.ApplyRemoteChangesAsync(pt);
                    await _bridge.ApplyLocalChangesAsync(pt);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Uncaught exception in {nameof(CloudStorageInstance)}.{nameof(SyncLoopMethod)} of type {e.GetType()} with message {e.Message}");
                    Debug.Indent();
                    Debug.WriteLine(e.StackTrace);
                    Debug.Unindent();
                    break;
                }
            }
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// The time until the syncing resumes
        /// </summary>
        public TimeSpan TimeUntilResume => _pauseTimer.Remaining;
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
            await _bridge.ForceLocalChangesAsync(_instancePts.Token.CancellationToken);
        }
        /// <summary>
        /// Pauses the syncing for a given amount of time
        /// </summary>
        /// <param name="howLong">the duration to pause the sync loop for</param>
        public void PauseSync(TimeSpan howLong)
        {
            _instancePts.IsPaused = true;
            _pauseTimer.Start(howLong);
        }
        /// <summary>
        /// Resumes the syncing immediately
        /// </summary>
        public void ResumeSync()
        {
            _pauseTimer.Stop();
            _instancePts.IsPaused = false;
        }
        #endregion

        #region IDisposable Support
        /// <inheritdoc />
        public void Dispose()
        {
            _instancePts?.Dispose();
            _syncLoopTask?.Dispose();

            _instancePts = null;
            _syncLoopTask = null;
        }
        #endregion
    }
}
