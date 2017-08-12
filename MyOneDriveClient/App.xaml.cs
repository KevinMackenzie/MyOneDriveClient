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
using LocalCloudStorage.AppCore;
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

            AppInstance.PrepWorkingDirectory();
            AppInstance.ScanForPlugins();
            //TODO: where should this happen?
            AppInstance.LoadInstances().Wait();
        }

        public static LocalCloudStorageApp AppInstance = new LocalCloudStorageApp($"{AppDomain.CurrentDomain.BaseDirectory}../../../WorkingDir/");
    }
}
