using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage
{
    public class FileStoreBridge
    {
        #region Private Fields
        private IEnumerable<string> _blacklist;
        private ILocalFileStoreInterface _local;
        private IRemoteFileStoreInterface _remote;
        private const string RemoteMetadataCachePath = ".remotemetadata";
        private const string LocalMetadataCachePath = ".localmetadata";
        private ConcurrentDictionary<int, IItemHandle> _uploadRequests = new ConcurrentDictionary<int, IItemHandle>();
        #endregion

        public FileStoreBridge(IEnumerable<string> blacklist, ILocalFileStoreInterface local,
            IRemoteFileStoreInterface remote)
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
                case RequestStatus.Success:
                    break;
                case RequestStatus.Pending:
                    break;
                case RequestStatus.InProgress:
                    break;
                case RequestStatus.Cancelled:
                    break;
                case RequestStatus.WaitForUser:
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

        private async Task CreateOrDownloadFile(IItemDelta delta, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var sha1 = await delta.Handle.GetSha1HashAsync(ct);
            if(ct.IsCancellationRequested)
                return;
            _local.RequestWritableStream(delta.Handle.Path, sha1, delta.Handle.LastModified, OnGetWritableStream);
        }

        private bool AssertStreamStatus(FileStoreRequest request, out LocalFileStoreInterface.RequestStreamExtraData streamData)
        {
            if (request.Status == RequestStatus.Cancelled)
            {
                //should this even be an option for the user?
            }
            else if (request.Status == RequestStatus.Success)
            {
                //successfully got the read stream
                streamData = request.ExtraData as LocalFileStoreInterface.RequestStreamExtraData;
                if (streamData != null)
                {
                    return true;
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
            streamData = null;
            return false;
        }

        private async Task OnGetReadOnlyStream(FileStoreRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!AssertStreamStatus(request, out LocalFileStoreInterface.RequestStreamExtraData streamData)) return;
            
            await _remote.RequestUploadImmediateAsync(request.Path, streamData.Stream, ct);
        }
        private void OnGetReadOnlyStream(FileStoreRequest request)
        {
            if (!AssertStreamStatus(request, out LocalFileStoreInterface.RequestStreamExtraData streamData)) return;

            _remote.RequestUpload(request.Path, streamData.Stream);
        }

        private async Task OnGetWritableStream(FileStoreRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!AssertStreamStatus(request, out LocalFileStoreInterface.RequestStreamExtraData streamData)) return;

            await _remote.RequestFileDownloadImmediateAsync(request.Path, streamData.Stream, ct);
        }
        private void OnGetWritableStream(FileStoreRequest request)
        {
            if (!AssertStreamStatus(request, out LocalFileStoreInterface.RequestStreamExtraData streamData)) return;

            _remote.RequestFileDownload(request.Path, streamData.Stream);
        }

        private async Task UploadImmediateAsync(string path, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var result = await _local.RequestReadOnlyStreamImmediateAsync(path, ct);

            ct.ThrowIfCancellationRequested();

            await OnGetReadOnlyStream(result, ct);
        }

        private async Task SaveRemoteItemMetadataCacheAsync(CancellationToken ct)
        {
            await _local.SaveNonSyncFile(RemoteMetadataCachePath, _remote.MetadataCache, ct);
        }
        private async Task SaveLocalItemMetadataCacheAsync(CancellationToken ct)
        {
            await _local.SaveNonSyncFile(LocalMetadataCachePath, _local.MetadataCache, ct);
        }
        private async Task LoadRemoteItemMetadataCacheAsync(CancellationToken ct)
        {
            //if the metadata doesn't exist, then it's a new install
            if (_local.ItemExists(RemoteMetadataCachePath))
            {
                //TODO: this does NO exception handling
                _remote.MetadataCache = await _local.ReadNonSyncFile(RemoteMetadataCachePath, ct);
            }
        }
        private async Task LoadLocalItemMetadataCacheAsync(CancellationToken ct)
        {
            //if the metadata doesn't exist, then it's a new install
            if (_local.ItemExists(LocalMetadataCachePath))
            {
                //TODO: this does NO exception handling
                _local.MetadataCache = await _local.ReadNonSyncFile(LocalMetadataCachePath, ct);
            }
        }
        #endregion

        #region Public Properties
        #endregion

        #region Public Methods
        public async Task ForceLocalChangesAsync(CancellationToken ct)
        {
            //TODO: this uses a deep search method instead of cumulative changes   
            throw new NotImplementedException();
        }
        public async Task GenerateLocalMetadataAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            //gets deltas, but doesn't do anything with them
            await _local.GetDeltasAsync(true, ct);
        }
        public async Task<bool> ApplyLocalChangesAsync(PauseToken pt)
        {
            await pt.WaitWhilePausedAsync();
            var ct = pt.CancellationToken;

            var localDeltas = await _local.GetDeltasAsync(false, ct);
            var any = false;

            foreach (var delta in localDeltas)
            {
                any = true;

                ct.ThrowIfCancellationRequested();

                if (IsBlacklisted(delta.Handle.Path))
                    continue;

                switch (delta.Type)
                {
                    case DeltaType.Modified:
                    case DeltaType.Created:
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
                    case DeltaType.Deleted:
                        _remote.RequestDelete(delta.OldPath);// item handle doesn't exist, so use old path
                        break;
                    case DeltaType.Renamed:
                        _remote.RequestRename(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    case DeltaType.Moved:
                        _remote.RequestMove(delta.OldPath, PathUtils.GetParentItemPath(delta.Handle.Path));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            //also process the local requests, because we may have made
            //  some writable stream requests
            while (!await _local.ProcessQueueAsync(pt))
            {
                //TODO: wait for user intervention
                await Utils.DelayNoThrow(TimeSpan.FromSeconds(1), ct);
            }

            while (!await _remote.ProcessQueueAsync(pt))
            {
                //TODO: wait for user intervention
                await Utils.DelayNoThrow(TimeSpan.FromSeconds(1), ct);
            }

            return any;
        }
        public async Task<bool> ApplyRemoteChangesAsync(PauseToken pt)
        {
            await pt.WaitWhilePausedAsync();
            var ct = pt.CancellationToken;

            var remoteDeltas = await _remote.RequestDeltasAsync(ct);
            var any = false;

            foreach (var delta in remoteDeltas)
            {
                any = true;
                ct.ThrowIfCancellationRequested();

                if (IsBlacklisted(delta.Handle.Path))
                    continue;

                switch (delta.Type)
                {
                    case DeltaType.Created:
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
                            await CreateOrDownloadFile(delta, ct);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case DeltaType.Deleted:
                        _local.RequestDelete(delta.Handle.Path, delta.Handle.LastModified);
                        break;
                    case DeltaType.Modified:
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
                            await CreateOrDownloadFile(delta, ct);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case DeltaType.Renamed:
                        _local.RequestRename(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    case DeltaType.Moved:
                        _local.RequestMove(delta.OldPath, PathUtils.GetParentItemPath(delta.Handle.Path));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            while (!await _local.ProcessQueueAsync(pt))
            {
                //TODO: wait for user intervention
                await Utils.DelayNoThrow(TimeSpan.FromSeconds(1), ct);
            }
            
            //also process the remote requests, because we may have made
            //  some download requests
            while (!await _remote.ProcessQueueAsync(pt))
            {
                //TODO: wait for user intervention
                await Utils.DelayNoThrow(TimeSpan.FromSeconds(1), ct);
            }

            return any;
        }

        public async Task ResolveLocalConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
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
                            case RequestType.Delete: //don't delete the local file and upload it
                            case RequestType.Write:
                                //cancel the download and submit an upload request
                                await UploadImmediateAsync(request.Path, ct);
                                break;
                            case RequestType.Rename:
                                //don't rename the file and upload both local files
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await UploadImmediateAsync(request.Path, ct);
                                    await UploadImmediateAsync(PathUtils.GetRenamedPath(request.Path, extraData.NewName), ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Move:
                                //don't move the file and upload both local files
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await UploadImmediateAsync(request.Path, ct);
                                    await UploadImmediateAsync($"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}", ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Create:
                            case RequestType.Read:
                            default:
                                break;
                        }
                        break;
                    case FileStoreInterface.ConflictResolutions.KeepRemote:
                        switch (request.Type)
                        {
                            case RequestType.Delete: //delete the local file
                            case RequestType.Write:
                                //delete local file
                                await _local.RequestDeleteItemImmediateAsync(request.Path, ct);
                                break;
                            case RequestType.Rename:
                                //Delete the destination file
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestDeleteItemImmediateAsync(PathUtils.GetRenamedPath(request.Path, extraData.NewName), ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Move:
                                //Delete the destination file
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestDeleteItemImmediateAsync($"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}", ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Create:
                            case RequestType.Read:
                            default:
                                break;
                        }
                        _local.SignalConflictResolved(request.RequestId);
                        break;
                    case FileStoreInterface.ConflictResolutions.KeepBoth:
                        switch (request.Type)
                        {
                            case RequestType.Write:
                                //rename the local file
                                await _local.RequestRenameItemImmediateAsync(request.Path,
                                    PathUtils.InsertDateTime(PathUtils.GetItemName(request.Path), DateTime.UtcNow), ct);
                                break;
                            case RequestType.Rename:
                                //rename the existing destination file
                            {
                                var extraData = (request.ExtraData as RequestRenameExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestRenameItemImmediateAsync(
                                        PathUtils.GetRenamedPath(request.Path, extraData.NewName),
                                        PathUtils.InsertDateTime(PathUtils.GetItemName(request.Path), DateTime.UtcNow), ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Rename request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Move:
                                //rename the existing destination file
                            {
                                var extraData = (request.ExtraData as RequestMoveExtraData);
                                if (extraData != null)
                                {
                                    await _local.RequestRenameItemImmediateAsync(
                                        $"{extraData.NewParentPath}/{PathUtils.GetItemName(request.Path)}",
                                        PathUtils.InsertDateTime(PathUtils.GetItemName(request.Path), DateTime.UtcNow), ct);
                                }
                                else
                                {
                                    Debug.WriteLine("Move request was called without approprate extra data");
                                }
                            }
                                break;
                            case RequestType.Delete:
                            case RequestType.Create:
                            case RequestType.Read:
                            default:
                                break;
                        }
                        _local.SignalConflictResolved(request.RequestId);
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
        public async Task ResolveRemoteConflictAsync(int requestId, FileStoreInterface.ConflictResolutions resolution, CancellationToken ct)
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
        public async Task LoadMetadataAsync(CancellationToken ct)
        {
            var task1 = LoadRemoteItemMetadataCacheAsync(ct);
            var task2 = LoadLocalItemMetadataCacheAsync(ct);
            await task1;
            await task2;
        }
        /// <summary>
        /// Saves the metadata to the disc
        /// </summary>
        /// <returns></returns>
        public async Task SaveMetadataAsync(CancellationToken ct)
        {
            var task1 = SaveRemoteItemMetadataCacheAsync(ct);
            var task2 = SaveLocalItemMetadataCacheAsync(ct);
            await task1;
            await task2;
        }
        #endregion
    }
}