using MyOneDriveClient.Events;
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
        Task<IRemoteItemHandle> GetFileHandleAsync(string localPath);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteItem"></param>
        /// <returns>The local version of the remote item</returns>
        Task<IRemoteItemHandle> GetFileHandleAsync(IRemoteItemHandle remoteItem);

        bool CreateLocalFolder(string folderPath);
        Task<string> GetLocalSHA1Async(string id);
        Task<bool> DeleteLocalItemAsync(IRemoteItemHandle remoteHandle);
        Task<bool> MoveLocalItemAsync(IRemoteItemHandle remoteHandle);

        bool ItemExists(IRemoteItemHandle remoteHandle);

        event EventDelegates.LocalFileStoreUpdateHandler OnUpdate;
    }
}
