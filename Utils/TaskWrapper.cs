using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage.Utils
{
    public class TaskWrapper : IDisposable
    {
        private Task _task;

        public TaskWrapper(Task task)
        {
            _task = task;
        }

        public void Start(CancellationToken ct, PauseToken pt)
        {
            
        }

        public void Stop()
        {

        }

        public async Task Await()
        {
            await _task;
        }
    }
}
