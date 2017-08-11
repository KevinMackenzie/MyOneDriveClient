using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Data;
using LocalCloudStorage.Events;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage.ViewModel
{
    public class CloudStorageInstanceViewModel : ViewModelBase, IDisposable
    {
        private IRemoteFileStoreInterface _remoteInterface;
        private readonly CloudStorageInstanceData _data;
        private ILocalFileStoreInterface _localInterface;
        //private ILocalFileStore _local;

        private FileStoreBridge _bridge;

        private PauseTokenSource _instancePts = new PauseTokenSource();


        #region Private Fields
        #region Background Loop
        private Task _syncLoopTask;
        #endregion

        private SingleTimer _pauseTimer = new SingleTimer();
        #endregion

        public CloudStorageInstanceViewModel(IRemoteFileStoreInterface remoteInterface, ILocalFileStoreInterface localInterface, CloudStorageInstanceData data, CancellationToken appClosingToken)
        {
            _remoteInterface = remoteInterface;
            _data = data;
            //_local = local;
            _localInterface = localInterface;
            _bridge = new FileStoreBridge(data.BlackList, _localInterface, remoteInterface);

            //create the requests viewmodels
            Requests = new RequestsViewModel(this);
            
            //make sure we cancel when the app is closing
            appClosingToken.Register(() => _instancePts.Cancel());

            //start the background loop
            _syncLoopTask = SyncLoopMethod(_instancePts.Token);
        }

        #region EventHandlers
        private void BlackList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }
        #endregion

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
                    Debug.WriteLine($"Uncaught exception in {nameof(CloudStorageInstanceViewModel)}.{nameof(SyncLoopMethod)} of type {e.GetType()} with message {e.Message}");
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
        public RequestsViewModel Requests { get; }
        #endregion

        #region Settings
        /// <summary>
        /// The path for the local file store
        /// </summary>
        public string LocalFileStorePath => _data.LocalFileStorePath;
        /// <summary>
        /// The name of this instance
        /// </summary>
        /// <remarks>
        /// This is an identifying property of the instance data
        /// </remarks>
        public string InstanceName
        {
            get => _data.InstanceName;
            set
            {
                if (_data.InstanceName == value) return;
                _data.InstanceName = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Whether data uploaded to remote should be encrypted
        /// </summary>
        public bool Encrypted
        {
            get => _data.Encrypted;
            set
            {
                if (_data.Encrypted == value) return;
                _data.Encrypted = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The remote service type that is being used
        /// </summary>
        public string ServiceName
        {
            get => _data.ServiceName;
            set
            {
                if (_data.ServiceName == value) return;
                _data.ServiceName = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Whether to create file links for all blacklisted files
        /// </summary>
        public bool EnableFileLinks
        {
            get => _data.EnableFileLinks;
            set
            {
                if (_data.EnableFileLinks == value) return;
                _data.EnableFileLinks = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// How frequently to check for remote deltas
        /// </summary>
        public TimeSpan RemoteDeltaFrequency
        {
            get => _data.RemoteDeltaFrequency;
            set
            {
                if (_data.RemoteDeltaFrequency == value) return;
                _data.RemoteDeltaFrequency = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Files/folders that should be excluded from syncing
        /// </summary>
        public IEnumerable<string> BlackList
        {
            get => _data.BlackList;
            set
            {
                _data.BlackList = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Public Events
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
        public async Task ResolveLocalConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution)
        {
            await _bridge.ResolveLocalConflictAsync(requestId, resolution, _instancePts.Token.CancellationToken);
        }
        public async Task ResolveRemoteConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution)
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
