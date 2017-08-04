using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage.Contracts
{
    /// <summary>
    /// Responsible for keeping track of local file changes and conflicts.  Recognizes and notifies when file system actions fail.
    /// </summary>
    public interface ILocalFileStoreInterface : IFileStoreInterface
    {
        bool ItemExists(string path);
        Task<IEnumerable<ItemDelta>> GetDeltasAsync(bool comprehensive);

        void RequestWritableStream(string path, string sha1, DateTime lastModified,
            Action<FileStoreRequest> onCompleteFunc);
        void RequestReadOnlyStream(string path, Action<FileStoreRequest> onCompleteFunc);
        void RequestDelete(string path);
        void RequestFolderCreate(string path, DateTime lastModified);
        void RequestMove(string path, string newParentPath);
        void RequestRename(string path, string newName);

        Task<FileStoreRequest> RequestWritableStreamImmediateAsync(string path, string sha1, DateTime lastModified);
        Task<FileStoreRequest> RequestReadOnlyStreamImmediateAsync(string path);
        Task RequestDeleteItemImmediateAsync(string path);
        Task RequestRenameItemImmediateAsync(string path, string newName);

        Task SaveNonSyncFile(string path, string content);
        Task<string> ReadNonSyncFile(string path);
    }
}
