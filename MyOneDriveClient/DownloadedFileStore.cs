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
    public class DownloadedFileStore : ILocalFileStore, IDisposable
    {
        public DownloadedFileStore(string pathRoot)
        {
            if (pathRoot.Last() == '/')
                PathRoot = pathRoot;
            else
                PathRoot = $"{pathRoot}/";

            SubscribeToLocalChanges();
        }

        public string PathRoot { get; }

        #region Private Helper Methods
        private string BuildPath(string path)
        {
            return RectifySlashes(path.First() == '/' ? $"{PathRoot}{path}" : $"{PathRoot}/{path}");
        }
        private string UnBuildPath(string path)
        {
            //remote the path root
            return RectifySlashes(path.Substring(PathRoot.Length - 1, path.Length - PathRoot.Length + 1));
        }
        private static string RectifySlashes(string path)
        {
            //replace back slashes with forward slashes
            path = path.Replace('\\', '/');

            //collapse all multi-forward slashes into singles
            while (path.Contains("//"))
            {
                path = path.Replace("//", "/");
            }
            return path;
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
        //Takes the file name and renames it so it is unique, but different from its original
        private string RenameFile(string localPath)
        {
            return "";
        }
        private Stream GetLocalFileStream(string localPath)
        {
            try
            {
                return new FileStream(BuildPath(localPath), FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception e)
            {
                return null;
            }
        }
        [Obsolete]
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
        private async Task<string> GetLocalSHA1Async(string localPath)
        {
            using (var fs = GetLocalFileStream(localPath))
            {
                if (fs == null)
                    return ""; //TODO :when will this happen?
                using (var cryptoProvider = new SHA1CryptoServiceProvider())
                {
                    return await Task.Run(
                        () => BitConverter.ToString(cryptoProvider.ComputeHash(fs)));
                }
            }
        }

        private void EnumerateItemsRecursive(ref List<ILocalItemHandle> items, string fqp)
        {
            var directories = Directory.EnumerateDirectories(fqp);
            foreach (var directory in directories)
            {
                items.Add(new DownloadedFileHandle(this, UnBuildPath(directory)));
                EnumerateItemsRecursive(ref items, directory);
            }

            var files = Directory.EnumerateFiles(fqp);
            foreach (var file in files)
            {
                var filePath = UnBuildPath(file);
                var info = GetItemInfo(filePath);
                if((info.Attributes & FileAttributes.Hidden) != 0)
                    continue;// do not enumerate hidden files

                items.Add(GetFileHandle(filePath));
            }
        }
        #endregion

        #region ILocalFileStore
        public bool CreateLocalFolder(string localPath, DateTime lastModified)
        {
            try
            {
                var fqp = BuildPath(localPath);
                Directory.CreateDirectory(fqp);
                Directory.SetLastWriteTimeUtc(fqp, lastModified);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }
        public bool DeleteLocalItem(string localPath)
        {
            try
            {
                //delete the file
                File.Delete(BuildPath(localPath));
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }
        public ILocalItemHandle GetFileHandle(string localPath)
        {
            return new DownloadedFileHandle(this, localPath);//ItemExists(localPath) ? new DownloadedFileHandle(this, localPath) : null;
        }
        public void SetItemAttributes(string localPath, FileAttributes attributes)
        {
            var fqp = BuildPath(localPath);
            File.SetAttributes(fqp, attributes);
        }
        public void SetItemLastModified(string localPath, DateTime lastModified)
        {
            string fqp = BuildPath(localPath);
            if (File.Exists(fqp))
            {
                File.SetLastWriteTimeUtc(fqp, lastModified);
            }
            else if (Directory.Exists(fqp))
            {
                Directory.SetLastWriteTimeUtc(fqp, lastModified);
            }
        }
        public bool MoveLocalItem(string localPath, string newLocalPath)
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
        public bool ItemExists(string localPath)
        {
            string fqp = BuildPath(localPath);
            return Directory.Exists(fqp) || File.Exists(fqp);
        }
        public async Task<List<ILocalItemHandle>> EnumerateItemsAsync(string localPath)
        {
            string fqp = BuildPath(localPath);
            if (!Directory.Exists(fqp))
                return null;

            List<ILocalItemHandle> ret = new List<ILocalItemHandle>();
            ret.Add(new DownloadedFileHandle(this, localPath));//add the root item of the request
            await Task.Run(() => EnumerateItemsRecursive(ref ret, fqp));
            return ret;
        }
        public event EventDelegates.LocalFileStoreChangedHandler OnChanged;
        #endregion

        #region Local Change Events
        FileSystemWatcher watcher = new FileSystemWatcher();
        private void SubscribeToLocalChanges()
        {
            watcher.Path = PathRoot;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.IncludeSubdirectories = true;
            watcher.Filter = "*";
            watcher.Changed += Watcher_Changed;
            watcher.Renamed += Watcher_Renamed;
            watcher.Deleted += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.EnableRaisingEvents = true;//do we want this?
        }
        private bool ShouldFilterLocalItem(string fqp)
        {
            var info = GetItemInfo(UnBuildPath(fqp));
            if (info == null) return false; //if it can't find the info, the file must have been deleted

            return (info.Attributes & FileAttributes.Hidden) != 0;
        }
        private async void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (ShouldFilterLocalItem(e.FullPath))
                return;

            //Will we get this when we check for deltas?
            var invoke = OnChanged?.Invoke(this,
                new LocalFileStoreEventArgs(e.ChangeType, UnBuildPath(e.FullPath), UnBuildPath(e.OldFullPath)));
            if (invoke != null)
                await invoke;
        }
        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (ShouldFilterLocalItem(e.FullPath))
                return;

            var localPath = UnBuildPath(e.FullPath);
            //Will we get this when we check for deltas?
            var invoke = OnChanged?.Invoke(this, new LocalFileStoreEventArgs(e.ChangeType, localPath));
            if (invoke != null)
                await invoke;
        }
        #endregion

        private class DownloadedFileHandle : ILocalItemHandle
        {
            private string _path;
            private DownloadedFileStore _fs;

            private bool? _isFolder;
            private string _name;
            private string _sha1Hash;
            private bool _lmInitialized = false;
            private DateTime _lastModified;
            private bool _sizeInitialized = false;
            private long _size;


            public DownloadedFileHandle(DownloadedFileStore fs, string path)
            {
                _fs = fs ?? throw new ArgumentNullException("fs");
                _path = path;
            }

            #region IRemoteItemHandle
            public string Path => _path;
            public Stream GetWritableStream()
            {
                try
                {
                    return new FileStream(_fs.BuildPath(_path), FileMode.Create, FileAccess.Write,
                        FileShare.None);
                }
                catch (Exception)
                {
                    return null;
                }
            }

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
                        _sha1Hash = IsFolder ? "" : _fs.GetLocalSHA1Async(_path).Result;
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
            public long Size
            {
                get
                {
                    if (!_sizeInitialized)
                    {
                        var info = _fs.GetItemInfo(Path);
                        var fileInfo = info as FileInfo;
                        if (fileInfo != null)
                        {
                            _size = fileInfo.Length;
                        }
                        else
                        {
                            _size = 0;
                        }
                        _sizeInitialized = true;
                    }
                    return _size;
                }
                
            }

            public async Task<Stream> GetFileDataAsync()
            {
                return _fs.GetLocalFileStream(_path);
            }

            public bool Exists()
            {
                return _fs.ItemExists(Path);
            }
            #endregion
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
