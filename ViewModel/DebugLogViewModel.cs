using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.ViewModel
{
    public class DebugLogViewModel : ViewModelBase
    {
        #region Private Fields
        private string _debugContents = "";
        #endregion

        public DebugLogViewModel()
        {
            Debug.AutoFlush = true;
            Debug.Listeners.Add(new DebugListener(this));
            Debug.WriteLine("Debug initialized");
        }


        #region Public Properties
        public string DebugContents
        {
            get
            {
                string ret;
                lock (_debugContents)
                {
                    ret = _debugContents;
                }
                return ret;
            }
            private set
            {
                lock (_debugContents)
                {
                    _debugContents = value;
                }
                OnPropertyChanged();
            }
        }
        #endregion

        private class DebugListener : TraceListener
        {
            private readonly DebugLogViewModel _vm;
            public DebugListener(DebugLogViewModel vm)
            {
                _vm = vm;
            }
            /// <inheritdoc />
            public override void Write(string message)
            {
                _vm.DebugContents += message;
            }
            /// <inheritdoc />
            public override void WriteLine(string message)
            {
                _vm.DebugContents += $"{message}{Environment.NewLine}";
            }
        }
    }
}
