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
    public class LocalFileStoreInterface : FileStoreInterface
    {
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
            public RequestWritableStreamExtraData(string sha1, DateTime lastModified, Action<FileStoreRequest> onCompleteFunc)
            {
                Sha1 = sha1;
                LastModified = lastModified;
                OnCompleted = onCompleteFunc;
            }
            public string Sha1 { get; }
            public DateTime LastModified { get; }
            public Action<FileStoreRequest> OnCompleted { get; }
        }
        private class RequestReadOnlyStreamExtraData : IFileStoreRequestExtraData
        {
            public RequestReadOnlyStreamExtraData(Action<FileStoreRequest> onCompleteFunc)
            {
                OnCompleted = onCompleteFunc;
            }
            public Action<FileStoreRequest> OnCompleted { get; }
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
        private int _requestId;//TODO: should this be volatile
        #endregion

        public LocalFileStoreInterface(ILocalFileStore local)
        {
            _local = local;
            _local.OnChanged += LocalOnChanged;
        }

        #region Private Methods
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
                            if (itemMetadata == null) //FILTERS OUT REBOUNDING DELTAS
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
                            else
                            {
                                if (itemMetadata.LastModified != itemHandle.LastModified) //FILTERS OUT REBOUNDING DELTAS
                                {
                                    //this happens when the item has been created, deleted, then re-created
                                    _localDeltas.Enqueue(new ItemDelta()
                                    {
                                        Type = ItemDelta.DeltaType.Created,
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
                case WatcherChangeTypes.Deleted:
                    if (TryGetItemHandle(e.LocalPath, out itemHandle))
                    {
                        //the deleted item exists, so at some point the user created a new one ... so wait for that event and do nothing
                    }
                    else
                    {
                        //item doesn't exist ...
                        if (itemMetadata == null) //FILTERS OUT REBOUNDING DELTAS
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
                            //... without metadata ... so create it
                            _localDeltas.Enqueue(new ItemDelta()
                            {
                                Type=ItemDelta.DeltaType.Created,
                                Handle = itemHandle
                            });
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
                                if (itemHandle.LastModified != itemMetadata.LastModified) //if this is the same, then we got this even twice?
                                {
                                    //... and is a file ...
                                    if (itemHandle.SHA1Hash != itemMetadata.Sha1) //FILTERS OUT REBOUNDING DELTAS
                                    {
                                        //... with a different sha1 hash as the last update ...
                                        var parentMetadata = _metadata.GetParentItemMetadata(e.LocalPath);
                                        if (parentMetadata == null)
                                        {
                                            //... but doesn't have a parent
                                            //TODO: will this ever happen?
                                            Debug.WriteLine(
                                                $"Failed to find parent item of created item \"{e.LocalPath}\"");
                                        }
                                        else
                                        {
                                            //... with a parent, so update the item metadata ...
                                            //_metadata.AddOrUpdateItemMetadata(itemMetadata);

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

                        }
                    }
                    else
                    {
                        //an item was created/changed, but it doesn't exist anymore?  the user must have moved or deleted it... so do nothing
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    if (TryGetItemHandle(e.LocalPath, out itemHandle))
                    {
                        //item exists ... 
                        var parentMetadata = _metadata.GetParentItemMetadata(e.OldLocalPath);
                        if (parentMetadata == null)
                        {
                            //... but doesn't have a parent
                            //TODO: will this ever happen?
                            Debug.WriteLine($"Failed to find parent item of renamed item \"{e.LocalPath}\"");
                        }
                        else
                        {
                            //... with a parent ...
                            if (itemMetadata == null) //FILTERS OUT REBOUNDING DELTAS
                            {
                                itemMetadata = _metadata.GetItemMetadata(e.OldLocalPath);
                                if (itemMetadata == null)
                                {
                                    //... and no metadata at the old location either, so add new metadata ...
                                    //_metadata.AddItemMetadata(new LocalRemoteItemHandle(itemHandle,
                                    //    _metadata.GetNextItemId().ToString(), parentMetadata.Id));

                                    //... and create it i guess
                                    _localDeltas.Enqueue(new ItemDelta()
                                    {
                                        Type = ItemDelta.DeltaType.Created,
                                        OldPath = e.OldLocalPath,
                                        Handle = itemHandle
                                    });
                                }
                                else
                                {
                                    //... so update the name ...
                                    //THIS HAS BEEN MOVED TO THE DEQUEUE METHOD

                                    //... and add the delta
                                    _localDeltas.Enqueue(new ItemDelta()
                                    {
                                        Type = ItemDelta.DeltaType.Renamed,
                                        OldPath = e.OldLocalPath,
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool CreateWritableHandle(ILocalItemHandle itemHandle, string parentId, FileStoreRequest request, DateTime lastModified)
        {
            var stream = itemHandle.GetWritableStream();
            if (stream == null)
            {
                //this means something is blocking the file from being written to...
                RequestAwaitUser(request, UserPrompts.CloseApplication);
                return false;
            }
            else
            {
                NotifyDisposedStream writeStream = new NotifyDisposedStream(stream);
                writeStream.OnDisposed +=
                    (sender) =>
                    {
                        return Task.Run(() =>
                        {
                            //after disposing, make sure that we set the last modified of the local and the metadata
                            _local.SetItemLastModified(request.Path, lastModified);

                            //add the new item to the metadata with the retrieved parent.  
                            //      Do this after so the file is done writing to
                            _metadata.AddOrUpdateItemMetadata(new LocalRemoteItemHandle(itemHandle,
                                _metadata.GetNextItemId().ToString(), parentId));
                            _metadata.GetItemMetadata(request.Path).LastModified =
                                lastModified; //TODO: this is OK to do, change everywhere else!!!
                        });
                    };

                var func = (request.ExtraData as RequestWritableStreamExtraData)?.OnCompleted;

                request.ExtraData = new RequestStreamExtraData(writeStream, true);
                InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);

                func?.Invoke(request);
                return true;
            }
        }
        private bool CreateReadOnlyHandle(FileStoreRequest request)
        {
            var itemHandle = _local.GetFileHandle(request.Path);
            var stream = itemHandle.GetFileDataAsync().Result;
            if (stream == null)
            {
                //this means something is blocking the file from being read from...
                RequestAwaitUser(request, UserPrompts.CloseApplication);
                return false;
            }
            else
            {
                var func = (request.ExtraData as RequestReadOnlyStreamExtraData)?.OnCompleted;

                request.ExtraData = new RequestStreamExtraData(stream, false);
                InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);

                func?.Invoke(request);
                return true;
            }
        }
        private bool RenameItem(ItemMetadataCache.ItemMetadata metadata, string newName, FileStoreRequest request)
        {
            var newPath = PathUtils.GetRenamedPath(metadata.Path, newName);
            if (_local.ItemExists(newPath))
            {
                //item exists at the new location, so prompt user
                RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
                return false;
            }
            else
            {
                if (_local.MoveLocalItem(metadata.Path, newPath))
                {
                    //successful move
                    metadata.Name = newName;
                    InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);
                    return true;
                }
                else
                {
                    //TODO: when will this happen?
                    RequestAwaitUser(request, UserPrompts.CloseApplication);
                    return false;
                }
            }

        }
        private bool MoveItem(ItemMetadataCache.ItemMetadata metadata, ItemMetadataCache.ItemMetadata newParentMetadata, FileStoreRequest request)
        {
            var newPath = $"{newParentMetadata.Path}/{metadata.Name}";
            if (_local.ItemExists(newPath))
            {
                //item exists at the new location, so prompt user
                RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
                return false;
            }
            else
            {
                if (_local.MoveLocalItem(metadata.Path, newPath))
                {
                    //successful move
                    metadata.ParentId = newParentMetadata.Id;
                    InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);
                    return true;
                }
                else
                {
                    //if the item doesn't exist already, when will this fail? (moving a folder with a item being modified in it?)
                    RequestAwaitUser(request, UserPrompts.CloseApplication);
                    return false;
                }
            }
        }
        private bool CreateItem(string path, DateTime lastModified, FileStoreRequest request)
        {
            //TODO: should we do any checking?  or just ignore it if the request failed
            _local.CreateLocalFolder(path, lastModified);
            InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);
            return true;
        }
        private bool DeleteItem(string path, FileStoreRequest request)
        {
            var success = _local.DeleteLocalItem(path);
            if (success) // item deleted or not
            {
                InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Success);
            }
            else // item in use/otherwise blocked
            {
                RequestAwaitUser(request, UserPrompts.CloseApplication);
            }
            return success;
        }

        protected override async Task<bool> ProcessQueueItemAsync(FileStoreRequest request)
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
                            return false;
                        }
                        if (itemMetadata != null)
                        {
                            if (!_local.ItemExists(request.Path))
                            {
                                //item has been synced before, but doesn't exist locally...  this is weird
                                FailRequest(request, $"Attempt to download previously synced file that no longer exists \"{request.Path}\"");
                                return false;
                            }
                            else
                            {
                                //item has been synced before
                                var lastSyncTime = itemMetadata.LastModified;

                                if (data.LastModified > lastSyncTime) //(this is pretty much a guarantee)
                                {
                                    if (data.Sha1 == itemMetadata.Sha1) // REBOUND FILTER
                                    {
                                        //if we are receiving an update AND the sha1 of the remote is the same as the sha1 of
                                        //  the previous sync, then this must mean that this request is caused by a delta
                                        //  that originated from a local source, so just update the metadata so we have the
                                        //  correct "last modified" and cancel the request

                                        _local.SetItemLastModified(fileHandle.Path, data.LastModified);
                                        itemMetadata.LastModified = data.LastModified;
                                                    
                                        InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Cancelled);
                                        return true;
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
                                                return false;
                                            }
                                            else
                                            {
                                                //local item has not been modified since last sync
                                                return CreateWritableHandle(fileHandle, itemMetadata.ParentId, request, data.LastModified);
                                            }
                                        //}
                                    }
                                }
                                else
                                {
                                    FailRequest(request, $"Attempt to download older version of \"{request.Path}\" than exists locally");
                                    return false;
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
                                return false;
                            }
                            else
                            {
                                if (_local.ItemExists(request.Path))
                                {
                                    //item hasn't been synced before, so definitely ask the user ... 
                                    RequestAwaitUser(request, UserPrompts.KeepOverwriteOrRename);
                                    return false;
                                }
                                else
                                {
                                    //item has't been synced before, but doesn't exist locally, so just create the file
                                    return CreateWritableHandle(fileHandle, parentMetadata.Id, request, data.LastModified);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Write request was called without approprate extra data");
                        return false;
                    }
                }
                    break;
                case FileStoreRequest.RequestType.Read: // get read-only stream
                {
                    var data = request.ExtraData as RequestReadOnlyStreamExtraData;
                    if (data != null)
                    {
                        if (!_local.ItemExists(request.Path))
                        {
                            FailRequest(request,
                                $"Attempt to open a local item that does not exist \"{request.Path}\"");
                            return false;
                        }
                        else
                        {
                            var fileHandle = _local.GetFileHandle(request.Path);
                            if (fileHandle.IsFolder)
                            {
                                FailRequest(request, $"Attempted to open a folder \"{request.Path}\" for reading");
                                return false;
                            }
                            if (itemMetadata == null)
                            {
                                //item hasn't been synced before
                                var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                                if (parentMetadata == null)
                                {
                                    //the item exists locally, but something is messed up with the metadata
                                    FailRequest(request,
                                        $"Attempt to open a file \"{request.Path}\" that has no parent metadata.  Check metadata cache state");
                                    return false;
                                }
                                else
                                {
                                    _metadata.AddItemMetadata(new LocalRemoteItemHandle(fileHandle,
                                        _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                                }
                            }
                            return CreateReadOnlyHandle(request);

                        }
                    }
                    else
                    {
                        Debug.WriteLine("Read request was called without appropriate extra data");
                        return false;
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
                            itemMetadata =
                                _metadata.GetItemMetadata(PathUtils.GetRenamedPath(request.Path, data.NewName));
                            if (itemMetadata == null)
                            {
                                FailRequest(request, $"Could not find file \"{request.Path}\" to rename");
                                return false;
                            }
                            else
                            {
                                //Rebounded request... but we can't update last modified...
                                return true;
                            }
                        }
                        else
                        {
                            //rename the file
                            return RenameItem(itemMetadata, data.NewName, request);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Rename request was called without approprate extra data");
                        return false;
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
                                return MoveItem(itemMetadata, parentMetadata, request);
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Move request was called without approprate extra data");
                        return false;
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
                            //the root... it must already exist
                            return true;
                        }
                        else
                        {
                            var parentMetadata = _metadata.GetParentItemMetadata(request.Path);
                            if (parentMetadata == null)
                            {
                                //new location doesn't exist TODO: should this be an error or should we create the new location?
                                FailRequest(request,
                                    $"Could not create \"{request.Path}\" because parent location doesn't exist");
                                return false;
                                }
                            else
                            {
                                var ret = _local.ItemExists(request.Path) || CreateItem(request.Path, data.LastModified, request);
                                if (itemMetadata == null)
                                {
                                    //no metadata though, so add it
                                    _metadata.AddOrUpdateItemMetadata(new LocalRemoteItemHandle(
                                        folderHandle, _metadata.GetNextItemId().ToString(),
                                        parentMetadata.Id));
                                }
                                return ret;
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Create folder was called without appropriate extra data");
                        return false;
                    }
                }
                    break;
                case FileStoreRequest.RequestType.Delete:
                {
                    if (itemMetadata == null)
                    {
                        //item doesn't exist.  Is this an issue?
                        FailRequest(request, $"Could not delete \"{request.Path}\" because it does not exist!");
                        return false;
                    }
                    else
                    {
                        var success = DeleteItem(itemMetadata.Path, request);
                        if (success)
                        {
                            _metadata.RemoveItemMetadataById(itemMetadata.Id);
                        }
                        return success;
                    }
                }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task<IEnumerable<ItemDelta>> GetEventDeltas()
        {
            List<ItemDelta> filteredDeltas = new List<ItemDelta>();
            while (_localDeltas.TryDequeue(out ItemDelta result))
            {
                var itemMetadata = _metadata.GetItemMetadata(result.Handle.Path);
                var parentMetadata = _metadata.GetParentItemMetadata(result.Handle.Path);
                switch (result.Type)
                {
                    case ItemDelta.DeltaType.Created:
                        if (parentMetadata != null)
                        {
                            if (_local.ItemExists(result.Handle.Path))
                            {
                                _metadata.AddItemMetadata(new LocalRemoteItemHandle(result.Handle,
                                    _metadata.GetNextItemId().ToString(), parentMetadata.Id));

                                if (result.Handle.IsFolder)
                                {
                                    //when creating a folder, scan its contents because it may have been copied over
                                    await DeepScanForChanges(result.Handle.Path);
                                }
                            }
                            else
                            {
                                //created item has been moved/deleted since it was created

                                //very often, an item will be renamed RIGHT after it is created -- this gets handled by 
                                //          the initial event handler
                                continue;
                                /*if (_localDeltas.TryPeek(out ItemDelta nextDelta))
                                {
                                    if (nextDelta.Type == ItemDelta.DeltaType.Renamed)
                                    {
                                        //...with a rename immediately following ...
                                        if (nextDelta.OldPath == result.Handle.Path)
                                        {
                                            //with our old path, so just create the other one...
                                            _metadata.AddItemMetadata(new LocalRemoteItemHandle(nextDelta.Handle,
                                                _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                                            
                                            //... and dequeue it
                                            _localDeltas.TryDequeue(out nextDelta);

                                            nextDelta.Type = ItemDelta.DeltaType.Created;
                                            nextDelta.OldPath = "";
                                            filteredDeltas.Add(nextDelta);
                                            continue;
                                        }
                                    }
                                }*/
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Created item has no valid parent when filtering delta queue");
                            continue; //we need to get ourselves together here
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
                                        if (itemMetadata != null)
                                        {
                                            var newParentItemMetadata =
                                                _metadata.GetParentItemMetadata(nextDelta.Handle.Path);
                                            if (newParentItemMetadata != null)
                                            {
                                                itemMetadata.ParentId = newParentItemMetadata.Id;
                                                //_metadata.AddOrUpdateItemMetadata(itemMetadata);
                                            }
                                            else
                                            {
                                                Debug.WriteLine(
                                                    "Moved item has no valid parent when filtering delta queue");
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
                        //remove from metadata ONLY if there is no corresponding "create" call 
                        _metadata.RemoveItemMetadata(result.OldPath);
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        if (itemMetadata == null)
                        {
                            _metadata.AddItemMetadata(new LocalRemoteItemHandle(result.Handle,
                                _metadata.GetNextItemId().ToString(), parentMetadata.Id));
                        }
                        else
                        {
                            itemMetadata.Name = result.Handle.Name;
                        }
                        break;
                    case ItemDelta.DeltaType.Modified:
                        itemMetadata.LastModified = result.Handle.LastModified;
                        itemMetadata.Sha1 = result.Handle.SHA1Hash;
                        break;
                    case ItemDelta.DeltaType.Moved:
                        break;
                }

                //if there are no objections ("continue") then let the request through
                filteredDeltas.Add(result);
            }

            //make sure there are no resultant orphans
            _metadata.ClearOrphanedMetadata();

            return (IEnumerable<ItemDelta>)filteredDeltas;
        }
        private async Task DeepScanForChanges(string path)
        {
            //get the local items
            var items = await _local.EnumerateItemsAsync(path);
            
            //remove the first item
            items.RemoveAt(0);

            //make sure the metadata is clean
            _metadata.ClearOrphanedMetadata();

            //get all metadata
            var metadata = _metadata.GetChildrenLastModified(path);

            //find the intersection of metadata and local items...

            //... if it exists in the local, but not the metadata, then it must be new
            var created = from item in items where !metadata.ContainsKey(item.Path) select item.Path;
            //... if it exists in the local and the metadata, and the last modified times are different, it must
            //          have been changed
            var changed = from item in items
                where metadata.ContainsKey(item.Path) && metadata[item.Path] != item.LastModified
                select item.Path;
            //... if it does not exist in local, but does exist in the metadata, then it must have been deleted
            var deleted = from mData in metadata where items.Any(s => s.Path == mData.Key) select mData.Key;

            // call for deleted items first
            foreach (var item in deleted)
            {
                await LocalOnChanged(this, new LocalFileStoreEventArgs(WatcherChangeTypes.Deleted, item));
            }
            // then create new ones
            foreach (var item in created)
            {
                await LocalOnChanged(this, new LocalFileStoreEventArgs(WatcherChangeTypes.Created, item));
            }
            // finally call the changed ones
            foreach (var item in changed)
            {
                await LocalOnChanged(this, new LocalFileStoreEventArgs(WatcherChangeTypes.Changed, item));
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
        /// <param name="comprehensive">whether to do a deep check of all files, or only look at events</param>
        /// <returns>a structure of item deltas</returns>
        /// <remarks>Call this method with <see cref="comprehensive"/> as true only
        ///  when there is a high likely hood that there were local changes that were 
        /// missed while the application was closed</remarks>
        public async Task<IEnumerable<ItemDelta>> GetDeltasAsync(bool comprehensive = false)
        {
            if (comprehensive)
            {
                await DeepScanForChanges("/");
            }
            return await GetEventDeltas();
        }

        /// <summary>
        /// Gets a writable stream.  To get the stream, use the <see cref="RequestStreamExtraData"/> of 
        ///  the request after calling <see cref="AwaitRequest"/>
        /// </summary>
        /// <param name="path">the path of the stream to get</param>
        /// <param name="sha1">the sha1 sum of the item to test against to determine conflicts.  leave blank/null to always overwrite local</param>
        /// <returns>the request id</returns>
        public void RequestWritableStream(string path, string sha1, DateTime lastModified, Action<FileStoreRequest> onCompleteFunc)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Write, path, new RequestWritableStreamExtraData(sha1, lastModified, onCompleteFunc)));
        }
        /// <summary>
        /// Gets a read-only stream.  To get the stream, use the <see cref="RequestStreamExtraData"/> of 
        ///  the request after calling <see cref="AwaitRequest"/>
        /// </summary>
        /// <param name="path">the path of the stream to get</param>
        /// <returns>the request id</returns>
        public void RequestReadOnlyStream(string path, Action<FileStoreRequest> onCompleteFunc)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Read, path, new RequestReadOnlyStreamExtraData(onCompleteFunc)));
        }
        /// <summary>
            /// Deletes a local item and its children
            /// </summary>
            /// <param name="path">the path of the item to delete</param>
            /// <returns>the request id</returns>
        public void RequestDeleteItem(string path)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Delete, path, null));
        }
        /// <summary>
        /// Creates a local folder
        /// </summary>
        /// <param name="path">the path of the folder to create</param>
        /// <returns>the request id</returns>
        public void RequestFolderCreate(string path, DateTime lastModified)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path,
                new RequestCreateFolderExtraData(lastModified)));
        }
        /// <summary>
        /// Moves a local item
        /// </summary>
        /// <param name="path">the current path of the item</param>
        /// <param name="newParentPath">the new location of the item post-move</param>
        /// <returns>the request id</returns>
        public void RequestMoveItem(string path, string newParentPath)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Move, path,
                new RequestMoveExtraData(newParentPath)));
        }
        /// <summary>
        /// Renames a local item
        /// </summary>
        /// <param name="path">the current path of the item</param>
        /// <param name="newName">the new name of the item</param>
        /// <returns>the request id</returns>
        public void RequestRenameItem(string path, string newName)
        {
            EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Rename, path,
                new RequestRenameExtraData(newName)));
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
