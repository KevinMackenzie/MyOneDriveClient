using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LocalCloudStorage.ViewModel;
using System.Drawing;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for BlackListEditView.xaml
    /// </summary>
    public partial class BlackListEditView : Window
    {
        public BlackListEditView(BlackListViewModel dataContext)
        {
            InitializeComponent();

            DataContext = dataContext;
        }

        private void OK_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
