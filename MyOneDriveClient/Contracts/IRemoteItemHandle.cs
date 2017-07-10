using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public interface IRemoteItemHandle : IItemHandle
    {
        //there should be NO guarantee of what this is, so it should probably not even exist....
        //JObject Metadata { get; }
        string Id { get; }
    }
}
