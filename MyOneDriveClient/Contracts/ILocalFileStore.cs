using MyOneDriveClient.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface ILocalFileStore
    {
        /// <summary>
        /// The path of the file store.  All path's given to other methods should be 
        /// relative to this path
        /// </summary>
        string PathRoot { get; }
        Task SaveFileAsync(string localPath, DateTime lastModified, Stream data);
        //changes to hidden files will not be propagated through the OnUpdate event
        Task SaveFileAsync(string localPath, DateTime lastModified, Stream data, FileAttributes attributes);
        Task<IItemHandle> GetFileHandleAsync(string localPath);
        bool CreateLocalFolder(string localPath, DateTime lastModified);
        Task<string> GetLocalSHA1Async(string localPath);
        Task<bool> DeleteLocalItemAsync(string localPath);
        Task<bool> MoveLocalItemAsync(string localPath, string newLocalPath);        
        bool ItemExists(string localPath);
        Task<IEnumerable<IItemHandle>> EnumerateItemsAsync(string localPath);
        event EventDelegates.LocalFileStoreUpdateHandler OnUpdate;
    }
}
