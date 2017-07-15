using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
            public ConcurrentDictionary<string, RemoteItemMetadata> LocalItems { get; set; } = new ConcurrentDictionary<string, RemoteItemMetadata>();
            public string DeltaLink { get; set; } = "";

        }

        private LocalFileStoreMetadataData _data = new LocalFileStoreMetadataData();
        //private AsyncLock _lock = new AsyncLock();

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
            _data.LocalItems.Clear();
        }
        public void Deserialize(string json)
        {
            _data.LocalItems.Clear();
            _data = JsonConvert.DeserializeObject<LocalFileStoreMetadataData>(json);
        }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(_data);
        }

        public RemoteItemMetadata GetItemMetadata(string localPath)
        {
            var items = (from localItem in _data.LocalItems
                where localItem.Value.Path == localPath
                select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
        }
        public RemoteItemMetadata GetItemMetadataById(string id)
        {
            if (_data.LocalItems.TryGetValue(id, out RemoteItemMetadata ret))
            {
                return ret;
            }
            else
            {
                return null;
            }
        }
        public bool AddItemMetadata(IRemoteItemHandle handle)
        {
            return AddItemMetadata(new RemoteItemMetadata() { IsFolder = handle.IsFolder, Id = handle.Id, Path = handle.Path, RemoteLastModified = handle.LastModified });
        }
        public bool AddItemMetadata(IItemHandle localHandle)
        {
            var metadata = GetItemMetadata(localHandle.Path);
            var id = metadata != null ? metadata.Id : GetUniqueId();
            return AddItemMetadata(new RemoteItemMetadata() { IsFolder = localHandle.IsFolder, Id = id, Path = localHandle.Path, RemoteLastModified = localHandle.LastModified });
        }
        public bool AddItemMetadata(RemoteItemMetadata metadata)
        {
            if (metadata.Id == "gen")
                metadata.Id = GetUniqueId();
            return _data.LocalItems.TryAdd(metadata.Id, metadata);
        }
        public bool RemoveItemMetadata(string localPath)
        {
            var metadata = GetItemMetadata(localPath);
            if (metadata == null)
                return false;//failure

            return RemoveItemMetadataById(metadata.Id);
        }
        public bool RemoveItemMetadataById(string id)
        {
            return _data.LocalItems.TryRemove(id, out RemoteItemMetadata value);
        }

        public class RemoteItemMetadata
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
            [JsonIgnore]
            public bool HasValidId => (Id?.Length ?? 0) > 9;
            [JsonIgnore]
            public string Name => Path?.Split(new char[] { '/' }).Last() ?? "";
        }
    }
}
