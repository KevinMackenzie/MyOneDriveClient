using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    /// <summary>
    /// This abstracts away worrying about file timestamps, overwriting, and losing work
    /// </summary>
    public interface IRemoteFileStoreDownload
    {
        string PathRoot { get; }

        Task SaveFileAsync(IRemoteItemHandle file);
        Task<IRemoteItemHandle> GetRemoteFileHandleAsync(string localPath);

        bool CreateLocalFolder(string folderPath);
        Task<string> GetLocalSHA1Async(string id);
        Task<bool> DeleteLocalItemAsync(IRemoteItemHandle remoteHandle);
        Task<bool> MoveLocalItemAsync(string localPath, string newLocalPath);
    }
}
