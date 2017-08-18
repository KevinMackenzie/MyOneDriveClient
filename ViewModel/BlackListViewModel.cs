using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.ViewModel
{
    public class BlackListViewModel : ViewModelBase
    {
        private TSafeObservableCollection<>
        public BlackListViewModel(IEnumerable<string> blackList)
        {
            BlackList = blackList;
        }

        public ReadOnlyObservableCollection<string> BlackList { get; }


        public void Add
    }
}
