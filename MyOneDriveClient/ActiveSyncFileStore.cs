using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ActiveSyncFileStore : ILocalFileStore, IDisposable
    {
        private Task _syncTask = null;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private IEnumerable<string> _blacklist;
        private string _pathRoot = "";
        private int _syncPeriod;

        public string PathRoot { get => _pathRoot; }
        public IEnumerable<string> Blacklist { get => _blacklist; }
        public int SyncPeriod { get => _syncPeriod; }

        /// <summary>
        /// Creates a new instance of the ActiveSyncFileStore
        /// </summary>
        /// <param name="pathRoot">the root path to store files</param>
        /// <param name="blacklist">the list of files that should not be synchronized</param>
        /// <param name="syncPeriod">the time duration in ms between sync attempts</param>
        public ActiveSyncFileStore(string pathRoot, IEnumerable<string> blacklist, int syncPeriod = 300000)
        {
            _pathRoot = pathRoot;
            _syncPeriod = syncPeriod;
            _blacklist = blacklist;
        }

        #region ILocalFileStore
        public Task<Stream> LoadFileAsync(string localPath)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string localPath, Stream data)
        {
            throw new NotImplementedException();
        }
        #endregion

        private IEnumerable<string> GetStaleFiles()
        {
            //
            throw new NotImplementedException();
        }

        private Task SyncTask(CancellationToken ct)
        {
            return new Task(()=>
            { 
                //Load timestamp
                //if no timestamp
                //  then Run first time code

                while (!ct.IsCancellationRequested)
                {


                    Thread.Sleep(_syncPeriod);
                }
            }, ct);
        }

        public void StartSyncTask()
        {
            if (_syncTask != null)
                return;
            
            //reset the cancellation token
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            //start the task
            _syncTask = SyncTask(_cts.Token);
            _syncTask.Start();
        }

        public async Task StopSyncTaskAsync()
        {
            if (_syncTask == null)
                return;

            //cancel the token
            _cts.Cancel();

            //wait for the task to finish
            await _syncTask;

            _syncTask.Dispose();
            _syncTask = null;
        }

        /// <summary>
        /// does not use one drive "delta" feature and iterates
        /// all files and compares last modified times of local
        /// and remote files, keeping the most up-to-date ones
        /// </summary>
        public void HardForceUpdate()
        {

        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cts?.Cancel();
                    _syncTask?.Wait();
                    _cts?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ActiveSyncFileStore() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion


    }
}
