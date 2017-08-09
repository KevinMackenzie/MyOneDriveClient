using System;
using System.ComponentModel.Composition;

namespace LocalCloudStorage
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class RemoteFileStoreConnectionFactoryMetadataAttribute : Attribute
    {
        public string ServiceName { get; set; }
    }
}
