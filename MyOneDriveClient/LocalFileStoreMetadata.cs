using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class LocalFileStoreMetadata
    {
        private class LocalFileStoreMetadataData
        {
            public Dictionary<string, RemoteItemMetadata> LocalItems { get; set; } = new Dictionary<string, RemoteItemMetadata>();
            public string DeltaLink { get; set; } = "";

        }

        private LocalFileStoreMetadataData _data = new LocalFileStoreMetadataData();
        private AsyncLock _lock = new AsyncLock();

        public string DeltaLink
        {
            get => _data.DeltaLink;
            set => _data.DeltaLink = value;
        }

        private string GetUniqueId()
        {
            int i = 0;
            while (_data.LocalItems.ContainsKey(i.ToString()))
            {
                i++;
            }
            return i.ToString();
        }

        public void Clear()
        {
            try
            {
                _lock.WaitAsync().Wait();
                _data.LocalItems.Clear();
            }
            finally
            {
                _lock.UnLock();
            }
        }
        public void Deserialize(string json)
        {
            try
            {
                _lock.WaitAsync().Wait();
                _data.LocalItems.Clear();
                _data = JsonConvert.DeserializeObject<LocalFileStoreMetadataData>(json);
            }
            finally
            {
                _lock.UnLock();
            }
        }
        public string Serialize()
        {
            try
            {
                _lock.WaitAsync().Wait();
                return JsonConvert.SerializeObject(_data);
            }
            finally
            {
                _lock.UnLock();
            }
        }

        public async Task<RemoteItemMetadata> GetItemMetadataAsync(string localPath)
        {
            try
            {
                await _lock.WaitAsync();

                var items = (from localItem in _data.LocalItems
                    where localItem.Value.Path == localPath
                    select localItem).ToList();

                if (items.Count == 0)
                    return null;
                //if (items.Count > 1) ;//???
                return items.First().Value;
            }
            finally
            {
                _lock.UnLock();
            }
        }
        public async Task<RemoteItemMetadata> GetItemMetadataByIdAsync(string id)
        {
            try
            {
                await _lock.WaitAsync();

                if (_data.LocalItems.TryGetValue(id, out RemoteItemMetadata ret))
                {
                    return ret;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                _lock.UnLock();
            }
        }
        public async Task AddItemMetadataAsync(IRemoteItemHandle handle)
        {
            await AddItemMetadataAsync(new RemoteItemMetadata() { IsFolder = handle.IsFolder, Id = handle.Id, Path = handle.Path, RemoteLastModified = handle.LastModified });
        }
        public async Task AddItemMetadataAsync(IItemHandle localHandle)
        {
            var metadata = await GetItemMetadataAsync(localHandle.Path);
            var id = metadata != null ? metadata.Id : GetUniqueId();
            await AddItemMetadataAsync(new RemoteItemMetadata() { IsFolder = localHandle.IsFolder, Id = id, Path = localHandle.Path, RemoteLastModified = localHandle.LastModified });
        }
        public async Task AddItemMetadataAsync(RemoteItemMetadata metadata)
        {
            try
            {
                await _lock.WaitAsync();

                if (metadata.Id == "gen")
                    metadata.Id = GetUniqueId();
                _data.LocalItems[metadata.Id] = metadata;
            }
            finally
            {
                _lock.UnLock();
            }
        }
        public async Task RemoveItemMetadataAsync(string localPath)
        {
            var metadata = await GetItemMetadataAsync(localPath);

            if(metadata != null)
            {
                try
                {
                    await _lock.WaitAsync();
                    _data.LocalItems.Remove(metadata.Id);
                }
                finally
                {
                    _lock.UnLock();
                }
            }
        }
        public async Task RemoveItemMetadataById(string id)
        {
            try
            {
                await _lock.WaitAsync();
                _data.LocalItems.Remove(id);
            }
            finally
            {
                _lock.UnLock();
            }
        }

        public class RemoteItemMetadata
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
            public bool HasValidId => (Id?.Length ?? 0) > 9;
        }
    }
}
