﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface ICachedItemMetadata
    {
        ConcurrentDictionary<string, ItemMetadataCache.ItemMetadata> LocalItems { get; set; }
    }
}