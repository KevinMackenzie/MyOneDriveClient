using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    public class RemoteRequestProgressChangedEventArgs : ProgressChangedEventArgs
    {
        public RemoteRequestProgressChangedEventArgs(int progressPercentage, object userState, int requestId) : base(progressPercentage, userState)
        {
            RequestId = requestId;
        }

        public int RequestId { get; }
    }
}
