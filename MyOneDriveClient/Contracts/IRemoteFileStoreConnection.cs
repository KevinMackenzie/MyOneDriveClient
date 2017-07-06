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
        void Initialize(string data);

        Task PromptUserLogin();
        void LogUserOut();

        //TODO: this isn't very helpful; most of the time we aren't looking for ONLY their path, usually their metadata too (or a portion of it)
        Task<IEnumerable<string>> EnumerateFilePaths(string remotePath);

        //TODO: this is more useful than the above method
        Task<IEnumerable<IRemoteFileHandle>> EnumerateFiles();

        //gets a list of all updates since the last check for updates
        Task<IEnumerable<IRemoteFileUpdate>> EnumerateUpdates();

        Task<string> GetFileMetadata(string remotePath);

        Task<IRemoteFileHandle> GetFileHandle(string remotePath);
        Task UploadFile(string remotePath, Stream data);

        /// <summary>
        /// When important settings change that need to be cached to the disk to be used on startup
        /// </summary>
        event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;
    }
}
