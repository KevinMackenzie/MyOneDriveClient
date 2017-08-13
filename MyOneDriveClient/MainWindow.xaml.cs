using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using LocalCloudStorage;
using LocalCloudStorage.AppCore;
using LocalCloudStorage.ViewModel;
using MyOneDriveClient.Annotations;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LocalCloudStorageApp _app;
        public MainWindow()
        {
            InitializeComponent();

            _app = App.AppInstance;
            DataContext = _app;

            //_app.LocalCloudStorage.PropertyChanged += LocalCloudStorageOnPropertyChanged;
            //LocalCloudStorageOnPropertyChanged(null, new PropertyChangedEventArgs(nameof(LocalCloudStorageViewModel.SelectedInstance)));

            /*LocalActiveRequests.ItemsSource = Requests.LocalRequests.ActiveRequests;
            LocalUserAwaitRequests.ItemsSource = Requests.LocalRequests.AwaitUserRequests;
            LocalFailedRequests.ItemsSource = Requests.LocalRequests.FailedRequests;

            RemoteActiveRequests.ItemsSource = Requests.RemoteRequests.ActiveRequests;
            RemoteUserAwaitRequests.ItemsSource = Requests.RemoteRequests.AwaitUserRequests;
            RemoteFailedRequests.ItemsSource = Requests.RemoteRequests.FailedRequests;*/
        }
        /*private void LocalCloudStorageOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == nameof(LocalCloudStorageViewModel.SelectedInstance))
            {
                var selectedInstance = _app.LocalCloudStorage.SelectedInstance;

                //what was the previous selection?
                var oldSelected = InstancesListBox.SelectedItem as CloudStorageInstanceViewModel;
                if (oldSelected != null)
                {
                    //remote our event handlers from that
                    oldSelected.Requests.LocalRequests.ActiveRequests.CollectionChanged -= LocalActiveRequestsOnCollectionChanged;
                    oldSelected.Requests.RemoteRequests.ActiveRequests.CollectionChanged -= RemoteActiveRequestsOnCollectionChanged;
                }

                //set the selection to the currently selected instance
                InstancesListBox.SelectedItem = selectedInstance;

                //set the active requests item sources to the currently selected item's requests
                LocalActiveRequests.ItemsSource = selectedInstance.Requests.LocalRequests.ActiveRequests;
                RemoteActiveRequests.ItemsSource = selectedInstance.Requests.RemoteRequests.ActiveRequests;

                //then register event handlers to ensure the list boxes are up to date
                selectedInstance.Requests.LocalRequests.ActiveRequests.CollectionChanged += LocalActiveRequestsOnCollectionChanged;
                selectedInstance.Requests.RemoteRequests.ActiveRequests.CollectionChanged += RemoteActiveRequestsOnCollectionChanged;
            }
        }
        private void LocalActiveRequestsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            LocalActiveRequests.Dispatcher.Invoke(() => LocalActiveRequests.Items.Refresh());
        }
        private void RemoteActiveRequestsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            RemoteActiveRequests.Dispatcher.Invoke(() => RemoteActiveRequests.Items.Refresh());
        }*/

        /// <inheritdoc />
        protected override void OnClosed(EventArgs e)
        {
            _app?.Dispose();
            base.OnClosed(e);
        }


        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            DequeueDebug();
        }
        
        private void DequeueDebug()
        {
            DebugBox.Text = _app.DebugLog.DebugContents;
            DebugBox.ScrollToEnd();
        }
        

        private async void KeepLocal_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AwaitUserRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
                }
                else
                {
                    Debug.WriteLine("User-Prompting view model does not exist in local or remote lists");
                }
            }
            else
            {
                Debug.WriteLine($"Sender DataContext is not of type {nameof(AwaitUserRequestViewModel)}");
            }
        }
        private async void KeepRemote_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AwaitUserRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
                }
                else
                {
                    Debug.WriteLine("User-Prompting view model does not exist in local or remote lists");
                }
            }
            else
            {
                Debug.WriteLine($"Sender DataContext is not of type {nameof(AwaitUserRequestViewModel)}");
            }
        }
        private async void KeepBoth_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AwaitUserRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await _app.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
                }
                else
                {
                    Debug.WriteLine("User-Prompting view model does not exist in local or remote lists");
                }
            }
            else
            {
                Debug.WriteLine($"Sender DataContext is not of type {nameof(AwaitUserRequestViewModel)}");
            }
        }

        private async void TryAgain_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as CloseAppRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request TODO
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request TODO
                }
                else
                {
                    Debug.WriteLine("User-Prompting view model does not exist in local or remote lists");
                }
            }
            else
            {
                Debug.WriteLine($"Sender DataContext is not of type {nameof(CloseAppRequestViewModel)}");
            }
        }

        private void AcknowledgeFailure_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AcknowledgeErrorRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    _app.LocalCloudStorage.SelectedInstance.CancelLocalRequest(request.InnerRequest.RequestId);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    _app.LocalCloudStorage.SelectedInstance.CancelRemoteRequest(request.InnerRequest.RequestId);
                }
                else
                {
                    Debug.WriteLine("User-Prompting view model does not exist in local or remote lists");
                }
            }
            else
            {
                Debug.WriteLine($"Sender DataContext is not of type {nameof(AcknowledgeErrorRequestViewModel)}");
            }
        }
        private async void GenMetadataButton_OnClick(object sender, RoutedEventArgs e)
        {
            //await App.FileStore.GenerateLocalMetadataAsync();
        }
        private async void NewInstanceButton_OnClick(object sender, RoutedEventArgs e)
        {
            var popup = new NewCloudStorageInstance(_app.RemoteConnectionFactories);
            var result = popup.ShowDialog() ?? false;
            if (result)
            {
                _app.LocalCloudStorage.AddCloudStorageInstance(popup.Data);
                await App.AppInstance.SaveInstances();
            }
        }
    }
}
