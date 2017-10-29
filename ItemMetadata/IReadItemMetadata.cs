using System;
using System.Collections.Generic;
using System.Text;

namespace LocalCloudStorage.ItemMetadata
{
    public interface IReadItemMetadata
    {
        bool IsFolder { get; }
        string Path { get; }
        string Name { get; }
        long Size { get; }
        string Id { get; }
        string ParentId { get; }

        bool TryGetProperty(string property, out string value);
        bool TryGetProperty(string property, out int value);
        bool TryGetProperty(string property, out double value);
    }
}
