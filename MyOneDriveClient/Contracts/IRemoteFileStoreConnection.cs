﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    /// <summary>
    /// A remote file server with read/write access 
    /// </summary>
    public interface IRemoteFileStoreConnection
    {
        Task PromptUserLogin();
        void LogUserOut();

        Task<IEnumerable<string>> EnumerateFilePaths(string remotePath);

        Task<string> GetFileMetadata(string remotePath);

        Task<FileData> DownloadFile(string remotePath);
        Task UploadFile(string remotePath, Stream data);
    }
}
