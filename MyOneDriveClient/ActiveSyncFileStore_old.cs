﻿using MyOneDriveClient.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ActiveSyncFileStore_old : IDisposable
    {
        private Task _syncTask = null;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private AsyncLock _metadataLock = new AsyncLock();
        //DO NOT ACCESS DIRECTLY.  ONLY ACCESS WITH LocalChangeEventHandler TODO: this is not a great design for safety purposes
        private ConcurrentQueue<LocalFileStoreEventArgs> _localFileStoreChangeQueue = new ConcurrentQueue<LocalFileStoreEventArgs>();

        private IEnumerable<string> _blacklist;
        private int _syncPeriod;

        private IRemoteFileStoreConnection _remote;
        private ILocalFileStore _local;
        
        public IEnumerable<string> Blacklist { get => _blacklist; }
        public int SyncPeriod { get => _syncPeriod; }

        public string Path { get => _local.PathRoot; }

        /// <summary>
        /// Creates a new instance of the ActiveSyncFileStore
        /// </summary>
        /// <param name="pathRoot">the root path to store files</param>
        /// <param name="blacklist">the list of files that should not be synchronized</param>
        /// <param name="syncPeriod">the time duration in ms between sync attempts</param>
        public ActiveSyncFileStore_old(IEnumerable<string> blacklist, ILocalFileStore local, IRemoteFileStoreConnection remote, int syncPeriod = 300000)
        {
            //_itemIdPathMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(itemIdMapJson);
            _syncPeriod = syncPeriod;
            _blacklist = blacklist;
            _remote = remote;
            _local = local;

            //TODO: where do we do this?
            LoadLocalItemDataAsync().Wait();
            _local.OnUpdate += LocalChangeEventHandler;
        }

        public bool IsBlacklisted(string localFilePath)
        {
            if (localFilePath == _localItemDataDB)
                return true;

            if (Blacklist == null)
                return false;

            var items = from item in Blacklist
                        where localFilePath.Substring(0, item.Length) == item
                        select item;
            if (items.Count() != 0)
                return true;
            else
                return false;
        }

        #region Metadata
        private static string _localItemDataDB = "ItemMetadata";
        private RemoteItemMetadataCache _metadata = new RemoteItemMetadataCache();
        private async Task LoadLocalItemDataAsync()
        {
            if (_local.ItemExists(_localItemDataDB))
            {
                var dbItem = await _local.GetFileHandleAsync(_localItemDataDB);
                if (dbItem != null)
                {
                    using (var itemMetadataFile = await dbItem.GetFileDataAsync())
                    {
                        StreamReader strReader = new StreamReader(itemMetadataFile, Encoding.UTF8);
                        try
                        {
                            _metadata.Deserialize(await strReader.ReadToEndAsync());
                        }
                        catch (Exception)
                        {
                            //this failed, so we want to build this database
                            //await ScanForLocalItemMetadataAsync(true);
                        }
                    }
                }
                else
                {
                    //await ScanForLocalItemMetadataAsync(true);
                }
            }
            else
            {
                //await ScanForLocalItemMetadataAsync(true);
            }
        }
        public async Task ScanForLocalItemMetadataAsync(bool firstTime) //this needs to happen AFTER authentication
        {
            //Get the deltas to build our metadata to start with (mainly the root item)
            await ApplyAllDeltas();

            await _metadataLock.WaitAsync();

            try
            {

                //goes through all of the local files and creates the metadatas
                var items = await _local.EnumerateItemsAsync("/");
                _metadata.ClearLocalMetadata();
                _metadata.ClearOrphanedMetadata();

                foreach (var item in items)
                {
                    if (item.Path == "/")
                    {
                        //this is the root item, but we don't do anything with it
                        continue;
                    }
                    var metadata = _metadata.GetItemMetadata(item.Path);
                    if (metadata == null)
                    {
                        //local item does NOT exist, so add it to the metadata ...
                        _metadata.AddItemMetadata(item);

                        //... and enqueue the change
                        _localFileStoreChangeQueue.Enqueue(new LocalFileStoreEventArgs(WatcherChangeTypes.Created, item.Path));
                    }
                    else
                    {
                        if (item.LastModified == metadata.RemoteLastModified)
                        {
                            //do nothing
                        }
                        else if (item.LastModified > metadata.RemoteLastModified)
                        {
                            //update
                            await LocalChangeEventHandler(null,
                                new LocalFileStoreEventArgs(WatcherChangeTypes.Changed, item.Path));
                        }
                        else
                        {
                            //we need to pull down the latest version (TODO is it safe to assume that ApplyDeltas will deal with this)?
                        }
                    }
                }
                await SaveLocalItemDataAsync();
            }
            finally
            {
                _metadataLock.UnLock();
            }

            await ProcessLocalChanges();
        }
        private async Task SaveLocalItemDataAsync()
        {
            using (var jsonStream = _metadata.Serialize().ToStream(Encoding.UTF8))
            {
                await _local.SaveFileAsync(_localItemDataDB, DateTime.UtcNow, jsonStream, FileAttributes.Hidden);
            }
        }
        #endregion


        private async Task<bool> ProcessLocalChanges()
        {
            while (_localFileStoreChangeQueue.TryPeek(out LocalFileStoreEventArgs e))
            {
                if (!await LocalChangeEventHandler(e))
                    return false;

                //only dequeue if we're successful
                _localFileStoreChangeQueue.TryDequeue(out LocalFileStoreEventArgs e2);
            }
            return true;
        }
        private async Task LocalChangeEventHandler(object sender, LocalFileStoreEventArgs e)
        {
            //DON"T filter here, because we don't want to bind _metadataLock
            _localFileStoreChangeQueue.Enqueue(e);
            await ProcessLocalChanges();
        }

        private void EnqueueDeleteChildren(string localPath)
        {
            var children = _metadata.GetChildMetadatas(localPath);
            if (children == null) return;

            foreach (var child in children)
            {
                _localFileStoreChangeQueue.Enqueue(new LocalFileStoreEventArgs(WatcherChangeTypes.Deleted,
                    child.Path));
                EnqueueDeleteChildren(child.Path);
            }
        }
        private async Task<bool> LocalChangeEventHandler(LocalFileStoreEventArgs e)
        {
            await _metadataLock.WaitAsync();

            try
            {
                if (IsBlacklisted(e.LocalPath))
                    return true;


                RemoteItemMetadataCache.RemoteItemMetadata metadata;
                try
                {
                    metadata = _metadata.GetItemMetadata(e.LocalPath);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Failed to retrieve metadata for item.  Reason: \"{exception.Message}\"");
                    return true;//this happens and I don't like it, but don't know what to do about it
                }

                var handle = await _local.GetFileHandleAsync(e.LocalPath);
                if (metadata != null && handle != null && metadata.RemoteLastModified == handle.LastModified)
                    return true;//SKIP, because this event either a) has already been handled, or b) was a remote delta

                if ((e.ChangeType & WatcherChangeTypes.Created) != 0)
                {
                    if (handle == null)
                    {
                        Debug.WriteLine($"Locally created item has no local reference: \"{e.LocalPath}\"");
                        return false; //this is a weird case
                    }

                    if (metadata != null)
                    {
                        if (metadata.HasValidId)
                        {
                            return true; //we created an item that already has a valid id.  This means that it was a delta item
                        }
                    }


                    var parentMetadata = _metadata.GetItemMetadata(PathUtils.GetParentItemPath(e.LocalPath));
                    if (parentMetadata == null)
                        return false;

                    IRemoteItemHandle remoteItem;
                    if (handle.IsFolder)
                    {
                        remoteItem = await _remote.CreateFolderByIdAsync(parentMetadata.Id, e.Name);
                    }
                    else
                    {
                        //new item
                        remoteItem = await _remote.UploadFileByIdAsync(parentMetadata.Id, e.Name, await handle.GetFileDataAsync());
                    }
                    if (remoteItem == null)
                    {
                        return false;
                    }
                    await _local.SetItemLastModifiedAsync(e.LocalPath, remoteItem.LastModified);
                    _metadata.AddItemMetadata(remoteItem);
                }
                else if ((e.ChangeType & WatcherChangeTypes.Deleted) != 0)
                {
                    //deleted item
                    bool result;

                    //when deleting an item, delete all of its children too
                    if (e.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        //TODO: this doesn't really help
                        //EnqueueDeleteChildren(e.LocalPath);
                    }

                    if (metadata != null)
                    {
                        result = await _remote.DeleteItemByIdAsync(metadata.Id);
                    }
                    else
                    {
                        result = await  _remote.DeleteItemAsync(e.LocalPath);
                    }
                    if (!result)
                        return false;

                    _metadata.RemoveItemMetadata(e.LocalPath);
                }
                else if ((e.ChangeType & WatcherChangeTypes.Renamed) != 0)
                {
                    if (metadata != null)
                        return true; //a "renamed" item with metadata at that name means it was a remote delta (usually a folder)

                    //renamed item
                    metadata = _metadata.GetItemMetadata(e.OldLocalPath);
                    if (metadata == null)
                        return false;
                    
                    string json = $"{{  \"name\": \"{e.Name}\"  }}";

                    //update the item
                    var remoteItem = await _remote.UpdateItemByIdAsync(metadata.Id, json);
                    if (remoteItem == null)
                        return false;

                    //and update the metadata
                    metadata.Name = e.Name;
                    _metadata.AddItemMetadata(metadata);
                }
                else if ((e.ChangeType & WatcherChangeTypes.Changed) != 0)
                {
                    if (handle == null)
                    {
                        Debug.WriteLine($"Locally changed file has no local file handle: \"{e.LocalPath}\"");
                        return false; //this is a weird case
                    }

                    //changes to conents of a file
                    var parentMetadata = _metadata.GetItemMetadata(PathUtils.GetParentItemPath(e.LocalPath));

                    if (parentMetadata == null || metadata == null)
                        return false;
                    
                    if(metadata.IsFolder)
                        return true;//when the contents of a folder change... we don't care
                        
                    var remoteItem = await _remote.UploadFileByIdAsync(parentMetadata.Id, metadata.Name, await handle.GetFileDataAsync());
                    if (remoteItem == null)
                        return false;

                    await _local.SetItemLastModifiedAsync(e.LocalPath, remoteItem.LastModified);

                    metadata.RemoteLastModified = remoteItem.LastModified;
                    _metadata.AddItemMetadata(metadata); //is this line necessary?
                }
                return true;
            }
            finally
            {
                _metadataLock.UnLock();
            }
        }
        
        public async Task ApplyAllDeltas()//TODO: this should be private, public for testing
        {
            if (!await _metadataLock.TryWaitAsync(5000))
                return;

            try
            {
                List<IRemoteItemUpdate> allDeltas = new List<IRemoteItemUpdate>();
                var nextPage = _metadata.DeltaLink;
                do
                {
                    //get the delta page
                    var deltaPage = await _remote.GetDeltasPageAsync(nextPage);

                    //copy its contents to our list
                    allDeltas.AddRange(deltaPage);

                    //set the next page equal to this next page
                    nextPage = deltaPage.NextPage;

                    //usually null.  Can we do this only the last time?
                    _metadata.DeltaLink = deltaPage.DeltaLink;

                } while (nextPage != null);

                //we should never get to this point
                if (string.IsNullOrEmpty(_metadata.DeltaLink))
                    return;

                foreach (var delta in allDeltas)
                {
                    if (delta.ItemHandle.Path == "/")
                    {
                        var localRoot = _metadata.GetItemMetadataById(delta.ItemHandle.Id);
                        if (localRoot == null)
                            _metadata.AddItemMetadata(new RemoteItemMetadataCache.RemoteItemMetadata()
                            {
                                Id = delta.ItemHandle.Id,
                                ParentId = "",
                                IsFolder = true,
                                RemoteLastModified = delta.ItemHandle.LastModified
                            });
                        continue; //don't sync the root item
                    }

                    //don't sync blacklisted items
                    if (IsBlacklisted(delta.ItemHandle.Path))
                    {
                        continue;
                    }

                    //update file/folder renames in BOTH the file system AND the itemIdMap

                    string remoteName = delta.ItemHandle.Path;

                    bool localExists = true;

                    var localMetadata = _metadata.GetItemMetadataById(delta.ItemHandle.Id);
                    if (localMetadata == null)
                    {
                        localMetadata = _metadata.GetItemMetadata(delta.ItemHandle.Path);
                        if (localMetadata == null)
                        {
                            //_metadata.AddItemMetadata(delta.ItemHandle);
                            //localMetadata = _metadata.GetItemMetadataById(delta.ItemHandle.Id);
                            localExists = false;
                        }
                        else
                        {
                            //when the item at that path has no ID, give it the remote item with that path
                            var oldId = localMetadata.Id;
                            localMetadata.Id = delta.ItemHandle.Id;
                            _metadata.AddItemMetadata(localMetadata);
                            _metadata.RemoveItemMetadataById(oldId);
                        }
                    }

                    if (delta.Deleted)
                    {
                        if (localExists)
                        {
                            //TODO: if the local file name changes, but the remote version is deleted, this will delete
                            //the file locally too.   Is this the desired behavior?
                            await _local.DeleteLocalItemAsync(_metadata.GetItemMetadataById(delta.ItemHandle.Id).Path);
                        }
                        //if the local item doesn't exist and we are deleting a file, just ignore this
                        _metadata.RemoveItemMetadataById(delta.ItemHandle.Id);
                    }
                    else
                    {
                        if (!localExists)
                        {
                            if (delta.ItemHandle.IsFolder)
                            {
                                //create folder
                                _local.CreateLocalFolder(delta.ItemHandle.Path, delta.ItemHandle.LastModified);
                            }
                            else
                            {
                                //download the file
                                await _local.SaveFileAsync(delta.ItemHandle.Path, delta.ItemHandle.LastModified,
                                    await delta.ItemHandle.GetFileDataAsync());
                            }
                        }
                        else
                        {
                            var localItem = await _local.GetFileHandleAsync(localMetadata.Path);
                            string localName = localItem.Path;
                            if (delta.ItemHandle.IsFolder)
                            {
                                if (localName != remoteName)
                                {
                                    await _local.MoveLocalItemAsync(localName, remoteName);
                                }
                            }
                            else
                            {
                                //different paths/names, so check the time stamps to see if we need to just move it, or actually download the new version
                                var remoteTS = delta.ItemHandle.LastModified;
                                var lastRemoteTS = localMetadata.RemoteLastModified;
                                var localTS = localItem.LastModified;

                                //if the file has not changed since our last update
                                if (localTS == lastRemoteTS)
                                {
                                    if (remoteTS > localTS)
                                    {
                                        //remote is newer than local, so delete local...
                                        await _local.DeleteLocalItemAsync(localName);

                                        //... and download the remote
                                        await _local.SaveFileAsync(remoteName, remoteTS,
                                            await delta.ItemHandle.GetFileDataAsync());
                                    }
                                    else if (remoteTS == localTS
                                    ) //will this code ever be reached?  when renaming a file, the TS also gets updated
                                    {
                                        if (localName != remoteName)
                                        {
                                            //same time, different name, so move the existing file
                                            await _local.MoveLocalItemAsync(localName, remoteName);
                                        }
                                    }
                                }
                                else //the local file has changed since the last update
                                {
                                    var newPath = GetNearestConflictResolution(localName);

                                    //move the old one
                                    await _local.MoveLocalItemAsync(localName, newPath);
                                    //add this to the metadata
                                    _metadata.AddItemMetadata(new RemoteItemMetadataCache.RemoteItemMetadata()
                                    {
                                        Id = "gen",
                                        IsFolder = false,
                                        ParentId = delta.ItemHandle.ParentId,
                                        RemoteLastModified = localTS
                                    });

                                    //download the new one
                                    await _local.SaveFileAsync(remoteName, remoteTS,
                                        await delta.ItemHandle.GetFileDataAsync());
                                }
                            }
                        }

                        //make sure the metadata is up to date
                        _metadata.AddItemMetadata(delta.ItemHandle);
                    }
                }

                //after going through the deltas, save the metadata file again
                await SaveLocalItemDataAsync();
            }
            finally
            {
                _metadataLock.UnLock();
            }
        }

        //TODO: where should these functions go?
        private string GetNearestConflictResolution(string path)
        {
            var pathParts = path.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            var name = pathParts.Last();

            pathParts = pathParts.Take(pathParts.Length - 1).ToArray();

            var nameParts = name.Split(new char[] {'.'}, 2);

            var i = 0;
            string newPath;
            var hasExt = nameParts.Length > 1;
            do
            {
                newPath = PathUtils.CompileString(pathParts, hasExt ? $"{nameParts[0]} (local conflict {i}).{nameParts[1]}" : $"{nameParts[0]} (local conflict {i})");
                i++;
            } while (_local.ItemExists(newPath));
            return newPath;
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

                while (!ct.IsCancellationRequested)
                {
                    //FindNewLocalItems();
                    ApplyAllDeltas().Wait();
                    //ApplyLocalChanges().Wait();

                    Task.Delay(_syncPeriod, _cts.Token);
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