using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
            Upload,
            Download,
            Rename,
            Move,
            Create,
            Delete
        }

        public interface IRemoteFileStoreRequestExtraData
        { }

        #region Request Extra Datas
        private class RequestUploadExtraData : IRemoteFileStoreRequestExtraData
        {
            public RequestUploadExtraData(IItemHandle itemHandle)
            {
                ItemHandle = itemHandle;
            }
            public IItemHandle ItemHandle { get; }
        }
        private class RequestDownloadExtraData : IRemoteFileStoreRequestExtraData
        {
            public RequestDownloadExtraData(ILocalItemHandle itemHandle)
            {
                ItemHandle = itemHandle;
            }
            public ILocalItemHandle ItemHandle { get; }
        }
        private class RequestRenameExtraData : IRemoteFileStoreRequestExtraData
        {
            public RequestRenameExtraData(string newName)
            {
                NewName = newName;
            }
            public string NewName { get; }
        }
        private class RequestMoveExtraData : IRemoteFileStoreRequestExtraData
        {
            public RequestMoveExtraData(string newParentPath)
            {
                NewParentPath = newParentPath;
            }
            public string NewParentPath { get; }
        }
        #endregion

        public struct RemoteFileStoreRequest
        {
            private int _requestId;
            public RemoteFileStoreRequest(ref int id, RequestType type, string path, IRemoteFileStoreRequestExtraData extraData)
            {
                _requestId = Interlocked.Increment(ref id);

                Path = path;
                Status = RequestStatus.Pending;
                ErrorMessage = null;
                Type = type;
                Progress = 0;
                ExtraData = extraData;
            }
            /// <summary>
            /// The ID of the request
            /// </summary>
            public int RequestId => _requestId;
            /// <summary>
            /// The path of the item in the request
            /// </summary>
            public string Path { get; }
            /// <summary>
            /// The current status of the request
            /// </summary>
            public RequestStatus Status { get; set; }
            /// <summary>
            /// If <see cref="Status"/> is <see cref="RequestStatus.Failure"/>, this will tell why
            /// </summary>
            public string ErrorMessage { get; set; }
            /// <summary>
            /// The type of the request
            /// </summary>
            public RequestType Type { get; }
            /// <summary>
            /// The progress from 0 to 1.0 of the request if applicable
            /// </summary>
            public float Progress { get; set; }

            public IRemoteFileStoreRequestExtraData ExtraData { get; }
        }

        public class RemoteItemDelta : ItemDelta
        {
            public RemoteItemDelta(int requestId)
            {
                RequestId = requestId;
            }
            /// <summary>
            /// The request Id of the item if <see cref="Type"/> is <see cref="DeltaType.ModifiedOrCreated"/>
            /// </summary>
            public int RequestId { get; }
        }

        #region Private Fields
        private IRemoteFileStoreConnection _remote;
        private RemoteItemMetadataCache _metadata = new RemoteItemMetadataCache();
        private ConcurrentQueue<RemoteFileStoreRequest> _requests = new ConcurrentQueue<RemoteFileStoreRequest>();
        private int _requestId;//TODO: should this be volatile
        #endregion

        public BufferedRemoteFileStoreInterface(IRemoteFileStoreConnection remote, string metadataCache)
        {
            _remote = remote;
            _metadata.Deserialize(metadataCache);
        }

        #region Private Methods
        private int EnqueueRequest(RemoteFileStoreRequest request)
        {
            _requests.Enqueue(request);
            return request.RequestId;
        }
        #endregion


        #region Public Properties
        /// <summary>
        /// The JSon text of the metadata cache to be saved locally
        /// </summary>
        public string MetadataCache => _metadata.Serialize();
        #endregion
        
        #region Public Methods
        /// <summary>
        /// Upload a file to the remote
        /// </summary>
        /// <param name="item">the item handle to stream data from</param>
        /// <returns>the request id</returns>
        public int RequestUpload(IItemHandle item)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Upload, item.Path,
                new RequestUploadExtraData(item)));
        }
        /// <summary>
        /// Download a file from the remote
        /// </summary>
        /// <param name="path">the path of the remote item</param>
        /// <param name="streamTo">where to stream the download to</param>
        /// <returns>the request id</returns>
        /// <remarks><see cref="streamTo"/> gets disposed in this method</remarks>
        public int RequestFileDownload(string path, ILocalItemHandle streamTo)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Download, path,
                new RequestDownloadExtraData(streamTo)));
        }
        /// <summary>
        /// Create a remote folder
        /// </summary>
        /// <param name="path">the path of the remote folder to create</param>
        /// <returns>the request id</returns>
        public int RequestFolderCreate(string path)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Create, path, null));
        }
        /// <summary>
        /// Deletes a remote item and its children
        /// </summary>
        /// <param name="path">the path of the item to delete</param>
        /// <returns>the request id</returns>
        public int RequestDelete(string path)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Delete, path, null));
        }
        /// <summary>
        /// Renames a remote item
        /// </summary>
        /// <param name="path">the path of the item to rename</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public int RequestRename(string path, string newName)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Rename, path,
                new RequestRenameExtraData(newName)));
        }
        /// <summary>
        /// Moves a remote item
        /// </summary>
        /// <param name="path">the previous path of the item</param>
        /// <param name="newParentPath">the folder the item is moved to</param>
        /// <returns>the request id</returns>
        public int RequestMove(string path, string newParentPath)
        {
            return EnqueueRequest(new RemoteFileStoreRequest(ref _requestId, RequestType.Move, path,
                new RequestMoveExtraData(newParentPath)));
        }

        public bool TryGetRequest(int requestId, out RemoteFileStoreRequest? request)
        {
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                request = null;
                return false;
            }
            else
            {
                request = reqs.First();
                return true;
            }

        }
        /// <summary>
        /// Enumerates the currently active requests
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RemoteFileStoreRequest> EnumerateActiveRequests()
        {
            //this creates a copy
            return new List<RemoteFileStoreRequest>(_requests);
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
        public async Task<IEnumerable<ItemDelta>> RequestDeltasAsync()
        {
            //TODO: this should pause the processing of the queue
            var deltas = await _remote.GetDeltasAsync(_metadata.DeltaLink);
            _metadata.DeltaLink = deltas.DeltaLink;

            var filteredDeltas = new List<ItemDelta>();
            foreach (var delta in deltas)
            {
                //TODO: go through all deltas and determine what should be done with them
                if (delta.Deleted)
                {
                    filteredDeltas.Add(new ItemDelta
                    {
                        IsFolder = delta.ItemHandle.IsFolder,
                        Path = delta.ItemHandle.Path,
                        Type = ItemDelta.DeltaType.Deleted
                    });
                }
                else
                {
                    var itemMetadata = _metadata.GetItemMetadataById(delta.ItemHandle.Id);
                    if (itemMetadata == null)
                    {
                        //new item
                        filteredDeltas.Add(new ItemDelta
                        {
                            IsFolder = delta.ItemHandle.IsFolder,
                            Path = delta.ItemHandle.Path,
                            Type = ItemDelta.DeltaType.ModifiedOrCreated
                        });

                        //new item, so add it
                        _metadata.AddItemMetadata(delta.ItemHandle);
                    }
                    else
                    {
                        //existing item ...
                        if (itemMetadata.Name == delta.ItemHandle.Name)
                        {
                            //... with the same name ...
                            if (itemMetadata.RemoteLastModified == delta.ItemHandle.LastModified)
                            {
                                //... with the same last modified so do nothing
                            }
                            else
                            {
                                //TODO: if an item has been modified in remote and local, but the network connection has problems and in between so local is waiting to push remote changes and remote is waiting to pull changes, but the local changes aren't the ones that are wanted.  In this case, the local item should be renamed, removed from the queue, and requested as a regular upload
                                //TODO: how do we tell the local to rename the file? -- through "status" and "error message"
                            }
                        }
                        else
                        {
                            //... with a different name so rename it
                            filteredDeltas.Add(new ItemDelta
                            {
                                IsFolder = delta.ItemHandle.IsFolder,
                                Path = delta.ItemHandle.Path,
                                OldPath = itemMetadata.Path,
                                Type = ItemDelta.DeltaType.Renamed
                            });
                        }

                    }
                }
            }

            return filteredDeltas;
        }
        #endregion

        /*
         * TODO: support remote change events
         */
    }
}
