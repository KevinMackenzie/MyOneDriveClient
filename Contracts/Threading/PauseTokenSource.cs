using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage.Threading
{
    /*
     * See https://blogs.msdn.microsoft.com/pfxteam/2013/01/13/cooperatively-pausing-async-methods/
     *     for more details
     * 
     */

    public class PauseTokenSource
    {
        #region Private Fields
        private volatile TaskCompletionSource<bool> _paused;
        #endregion

        internal static readonly Task CompletedTask = Task.FromResult(true);
        internal Task WaitWhilePausedAsync()
        {
            var cur = _paused;
            return cur != null ? cur.Task : CompletedTask;
        }

        public bool IsPaused
        {
            get => _paused != null;
            set
            {
                if (value)
                {
                    Interlocked.CompareExchange(ref _paused, new TaskCompletionSource<bool>(), null);
                }
                else
                {
                    while (true)
                    {
                        var tcs = _paused;
                        if (tcs == null) return;
                        if (Interlocked.CompareExchange(ref _paused, null, tcs) == tcs)
                        {
                            tcs.SetResult(true);
                            break;
                        }
                    }
                }
            }
        }
        public PauseToken Token => new PauseToken(this);
    }

    public struct PauseToken
    {
        private readonly PauseTokenSource _source;
        internal PauseToken(PauseTokenSource source)
        {
            _source = source;
        }

        public bool IsPaused => _source?.IsPaused ?? false;
        public Task WaitWhilePausedAsync()
        {
            return IsPaused ? _source.WaitWhilePausedAsync() : PauseTokenSource.CompletedTask;
        }
    }
}
