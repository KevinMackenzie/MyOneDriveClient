using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage
{
    public class FileStoreBridge
    {
        #region Private Fields
        private IEnumerable<string> _blacklist;
        private LocalFileStoreInterface _local;
        private BufferedRemoteFileStoreInterface _remote;
        private const string RemoteMetadataCachePath = ".remotemetadata";
        private const string LocalMetadataCachePath = ".localmetadata";
        private ConcurrentDictionary<int, IItemHandle> _uploadRequests = new ConcurrentDictionary<int, IItemHandle>();
        #endregion

        public FileStoreBridge(IEnumerable<string> blacklist, LocalFileStoreInterface local,
            BufferedRemoteFileStoreInterface remote)
        {
            _blacklist = blacklist;
            _local = local;
            _remote = remote;

            _local.OnRequestStatusChanged += OnLocalRequestStatusChanged;
            _remote.OnRequestStatusChanged += OnRemoteRequestStatusChanged;
        }

        #region Private Methods
        private void OnRemoteRequestStatusChanged(object sender, RequestStatusChangedEventArgs requestStatusChangedEventArgs)
        {
        }
        private void OnLocalRequestStatusChanged(object sender, RequestStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case FileStoreRequest.RequestStatus.Success:
                    break;
                case FileStoreRequest.RequestStatus.Pending:
                    break;
                case FileStoreRequest.RequestStatus.InProgress:
                    break;
                case FileStoreRequest.RequestStatus.Cancelled:
                    break;
                case FileStoreRequest.RequestStatus.WaitForUser:
                    break;
            }
        }
        private bool IsBlacklisted(string path)
        {
            //hopefully it never gets to this point, but it is a good thing to check
            if (path == RemoteMetadataCachePath || path == LocalMetadataCachePath)
                return true;

            var len = path.Length;
            return _blacklist.Where(item => item.Length <= len).Any(item => path.Substring(item.Length) == item);
        }

        private async Task CreateOrDownloadFile(ItemDelta delta)
        {
            _local.RequestWritableStream(delta.Handle.Path, await delta.Handle.GetSha1HashAsync(), delta.Handle.LastModified, OnGetWritableStream);
        }

        private async Task OnGetReadOnlyStream(FileStoreRequest request, bool immediate)
        {
            if (request.Status == FileStoreRequest.RequestStatus.Cancelled)
            {
                //should this even be an option for the user?
            }
            else if (request.Status == FileStoreRequest.RequestStatus.Success)
            {
                //successfully got the read stream
                var streamData = request.ExtraData as LocalFileStoreInterface.RequestStreamExtraData;
                if (streamData != null)
                {
                    if (immediate)
                    {
                        await _remote.RequestUploadImmediateAsync(request.Path, streamData.Stream);
                    }
                    else
                    {
                        _remote.RequestUpload(request.Path, streamData.Stream);
                    }
                }
                else
                {
                    Debug.WriteLine($"Request data from stream request \"{request.Path}\" was not a stream!");
                }
            }
            else
            {
                Debug.WriteLine($"Awaited stream request for \"{request.Path}\" was neither successful nor cancelled!");
            }
        }
        private void OnGetReadOnlyStream(FileStoreRequest request)
        {
            OnGetReadOnlyStream(request, false).Wait();
        }

        private async Task OnGetWritableStream(FileStoreRequest request, bool immediate)
        {
            if (request.Status == FileStoreRequest.RequestStatus.Cancelled)
            {
                //cancelled request, so that mean's we'll skip this delta... (this can happen when the local and remote files are the same or if the user choses to keep the local)
            }
            else if (request.Status == FileStoreRequest.RequestStatus.Success)
            {
                //successful request, so overwrite/create local
                var extraData = request.ExtraData as LocalFileStoreInterface.RequestStreamExtraData;
                if (extraData != null)
                {
                    if (immediate)
                    {
                        await _remote.RequestFileDownloadImmediateAsync(request.Path, extraData.Stream);
                    }
                    else
                    {
                        _remote.RequestFileDownload(request.Path, extraData.Stream);
                    }
                }
                else
                {
                    Debug.WriteLine($"Request data from stream request \"{request.Path}\" was not a stream!");
                }
            }
            else
            {
                Debug.WriteLine($"Awaited stream request for \"{request.Path}\" was neither successful nor cancelled!");
            }
        }
        private void OnGetWritableStream(FileStoreRequest request)
        {
            OnGetWritableStream(request, false).Wait();
        }

        private async Task UploadImmediateAsync(string path)
        {
            var result = await _local.RequestReadOnlyStreamImmediateAsync(path);
            await OnGetReadOnlyStream(result, true);
        }

        private async Task SaveRemoteItemMetadataCacheAsync()
        {
            await _local.SaveNonSyncFile(RemoteMetadataCachePath, _remote.MetadataCache);
        }
        private async Task SaveLocalItemMetadataCacheAsync()
        {
            await _local.SaveNonSyncFile(LocalMetadataCachePath, _local.MetadataCache);
        }
        private async Task LoadRemoteItemMetadataCacheAsync()
        {
            //if the metadata doesn't exist, then it's a new install
            if (_local.ItemExists(RemoteMetadataCachePath))
            {
                //TODO: this does NO exception handling
                _remote.MetadataCache = await _local.ReadNonSyncFile(RemoteMetadataCachePath);
            }
        }
        private async Task LoadLocalItemMetadataCacheAsync()
        {
            //if the metadata doesn't exist, then it's a new install
            if (_local.ItemExists(LocalMetadataCachePath))
            {
                //TODO: this does NO exception handling
                _local.MetadataCache = await _local.ReadNonSyncFile(LocalMetadataCachePath);
            }
        }
        #endregion

        #region Public Properties
        #endregion

        #region Public Methods
        public void ForceLocalChanges()
        {
            //TODO: this uses a deep search method instead of cumulative changes   
        }
        public async Task GenerateLocalMetadataAsync()
        {
            //gets deltas, but doesn't do anything with them
            await _local.GetDeltasAsync(true);
        }
        public async Task ApplyLocalChangesAsync()
        {
            var localDeltas = await _local.GetDeltasAsync();
            foreach (var delta in localDeltas)
            {
                if(IsBlacklisted(delta.Handle.Path))
                    continue;

                switch (delta.Type)
                {
                    case ItemDelta.DeltaType.Modified:
                    case ItemDelta.DeltaType.Created:
                        /*if (_local.TryGetItemHandle(delta.Handle.Path, out ILocalItemHandle itemHandle))
                        {
                            if (itemHandle.IsFolder)
                            {
                                _remote.RequestFolderCreate(delta.Handle.Path);
                            }
                            else
                            {
                                _remote.RequestUpload(itemHandle);
                            }
                        }
                        else
                        {
                            //TODO: how should we tell the user this?  is this a case that will actually happen once done?
                            Debug.WriteLine("Locally Created/Modified delta item does not exist locally");
                        }*/
                        if (delta.Handle.IsFolder)
                        {
                            _remote.RequestFolderCreate(delta.Handle.Path);
                        }
                        else
                        {
                            //get the read stream from the local
                            _local.RequestReadOnlyStream(delta.Handle.Path, OnGetReadOnlyStream);
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _remote.RequestDelete(delta.OldPath);// item handle doesn't exist, so use old path
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _remote.RequestRename(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _remote.RequestMove(delta.OldPath, PathUtils.GetParentItemPath(delta.Handle.Path));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            while (!await _remote.ProcessQueueAsync())
            {
                //TODO: wait for user intervention
                await Task.Delay(1000);
            }

            //also process the local requests, because we may have made
            //  some writable stream requests
            while (!await _local.ProcessQueueAsync())
            {
                //TODO: wait for user intervention
                await Task.Delay(1000);
            }
        }
        public async Task ApplyRemoteChangesAsync()
        {
            var remoteDeltas = await _remote.RequestDeltasAsync();
            foreach (var delta in remoteDeltas)
            {
                if (IsBlacklisted(delta.Handle.Path))
                    continue;

                switch (delta.Type)
                {
                    case ItemDelta.DeltaType.Created:
                        if (delta.Handle.IsFolder)
                        {
                            //item is folder...
                            if (!_local.ItemExists(delta.Handle.Path))
                            {
                                //... that doesn't exist, so create it
                                _local.RequestFolderCreate(delta.Handle.Path, delta.Handle.LastModified);
                            }
                        }
                        else
                        {
                            ///////// CODE UNDER MAINTINENCE


                            //item is file...
                            /*if (!_local.TryGetItemHandle(delta.Handle.Path, out ILocalItemHandle itemHandle))
                            {
                                //... that doesn't exist, so download it
                                var requestId = _remote.RequestFileDownload(delta.Handle.Path, itemHandle);

                                //and add it to the downloading items list
                                _downloadRequests[requestId] = delta.Handle;
                            }
                            else
                            {
                                //TODO: see comments around TryStealItemHandle for more details
                            }

                            */
                            
                            //this is the same for both creating files and downloading existing ones
                            await CreateOrDownloadFile(delta);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _local.RequestDeleteItem(delta.Handle.Path); //TODO: check timestamps
                        break;
                    case ItemDelta.DeltaType.Modified:
                        if (delta.Handle.IsFolder)
                        {
                            //item is a folder ... so set it's last modified
                            //_local.TrySetItemLastModified(delta.Handle.Path, delta.Handle.LastModified);
                        }
                        else
                        {
                            ///////// CODE UNDER MAINTINENCE


                            //item is a file ...
                            //if (_local.TryGetItemHandle(delta.Handle.Path, out ILocalItemHandle itemHandle))
                            //{
                            //    //TODO: see comments around TryStealItemHandle for more details
                            //}

                            //trying to modify and item that doesn't exist... so download it...
                            //var requestId = _remote.RequestFileDownload(delta.Handle.Path, itemHandle);

                            //and add it to the downloading items list
                            //_downloadRequests[requestId] = delta.Handle;


                            //this is the same for both creating files and downloading existing ones
                            await CreateOrDownloadFile(delta);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _local.RequestRenameItem(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _local.RequestMoveItem(delta.OldPath, PathUtils.GetParentItemPath(delta.Handle.Path));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            while (!await _local.ProcessQueueAsync())
            {
                //TODO: wait for user intervention
                await Task.Delay(1000);
            }
            
            //also process the remote requests, because we may have made
            //  some download requests
            while (!await _remote.ProcessQueueAsync())
            {
                //TODO: wait for user intervention
                await Task.Delay(1000);
            }
        }

        public async Task ResolveLocalConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution)
        {
            if (_local.TryGetRequest(requestId, out FileStoreRequest request))
            {
                if (request.Complete)
                    return;
                switch (resolution)
                {
                    case FileStoreInterface.ConflictResolutions.KeepLocal:
                        _local.CancelRequest(request.RequestId);
                        switch (request.Type)
                        {
                            case FileStoreRequest.RequestType.Delete: //don't delete the local file and upload it
                            case FileStoreRequest.RequestType.Write:
                                //cancel the download and submit an upload request
                                await UploadImmediateAsync(request.Path);
                                break;
                            case FileStoreRequest.RequestType.Rename:
                                //don't rename the file and upload both local files
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await UploadImmediateAsync(request.Path);
                                    await UploadImmediateAsync(PathUtils.GetRenamedPath(request.Path, extraData.NewName));
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Move:
                                //don't move the file and upload both local files
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await UploadImmediateAsync(request.Path);
                                    await UploadImmediateAsync($"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}");
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Create:
                            case FileStoreRequest.RequestType.Read:
                            default:
                                break;
                        }
                        break;
                    case FileStoreInterface.ConflictResolutions.KeepRemote:
                        switch (request.Type)
                        {
                            case FileStoreRequest.RequestType.Delete: //delete the local file
                            case FileStoreRequest.RequestType.Write:
                                //delete local file
                                await _local.RequestDeleteItemImmediateAsync(request.Path);
                                break;
                            case FileStoreRequest.RequestType.Rename:
                                //Delete the destination file
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestDeleteItemImmediateAsync(PathUtils.GetRenamedPath(request.Path, extraData.NewName));
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Move:
                                //Delete the destination file
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestDeleteItemImmediateAsync($"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}");
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Create:
                            case FileStoreRequest.RequestType.Read:
                            default:
                                break;
                        }
                        _local.ResolveRequest(request.RequestId);
                        break;
                    case FileStoreInterface.ConflictResolutions.KeepBoth:
                        switch (request.Type)
                        {
                            case FileStoreRequest.RequestType.Write:
                                //rename the local file
                                await _local.RequestRenameItemImmediateAsync(request.Path,
                                    PathUtils.InsertString(request.Path, DateTime.UtcNow.ToString()));
                                break;
                            case FileStoreRequest.RequestType.Rename:
                                //rename the existing destination file
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestRenameItemImmediateAsync(
                                        PathUtils.GetRenamedPath(request.Path, extraData.NewName),
                                        PathUtils.InsertString(request.Path, DateTime.UtcNow.ToString()));
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Move:
                                //rename the existing destination file
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestRenameItemImmediateAsync(
                                        $"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}",
                                        PathUtils.InsertString(request.Path, DateTime.UtcNow.ToString()));
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case FileStoreRequest.RequestType.Delete:
                            case FileStoreRequest.RequestType.Create:
                            case FileStoreRequest.RequestType.Read:
                            default:
                                break;
                        }
                        _local.ResolveRequest(request.RequestId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
                }
            }
            else
            {
                Debug.WriteLine($"Cannot resolve local conflict with request id: {requestId} because it could not be found");
            }
        }
        public async Task ResolveRemoteConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution)
        {
            throw new NotImplementedException();
            //TODO: at this point no remote requests have conflict handling...
            switch (resolution)
            {
                case FileStoreInterface.ConflictResolutions.KeepLocal:
                    break;
                case FileStoreInterface.ConflictResolutions.KeepRemote:
                    break;
                case FileStoreInterface.ConflictResolutions.KeepBoth:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }

        /// <summary>
        /// Loads the metadata from the disc
        /// </summary>
        /// <returns></returns>
        public async Task LoadMetadataAsync()
        {
            var task1 = LoadRemoteItemMetadataCacheAsync();
            var task2 = LoadLocalItemMetadataCacheAsync();
            await task1;
            await task2;
        }
        /// <summary>
        /// Saves the metadata to the disc
        /// </summary>
        /// <returns></returns>
        public async Task SaveMetadataAsync()
        {
            var task1 = SaveRemoteItemMetadataCacheAsync();
            var task2 = SaveLocalItemMetadataCacheAsync();
            await task1;
            await task2;
        }
        #endregion
    }
}