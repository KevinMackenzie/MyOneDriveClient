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
    }
}
