using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Events
{
    public class ImportEventArgs : EventArgs
    {
        public string StatusMessage { get; set; }
    }
}
