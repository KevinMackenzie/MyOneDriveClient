using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    public class ConvertedTaskCancelledException : TaskCanceledException
    {
        public ConvertedTaskCancelledException(Exception e) : base(
            $"Task Cancelled because {e.TargetSite} threw an exception, see log for details", e)
        {}
    }
}
