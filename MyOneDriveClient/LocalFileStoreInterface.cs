using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MyOneDriveClient
{
    public class LocalFileStoreInterface
    {
        public enum UserPrompts
        {
            KeepOverwriteOrRename,
            CloseApplication
        }

        private class DeletedItemHandle : IItemHandle
        {
            public DeletedItemHandle(ItemMetadataCache.ItemMetadata metadata)
            {
                IsFolder = metadata.IsFolder;
                Path = metadata.Path;
                Name = metadata.Name;
                LastModified = metadata.LastModified;
            }
            /// <inheritdoc />
            public bool IsFolder { get; }
            /// <inheritdoc />
            public string Path { get; }
            /// <inheritdoc />
            public string Name { get; }
            /// <inheritdoc />
            public long Size => throw new NotSupportedException();
            /// <inheritdoc />
            public string SHA1Hash => throw new NotSupportedException();
            /// <inheritdoc />
            public DateTime LastModified { get; }
            /// <inheritdoc />
            public Task<Stream> GetFileDataAsync()
            {
                throw new NotSupportedException();
            }
        }
        private class LocalRemoteItemHandle : IRemoteItemHandle
        {
            private IItemHandle _handle;
            private string _sha1;
            public LocalRemoteItemHandle(IItemHandle handle, string id, string parentId, string sha1Hash = null)
            {
                _handle = handle;
                Id = id;
                ParentId = parentId;
                _sha1 = sha1Hash;
            }


            /// <inheritdoc />
            public bool IsFolder => _handle.IsFolder;
            /// <inheritdoc />
            public string Path => _handle.Path;
            /// <inheritdoc />
            public string Name => _handle.Name;
            /// <inheritdoc />
            public long Size => _handle.Size;
            /// <inheritdoc />
            public string SHA1Hash
            {
                get
                {
                    if (_sha1 == null)
                    {
                        _sha1 = _handle.SHA1Hash;
                    }
                    return _sha1;
                }
            }
            /// <inheritdoc />
            public DateTime LastModified => _handle.LastModified;
            /// <inheritdoc />
            public Task<Stream> GetFileDataAsync()
            {
                throw new NotSupportedException();
            }
            /// <inheritdoc />
            public string Id { get; }
            /// <inheritdoc />
            public string ParentId { get; }
            /// <inheritdoc />
            public Task<HttpResult<Stream>> TryGetFileDataAsync()
            {
                throw new NotSupportedException();
            }
        }

        private class RequestCreateFolderExtraData : IFileStoreRequestExtraData
        {
            public RequestCreateFolderExtraData(DateTime lastModified)
            {
                LastModified = lastModified;
            }
            public DateTime LastModified { get; }
        }
        /// <summary>
        /// Request data for getting a writable stream
        /// </summary>
        private class RequestWritableStreamExtraData : IFileStoreRequestExtraData
        {
            public RequestWritableStreamExtraData(string sha1, DateTime lastModified)
            {
                Sha1 = sha1;
                LastModified = lastModified;
            }
            public string Sha1 { get; }
            public DateTime LastModified { get; }
        }
        /// <summary>
        /// Data returned from a request after getting the stream
        /// </summary>
        public class RequestStreamExtraData : IFileStoreRequestExtraData
        {
            public RequestStreamExtraData(Stream stream, bool writable)
            {
                Stream = stream;
                Writable = writable;
            }
            public bool Writable { get; }
            public Stream Stream { get; }
        }


        #region Private Fields
        private ILocalFileStore _local;
        private ConcurrentQueue<ItemDelta> _localDeltas = new ConcurrentQueue<ItemDelta>();
        private LocalItemMetadataCache _metadata = new LocalItemMetadataCache();
        private ConcurrentQueue<FileStoreRequest> _requests = new ConcurrentQueue<FileStoreRequest>();
        private ConcurrentDictionary<int, FileStoreRequest> _limboRequests = new ConcurrentDictionary<int, FileStoreRequest>();
        private ConcurrentDictionary<int, object> _cancelledRequests = new ConcurrentDictionary<int, object>();
        private int _requestId;//TODO: should this be volatile
        
        private CancellationTokenSource _processQueueCancellationTokenSource;
        private Task _processQueueTask;
        #endregion

        public LocalFileStoreInterface(ILocalFileStore local)
        {
            _local = local;
            _local.OnChanged += LocalOnChanged;
        }

        #region Private Methods
        private void RequestAwaitUser(FileStoreRequest request, UserPrompts prompt)
        {
            request.Status = FileStoreRequest.RequestStatus.WaitForUser;
            request.ErrorMessage = prompt.ToString();
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);
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
        private int EnqueueRequest(FileStoreRequest request)
        {
            _requests.Enqueue(request);
            return request.RequestId;
        }
        private async Task LocalOnChanged(object sender, LocalFileStoreEventArgs e)
        {
            var itemMetadata = _metadata.GetItemMetadata(e.LocalPath);
            ILocalItemHandle itemHandle;
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    if (TryGetItemHandle(e.LocalPath, out itemHandle))
                    {
                        //item exists ... 
                        var parentMetadata = _metadata.GetParentItemMetadata(e.LocalPath);
                        if (parentMetadata == null)
                        {
                            //... but doesn't have a parent
                            //TODO: will this ever happen?
                            Debug.WriteLine($"Failed to find parent item of created item \"{e.LocalPath}\"");
                        }
                        else
                        {
                            //... with a parent, so add the item metadata ... (this happens when filtering instead now)
                            //_metadata.AddItemMetadata(new LocalRemoteItemHandle(itemHandle,
                            //    _metadata.GetNextItemId().ToString(), parentMetadata.Id));

                            //... and add the delta
                            _localDeltas.Enqueue(new ItemDelta()
                            {
                                Type = ItemDelta.DeltaType.Created,
                                Handle = itemHandle
                            });
                        }
                    }
                    else
                    {
                        //an item was created/changed, but it doesn't exist anymore?  the user must have moved or deleted it... so do nothing
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    if (TryGetItemHandle(e.LocalPath, out itemHandle))
                    {
                        //the deleted item exists, so at some point the user created a new one ... so wait for that event and do nothing
                    }
                    else
                    {
                        //item doesn't exist ...
                        if (itemMetadata == null)
                        {
                            //... and also doesn't exist in metadata, so it's as if it never existed ... so do nothing
                        }
                        else
                        {
                            //... but does exist in metadata, so remove it ... (this happens when filtering instead now)
                            //_metadata.RemoveItemMetadataById(itemMetadata.Id);

                            //... and add the delta
                            _localDeltas.Enqueue(new ItemDelta()
                            {
                                Type = ItemDelta.DeltaType.Deleted,
                                Handle = new DeletedItemHandle(itemMetadata),
                                OldPath = e.LocalPath
                            });
                        }
                    }
                    break;
                case WatcherChangeTypes.Changed:
                    if (TryGetItemHandle(e.LocalPath, out itemHandle))
                    {
                        //item exists ... 
                        if (itemMetadata == null)
                        {
                            //... without metadata ... TODO: what do we do here?
                        }
                        else
                        {
                            //... with metadata ...
                            if (itemHandle.IsFolder)
                            {
                                //... but the item is a folder, and we don't sync folder contents changes
                            }
                            else
                            {
                                //... and is a file ...
                                var parentMetadata = _metadata.GetParentItemMetadata(e.LocalPath);
                                if (parentMetadata == null)
                                {
                                    //... but doesn't have a parent
                                    //TODO: will this ever happen?
                                    Debug.WriteLine($"Failed to find parent item of created item \"{e.LocalPath}\"");
                                }
                                else
                                {
                                    //... with a parent, so update the item metadata ...
                                    _metadata.AddOrUpdateItemMetadata(itemMetadata);

                                    //... and add the delta
                                    _localDeltas.Enqueue(new ItemDelta()
                                    {
                                        Type = ItemDelta.DeltaType.Modified,
                                        Handle = itemHandle
                                    });
                                }
                            }

                        }
                    }
                    else
                    {
                        //an item was created/changed, but it doesn't exist anymore?  the user must have moved or deleted it... so do nothing
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    if (TryGetItemHandle(e.OldLocalPath, out itemHandle))
                    {
                        //item exists ... 
                        var parentMetadata = _metadata.GetParentItemMetadata(e.LocalPath);
                        if (parentMetadata == null)
                        {
                            //... but doesn't have a parent
                            //TODO: will this ever happen?
                            Debug.WriteLine($"Failed to find parent item of renamed item \"{e.LocalPath}\"");
                        }
                        else
                        {
                            //... with a parent, so add the item metadata ...
                            _metadata.AddItemMetadata(new LocalRemoteItemHandle(itemHandle,
                                _metadata.GetNextItemId().ToString(), parentMetadata.Id));

                            //... and add the delta
                            _localDeltas.Enqueue(new ItemDelta()
                            {
                                Type = ItemDelta.DeltaType.Renamed,
                                Handle = itemHandle
                            });
                        }
                    }
                    else
                    {
                        //an item was created/changed, but it doesn't exist anymore?  the user must have moved or deleted it... so do nothing
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool CreateWritableHandle(FileStoreRequest request, DateTime lastModified)
        {
            var itemHandle = _local.GetFileHandle(request.Path);
            var stream = itemHandle.GetWritableStream();
            if (stream == null)
            {
                //this means something is blocking the file from being written to...
                RequestAwaitUser(request, UserPrompts.CloseApplication);
            }
            else
            {
                NotifyDisposedStream writeStream = new NotifyDisposedStream(stream);
                writeStream.OnDisposed +=
                    (sender) =>
                    {
                        //after disposing, make sure that we set the last modified of the local and the metadata
                        _local.SetItemLastModified(request.Path, lastModified);

                        _metadata.GetItemMetadata(request.Path).LastModified =
                            lastModified; //TODO: this is OK to do, change everywhere else!!!
                    };

                request.ExtraData = new RequestStreamExtraData(writeStream, true);
            }
            return true;
        }
        private bool CreateReadOnlyHandle(FileStoreRequest request)
        {
            var itemHandle = _local.GetFileHandle(request.Path);
            var stream = itemHandle.GetFileDataAsync().Result;
            if (stream == null)
            {
                //this means something is blocking the file from being read from...
                RequestAwaitUser(request, UserPrompts.CloseApplication);
            }
            else
            {
                request.ExtraData = new RequestStreamExtraData(stream, false);
            }
            return true;
        }
        private bool RenameItem(ItemMetadataCache.ItemMetadata metadata, string newName, FileStoreRequest request)
        {
            var newPath = $"{PathUtils.GetParentItemPath(metadata.Path)}/{newName}";
            if (_local.ItemExists(newPath))
            {
                //item exists at the new location, so prompt user
                RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
            }
            else
            {
                if (_local.MoveLocalItem(metadata.Path, newPath))
                {
                    //successful move
                    metadata.Name = newName;
                    request.Status = FileStoreRequest.RequestStatus.Success;
                    InvokeStatusChanged(request);
                }
                else
                {
                    //TODO: when will this happen?
                    RequestAwaitUser(request, UserPrompts.CloseApplication);
                }
            }

            return true;
        }
        private bool MoveItem(ItemMetadataCache.ItemMetadata metadata, ItemMetadataCache.ItemMetadata newParentMetadata, FileStoreRequest request)
        {
            var newPath = $"{newParentMetadata.Path}/{metadata.Name}";
            if (_local.ItemExists(newPath))
            {
                //item exists at the new location, so prompt user
                RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
            }
            else
            {
                if (_local.MoveLocalItem(metadata.Path, newPath))
                {
                    //successful move
                    metadata.ParentId = newParentMetadata.Id;
                    request.Status = FileStoreRequest.RequestStatus.Success;
                    InvokeStatusChanged(request);
                }
                else
                {
                    //if the item doesn't exist already, when will this fail? (moving a folder with a item being modified in it?)
                    RequestAwaitUser(request, UserPrompts.CloseApplication);
                }
            }
            return true;
        }
        private bool CreateItem(string path, DateTime lastModified, FileStoreRequest request)
        {
            //TODO: should we do any checking?  or just ignore it if the request failed
            _local.CreateLocalFolder(path, lastModified);
            return true;
        }
        private bool DeleteItem(string path, FileStoreRequest request)
        {
            //TODO: should we do any checking?  or just ignore it if the request failed
            _local.DeleteLocalItem(path);
            return true;
        }

        private async Task ProcessQueue(TimeSpan delay, TimeSpan errorDelay, CancellationToken ct)
        {
            /*
             * 
             *   Only modify metadata when processing remote requests,
             *      because we have the "last modified" attribute 
             *      inherant to the file system.  We will need to use
             *      something other than the metadata to check if items
             *      exist et. al.
             * 
             */

            bool dequeue = false;
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

                    var itemMetadata = _metadata.GetItemMetadata(request.Path);

                    switch (request.Type)
                    {
                        case FileStoreRequest.RequestType.Write: // get writable stream
                        {
                            var data = request.ExtraData as RequestWritableStreamExtraData;
                            if (data != null)
                            {
                                //get the file handle
                                var fileHandle = _local.GetFileHandle(request.Path);
                                if (_local.ItemExists(request.Path) && fileHandle.IsFolder)
                                {
                                    FailRequest(request, $"Attempted to open a folder \"{request.Path}\" for writing");
                                }
                                if (itemMetadata != null)
                                {
                                    if (!_local.ItemExists(request.Path))
                                    {
                                        //item has been synced before, but doesn't exist locally...  this is weird
                                        FailRequest(request, $"Attempt to download previously synced file that no longer exists \"{request.Path}\"");
                                    }
                                    else
                                    {
                                        //item has been synced before
                                        var lastSyncTime = itemMetadata.LastModified;

                                        if (data.LastModified > lastSyncTime) //(this is pretty much a guarantee)
                                        {
                                            if (data.Sha1 == itemMetadata.Sha1)
                                            {
                                                //if we are receiving an update AND the sha1 of the remote is the same as the sha1 of
                                                //  the previous sync, then this must mean that this request is caused by a delta
                                                //  that originated from a local source, so just update the metadata so we have the
                                                //  correct "last modified" and cancel the request

                                                _local.SetItemLastModified(fileHandle.Path, data.LastModified);
                                                itemMetadata.LastModified = data.LastModified;

                                                request.Status = FileStoreRequest.RequestStatus.Cancelled;
                                                InvokeStatusChanged(request);
                                                dequeue = true;
                                            }
                                            else
                                            {
                                                /*if (data.Sha1 == fileHandle.SHA1Hash)
                                                {
                                                    //TODO: is this a desirable option?
                                                    //same sha1, just update metadata and file info
                                                    _local.SetItemLastModified(fileHandle.Path, data.LastModified);
                                                    itemMetadata.LastModified = data.LastModified;
                                                }
                                                else
                                                {*/
                                                    if (fileHandle.LastModified > lastSyncTime)
                                                    {
                                                        //local item has also been modified since last sync
                                                        RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
                                                        dequeue =
                                                            true; //TODO: we must pause everything while requesting the user!!!
                                                    }
                                                    else
                                                    {
                                                        //local item has not been modified since last sync
                                                        dequeue = CreateWritableHandle(request, data.LastModified);
                                                    }
                                                //}
                                            }
                                        }
                                        else
                                        {
                                            FailRequest(request, $"Attempt to download older version of \"{request.Path}\" than exists locally");
                                            dequeue = true;
                                        }
                                    }
                                }
                                else
                                {
                                        //item metadata doesn't exist, so get it's parent
                                    var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                                    if (parentMetadata == null)
                                    {
                                        FailRequest(request, $"Attempted to get parent metadata of item \"{request.Path}\" that does not exist");
                                        dequeue = true;
                                    }
                                    else
                                    {
                                            //add the new item to the metadata with the retrieved parent
                                        _metadata.AddItemMetadata(new LocalRemoteItemHandle(fileHandle, _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                                        if (_local.ItemExists(request.Path))
                                        {
                                            //item hasn't been synced before, so definitely ask the user ... 
                                            RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
                                            dequeue = true;//TODO: we must pause everything while requesting the user!!!
                                        }
                                        else
                                        {
                                            //item has't been synced before, but doesn't exist locally, so just create the file
                                            dequeue = CreateWritableHandle(request, data.LastModified);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.Write("Write request was called without approprate extra data");
                            }
                        }
                            break;
                        case FileStoreRequest.RequestType.Read: // get read-only stream
                        {
                            if (!_local.ItemExists(request.Path))
                            {
                                FailRequest(request,
                                    $"Attempt to open a local item that does not exist \"{request.Path}\"");
                                dequeue = true;
                            }
                            else
                            {
                                var fileHandle = _local.GetFileHandle(request.Path);
                                if (fileHandle.IsFolder)
                                {
                                    FailRequest(request, $"Attempted to open a folder \"{request.Path}\" for reading");
                                    dequeue = true;
                                }
                                if (itemMetadata == null)
                                {
                                    //item hasn't been synced before
                                    var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                                    if (parentMetadata == null)
                                    {
                                        //the item exists locally, but something is messed up with the metadata
                                        FailRequest(request, $"Attempt to open a file \"{request.Path}\" that has no parent metadata.  Check metadata cache state");
                                        dequeue = true;
                                    }
                                    else
                                    {
                                        _metadata.AddItemMetadata(new LocalRemoteItemHandle(fileHandle,
                                            _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                                    }
                                }
                                dequeue = CreateReadOnlyHandle(request);
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
                                    dequeue = RenameItem(itemMetadata, data.NewName, request);
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
                                        dequeue = MoveItem(itemMetadata, parentMetadata, request);
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
                            var data = request.ExtraData as RequestCreateFolderExtraData;
                            if (data != null)
                            {
                                var folderHandle = _local.GetFileHandle(request.Path);
                                if (request.Path == "/")
                                {
                                    //the root...
                                    dequeue = true;
                                }
                                else
                                {
                                    var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                                    if (parentMetadata == null)
                                    {
                                        //new location doesn't exist TODO: should this be an error or should we create the new location?
                                        FailRequest(request,
                                            $"Could not create \"{request.Path}\" because parent location doesn't exist");

                                        dequeue = true;
                                    }
                                    else
                                    {
                                        if (_local.ItemExists(request.Path))
                                        {
                                            //there's no point in creating a folder that already exists
                                            if (itemMetadata == null)
                                            {
                                                //no metadata though, so add it
                                                _metadata.AddOrUpdateItemMetadata(new LocalRemoteItemHandle(
                                                    folderHandle, _metadata.GetNextItemId().ToString(),
                                                    parentMetadata.Id));
                                            }
                                            dequeue = true;
                                        }
                                        else
                                        {
                                            dequeue = CreateItem(request.Path, data.LastModified, request);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.Write("Create folder was called without appropriate extra data");
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
                                dequeue = DeleteItem(itemMetadata.Id, request);
                            }
                        }
                            break;
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
                        await Task.Delay(errorDelay, ct);
                    }
                }

                //while there are limbo'd requests, pause other requests
                while(!_limboRequests.IsEmpty)
                {
                    await Task.Delay(errorDelay, ct);
                    if (ct.IsCancellationRequested)
                        break;
                }
                await Task.Delay(delay, ct);
            }
        }
        #endregion

        #region Public Properties
        public string MetadataCache
        {
            get => _metadata.Serialize();
            set => _metadata.Deserialize(value);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to get an item handle and bypasses the request system
        /// </summary>
        /// <param name="path">the path of the handle to get</param>
        /// <param name="itemHandle">the handle to the item, whether it exists or not</param>
        /// <returns>whether the item exists</returns>
        public bool TryGetItemHandle(string path, out ILocalItemHandle itemHandle)
        {
            itemHandle = _local.GetFileHandle(path);
            return _local.ItemExists(path);
        }
        public bool ItemExists(string path)
        {
            return _local.ItemExists(path);
        }
        
        /// <summary>
        /// Gets all of the deltas since the previous check
        /// </summary>
        /// <returns>a structure of item deltas</returns>
        public Task<IEnumerable<ItemDelta>> GetDeltasAsync()
        {
            return Task.Run(() =>
            {
                List<ItemDelta> filteredDeltas = new List<ItemDelta>();
                while (_localDeltas.TryDequeue(out ItemDelta result))
                {
                    switch (result.Type)
                    {
                        case ItemDelta.DeltaType.Created:
                            var parentMetadata = _metadata.GetParentItemMetadata(result.Handle.Path);
                            if (parentMetadata != null)
                            {
                                _metadata.AddItemMetadata(new LocalRemoteItemHandle(result.Handle,
                                    _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                            }
                            else
                            {
                                Debug.WriteLine("Created item has no valid parent when filtering delta queue");
                                continue;//we need to get ourselves together here
                            }
                            break;
                        case ItemDelta.DeltaType.Deleted:
                            //deleted item ...
                            if (_localDeltas.TryPeek(out ItemDelta next))
                            {
                                if (next.Type == ItemDelta.DeltaType.Created)
                                {
                                    //... with a creation immediately following ...
                                    if (next.Handle.LastModified == result.Handle.LastModified)
                                    {
                                        //... with the same last modified ...
                                        if (_localDeltas.TryDequeue(out ItemDelta nextDelta))
                                        {
                                            //... so update the metadata accordingly ... 
                                            var itemMetadata = _metadata.GetItemMetadata(result.Handle.Path);
                                            if (itemMetadata != null)
                                            {
                                                var newParentItemMetadata =
                                                    _metadata.GetParentItemMetadata(nextDelta.Handle.Path);
                                                if (newParentItemMetadata != null)
                                                {
                                                    itemMetadata.ParentId = newParentItemMetadata.Id;
                                                    _metadata.AddOrUpdateItemMetadata(itemMetadata);
                                                }
                                                else
                                                {
                                                    Debug.WriteLine("Moved item has no valid parent when filtering delta queue");
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Moved item has no valid initial metadata");
                                                continue;
                                            }

                                            //... and merge the two into a "moved" delta ...
                                            filteredDeltas.Add(new ItemDelta()
                                            {
                                                Handle = nextDelta.Handle,
                                                OldPath = result.OldPath,
                                                Type = ItemDelta.DeltaType.Moved
                                            });
                                            continue;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //remove from metadata ONLY if there is no corresponding "create" call 
                                _metadata.RemoveItemMetadata(result.OldPath);
                            }
                            break;
                        case ItemDelta.DeltaType.Renamed:
                            break;
                        case ItemDelta.DeltaType.Modified:
                            break;
                        case ItemDelta.DeltaType.Moved:
                            break;
                        //TODO: detect item "moves"
                    }

                    //if there are no objections ("continue") then let the request through
                    filteredDeltas.Add(result);
                }
                return (IEnumerable<ItemDelta>) filteredDeltas;
            });
        }

        /// <summary>
        /// Gets a writable stream.  To get the stream, use the <see cref="RequestStreamExtraData"/> of 
        ///  the request after calling <see cref="AwaitRequest"/>
        /// </summary>
        /// <param name="path">the path of the stream to get</param>
        /// <param name="sha1">the sha1 sum of the item to test against to determine conflicts.  leave blank/null to always overwrite local</param>
        /// <returns>the request id</returns>
        public int RequestWritableStream(string path, string sha1, DateTime lastModified)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Write, path, new RequestWritableStreamExtraData(sha1, lastModified)));
        }
        /// <summary>
        /// Gets a read-only stream.  To get the stream, use the <see cref="RequestStreamExtraData"/> of 
        ///  the request after calling <see cref="AwaitRequest"/>
        /// </summary>
        /// <param name="path">the path of the stream to get</param>
        /// <returns>the request id</returns>
        public int RequestReadOnlyStream(string path)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Read, path, null));
        }
        /// <summary>
            /// Deletes a local item and its children
            /// </summary>
            /// <param name="path">the path of the item to delete</param>
            /// <returns>the request id</returns>
        public int RequestDeleteItem(string path)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Delete, path, null));
        }
        /// <summary>
        /// Creates a local folder
        /// </summary>
        /// <param name="path">the path of the folder to create</param>
        /// <returns>the request id</returns>
        public int RequestFolderCreate(string path, DateTime lastModified)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path,
                new RequestCreateFolderExtraData(lastModified)));
        }
        /// <summary>
        /// Moves a local item
        /// </summary>
        /// <param name="path">the current path of the item</param>
        /// <param name="newParentPath">the new location of the item post-move</param>
        /// <returns>the request id</returns>
        public int RequestMoveItem(string path, string newParentPath)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path,
                new RequestMoveExtraData(newParentPath)));
        }
        /// <summary>
        /// Renames a local item
        /// </summary>
        /// <param name="path">the current path of the item</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public int RequestRenameItem(string path, string newName)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path,
                new RequestRenameExtraData(newName)));
        }


        /// <summary>
        /// Starts the processing of the request queue
        /// </summary>
        public void StartRequestProcessing()
        {
            if (_processQueueTask != null) return;
            _processQueueCancellationTokenSource = new CancellationTokenSource();
            _processQueueTask = ProcessQueue(TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(100), _processQueueCancellationTokenSource.Token);
        }
        /// <summary>
        /// Stops the processing of the request queue
        /// </summary>
        /// <returns></returns>
        public async Task StopRequestProcessingAsync()
        {
            if (_processQueueTask == null) return;
            _processQueueCancellationTokenSource.Cancel();
            await _processQueueTask;
        }

        //TODO: the two methods below are REALLY bad b/c they do no null/exception checking

        public async Task SaveNonSyncFile(string path, string content)
        {
            var itemHandle = _local.GetFileHandle(path);
            using (var writableStream = itemHandle.GetWritableStream())
            {
                using (var contentStream = content.ToStream(Encoding.UTF8))
                {
                    await contentStream.CopyToStreamAsync(writableStream);
                }
            }
            _local.SetItemAttributes(path, FileAttributes.Hidden);
        }
        public async Task<string> ReadNonSyncFile(string path)
        {
            return await (await _local.GetFileHandle(path).GetFileDataAsync()).ReadAllToStringAsync(Encoding.UTF8);
        }

        /// <summary>
        /// Waits for the given request status to reach a conclusive statis
        ///  (<see cref="FileStoreRequest.RequestStatus.Cancelled"/>
        ///  or <see cref="FileStoreRequest.RequestStatus.Success"/>
        ///  or <see cref="FileStoreRequest.RequestStatus.Failure"/>
        /// </summary>
        /// <param name="requestId">the id of the request to wait for</param>
        /// <returns>the request with that id</returns>
        public async Task<FileStoreRequest> AwaitRequest(int requestId)
        {
            if (TryGetRequest(requestId, out FileStoreRequest request))
            {
                var done = false;
                OnRequestStatusChanged += async (sender, args) =>
                {
                    if (args.RequestId == requestId && (args.Status == FileStoreRequest.RequestStatus.Success ||
                                                        args.Status == FileStoreRequest.RequestStatus.Cancelled ||
                                                        args.Status == FileStoreRequest.RequestStatus.Failure))
                        done = true;
                };

                var cts = new CancellationTokenSource();
                while (!done)
                {
                    if (cts.IsCancellationRequested)
                        break;
                    await Task.Delay(50, cts.Token);
                }
                return request;
            }
            else
            {
                return null;
            }
        }
        public bool TryGetRequest(int requestId, out FileStoreRequest request)
        {
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                //no queue items... let's check limbo
                if (_limboRequests.TryGetValue(requestId, out FileStoreRequest limboReq))
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
                //no queue items... let's check limbo
                if (_limboRequests.TryRemove(requestId, out FileStoreRequest value))
                {
                    value.Status = FileStoreRequest.RequestStatus.Cancelled;
                    InvokeStatusChanged(value);
                }
            }
            else
            {
                //there is a queue item, so add it to the cancellation dictionary
                _cancelledRequests.TryAdd(requestId, null);
            }
        }
        #endregion

        #region Public Events
        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="FileStoreRequest.RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        public event EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;//TODO: local status' are a bit different, because there is a rename/merge option
        #endregion
    }
}
