using System;
using System.Collections.Generic;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Composition
{
    public class RemoteConnectionFactoryManager
    {
        public IEnumerable<IRemoteFileStoreConnectionFactory> Factories { get; private set; }

        /// <summary>
        /// Imports all of the inheriters of <see cref="IRemoteFileStoreConnectionFactory"/> in 
        ///  all of the assemblies in a given path.
        /// </summary>
        /// <param name="pluginPath">the path to look for assemblies</param>
        /// <remarks>This deletes all existing factories</remarks>
        public void ImportFactories(string pluginPath)
        {
            var conventions = new ConventionBuilder();
            conventions
                .ForTypesDerivedFrom<IRemoteFileStoreConnectionFactory>()
                .Export<IRemoteFileStoreConnectionFactory>()
                .Shared();

            var configuration = new ContainerConfiguration()
                .WithAssembliesInPath(pluginPath);

            using (var container = configuration.CreateContainer())
            {
                Factories = container.GetExports<IRemoteFileStoreConnectionFactory>();
            }
        }
    }
}
