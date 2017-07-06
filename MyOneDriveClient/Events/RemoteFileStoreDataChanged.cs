using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    /// <summary>
    /// Contains a string with important startup information of <see cref="IRemoteFileStoreConnection"/> changes
    /// </summary>
    public class RemoteFileStoreDataChanged
    {
        public RemoteFileStoreDataChanged(string message)
        {
            Message = message;
        }
        string Message { get; }
    }
}
