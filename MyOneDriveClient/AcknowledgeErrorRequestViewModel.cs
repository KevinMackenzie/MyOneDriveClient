using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class AcknowledgeErrorRequestViewModel : FileStoreRequestViewModelBase
    {
        public AcknowledgeErrorRequestViewModel(FileStoreRequestViewModel me, string errorMessage) : base(me.RequestId)
        {
            ErrorMessage = errorMessage;
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public FileStoreRequest.RequestType Type => InnerRequest.Type;
        public string ErrorMessage { get; }
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
