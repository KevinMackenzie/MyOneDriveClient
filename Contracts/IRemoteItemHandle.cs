using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface IRemoteItemHandle : IItemHandle
    {
        string Sha1 { get; }
        Task<HttpResult<Stream>> TryGetFileDataAsync(CancellationToken ct);
    }
}
