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
        #region Extra Datas
        public class RequestUploadExtraData : IFileStoreRequestExtraData
        {
            public RequestUploadExtraData(Stream streamFrom)
            {
                StreamFrom = streamFrom;
            }
            public Stream StreamFrom { get; }
        }
        public class RequestDownloadExtraData : IFileStoreRequestExtraData
        {
            public RequestDownloadExtraData(Stream streamTo)
            {
                StreamTo = streamTo;
            }
            public Stream StreamTo { get; }
        }
        #endregion

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
        private ConcurrentQueue<FileStoreRequest> _requests = new ConcurrentQueue<FileStoreRequest>();
        /// <summary>
        /// Requests that failed for reasons other than network connection
        /// </summary>
        private ConcurrentDictionary<int, FileStoreRequest> _limboRequests = new ConcurrentDictionary<int, FileStoreRequest>();
        private ConcurrentDictionary<int, object> _cancelledRequests = new ConcurrentDictionary<int, object>();
        private int _requestId;//TODO: should this be volatile
        #endregion

        public BufferedRemoteFileStoreInterface(IRemoteFileStoreConnection remote)
        {
            _remote = remote;
        }

        #region Private Methods
        private void SetStatusFromHttpResponse(FileStoreRequest request, HttpResponseMessage result)
        {
            if (result == null)
            {
                //didn't fail, but no network connection...
                var oldStatus = request.Status;
                request.Status = FileStoreRequest.RequestStatus.Pending;

                //if we started something, make sure we let the listeners know that we stopped!
                if(oldStatus != FileStoreRequest.RequestStatus.Pending)
                    InvokeStatusChanged(request);
            }
            else
            {
                if (result.IsSuccessStatusCode)
                {
                    //success!
                    request.Status = FileStoreRequest.RequestStatus.Success;
                }
                else
                {
                    //failure...
                    request.Status = FileStoreRequest.RequestStatus.Failure;
                    request.ErrorMessage =
                        $"Http Error \"{result.StatusCode}\" because: \"{result.ReasonPhrase}\"";
                }
                InvokeStatusChanged(request);
            }
        }
        private void InvokeStatusChanged(FileStoreRequest request)
        {
            OnRequestStatusChanged?.Invoke(this, new RequestStatusChangedEventArgs(request));
        }
        private void FailRequest(FileStoreRequest request, string errorMessage)
        {
            request.Status = FileStoreRequest.RequestStatus.Failure;
            request.ErrorMessage = errorMessage;
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);
        }
        private void RequestAwaitUser(FileStoreRequest request)
        {
            request.Status = FileStoreRequest.RequestStatus.WaitForUser;
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);
        }
        private int EnqueueRequest(FileStoreRequest request)
        {
            _requests.Enqueue(request);
            return request.RequestId;
        }

        private async Task<bool> UploadFileWithProgressAsync(string parentId, bool isNew, Stream readFrom, FileStoreRequest request)
        {
            HttpResult<IRemoteItemHandle> uploadResult; 
            //wrap the local stream to track read progress of local file
            using (readFrom)
            {
                var wrapper = new ProgressableStreamWrapper(readFrom);
                wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                    new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));

                //request is in progress now
                request.Status = FileStoreRequest.RequestStatus.InProgress;
                InvokeStatusChanged(request);

                //make the upload request
                uploadResult = await _remote.UploadFileByIdAsync(parentId, PathUtils.GetItemName(request.Path), wrapper);
            }

            //set the request status and error
            SetStatusFromHttpResponse(request, uploadResult.HttpMessage);

            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if(success)
                _metadata.AddOrUpdateItemMetadata(uploadResult.Value);

            return success;
        }
        private async Task<bool> DownloadFileWithProgressAsync(string itemId, Stream writeTo, FileStoreRequest request)
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
            

            //get the write stream
            //var writeStream = item.GetWritableStream();
            //if (writeStream != null)
            //{
                //write stream isn't null, so start trying to stream item
                using (var stream = getDataRequest.Value)
                {
                    //request is in progress now
                    request.Status = FileStoreRequest.RequestStatus.InProgress;
                    InvokeStatusChanged(request);

                    //wrap the read stream around the progress notifier
                    var wrapper = new ProgressableStreamWrapper(stream, remoteItem.Size);
                    wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                        new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));
                    
                    using (writeTo)
                    {
                        await wrapper.CopyToAsync(writeTo);
                    }
                    //successfully completed stream
                    request.Status = FileStoreRequest.RequestStatus.Success;
                    InvokeStatusChanged(request);
                }
            //}
            //else
            //{
                //write stream is null, which means the file cannot be opened at this time, so put in limbo
            //    RequestAwaitUser(request);
            //}
            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddOrUpdateItemMetadata(getHandleRequest.Value);

            return success;
        }
        private async Task<bool> RenameItemAsync(string itemId, string newName, FileStoreRequest request)
        {
            var response = await _remote.RenameItemByIdAsync(itemId, newName);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> MoveItemAsync(string itemId, string newParentId, FileStoreRequest request)
        {
            var response = await _remote.MoveItemByIdAsync(itemId, newParentId);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> CreateItemAsync(string parentId, string name, FileStoreRequest request)
        {
            var response = await _remote.CreateFolderByIdAsync(parentId, name);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> DeleteItemAsync(string itemId, FileStoreRequest request)
        {
            var response = await _remote.DeleteItemByIdAsync(itemId);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == FileStoreRequest.RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.RemoveItemMetadataById(itemId);

            return success;
        }

        private async Task ProcessQueue(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_requests.TryPeek(out FileStoreRequest request))
                {
                    //was this request cancelled?
                    if (_cancelledRequests.ContainsKey(request.RequestId))
                    {
                        //if so, dequeue it and move on
                        _requests.TryDequeue(out FileStoreRequest result);
                        _cancelledRequests.TryRemove(request.RequestId, out object alwaysNull);
                        request.Status = FileStoreRequest.RequestStatus.Cancelled;
                        InvokeStatusChanged(request);
                        continue;
                    }

                    bool dequeue = false;
                    var itemMetadata = _metadata.GetItemMetadata(request.Path);
                    //since only this class has access to the queue, we have good confidence that 
                    //the appropriate extra data will be with the appropriate request type
                    switch (request.Type)
                    {
                        case FileStoreRequest.RequestType.Write: //(upload)
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
                                            data.StreamFrom, request);
                                    }
                                }
                                else
                                {
                                    //the item already exists, so upload a new version
                                    dequeue = await UploadFileWithProgressAsync(itemMetadata.ParentId, false,
                                        data.StreamFrom, request);
                                }

                            }
                            else
                            {
                                Debug.WriteLine("Upload request was called without appropriate extra data");
                            }
                        }
                            break;
                        case FileStoreRequest.RequestType.Read: // (Download)
                        {
                            var data = request.ExtraData as RequestDownloadExtraData;
                            if (data != null)
                            {
                                //try to get item metadata
                                if (itemMetadata != null)
                                {
                                    //item exists already, so download it
                                    dequeue = await DownloadFileWithProgressAsync(itemMetadata.Id, data.StreamTo, request);
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
                        case FileStoreRequest.RequestType.Rename:
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
                        case FileStoreRequest.RequestType.Move:
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
                        case FileStoreRequest.RequestType.Create:
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
                        case FileStoreRequest.RequestType.Delete:
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
                        _requests.TryDequeue(out FileStoreRequest result);
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
        public string MetadataCache
        {
            get => _metadata.Serialize();
            set => _metadata.Deserialize(value);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Upload a file to the remote
        /// </summary>
        /// <param name="path">the path of the item to upload</param>
        /// <param name="streamFrom">the stream to read data from</param>
        /// <returns>the request id</returns>
        /// <remarks><see cref="streamFrom"/> gets disposed in this method</remarks>
        public int RequestUpload(string path, Stream streamFrom)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Write, path,
                new RequestUploadExtraData(streamFrom)));
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
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Read, path,
                new RequestDownloadExtraData(streamTo)));
        }
        /// <summary>
        /// Create a remote folder
        /// </summary>
        /// <param name="path">the path of the remote folder to create</param>
        /// <returns>the request id</returns>
        public int RequestFolderCreate(string path)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path, null));
        }
        /// <summary>
        /// Deletes a remote item and its children
        /// </summary>
        /// <param name="path">the path of the item to delete</param>
        /// <returns>the request id</returns>
        public int RequestDelete(string path)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Delete, path, null));
        }
        /// <summary>
        /// Renames a remote item
        /// </summary>
        /// <param name="path">the path of the item to rename</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public int RequestRename(string path, string newName)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Rename, path,
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
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Move, path,
                new RequestMoveExtraData(newParentPath)));
        }

        public bool TryGetRequest(int requestId, out FileStoreRequest request)
        {
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                //no queue items... let's check limbo
                if(_limboRequests.TryGetValue(requestId, out FileStoreRequest limboReq))
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
                _limboRequests.TryRemove(requestId, out FileStoreRequest value);
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
        public IEnumerable<FileStoreRequest> EnumerateActiveRequests()
        {
            //this creates a copy
            return new List<FileStoreRequest>(_requests);
        }
        /// <summary>
        /// When an existing request's progress changes
        /// </summary>
        public EventDelegates.RemoteRequestProgressChangedHandler OnRequestProgressChanged;
        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="FileStoreRequest.RequestStatus.Success"/>, there
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
                            Type = ItemDelta.DeltaType.Created
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
                                    Type = ItemDelta.DeltaType.Modified
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
