using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Model
{
    /// <summary>
    /// The settings and other data for CloudStorageInstance 
    /// </summary>
    public class CloudStorageInstanceData
    {
        /// <summary>
        /// The path for the local file store
        /// </summary>
        public string LocalFileStorePath { get; set; } = "";
        /// <summary>
        /// The name of this instance
        /// </summary>
        /// <remarks>
        /// This is an identifying property of the instance data
        /// </remarks>
        public string InstanceName { get; set; } = "";
        /// <summary>
        /// Whether data uploaded to remote should be encrypted
        /// </summary>
        public bool Encrypted { get; set; }
        /// <summary>
        /// The remote service type that is being used
        /// </summary>
        public string ServiceName { get; set; } = "";
        /// <summary>
        /// Whether to create file links for all blacklisted files
        /// </summary>
        public bool EnableFileLinks { get; set; }
        /// <summary>
        /// How frequently to check for remote deltas
        /// </summary>
        public TimeSpan RemoteDeltaFrequency { get; set; } = TimeSpan.FromMinutes(1);
        /// <summary>
        /// Files/folders that should be excluded from syncing
        /// </summary>
        public IEnumerable<string> BlackList { get; set; } = new List<string>();
    }
}
