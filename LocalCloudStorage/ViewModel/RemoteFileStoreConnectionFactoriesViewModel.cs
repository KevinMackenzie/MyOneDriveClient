using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Composition;

namespace LocalCloudStorage.ViewModel
{
    public class RemoteFileStoreConnectionFactoriesViewModel : ViewModelBase
    {
        private IEnumerable<IRemoteFileStoreConnectionFactory> _factories;
        private readonly RemoteConnectionFactoryManager _factoryManager;
        public RemoteFileStoreConnectionFactoriesViewModel(RemoteConnectionFactoryManager factoryManager)
        {
            _factoryManager = factoryManager;
            _factories = _factoryManager.Factories;

            factoryManager.OnFactoryImport += (sender, args) => Factories = _factoryManager.Factories;
        }

        public IEnumerable<IRemoteFileStoreConnectionFactory> Factories
        {
            get => _factories;
            private set
            {
                _factories = value;
                OnPropertyChanged();
            }
        }
    }
}
