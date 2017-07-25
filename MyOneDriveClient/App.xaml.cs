using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Identity.Client;
using MyOneDriveClient.OneDrive;

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
            FileStore = new FileStoreBridge(
            new string[]
            {
            }, 
            new LocalFileStoreInterface(new DownloadedFileStore("C:/Users/kjmac/OneDriveTest")), 
            new BufferedRemoteFileStoreInterface(new OneDriveRemoteFileStoreConnection()));
            FileStore.LoadMetadataAsync().Wait();
        }

        public static OneDrive.OneDriveRemoteFileStoreConnection OneDriveConnection;
        public static FileStoreBridge FileStore;
    }
}
