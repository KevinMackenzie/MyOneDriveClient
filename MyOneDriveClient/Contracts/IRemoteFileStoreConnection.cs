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

        Task DownloadFile(string localPath, string remotePath);
        Task DownloadFolder(string localPath, string remotePath);

        Task UploadFile(string localPath, string remotePath);
        Task UploadFolder(string localPath, string remotePath);
    }
}
