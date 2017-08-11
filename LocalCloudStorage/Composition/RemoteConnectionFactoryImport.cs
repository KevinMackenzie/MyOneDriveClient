using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage.Composition
{
    class RemoteConnectionFactoryImport
    {
        public event EventHandler<ImportEventArgs> ImportsSatisfied;

        [ImportMany()]
        public IEnumerable<IRemoteFileStoreConnectionFactory> Factories { get; set; }

        [OnImportsSatisfied]
        public void OnImportsSatisfied()
        {
            ImportsSatisfied?.Invoke(this, new ImportEventArgs { StatusMessage = "IRemoteFileStoreConnectionFactory imports successful" });
        }
    }
}
