using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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
            Failure,
            /// <summary>
            /// Request was cancelled
            /// </summary>
            Cancelled
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

        public class RemoteFileStoreRequest
        {
            private int _requestId;
            public RemoteFileStoreRequest(ref int id, RequestType type, string path, IRemoteFileStoreRequestExtraData extraData)
            {
                _requestId = Interlocked.Increment(ref id);

                Path = path;
                Status = RequestStatus.Pending;
                ErrorMessage = null;
                Type = type;
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

            public IRemoteFileStoreRequestExtraData ExtraData { get; }
        }

        public class RemoteItemDelta : ItemDelta
        {
            public RemoteItemDelta(int requestId)
            {
                RequestId = requestId;
            }
            /// <summary>
            /// The request Id of the item if <see cref="Type"/> is <see cref="ItemDelta.DeltaType.ModifiedOrCreated"/>
            /// </summary>
            public int RequestId { get; }
        }

        #region Private Fields
        private IRemoteFileStoreConnection _remote;
        private RemoteItemMetadataCache _metadata = new RemoteItemMetadataCache();
        private ConcurrentQueue<RemoteFileStoreRequest> _requests = new ConcurrentQueue<RemoteFileStoreRequest>();
        /// <summary>
        /// Requests that failed for reasons other than network connection
        /// </summary>
        private ConcurrentDictionary<int, RemoteFileStoreRequest> _limboRequests = new ConcurrentDictionary<int, RemoteFileStoreRequest>();
        private ConcurrentDictionary<int, object> _cancelledRequests = new ConcurrentDictionary<int, object>();
        private int _requestId;//TODO: should this be volatile
        #endregion

        public BufferedRemoteFileStoreInterface(IRemoteFileStoreConnection remote, string metadataCache)
        {
            _remote = remote;
            _metadata.Deserialize(metadataCache);
        }

        #region Private Methods
        private void SetStatusFromHttpResponse(RemoteFileStoreRequest request, HttpResponseMessage result)
        {
            if (result == null)
            {
                //didn't fail, but no network connection...
                var oldStatus = request.Status;
                request.Status = RequestStatus.Pending;

                //if we started something, make sure we let the listeners know that we stopped!
                if(oldStatus != RequestStatus.Pending)
                    InvokeStatusChanged(request);
            }
            else
            {
                if (result.IsSuccessStatusCode)
                {
                    //success!
                    request.Status = RequestStatus.Success;
                }
                else
                {
                    //failure...
                    request.Status = RequestStatus.Failure;
                    request.ErrorMessage =
                        $"Http Error \"{result.StatusCode}\" because: \"{result.ReasonPhrase}\"";
                }
                InvokeStatusChanged(request);
            }
        }
        private void InvokeStatusChanged(RemoteFileStoreRequest request)
        {
            OnRequestStatusChanged?.Invoke(this, new RequestStatusChangedEventArgs(request.RequestId, request.Status));
        }
        private void FailRequest(RemoteFileStoreRequest request, string errorMessage)
        {
            request.Status = RequestStatus.Failure;
            request.ErrorMessage = errorMessage;
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);
        }
        private int EnqueueRequest(RemoteFileStoreRequest request)
        {
            _requests.Enqueue(request);
            return request.RequestId;
        }

        private async Task<bool> UploadFileWithProgressAsync(string parentId, bool isNew, IItemHandle item, RemoteFileStoreRequest request)
        {
            HttpResult<IRemoteItemHandle> uploadResult; 
            //wrap the local stream to track read progress of local file
            using (var stream = await item.GetFileDataAsync())
            {
                var wrapper = new ProgressableStreamWrapper(stream);
                wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                    new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));

                //request is in progress now
                request.Status = RequestStatus.InProgress;
                InvokeStatusChanged(request);

                //make the upload request
                uploadResult = await _remote.UploadFileByIdAsync(parentId, item.Name, wrapper);
            }

            //set the request status and error
            SetStatusFromHttpResponse(request, uploadResult.HttpMessage);

            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if(success)
                _metadata.AddOrUpdateItemMetadata(uploadResult.Value);

            return success;
        }
        private async Task<bool> DownloadFileWithProgressAsync(string itemId, ILocalItemHandle item, RemoteFileStoreRequest request)
        {
            var getHandleRequest = await _remote.GetItemHandleByIdAsync(itemId);
            if (!getHandleRequest.Success)
            {
                SetStatusFromHttpResponse(request, getHandleRequest.HttpMessage);
                return false;
            }

            //successfully got item
            var remoteItem = getHandleRequest.Value;

            //try to get the item
            var getDataRequest = await remoteItem.TryGetFileDataAsync();
            if (!getDataRequest.Success)
            {
                //if we failed, then that's what the request should say
                SetStatusFromHttpResponse(request, getDataRequest.HttpMessage);
                return false;
            }
            
            //request is in progress now
            request.Status = RequestStatus.InProgress;
            InvokeStatusChanged(request);

            //start trying to stream item
            using (var stream =  getDataRequest.Value)
            {
                var wrapper = new ProgressableStreamWrapper(stream, remoteItem.Size);
                wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                    new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));

                using (var writeStream = item.GetWritableStream())
                {
                    await wrapper.CopyToAsync(writeStream);
                }
            }
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddOrUpdateItemMetadata(getHandleRequest.Value);

            return success;
        }
        private async Task<bool> RenameItemAsync(string itemId, string newName, RemoteFileStoreRequest request)
        {
            var response = await _remote.RenameItemByIdAsync(itemId, newName);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> MoveItemAsync(string itemId, string newParentId, RemoteFileStoreRequest request)
        {
            var response = await _remote.MoveItemByIdAsync(itemId, newParentId);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> CreateItemAsync(string parentId, string name, RemoteFileStoreRequest request)
        {
            var response = await _remote.CreateFolderByIdAsync(parentId, name);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> DeleteItemAsync(string itemId, RemoteFileStoreRequest request)
        {
            var response = await _remote.DeleteItemByIdAsync(itemId);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.RemoveItemMetadataById(itemId);

            return success;
        }

        private async Task ProcessQueue(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_requests.TryPeek(out RemoteFileStoreRequest request))
                {
                    //was this request cancelled?
                    if (_cancelledRequests.ContainsKey(request.RequestId))
                    {
                        //if so, dequeue it and move on
                        _requests.TryDequeue(out RemoteFileStoreRequest result);
                        _cancelledRequests.TryRemove(request.RequestId, out object alwaysNull);
                        request.Status = RequestStatus.Cancelled;
                        InvokeStatusChanged(request);
                        continue;
                    }

                    bool dequeue = false;
                    var itemMetadata = _metadata.GetItemMetadata(request.Path);
                    //since only this class has access to the queue, we have good confidence that 
                    //the appropriate extra data will be with the appropriate request type
                    switch (request.Type)
                    {
                        case RequestType.Upload:
                        {
                            var data = request.ExtraData as RequestUploadExtraData;
                            if (data != null)
                            {
                                //try to get the item metadata
                                if (itemMetadata == null)
                                {
                                    //this is a new item, so get it's parent metadata
                                    var parentMetadata = _metadata.GetParentItemMetadata(request.Path);

                                    if (parentMetadata == null)
                                    {
                                        //no parent item, so we have issues. TODO: This could be resolved by creating the directory path
                                        FailRequest(request, $"Could not find parent of file \"{request.Path}\" to upload");

                                        dequeue = true;
                                    }
                                    else
                                    {
                                        //We found the parent, so upload this child to it
                                        dequeue = await UploadFileWithProgressAsync(parentMetadata.Id, true,
                                            data.ItemHandle, request);
                                    }
                                }
                                else
                                {
                                    //the item already exists, so upload a new version
                                    dequeue = await UploadFileWithProgressAsync(itemMetadata.ParentId, false,
                                        data.ItemHandle, request);
                                }

                            }
                            else
                            {
                                Debug.WriteLine("Upload request was called without appropriate extra data");
                            }
                        }
                            break;
                        case RequestType.Download:
                        {
                            var data = request.ExtraData as RequestDownloadExtraData;
                            if (data != null)
                            {
                                //try to get item metadata
                                if (itemMetadata != null)
                                {
                                    //item exists already, so download it
                                    dequeue = await DownloadFileWithProgressAsync(itemMetadata.Id, data.ItemHandle, request);
                                }
                                else
                                {
                                    //item doesn't exist
                                    FailRequest(request, $"Could not find file \"{request.Path}\" to download");

                                    dequeue = true;
                                }
                            }
                            else
                            {
                                Debug.Write("Download request was called without approprate extra data");
                            }
                        }
                            break;
                        case RequestType.Rename:
                        {
                            var data = request.ExtraData as RequestRenameExtraData;
                            if (data != null)
                            {
                                if (itemMetadata == null)
                                {
                                    //item doesn't exist
                                    FailRequest(request, $"Could not find file \"{request.Path}\" to rename");

                                    dequeue = true;
                                }
                                else
                                {
                                    //rename the file
                                    dequeue = await RenameItemAsync(itemMetadata.Id, data.NewName, request);
                                }
                            }
                            else
                            {
                                Debug.Write("Rename request was called without approprate extra data");
                            }
                        }
                            break;
                        case RequestType.Move:
                        {
                            var data = request.ExtraData as RequestMoveExtraData;
                            if (data != null)
                            {
                                if (itemMetadata == null)
                                {
                                    //item doesn't exist
                                    FailRequest(request, $"Could not find file \"{request.Path}\" to move");

                                    dequeue = true;
                                }
                                else
                                {
                                    var parentMetadata = _metadata.GetItemMetadata(data.NewParentPath);
                                    if (parentMetadata == null)
                                    {
                                        //new location doesn't exist TODO: should this be an error or should we create the new location?
                                        FailRequest(request, $"Could not find new location \"{data.NewParentPath}\" for \"{request.Path}\" to move to");

                                        dequeue = true;
                                    }
                                    else
                                    {
                                        //item exists, so move it
                                        dequeue = await MoveItemAsync(itemMetadata.Id, parentMetadata.Id, request);
                                    }
                                }
                            }
                            else
                            {
                                Debug.Write("Move request was called without approprate extra data");
                            }
                        }
                            break;
                        case RequestType.Create:
                        {
                            var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                            if (parentMetadata == null)
                            {
                                //new location doesn't exist TODO: should this be an error or should we create the new location?
                                FailRequest(request, $"Could not create \"{request.Path}\" because parent location doesn't exist");

                                dequeue = true;
                            }
                            else
                            {
                                dequeue = await CreateItemAsync(parentMetadata.Id, PathUtils.GetItemName(request.Path),
                                    request);
                            }
                        }
                            break;
                        case RequestType.Delete:
                        {
                            if (itemMetadata == null)
                            {
                                //item doesn't exist.  Is this an issue?
                                FailRequest(request, $"Could not delete \"{request.Path}\" because it does not exist!");
                                dequeue = true;
                            }
                            else
                            {
                                dequeue = await DeleteItemAsync(itemMetadata.Id, request);
                            }
                        }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //should we dequeue the item?
                    if (dequeue)
                    {
                        //yes
                        _requests.TryDequeue(out RemoteFileStoreRequest result);
                    }
                    else
                    {
                        //something failed, so we should wait a little bit before trying again
                        await Task.Delay(100, ct);
                    }
                }
                await Task.Delay(100, ct);
            }
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

        public bool TryGetRequest(int requestId, out RemoteFileStoreRequest request)
        {
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                //no queue items... let's check limbo
                if(_limboRequests.TryGetValue(requestId, out RemoteFileStoreRequest limboReq))
                {
                    //there is a limbo request
                    request = limboReq;
                    return true;
                }
                else
                {
                    //nope, no request found
                    request = null;
                    return false;
                }
            }
            else
            {
                //there is a queue item!
                request = reqs.First();
                return true;
            }
        }
        public void CancelRequest(int requestId)
        {
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                //no queue items... let's check limbo (but we don't care if it fails
                _limboRequests.TryRemove(requestId, out RemoteFileStoreRequest value);
            }
            else
            {
                //there is a queue item, so add it to the cancellation bag
                _cancelledRequests.TryAdd(requestId, null);
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
                if (delta.Deleted)
                {
                    filteredDeltas.Add(new ItemDelta
                    {
                        Handle = delta.ItemHandle,
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
                            Handle = delta.ItemHandle,
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
                            if (itemMetadata.LastModified == delta.ItemHandle.LastModified)
                            {
                                //... with the same last modified ...
                                if (itemMetadata.ParentId == delta.ItemHandle.ParentId)
                                {
                                    //... with the same parent so do nothing
                                    //TODO: when does this happen
                                    Debug.WriteLine("Delta found with same name, location, and timestamp");
                                }
                                else
                                {
                                    //... with a different parent so move it
                                    filteredDeltas.Add(new ItemDelta
                                    {
                                        Handle = delta.ItemHandle,
                                        OldPath = itemMetadata.Path,
                                        Type = ItemDelta.DeltaType.Moved
                                    });

                                    //and update the metadata
                                    _metadata.UpdateItemMetadata(delta.ItemHandle);
                                }
                            }
                            else
                            {
                                //... with a different last modified so download it
                                filteredDeltas.Add(new ItemDelta
                                {
                                    Handle = delta.ItemHandle,
                                    Type = ItemDelta.DeltaType.ModifiedOrCreated
                                });

                                //and update the metadata
                                _metadata.UpdateItemMetadata(delta.ItemHandle);

                                //TODO: if an item has been modified in remote and local, but the network connection has problems and in between so local is waiting to push remote changes and remote is waiting to pull changes, but the local changes aren't the ones that are wanted.  In this case, the local item should be renamed, removed from the queue, and requested as a regular upload
                                //TODO: how do we tell the local to rename the file? -- through "status" and "error message"
                            }
                        }
                        else
                        {
                            //... with a different name so rename it
                            filteredDeltas.Add(new ItemDelta
                            {
                                Handle = delta.ItemHandle,
                                OldPath = itemMetadata.Path,
                                Type = ItemDelta.DeltaType.Renamed
                            });

                            //and update the metadata
                            _metadata.UpdateItemMetadata(delta.ItemHandle);
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
