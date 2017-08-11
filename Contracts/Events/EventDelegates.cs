using System.Threading.Tasks;

namespace LocalCloudStorage.Events
{
    public static class EventDelegates
    {
        public delegate Task RemoteFileStoreConnectionUpdateHandler(object sender, RemoteFileStoreDataChanged e);
        public delegate Task LocalFileStoreChangedHandler(object sender, LocalFileStoreEventArgs e);

        public delegate void ProgressChangedHandler(object sender, ProgressChangedEventArgs e);

        public delegate void RemoteRequestProgressChangedHandler(object sender, RemoteRequestProgressChangedEventArgs e);
        public delegate void RequestStatusChangedHandler(object sender, RequestStatusChangedEventArgs e);

        public delegate Task NotifyStreamDisposedHandler(object sender);

        public delegate void ImportRFSCFactoryHandler(object sender, ImportEventArgs e);
    }
}
