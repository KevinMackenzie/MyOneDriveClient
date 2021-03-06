﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Model
{
    public class LocalCloudStorageData
    {
        /// <summary>
        /// The maximum upload rate.  If 0, then no limit
        /// </summary>
        public int MaxUploadRate { get; set; }
        /// <summary>
        /// The maximum download rate.  If 0, then no limit
        /// </summary>
        public int MaxDownloadRate { get; set; }
        /// <summary>
        /// Whether to start the application on startup
        /// </summary>
        public bool StartOnBoot { get; set; } = true;
        /// <summary>
        /// The cloud storage instances
        /// </summary>
        public List<CloudStorageInstanceData> CloudStorageInstances { get; set; } =
            new List<CloudStorageInstanceData>();
    }
}
