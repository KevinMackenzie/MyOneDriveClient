using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace LocalCloudStorage
{
    public class SingleTimer
    {
        private TimeSpan _duration;
        private DateTime _startTime;
        private Task _delayTask;
        private bool _timerStarted = false;

        private CancellationTokenSource _cts;

        public void Start(TimeSpan duration)
        {
            if (_timerStarted)
            {
                //trying to start an already started timer
                Stop();
                Start(duration);
                _timerStarted = false;//this should be a guarantee
                return;
            }

            _startTime = DateTime.UtcNow;
            _duration = duration;
            _cts = new CancellationTokenSource();

            _delayTask = Utils.DelayNoThrow(duration, TimeSpan.FromSeconds(0.5), _cts.Token)
                .ContinueWith(task => _timerStarted = false);
        }

        public TimeSpan Remaining => _timerStarted ? _duration - (DateTime.UtcNow - _startTime) : TimeSpan.Zero;

        public void Stop()
        {
            _cts.Cancel();
            _timerStarted = false;
        }
    }
}
