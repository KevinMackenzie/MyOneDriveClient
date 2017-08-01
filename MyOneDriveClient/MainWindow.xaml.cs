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
using MyOneDriveClient.Annotations;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Set the API Endpoint to Graph 'me' endpoint
        string graphAPIEndpoint = "https://graph.microsoft.com/v1.0/me";

        //Set the scope for API call to user.read
        string[] scopes = new string[] { "user.read" };

        public RequestsViewModel Requests { get; }

        public MainWindow()
        {
            InitializeComponent();

            //TODO: where should this construction happen?
            DataContext = Requests = new RequestsViewModel(App.LocalInterface, App.RemoteInterface);

            /*LocalActiveRequests.ItemsSource = Requests.LocalRequests.ActiveRequests;
            LocalUserAwaitRequests.ItemsSource = Requests.LocalRequests.AwaitUserRequests;
            LocalFailedRequests.ItemsSource = Requests.LocalRequests.FailedRequests;

            RemoteActiveRequests.ItemsSource = Requests.RemoteRequests.ActiveRequests;
            RemoteUserAwaitRequests.ItemsSource = Requests.RemoteRequests.AwaitUserRequests;
            RemoteFailedRequests.ItemsSource = Requests.RemoteRequests.FailedRequests;*/
           
            Debug.Listeners.Add(new DebugListener(this));

            Debug.WriteLine("Debug initialized");
        }

        /// <summary>
        /// Call AcquireTokenAsync - to acquire a token requiring user to sign-in
        /// </summary>
        /*private async void CallGraphButton_Click(object sender, RoutedEventArgs e)
        {
            /*AuthenticationResult authResult = null;
            ResultText.Text = string.Empty;
            TokenInfoText.Text = string.Empty;

            try
            {
                authResult = await App.PublicClientApp.AcquireTokenSilentAsync(scopes, App.PublicClientApp.Users.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    authResult = await App.PublicClientApp.AcquireTokenAsync(scopes);
                }
                catch (MsalException msalex)
                {
                    ResultText.Text = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
                return;
            }

            if (authResult != null)
            {
                ResultText.Text = await GetHttpContentWithToken(graphAPIEndpoint, authResult.AccessToken);
                DisplayBasicTokenInfo(authResult);
                this.SignOutButton.Visibility = Visibility.Visible;
            }
        }*/

        /// <summary>
        /// Perform an HTTP GET request to a URL using an HTTP Authorization header
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="token">The token</param>
        /// <returns>String containing the results of the GET operation</returns>
        /*public async Task<string> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }*/

        /// <summary>
        /// Sign out the current user
        /// </summary>
        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.OneDriveConnection.LogUserOut();
                this.SignOutButton.Visibility = Visibility.Collapsed;
            }
            catch (MsalException ex)
            {
            }
        }

        DeltaPage _deltaPage = null;
        private async void GetDeltasButton_Click(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLoginAsync();
            await App.FileStore.ApplyRemoteChangesAsync();
        }

        private async void ScanForChangesButton_OnClick(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLoginAsync();
            await App.FileStore.ApplyLocalChangesAsync();
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

        private async void StartRemoteQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLoginAsync();
            //App.RemoteInterface.StartRequestProcessing();
        }
        private async void StopRemoteQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            //await App.RemoteInterface.StopRequestProcessingAsync();
        }
        private void StartLocalQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            //App.LocalInterface.StartRequestProcessing();
        }
        private async void StopLocalQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            //await App.LocalInterface.StopRequestProcessingAsync();
        }
        private async void SaveMetadataButton_OnClick(object sender, RoutedEventArgs e)
        {
            await App.FileStore.SaveMetadataAsync();
        }

        private async void KeepLocal_OnClick(object sender, RoutedEventArgs e)
        {
            var request = (sender as Button)?.DataContext as AwaitUserRequestViewModel;
            if (request != null)
            {
                if (LocalActiveRequests.Items.Contains(request))
                {
                    //local request
                    await App.FileStore.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.FileStore.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepLocal);
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
                    await App.FileStore.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.FileStore.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepRemote);
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
                    await App.FileStore.ResolveLocalConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    await App.FileStore.ResolveRemoteConflictAsync(request.InnerRequest.RequestId, FileStoreInterface.ConflictResolutions.KeepBoth);
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
                    App.LocalInterface.CancelRequest(request.InnerRequest.RequestId);
                }
                else if (RemoteActiveRequests.Items.Contains(request))
                {
                    //remote request
                    App.RemoteInterface.CancelRequest(request.InnerRequest.RequestId);
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
    }
}
