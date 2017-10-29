using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace Contracts
{
    public interface IRemoteDeltaConverter
    {
        Task<bool> TrySubmitDeltaAsync(IConnection connection, IItemDelta itemDelta);
    }
}
