using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    class CloseAppRequestViewModel : FileStoreRequestViewModelBase
    {
        public CloseAppRequestViewModel(FileStoreRequestViewModel me) : base(me.RequestId)
        {
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public FileStoreRequest.RequestType Type => InnerRequest.Type;
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
