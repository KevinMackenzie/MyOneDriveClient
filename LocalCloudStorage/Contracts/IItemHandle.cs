﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface IItemHandle
    {
        bool IsFolder { get; }
        string Path { get; }
        string Name { get; }
        long Size { get; }
        Task<string> GetSha1HashAsync();
        DateTime LastModified { get; }
        //DateTime Created { get; } (less important)
        /// <summary>
        /// Gets a stream to this item's data, null if failed or <see cref="IsFolder"/> is true
        /// </summary>
        /// <returns></returns>
        Task<Stream> GetFileDataAsync();
    }
}
