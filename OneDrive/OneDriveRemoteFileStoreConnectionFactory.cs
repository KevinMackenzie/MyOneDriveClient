using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;
using LocalCloudStorage.OneDrive;

namespace OneDrive
{
    [Export(typeof(IRemoteFileStoreConnectionFactory))]
    [RemoteFileStoreConnectionFactoryMetadata(ServiceName = "OneDrive")]
    public class OneDriveRemoteFileStoreConnectionFactory : IRemoteFileStoreConnectionFactory
    {
        /// <inheritdoc />
        public IRemoteFileStoreConnection Construct(string cacheLocation)
        {
            return new OneDriveRemoteFileStoreConnection(new TokenCacheHelper(cacheLocation));
        }
        /// <inheritdoc />
        public string ServiceName => "OneDrive";
        /// <inheritdoc />
        public bool OverridesFileStoreInterface => false;
        /// <inheritdoc />
        public IRemoteFileStoreInterface ConstructInterface()
        {
            //this should never be called
            throw new NotSupportedException();
        }
    }
}
