﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalCloudStorage
{
    public abstract class ItemMetadataCache
    {
        protected ICachedItemMetadata _data;
        //private AsyncLock _lock = new AsyncLock();

        protected ItemMetadataCache(ICachedItemMetadata data)
        {
            _data = data;
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
        public abstract void Deserialize(string json);
        public string Serialize()
        {
            ClearOrphanedMetadata();
            return JsonConvert.SerializeObject(_data, Formatting.Indented);
        }
        
        public DateTime LastSyncTime
        {
            get => _data.LastSyncTime;
            set => _data.LastSyncTime = value;
        }

        /// <summary>
        /// Deletes all metadata whose parents have been deleted (<see cref="ItemMetadata.Path"/> throws an exception)
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

        public ItemMetadata GetItemMetadata(string localPath)
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
        public ItemMetadata GetItemMetadataById(string id)
        {
            return _data.LocalItems.TryGetValue(id, out ItemMetadata ret) ? ret : null;
        }

        public ItemMetadata GetParentItemMetadata(string localPath)
        {
            var parentPath = PathUtils.GetParentItemPath(localPath);
            return GetItemMetadata(parentPath);
        }
        public IDictionary<string, DateTime> GetChildrenLastModified(string localPath)
        {
            //only the root element
            if(_data.LocalItems.Count == 1) return new Dictionary<string, DateTime>();

            var retValues = new Dictionary<string, DateTime>();
            if (localPath == "/" || localPath == "")
            {
                foreach (var item in _data.LocalItems)
                {
                    retValues.Add(item.Value.Path, item.Value.LastModified);
                }
                return retValues;
                //return (IDictionary<string, DateTime>) (from item in _data.LocalItems
                //    select new KeyValuePair<string, DateTime>(item.Value.Path, item.Value.LastModified));
            }
            else
            {
                foreach (var item in _data.LocalItems)
                {
                    var path = item.Value.Path;
                    if (path.StartsWith(localPath) && path != localPath)
                    {
                        try
                        {
                            retValues.Add(path, item.Value.LastModified);
                        }
                        catch (Exception e)
                        {
                            int i = 0;
                        }
                    }
                }
                return retValues;
            }
        }

        public bool AddItemMetadata(IRemoteItemHandle handle)
        {
            if (_data.LocalItems.ContainsKey(handle.Id))
                return false;

            AddOrUpdateItemMetadata(handle);
            return true;
        }

        public bool UpdateItemMetadata(IRemoteItemHandle handle)
        {
            if (!_data.LocalItems.TryGetValue(handle.Id, out ItemMetadata value))
                return false;

            value.ParentId = handle.ParentId;
            value.IsFolder = handle.IsFolder;
            value.LastModified = handle.LastModified;
            value.Name = handle.Name;
            value.Sha1 = handle.Sha1;
            //AddOrUpdateItemMetadata(handle);
            return true;
        }

        public void AddOrUpdateItemMetadata(IRemoteItemHandle handle)
        {
            AddOrUpdateItemMetadata(new ItemMetadata
            {
                IsFolder = handle.IsFolder,
                Id = handle.Id,
                ParentId = handle.ParentId,
                Sha1 = handle.Sha1,
                Name = handle.Name,
                LastModified = handle.LastModified,
                Metadata = this
            }, false);
        }
        public void AddOrUpdateItemMetadata(ItemMetadata metadata)
        {
            metadata.Metadata = this;
            AddOrUpdateItemMetadata(metadata, false);
        }
        private void AddOrUpdateItemMetadata(ItemMetadata metadata, bool b)
        {
            _data.LocalItems[metadata.Id] = metadata;
        }

        public bool RemoveItemMetadata(string localPath)
        {
            var metadata = GetItemMetadata(localPath);
            return metadata != null && RemoveItemMetadataById(metadata.Id);
        }
        public bool RemoveItemMetadataById(string id)
        {
            if (!_data.LocalItems.TryRemove(id, out ItemMetadata value)) return false;
            ClearOrphanedMetadata();
            return true;
        }

        public class ItemMetadata
        {
            [JsonIgnore]
            public ItemMetadataCache Metadata { get; set; }
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
            public DateTime LastModified { get; set; }
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string Name { get; set; }
            public string Sha1 { get; set; }
        }
    }

    public class ItemMetadataCache<TMetadataCacheType> : ItemMetadataCache where TMetadataCacheType : class, ICachedItemMetadata
    {
        protected TMetadataCacheType MyData => _data as TMetadataCacheType;

        public ItemMetadataCache(TMetadataCacheType data) : base(data)
        {
        }
        /// <inheritdoc />
        public override void Deserialize(string json)
        {
            _data.LocalItems.Clear();
            _data = JsonConvert.DeserializeObject<TMetadataCacheType>(json);
            foreach (var data in _data.LocalItems)
            {
                data.Value.Metadata = this;
            }
        }
    }
}
