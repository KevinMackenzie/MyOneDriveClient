using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public class RemoteItemUpdate : IRemoteItemUpdate
    {
        bool _deleted;
        IRemoteItemHandle _fileHandle;

        public RemoteItemUpdate(bool deleted, IRemoteItemHandle fileHandle)
        {
            _deleted = deleted;
            _fileHandle = fileHandle;
        }

        public bool Deleted { get => _deleted; }

        public IRemoteItemHandle ItemHandle { get => _fileHandle; }
    }
}
