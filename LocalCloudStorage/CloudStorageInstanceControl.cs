using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage
{
    public class CloudStorageInstanceControl : IDisposable
    {
        private IRemoteFileStoreInterface _remoteInterface;
        private ILocalFileStoreInterface _localInterface;

        private FileStoreBridge _bridge;

        private PauseTokenSource _instancePts = new PauseTokenSource();
        private Task _syncLoopTask;
        private SingleTimer _pauseTimer = new SingleTimer();

        private Atomic<TimeSpan> _remoteDeltaFrequency;

        public CloudStorageInstanceControl(IRemoteFileStoreInterface remoteInterface,
            ILocalFileStoreInterface localInterface, IEnumerable<string> blackList,
            TimeSpan remoteDeltaFrequency, CancellationToken appClosingToken)
        {
            _remoteInterface = remoteInterface;
            _localInterface = localInterface;
            _remoteDeltaFrequency = new Atomic<TimeSpan>(remoteDeltaFrequency);
            _bridge = new FileStoreBridge(blackList, _localInterface, remoteInterface);


            //make sure we cancel when the app is closing
            appClosingToken.Register(() => _instancePts.Cancel());

            //start the background loop
            _syncLoopTask = SyncLoopMethod(_instancePts.Token);
        }


        #region Private Methods
        private async Task SyncLoopMethod(PauseToken pt)
        {
            //this seems like a fine place to do this... (it only gets called once)
            await _bridge.LoadMetadataAsync(_instancePts.Token.CancellationToken);

            var ct = pt.CancellationToken;
            var any = false;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    any = await _bridge.ApplyRemoteChangesAsync(pt);
                    any |= await _bridge.ApplyLocalChangesAsync(pt);
                    if (any)
                    {
                        await _bridge.SaveMetadataAsync(pt.CancellationToken);
                    }
                    await Utils.Delay(RemoteDeltaFrequency, TimeSpan.FromSeconds(1));
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Uncaught exception in \"{nameof(CloudStorageInstanceControl)}.{nameof(SyncLoopMethod)}\" of type \"{e.GetType()}\" with message \"{e.Message}\"");
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
        public bool IsPaused => _pauseTimer.IsStarted;
        /// <summary>
        /// How frequently to check for remote deltas
        /// </summary>
        public TimeSpan RemoteDeltaFrequency
        {
            get => _remoteDeltaFrequency.Value;
            set => _remoteDeltaFrequency.Value = value;
        }
        /// <summary>
        /// Files/folders that should be excluded from syncing
        /// </summary>
        public IEnumerable<string> BlackList
        {
            get => _bridge.BlackList;
            set => _bridge.BlackList = value;
        }
        #endregion

        #region Event Subscriptions
        /// <summary>
        /// When the status of a local request changes
        /// </summary>
        public event EventDelegates.RequestStatusChangedHandler OnLocalRequestStatusChanged
        {
            add => _localInterface.OnRequestStatusChanged += value;
            remove => _localInterface.OnRequestStatusChanged -= value;
        }
        /// <summary>
        /// When the status of a remote request changes
        /// </summary>
        public event EventDelegates.RequestStatusChangedHandler OnRemoteRequestStatusChanged
        {
            add => _remoteInterface.OnRequestStatusChanged += value;
            remove => _remoteInterface.OnRequestStatusChanged -= value;
        }
        /// <summary>
        /// When the progress of a remote request changes
        /// </summary>
        public event EventDelegates.RemoteRequestProgressChangedHandler OnRemoteRequestProgressChanged
        {
            add => _remoteInterface.OnRequestProgressChanged += value;
            remove => _remoteInterface.OnRequestProgressChanged -= value;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Looks past the existing knowledge of the local file store and 
        /// looks for new/changed/deleted files
        /// </summary>
        /// <returns></returns>
        public void ForceLocalChanges()
        {
            _bridge.ForceLocalChanges();
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
        public async Task ResolveLocalConflictAsync(int requestId, ConflictResolutions resolution)
        {
            await _bridge.ResolveLocalConflictAsync(requestId, resolution, _instancePts.Token.CancellationToken);
        }
        public async Task ResolveRemoteConflictAsync(int requestId, ConflictResolutions resolution)
        {
            await _bridge.ResolveRemoteConflictAsync(requestId, resolution, _instancePts.Token.CancellationToken);
        }
        public void CancelLocalRequest(int requestId)
        {
            _localInterface.CancelRequest(requestId);
        }
        public void CancelRemoteRequest(int requestId)
        {
            _remoteInterface.CancelRequest(requestId);
        }

        public Task<ICollection<StaticItemHandle>> GetLocalPathListAsync(CancellationToken ct)
        {
            return _bridge.GetLocalPathListAsync(ct);
        }
        public Task<ICollection<StaticItemHandle>> GetRemotePathListAsync(CancellationToken ct)
        {
            return _bridge.GetRemotePathListAsync(ct);
        }
        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            _instancePts?.Dispose();

            _instancePts = null;
            _syncLoopTask = null;
        }
    }
}
