using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Identity.Client;
using LocalCloudStorage;
using LocalCloudStorage.Composition;
using LocalCloudStorage.Data;
using LocalCloudStorage.ViewModel;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {            
            /*OneDriveConnection = new OneDrive.OneDriveRemoteFileStoreConnection();
            LocalFileStore = new DownloadedFileStore("C:/Users/kjmac/OneDriveTest");
            RemoteInterface = new BufferedRemoteFileStoreInterface(OneDriveConnection);
            LocalInterface = new LocalFileStoreInterface(LocalFileStore);

            FileStore = new FileStoreBridge(
            new string[]
            {
            }, 
            LocalInterface, RemoteInterface);

            CancellationTokenSource cts = new CancellationTokenSource();
            FileStore.LoadMetadataAsync(cts.Token).Wait();*/

            var data = new LocalCloudStorageData();
            var connectionFactoryManager = new RemoteConnectionFactoryManager();
            connectionFactoryManager.ImportFactories(AppDomain.CurrentDomain.BaseDirectory);

            ConnectionFactoryManager = new RemoteFileStoreConnectionFactoriesViewModel(connectionFactoryManager);
            LocalCloudStorage = new LocalCloudStorageViewModel(data, connectionFactoryManager);
        }

        public static LocalCloudStorageViewModel LocalCloudStorage { get; }
        public static RemoteFileStoreConnectionFactoriesViewModel ConnectionFactoryManager { get; }
    }
}
