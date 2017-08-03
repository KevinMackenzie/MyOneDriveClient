using LocalCloudStorage.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface ILocalFileStore
    {
        /// <summary>
        /// The path of the file store.  All path's given to other methods should be 
        /// relative to this path
        /// </summary>
        string PathRoot { get; }
        void SetItemLastModified(string localPath, DateTime lastModified);
        void SetItemAttributes(string localPath, FileAttributes attributes);

        ILocalItemHandle GetFileHandle(string localPath);
        bool CreateLocalFolder(string localPath, DateTime lastModified);
        bool DeleteLocalItem(string localPath);
        bool MoveLocalItem(string localPath, string newLocalPath);     
        
        bool ItemExists(string localPath);

        Task<List<ILocalItemHandle>> EnumerateItemsAsync(string localPath);

        event EventDelegates.LocalFileStoreChangedHandler OnChanged;
    }
}
