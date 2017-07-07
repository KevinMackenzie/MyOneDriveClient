using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    /// <summary>
    /// A remote file server with read/write access 
    /// </summary>
    public interface IRemoteFileStoreConnection
    {
        /// <summary>
        /// Called before anything else to prepare the file store with any data saved to the disk
        /// </summary>
        /// <param name="data">the data needed for initialization.  Typically comes from a previous <see cref="OnUpdate"/> event</param>
        //void Initialize(string data);

        /// <summary>
        /// Prompts the user to authenticate
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This isn't the most portable option.  Perhaps this should instead take authentication information
        /// and be replaced by an "authenticate" method.
        /// </remarks>
        Task PromptUserLoginAsync();
        /// <summary>
        /// Forgets the user authentication information
        /// </summary>
        void LogUserOut();

        /// <summary>
        /// Pulls a page of the latest deltas.  If <paramref name="deltaLink"/> is blank or null, it pulls all files
        /// </summary>
        /// <param name="deltaLink">the delta link from the previous page</param>
        /// <returns>The next page of deltas since the last check for deltas</returns>
        Task<DeltaPage> GetDeltasPageAsync(string deltaLink);
        Task<DeltaPage> GetDeltasPageAsync(DeltaPage prevPage);
        
        /// <summary>
        /// Gets item metadata with a given path.  Can be used to check if an item exists
        /// </summary>
        /// <param name="remotePath">the remote path of the item</param>
        /// <returns>the item metadata at the remote path.  Null if it doesn't exist</returns>
        Task<string> GetItemMetadataAsync(string remotePath);
        /// <summary>
        /// Retrieves an item handle from the remote with the given remote path
        /// </summary>
        /// <param name="remotePath">the remote path of the item</param>
        /// <returns>an item handle for the given path</returns>
        /// <remarks>
        /// This should seldom be used
        /// </remarks>
        Task<IRemoteItemHandle> GetItemHandleAsync(string remotePath);
        /// <summary>
        /// Uploads a given file with a remote file path
        /// </summary>
        /// <param name="remotePath">the remote path of the uploaded item</param>
        /// <param name="data">the data contents of the file</param>
        /// <returns>the id of the created folder</returns>
        Task<string> UploadFileAsync(string remotePath, Stream data);
        /// <summary>
        /// Creates a folder with the given remote path
        /// </summary>
        /// <param name="remotePath">the remote path of the folder</param>
        /// <returns>the id of the created folder.  if the folder already exists, returns id of existing folder</returns>
        Task<string> CreateFolderAsync(string remotePath);

        /// <summary>
        /// Gets item metadata with a given id
        /// </summary>
        /// <param name="id">the id of the item</param>
        /// <returns>the metadata of the item.  Null if the item doesn't exist</returns>
        Task<string> GetItemMetadataByIdAsync(string id);
        /// <summary>
        /// Retrieves an item handle from the remote with the given ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns>the remote item handle with the given id</returns>
        Task<IRemoteItemHandle> GetItemHandleByIdAsync(string id);
        /// <summary>
        /// Uploads a given file with a given parent id
        /// </summary>
        /// <param name="parentId">the id of the parent of this file (should be a folder)</param>
        /// <param name="fileName">the name of the file to create</param>
        /// <param name="data">the data contents of the file</param>
        /// <returns>the ID of the created item</returns>
        /// <remarks>
        /// This does not check to see if the file already exists.  There should be an option to keep both or overwrite
        /// </remarks>
        Task<string> UploadFileByIdAsync(string parentId, string fileName, Stream data);
        /// <summary>
        /// Create a remote folder as a child of the given parent id and name
        /// </summary>
        /// <param name="parentId">the id of the parent item</param>
        /// <param name="name">the name of the folder to create</param>
        /// <returns>the id of the created folder</returns>
        /// <remarks>
        /// This will return the id of the existing folder if one already exists with the given name and parent id
        /// </remarks>
        Task<string> CreateFolderByIdAsync(string parentId, string name);

        /// <summary>
        /// When important settings change that need to be cached to the disk to be used on startup
        /// </summary>
        //event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;
    }
}
