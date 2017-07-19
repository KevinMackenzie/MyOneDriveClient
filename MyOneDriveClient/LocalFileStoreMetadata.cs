using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
            foreach (var data in _data.LocalItems)
            {
                data.Value.Metadata = this;
            }
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

        public RemoteItemMetadata GetParentItemMetadata(string localPath)
        {
            var parentPath = PathUtils.GetParentItemPath(localPath);
            return GetItemMetadata(localPath);
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
            return AddItemMetadata(new RemoteItemMetadata()
            {
                IsFolder = handle.IsFolder,
                Id = handle.Id,
                ParentId = handle.ParentId,
                Name = handle.Name,
                RemoteLastModified = handle.LastModified
            });
        }
        public bool AddItemMetadata(IItemHandle localHandle)
        {
            var metadata = GetItemMetadata(localHandle.Path);
            string id;
            string parentId;
            if (metadata == null)
            {
                id = "gen";
                var parentItem = GetParentItemMetadata(localHandle.Path);
                if (parentItem == null)
                {
                    //TODO: this should never happen
                    parentId = "-1";
                }
                else
                {
                    parentId = parentItem.Id;
                }
            }
            else
            {
                id = metadata.Id;
                parentId = metadata.ParentId;
            }
            return AddItemMetadata(new RemoteItemMetadata()
            {
                IsFolder = localHandle.IsFolder,
                Id = id,
                ParentId = parentId,
                Name = localHandle.Name,
                RemoteLastModified = localHandle.LastModified
            });
        }
        public bool AddItemMetadata(RemoteItemMetadata metadata)
        {
            if (metadata.Id == "gen")
                metadata.Id = GetUniqueId();
            metadata.Metadata = this;
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
            [JsonIgnore]
            private RemoteItemMetadata _parentItemMetadata;
            [JsonIgnore]
            public LocalFileStoreMetadata Metadata { get; set; }
            [JsonIgnore]
            public string Path
            {

                get
                {
                    if (ParentId == "")
                        return "/";
                    if (_parentItemMetadata == null)
                    {
                        _parentItemMetadata = Metadata.GetItemMetadataById(ParentId);
                        if(_parentItemMetadata == null)
                            throw new Exception("Could not find item parent!");
                    }
                    return $"{_parentItemMetadata.Path}/{Name}".Substring(1);//remove the leading "/"
                }

            }
            [JsonIgnore]
            public bool HasValidId => (Id?.Length ?? 0) > 9;

            public bool IsFolder { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string Name { get; set; }
        }
    }
}
