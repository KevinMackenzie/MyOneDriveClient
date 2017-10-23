using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    public interface IItemHandle
    {
        bool IsFolder { get; }
        string Path { get; }
        string Name { get; }
        long Size { get; }
        string Id { get; }
        string ParentId { get; }

        Task<string> GetSha1HashAsync(CancellationToken ct);
        Task<string> GetSha1HashAsync();

        DateTime LastModified { get; }

        /// <summary>
        /// Gets a stream to this item's data, null if failed or <see cref="IsFolder"/> is true
        /// </summary>
        /// <returns></returns>
        Task<Stream> GetFileDataAsync(CancellationToken ct);
    }
}
