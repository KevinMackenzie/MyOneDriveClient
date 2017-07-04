using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private async Task<List<string>> RecurseFoldersAsync(string remotePath)
        {
            string url = "";

            //check if it's the root
            if(remotePath == "/" || remotePath == "")
            {
                url = $"{_onedriveEndpoint}/root/children";
            }
            else
            {
                url = $"{_onedriveEndpoint}/root:/{remotePath}:/children";
            }

            List<string> files = new List<string>();

            //query the elements in the folder
            string json = await XHttpContentAsStringWithToken(url, _authResult.AccessToken, HttpMethod.Get);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            //get files
            files.AddRange((from value in obj["value"]
                                                where (value.Value<object>("folder") == null)
                                                select $"{remotePath}{(string)value["name"]}").ToList());

            //get folders
            IEnumerable<string> folders = (from value in obj["value"]
                                           where (value.Value<object>("folder") != null)
                                           select (string)value["name"]).ToList();

            //recurse the folders...
            List<Task<List<string>>> recurseTasks = new List<Task<List<string>>>();
            foreach(var folder in folders)
            {
                recurseTasks.Add(RecurseFoldersAsync($"{remotePath}{folder}/"));
            }

            //... and wait for them to finish
            foreach(var task in recurseTasks)
            {
                await task;
            }

            //then add the files to our files list
            //var taskResults = (from task in recurseTasks select (from str in task.Result select $"{remotePath}/{str}").ToList()).ToList();
            foreach(var task in recurseTasks)
            {
                files.AddRange(task.Result);
            }

            return files;
        }
        public async Task<IEnumerable<string>> EnumerateFilePaths(string remotePath)
        {
            return await RecurseFoldersAsync(remotePath);
        }

        public async Task<string> GetFileMetadata(string remotePath)
        {
            return await XHttpContentAsStringWithToken(AssembleUrl(remotePath), _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
        }

        public async Task<FileData> DownloadFile(string remotePath)
        {
            FileData ret = new FileData();
            string downloadUrl = "";

            //get the download URL
            try
            {
                ret.Metadata = await GetFileMetadata(remotePath);
                var data = (JObject)JsonConvert.DeserializeObject(ret.Metadata);
                downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();
            }
            catch(Exception ex)
            {
                ret.Metadata = ex.ToString();
                return ret;
            }

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

        public async Task UploadFile(string remotePath, byte[] data)
        {
            //TODO: add support for resumable item uploads: https://dev.onedrive.com/items/upload_large_files.htm
            await XHttpContentWithToken($"{AssembleUrl(remotePath)}:/content", _authResult.AccessToken, HttpMethod.Put, data);
        }
        #endregion

        /// <summary>
        /// A test method for making HTTP GET requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<byte[]> XHttpContentWithToken(string url, string token, System.Net.Http.HttpMethod verb, byte[] body = null)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(verb, url);
                if(body != null)
                {
                    request.Content = new ByteArrayContent(body);
                }

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
        public async Task<string> XHttpContentAsStringWithToken(string url, string token, System.Net.Http.HttpMethod verb, byte[] body = null)
        {
            return Encoding.UTF8.GetString(await XHttpContentWithToken(url, token, verb, body));
        }
    }
}
