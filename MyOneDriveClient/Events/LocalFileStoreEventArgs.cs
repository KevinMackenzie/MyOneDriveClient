using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.Events
{
    /// <summary>
    /// Event arguments for a local file store change
    /// </summary>
    /// <remarks>
    /// Eventually this will have information on whether the file was created by the user, or by the internal code (which would allow
    /// the calling code to prompt the user for keeping or removing duplicates/resolve sync conflicts)
    /// </remarks>
    public class LocalFileStoreEventArgs
    {
        public LocalFileStoreEventArgs(FileSystemEventArgs e, string localPath)
        {
            InnerEventArgs = e;
            LocalPath = localPath;
        }

        public FileSystemEventArgs InnerEventArgs { get; }
        public string LocalPath { get; }
    }
}
