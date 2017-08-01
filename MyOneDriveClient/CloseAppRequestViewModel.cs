using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    class CloseAppRequestViewModel : ViewModelBase
    {
        public CloseAppRequestViewModel(FileStoreRequestViewModel me)
        {
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public FileStoreRequest.RequestType Type => InnerRequest.Type;
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
