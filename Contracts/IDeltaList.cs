using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface IDeltaList : IList<IRemoteItemUpdate>
    {
        string NextRequestData { get; }
    }
}
