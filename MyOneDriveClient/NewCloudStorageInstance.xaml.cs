using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using LocalCloudStorage;
using LocalCloudStorage.Data;
using LocalCloudStorage.ViewModel;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for NewCloudStorageInstance.xaml
    /// </summary>
    public partial class NewCloudStorageInstance : Window
    {
        private class ViewModel : ViewModelBase
        {
            public ViewModel(CloudStorageInstanceBasicViewModel instance,
                RemoteFileStoreConnectionFactoriesViewModel factories)
            {
                Instance = instance;
                Factories = factories;
            }
            public CloudStorageInstanceBasicViewModel Instance { get; }
            public RemoteFileStoreConnectionFactoriesViewModel Factories { get; }
        }

        private ViewModel _vm;
        public NewCloudStorageInstance(RemoteFileStoreConnectionFactoriesViewModel factories)
        {
            Data = new CloudStorageInstanceData();
            DataContext = _vm = new ViewModel(new CloudStorageInstanceBasicViewModel(Data), factories);
            InitializeComponent();
        }

        public CloudStorageInstanceData Data { get; }

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            //TODO: this is the REALLY crappy one
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = _vm.Instance.LocalFileStorePath;
                dlg.ShowNewFolderButton = true;
                var result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _vm.Instance.LocalFileStorePath = dlg.SelectedPath;
                    //BindingExpression be = GetBindingExpression(TextProperty);
                    //if (be != null)
                    //    be.UpdateSource();
                }
            }
        }
        private void OKButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
