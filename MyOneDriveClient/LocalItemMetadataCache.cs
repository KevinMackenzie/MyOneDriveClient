using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class LocalCachedItemMetadata : ICachedItemMetadata
    {
        /// <inheritdoc />
        public ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata> LocalItems { get; set; } = new ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata>();
    }
    public class LocalItemMetadataCache : ItemMetadataCache<LocalCachedItemMetadata>
    {
        /// <inheritdoc />
        public LocalItemMetadataCache() : base(new LocalCachedItemMetadata())
        {
        }
    }
}
