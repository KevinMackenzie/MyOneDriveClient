using System;
using System.Collections.Generic;
using System.Text;

namespace LocalCloudStorage.ItemMetadata
{
    public class ItemMetadataChangedEventData
    {
        public ItemMetadataChangedEventData(IItemDelta delta)
        {
            ItemDelta = delta;
        }
        public IItemDelta ItemDelta { get; }
    }
}
