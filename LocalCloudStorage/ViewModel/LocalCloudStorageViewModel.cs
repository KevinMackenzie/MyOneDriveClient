using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Contracts.Exceptions;
using LocalCloudStorage.Data;

namespace LocalCloudStorage.ViewModel
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class LocalCloudStorageViewModel : ViewModelBase
    {
        private readonly LocalCloudStorageData _data;
        private readonly ObservableCollection<CloudStorageInstanceViewModel> _cloudStorageInstances = new ObservableCollection<CloudStorageInstanceViewModel>();
        private CloudStorageInstanceViewModel _selectedInstance;
        public LocalCloudStorageViewModel(LocalCloudStorageData data)
        {
            foreach (var factory in RemoteConnectionFactories)
            {
            }

            //TODO: someone should be responsible for saving the settings when the changed
            _data = data;
            CloudStorageInstances = new ReadOnlyObservableCollection<CloudStorageInstanceViewModel>(_cloudStorageInstances);
            
            //create a viewmodel for each of the cloud storage instances
            foreach (var cloudStorageInstace in data.CloudStorageInstances)
            {
                AddCloudStorageInstance(cloudStorageInstace);
            }

            //make sure to update the data list when the instances list changes
            ///_cloudStorageInstances.CollectionChanged += (sender, args) => UpdateCloudStorageInstancesData();
        }

        #region Private Methods
        private CloudStorageInstanceViewModel CreateCloudStorageInstance(CloudStorageInstanceData data)
        {
            //get the appropriate factory
            foreach (var factory in RemoteConnectionFactories)
            {
                if (factory.Metadata.ServiceName == data.ServiceName)
                {
                    //we have a match...

                    //... so get the appropriate remote interface ...
                    var remoteInterface = factory.Value.OverridesFileStoreInterface
                        ? factory.Value.ConstructInterface()
                        : new BufferedRemoteFileStoreInterface(factory.Value.Construct());

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
        public CancellationToken AppClosingCancellationToken { get; }
        #endregion
        
        [ImportMany()]
        public IEnumerable<Lazy<IRemoteFileStoreConnectionFactory, RemoteFileStoreConnectionFactoryMetadataAttribute>> RemoteConnectionFactories { get; }
    }
}
