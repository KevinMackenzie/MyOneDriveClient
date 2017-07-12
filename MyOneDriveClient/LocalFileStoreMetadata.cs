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
        private Dictionary<string, RemoteItemMetadata> _localItems = null;

        public void Deserialize(string json)
        {
            _localItems.Clear();
            _localItems = JsonConvert.DeserializeObject<Dictionary<string, RemoteItemMetadata>>(json);
        }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(_localItems);
        }

        public RemoteItemMetadata GetItemMetadata(string localPath)
        {
            var items = (from localItem in _localItems
                         where localItem.Value.Path == localPath
                         select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
        }
        public RemoteItemMetadata GetItemMetadataById(string id)
        {
            RemoteItemMetadata ret;
            if(_localItems.TryGetValue(id, out ret))
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
        public void AddItemMetadata(RemoteItemMetadata metadata)
        {
            _localItems[metadata.Id] = metadata;
        }
        public void RemoveItemMetadata(string localPath)
        {
            var metadata = GetItemMetadata(localPath);
            if(metadata != null)
            {
                _localItems.Remove(metadata.Id);
            }
        }
        public void RemoveItemMetadataById(string id)
        {
            _localItems.Remove(id);
        }

        public class RemoteItemMetadata
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
        }
    }
}
