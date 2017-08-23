using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public enum DeltaType
    {
        Created,
        Deleted,
        Modified,
        Renamed,
        Moved
    }
    public interface IItemDelta
    {
        IItemHandle Handle { get; set; }
        /// <summary>
        /// The type of delta
        /// </summary>
        DeltaType Type { get; set; }
        /// <summary>
        /// The old path of the item if <see cref="Type"/> is <see cref="DeltaType.Renamed"/> or <see cref="DeltaType.Moved"/>
        /// </summary>
        string OldPath { get; set; }
    }
}
