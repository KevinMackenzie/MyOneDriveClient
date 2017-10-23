using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface ILocalFileStoreCache
    {
        /// <summary>
        /// How long to keep cached files for before deleting them
        /// </summary>
        TimeSpan CacheExpirationDuration { get; set; }

        /// <summary>
        /// The maximum size of the cache (in megabytes)
        /// </summary>
        uint MaxCacheSize { get; set; }

        /// <summary>
        /// Sets the given file to be cached and events to be submitted from its changes
        /// </summary>
        /// <param name="item">the item to cache</param>
        /// <returns>the name of the file in the cache</returns>
        /// <remarks>Throws an exception if <paramref name="item"/> is a folder</remarks>
        string CacheFile(IItemHandle item);
        /// <summary>
        /// Removes an item from the cache and deletes it immediately
        /// </summary>
        /// <param name="item">the item to remove from the cache</param>
        void DeCacheFile(IItemHandle item);

    }
}
