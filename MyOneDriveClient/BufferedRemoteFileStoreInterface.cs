using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    /// <summary>
    /// This is responsible for buffering requests to the server, recognizing when they fail, and notifying of conflicts while letting unaffected requests pass through
    /// This is also responsible for maintaining the local cache of the remote file metadata
    /// </summary>
    public class BufferedRemoteFileStoreInterface
    {
        public enum RequestStatus
        {
            /// <summary>
            /// Request was successfuly completed
            /// </summary>
            Success,
            /// <summary>
            /// Request is in queue/await network connection
            /// </summary>
            Pending,
            /// <summary>
            /// Request is being processed
            /// </summary>
            InProgress,
            /// <summary>
            /// Request did not successfully complete
            /// </summary>
            Failure
        }

        public enum RequestType
        {
            Write,
            Read,
            Rename,
            Create,
            Delete
        }

        public struct RemoteFileStoreRequest
        {
            /// <summary>
            /// The ID of the request
            /// </summary>
            public int RequestId { get; }
            /// <summary>
            /// The ID of the item involved in the request
            /// </summary>
            public string ItemId { get; }
            /// <summary>
            /// The current status of the request
            /// </summary>
            public RequestStatus Status { get; }
            /// <summary>
            /// If <see cref="Status"/> is <see cref="RequestStatus.Failure"/>, this will tell why
            /// </summary>
            public string ErrorMessage { get; }
            /// <summary>
            /// The type of the request
            /// </summary>
            public RequestType Type { get; }
            /// <summary>
            /// The progress from 0 to 1.0 of the request if applicable
            /// </summary>
            public float Progress { get; }
        }

        public class RemoteItemDelta : ItemDelta
        {
            /// <summary>
            /// The request Id of the item if <see cref="Type"/> is <see cref="DeltaType.ModifiedOrCreated"/>
            /// </summary>
            public int RequestId { get; }
        }



        public BufferedRemoteFileStoreInterface(IRemoteFileStoreConnection remote, string metadataCache)
        {

        }


        #region Public Methods
        /// <summary>
        /// The JSon text of the metadata cache to be saved locally
        /// </summary>
        public string MetadataCache { get; }
        /// <summary>
        /// Upload a file to the remote
        /// </summary>
        /// <param name="item">the item handle to stream data from</param>
        /// <returns>the request id</returns>
        public int RequestUpload(IItemHandle item)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Download a file from the remote
        /// </summary>
        /// <param name="path">the path of the remote item</param>
        /// <param name="streamTo">where to stream the download to</param>
        /// <returns>the request id</returns>
        /// <remarks><see cref="streamTo"/> gets disposed in this method</remarks>
        public int RequestFileDownload(string path, Stream streamTo)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Create a remote folder
        /// </summary>
        /// <param name="path">the path of the remote folder to create</param>
        /// <returns>the request id</returns>
        public int RequestFolderCreate(string path)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Deletes a remote item and its children
        /// </summary>
        /// <param name="path">the path of the item to delete</param>
        /// <returns>the request id</returns>
        public int RequestDelete(string path)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Renames a remote item
        /// </summary>
        /// <param name="path">the path of the item to rename</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public int RequestRename(string path, string newName)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Moves a remote item
        /// </summary>
        /// <param name="path">the previous path of the item</param>
        /// <param name="newPath">the new path of the item</param>
        /// <returns>the request id</returns>
        public int RequestMove(string path, string newPath)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Enumerates the currently active requests
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RemoteFileStoreRequest> EnumerateActiveRequests()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// When an existing request's progress changes
        /// </summary>
        public EventDelegates.RemoteRequestProgressChangedHandler OnRequestProgressChanged;
        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        public EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;


        /// <summary>
        /// Requests the remote deltas since the previous request
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RemoteItemDelta> RequestDeltas()
        {
            throw new NotImplementedException();
        }
        #endregion

        /*
         * TODO: support remote change events
         */
    }
}
