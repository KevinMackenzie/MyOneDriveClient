using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
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


        public MainWindow()
        {
            InitializeComponent();
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
                this.DownloadFileButton.Visibility = Visibility.Visible;
                this.SignOutButton.Visibility = Visibility.Collapsed;
            }
            catch (MsalException ex)
            {
                MetadataText.Text = ex.ToString();
            }
        }

        /// <summary>
        /// Display file metadata
        /// </summary>
        private void DisplayFileMetadata(string metadata)
        {
            //TODO: make this prettier
            MetadataText.Text = metadata;
            //if (authResult != null)
            //{
                //TokenInfoText.Text += $"Name: {authResult.User.Name}" + Environment.NewLine;
                //TokenInfoText.Text += $"Username: {authResult.User.DisplayableId}" + Environment.NewLine;
                //TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
                //TokenInfoText.Text += $"Access Token: {authResult.AccessToken}" + Environment.NewLine;
            //}
        }

        private async void DownloadFileButton_Click(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLogin();
            FileData data = await App.OneDriveConnection.DownloadFile(RemoteFilePath.Text);

            DisplayFileMetadata(data.Metadata);
            ContentsText.Text = Encoding.UTF8.GetString(data.Data);

            SignOutButton.Visibility = Visibility.Visible;
        }

        private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLogin();

            byte[] data = Encoding.UTF8.GetBytes(ContentsText.Text);

            try
            {
                await App.OneDriveConnection.UploadFile(RemoteFilePath.Text, data);
            }
            catch(Exception ex)
            {
                MetadataText.Text = e.ToString();
            }
        }

        private async void ListDirButton_Click(object sender, RoutedEventArgs e)
        {
            await App.OneDriveConnection.PromptUserLogin();
            
            //try
            //{
                var files = await App.OneDriveConnection.EnumerateFilePaths(RemoteFilePath.Text);

                ContentsText.Text = "";
                foreach (var file in files)
                {
                    ContentsText.Text += $"{file}{Environment.NewLine}";
                }
            //}
            //catch(Exception ex)
            //{
            //    MetadataText.Text = ex.ToString();
            //}
        }
    }
}
