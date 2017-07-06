using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface IRemoteFileUpdate
    {
        /// <summary>
        /// Whether this the file was deleted from the server
        /// </summary>
        bool Deleted { get; }

        /// <summary>
        /// A handle to the updated file.  Null if <see cref="Deleted" is true/>
        /// </summary>
        IRemoteFileHandle FileHandle { get; }
    }
}
