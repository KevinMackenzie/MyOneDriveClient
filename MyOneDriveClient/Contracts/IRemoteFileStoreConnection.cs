using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    /// <summary>
    /// A remote file server with read/write access 
    /// </summary>
    public interface IRemoteFileStoreConnection
    {
        Task PromptUserLogin();
        void LogUserOut();

        Task<string> GetFileMetadata(string remotePath);

        Task<FileData> DownloadFile(string remotePath);
        Task UploadFile(string remotePath, byte[] data);
    }
}
