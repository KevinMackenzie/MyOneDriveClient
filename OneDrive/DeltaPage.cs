using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.OneDrive
{
    public class DeltaPage : List<IRemoteItemUpdate>, IDeltaList
    {
        /// <summary>
        /// Link to the next page of deltas.  Null if no more deltas
        /// </summary>
        public string NextPage { get; }
        /// <summary>
        /// Link to use when checking for deltas again.  Null if <see cref="NextPage"/> is not null
        /// </summary>
        public string NextRequestData { get; }

        public DeltaPage(string nextPage, string deltaLink)
        {
            NextPage = nextPage;
            NextRequestData = deltaLink;
        }
    }
}
