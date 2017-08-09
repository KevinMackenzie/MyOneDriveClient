using System.IO;

namespace LocalCloudStorage
{
    public interface ILocalItemHandle : IItemHandle
    {
        /// <summary>
        /// Retrives a writable stream for the given item handle
        /// </summary>
        /// <returns>a write-only stream to the file data, or null if item is being blocked</returns>
        Stream GetWritableStream();
        bool Exists();
    }
}
