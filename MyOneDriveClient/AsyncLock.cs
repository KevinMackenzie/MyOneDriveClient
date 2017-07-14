using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class AsyncLock : IDisposable
    {
        private Queue<CancellationTokenSource> _cancellationTokenSources = new Queue<CancellationTokenSource>();
        
        public async Task<bool> TryWaitAsync(int timeoutMs)
        {
            var cts = GetLock();
            try
            {
                await Task.Delay(timeoutMs, cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            return cts.IsCancellationRequested;
        }

        public async Task WaitAsync()
        {
            try
            {
                //wait a really long time
                await Task.Delay(2000000000, GetLock().Token);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private CancellationTokenSource GetLock()
        {
            var cts = new CancellationTokenSource();
            lock (_cancellationTokenSources)
            {
                if (_cancellationTokenSources.Count == 0)
                {
                    cts.Cancel(); //if we're empty, immediately cancel ourselves
                }
                _cancellationTokenSources.Enqueue(cts);
            }
            return cts;
        }

        public void UnLock()
        {
            lock (_cancellationTokenSources)
            {
                //remove ourselves...
                _cancellationTokenSources.Dequeue();

                //... and let the next one in
                if (_cancellationTokenSources.Count == 0) return;
                _cancellationTokenSources.Peek().Cancel();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var item in _cancellationTokenSources)
            {
                item?.Dispose();
            }
        }
    }
}
