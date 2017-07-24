using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface IItemHandle
    {
        bool IsFolder { get; }
        string Path { get; }
        string Name { get; }
        long Size { get; }
        string SHA1Hash { get; }
        DateTime LastModified { get; }
        //DateTime Created { get; } (less important)
        Task<Stream> GetFileDataAsync();
    }
}
