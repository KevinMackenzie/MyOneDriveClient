using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage.Contracts
{
    public interface IRemoteFileStoreInterface : IFileStoreInterface
    {
        Task<IEnumerable<ItemDelta>> RequestDeltasAsync();

        void RequestUpload(string path, Stream streamFrom);
        void RequestFileDownload(string path, Stream streamTo);
        void RequestDelete(string path);
        void RequestFolderCreate(string path);
        void RequestMove(string path, string newParentPath);
        void RequestRename(string path, string newName);

        Task RequestUploadImmediateAsync(string path, Stream streamFrom);
        Task RequestFileDownloadImmediateAsync(string path, Stream streamTo);
        Task RequestDeleteItemImmediateAsync(string path);
        Task RequestRenameItemImmediateAsync(string path, string newName);

        /// <summary>
        /// When an existing request's progress changes
        /// </summary>
        event EventDelegates.RemoteRequestProgressChangedHandler OnRequestProgressChanged;
    }
}
