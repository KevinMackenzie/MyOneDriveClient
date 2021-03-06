﻿using System;
using System.Collections.Concurrent;

namespace LocalCloudStorage
{
    public class RemoteCachedItemMetadata : ICachedItemMetadata
    {
        public ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata> LocalItems { get; set; } = new ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata>();
        public DateTime LastSyncTime { get; set; } = DateTime.MinValue; //use the min value so the last sync is infinitely in the past
        public string DeltaLink { get; set; } = "";
    }
    public class RemoteItemMetadataCache : ItemMetadataCache<RemoteCachedItemMetadata>
    {
        /// <inheritdoc />
        public RemoteItemMetadataCache() : base(new RemoteCachedItemMetadata())
        {
        }

        public string DeltaLink
        {
            get => MyData.DeltaLink;
            set => MyData.DeltaLink = value;
        }
    }
}
