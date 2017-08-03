using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
