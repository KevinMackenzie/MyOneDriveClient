using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace MyOneDriveClient.Events
{
    public class RemoteRequestProgressChangedEventArgs : ProgressChangedEventArgs, IFileStoreRequestIdentifiable
    {
        public RemoteRequestProgressChangedEventArgs(long complete, long total, int requestId) : base(complete, total)
        {
            RequestId = requestId;
        }

        public int RequestId { get; }
    }
}
