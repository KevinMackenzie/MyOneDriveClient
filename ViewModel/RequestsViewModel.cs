using LocalCloudStorage.Events;
using LocalCloudStorage.ViewModel;

namespace LocalCloudStorage
{
    public class RequestsViewModel
    {
        public RequestsViewModel(CloudStorageInstanceViewModel instance)
        {
            LocalRequests = new BaseRequestsViewModel(statusHandler => instance.OnLocalRequestStatusChanged += statusHandler);
            RemoteRequests = new RemoteRequestsViewModel(
                statusHandler => instance.OnRemoteRequestStatusChanged += statusHandler,
                progressHandler => instance.OnRemoteRequestProgressChanged += progressHandler);
        }

        public BaseRequestsViewModel LocalRequests { get; }
        public RemoteRequestsViewModel RemoteRequests { get; }
    }
}
