using System;
using System.Collections.Generic;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Composition
{
    public static class ContainerConfigurationExtensions
    {
        public static ContainerConfiguration WithAssembliesInPath(this ContainerConfiguration configuration,
            string path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return WithAssembliesInPath(configuration, path, null, searchOption);
        }

        public static ContainerConfiguration WithAssembliesInPath(this ContainerConfiguration configuration,
            string path, AttributedModelProvider conventions, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var assemblies = Directory
                .GetFiles(path, "*.dll", searchOption).Select(Assembly.LoadFile).ToList();
            //TODO this does not work in .net 4.6.2 (only .net core)
            //    .Select(AssemblyLoadContext.GetAssemblyName)
            //    .Select(AssemblyLoadContext.Default.LoadFromAssemblyName)
            //    .ToList();

            configuration = configuration.WithAssemblies(assemblies, conventions);

            return configuration;
        }
    }
}
