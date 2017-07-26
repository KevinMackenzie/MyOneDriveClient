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
            public LocalRemoteItemHandle(IItemHandle handle, string id, string parentId)
            {
                _handle = handle;
                Id = id;
                ParentId = parentId;
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
            public string SHA1Hash => _handle.SHA1Hash;
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

        /// <summary>
        /// Request data for getting a writable stream
        /// </summary>
        private class RequestWritableStreamExtraData : IFileStoreRequestExtraData
        {
            public RequestWritableStreamExtraData(string sha1)
            {
                Sha1 = sha1;
            }
            public string Sha1 { get; }
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
        #endregion

        public LocalFileStoreInterface(ILocalFileStore local)
        {
            _local = local;
            _local.OnChanged += LocalOnChanged;
        }

        #region Private Methods
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
            throw new NotImplementedException();
        }
        //TODO: should this be a request?
        public bool TrySetItemLastModified(string path, DateTime lastModified)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Gets an item handle for a local item, renaming conflicts as required
        /// </summary>
        /// <param name="path">the path of the item to steal</param>
        /// <param name="sha1">the local file will be renamed if this does not match the existing item's sha1</param>
        /// <param name="itemHandle">the item handle (with write access) at the given path</param>
        /// <returns>whether the retrieved item can be modified</returns>
        /// <remarks>if returns false, then the user will have to intervene (choosing to keep local/remote, prompting to close application, etc)</remarks>
        public bool TryStealItemHandle(string path, string sha1, out ILocalItemHandle itemHandle)
        {
            /*
             * If we rename the local file automatically when the sha1 sums don't match up, then there is no way for the user to intervene.
             * If the item cannot be opened due to it being currently opened by another editor/otherwise being blocked, then the stream will
             *      be null, but there is currently no other way to signal to the BufferedRemoteFileStore that the item should not be immediately
             *      overwritten.  
             *      
             *      So there must be some way to signal the remote with at least the capibilities to distinguish the difference between:
             *          -A file that has been modified locally since the downloading of updates
             *          -A file that currently cannot be opened for writing because it is being blocked
             *          
             *          
             *          
             *      Maybe, "RequestItemHandle" is a better option because it puts this class in the position of notifying the user for 
             *          intervention before it gets to the remote request.  Or maybe requesting the stream is better because that
             *          guarantees blocking of the file while we wait.
             */
            throw new NotImplementedException();
        }
        public bool ItemExists(string path)
        {
            throw new NotImplementedException();
        }

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
        public int RequestWritableStream(string path, string sha1)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Write, path, new RequestWritableStreamExtraData(sha1)));
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
        public int RequestFolderCreate(string path)
        {
            return EnqueueRequest(new FileStoreRequest(ref _requestId, FileStoreRequest.RequestType.Create, path, null));
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

        public async Task SaveNonSyncFile(string path, string content)
        {
            TryGetItemHandle(path, out ILocalItemHandle itemHandle);
            using (var writableStream = itemHandle.GetWritableStream())
            {
                using (var contentStream = content.ToStream(Encoding.UTF8))
                {
                    await contentStream.CopyToStreamAsync(writableStream);
                }
            }
            _local.SetItemAttributes(path, FileAttributes.Hidden);
        }


        /// <summary>
        /// Waits for the given request status to be <see cref="FileStoreRequest.RequestStatus.Cancelled"/>
        ///  or <see cref="FileStoreRequest.RequestStatus.Success"/>
        /// </summary>
        /// <param name="requestId">the id of the request to wait for</param>
        /// <returns>the request with that id</returns>
        public async Task<FileStoreRequest> AwaitRequest(int requestId)
        {
            throw new NotImplementedException();
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
