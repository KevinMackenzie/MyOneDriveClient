using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface ILocalFileStore
    {
        Task SaveFileAsync(string localPath, byte[] data);
        Task<byte[]> LoadFileAsync(string localPath);
    }
}
