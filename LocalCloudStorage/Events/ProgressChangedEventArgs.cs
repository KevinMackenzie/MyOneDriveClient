using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class ProgressChangedEventArgs
    {
        public ProgressChangedEventArgs(double amountComplete, double total)
        {
            Complete = amountComplete;
            Total = total;
        }

        public double Complete { get; }
        public double Total { get; }
        public double Percent => Total / Complete;
    }
}
