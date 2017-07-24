using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    public class RequestStatusChangedEventArgs : EventArgs
    {
        public RequestStatusChangedEventArgs(int requestId, BufferedRemoteFileStoreInterface.RequestStatus newStatus)
        {
            RequestId = requestId;
            NewStatus = newStatus;
        }

        public int RequestId { get; }
        public BufferedRemoteFileStoreInterface.RequestStatus NewStatus { get; }
    }
}
