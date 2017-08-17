using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;
using LocalCloudStorage.Model;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage.ViewModel
{
    public class CloudStorageInstanceViewModel : ViewModelBase, IDisposable
    {
        
        private readonly CloudStorageInstanceData _data;
        private readonly CloudStorageInstanceControl _control;

        public CloudStorageInstanceViewModel(CloudStorageInstanceData data, CloudStorageInstanceControl control, CancellationToken appClosingToken)
        {
            _data = data;
            _control = control;

            //create the requests viewmodels
            Requests = new RequestsViewModel(
                statusChanged => _control.OnLocalRequestStatusChanged += statusChanged,
                statusChanged => _control.OnRemoteRequestStatusChanged += statusChanged,
                progressChanged => _control.OnRemoteRequestProgressChanged += progressChanged);
        }
        
        #region Public Properties
        /// <summary>
        /// The time until the syncing resumes
        /// </summary>
        public TimeSpan TimeUntilResume => _control.TimeUntilResume;
        public bool IsPaused => TimeUntilResume < TimeSpan.MinValue;
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
                _control.RemoteDeltaFrequency = value;
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
                _control.BlackList = value;
                OnPropertyChanged();
            }
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
            _control.ForceLocalChanges();
        }
        /// <summary>
        /// Pauses the syncing for a given amount of time
        /// </summary>
        /// <param name="howLong">the duration to pause the sync loop for</param>
        public void PauseSync(TimeSpan howLong)
        {
            _control.PauseSync(howLong);
        }
        /// <summary>
        /// Resumes the syncing immediately
        /// </summary>
        public void ResumeSync()
        {
            _control.ResumeSync();
        }
        public Task ResolveLocalConflictAsync(int requestId, ConflictResolutions resolution)
        {
            return _control.ResolveLocalConflictAsync(requestId, resolution);
        }
        public Task ResolveRemoteConflictAsync(int requestId, ConflictResolutions resolution)
        {
            return _control.ResolveRemoteConflictAsync(requestId, resolution);
        }
        public void CancelLocalRequest(int requestId)
        {
            _control.CancelLocalRequest(requestId);
        }
        public void CancelRemoteRequest(int requestId)
        {
            _control.CancelRemoteRequest(requestId);
        }
        #endregion

        #region IDisposable Support
        /// <inheritdoc />
        public void Dispose()
        {
            _control?.Dispose();
        }
        #endregion
    }
}
