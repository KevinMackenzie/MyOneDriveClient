﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Identity.Client;

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
            FileStore = new ActiveSyncFileStore(
            new string[]
            {
            }, new DownloadedFileStore("C:/Users/kjmac/OneDriveTest"), OneDriveConnection);
        }

        public static OneDrive.OneDriveRemoteFileStoreConnection OneDriveConnection;
        public static ActiveSyncFileStore FileStore;
    }
}
