using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Contracts
{
    /// <summary>
    /// Creates a remote file store connection for a given type
    /// </summary>
    public interface IRemoteFileStoreConnectionFactory
    {
        /// <summary>
        /// Creates a new instance of the connection.  This instance must
        ///  be ready for <see cref="IRemoteFileStoreConnection.LogUserInAsync"/>
        ///  to be called
        /// </summary>
        /// <returns>The initialized connection instance</returns>
        IRemoteFileStoreConnection Construct();
    }
}
