﻿using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class RemoteItemMetadataCache
    {
        private class RemoteItemMetadataData
        {
            public ConcurrentDictionary<string, RemoteItemMetadata> LocalItems { get; set; } = new ConcurrentDictionary<string, RemoteItemMetadata>();
            public string DeltaLink { get; set; } = "";

        }

        private RemoteItemMetadataData _data = new RemoteItemMetadataData();
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
            _data = JsonConvert.DeserializeObject<RemoteItemMetadataData>(json);
            foreach (var data in _data.LocalItems)
            {
                data.Value.Metadata = this;
            }
        }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(_data);
        }
        
        /// <summary>
        /// Deletes all metadata whose parents have been deleted (<see cref="RemoteItemMetadata.Path"/> throws an exception)
        /// </summary>
        public void ClearOrphanedMetadata()
        {
            var items = new List<string>();
            foreach (var item in _data.LocalItems)
            {
                try
                {
                    var path = item.Value.Path;
                }
                catch (Exception)
                {
                    items.Add(item.Value.Id);
                }
            }
            foreach (var item in items)
            {
                RemoveItemMetadataById(item);
            }
        }

        public RemoteItemMetadata GetItemMetadata(string localPath)
        {
            if (localPath == "/")
                localPath = "";

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
            return _data.LocalItems.TryGetValue(id, out RemoteItemMetadata ret) ? ret : null;
        }

        public RemoteItemMetadata GetParentItemMetadata(string localPath)
        {
            var parentPath = PathUtils.GetParentItemPath(localPath);
            return GetItemMetadata(parentPath);
        }
        public IEnumerable<RemoteItemMetadata> GetChildMetadatas(string localPath)
        {
            var parentItem = GetItemMetadata(localPath);
            if (parentItem == null)
                return null;
            var parentId = parentItem.Id;

            return (from item in _data.LocalItems
                where item.Value.ParentId == parentId
                select item.Value);
        }

        public bool AddItemMetadata(IRemoteItemHandle handle)
        {
            if (_data.LocalItems.ContainsKey(handle.Id))
                return false;

            return AddOrUpdateItemMetadata(handle);
        }

        public bool UpdateItemMetadata(IRemoteItemHandle handle)
        {
            if (!_data.LocalItems.ContainsKey(handle.Id))
                return false;

            return AddOrUpdateItemMetadata(handle);
        }

        public bool AddOrUpdateItemMetadata(IRemoteItemHandle handle)
        {
            return _data.LocalItems.TryAdd(handle.Id, new RemoteItemMetadata
            {
                IsFolder = handle.IsFolder,
                Id = handle.Id,
                ParentId = handle.ParentId,
                Name = handle.Name,
                RemoteLastModified = handle.LastModified,
                Metadata = this
            });
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
            public RemoteItemMetadataCache Metadata { get; set; }
            [JsonIgnore]
            public string Path
            {
                get
                {
                    if (ParentId == "")
                        return "";
                    var parentItemMetadata = Metadata.GetItemMetadataById(ParentId);
                    if(parentItemMetadata == null)
                        throw new Exception("Could not find item parent!");
                    return $"{parentItemMetadata.Path}/{Name}";
                }
            }
            //[JsonIgnore]
            //public bool HasValidId => (Id?.Length ?? 0) > 9;

            public bool IsFolder { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string Name { get; set; }
        }
    }
}