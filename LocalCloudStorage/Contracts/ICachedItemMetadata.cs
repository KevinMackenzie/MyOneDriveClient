﻿using System.Collections.Concurrent;

namespace LocalCloudStorage
{
    public interface ICachedItemMetadata
    {
        ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata> LocalItems { get; set; }
    }
}
