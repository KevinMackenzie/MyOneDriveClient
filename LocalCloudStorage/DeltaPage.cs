using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class DeltaPage : List<IRemoteItemUpdate>
    {
        /// <summary>
        /// Link to the next page of deltas.  Null if no more deltas
        /// </summary>
        public string NextPage { get; }
        /// <summary>
        /// Link to use when checking for deltas again.  Null if <see cref="NextPage"/> is not null
        /// </summary>
        public string DeltaLink { get; }

        public DeltaPage(string nextPage, string deltaLink)
        {
            NextPage = nextPage;
            DeltaLink = deltaLink;
        }
    }
}
