using MyOneDriveClient.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ActiveSyncFileStore : IDisposable
    {
        private Task _syncTask = null;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private IEnumerable<string> _blacklist;
        private int _syncPeriod;
        private IRemoteFileStoreConnection _remote;
        private IRemoteFileStoreDownload _local;
        
        public IEnumerable<string> Blacklist { get => _blacklist; }
        public int SyncPeriod { get => _syncPeriod; }

        /// <summary>
        /// Creates a new instance of the ActiveSyncFileStore
        /// </summary>
        /// <param name="pathRoot">the root path to store files</param>
        /// <param name="blacklist">the list of files that should not be synchronized</param>
        /// <param name="syncPeriod">the time duration in ms between sync attempts</param>
        public ActiveSyncFileStore(string pathRoot, IEnumerable<string> blacklist, IRemoteFileStoreDownload local, IRemoteFileStoreConnection remote, int syncPeriod = 300000)
        {
            //_itemIdPathMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(itemIdMapJson);
            _syncPeriod = syncPeriod;
            _blacklist = blacklist;
            _remote = remote;
            _local = local;
        }
        
        private string GetParentItemPath(string path)
        {
            var parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string ret = "";
            for(int i = 0; i < parts.Length - 1; ++i)
            {
                ret = $"/{ret}{parts[i]}";
            }
            return ret;
        }

        private string GetItemName(string path)
        {
            return path.Split(new char[] { '/' }).Last();
        }
        
        private ConcurrentQueue<LocalFileStoreEventArgs> _localChangeQueue = new ConcurrentQueue<LocalFileStoreEventArgs>();
        private async Task LocalChangeEventHandler(LocalFileStoreEventArgs e)
        {
            if ((e.InnerEventArgs.ChangeType & WatcherChangeTypes.Created) != 0)
            {
                //new item
                string id = await _remote.UploadFileAsync(e.Item.Path, await e.Item.GetFileDataAsync());
            }
            else if ((e.ChangeType & WatcherChangeTypes.Deleted) != 0)
            {
                //deleted item
                string id = IdFromLocalPath(path);
                _remote.DeleteItemByIdAsync(id).Wait();
                _itemIdPathMap.Remove(id);
            }
            else if ((e.ChangeType & WatcherChangeTypes.Renamed) != 0)
            {
                //renamed item
                string json = $"{{  \"name\": \"{e.Name}\"  }}";
                string id = IdFromLocalPath(path);
                //TODO: how do we know what the OLD name/path was BEFORE the rename?
                _remote.UpdateItemByIdAsync(id, json).Wait();
                _itemIdPathMap[id] = "";//????
            }
            else if ((e.ChangeType & WatcherChangeTypes.Changed) != 0)
            {
                //changes to conents of a file
                string parentId = _itemIdPathMap[GetParentItemPath(path)];
                string name = GetItemName(path);

                _remote.UploadFileByIdAsync(parentId, name, LoadFileAsync(path).Result).Wait();
            }
        }
        private void ApplyLocalChanges()
        {
            LocalFileStoreEventArgs e;
            while (_localChangeQueue.TryDequeue(out e))
            {
                LocalChangeEventHandler(e);
            }
        }

        private DeltaPage _lastDeltaPage = null;
        private void ApplyAllDeltas()
        {
            do
            {
                _lastDeltaPage = _remote.GetDeltasPageAsync(_lastDeltaPage).Result;
                
                foreach(var delta in _lastDeltaPage)
                {
                    //update file/folder renames in BOTH the file system AND the itemIdMap
                    
                    bool result = _local.ItemExists(delta.ItemHandle);
                    if(!result)
                    {
                        //if the item doesn't exist, create it/download it
                        if(delta.ItemHandle.IsFolder)
                        {
                            //create folder
                            _local.CreateLocalFolder(delta.ItemHandle.Path);
                        }
                        else
                        {
                            //download the file
                            _local.SaveFileAsync(delta.ItemHandle).Wait();
                        }
                    }
                    else
                    {
                        //the item does exist, but what do we do with it?
                        string remoteName = delta.ItemHandle.Path;
                        var localItem = _local.GetFileHandleAsync(delta.ItemHandle).Result;
                        string localName = localItem.Path;
                        if(localName != remoteName)
                        {
                            if (delta.ItemHandle.IsFolder)
                            {
                                _local.MoveLocalItemAsync(delta.ItemHandle).Wait();
                            }
                            else
                            {
                                //different paths/names, so check sha1sum to see if we need to just move it, or actually download the new version
                                string localSHA1 = _local.GetLocalSHA1Async(delta.ItemHandle.Id).Result;
                                if (localSHA1 != delta.ItemHandle.SHA1Hash)
                                {
                                    //different hashes, so delete the old file and download the new
                                    _local.DeleteLocalItemAsync(delta.ItemHandle).Wait();
                                    _local.SaveFileAsync(delta.ItemHandle).Wait();
                                }
                                else
                                {
                                    //same file, different location, so move it
                                    _local.MoveLocalItemAsync(delta.ItemHandle).Wait();
                                }
                            }

                            //different location regardless, so update the dictionary
                            //_itemIdPathMap[delta.ItemHandle.Id] = remoteName;
                        }
                        else
                        {
                            //same path/names, so just download the new version
                            _local.SaveFileAsync(delta.ItemHandle).Wait();
                        }
                    }
                }
            } while (_lastDeltaPage?.NextPage != null);

            //to save memory, clear the metadatas
            _lastDeltaPage.Clear();
        }
        
        private void UploadLocalChanges()
        {

        }

        private Task SyncTask(CancellationToken ct)
        {
            return new Task(()=>
            {
                //save custom metadata document that has: {id, path, remoteLastModified} and compare remoteLastModified against each local file's last modified
                // ON FIRST TIME
                UploadLocalChanges();

                //register for file/folder changes
                _local.OnUpdate += (object sender, LocalFileStoreEventArgs e) =>
                {
                    return new Task(() =>
                    {
                        _localChangeQueue.Enqueue(e);
                    });
                };

                while (!ct.IsCancellationRequested)
                {
                    //FindNewLocalItems();
                    ApplyAllDeltas();
                    ApplyLocalChanges();

                    Thread.Sleep(_syncPeriod);
                }
            }, ct);
        }

        public void StartSyncTask()
        {
            if (_syncTask != null)
                return;
            
            //reset the cancellation token
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            //start the task
            _syncTask = SyncTask(_cts.Token);
            _syncTask.Start();
        }

        public async Task StopSyncTaskAsync()
        {
            if (_syncTask == null)
                return;

            //cancel the token
            _cts.Cancel();

            //wait for the task to finish
            await _syncTask;

            _syncTask.Dispose();
            _syncTask = null;
        }

        /// <summary>
        /// does not use one drive "delta" feature and iterates
        /// all files and compares last modified times of local
        /// and remote files, keeping the most up-to-date ones
        /// </summary>
        public void HardForceUpdate(string folder)
        {

        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cts?.Cancel();
                    _syncTask?.Wait();
                    _cts?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ActiveSyncFileStore() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion


    }
}
