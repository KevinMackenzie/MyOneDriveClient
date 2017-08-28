using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage
{
    public interface IRemoteFileStoreInterface : IFileStoreInterface, IDisposable
    {
        Task<IEnumerable<IItemDelta>> RequestDeltasAsync(CancellationToken ct);

        void RequestUpload(string path, Stream streamFrom);
        void RequestFileDownload(string path, Stream streamTo);
        void RequestDelete(string path);
        void RequestFolderCreate(string path);
        void RequestMove(string path, string newParentPath);
        void RequestRename(string path, string newName);

        Task RequestUploadImmediateAsync(string path, Stream streamFrom, CancellationToken ct);
        Task RequestFileDownloadImmediateAsync(string path, Stream streamTo, CancellationToken ct);
        Task RequestDeleteItemImmediateAsync(string path, CancellationToken ct);
        Task RequestRenameItemImmediateAsync(string path, string newName, CancellationToken ct);

        /// <summary>
        /// When an existing request's progress changes
        /// </summary>
        event EventDelegates.RemoteRequestProgressChangedHandler OnRequestProgressChanged;
    }
}
