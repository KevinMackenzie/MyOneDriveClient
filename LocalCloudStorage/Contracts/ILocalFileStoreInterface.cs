using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        Task<IEnumerable<ItemDelta>> GetDeltasAsync(bool comprehensive, CancellationToken ct);

        void RequestWritableStream(string path, string sha1, DateTime lastModified,
            Action<FileStoreRequest> onCompleteFunc);
        void RequestReadOnlyStream(string path, Action<FileStoreRequest> onCompleteFunc);
        void RequestDelete(string path);
        void RequestFolderCreate(string path, DateTime lastModified);
        void RequestMove(string path, string newParentPath);
        void RequestRename(string path, string newName);

        Task<FileStoreRequest> RequestWritableStreamImmediateAsync(string path, string sha1, DateTime lastModified, CancellationToken ct);
        Task<FileStoreRequest> RequestReadOnlyStreamImmediateAsync(string path, CancellationToken ct);
        Task RequestDeleteItemImmediateAsync(string path, CancellationToken ct);
        Task RequestRenameItemImmediateAsync(string path, string newName, CancellationToken ct);

        Task SaveNonSyncFile(string path, string content, CancellationToken ct);
        Task<string> ReadNonSyncFile(string path, CancellationToken ct);
    }
}
