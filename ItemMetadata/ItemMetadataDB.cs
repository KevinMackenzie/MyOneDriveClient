using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LocalCloudStorage.ItemMetadata
{
    public class ItemMetadataDb : DbContext
    {
        public DbSet<ItemMetadata> ItemMetadata { get; set; }
    }
}
