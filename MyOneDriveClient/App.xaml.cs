using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Identity.Client;
using MyOneDriveClient.OneDrive;
using LocalCloudStorage;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {            
            OneDriveConnection = new OneDrive.OneDriveRemoteFileStoreConnection();
            LocalFileStore = new DownloadedFileStore("C:/Users/kjmac/OneDriveTest");
            RemoteInterface = new BufferedRemoteFileStoreInterface(OneDriveConnection);
            LocalInterface = new LocalFileStoreInterface(LocalFileStore);

            FileStore = new FileStoreBridge(
            new string[]
            {
            }, 
            LocalInterface, RemoteInterface);

            FileStore.LoadMetadataAsync().Wait();
        }

        public static OneDriveRemoteFileStoreConnection OneDriveConnection;
        public static DownloadedFileStore LocalFileStore;
        public static BufferedRemoteFileStoreInterface RemoteInterface;
        public static LocalFileStoreInterface LocalInterface;
        public static FileStoreBridge FileStore;
    }
}
