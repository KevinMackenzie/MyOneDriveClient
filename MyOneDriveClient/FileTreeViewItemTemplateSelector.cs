using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LocalCloudStorage;
using LocalCloudStorage.ViewModel;

namespace MyOneDriveClient
{
    class FileTreeViewItemTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var parsedItem = item as TSafeObservableTreeItem<PathListingViewModel>;
            var element = container as FrameworkElement;
            if (element == null || parsedItem == null) return null;
            
            if (parsedItem.Value.ItemData.IsFolder)
            {
                //folder
                return element.FindResource("FolderItem") as DataTemplate;
            }
            else
            {
                //file
                return element.FindResource("FileItem") as DataTemplate;
            }
        }
    }
}
