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
        public void AddItemMetadata(IRemoteItemHandle handle)
        {
            AddItemMetadata(new RemoteItemMetadata() { IsFolder = handle.IsFolder, Id = handle.Id, Path = handle.Path, RemoteLastModified = handle.LastModified });
        }
        public void AddItemMetadata(IItemHandle localHandle)
        {
            var metadata = GetItemMetadata(localHandle.Path);
            var id = metadata != null ? metadata.Id : GetUniqueId();
            AddItemMetadata(new RemoteItemMetadata() { IsFolder = localHandle.IsFolder, Id = id, Path = localHandle.Path, RemoteLastModified = localHandle.LastModified });
        }
        public void AddItemMetadata(RemoteItemMetadata metadata)
        {
            if (metadata.Id == "gen")
                metadata.Id = GetUniqueId();
            _data.LocalItems[metadata.Id] = metadata;
        }
        public void RemoveItemMetadata(string localPath)
        {
            var metadata = GetItemMetadata(localPath);
            if(metadata != null)
            {
                _data.LocalItems.Remove(metadata.Id);
            }
        }
        public void RemoveItemMetadataById(string id)
        {
            _data.LocalItems.Remove(id);
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
