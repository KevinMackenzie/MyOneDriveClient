using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using LocalCloudStorage.ViewModel;
using MyOneDriveClient.Annotations;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            

            /*LocalActiveRequests.ItemsSource = Requests.LocalRequests.ActiveRequests;
            LocalUserAwaitRequests.ItemsSource = Requests.LocalRequests.AwaitUserRequests;
            LocalFailedRequests.ItemsSource = Requests.LocalRequests.FailedRequests;

            RemoteActiveRequests.ItemsSource = Requests.RemoteRequests.ActiveRequests;
            RemoteUserAwaitRequests.ItemsSource = Requests.RemoteRequests.AwaitUserRequests;
            RemoteFailedRequests.ItemsSource = Requests.RemoteRequests.FailedRequests;*/
           
            Debug.Listeners.Add(new DebugListener(this));

            Debug.WriteLine("Debug initialized");
        }
        

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            DequeueDebug();
        }

        private ConcurrentQueue<string> debugQueue = new ConcurrentQueue<string>();

        private void DequeueDebug()
        {
            while (debugQueue.TryDequeue(out string result))
            {
                DebugBox.Text += result;
                DebugBox.ScrollToEnd();
            }
        }

        private class DebugListener : TraceListener
        {
            private MainWindow _window;
            public DebugListener(MainWindow window)
            {
                _window = window;
            }

            /// <inheritdoc />
            public override void Write(string message)
            {
                _window.debugQueue.Enqueue(message);
                //_window.DebugBox.Text += message;
                //_window.DebugBox.ScrollToEnd();
            }

            /// <inheritdoc />
            public override void WriteLine(string message)
            {
                _window.debugQueue.Enqueue($"{message}{Environment.NewLine}");
                //_window.DebugBox.Text += $"{message}{Environment.NewLine}";
                //_window.DebugBox.ScrollToEnd();
            }
        }
        

        private async void KeepLocal_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AwaitUserRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    await App.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
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
                    await App.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
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
                    await App.LocalCloudStorage.SelectedInstance.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.LocalCloudStorage.SelectedInstance.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
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
                    App.LocalCloudStorage.SelectedInstance.CancelLocalRequest(request.InnerRequest.RequestId);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    App.LocalCloudStorage.SelectedInstance.CancelRemoteRequest(request.InnerRequest.RequestId);
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
    }
}
