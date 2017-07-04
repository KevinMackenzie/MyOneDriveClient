using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient.OneDrive
{
    public class OneDriveRemoteFileStoreConnection : IRemoteFileStoreConnection
    {
        private static string[] _scopes = new string[] { "files.readwrite" };
        //Below is the clientId of your app registration. 
        //You have to replace the below with the Application Id for your app registration
        private static string ClientId = "f9dc0bbd-fc1b-4cf4-ac6c-e2a41a05d583";//"0b8b0665-bc13-4fdc-bd72-e0227b9fc011";
        private static string _onedriveEndpoint = "https://graph.microsoft.com/v1.0/me/drive";

        private AuthenticationResult _authResult = null;

        private PublicClientApplication _clientApp;
        public PublicClientApplication PublicClientApp { get { return _clientApp; } }


        public OneDriveRemoteFileStoreConnection()
        {
            _clientApp = new PublicClientApplication(ClientId, "https://login.microsoftonline.com/common", TokenCacheHelper.GetUserCache());
        }

        public static string AssembleUrl(string path)
        {
            return $"{_onedriveEndpoint}/root:{path}";
        }

        #region IRemoteFileStoreConnection
        public async Task<FileData> DownloadFile(string remotePath)
        {
            FileData ret = new FileData();

            //get the download URL
            try
            {
                ret.Metadata = await XHttpContentAsStringWithToken(AssembleUrl(remotePath), _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
            }
            catch(Exception e)
            {
                ret.Metadata = e.ToString();
                return ret;
            }

            var data = (JObject)JsonConvert.DeserializeObject(ret.Metadata);
            string downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();

            //now download the text
            try
            {
                ret.Data = await XHttpContentWithToken(downloadUrl, _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
            }
            catch(Exception e)
            {
                ret.Metadata = e.ToString();
                ret.Data = null;
            }
            return ret;
        }

        /// <summary>
        /// Gets the file store connection authenticated
        /// </summary>
        /// <returns></returns>
        public async Task PromptUserLogin()
        {
            string resultText = string.Empty;

            try
            {
                _authResult = await PublicClientApp.AcquireTokenSilentAsync(_scopes, PublicClientApp.Users.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    _authResult = await PublicClientApp.AcquireTokenAsync(_scopes);
                }
                catch (MsalException msalex)
                {
                    resultText = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                resultText = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
                return;
            }
            
            if (_authResult != null)
            {
                //resultText = await GetHttpContentWithToken(_onedriveEndpoint, _authResult.AccessToken);
                //DisplayBasicTokenInfo(authResult);
                //this.SignOutButton.Visibility = Visibility.Visible;
            }
        }

        public void LogUserOut()
        {
            if (PublicClientApp.Users.Any())
            {
                PublicClientApp.Remove(PublicClientApp.Users.FirstOrDefault());
            }
        }

        /// <summary>
        /// A test method for making HTTP GET requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<byte[]> XHttpContentWithToken(string url, string token, System.Net.Http.HttpMethod verb)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(verb, url);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsByteArrayAsync();
                return content;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<string> XHttpContentAsStringWithToken(string url, string token, System.Net.Http.HttpMethod verb)
        {
            return Encoding.UTF8.GetString(await XHttpContentWithToken(url, token, verb));
        }

        public async Task UploadFile(string remotePath, byte[] data)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
