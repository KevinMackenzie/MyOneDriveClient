using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class DownloadedFileStore : IRemoteFileStoreDownload, IDisposable
    {
        public DownloadedFileStore(string pathRoot)
        {
            if (pathRoot.Last() == '/')
                PathRoot = pathRoot;
            else
                PathRoot = $"{pathRoot}/";
            LoadLocalItemDataAsync().Wait();
        }

        public string PathRoot { get; }

        #region Metadata
        private static string _localItemDataDB = "ItemMetadata";
        private Dictionary<string, RemoteItemMetadata> _localItems = null;
        private async Task LoadLocalItemDataAsync()
        {
            if (File.Exists(BuildPath(_localItemDataDB)))
            {
                using (var itemMetadataFile = new FileStream(BuildPath(_localItemDataDB), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    StreamReader strReader = new StreamReader(itemMetadataFile, Encoding.UTF8);
                    try
                    {
                        _localItems = JsonConvert.DeserializeObject<Dictionary<string, RemoteItemMetadata>>(await strReader.ReadToEndAsync());
                    }
                    catch (Exception)
                    {
                        //this failed, so we want to build this database
                        await BuildLocalItemDataAsync();
                    }
                }
            }
            else
            {
                await BuildLocalItemDataAsync();
            }
        }
        private async Task BuildLocalItemDataAsync()
        {
            //goes through all of the local files and creates the RemoteItemMetadata's
            throw new NotImplementedException();
        }
        private async Task SaveLocalItemDataAsync()
        {
            using (var itemMetadataFile = new FileStream(BuildPath(_localItemDataDB), FileMode.Open, FileAccess.Write, FileShare.None))
            {
                StreamWriter strWriter = new StreamWriter(itemMetadataFile, Encoding.UTF8);
                await strWriter.WriteLineAsync(JsonConvert.SerializeObject(_localItemDataDB));
            }
        }
        #endregion

        #region Private Helper Methods
        private string BuildPath(string path)
        {
            if (path.First() == '/')
                return $"{PathRoot}{path}";
            else
                return $"{PathRoot}/{path}";
        }
        private string UnBuildPath(string path)
        {
            //remote the path root
            string ret = path.Substring(0, PathRoot.Length);

            //replace back slashes with forward slashes
            return ret.Replace('\\', '/');
        }
        private FileSystemInfo GetItemInfo(string localPath)
        {
            var fqp = BuildPath(localPath);
            if (Directory.Exists(fqp))
            {
                return new DirectoryInfo(fqp);
            }
            else if (File.Exists(fqp))
            {
                return new FileInfo(fqp);
            }
            else
            {
                return null;
            }
        }
        private RemoteItemMetadata GetItemMetadata(string localPath)
        {
            var items = (from localItem in _localItems
                         where localItem.Value.Path == localPath
                         select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
        }
        private RemoteItemMetadata GetItemMetadataById(string id)
        {
            var items = (from localItem in _localItems
                         where localItem.Value.Id == id
                         select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
        }
        //Takes the file name and renames it so it is unique, but different from its original
        private string RenameFile(string localPath)
        {
            return "";
        }
        private Stream GetLocalFileStream(string id)
        {
            var localItem = GetItemMetadataById(id);
            return new FileStream(BuildPath(localItem.Path), FileMode.Open, FileAccess.Read, FileShare.None);
        }
        private bool CopyLocalItem(string localPath, string newLocalPath)
        {
            try
            {
                string fqp = BuildPath(localPath);
                if (Directory.Exists(fqp))
                {
                    //move the folder, but create one with the old name... is this useful?
                    Directory.Move(fqp, BuildPath(newLocalPath));
                    Directory.CreateDirectory(fqp);
                    return true;
                }
                else if (File.Exists(fqp))
                {
                    File.Copy(fqp, BuildPath(newLocalPath));
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool MoveLocalItem(string localPath, string newLocalPath)
        {
            try
            {
                string fqp = BuildPath(localPath);
                if (Directory.Exists(fqp))
                {
                    Directory.Move(fqp, BuildPath(newLocalPath));
                }
                else if (File.Exists(fqp))
                {
                    File.Move(fqp, BuildPath(newLocalPath));
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region IRemoteFileStoreDownload
        public bool CreateLocalFolder(string folderPath)
        {
            try
            {
                System.IO.Directory.CreateDirectory(BuildPath(folderPath));
                //TODO: send message
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }
        public async Task<bool> DeleteLocalItemAsync(IRemoteItemHandle remoteHandle)
        {
            var localItem = GetItemMetadata(remoteHandle.Path);
            if (localItem == null)
                return false;
            var localItemInfo = GetItemInfo(localItem.Path);

            if (localItemInfo.LastWriteTime > localItem.RemoteLastModified && remoteHandle.LastModified > localItem.RemoteLastModified)
            {
                //if local item has changed since last sync AND remote has too

                //rename local file instead
                return MoveLocalItem(localItem.Path, RenameFile(localItem.Path));
                //TODO: also send a file-moved message
            }
            else
            {
                try
                {
                    //delete the file
                    File.Delete(BuildPath(localItem.Path));
                    _localItems.Remove(localItem.Id);
                    return true;
                }
                catch(Exception)
                {
                    return false;
                }
            }
        }
        public async Task<string> GetLocalSHA1Async(string id)
        {
            using (var fs = GetLocalFileStream(id))
            {
                using (var cryptoProvider = new SHA1CryptoServiceProvider())
                {
                    return BitConverter.ToString(cryptoProvider.ComputeHash(fs));
                }
            }
        }
        public async Task<IRemoteItemHandle> GetFileHandleAsync(string localPath)
        {
            var localItem = GetItemMetadata(localPath);
            if (localItem == null)
                return null;

            return new DownloadedFileHandle(this, localItem.Id, localItem.Path);
        }
        public async Task<IRemoteItemHandle> GetFileHandleAsync(IRemoteItemHandle remoteHandle)
        {
            var localItem = GetItemMetadataById(remoteHandle.Id);
            if (localItem == null)
                return null;

            return new DownloadedFileHandle(this, localItem.Id, localItem.Path);
        }
        public async Task SaveFileAsync(IRemoteItemHandle file)
        {
            var localItem = GetItemMetadataById(file.Id);
            if(localItem == null)
            {
                localItem = _localItems[file.Id] = new RemoteItemMetadata() { Id = file.Id, IsFolder = file.IsFolder, Path = file.Path, RemoteLastModified = file.LastModified };
            }

            using (var localStream = new FileStream(BuildPath(localItem.Path), FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var remoteStream = await file.GetFileDataAsync())
                {
                    //TODO: this is VERY complicated to do asynchronously... https://psycodedeveloper.wordpress.com/2013/04/04/reliably-asynchronously-reading-and-writing-binary-streams-in-c-always-check-method-call-return-values/
                }
            }
        }
        public async Task<bool> MoveLocalItemAsync(IRemoteItemHandle remoteHandle)
        {
            var localItem = GetItemMetadataById(remoteHandle.Id);
            if (localItem == null)
                return false;
            var localItemInfo = GetItemInfo(localItem.Path);

            if(localItemInfo.LastWriteTime > localItem.RemoteLastModified && remoteHandle.LastModified > localItem.RemoteLastModified)
            {
                //if local item has changed since last sync AND remote has too
                if (MoveLocalItem(localItem.Path, remoteHandle.Path))//do the same thing? ...
                { 
                    localItem.Path = remoteHandle.Path;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //move the item
                if(MoveLocalItem(localItem.Path, remoteHandle.Path))
                {
                    localItem.Path = remoteHandle.Path;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public bool ItemExists(IRemoteItemHandle remoteHandle)
        {
            var localItem = GetItemMetadataById(remoteHandle.Id);
            if (localItem == null)
                return false;
            else
                return true;
        }
        public event EventDelegates.LocalFileStoreUpdateHandler OnUpdate;
        #endregion

        #region Local Change Events
        FileSystemWatcher watcher = new FileSystemWatcher();
        private void SubscribeToLocalChanges()
        {
            watcher.Path = PathRoot;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*";
            watcher.Changed += new FileSystemEventHandler(FSWatcherEventHandler);
            watcher.EnableRaisingEvents = true;//do we want this?
        }
        public async void FSWatcherEventHandler(object sender, FileSystemEventArgs e)
        {
            LocalFileStoreEventArgs newE = new LocalFileStoreEventArgs(e, UnBuildPath(e.FullPath));
            await OnUpdate.Invoke(this, newE);
        }
        #endregion

        private class DownloadedFileHandle : IRemoteItemHandle
        {
            private string _id;
            private string _path;
            private DownloadedFileStore _fs;

            private bool? _isFolder;
            private string _name;
            private string _sha1Hash;
            private bool _lmInitialized = false;
            private DateTime _lastModified;


            public DownloadedFileHandle(DownloadedFileStore fs, string id, string path)
            {
                if (fs == null)
                    throw new ArgumentNullException("fs");
                _id = id;
                _path = path;
            }

            #region IRemoteItemHandle
            public string Id => _id;
            public string Path => _path;

            public bool IsFolder
            {
                get
                {
                    if(_isFolder == null)
                    {
                        _isFolder = Directory.Exists(_fs.BuildPath(Path));
                    }
                    return _isFolder ?? false;
                }
            }
            public string Name
            {
                get
                {
                    if(_name == null)
                    {
                        _name = _path.Split('/').Last();
                    }
                    return _name;
                }
            }
            public string SHA1Hash
            {
                get
                {
                    if(_sha1Hash == null)
                    {
                        _sha1Hash = _fs.GetLocalSHA1Async(_id).Result;
                    }
                    return _sha1Hash;
                }
            }
            public DateTime LastModified
            {
                get
                {
                    if(!_lmInitialized)
                    {
                        _lastModified = _fs.GetItemInfo(Path).LastWriteTimeUtc;
                        _lmInitialized = true;
                    }
                    return _lastModified;
                }
            }
            public async Task<Stream> GetFileDataAsync()
            {
                return _fs.GetLocalFileStream(_id);
            }
            #endregion
        }
        private class RemoteItemMetadata
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    watcher?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DownloadedFileStore() {
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
