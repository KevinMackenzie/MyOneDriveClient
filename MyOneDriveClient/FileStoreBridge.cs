using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class FileStoreBridge
    {
        #region Private Fields
        private IEnumerable<string> _blacklist;
        private LocalFileStoreInterface _local;
        private BufferedRemoteFileStoreInterface _remote;
        private const string RemoteMetadataCachePath = ".remotemetadata";
        private const string LocalMetadataCachePath = ".localmetadata";
        private ConcurrentDictionary<int, IItemHandle> _downloadRequests = new ConcurrentDictionary<int, IItemHandle>();
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
        private async Task OnRemoteRequestStatusChanged(object sender, RequestStatusChangedEventArgs requestStatusChangedEventArgs)
        {
            switch (requestStatusChangedEventArgs.Status)
            {
                case FileStoreRequest.RequestStatus.Success:
                    //downloaded file maybe
                    if (_downloadRequests.TryGetValue(requestStatusChangedEventArgs.RequestId, out IItemHandle value))
                    {
                        //yes, so set the local last modified
                        _local.TrySetItemLastModified(value.Path, value.LastModified);

                        //and remove it from the download list
                        _downloadRequests.TryRemove(requestStatusChangedEventArgs.RequestId, out var other);
                    }
                    break;
            }
        }
        private async Task OnLocalRequestStatusChanged(object sender, RequestStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case FileStoreRequest.RequestStatus.Success:
                    //folder created maybe
                    if (_downloadRequests.TryGetValue(e.RequestId, out IItemHandle value))
                    {
                        //yes, so set the local last modified
                        _local.TrySetItemLastModified(value.Path, value.LastModified);

                        //and remove it from the download list
                        _downloadRequests.TryRemove(e.RequestId, out var other);
                    }
                    break;
                case FileStoreRequest.RequestStatus.Pending:
                    break;
                case FileStoreRequest.RequestStatus.InProgress:
                    break;
                case FileStoreRequest.RequestStatus.Failure:
                    break;
                case FileStoreRequest.RequestStatus.Cancelled:
                    break;
                case FileStoreRequest.RequestStatus.WaitForUser:
                    break;
            }
        }
        private bool IsBlacklisted(string path)
        {
            var len = path.Length;
            return _blacklist.Where(item => item.Length <= len).Any(item => path.Substring(item.Length) == item);
        }

        private async Task CreateOrDownloadFileAsync(ItemDelta delta)
        {
            var streamRequest = await _local.AwaitRequest(_local.RequestWritableStream(delta.Handle.Path, delta.Handle.SHA1Hash));
            if (streamRequest.Status == FileStoreRequest.RequestStatus.Cancelled)
            {
                //cancelled request, so TODO KEEP LOCAL ...
            }
            else if (streamRequest.Status == FileStoreRequest.RequestStatus.Success)
            {
                //successful request, so overwrite/create local
                var extraData = streamRequest.ExtraData as LocalFileStoreInterface.RequestStreamExtraData;
                if (extraData != null)
                {
                    _downloadRequests[_remote.RequestFileDownload(delta.Handle.Path, extraData.Stream)] =
                        delta.Handle;
                }
                else
                {
                    Debug.WriteLine($"Request data from stream request \"{delta.Handle.Path}\" was not a stream!");
                }
            }
            else
            {
                Debug.WriteLine($"Awaited stream request for \"{delta.Handle.Path}\" was neither successful nor cancelled!");
            }
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
            if (_local.TryGetItemHandle(RemoteMetadataCachePath, out ILocalItemHandle itemHandle))
            {
                //TODO: this does NO exception handling
                _remote.MetadataCache = await (await itemHandle.GetFileDataAsync()).ReadAllToStringAsync(Encoding.UTF8);
            }
        }
        private async Task LoadLocalItemMetadataCacheAsync()
        {
            //if the metadata doesn't exist, then it's a new install
            if (_local.TryGetItemHandle(LocalMetadataCachePath, out ILocalItemHandle itemHandle))
            {
                //TODO: this does NO exception handling
                _local.MetadataCache = await (await itemHandle.GetFileDataAsync()).ReadAllToStringAsync(Encoding.UTF8);
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

                        //get the read stream from the local
                        var readStream = await _local.AwaitRequest(_local.RequestReadOnlyStream(delta.Handle.Path));

                        if (readStream.Status == FileStoreRequest.RequestStatus.Cancelled)
                        {
                            //should this even be an option for the user?
                        }
                        else if(readStream.Status == FileStoreRequest.RequestStatus.Success)
                        {
                            //successfully got the read stream
                            var streamData = readStream.ExtraData as LocalFileStoreInterface.RequestStreamExtraData;
                            if (streamData != null)
                            {
                                _remote.RequestUpload(delta.Handle.Path, streamData.Stream);
                            }
                            else
                            {
                                Debug.WriteLine($"Request data from stream request \"{delta.Handle.Path}\" was not a stream!");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Awaited stream request for \"{delta.Handle.Path}\" was neither successful nor cancelled!");
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _remote.RequestDelete(delta.OldPath);// item handle doesn't exist, so use old path
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _remote.RequestRename(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _remote.RequestMove(delta.OldPath, delta.Handle.Path);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            await SaveRemoteItemMetadataCacheAsync();
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
                            if (!_local.TryGetItemHandle(delta.Handle.Path, out ILocalItemHandle itemHandle))
                            {
                                //... that doesn't exist, so create it
                                _local.RequestFolderCreate(delta.Handle.Path);
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
                            await CreateOrDownloadFileAsync(delta);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _local.RequestDeleteItem(delta.Handle.Path);
                        break;
                    case ItemDelta.DeltaType.Modified:
                        if (delta.Handle.IsFolder)
                        {
                            //item is a folder ... so set it's last modified
                            _local.TrySetItemLastModified(delta.Handle.Path, delta.Handle.LastModified);
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
                            await CreateOrDownloadFileAsync(delta);

                            ////////// END CODE UNDER MAINTINANCE
                        }
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _local.RequestMoveItem(delta.OldPath, delta.Handle.Path);
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _local.RequestRenameItem(delta.OldPath, PathUtils.GetItemName(delta.Handle.Path));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
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