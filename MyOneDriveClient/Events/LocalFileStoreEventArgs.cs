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
        public LocalFileStoreEventArgs(FileSystemEventArgs e, IRemoteItemHandle item)
        {
            InnerEventArgs = e;
            Item = item;
        }

        public LocalFileStoreEventArgs(RenamedEventArgs e, IRemoteItemHandle item, string oldLocalPath)
        {
            InnerEventArgs = e;
            Item = item;
            OldLocalPath = oldLocalPath;
        }

        /// <summary>
        /// can be casted to <see cref="RenamedEventArgs"/> if <see cref="FileSystemEventArgs.ChangeType"/> is <see cref="WatcherChangeTypes.Renamed"/>
        /// </summary>
        public FileSystemEventArgs InnerEventArgs { get; }
        public IRemoteItemHandle Item { get; }
        public string OldLocalPath { get; }
    }
}
