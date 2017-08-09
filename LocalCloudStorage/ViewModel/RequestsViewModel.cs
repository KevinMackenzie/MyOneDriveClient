using LocalCloudStorage.ViewModel;

namespace LocalCloudStorage
{
    public class RequestsViewModel
    {
        public RequestsViewModel(LocalFileStoreInterface localFileStoreInterface,
            BufferedRemoteFileStoreInterface remoteFileStoreInterface)
        {
            LocalRequests = new BaseRequestsViewModel(localFileStoreInterface);
            RemoteRequests = new RemoteRequestsViewModel(remoteFileStoreInterface);
        }

        public BaseRequestsViewModel LocalRequests { get; }
        public RemoteRequestsViewModel RemoteRequests { get; }
    }
}
