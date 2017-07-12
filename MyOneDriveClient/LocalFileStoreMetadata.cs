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
            var items = (from localItem in _localItems
                         where localItem.Value.Id == id
                         select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
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
