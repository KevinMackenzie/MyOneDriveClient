using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <summary>
    /// 
    /// </summary>
    public class LocalCloudStorage
    {
        #region Singleton Implementation
        private static LocalCloudStorage _instance;
        public LocalCloudStorage Instance
        {
            get
            {
                if(_instance == null)
                    _instance = new LocalCloudStorage();
                return _instance;
            }
        }
        #endregion

        #region Application-Wide Settings
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
        public bool StartOnBoot { get; set; }
        #endregion

        //TODO: implement dependency-injection for cloud storage services
    }
}
