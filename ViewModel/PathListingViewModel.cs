using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace LocalCloudStorage.ViewModel
{
    public class PathListingViewModel : ViewModelBase
    {
        private bool _selected;

        public PathListingViewModel(bool selected, StaticItemHandle itemData)
        {
            _selected = selected;
            ItemData = itemData;
        }


        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged();
            }
        }
        public StaticItemHandle ItemData { get; }
    }
}
