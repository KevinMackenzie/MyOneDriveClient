﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;
using LocalCloudStorage.Threading;
using Utils;

namespace LocalCloudStorage
{
    /// <summary>
    /// This is responsible for buffering requests to the server, recognizing when they fail, and notifying of conflicts while letting unaffected requests pass through
    /// This is also responsible for maintaining the local cache of the remote file metadata
    /// </summary>
    public class BufferedRemoteFileStoreInterface : FileStoreInterface, IRemoteFileStoreInterface
    {

        /// <summary>
        /// An item handle for deleted items, because they don't have a parent reference path
        /// </summary>
        private class DeletedItemHandle : IItemHandle
        {
            private IItemHandle _itemHandle;
            public DeletedItemHandle(IItemHandle itemHandle, string path, DateTime lastModified)
            {
                _itemHandle = itemHandle;
                LastModified = lastModified;
                Path = path;
            }

            /// <inheritdoc />
            public bool IsFolder => _itemHandle.IsFolder;
            /// <inheritdoc />
            public string Name => _itemHandle.Name;
            /// <inheritdoc />
            public long Size => _itemHandle.Size;
            /// <inheritdoc />
            public async Task<string> GetSha1HashAsync()
            {
                return await _itemHandle.GetSha1HashAsync();
            }
            /// <inheritdoc />
            public async Task<string> GetSha1HashAsync(CancellationToken ct)
            {
                return await _itemHandle.GetSha1HashAsync(ct);
            }
            /// <inheritdoc />
            public DateTime LastModified { get; }
            /// <inheritdoc />
            public Task<Stream> GetFileDataAsync(CancellationToken ct)
            {
                return _itemHandle.GetFileDataAsync(ct);
            }

            public string Path { get; }
        }

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

        #region Private Fields
        private IRemoteFileStoreConnection _remote;
        private RemoteItemMetadataCache _metadata = new RemoteItemMetadataCache();
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
                InvokeStatusChanged(request, RequestStatus.Pending);
            }
            else
            {
                if (result.IsSuccessStatusCode)
                {
                    //success!
                    InvokeStatusChanged(request, RequestStatus.Success);
                }
                else
                {
                    //failure...
                    FailRequest(request, $"Http Error \"{result.StatusCode}\" because: \"{result.ReasonPhrase}\"");
                }
            }
        }

        private async Task<bool> UploadFileWithProgressAsync(string parentId, bool isNew, Stream readFrom, FileStoreRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            HttpResult<IRemoteItemHandle> uploadResult = null; 
            //wrap the local stream to track read progress of local file
            using (readFrom)
            {
                var wrapper = new ProgressableStreamWrapper(readFrom);
                wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                    new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));

                //request is in progress now
                request.Status = RequestStatus.InProgress;
                InvokeStatusChanged(request);

                try
                {
                    while (uploadResult == null)
                    {
                        try
                        {
                            //make the upload request
                            uploadResult =
                                await _remote.UploadFileByIdAsync(parentId, PathUtils.GetItemName(request.Path),
                                    wrapper, ct);
                        }
                        catch (TaskCanceledException)
                        {
                            //the request was cancelled for real, so don't send again...
                            if (ct.IsCancellationRequested)
                                throw;

                            //the request timed out or something...
                            Debug.WriteLine("Request Timed Out, trying again...");
                        }
                    }
                }
                catch (Exception e)
                {
                    readFrom.Dispose();
                    Utils.LogException(e);
                    Debug.WriteLine("Cancelling ...");

                    //convert this to a task cancel exception
                    throw new ConvertedTaskCancelledException(e);
                }
            }

            //set the request status and error
            SetStatusFromHttpResponse(request, uploadResult.HttpMessage);

            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if(success)
                _metadata.AddOrUpdateItemMetadata(uploadResult.Value);

            return success;
        }
        private async Task<bool> DownloadFileWithProgressAsync(string itemId, Stream writeTo, FileStoreRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var getHandleRequest = await _remote.GetItemHandleByIdAsync(itemId, ct);
            if (!getHandleRequest.Success)
            {
                SetStatusFromHttpResponse(request, getHandleRequest.HttpMessage);
                return false;
            }

            //successfully got item
            var remoteItem = getHandleRequest.Value;

            //try to get the item
            var getDataRequest = await remoteItem.TryGetFileDataAsync(ct);
            if (!getDataRequest.Success)
            {
                //if we failed, then that's what the request should say
                SetStatusFromHttpResponse(request, getDataRequest.HttpMessage);
                return false;
            }
            
            
            //Start trying to stream item
            using (var stream = getDataRequest.Value)
            {
                //request is in progress now
                request.Status = RequestStatus.InProgress;
                InvokeStatusChanged(request);

                //wrap the read stream around the progress notifier
                var wrapper = new ProgressableStreamWrapper(stream, remoteItem.Size);
                wrapper.OnReadProgressChanged += (sender, args) => OnRequestProgressChanged?.Invoke(this,
                    new RemoteRequestProgressChangedEventArgs(args.Complete, args.Total, request.RequestId));
                    
                using (writeTo)
                {
                    try
                    {
                        await wrapper.CopyToStreamAsync(writeTo, ct);
                    }
                    catch (Exception e)
                    {
                        //When will this happen?  How to handle, each case will be different
                        writeTo.Dispose();
                        stream.Dispose();

                        Utils.LogException(e);
                        Debug.WriteLine("Cancelling ...");

                        //convert this to a task cancel exception
                        throw new ConvertedTaskCancelledException(e);
                    }
                }
                //successfully completed stream
                request.Status = RequestStatus.Success;
                InvokeStatusChanged(request);
            }
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddOrUpdateItemMetadata(getHandleRequest.Value);

            return success;
        }
        private async Task<bool> RenameItemAsync(string itemId, string newName, FileStoreRequest request, CancellationToken ct)
        {
            var response = await _remote.RenameItemByIdAsync(itemId, newName, ct);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> MoveItemAsync(string itemId, string newParentId, FileStoreRequest request, CancellationToken ct)
        {
            var response = await _remote.MoveItemByIdAsync(itemId, newParentId, ct);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.UpdateItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> CreateItemAsync(string parentId, string name, FileStoreRequest request, CancellationToken ct)
        {
            var response = await _remote.CreateFolderByIdAsync(parentId, name, ct);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.AddItemMetadata(response.Value);

            return success;
        }
        private async Task<bool> DeleteItemAsync(string itemId, FileStoreRequest request, CancellationToken ct)
        {
            var response = await _remote.DeleteItemByIdAsync(itemId, ct);
            SetStatusFromHttpResponse(request, response.HttpMessage);
            var success = request.Status == RequestStatus.Success;

            //if we were successful, update the metadata
            if (success)
                _metadata.RemoveItemMetadataById(itemId);

            return success;
        }

        protected override async Task<bool> ProcessQueueItemAsync(FileStoreRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            //TODO: i'd like to not do this EVERY time
            await _remote.LogUserInAsync();

            var itemMetadata = _metadata.GetItemMetadata(request.Path);
            //since only this class has access to the queue, we have good confidence that 
            //the appropriate extra data will be with the appropriate request type
            switch (request.Type)
            {
                case RequestType.Write: //(upload)
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
                                return false;
                            }
                            else
                            {
                                //We found the parent, so upload this child to it
                                return await UploadFileWithProgressAsync(parentMetadata.Id, true,
                                    data.StreamFrom, request, ct);
                            }
                        }
                        else
                        {
                            //the item already exists, so upload a new version
                            return await UploadFileWithProgressAsync(itemMetadata.ParentId, false,
                                data.StreamFrom, request, ct);
                        }

                    }
                    else
                    {
                        Debug.WriteLine("Upload request was called without appropriate extra data");
                        return false;
                    }
                }
                    break;
                case RequestType.Read: // (Download)
                {
                    var data = request.ExtraData as RequestDownloadExtraData;
                    if (data != null)
                    {
                        //try to get item metadata
                        if (itemMetadata != null)
                        {
                            //item exists already, so download it
                            return await DownloadFileWithProgressAsync(itemMetadata.Id, data.StreamTo, request, ct);
                        }
                        else
                        {
                            //item doesn't exist
                            FailRequest(request, $"Could not find file \"{request.Path}\" to download");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.Write("Download request was called without approprate extra data");
                        return false;
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
                            return false;
                        }
                        else
                        {
                            //rename the file
                            return await RenameItemAsync(itemMetadata.Id, data.NewName, request, ct);
                        }
                    }
                    else
                    {
                        Debug.Write("Rename request was called without approprate extra data");
                        return false;
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
                            return false;
                        }
                        else
                        {
                            var parentMetadata = _metadata.GetItemMetadata(data.NewParentPath);
                            if (parentMetadata == null)
                            {
                                //new location doesn't exist TODO: should this be an error or should we create the new location?
                                FailRequest(request, $"Could not find new location \"{data.NewParentPath}\" for \"{request.Path}\" to move to");
                                return false;
                            }
                            else
                            {
                                //item exists, so move it
                                return await MoveItemAsync(itemMetadata.Id, parentMetadata.Id, request, ct);
                            }
                        }
                    }
                    else
                    {
                        Debug.Write("Move request was called without approprate extra data");
                        return false;
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
                        return false;
                    }
                    else
                    {
                        return await CreateItemAsync(parentMetadata.Id, PathUtils.GetItemName(request.Path), request, ct);
                    }
                }
                    break;
                case RequestType.Delete:
                {
                    if (itemMetadata == null)
                    {
                        //item doesn't exist.  Is this an issue?
                        FailRequest(request, $"Could not delete \"{request.Path}\" because it does not exist!");
                        return false;
                    }
                    else
                    {
                        return await DeleteItemAsync(itemMetadata.Id, request, ct);
                    }
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        protected override async Task PreQueue(CancellationToken ct)
        {
        }
        protected override async Task PostQueue(CancellationToken ct)
        {}
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
        public void RequestUpload(string path, Stream streamFrom)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Write, path,
                new RequestUploadExtraData(streamFrom)));
        }
        /// <summary>
        /// Download a file from the remote
        /// </summary>
        /// <param name="path">the path of the remote item</param>
        /// <param name="streamTo">where to stream the download to</param>
        /// <returns>the request id</returns>
        /// <remarks><see cref="streamTo"/> gets disposed in this method</remarks>
        public void RequestFileDownload(string path, Stream streamTo)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Read, path,
                new RequestDownloadExtraData(streamTo)));
        }
        /// <summary>
        /// Create a remote folder
        /// </summary>
        /// <param name="path">the path of the remote folder to create</param>
        /// <returns>the request id</returns>
        public void RequestFolderCreate(string path)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Create, path, null));
        }
        /// <summary>
        /// Deletes a remote item and its children
        /// </summary>
        /// <param name="path">the path of the item to delete</param>
        /// <returns>the request id</returns>
        public void RequestDelete(string path)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Delete, path, null));
        }
        /// <summary>
        /// Renames a remote item
        /// </summary>
        /// <param name="path">the path of the item to rename</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public void RequestRename(string path, string newName)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Rename, path,
                new RequestRenameExtraData(newName)));
        }
        /// <summary>
        /// Moves a remote item
        /// </summary>
        /// <param name="path">the previous path of the item</param>
        /// <param name="newParentPath">the folder the item is moved to</param>
        /// <returns>the request id</returns>
        public void RequestMove(string path, string newParentPath)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, RequestType.Move, path,
                new RequestMoveExtraData(newParentPath)));
        }


        public async Task RequestUploadImmediateAsync(string path, Stream streamFrom, CancellationToken ct)
        {
            await ProcessRequestAsync(new FileStoreRequest(ref _requestId, RequestType.Write, path,
                new RequestUploadExtraData(streamFrom)), ct);
        }
        public async Task RequestFileDownloadImmediateAsync(string path, Stream streamTo, CancellationToken ct)
        {
            await ProcessRequestAsync(new FileStoreRequest(ref _requestId, RequestType.Read, path,
                new RequestDownloadExtraData(streamTo)), ct);
        }
        public async Task RequestDeleteItemImmediateAsync(string path, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public async Task RequestRenameItemImmediateAsync(string path, string newName, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Requests the remote deltas since the previous request
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<IItemDelta>> RequestDeltasAsync(CancellationToken ct)
        {
            await _remote.LogUserInAsync();

            //TODO: this should pause the processing of the queue
            var deltas = await _remote.GetDeltasAsync(_metadata.DeltaLink, ct);
            _metadata.DeltaLink = deltas.NextRequestData;

            var filteredDeltas = new List<IItemDelta>();
            foreach (var delta in deltas)
            {
                var itemMetadata = _metadata.GetItemMetadataById(delta.ItemHandle.Id);
                if (delta.Deleted)
                {
                    if (itemMetadata != null)
                    {
                        filteredDeltas.Add(new ItemDelta
                        {
                            //use the metadata path/last modified, because that information isn't known to us in the delta
                            Handle = new DeletedItemHandle(delta.ItemHandle, itemMetadata.Path, itemMetadata.LastModified),
                            Type = DeltaType.Deleted
                        });

                        //We weren't deleting this metadata before...
                        _metadata.RemoveItemMetadataById(itemMetadata.Id);
                    }
                    else
                    {
                        //we're trying to delete an item that we don't even know about...
                        Debug.WriteLine($"Attempt to delete item not in remote metadata, \"{delta.ItemHandle.Id}, ({delta.ItemHandle.Path})\"");
                    }
                }
                else
                {
                    if (itemMetadata == null)
                    {
                        //new item
                        filteredDeltas.Add(new ItemDelta
                        {
                            Handle = delta.ItemHandle,
                            Type = DeltaType.Created
                        });

                        if (delta.ItemHandle.Path == "/")
                        {
                            //root item...
                            _metadata.AddOrUpdateItemMetadata(new ItemMetadataCache.ItemMetadata()
                            {
                                Id = delta.ItemHandle.Id,
                                IsFolder = delta.ItemHandle.IsFolder,
                                LastModified = delta.ItemHandle.LastModified,
                                Name = "root",
                                ParentId = "",
                                Sha1 = ""
                            });
                        }
                        else
                        {
                            //new item, so add it
                            _metadata.AddItemMetadata(delta.ItemHandle);
                        }
                    }
                    else
                    {
                        //existing item ...
                        if (itemMetadata.Name == delta.ItemHandle.Name)
                        {
                            //... with the same last modified ...
                            if (itemMetadata.ParentId == "") continue;

                            //... and not the root item ...
                            if (itemMetadata.ParentId == delta.ItemHandle.ParentId)
                            {
                                //... with the same parent so do nothing
                                //      This will happen when the app gets closed partway through the
                                //          delta queue
                                Debug.WriteLine("Delta found with same name, location, and timestamp");
                            }
                            else
                            {
                                //... with a different parent so move it
                                filteredDeltas.Add(new ItemDelta
                                {
                                    Handle = delta.ItemHandle,
                                    OldPath = itemMetadata.Path,
                                    Type = DeltaType.Moved
                                });

                                //and update the metadata
                                itemMetadata.ParentId = delta.ItemHandle.ParentId;
                            }

                            //in item could be modified AND moved... so make sure we account for both

                            //... with the same name ...
                            if (itemMetadata.LastModified != delta.ItemHandle.LastModified)
                            {
                                //... with a different last modified so download it
                                filteredDeltas.Add(new ItemDelta
                                {
                                    Handle = delta.ItemHandle,
                                    Type = DeltaType.Modified
                                });

                                //and update the metadata
                                itemMetadata.LastModified = delta.ItemHandle.LastModified;

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
                                Type = DeltaType.Renamed
                            });

                            //and update the metadata
                            itemMetadata.Name = delta.ItemHandle.Name;
                            itemMetadata.LastModified = delta.ItemHandle.LastModified;
                        }

                    }
                }
            }

            return filteredDeltas;
        }

        public async Task<ICollection<StaticItemHandle>> GetPathListingAsync(CancellationToken ct)
        {
            await _remote.LogUserInAsync();

            var deltas = await _remote.GetDeltasAsync("", ct);
            return (from delta in deltas select new StaticItemHandle(delta.ItemHandle)).ToList();
        }
        #endregion

        /// <summary>
        /// When an existing request's progress changes
        /// </summary>
        public event EventDelegates.RemoteRequestProgressChangedHandler OnRequestProgressChanged;

        /*
         * TODO: support remote change events
         */
        /// <inheritdoc />
        public void Dispose()
        {
            _remote?.Dispose();
        }
    }
}
