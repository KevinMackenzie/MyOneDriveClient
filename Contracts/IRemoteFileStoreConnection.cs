using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage
{
    /// <summary>
    /// A remote file server with read/write access.  Implementations
    ///  should only throw <see cref="TaskCanceledException"/>s
    /// </summary>
    public interface IRemoteFileStoreConnection : IDisposable
    {
        /// <summary>
        /// Called before anything else to prepare the file store with any data saved to the disk
        /// </summary>
        /// <param name="data">the data needed for initialization.  Typically comes from a previous <see cref="OnUpdate"/> event</param>
        //void Initialize(string data);
        
        /// <summary>
        /// Authenticates the user with implementation-specific authentication
        ///  information
        /// </summary>
        /// <returns></returns>
        Task LogUserInAsync();
        /// <summary>
        /// Forgets the user authentication information
        /// </summary>
        void LogUserOut();

        /// <summary>
        /// Pulls all of remote deltas
        /// </summary>
        /// <param name="nextRequestData">the delta link from the previous request</param>
        /// <returns>The next page of deltas since the last check for deltas</returns>
        Task<IDeltaList> GetDeltasAsync(string nextRequestData, CancellationToken ct);
        
        /// <summary>
        /// Gets item metadata with a given path.  Can be used to check if an item exists
        /// </summary>
        /// <param name="remotePath">the remote path of the item</param>
        /// <returns>the item metadata at the remote path.  Null if it doesn't exist</returns>
        Task<HttpResult<string>> GetItemMetadataAsync(string remotePath, CancellationToken ct);
        /// <summary>
        /// Retrieves an item handle from the remote with the given remote path
        /// </summary>
        /// <param name="remotePath">the remote path of the item</param>
        /// <returns>an item handle for the given path</returns>
        /// <remarks>
        /// This should seldom be used
        /// </remarks>
        Task<HttpResult<IRemoteItemHandle>> GetItemHandleAsync(string remotePath, CancellationToken ct);
        /// <summary>
        /// Uploads a given file with a remote file path
        /// </summary>
        /// <param name="remotePath">the remote path of the uploaded item</param>
        /// <param name="data">the data contents of the file</param>
        /// <returns>the item handle of the created item</returns>
        Task<HttpResult<IRemoteItemHandle>> UploadFileAsync(string remotePath, Stream data, CancellationToken ct);
        /// <summary>
        /// Creates a folder with the given remote path
        /// </summary>
        /// <param name="remotePath">the remote path of the folder</param>
        /// <returns>the id of the created folder.  if the folder already exists, returns id of existing folder</returns>
        Task<HttpResult<IRemoteItemHandle>> CreateFolderAsync(string remotePath, CancellationToken ct);
        Task<HttpResult<bool>> DeleteItemAsync(string remotePath, CancellationToken ct);
        //Task<HttpResult<IRemoteItemHandle>> UpdateItemAsync(string remotePath, string json);
        Task<HttpResult<IRemoteItemHandle>> RenameItemAsync(string remotePath, string newName, CancellationToken ct);

        /// <summary>
        /// Gets item metadata with a given id
        /// </summary>
        /// <param name="id">the id of the item</param>
        /// <returns>the metadata of the item.  Null if the item doesn't exist</returns>
        Task<HttpResult<string>> GetItemMetadataByIdAsync(string id, CancellationToken ct);
        /// <summary>
        /// Retrieves an item handle from the remote with the given ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns>the remote item handle with the given id</returns>
        Task<HttpResult<IRemoteItemHandle>> GetItemHandleByIdAsync(string id, CancellationToken ct);
        /// <summary>
        /// Uploads a given file with a given parent id
        /// </summary>
        /// <param name="parentId">the id of the parent of this file (should be a folder)</param>
        /// <param name="fileName">the name of the file to create</param>
        /// <param name="data">the data contents of the file</param>
        /// <returns>the item handle of the created item</returns>
        /// <remarks>
        /// This does not check to see if the file already exists.  There should be an option to keep both or overwrite
        /// </remarks>
        Task<HttpResult<IRemoteItemHandle>> UploadFileByIdAsync(string parentId, string fileName, Stream data, CancellationToken ct);
        /// <summary>
        /// Create a remote folder as a child of the given parent id and name
        /// </summary>
        /// <param name="parentId">the id of the parent item</param>
        /// <param name="name">the name of the folder to create</param>
        /// <returns>the id of the created folder</returns>
        /// <remarks>
        /// This will return the id of the existing folder if one already exists with the given name and parent id
        /// </remarks>
        Task<HttpResult<IRemoteItemHandle>> CreateFolderByIdAsync(string parentId, string name, CancellationToken ct);
        Task<HttpResult<bool>> DeleteItemByIdAsync(string id, CancellationToken ct);
        //Task<HttpResult<IRemoteItemHandle>> UpdateItemByIdAsync(string id, string json);
        Task<HttpResult<IRemoteItemHandle>> RenameItemByIdAsync(string id, string newName, CancellationToken ct);
        Task<HttpResult<IRemoteItemHandle>> MoveItemByIdAsync(string id, string newParentId, CancellationToken ct);

        /// <summary>
        /// When important settings change that need to be cached to the disk to be used on startup
        /// </summary>
        //event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;
    }
}
