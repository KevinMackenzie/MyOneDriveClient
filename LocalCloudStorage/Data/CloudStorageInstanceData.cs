using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Data
{
    /// <summary>
    /// The settings and other data for CloudStorageInstance 
    /// </summary>
    public class CloudStorageInstanceData
    {
        /// <summary>
        /// Whether data uploaded to remote should be encrypted
        /// </summary>
        public bool Encrypted { get; set; }
        /// <summary>
        /// Whether to create file links for all blacklisted files
        /// </summary>
        public bool EnableFileLinks { get; set; }
        /// <summary>
        /// How frequently to check for remote deltas
        /// </summary>
        public TimeSpan RemoteDeltaFrequency { get; set; } = TimeSpan.FromMinutes(1);
    }
}
