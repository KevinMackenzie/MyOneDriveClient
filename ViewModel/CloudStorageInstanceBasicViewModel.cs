using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Model;

namespace LocalCloudStorage.ViewModel
{
    public class CloudStorageInstanceBasicViewModel : ViewModelBase
    {
        private readonly CloudStorageInstanceData _data;
        public CloudStorageInstanceBasicViewModel(CloudStorageInstanceData data)
        {
            _data = data;
        }

        /// <summary>
        /// The path for the local file store
        /// </summary>
        public string LocalFileStorePath
        {
            get => _data.LocalFileStorePath;
            set
            {
                _data.LocalFileStorePath = value;
                OnPropertyChanged();
            }
        }
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
                _data.EnableFileLinks = value;
                OnPropertyChanged();
            }
        }
    }
}
