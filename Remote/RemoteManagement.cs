using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Remote
{
    public class RemoteManagement : IDisposable
    {
        private Task _updateTask;
        private IConnection _connection;

        public RemoteManagement(IConnection connection)
        {
            _connection = connection;
        }

        public void StartUpdateTask()
        {
            
        }

        public void StopUpdateTask()
        {
            
        }

        public void Dispose()
        {
            _updateTask?.Dispose();
        }
    }
}
