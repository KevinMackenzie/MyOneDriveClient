using Newtonsoft.Json.Linq;
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
        JObject Metadata { get; }
        string Id { get; }
        bool IsFolder { get; }
        string Path { get; }
        string Name { get; }
        string SHA1Hash { get; }
        Task<Stream> DownloadFileAsync();
    }
}
