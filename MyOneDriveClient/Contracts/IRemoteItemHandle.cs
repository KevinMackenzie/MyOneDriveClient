using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface IRemoteItemHandle
    {
        string Metadata { get; }
        bool IsFolder { get; }
        Task<Stream> DownloadFileAsync();
    }
}
