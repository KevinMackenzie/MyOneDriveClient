using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    public static class EventDelegates
    {
        public delegate Task RemoteFileStoreConnectionUpdateHandler(object sender, RemoteFileStoreDataChanged e);
        public delegate Task LocalFileStoreChangedHandler(object sender, LocalFileStoreEventArgs e);

        public delegate void ProgressChangedHandler(object sender, ProgressChangedEventArgs e);

        public delegate Task RemoteRequestProgressChangedHandler(object sender, RemoteRequestProgressChangedEventArgs e);
        public delegate Task RequestStatusChangedHandler(object sender, RequestStatusChangedEventArgs e);

        public delegate void NotifyStreamDisposedHandler(object sender);
    }
}
