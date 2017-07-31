﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class LocalCachedItemMetadata : ICachedItemMetadata
    {
        /// <inheritdoc />
        public ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata> LocalItems { get; set; } = new ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata>();
        public long ItemId;
    }
    public class LocalItemMetadataCache : ItemMetadataCache<LocalCachedItemMetadata>
    {
        /// <inheritdoc />
        public LocalItemMetadataCache() : base(new LocalCachedItemMetadata())
        {
            //try to add the root item
            MyData.LocalItems.TryAdd("0",
                new ItemMetadata()
                {
                    Id = "0",
                    IsFolder = true,
                    LastModified = DateTime.UtcNow,
                    Metadata = this,
                    Name = "",
                    ParentId = "",
                    Sha1 = ""
                });
        }

        public long GetNextItemId()
        {
            return Interlocked.Increment(ref MyData.ItemId);
        }
    }
}