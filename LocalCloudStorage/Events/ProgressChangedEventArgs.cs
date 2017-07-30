using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ProgressChangedEventArgs
    {
        public ProgressChangedEventArgs(long amountComplete, long total)
        {
            Complete = amountComplete;
            Total = total;
        }

        public long Complete { get; }
        public long Total { get; }
        public double Progress => (double)Complete / Total;
    }
}
