using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ActiveSyncFileStore : ILocalFileStore, IDisposable
    {
        private Task _syncTask = null;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private IEnumerable<string> _blacklist;
        private string _pathRoot = "";
        private int _syncPeriod;
        private IRemoteFileStoreConnection _remote;
        private Dictionary<string, string> _itemIdPathMap = null;

        public string PathRoot { get => _pathRoot; }
        public IEnumerable<string> Blacklist { get => _blacklist; }
        public int SyncPeriod { get => _syncPeriod; }

        /// <summary>
        /// Creates a new instance of the ActiveSyncFileStore
        /// </summary>
        /// <param name="pathRoot">the root path to store files</param>
        /// <param name="blacklist">the list of files that should not be synchronized</param>
        /// <param name="syncPeriod">the time duration in ms between sync attempts</param>
        public ActiveSyncFileStore(string pathRoot, IEnumerable<string> blacklist, IRemoteFileStoreConnection remote, string itemIdMapJson, int syncPeriod = 300000)
        {
            _itemIdPathMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(itemIdMapJson);
            _pathRoot = pathRoot;
            _syncPeriod = syncPeriod;
            _blacklist = blacklist;
            _remote = remote;
        }

        #region ILocalFileStore
        public Task<Stream> LoadFileAsync(string localPath)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string localPath, Stream data)
        {
            throw new NotImplementedException();
        }
        #endregion
        

        private void CreateLocalFolder(string folderPath)
        {
            throw new NotImplementedException();
        }

        private void DownloadFileToLocal(IRemoteItemHandle itemHandle)
        {
            throw new NotImplementedException();
        }

        private string GetLocalSHA1(string id)
        {
            throw new NotImplementedException();
        }

        private void DeleteLocalFile(string localPath)
        {
            throw new NotImplementedException();
        }

        private void MoveLocalFile(string localPath, string newLocalPath)
        {
            throw new NotImplementedException();
        }

        FileSystemWatcher watcher = new FileSystemWatcher();
        private void SubscribeToLocalChanges()
        {
            watcher.Path = _pathRoot;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*";
            watcher.Changed += new FileSystemEventHandler(
                (object sender, FileSystemEventArgs e) => 
                {

                });
            watcher.EnableRaisingEvents = true;//do we want this?
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
                    string localName;
                    bool result = _itemIdPathMap.TryGetValue(delta.ItemHandle.Id, out localName);
                    if(!result)
                    {
                        //if the item doesn't exist, create it/download it
                        if(delta.ItemHandle.IsFolder)
                        {
                            //create folder
                            CreateLocalFolder(delta.ItemHandle.Path);
                        }
                        else
                        {
                            //download the file
                            DownloadFileToLocal(delta.ItemHandle);
                        }
                    }
                    else
                    {
                        //the item does exist, but what do we do with it?
                        string remoteName = delta.ItemHandle.Path;
                        if(localName != remoteName)
                        {
                            //different paths/names, so check sha1sum to see if we need to just move it, or actually download the new version
                            string localSHA1 = GetLocalSHA1(delta.ItemHandle.Id);
                            if(localSHA1 != delta.ItemHandle.SHA1Hash)
                            {
                                //different hashes, so delete the old file and download the new
                                DeleteLocalFile(localName);
                            }
                            else
                            {
                                //same file, different location, so move it
                                MoveLocalFile(localName, remoteName);
                            }

                            //different location regardless, so update the dictionary
                            _itemIdPathMap[delta.ItemHandle.Id] = remoteName;
                        }
                        else
                        {
                            //same path/names, so just download the new version
                            DownloadFileToLocal(delta.ItemHandle);
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
                SubscribeToLocalChanges();

                while (!ct.IsCancellationRequested)
                {
                    ApplyAllDeltas();

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
        public void HardForceUpdate()
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
                    watcher?.Dispose();
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
