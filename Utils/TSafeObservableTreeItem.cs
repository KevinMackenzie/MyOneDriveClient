using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <summary>
    /// Represents a leaf on a tree of items
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class TSafeObservableTreeItem<TValue> : ViewModelBase
    {
        private TValue _value;
        private TSafeObservableCollection<TSafeObservableTreeItem<TValue>> _children;

        public TSafeObservableTreeItem(TValue value)
        {
            _value = value;
        }

        #region Public Properties
        public TSafeObservableCollection<TSafeObservableTreeItem<TValue>> Children
        {
            get
            {
                if(_children == null)
                    _children = new TSafeObservableCollection<TSafeObservableTreeItem<TValue>>();
                return _children;
            }
        }
        public bool HasChildren => _children != null && _children.Count != 0;
        public TValue Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
