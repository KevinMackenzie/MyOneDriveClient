using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.ViewModel
{
    //TODO: how to have a "dot" to indicate that not all children are being synced?
    public class BlackListViewModel : ViewModelBase
    {
        public BlackListViewModel(IEnumerable<StaticItemHandle> pathList, ICollection<string> blackList)
        {
            BlackList = ConstructTree(pathList, blackList);
        }

        private static IEnumerable<StaticItemHandle> SortItemHandleList(IEnumerable<StaticItemHandle> itemhandleList)
        {
            var ret = new List<StaticItemHandle>(itemhandleList);
            //ret.Sort();
            return ret;
        }
        private static void AddPathToTree(TSafeObservableTreeItem<PathListingViewModel> tree, StaticItemHandle itemData, bool selected)
        {
            var path = itemData.Path;
            while (true)
            {
                var pathParts = path.Split(new char[] {'/'}, 2, StringSplitOptions.RemoveEmptyEntries);

                if (pathParts.Length == 0)
                    return;
                if (pathParts.Length == 1)
                {
                    tree.Children.Add(new TSafeObservableTreeItem<PathListingViewModel>(new PathListingViewModel(selected, itemData)));
                    return;
                }

                var nextFolderName = pathParts[0];
                var children = (from child in tree.Children where child.Value.ItemData.Name == nextFolderName select child).ToList();

                if (!children.Any())
                {
                    throw new ArgumentException("Incomplete Directory Tree!", nameof(tree));
                    //var nextTree = new TSafeObservableTreeItem<PathListingViewModel>(new PathListingViewModel(selected, nextFolderName));
                    //tree.Children.Add(nextTree);
                    //tree = nextTree;
                }
                else
                {
                    tree = children[0];
                }
                path = pathParts[1];
            }
        }
        private static void GetPathListFromTree(TSafeObservableTreeItem<PathListingViewModel> tree, string cumulativePath, ICollection<string> list)
        {
            var childPath = $"{cumulativePath}/{tree.Value}";
            list.Add(childPath);
            foreach (var child in tree.Children)
            {
                GetPathListFromTree(child, childPath, list);
            }
        }
        private static BlackListTreeViewModel ConstructTree(IEnumerable<StaticItemHandle> pathList, ICollection<string> blackList)
        {
            var ret = new BlackListTreeViewModel(new PathListingViewModel(!blackList.Any(),
                new StaticItemHandle(true, "", "", 0, DateTime.UtcNow)));
            var sortedPathList = SortItemHandleList(pathList);

            foreach (var path in sortedPathList)
            {
                AddPathToTree(ret, path, !blackList.Contains(path.Path));
            }
            return ret;
        }

        #region Public Properties
        public BlackListTreeViewModel BlackList { get; }
        #endregion

        #region Public Methods
        public ICollection<string> GetBlackList()
        {
            var ret = new List<string>();
            GetPathListFromTree(BlackList, "", ret);
            return ret;
        }
        #endregion
    }
}
