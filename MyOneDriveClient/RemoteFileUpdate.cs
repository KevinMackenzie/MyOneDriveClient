using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class RemoteFileUpdate : IRemoteFileUpdate
    {
        bool _deleted;
        IRemoteFileHandle _fileHandle;

        public RemoteFileUpdate(bool deleted, IRemoteFileHandle fileHandle)
        {
            _deleted = deleted;
            _fileHandle = fileHandle;
        }

        public bool Deleted { get => _deleted; }

        public IRemoteFileHandle FileHandle { get => _fileHandle; }
    }
}
