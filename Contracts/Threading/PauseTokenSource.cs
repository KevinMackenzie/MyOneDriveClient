using System;
using System.Collections.Concurrent;
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

    public class PauseTokenSource : IDisposable
    {
        #region Private Fields
        private volatile TaskCompletionSource<bool> _paused;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private ConcurrentBag<Action> _onPauseActions = new ConcurrentBag<Action>();
        #endregion

        internal static readonly Task CompletedTask = Task.FromResult(true);
        internal Task WaitWhilePausedAsync()
        {
            var cur = _paused;
            return cur != null ? cur.Task : CompletedTask;
        }
        internal void Register(Action onPauseAction)
        {
            _onPauseActions.Add(onPauseAction);
        }
        internal void ThrowIfCancellationRequested()
        {
            if (_cts.IsCancellationRequested)
            {
                throw new OperationCanceledException("Paused task has been cancelled!");
            }
        }
        internal CancellationToken CToken => _cts.Token;

        public bool IsPaused
        {
            get => _paused != null;
            set
            {
                if (value)
                {
                    Interlocked.CompareExchange(ref _paused, new TaskCompletionSource<bool>(), null);
                    foreach (var action in _onPauseActions)
                    {
                        try
                        {
                            action.Invoke();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
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
        public void Cancel()
        {
            //cancel...
            _cts.Cancel();

            //... then un-pause
            IsPaused = false;
        }
        /// <inheritdoc />
        public void Dispose()
        {
            _cts?.Dispose();
        }
    }

    public struct PauseToken
    {
        private readonly PauseTokenSource _source;
        private static async Task ThrowIfCancellationRequested(PauseTokenSource pts)
        {
            pts.ThrowIfCancellationRequested();
        }
        internal PauseToken(PauseTokenSource source)
        {
            _source = source;
        }

        public bool IsPaused => _source?.IsPaused ?? false;
        /// <summary>
        /// Always check for cancellation after pause is complete
        /// </summary>
        /// <returns></returns>
        public Task WaitWhilePausedAsync()
        {
            _source.ThrowIfCancellationRequested();
            return IsPaused ? _source.WaitWhilePausedAsync()
                .ContinueWith((task, o) => ThrowIfCancellationRequested(o as PauseTokenSource), _source)  //TODO: will this work?
                : PauseTokenSource.CompletedTask;
        }
        public CancellationToken CancellationToken => _source.CToken;

        public void Register(Action onPausedAction)
        {
            _source.Register(onPausedAction);
        }
    }
}
