using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface IRemoteItemHandle : IItemHandle
    {
        //there should be NO guarantee of what this is, so it should probably not even exist....
        //JObject Metadata { get; }
        string Id { get; }
        string ParentId { get; }
        string Sha1 { get; }
        Task<HttpResult<Stream>> TryGetFileDataAsync(CancellationToken ct);
    }
}
