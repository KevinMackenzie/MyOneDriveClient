using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Events
{
    /// <summary>
    /// Event arguments for a local file store change
    /// </summary>
    /// <remarks>
    /// Eventually this will have information on whether the file was created by the user, or by the internal code (which would allow
    /// the calling code to prompt the user for keeping or removing duplicates/resolve sync conflicts)
    /// </remarks>
    public class LocalFileStoreEventArgs : EventArgs
    {
        public LocalFileStoreEventArgs(WatcherChangeTypes changeType, string path)
        {
            ChangeType = changeType;
            LocalPath = path;
        }

        public LocalFileStoreEventArgs(WatcherChangeTypes changeType, string path, string oldLocalPath) : this(changeType, path)
        {
            OldLocalPath = oldLocalPath;
        }

        /// <summary>
        /// can be casted to <see cref="RenamedEventArgs"/> if <see cref="FileSystemEventArgs.ChangeType"/> is <see cref="WatcherChangeTypes.Renamed"/>
        /// </summary>
        public WatcherChangeTypes ChangeType { get; }
        public string LocalPath { get; }
        public string Name => PathUtils.GetItemName(LocalPath);
        public string OldLocalPath { get; }
    }
}
