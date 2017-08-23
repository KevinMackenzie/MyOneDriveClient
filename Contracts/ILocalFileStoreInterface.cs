using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <summary>
    /// Responsible for keeping track of local file changes and conflicts.  Recognizes and notifies when file system actions fail.
    /// </summary>
    public interface ILocalFileStoreInterface : IFileStoreInterface
    {
        bool ItemExists(string path);
        Task<IEnumerable<IItemDelta>> GetDeltasAsync(bool comprehensive, CancellationToken ct);

        void RequestWritableStream(string path, string sha1, DateTime lastModified,
            Action<FileStoreRequest> onCompleteFunc);
        void RequestReadOnlyStream(string path, Action<FileStoreRequest> onCompleteFunc);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lastModified">the time the item was last modified (NOT the time the file was deleted)</param>
        void RequestDelete(string path, DateTime lastModified);
        void RequestFolderCreate(string path, DateTime lastModified);
        void RequestMove(string path, string newParentPath);
        void RequestRename(string path, string newName);

        Task<FileStoreRequest> RequestWritableStreamImmediateAsync(string path, string sha1, DateTime lastModified, CancellationToken ct);
        Task<FileStoreRequest> RequestReadOnlyStreamImmediateAsync(string path, CancellationToken ct);
        Task<bool> RequestDeleteItemImmediateAsync(string path, CancellationToken ct);
        Task<bool> RequestRenameItemImmediateAsync(string path, string newName, CancellationToken ct);

        Task SaveNonSyncFile(string path, string content, CancellationToken ct);
        Task<string> ReadNonSyncFile(string path, CancellationToken ct);
    }
}
