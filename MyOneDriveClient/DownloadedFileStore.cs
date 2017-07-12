﻿using Newtonsoft.Json;
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
        }

        public string PathRoot { get; }

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
        //Takes the file name and renames it so it is unique, but different from its original
        private string RenameFile(string localPath)
        {
            return "";
        }
        private Stream GetLocalFileStream(string localPath)
        {
            return new FileStream(BuildPath(localPath), FileMode.Open, FileAccess.Read, FileShare.None);
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
        #endregion

        #region IRemoteFileStoreDownload
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
        public async Task<bool> DeleteLocalItemAsync(string localPath)
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
        public async Task<string> GetLocalSHA1Async(string localPath)
        {
            using (var fs = GetLocalFileStream(localPath))
            {
                using (var cryptoProvider = new SHA1CryptoServiceProvider())
                {
                    return BitConverter.ToString(cryptoProvider.ComputeHash(fs));
                }
            }
        }
        public async Task<IItemHandle> GetFileHandleAsync(string localPath)
        {
            return new DownloadedFileHandle(this, localPath);
        }
        public async Task SaveFileAsync(string localPath, DateTime lastModified, Stream data)
        {
            string fqp = BuildPath(localPath);
            using (var localStream = new FileStream(fqp, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                await data.CopyToStreamAsync(localStream);                
            }
            File.SetLastWriteTimeUtc(fqp, lastModified);
        }
        public async Task<bool> MoveLocalItemAsync(string localPath, string newLocalPath)
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
        public event EventDelegates.LocalFileStoreUpdateHandler OnUpdate;
        #endregion

        #region Local Change Events
        FileSystemWatcher watcher = new FileSystemWatcher();
        private void SubscribeToLocalChanges()
        {
            watcher.Path = PathRoot;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Filter = "*";
            watcher.Changed += Watcher_Changed;
            watcher.Renamed += Watcher_Renamed;
            watcher.Deleted += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.EnableRaisingEvents = true;//do we want this?
        }

        public void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            Watcher_Changed(sender, e);
        }
        public async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            var info = GetItemInfo(e.FullPath);
            if ((info.Attributes & FileAttributes.Hidden) != 0)
                return;//don't send updates for hidden files

            //TODO: how do we effectively GET the ID of a created file back when the owner of this uploads it?
            //Will we get this when we check for deltas?
            LocalFileStoreEventArgs newE = new LocalFileStoreEventArgs(e, UnBuildPath(e.FullPath));
            await OnUpdate.Invoke(this, newE);
        }
        #endregion

        private class DownloadedFileHandle : IItemHandle
        {
            private string _path;
            private DownloadedFileStore _fs;

            private bool? _isFolder;
            private string _name;
            private string _sha1Hash;
            private bool _lmInitialized = false;
            private DateTime _lastModified;


            public DownloadedFileHandle(DownloadedFileStore fs, string path)
            {
                _fs = fs ?? throw new ArgumentNullException("fs");
                _path = path;
            }

            #region IRemoteItemHandle
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
                        _sha1Hash = _fs.GetLocalSHA1Async(_path).Result;
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
                return _fs.GetLocalFileStream(_path);
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
