using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        #endregion

        public FileStoreBridge(IEnumerable<string> blacklist, LocalFileStoreInterface local,
            BufferedRemoteFileStoreInterface remote)
        {
            _blacklist = blacklist;
            _local = local;
            _remote = remote;
        }

        #region Private Methods
        private bool IsBlacklisted(string path)
        {
            var len = path.Length;
            return _blacklist.Where(item => item.Length <= len).Any(item => path.Substring(item.Length) == item);
        }
        #endregion

        #region Public Properties
        #endregion

        #region Public Methods
        public void ForceLocalChanges()
        {
            //TODO: this uses a deep search method instead of cumulative changes   
        }
        public void ApplyLocalChanges()
        {
            var localDeltas = _local.GetDeltas();
            foreach (var delta in localDeltas)
            {
                if(IsBlacklisted(delta.Path))
                    continue;

                switch (delta.Type)
                {
                    case ItemDelta.DeltaType.ModifiedOrCreated:
                        if (_local.TryGetItemHandle(delta.Path, out ILocalItemHandle itemHandle))
                        {
                            if (itemHandle.IsFolder)
                            {
                                _remote.RequestFolderCreate(delta.Path);
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
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _remote.RequestDelete(delta.Path);
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _remote.RequestRename(delta.OldPath, PathUtils.GetItemName(delta.Path));
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _remote.RequestMove(delta.OldPath, delta.Path);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        public void ApplyRemoteChanges()
        {
            var remoteDeltas = _remote.RequestDeltas();
            foreach (var delta in remoteDeltas)
            {
                if (IsBlacklisted(delta.Path))
                    continue;

                switch (delta.Type)
                {
                    case ItemDelta.DeltaType.ModifiedOrCreated:
                        if (delta.IsFolder)
                        {
                            if (!_local.TryGetItemHandle(delta.Path, out ILocalItemHandle itemHandle))
                            {
                                _local.RequestFolderCreate(delta.Path);
                            }
                        }
                        else
                        {
                            if (_local.TryGetWritableStream(delta.Path, out Stream writableStream))
                            {
                                _remote.RequestFileDownload(delta.Path, writableStream);
                            }
                            else
                            {
                                //TODO: when does this happen?
                            }
                        }
                        break;
                    case ItemDelta.DeltaType.Deleted:
                        _local.RequestDeleteItem(delta.Path);
                        break;
                    case ItemDelta.DeltaType.Renamed:
                        _local.RequestMoveItem(delta.OldPath, delta.Path);
                        break;
                    case ItemDelta.DeltaType.Moved:
                        _local.RequestMoveItem(delta.OldPath, delta.Path);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        public async Task SaveRemoteItemMetadataCacheAsync()
        {
            await _local.SaveNonSyncFile(RemoteMetadataCachePath, _remote.MetadataCache);
        }
        public async Task SaveLocalItemMetadataCacheAsync()
        {
            await _local.SaveNonSyncFile(LocalMetadataCachePath, _local.MetadataCache);
        }
        #endregion
    }
}