using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.ItemMetadata
{
    public class ItemMetadata : IReadItemMetadata
    {
        public bool IsFolder { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }

        public bool TryGetProperty(string property, out string value)
        {
            throw new NotImplementedException();
        }
        public bool TryGetProperty(string property, out int value)
        {
            throw new NotImplementedException();
        }
        public bool TryGetProperty(string property, out double value)
        {
            throw new NotImplementedException();
        }

        public void SetProperty(string property, object value)
        {
            throw new NotImplementedException();
        }
    }
}
