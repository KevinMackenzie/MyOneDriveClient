using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace MyOneDriveClient
{
    public abstract class FileStoreRequestViewModelBase : ViewModelBase, IFileStoreRequestIdentifiable
    {
        public FileStoreRequestViewModelBase(int requestId)
        {
            RequestId = requestId;
        }
        public int RequestId { get; }
    }
}
