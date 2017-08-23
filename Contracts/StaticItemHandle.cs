using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <summary>
    /// This is an item handle that will not change over time.
    /// </summary>
    public class StaticItemHandle : IComparable<StaticItemHandle>
    {
        public StaticItemHandle(IItemHandle handle)
        {
            IsFolder = handle.IsFolder;
            Path = handle.Path;
            Name = handle.Name;
            Size = handle.Size;
            LastModified = handle.LastModified;
        }
        public StaticItemHandle(bool isFolder, string path, string name, long size, DateTime lastModified)
        {
            IsFolder = isFolder;
            Path = path;
            Name = name;
            Size = size;
            LastModified = lastModified;
        }

        public bool IsFolder { get; }
        public string Path { get; }
        public string Name { get; }
        public long Size { get; }
        public DateTime LastModified { get; }

        /// <inheritdoc />
        public int CompareTo(StaticItemHandle other)
        {
            return string.CompareOrdinal(Path, other.Path);
        }
    }
}
