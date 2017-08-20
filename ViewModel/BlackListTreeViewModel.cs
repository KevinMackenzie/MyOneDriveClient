using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace LocalCloudStorage.ViewModel
{
    public class BlackListTreeViewModel : TSafeObservableTreeItem<PathListingViewModel>
    {
        /// <inheritdoc />
        public BlackListTreeViewModel(PathListingViewModel value) : base(value)
        {
        }
    }
}
