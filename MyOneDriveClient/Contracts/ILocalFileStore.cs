﻿using MyOneDriveClient.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    //This contract is VERY important.  Make it better!
    public interface ILocalFileStore
    {
        /// <summary>
        /// The path of the file store.  All path's given to other methods should be 
        /// relative to this path
        /// </summary>
        string PathRoot { get; }
        Task SaveFileAsync(string localPath, Stream data);
        Task<IItemHandle> GetFileHandleAsync(string localPath);
        bool CreateLocalFolder(string localPath);
        Task<string> GetLocalSHA1Async(string localPath);
        Task<bool> DeleteLocalItemAsync(string localPath);
        Task<bool> MoveLocalItemAsync(string localPath, string newLocalPath);        
        bool ItemExists(string localPath);
        event EventDelegates.LocalFileStoreUpdateHandler OnUpdate;
    }
}
