using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
{

    public class FileStoreRequestGraveyard : IDisposable
    {
        private ConcurrentDictionary<int, Tuple<DateTime, FileStoreRequest>> _completeRequests = new ConcurrentDictionary<int, Tuple<DateTime, FileStoreRequest>>();
        private CancellationTokenSource _cleanupCancellationTokenSource;
        private Task _cleanupTask;
        private TimeSpan _decayTimeSpan;

        public FileStoreRequestGraveyard(TimeSpan requestDelayTimeSpan)
        {
            _decayTimeSpan = requestDelayTimeSpan;
            StartCleanupTask();
        }

        private void CleanupTask()
        {
            while (_cleanupCancellationTokenSource.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var toBeRemoved = (from item in _completeRequests where now - item.Value.Item1 > _decayTimeSpan select item.Key).ToList();
                foreach (var removeId in toBeRemoved)
                {
                    _completeRequests.TryRemove(removeId, out Tuple<DateTime, FileStoreRequest> value);
                }

                Utils.DelayNoThrow(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100),
                    _cleanupCancellationTokenSource.Token).Wait();
            }
        }
        private void StartCleanupTask()
        {
            if (_cleanupCancellationTokenSource != null) return;

            _cleanupCancellationTokenSource = new CancellationTokenSource();
            _cleanupTask = Task.Run(() => CleanupTask());
        }
        private async Task StopCleanupTaskAsync()
        {
            if (_cleanupCancellationTokenSource == null) return;

            _cleanupCancellationTokenSource.Cancel();
            await _cleanupTask;
        }

        public void Add(FileStoreRequest request)
        {
            _completeRequests[request.RequestId] = new Tuple<DateTime, FileStoreRequest>(DateTime.UtcNow, request);
        }

        public bool TryGetItem(int requestId, out FileStoreRequest request)
        {
            var ret = _completeRequests.TryGetValue(requestId, out var value);
            request = value?.Item2;
            return ret;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StopCleanupTaskAsync().Wait();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
