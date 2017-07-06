﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface ILocalFileStore
    {
        Task SaveFileAsync(string localPath, Stream data);
        Task<Stream> LoadFileAsync(string localPath);
    }
}
