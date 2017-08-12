using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Contracts.Exceptions;
using LocalCloudStorage.Composition;
using LocalCloudStorage.Data;

namespace LocalCloudStorage.ViewModel
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class LocalCloudStorageViewModel : ViewModelBase, IDisposable
    {
        private readonly LocalCloudStorageData _data;
        private readonly ObservableCollection<CloudStorageInstanceViewModel> _cloudStorageInstances = new ObservableCollection<CloudStorageInstanceViewModel>();
        private CloudStorageInstanceViewModel _selectedInstance;
        private readonly RemoteConnectionFactoryManager _factoryManager;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public LocalCloudStorageViewModel(LocalCloudStorageData data, RemoteConnectionFactoryManager factoryManager)
        {
            //TODO: factory viewmodel instantiation
            _factoryManager = factoryManager;

            //TODO: someone should be responsible for saving the settings when the changed
            _data = data;
            _data.CloudStorageInstances.DeleteNullElements();

            CloudStorageInstances = new ReadOnlyObservableCollection<CloudStorageInstanceViewModel>(_cloudStorageInstances);

            if (data.CloudStorageInstances != null)
            {
                //create a viewmodel for each of the cloud storage instances
                foreach (var cloudStorageInstace in data.CloudStorageInstances)
                {
                    _cloudStorageInstances.Add(CreateCloudStorageInstance(cloudStorageInstace));
                }
            }

            //make sure to update the data list when the instances list changes
            ///_cloudStorageInstances.CollectionChanged += (sender, args) => UpdateCloudStorageInstancesData();
        }

        #region Private Methods
        private CloudStorageInstanceViewModel CreateCloudStorageInstance(CloudStorageInstanceData data)
        {
            //get the appropriate factory
            foreach (var factory in _factoryManager.Factories)
            {
                if (factory.ServiceName == data.ServiceName)
                {
                    //we have a match...

                    //... so get the appropriate remote interface ...
                    var remoteInterface = factory.OverridesFileStoreInterface
                        ? factory.ConstructInterface()
                        : new BufferedRemoteFileStoreInterface(factory.Construct(data.InstanceName));

                    //... and a local interface
                    var localInterface = new LocalFileStoreInterface(new DownloadedFileStore(data.LocalFileStorePath));

                    return new CloudStorageInstanceViewModel(
                        remoteInterface,
                        localInterface, 
                        data,
                        AppClosingCancellationToken);
                }
            }
            throw new FactoryNotFoundException(data.ServiceName);
        }
        #endregion

        #region Application-Wide Settings/Data
        /// <summary>
        /// The maximum upload rate.  If 0, then no limit
        /// </summary>
        public int MaxUploadRate
        {
            get => _data.MaxUploadRate;
            set
            {
                if (_data.MaxUploadRate == value) return;
                _data.MaxUploadRate = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The maximum download rate.  If 0, then no limit
        /// </summary>
        public int MaxDownloadRate
        {
            get => _data.MaxDownloadRate;
            set
            {
                if (_data.MaxDownloadRate == value) return;
                _data.MaxDownloadRate = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Whether to start the application on startup
        /// </summary>
        public bool StartOnBoot
        {
            get => _data.StartOnBoot;
            set
            {
                if (_data.StartOnBoot == value) return;
                _data.StartOnBoot = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The cloud storage instances
        /// </summary>
        public ReadOnlyObservableCollection<CloudStorageInstanceViewModel> CloudStorageInstances { get; }
        public CloudStorageInstanceViewModel SelectedInstance
        {
            get => _selectedInstance;
            set
            {
                _selectedInstance = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Addes a new cloud storage instance with the given instantiation data
        /// </summary>
        /// <param name="data">the data to instantiate with</param>
        public void AddCloudStorageInstance(CloudStorageInstanceData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data), "Attempt to add cloud storage instance with null instantiation data!");

            _cloudStorageInstances.Add(CreateCloudStorageInstance(data));
            _data.CloudStorageInstances.Add(data);
            OnPropertyChanged(nameof(_data.CloudStorageInstances));
        }
        /// <summary>
        /// Attempts to remove a cloud storage instance with the given
        ///  local path
        /// </summary>
        /// <param name="name">the name of the instance</param>
        public void RemoveCloudStorageInstance(string name)
        {
            var results = from csi in _cloudStorageInstances
                where csi.InstanceName == name
                select csi;

            var instance = results.First();
            if (instance == null)
            {
                Debug.WriteLine($"Attempt to remove cloud storage instance \"{name}\" that does not exist");
                return;
            }

            _cloudStorageInstances.Remove(instance);

            var dataResults = from csid in _data.CloudStorageInstances
                where csid.InstanceName == name
                select csid;

            var data = dataResults.First();
            if (data == null)
            {
                Debug.WriteLine($"Attempt to remove cloud storage instance \"{name}\" that does not exist");
                return;
            }

            _data.CloudStorageInstances.Remove(data);

            OnPropertyChanged(nameof(_data.CloudStorageInstances));
        }
        #endregion

        #region Internal variables
        public CancellationToken AppClosingCancellationToken => _cts.Token;
        #endregion


        /// <inheritdoc />
        public void Dispose()
        {
            _cts?.Dispose();
            _selectedInstance?.Dispose();
        }
    }
}
