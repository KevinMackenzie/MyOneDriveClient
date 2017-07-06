﻿using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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

        private string _deltaUrl = "";

        ///private GraphServiceClient _graphClient;
        
        public OneDriveRemoteFileStoreConnection()
        {
            _clientApp = new PublicClientApplication(ClientId, "https://login.microsoftonline.com/common", TokenCacheHelper.GetUserCache());

            try
            {
                /*_graphClient = new GraphServiceClient(
                            "https://graph.microsoft.com/v1.0",
                            new DelegateAuthenticationProvider(
                                async (requestMessage) =>
                                {
                                    await PromptUserLogin();
                                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", _authResult.AccessToken);
                                }));*/
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Could not create a graph client: " + ex.Message);
            }
        }

        #region IRemoteFileStoreConnection
        public void Initialize(string data)
        {
            //TODO: some checking that this is ACTUALLY a good link
            _deltaUrl = data;
        }

        public event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;

        public async Task<List<string>> RecurseFoldersAsync(string remotePath)
        {
            /*var children = await _graphClient.Me.Drive.Root.ItemWithPath(remotePath)?.Children?.Request()?.GetAsync();

            List<string> ret = new List<string>();

            if (children == null)
                return ret;

            var tasks = new List<Task<IEnumerable<string>>>();
            foreach(var child in children)
            {
                string itemName = $"{child.ParentReference.Path.Split(new char[]{':'}, 2).Last()}/{child.Name}";
                if (child.Folder != null)
                {
                    tasks.Add(RecurseFoldersAsync(itemName));
                }
                else
                {
                    ret.Add(itemName);
                }
            }

            foreach(var task in tasks)
            {
                ret.AddRange(await task);
            }

            return ret;*/

            List<string> files = new List<string>();

            //TODO: how do we deal with these?
            if (remotePath.Contains("#") || remotePath.Contains("%"))
                return files;

            string url = "";

            //check if it's the root
            if (remotePath == "/" || remotePath == "")
            {
                url = $"{_onedriveEndpoint}/root/children";
            }
            else
            {
                url = $"{_onedriveEndpoint}/root:/{remotePath}:/children";
            }
            
            //query the elements in the folder
            string json = await AuthenticatedHttpRequestAsString(url, _authResult.AccessToken, HttpMethod.Get);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            if (obj["value"] == null)
                return files;


            List<string> folders = new List<string>();
            foreach(var value in obj["value"])
            {
                if (value["folder"] == null)
                {
                    files.Add($"{remotePath}{(string)value["name"]}");
                }
                else
                {
                    folders.Add((string)value["name"]);
                }
            }

            //get files
            //files.AddRange((from value in obj["value"]
            //                where (value["folder"] == null)
            //                select $"{remotePath}{(string)value["name"]}").ToList());

            //get folders
            //IEnumerable<string> folders = (from value in obj["value"]
            //                               where (value["folder"] != null)
            //                               select (string)value["name"]).ToList();

            //recurse the folders ...
            var recurseTasks = new List<Task<List<string>>>();
            foreach(var folder in folders)
            {
                recurseTasks.Add(RecurseFoldersAsync($"{remotePath}{folder}/"));
            }

            //... and wait for them to finish
            foreach(var task in recurseTasks)
            {
                files.AddRange(await task);
            }

            return files;
        }
        public async Task<IEnumerable<string>> EnumerateFilePaths(string remotePath)
        {
            return await RecurseFoldersAsync(remotePath);
        }

        public async Task<IEnumerable<IRemoteFileHandle>> EnumerateFiles()
        {
            //this should be OK
            return (from update in (await EnumerateUpdatesInternal($"{_onedriveEndpoint}/root/delta")) select update.FileHandle);
        }

        public async Task<IEnumerable<IRemoteFileUpdate>> EnumerateUpdates()
        {
            string downloadUrl = "";
            if (_deltaUrl == "")
            {
                downloadUrl = $"{_onedriveEndpoint}/root/delta";
            }
            else
            {
                downloadUrl = _deltaUrl;
            }
            return await EnumerateUpdatesInternal(downloadUrl);
        }
        private async Task<IEnumerable<IRemoteFileUpdate>> EnumerateUpdatesInternal(string downloadUrl)
        {

            List<IRemoteFileUpdate> ret = new List<IRemoteFileUpdate>();

            JObject obj = null;
            do
            {
                string json = await AuthenticatedHttpRequestAsString(downloadUrl, _authResult.AccessToken, HttpMethod.Get);
                obj = (JObject)JsonConvert.DeserializeObject(json);

                //no values
                if (obj["value"] == null)
                    break;

                //set the download Url to the next link
                downloadUrl = (string)obj["@odata.nextLink"];

                //get the file handles
                foreach(var value in obj["value"])
                {
                    //TODO: folders are important deltas, because when folders are renamed, their descendents DON"T get the delta update
                    if (value["folder"] != null)
                        continue;

                    ret.Add(new RemoteFileUpdate(value["deleted"] != null, new OneDriveRemoteFileHandle(this, value["@microsoft.graph.downloadUrl"].ToString(), value.ToString())));
                }
            }
            while (obj["@odata.deltaLink"] == null && downloadUrl != null);

            //once we get all the updates, set the delta link equal to the one given
            _deltaUrl = (string)obj["@odata.deltaLink"] ?? "";

            //call the event handler (TODO: do we want to await this?)
            await OnUpdate.Invoke(this, new Events.RemoteFileStoreDataChanged(_deltaUrl));

            return ret;
        }

        public async Task<string> GetFileMetadataById(string id)
        {
            return await AuthenticatedHttpRequestAsString($"{_onedriveEndpoint}/items/{id}", _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
        }
        public async Task<string> GetFileMetadata(string remotePath)
        {
            return await AuthenticatedHttpRequestAsString($"{_onedriveEndpoint}/root:{remotePath}", _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
        }


        private async Task<Stream> DownloadFileWithLink(string downloadUrl)
        {
            return await AuthenticatedHttpRequest(downloadUrl, _authResult.AccessToken, System.Net.Http.HttpMethod.Get);
        }
        public async Task<IRemoteFileHandle> GetFileHandleById(string id)
        {
            string downloadUrl = "";
            string metadata = "";

            //get the download URL
            try
            {
                metadata = await GetFileMetadataById(id);
                var data = (JObject)JsonConvert.DeserializeObject(metadata);
                downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, downloadUrl, metadata);
        }
        public async Task<IRemoteFileHandle> GetFileHandle(string remotePath)
        {
            string downloadUrl = "";
            string metadata = "";

            //get the download URL
            try
            {
                metadata = await GetFileMetadata(remotePath);
                var data = (JObject)JsonConvert.DeserializeObject(metadata);
                downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, downloadUrl, metadata);
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

        private static int _4MB = 4 * 1024 * 1024;
        public async Task UploadFile(string remotePath, Stream data)
        {
            if(data.Length > _4MB) //if data > 4MB, then use chunked upload
            {

            }
            else //use regular upload
            {
                await AuthenticatedHttpRequest($"{_onedriveEndpoint}/root:{remotePath}:/content", _authResult.AccessToken, HttpMethod.Put, data);
            }
        }
        #endregion


        public class OneDriveRemoteFileHandle : IRemoteFileHandle
        {
            private string _downloadUrl;
            private string _metadata;
            private OneDriveRemoteFileStoreConnection _fileStore;

            public string Metadata { get => _metadata; }

            public OneDriveRemoteFileHandle(OneDriveRemoteFileStoreConnection fileStore, string downloadUrl, string metadata)
            {
                _fileStore = fileStore;
                _downloadUrl = downloadUrl;
                _metadata = metadata;
            }

            public async Task<Stream> DownloadFile()
            {
                return await _fileStore.DownloadFileWithLink(_downloadUrl);
            }
        }

        /// <summary>
        /// A test method for making HTTP GET requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<Stream> AuthenticatedHttpRequest(string url, string token, System.Net.Http.HttpMethod verb, Stream body = null)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(verb, url);
                if(body != null)
                {
                    request.Content = new StreamContent(body);
                }

                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                return await response.Content.ReadAsStreamAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<string> AuthenticatedHttpRequestAsString(string url, string token, System.Net.Http.HttpMethod verb, Stream body = null)
        {
            var stream = new StreamReader(await AuthenticatedHttpRequest(url, token, verb, body));
            return await stream.ReadToEndAsync();
        }
    }
}
