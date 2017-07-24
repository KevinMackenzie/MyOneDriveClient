using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    public class RemoteRequestProgressChangedEventArgs : ProgressChangedEventArgs
    {
        public RemoteRequestProgressChangedEventArgs(double complete, double total, int requestId) : base(complete, total)
        {
            RequestId = requestId;
        }

        public int RequestId { get; }
    }
}
