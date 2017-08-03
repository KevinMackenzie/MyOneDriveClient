using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.ViewModel;
using LocalCloudStorage.Events;

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
