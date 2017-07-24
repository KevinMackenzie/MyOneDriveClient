using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ItemDelta
    {
        public enum DeltaType
        {
            ModifiedOrCreated,
            Deleted,
            Renamed,
            Moved
        }

        /// <summary>
        /// If the item is a folder
        /// </summary>
        public bool IsFolder { get; }
        /// <summary>
        /// The type of delta
        /// </summary>
        public DeltaType Type { get; }
        /// <summary>
        /// The path of the item
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// The old path of the item if <see cref="Type"/> is <see cref="DeltaType.Renamed"/> or <see cref="DeltaType.Moved"/>
        /// </summary>
        public string OldPath { get; }
    }
}
